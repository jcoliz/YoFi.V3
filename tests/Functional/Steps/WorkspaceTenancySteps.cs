using Microsoft.Playwright;
using YoFi.V3.Tests.Functional.Pages;
using YoFi.V3.Tests.Functional.Helpers;
using YoFi.V3.Tests.Functional.Generated;

namespace YoFi.V3.Tests.Functional.Steps;

/// <summary>
/// Step definitions for Workspace Tenancy feature tests
/// </summary>
/// <remarks>
/// TEST PREFIX HANDLING PATTERN:
/// - DataTable values in feature files are user-readable (e.g., "alice", "Personal Budget")
/// - The __TEST__ prefix is added immediately upon entering step methods using AddTestPrefix()
/// - All internal storage (_userCredentials, _workspaceKeys, _objectStore) uses FULL names with prefix
/// - This ensures consistency with what the Test Controller API and UI expect
/// </remarks>
public abstract class WorkspaceTenancySteps : FunctionalTest
{
    #region Test Data Storage

    // Store user credentials created in Background (keys are FULL usernames with __TEST__ prefix)
    private readonly Dictionary<string, TestUserCredentials> _userCredentials = new();

    // Store workspace keys for later reference (keys are FULL workspace names as returned by API)
    private readonly Dictionary<string, Guid> _workspaceKeys = new();

    #endregion

    #region Helpers

    /// <summary>
    /// Adds the __TEST__ prefix to a name for test controller API calls
    /// </summary>
    private static string AddTestPrefix(string name) => $"__TEST__{name}";

    #endregion

    #region Steps: GIVEN

    /// <summary>
    /// Given: these users exist
    /// </summary>
    protected async Task GivenTheseUsersExist(DataTable usersTable)
    {
        // Clear existing users and workspaces to avoid conflicts
        await testControlClient.DeleteAllTestDataAsync();

        // Add __TEST__ prefix to usernames before API call
        var usernames = usersTable.ToSingleColumnList()
            .Select(AddTestPrefix)
            .ToList();

        var credentials = await testControlClient.CreateBulkUsersAsync(usernames);

        // Store with FULL username (what test controller returns)
        foreach (var cred in credentials)
        {
            _userCredentials[cred.Username] = cred;
        }
    }

    /// <summary>
    /// Given: I am logged in as {username}
    /// </summary>
    protected async Task GivenIAmLoggedInAs(string username)
    {
        // Add __TEST__ prefix to match stored credentials
        var fullUsername = AddTestPrefix(username);

        if (!_userCredentials.TryGetValue(fullUsername, out var cred))
        {
            throw new InvalidOperationException($"User '{fullUsername}' credentials not found. Ensure user was created in Background.");
        }

        await Page.GotoAsync("/login");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var loginPage = new LoginPage(Page);
        await loginPage.LoginAsync(cred.Username, cred.Password);

        // Wait for redirect after successful login
        await Page.WaitForURLAsync(url => !url.Contains("/login"), new() { Timeout = 10000 });

        // Store FULL username for future reference
        _objectStore.Add("LoggedInAs", fullUsername);
    }

    /// <summary>
    /// Given: I have access to these workspaces
    /// </summary>
    protected async Task GivenIHaveAccessToTheseWorkspaces(DataTable workspacesTable)
    {
        var currentUsername = GetCurrentTestUsername();

        // Add __TEST__ prefix to workspace names before API call
        var requests = workspacesTable.Select(row => new WorkspaceSetupRequest
        {
            Name = AddTestPrefix(row["Workspace Name"]),
            Description = $"Test workspace: {row["Workspace Name"]}",
            Role = row["My Role"]
        }).ToList();

        var results = await testControlClient.BulkWorkspaceSetupAsync(currentUsername, requests);

        // Store with FULL workspace names (what API returns)
        foreach (var result in results)
        {
            _workspaceKeys[result.Name] = result.Key;
        }
    }

    /// <summary>
    /// Given: I have a workspace called {workspaceName}
    /// </summary>
    protected async Task GivenIHaveAWorkspaceCalled(string workspaceName)
    {
        var currentUsername = GetCurrentTestUsername();
        var fullWorkspaceName = AddTestPrefix(workspaceName);

        var request = new WorkspaceCreateRequest
        {
            Name = fullWorkspaceName,
            Description = $"Test workspace: {workspaceName}",
            Role = "Owner"
        };

        TenantResultDto? result;
        try
        {
            result = await testControlClient.CreateWorkspaceForUserAsync(currentUsername, request);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating workspace '{fullWorkspaceName}': {ex.Message}");
            throw;
        }

        // Store with FULL workspace name (what API returns)
        _workspaceKeys[result!.Name] = result.Key;
    }

