using Microsoft.Playwright;
using YoFi.V3.Tests.Functional.Pages;
using YoFi.V3.Tests.Functional.Helpers;
using YoFi.V3.Tests.Functional.Generated;

namespace YoFi.V3.Tests.Functional.Steps;

/// <summary>
/// Step definitions for Workspace Tenancy feature tests
/// </summary>
public abstract class WorkspaceTenancySteps : FunctionalTest
{
    #region Test Data Storage

    // Store user credentials created in Background
    private readonly Dictionary<string, TestUserCredentials> _userCredentials = new();

    // Store workspace keys for later reference
    private readonly Dictionary<string, Guid> _workspaceKeys = new();

    #endregion

    #region Steps: GIVEN

    /// <summary>
    /// Given: these users exist
    /// </summary>
    protected async Task GivenTheseUsersExist(DataTable usersTable)
    {
        var usernames = usersTable.GetColumn("Username").ToArray();
        var credentials = await testControlClient.CreateBulkUsersAsync(usernames);

        foreach (var cred in credentials)
        {
            var shortUsername = cred.Username.Replace("__TEST__", "");
            _userCredentials[shortUsername] = cred;
        }
    }

    /// <summary>
    /// Given: I am logged in as {username}
    /// </summary>
    protected async Task GivenIAmLoggedInAs(string username)
    {
        if (!_userCredentials.TryGetValue(username, out var cred))
        {
            throw new InvalidOperationException($"User '{username}' credentials not found. Ensure user was created in Background.");
        }

        await Page.GotoAsync("/login");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var loginPage = new LoginPage(Page);
        await loginPage.EnterCredentialsAsync(cred.Email, cred.Password);
        await loginPage.ClickLoginButtonAsync();

        // Wait for redirect after successful login
        await Page.WaitForURLAsync(url => !url.Contains("/login"), new() { Timeout = 10000 });
    }