    /// <summary>
    /// Given: I own a workspace called {workspaceName}
    /// </summary>
    protected async Task GivenIAWorkspaceCalled(string workspaceName)
    {
        // Alias for GivenIHaveAWorkspaceCalled with explicit Owner role
        await GivenIHaveAWorkspaceCalled(workspaceName);
    }

    /// <summary>
    /// Given: I can edit data in {workspaceName}
    /// </summary>
    protected async Task GivenICanEditDataIn(string workspaceName)
    {
        var currentUsername = GetCurrentTestUsername();
        var fullWorkspaceName = AddTestPrefix(workspaceName);

        if (!_workspaceKeys.TryGetValue(fullWorkspaceName, out var workspaceKey))
        {
            throw new InvalidOperationException($"Workspace '{fullWorkspaceName}' key not found.");
        }

        var assignment = new UserRoleAssignment { Role = "Editor" };
        await testControlClient.AssignUserToWorkspaceAsync(currentUsername, workspaceKey, assignment);
    }

    /// <summary>
    /// Given: I can view data in {workspaceName}
    /// </summary>
    protected async Task GivenICanViewDataIn(string workspaceName)
    {
        var currentUsername = GetCurrentTestUsername();
        var fullWorkspaceName = AddTestPrefix(workspaceName);

        if (!_workspaceKeys.TryGetValue(fullWorkspaceName, out var workspaceKey))
        {
            throw new InvalidOperationException($"Workspace '{fullWorkspaceName}' key not found.");
        }

        var assignment = new UserRoleAssignment { Role = "Viewer" };
        await testControlClient.AssignUserToWorkspaceAsync(currentUsername, workspaceKey, assignment);
    }

    /// <summary>
    /// Given: I have two workspaces
    /// </summary>
    protected async Task GivenIHaveTwoWorkspaces(DataTable workspacesTable)
    {
        // Reuse the same logic as GivenIHaveAccessToTheseWorkspaces
        await GivenIHaveAccessToTheseWorkspaces(workspacesTable);
    }

    /// <summary>
    /// Given: {workspaceName} contains {transactionCount} transactions
    /// </summary>
    protected async Task GivenWorkspaceContainsTransactions(string workspaceName, int transactionCount)
    {
        var currentUsername = GetCurrentTestUsername();
        var fullWorkspaceName = AddTestPrefix(workspaceName);

        if (!_workspaceKeys.TryGetValue(fullWorkspaceName, out var workspaceKey))
        {
            throw new InvalidOperationException($"Workspace '{fullWorkspaceName}' key not found.");
        }

        var request = new TransactionSeedRequest
        {
            Count = transactionCount,
            PayeePrefix = "Test Transaction"
        };

        await testControlClient.SeedTransactionsAsync(currentUsername, workspaceKey, request);
    }

    /// <summary>
    /// Given: I have access to {workspaceName}
    /// </summary>
    protected async Task GivenIHaveAccessTo(string workspaceName)
    {
        var currentUsername = GetCurrentTestUsername();
        var fullWorkspaceName = AddTestPrefix(workspaceName);

        var request = new WorkspaceSetupRequest
        {
            Name = fullWorkspaceName,
            Description = $"Test workspace: {workspaceName}",
            Role = "Viewer" // Default to minimum access level
        };

        var results = await testControlClient.BulkWorkspaceSetupAsync(currentUsername, new[] { request });
        var result = results.First();

        // Store with FULL workspace name (what API returns)
        _workspaceKeys[result.Name] = result.Key;
    }

    /// <summary>
    /// Given: there is a workspace called {workspaceName} that I don't have access to
    /// </summary>
    protected async Task GivenThereIsAWorkspaceCalledThatIDontHaveAccessTo(string workspaceName)
    {
        // Get a different user from the credentials dictionary (not the current user)
        var currentUsername = GetCurrentTestUsername();
        var otherUser = _userCredentials.FirstOrDefault(kvp => kvp.Key != currentUsername);

        if (otherUser.Key == null)
        {
            throw new InvalidOperationException("No other test users available. Need at least one user besides the current user.");
        }

        var fullWorkspaceName = AddTestPrefix(workspaceName);

        // Create workspace for the other user
        var request = new WorkspaceCreateRequest
        {
            Name = fullWorkspaceName,
            Description = $"Test workspace (no access): {workspaceName}"
        };

        var result = await testControlClient.CreateWorkspaceForUserAsync(otherUser.Value.Username, request);

        // Store with FULL workspace name (what API returns)
        _workspaceKeys[result.Name] = result.Key;
    }

    /// <summary>
    /// Given: I own {workspaceName}
    /// </summary>
    protected async Task GivenIOwn(string workspaceName)
    {
        await GivenIHaveAWorkspaceCalled(workspaceName);
    }

    /// <summary>
    /// Given: there are other workspaces in the system
    /// </summary>
    protected async Task GivenThereAreOtherWorkspacesInTheSystem(DataTable workspacesTable)
    {
        foreach (var row in workspacesTable)
        {
            var workspaceName = row["Workspace Name"];
            var owner = row["Owner"];
            var fullOwnerUsername = AddTestPrefix(owner);
            var fullWorkspaceName = AddTestPrefix(workspaceName);

            if (!_userCredentials.ContainsKey(fullOwnerUsername))
            {
                throw new InvalidOperationException($"Owner '{fullOwnerUsername}' credentials not found.");
            }

            var request = new WorkspaceCreateRequest
            {
                Name = fullWorkspaceName,
                Description = $"Test workspace for {owner}: {workspaceName}"
            };

            var result = await testControlClient.CreateWorkspaceForUserAsync(_userCredentials[fullOwnerUsername].Username, request);

            // Store with FULL workspace name (what API returns)
            _workspaceKeys[result.Name] = result.Key;
        }
    }

    #endregion

    #region Steps: WHEN

    /// <summary>
    /// When: a new user {username} registers and logs in
    /// </summary>
    protected async Task WhenANewUserRegistersAndLogsIn(string username)
    {
        // Add __TEST__ prefix
        var fullUsername = AddTestPrefix(username);

        // Generate a unique email and password for the new user
        var email = $"{fullUsername}@test.local";
        var password = "Test123!";

        // Navigate to register page
        await Page.GotoAsync("/register");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var registerPage = new RegisterPage(Page);

        await registerPage.RegisterAsync(email, fullUsername, password);

        // Store credentials with FULL username
        _userCredentials[fullUsername] = new TestUserCredentials { Username = fullUsername, Email = email, Password = password };

        // New users get a workspace with their name
        _objectStore.Add("CurrentWorkspaceName", fullUsername);

        // Click the "Continue" button to proceed after registration
        await registerPage.ContinueButton.ClickAsync();

        // Now we are on the login button and we should login
        var loginPage = new LoginPage(Page);
        await loginPage.LoginAsync(fullUsername, password);
    }

    /// <summary>
    /// When: I create a new workspace called {name} for {description}
    /// </summary>
    protected async Task WhenICreateANewWorkspaceCalledFor(string name, string description)
    {
        var workspaceName = AddTestPrefix(name);
        var workspacesPage = GetOrCreateWorkspacesPage();
        await workspacesPage.NavigateAsync();
        await workspacesPage.CreateWorkspaceAsync(workspaceName, description);

        // Store the workspace name for future reference
        _objectStore.Add("LastCreatedWorkspace", workspaceName);
        _objectStore.Add("CurrentWorkspaceName", workspaceName);
    }

    /// <summary>
    /// When: I create a workspace called {name}
    /// </summary>
    protected async Task WhenICreateAWorkspaceCalled(string name)
    {
        await WhenICreateANewWorkspaceCalledFor(name, $"__TEST__ Test workspace: {name}");
    }

    /// <summary>
    /// When: I view my workspace list
    /// </summary>
    protected async Task WhenIViewMyWorkspaceList()
    {
        // TODO: Use WorkspacesPage page object
        var workspacesPage = GetOrCreateWorkspacesPage();
        await workspacesPage.NavigateAsync();
    }

    /// <summary>
    /// When: I view the details of {workspaceName}
    /// </summary>
    protected async Task WhenIViewTheDetailsOf(string workspaceName)
    {
        var fullWorkspaceName = AddTestPrefix(workspaceName);

        var workspacesPage = GetOrCreateWorkspacesPage();
        await workspacesPage.NavigateAsync();

        // Open workspace selector dropdown to view details
        await workspacesPage.WorkspaceSelector.SelectWorkspaceAsync(fullWorkspaceName);
        await workspacesPage.WorkspaceSelector.CloseMenuAsync();

        _objectStore.Add("CurrentWorkspaceName", fullWorkspaceName);
    }

    /// <summary>
    /// When: I rename it to {newName}
    /// </summary>
    protected async Task WhenIRenameItTo(string newName)
    {
        var fullNewName = AddTestPrefix(newName);

        // Store the new name for assertions
        _objectStore.Add("NewWorkspaceName", fullNewName);

        // Get the current workspace name from object store
        var oldName = _objectStore.Get<string>("CurrentWorkspaceName") ?? throw new InvalidOperationException("No workspace name found");

        var workspacesPage = GetOrCreateWorkspacesPage();
        await workspacesPage.NavigateAsync();
        await workspacesPage.UpdateWorkspaceAsync(oldName, fullNewName);

        _objectStore.Add("CurrentWorkspaceName", fullNewName);
    }