    /// <summary>
    /// Given: I have access to these workspaces
    /// </summary>
    protected async Task GivenIHaveAccessToTheseWorkspaces(DataTable workspacesTable)
    {
        var currentUsername = GetCurrentTestUsername();
        var requests = new List<WorkspaceSetupRequest>();

        foreach (var row in workspacesTable)
        {
            var workspaceName = row["Workspace Name"];
            var role = row["My Role"];

            requests.Add(new WorkspaceSetupRequest
            {
                Name = workspaceName,
                Description = $"Test workspace: {workspaceName}",
                Role = role
            });
        }

        var results = await testControlClient.BulkWorkspaceSetupAsync(currentUsername, requests);

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
        var request = new WorkspaceCreateRequest
        {
            Name = workspaceName,
            Description = $"Test workspace: {workspaceName}"
        };

        var result = await testControlClient.CreateWorkspaceForUserAsync(currentUsername, request);
        _workspaceKeys[workspaceName] = result.Key;
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

        if (!_workspaceKeys.TryGetValue(workspaceName, out var workspaceKey))
        {
            throw new InvalidOperationException($"Workspace '{workspaceName}' key not found.");
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

        if (!_workspaceKeys.TryGetValue(workspaceName, out var workspaceKey))
        {
            throw new InvalidOperationException($"Workspace '{workspaceName}' key not found.");
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

        if (!_workspaceKeys.TryGetValue(workspaceName, out var workspaceKey))
        {
            throw new InvalidOperationException($"Workspace '{workspaceName}' key not found.");
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
        // TODO: Create workspace and assign current user with any role
        await Task.CompletedTask;
    }

    /// <summary>
    /// Given: there is a workspace called {workspaceName} that I don't have access to
    /// </summary>
    protected async Task GivenThereIsAWorkspaceCalledThatIDontHaveAccessTo(string workspaceName)
    {
        // TODO: Create workspace for a different user (not current user)
        // Use Test Control API to create workspace owned by another test user

        await Task.CompletedTask;
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

            if (!_userCredentials.ContainsKey(owner))
            {
                throw new InvalidOperationException($"Owner '{owner}' credentials not found.");
            }

            var request = new WorkspaceCreateRequest
            {
                Name = workspaceName,
                Description = $"Test workspace for {owner}: {workspaceName}"
            };

            var result = await testControlClient.CreateWorkspaceForUserAsync(_userCredentials[owner].Username, request);
            _workspaceKeys[workspaceName] = result.Key;
        }
    }

    #endregion

    #region Steps: WHEN

    /// <summary>
    /// When: a new user {username} registers and logs in
    /// </summary>
    protected async Task WhenANewUserRegistersAndLogsIn(string username)
    {
        // Generate a unique email and password for the new user
        var email = $"{username}@test.local";
        var password = "Test123!";

        // Navigate to register page
        await Page.GotoAsync("/register");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        var registerPage = new RegisterPage(Page);

        // TODO: Need RegisterPage.RegisterAsync(email, password, confirmPassword) method
        // For now, use individual field fills
        await registerPage.EmailInput.FillAsync(email);
        await registerPage.PasswordInput.FillAsync(password);
        await registerPage.PasswordAgainInput.FillAsync(password);
        await registerPage.RegisterButton.ClickAsync();

        // Wait for redirect after registration (should auto-login)
        await Page.WaitForURLAsync(url => !url.Contains("/register"), new() { Timeout = 10000 });

        // Store credentials for future reference
        _userCredentials[username] = new TestUserCredentials { Email = email, Password = password };
        _objectStore.Add("CurrentWorkspaceName", username); // New users get a workspace with their name
    }

    /// <summary>
    /// When: I create a new workspace called {name} for {description}
    /// </summary>
    protected async Task WhenICreateANewWorkspaceCalledFor(string name, string description)
    {
        var workspacesPage = GetOrCreateWorkspacesPage();
        await workspacesPage.NavigateAsync();
        await workspacesPage.CreateWorkspaceAsync(name, description);

        // Store the workspace name for future reference
        _objectStore.Add("LastCreatedWorkspace", name);
        _objectStore.Add("CurrentWorkspaceName", name);
    }

    /// <summary>
    /// When: I create a workspace called {name}
    /// </summary>
    protected async Task WhenICreateAWorkspaceCalled(string name)
    {
        await WhenICreateANewWorkspaceCalledFor(name, $"Test workspace: {name}");
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
        var workspacesPage = GetOrCreateWorkspacesPage();
        await workspacesPage.NavigateAsync();

        // Open workspace selector dropdown to view details
        await workspacesPage.WorkspaceSelector.SelectWorkspaceAsync(workspaceName);
        await workspacesPage.WorkspaceSelector.OpenMenuAsync();

        _objectStore.Add("CurrentWorkspaceName", workspaceName);
    }

    /// <summary>
    /// When: I rename it to {newName}
    /// </summary>
    protected async Task WhenIRenameItTo(string newName)
    {
        // Store the new name for assertions
        _objectStore.Add("NewWorkspaceName", newName);

        // Get the current workspace name from object store
        var oldName = _objectStore.Get<string>("CurrentWorkspaceName") ?? throw new InvalidOperationException("No workspace name found");

        var workspacesPage = GetOrCreateWorkspacesPage();
        await workspacesPage.NavigateAsync();
        await workspacesPage.UpdateWorkspaceAsync(oldName, newName);

        _objectStore.Add("CurrentWorkspaceName", newName);
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
        var workspacesPage = GetOrCreateWorkspacesPage();
        await workspacesPage.NavigateAsync();
        await workspacesPage.DeleteWorkspaceAsync(workspaceName);
    }

    /// <summary>
    /// When: I try to delete {workspaceName}
    /// </summary>
    protected async Task WhenITryToDelete(string workspaceName)
    {
        var workspacesPage = GetOrCreateWorkspacesPage();
        await workspacesPage.NavigateAsync();

        // Check if delete button is available
        var canDelete = await workspacesPage.IsDeleteAvailableAsync(workspaceName);
        _objectStore.Add("CanDeleteWorkspace", (object)canDelete);
        _objectStore.Add("CurrentWorkspaceName", workspaceName);
    }

    /// <summary>
    /// When: I view transactions in {workspaceName}
    /// </summary>
    protected async Task WhenIViewTransactionsIn(string workspaceName)
    {
        var transactionsPage = GetOrCreateTransactionsPage();
        await transactionsPage.NavigateAsync();

        // Select the workspace
        await transactionsPage.WorkspaceSelector.SelectWorkspaceAsync(workspaceName);
        await transactionsPage.WaitForLoadingCompleteAsync();

        _objectStore.Add("CurrentWorkspaceName", workspaceName);
    }

    /// <summary>
    /// When: I try to view transactions in {workspaceName}
    /// </summary>
    protected async Task WhenITryToViewTransactionsIn(string workspaceName)
    {
        // TODO: Need Test Control API to get workspace key by name
        // For now, attempt to navigate and check if workspace is available
        var transactionsPage = GetOrCreateTransactionsPage();
        await transactionsPage.NavigateAsync();

        // Check if workspace is in available list
        var availableWorkspaces = await transactionsPage.WorkspaceSelector.GetAvailableWorkspacesAsync();
        var hasAccess = availableWorkspaces.Contains(workspaceName);
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
        var transactionsPage = GetOrCreateTransactionsPage();
        await transactionsPage.NavigateAsync();
        await transactionsPage.WorkspaceSelector.SelectWorkspaceAsync(workspaceName);

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
    /// Then: {username} should have a workspace ready to use
    /// </summary>
    protected async Task ThenUserShouldHaveAWorkspaceReadyToUse(string username)
    {
        var transactionsPage = GetOrCreateTransactionsPage();
        await transactionsPage.NavigateAsync();

        // Check that a workspace is selected
        var hasWorkspace = await transactionsPage.WorkspaceSelector.HasWorkspaceSelectedAsync();
        Assert.That(hasWorkspace, Is.True, $"User {username} should have a workspace selected");
    }

    /// <summary>
    /// Then: the workspace should be personalized with the name {expectedName}
    /// </summary>
    protected async Task ThenTheWorkspaceShouldBePersonalizedWithTheName(string expectedName)
    {
        var transactionsPage = GetOrCreateTransactionsPage();
        var workspaceName = await transactionsPage.WorkspaceSelector.GetCurrentWorkspaceNameAsync();

        Assert.That(workspaceName, Does.Contain(expectedName), $"Workspace name should contain '{expectedName}'");
    }

    /// <summary>
    /// Then: I should see {workspaceName} in my workspace list
    /// </summary>
    protected async Task ThenIShouldSeeInMyWorkspaceList(string workspaceName)
    {
        var workspacesPage = GetOrCreateWorkspacesPage();
        var hasWorkspace = await workspacesPage.HasWorkspaceAsync(workspaceName);
        Assert.That(hasWorkspace, Is.True,
            $"Workspace '{workspaceName}' should be visible in the list");
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

        // Verify that role badges are displayed
        // TODO: Need to get workspace names from previous step to check their roles
        var workspaceCount = await workspacesPage.GetWorkspaceCountAsync();
        Assert.That(workspaceCount, Is.GreaterThan(0), "Should have at least one workspace");
    }

    /// <summary>
    /// Then: I should see the workspace information
    /// </summary>
    protected async Task ThenIShouldSeeTheWorkspaceInformation()
    {
        var workspacesPage = GetOrCreateWorkspacesPage();

        // Verify workspace selector shows information
        var workspaceName = await workspacesPage.WorkspaceSelector.GetCurrentWorkspaceNameAsync();
        Assert.That(workspaceName, Is.Not.Null.And.Not.Empty, "Workspace information should be visible");
    }

    /// <summary>
    /// Then: I should see when it was created
    /// </summary>
    protected async Task ThenIShouldSeeWhenItWasCreated()
    {
        var workspacesPage = GetOrCreateWorkspacesPage();

        // TODO: Need WorkspaceSelector component method to check if created date is displayed
        // For now, just verify workspace details are visible
        await workspacesPage.WorkspaceSelector.OpenMenuAsync();
        // Menu should show workspace details including created date
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
        var workspacesPage = GetOrCreateWorkspacesPage();
        var hasWorkspace = await workspacesPage.HasWorkspaceAsync(workspaceName);
        Assert.That(hasWorkspace, Is.False,
            $"Workspace '{workspaceName}' should not be in the list");
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
        // Verify we're viewing the correct workspace
        var transactionsPage = GetOrCreateTransactionsPage();
        var currentWorkspace = await transactionsPage.WorkspaceSelector.GetCurrentWorkspaceNameAsync();
        Assert.That(currentWorkspace, Is.EqualTo(workspaceName), $"Should be viewing '{workspaceName}' workspace");
    }

    /// <summary>
    /// Then: I should not see any transactions from {workspaceName}
    /// </summary>
    protected async Task ThenIShouldNotSeeAnyTransactionsFrom(string workspaceName)
    {
        // Transactions are workspace-isolated, so if we're in a different workspace, we won't see them
        var transactionsPage = GetOrCreateTransactionsPage();
        var currentWorkspace = await transactionsPage.WorkspaceSelector.GetCurrentWorkspaceNameAsync();
        Assert.That(currentWorkspace, Is.Not.EqualTo(workspaceName), $"Should not be viewing '{workspaceName}' workspace");
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
        var workspacesPage = GetOrCreateWorkspacesPage();

        // Verify exactly one workspace is visible
        var count = await workspacesPage.GetWorkspaceCountAsync();
        Assert.That(count, Is.EqualTo(1), "Should see exactly one workspace");

        // Verify it's the expected workspace
        var hasWorkspace = await workspacesPage.HasWorkspaceAsync(workspaceName);
        Assert.That(hasWorkspace, Is.True,
            $"The only workspace should be '{workspaceName}'");
    }

    /// <summary>
    /// Then: I should not see {workspaceName} in my list
    /// </summary>
    protected async Task ThenIShouldNotSeeInMyList(string workspaceName)
    {
        var workspacesPage = GetOrCreateWorkspacesPage();
        var hasWorkspace = await workspacesPage.HasWorkspaceAsync(workspaceName);
        Assert.That(hasWorkspace, Is.False,
            $"Workspace '{workspaceName}' should not be visible");
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
    /// Get the current test username (strips __TEST__ prefix if present)
    /// </summary>
    private string GetCurrentTestUsername()
    {
        // For now, get the first user from _userCredentials
        // In a real scenario, you'd track which user is currently "logged in" in the test context
        var firstUser = _userCredentials.FirstOrDefault();
        if (firstUser.Key == null)
        {
            throw new InvalidOperationException("No test users found. Ensure users are created in Background.");
        }
        return firstUser.Value.Username;
    }

    #endregion
}