    /// <summary>
    /// When: I update the description to {newDescription}
    /// </summary>
    protected async Task WhenIUpdateTheDescriptionTo(string newDescription)
    {
        var workspaceName = _objectStore.Contains<string>("NewWorkspaceName")
            ? _objectStore.Get<string>("NewWorkspaceName")
            : _objectStore.Get<string>("CurrentWorkspaceName");
        if (workspaceName == null)
            throw new InvalidOperationException("No workspace name found");

        var workspacesPage = GetOrCreateWorkspacesPage();
        await workspacesPage.NavigateAsync();
        // Update with same name but new description
        await workspacesPage.UpdateWorkspaceAsync(workspaceName, workspaceName, newDescription);
    }

    /// <summary>
    /// When: I try to change the workspace name or description
    /// </summary>
    protected async Task WhenITryToChangeTheWorkspaceNameOrDescription()
    {
        var workspaceName = _objectStore.Get<string>("CurrentWorkspaceName") ?? throw new InvalidOperationException("No workspace name found");

        var workspacesPage = GetOrCreateWorkspacesPage();
        await workspacesPage.NavigateAsync();

        // Check if edit button is available
        var canEdit = await workspacesPage.IsEditAvailableAsync(workspaceName);
        _objectStore.Add("CanEditWorkspace", (object)canEdit);
    }

    /// <summary>
    /// When: I delete {workspaceName}
    /// </summary>
    protected async Task WhenIDelete(string workspaceName)
    {
        var fullWorkspaceName = AddTestPrefix(workspaceName);

        var workspacesPage = GetOrCreateWorkspacesPage();
        await workspacesPage.NavigateAsync();
        await workspacesPage.DeleteWorkspaceAsync(fullWorkspaceName);
    }

    /// <summary>
    /// When: I try to delete {workspaceName}
    /// </summary>
    protected async Task WhenITryToDelete(string workspaceName)
    {
        var fullWorkspaceName = AddTestPrefix(workspaceName);

        var workspacesPage = GetOrCreateWorkspacesPage();
        await workspacesPage.NavigateAsync();

        // Check if delete button is available
        var canDelete = await workspacesPage.IsDeleteAvailableAsync(fullWorkspaceName);
        _objectStore.Add("CanDeleteWorkspace", (object)canDelete);
        _objectStore.Add("CurrentWorkspaceName", fullWorkspaceName);
    }

    /// <summary>
    /// When: I view transactions in {workspaceName}
    /// </summary>
    protected async Task WhenIViewTransactionsIn(string workspaceName)
    {
        var fullWorkspaceName = AddTestPrefix(workspaceName);

        var transactionsPage = GetOrCreateTransactionsPage();
        await transactionsPage.NavigateAsync();

        // Select the workspace
        await transactionsPage.WorkspaceSelector.SelectWorkspaceAsync(fullWorkspaceName);
        await transactionsPage.WaitForLoadingCompleteAsync();

        _objectStore.Add("CurrentWorkspaceName", fullWorkspaceName);
    }

    /// <summary>
    /// When: I try to view transactions in {workspaceName}
    /// </summary>
    protected async Task WhenITryToViewTransactionsIn(string workspaceName)
    {
        var fullWorkspaceName = AddTestPrefix(workspaceName);

        var transactionsPage = GetOrCreateTransactionsPage();
        await transactionsPage.NavigateAsync();

        // Check if workspace is in available list
        var availableWorkspaces = await transactionsPage.WorkspaceSelector.GetAvailableWorkspacesAsync();
        var hasAccess = availableWorkspaces.Contains(fullWorkspaceName);
        _objectStore.Add("HasWorkspaceAccess", (object)hasAccess);
    }

    /// <summary>
    /// When: I try to add or edit transactions
    /// </summary>
    protected async Task WhenITryToAddOrEditTransactions()
    {
        var transactionsPage = GetOrCreateTransactionsPage();

        // Check if New Transaction button is available
        var canCreate = await transactionsPage.IsNewTransactionAvailableAsync();
        _objectStore.Add("CanCreateTransaction", (object)canCreate);

        // TODO: Check if edit buttons are available on existing transactions
        // This would require knowing which transactions exist
    }

    /// <summary>
    /// When: I add a transaction to {workspaceName}
    /// </summary>
    protected async Task WhenIAddATransactionTo(string workspaceName)
    {
        var fullWorkspaceName = AddTestPrefix(workspaceName);

        var transactionsPage = GetOrCreateTransactionsPage();
        await transactionsPage.NavigateAsync();
        await transactionsPage.WorkspaceSelector.SelectWorkspaceAsync(fullWorkspaceName);

        // Add a test transaction
        var testDate = DateTime.Today.ToString("yyyy-MM-dd");
        var testPayee = "Test Transaction " + Guid.NewGuid().ToString()[..8];
        await transactionsPage.CreateTransactionAsync(testDate, testPayee, 100.00m);

        _objectStore.Add("LastTransactionPayee", testPayee);
    }

    /// <summary>
    /// When: I update that transaction
    /// </summary>
    protected async Task WhenIUpdateThatTransaction()
    {
        var payee = _objectStore.Get<string>("LastTransactionPayee") ?? throw new InvalidOperationException("No transaction payee found");

        var transactionsPage = GetOrCreateTransactionsPage();
        var newDate = DateTime.Today.ToString("yyyy-MM-dd");
        var newPayee = "Updated " + payee;
        await transactionsPage.UpdateTransactionAsync(payee, newDate, newPayee, 200.00m);

        _objectStore.Add("LastTransactionPayee", newPayee);
    }

    /// <summary>
    /// When: I delete that transaction
    /// </summary>
    protected async Task WhenIDeleteThatTransaction()
    {
        var payee = _objectStore.Get<string>("LastTransactionPayee") ?? throw new InvalidOperationException("No transaction payee found");

        var transactionsPage = GetOrCreateTransactionsPage();
        await transactionsPage.DeleteTransactionAsync(payee);
    }

    #endregion

    #region Steps: THEN

    /// <summary>
    /// Then: user should have a workspace ready to use
    /// </summary>
    protected async Task ThenUserShouldHaveAWorkspaceReadyToUse()
    {
        var transactionsPage = GetOrCreateTransactionsPage();
        await transactionsPage.NavigateAsync();

        // Get the current workspace name
        var noWorkspaceMessage = await transactionsPage.WorkspaceSelector.NoWorkspaceMessage.IsVisibleAsync();

        Assert.That(noWorkspaceMessage, Is.False, "User should have at least one workspace available");
    }

    /// <summary>
    /// Then: the workspace should be personalized with the name {expectedName}
    /// </summary>
    protected async Task ThenTheWorkspaceShouldBePersonalizedWithTheName(string expectedName)
    {
        var fullExpectedName = AddTestPrefix(expectedName);

        var transactionsPage = GetOrCreateTransactionsPage();
        await transactionsPage.NavigateAsync();

        var workspaceName = await transactionsPage.WorkspaceSelector.GetCurrentWorkspaceNameAsync();
        Assert.That(workspaceName, Does.Contain(expectedName), $"Workspace name should contain '{expectedName}'");
    }

    /// <summary>
    /// Then: I should see {workspaceName} in my workspace list
    /// </summary>
    protected async Task ThenIShouldSeeInMyWorkspaceList(string workspaceName)
    {
        var fullWorkspaceName = AddTestPrefix(workspaceName);

        var workspacesPage = GetOrCreateWorkspacesPage();
        var hasWorkspace = await workspacesPage.HasWorkspaceAsync(fullWorkspaceName);
        Assert.That(hasWorkspace, Is.True,
            $"Workspace '{fullWorkspaceName}' should be visible in the list");
    }

    /// <summary>
    /// Then: I should be able to manage that workspace
    /// </summary>
    protected async Task ThenIShouldBeAbleToManageThatWorkspace()
    {
        // Check that we have Owner permissions on the workspace
        var workspacesPage = GetOrCreateWorkspacesPage();

        // Get the last created workspace name
        var workspaceName = _objectStore.Contains<string>("LastCreatedWorkspace")
            ? _objectStore.Get<string>("LastCreatedWorkspace")
            : null;

        if (workspaceName != null)
        {
            await workspacesPage.NavigateAsync();
            var role = await workspacesPage.GetWorkspaceRoleAsync(workspaceName);
            Assert.That(role, Is.EqualTo("Owner"), "User should be Owner of the workspace");
        }
    }

    /// <summary>
    /// Then: I should have {expectedCount} workspaces available
    /// </summary>
    protected async Task ThenIShouldHaveWorkspacesAvailable(int expectedCount)
    {
        var workspacesPage = GetOrCreateWorkspacesPage();
        var actualCount = await workspacesPage.GetWorkspaceCountAsync();
        Assert.That(actualCount, Is.EqualTo(expectedCount),
            $"Should have exactly {expectedCount} workspaces");
    }

    /// <summary>
    /// Then: I can work with either workspace independently
    /// </summary>
    protected async Task ThenICanWorkWithEitherWorkspaceIndependently()
    {
        // Verify we can switch between workspaces
        var transactionsPage = GetOrCreateTransactionsPage();
        await transactionsPage.NavigateAsync();

        var availableWorkspaces = await transactionsPage.WorkspaceSelector.GetAvailableWorkspacesAsync();
        Assert.That(availableWorkspaces.Length, Is.GreaterThanOrEqualTo(2), "Should have at least 2 workspaces available");
    }

    /// <summary>
    /// Then: I should see all {expectedCount} workspaces
    /// </summary>
    protected async Task ThenIShouldSeeAllWorkspaces(int expectedCount)
    {
        await ThenIShouldHaveWorkspacesAvailable(expectedCount);
    }

    /// <summary>
    /// Then: I should see what I can do in each workspace
    /// </summary>
    protected async Task ThenIShouldSeeWhatICanDoInEachWorkspace()
    {
        var workspacesPage = GetOrCreateWorkspacesPage();
        await workspacesPage.NavigateAsync();

        // Verify that role badges are displayed for each workspace we created
        foreach (var workspaceName in _workspaceKeys.Keys)
        {
            var role = await workspacesPage.GetWorkspaceRoleAsync(workspaceName);
            Assert.That(role, Is.Not.Null.And.Not.Empty,
                $"Workspace '{workspaceName}' should display a role badge");
            Assert.That(role, Is.AnyOf("Owner", "Editor", "Viewer"),
                $"Workspace '{workspaceName}' should have a valid role (Owner, Editor, or Viewer), but got: {role}");
        }

        var workspaceCount = await workspacesPage.GetWorkspaceCountAsync();
        Assert.That(workspaceCount, Is.GreaterThan(0), "Should have at least one workspace");
    }

    /// <summary>
    /// Then: I should see the workspace information
    /// </summary>
    /// <remarks>
    /// ...for the expected workspace!
    /// </remarks>
    protected async Task ThenIShouldSeeTheWorkspaceInformation()
    {
        var workspacesPage = GetOrCreateWorkspacesPage();

        var expected = _objectStore.Get<string>("CurrentWorkspaceName");

        // Verify workspace selector shows expected information
        var workspaceName = await workspacesPage.WorkspaceSelector.GetCurrentWorkspaceNameAsync();
        Assert.That(workspaceName, Is.EqualTo(expected), "Workspace information should be visible");
    }

    /// <summary>
    /// Then: I should see when it was created
    /// </summary>
    protected async Task ThenIShouldSeeWhenItWasCreated()
    {
        var workspacesPage = GetOrCreateWorkspacesPage();
        await workspacesPage.NavigateAsync();

        var currentWorkspaceName = _objectStore.Get<string>("CurrentWorkspaceName");
        var createdDate = await workspacesPage.GetWorkspaceCardCreatedDate(currentWorkspaceName);

        // Can I verify that it looks like a date?
        DateTime parsedDate;
        bool isValidDate = DateTime.TryParse(createdDate, out parsedDate);
        Assert.That(isValidDate, Is.True, "Workspace created date should be a valid date");
    }

    /// <summary>
    /// Then: the workspace should reflect the changes
    /// </summary>
    protected async Task ThenTheWorkspaceShouldReflectTheChanges()
    {
        var newName = _objectStore.Get<string>("NewWorkspaceName") ?? throw new InvalidOperationException("No new workspace name found");

        var workspacesPage = GetOrCreateWorkspacesPage();
        await workspacesPage.NavigateAsync();

        var hasWorkspace = await workspacesPage.HasWorkspaceAsync(newName);
        Assert.That(hasWorkspace, Is.True, $"Updated workspace '{newName}' should be visible");
    }

    /// <summary>
    /// Then: I should not be able to make those changes
    /// </summary>
    protected async Task ThenIShouldNotBeAbleToMakeThoseChanges()
    {
        var canEdit = _objectStore.Get<object>("CanEditWorkspace") as bool? ?? throw new InvalidOperationException("Edit permission not checked");
        Assert.That(canEdit, Is.False, "User should not be able to edit workspace settings");
    }

    /// <summary>
    /// Then: {workspaceName} should no longer appear in my list
    /// </summary>
    protected async Task ThenShouldNoLongerAppearInMyList(string workspaceName)
    {
        var fullWorkspaceName = AddTestPrefix(workspaceName);

        var workspacesPage = GetOrCreateWorkspacesPage();
        var hasWorkspace = await workspacesPage.HasWorkspaceAsync(fullWorkspaceName);
        Assert.That(hasWorkspace, Is.False,
            $"Workspace '{fullWorkspaceName}' should not be in the list");
    }

    /// <summary>
    /// Then: the workspace should remain intact
    /// </summary>
    protected async Task ThenTheWorkspaceShouldRemainIntact()
    {
        var canDelete = _objectStore.Get<object>("CanDeleteWorkspace") as bool? ?? throw new InvalidOperationException("Delete permission not checked");
        Assert.That(canDelete, Is.False, "User should not be able to delete the workspace");
    }

    /// <summary>
    /// Then: I should see exactly {expectedCount} transactions
    /// </summary>
    protected async Task ThenIShouldSeeExactlyTransactions(int expectedCount)
    {
        var transactionsPage = GetOrCreateTransactionsPage();
        var actualCount = await transactionsPage.GetTransactionCountAsync();
        Assert.That(actualCount, Is.EqualTo(expectedCount), $"Expected exactly {expectedCount} transactions");
    }

    /// <summary>
    /// Then: they should all be from {workspaceName} workspace
    /// </summary>
    protected async Task ThenTheyShouldAllBeFromWorkspace(string workspaceName)
    {
        var fullWorkspaceName = AddTestPrefix(workspaceName);

        // Verify we're viewing the correct workspace
        var transactionsPage = GetOrCreateTransactionsPage();
        var currentWorkspace = await transactionsPage.WorkspaceSelector.GetCurrentWorkspaceNameAsync();
        Assert.That(currentWorkspace, Is.EqualTo(fullWorkspaceName), $"Should be viewing '{fullWorkspaceName}' workspace");
    }

    /// <summary>
    /// Then: I should not see any transactions from {workspaceName}
    /// </summary>
    protected async Task ThenIShouldNotSeeAnyTransactionsFrom(string workspaceName)
    {
        var fullWorkspaceName = AddTestPrefix(workspaceName);

        // Transactions are workspace-isolated, so if we're in a different workspace, we won't see them
        var transactionsPage = GetOrCreateTransactionsPage();
        var currentWorkspace = await transactionsPage.WorkspaceSelector.GetCurrentWorkspaceNameAsync();
        Assert.That(currentWorkspace, Is.Not.EqualTo(fullWorkspaceName), $"Should not be viewing '{fullWorkspaceName}' workspace");
    }

    /// <summary>
    /// Then: I should not be able to access that data
    /// </summary>
    protected async Task ThenIShouldNotBeAbleToAccessThatData()
    {
        var hasAccess = _objectStore.Get<object>("HasWorkspaceAccess") as bool? ?? throw new InvalidOperationException("Workspace access not checked");
        Assert.That(hasAccess, Is.False, "User should not have access to the workspace");
    }

    /// <summary>
    /// Then: I should see the transactions
    /// </summary>
    protected async Task ThenIShouldSeeTheTransactions()
    {
        var transactionsPage = GetOrCreateTransactionsPage();
        var count = await transactionsPage.GetTransactionCountAsync();
        Assert.That(count, Is.GreaterThan(0), "Should see some transactions");
    }

    /// <summary>
    /// Then: the transaction should be saved successfully
    /// </summary>
    protected async Task ThenTheTransactionShouldBeSavedSuccessfully()
    {
        var payee = _objectStore.Get<string>("LastTransactionPayee") ?? throw new InvalidOperationException("No transaction payee found");

        var transactionsPage = GetOrCreateTransactionsPage();
        var hasTransaction = await transactionsPage.HasTransactionAsync(payee);
        Assert.That(hasTransaction, Is.True, "Transaction should be visible in the list");
    }

    /// <summary>
    /// Then: my changes should be saved
    /// </summary>
    protected async Task ThenMyChangesShouldBeSaved()
    {
        var payee = _objectStore.Get<string>("LastTransactionPayee") ?? throw new InvalidOperationException("No transaction payee found");

        var transactionsPage = GetOrCreateTransactionsPage();
        var hasTransaction = await transactionsPage.HasTransactionAsync(payee);
        Assert.That(hasTransaction, Is.True, "Updated transaction should be visible");
    }

    /// <summary>
    /// Then: it should be removed
    /// </summary>
    protected async Task ThenItShouldBeRemoved()
    {
        var payee = _objectStore.Get<string>("LastTransactionPayee") ?? throw new InvalidOperationException("No transaction payee found");

        var transactionsPage = GetOrCreateTransactionsPage();
        var hasTransaction = await transactionsPage.HasTransactionAsync(payee);
        Assert.That(hasTransaction, Is.False, "Transaction should be removed from the list");
    }

    /// <summary>
    /// Then: I can add, edit, and delete transactions
    /// </summary>
    protected async Task ThenICanAddEditAndDeleteTransactions()
    {
        var transactionsPage = GetOrCreateTransactionsPage();
        await transactionsPage.NavigateAsync();

        var canCreate = await transactionsPage.IsNewTransactionAvailableAsync();
        Assert.That(canCreate, Is.True, "Owner should be able to create transactions");

        // TODO: Check edit and delete permissions once transactions exist
    }

    /// <summary>
    /// Then: I can change workspace settings
    /// </summary>
    protected async Task ThenICanChangeWorkspaceSettings()
    {
        var workspaceName = _objectStore.Get<string>("CurrentWorkspaceName") ?? throw new InvalidOperationException("No workspace name found");

        var workspacesPage = GetOrCreateWorkspacesPage();
        await workspacesPage.NavigateAsync();

        var canEdit = await workspacesPage.IsEditAvailableAsync(workspaceName);
        Assert.That(canEdit, Is.True, "Owner should be able to edit workspace settings");
    }

    /// <summary>
    /// Then: I can remove the workspace if needed
    /// </summary>
    protected async Task ThenICanRemoveTheWorkspaceIfNeeded()
    {
        var workspaceName = _objectStore.Get<string>("CurrentWorkspaceName") ?? throw new InvalidOperationException("No workspace name found");

        var workspacesPage = GetOrCreateWorkspacesPage();
        await workspacesPage.NavigateAsync();

        var canDelete = await workspacesPage.IsDeleteAvailableAsync(workspaceName);
        Assert.That(canDelete, Is.True, "Owner should be able to delete workspace");
    }

    /// <summary>
    /// Then: I should see only {workspaceName} in my list
    /// </summary>
    protected async Task ThenIShouldSeeOnlyInMyList(string workspaceName)
    {
        var fullWorkspaceName = AddTestPrefix(workspaceName);

        var workspacesPage = GetOrCreateWorkspacesPage();

        // Verify exactly one workspace is visible
        var count = await workspacesPage.GetWorkspaceCountAsync();
        Assert.That(count, Is.EqualTo(1), "Should see exactly one workspace");

        // Verify it's the expected workspace
        var hasWorkspace = await workspacesPage.HasWorkspaceAsync(fullWorkspaceName);
        Assert.That(hasWorkspace, Is.True,
            $"The only workspace should be '{fullWorkspaceName}'");
    }

    /// <summary>
    /// Then: I should not see {workspaceName} in my list
    /// </summary>
    protected async Task ThenIShouldNotSeeInMyList(string workspaceName)
    {
        var fullWorkspaceName = AddTestPrefix(workspaceName);

        var workspacesPage = GetOrCreateWorkspacesPage();
        var hasWorkspace = await workspacesPage.HasWorkspaceAsync(fullWorkspaceName);
        Assert.That(hasWorkspace, Is.False,
            $"Workspace '{fullWorkspaceName}' should not be visible");
    }

    /// <summary>
    /// Then: the workspace count should be {expectedCount}
    /// </summary>
    protected async Task ThenTheWorkspaceCountShouldBe(int expectedCount)
    {
        await ThenIShouldHaveWorkspacesAvailable(expectedCount);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Get or create WorkspacesPage and store it in the object store
    /// </summary>
    private WorkspacesPage GetOrCreateWorkspacesPage()
    {
        if (!_objectStore.Contains<WorkspacesPage>())
        {
            var workspacesPage = new WorkspacesPage(Page);
            _objectStore.Add(workspacesPage);
        }
        return It<WorkspacesPage>();
    }

    /// <summary>
    /// Get or create TransactionsPage and store it in the object store
    /// </summary>
    private TransactionsPage GetOrCreateTransactionsPage()
    {
        if (!_objectStore.Contains<TransactionsPage>())
        {
            var transactionsPage = new TransactionsPage(Page);
            _objectStore.Add(transactionsPage);
        }
        return It<TransactionsPage>();
    }

    /// <summary>
    /// Get the current test username (with __TEST__ prefix)
    /// </summary>
    /// <remarks>
    /// Returns the FULL username with prefix. Checks _objectStore first (which is set by GivenIAmLoggedInAs),
    /// otherwise falls back to the first user in _userCredentials.
    /// </remarks>
    private string GetCurrentTestUsername()
    {
        // Check if we have a logged-in user in object store
        if (_objectStore.Contains<string>("LoggedInAs"))
        {
            return _objectStore.Get<string>("LoggedInAs");
        }

        // Fall back to first user from credentials
        var firstUser = _userCredentials.FirstOrDefault();
        if (firstUser.Key == null)
        {
            throw new InvalidOperationException("No test users found. Ensure users are created in Background.");
        }

        // Return the FULL username (key in dictionary already has prefix)
        return firstUser.Key;
    }

    #endregion
}
