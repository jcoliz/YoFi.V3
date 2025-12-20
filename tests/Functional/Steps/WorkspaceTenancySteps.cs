using Microsoft.Playwright;
using YoFi.V3.Tests.Functional.Pages;
using YoFi.V3.Tests.Functional.Helpers;
using YoFi.V3.Tests.Functional.Generated;
using YoFi.V3.Tests.Functional.Steps.Common;
using NUnit.Framework.Internal;

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
public abstract class WorkspaceTenancySteps : CommonThenSteps
{
    #region Test Data Storage

    // Store user credentials created in Background (keys are FULL usernames with __TEST__ prefix)
    private readonly Dictionary<string, TestUserCredentials> _userCredentials = new();

    // Store workspace keys for later reference (keys are FULL workspace names as returned by API)
    private readonly Dictionary<string, Guid> _workspaceKeys = new();

    [SetUp]
    public void SetupWorkspaceTenancySteps()
    {
        // Clear test data before each scenario
        _userCredentials.Clear();
        _workspaceKeys.Clear();
    }

    #endregion

    #region Object Store Keys

    private const string KEY_LOGGED_IN_AS = "LoggedInAs";
    private const string KEY_PENDING_USER_CONTEXT = "PendingUserContext"; // User context for pre-login steps
    private const string KEY_CURRENT_WORKSPACE = "CurrentWorkspaceName";
    private const string KEY_NEW_WORKSPACE_NAME = "NewWorkspaceName";
    private const string KEY_LAST_TRANSACTION_PAYEE = "LastTransactionPayee";
    private const string KEY_CAN_DELETE_WORKSPACE = "CanDeleteWorkspace";
    private const string KEY_CAN_MAKE_DESIRED_CHANGES = "CanMakeDesiredChanges";
    private const string KEY_HAS_WORKSPACE_ACCESS = "HasWorkspaceAccess";

    #endregion

    #region Helpers

    /// <summary>
    /// Adds the __TEST__ prefix to a name for test controller API calls
    /// </summary>
    private static string AddTestPrefix(string name) => $"__TEST__{name}";

    /// <summary>
    /// Get or create a page object of type T from the object store
    /// </summary>
    private T GetOrCreatePage<T>() where T : class
    {
        if (!_objectStore.Contains<T>())
        {
            var page = Activator.CreateInstance(typeof(T), Page) as T;
            _objectStore.Add(page!);
        }
        return It<T>();
    }

    /// <summary>
    /// Gets a required value from the object store, throwing if not found
    /// </summary>
    private string GetRequiredFromStore(string key)
    {
        return _objectStore.Get<string>(key)
            ?? throw new InvalidOperationException($"Required value '{key}' not found in object store");
    }

    /// <summary>
    /// Gets the current or newly renamed workspace name from object store
    /// </summary>
    private string GetCurrentOrNewWorkspaceName()
    {
        return _objectStore.Contains<string>(KEY_NEW_WORKSPACE_NAME)
            ? _objectStore.Get<string>(KEY_NEW_WORKSPACE_NAME)!
            : _objectStore.Get<string>(KEY_CURRENT_WORKSPACE)!;
    }

    /// <summary>
    /// Gets the last transaction payee from object store
    /// </summary>
    private string GetLastTransactionPayee()
    {
        return GetRequiredFromStore(KEY_LAST_TRANSACTION_PAYEE);
    }

    /// <summary>
    /// Asserts that a user cannot perform a specific action
    /// </summary>
    private void AssertCannotPerformAction(string actionKey, string message)
    {
        var canPerform = _objectStore.Get<object>(actionKey) as bool?
            ?? throw new InvalidOperationException($"Permission check '{actionKey}' not found");
        Assert.That(canPerform, Is.False, message);
    }

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

        var loginPage = new LoginPage(Page);
        await loginPage.NavigateAsync();

        await loginPage.LoginAsync(cred.Username, cred.Password);

        // Wait for redirect after successful login
        await Page.WaitForURLAsync(url => !url.Contains("/login"), new() { Timeout = 10000 });

        // Store FULL username for future reference
        _objectStore.Add(KEY_LOGGED_IN_AS, fullUsername);
    }

    /// <summary>
    /// Given: {username} owns a workspace called {workspaceName}
    /// Given: {username} owns {workspaceName}
    /// </summary>
    protected async Task GivenUserOwnsAWorkspaceCalled(string username, string workspaceName)
    {
        var fullUsername = AddTestPrefix(username);
        var fullWorkspaceName = AddTestPrefix(workspaceName);

        if (!_userCredentials.TryGetValue(fullUsername, out var cred))
        {
            throw new InvalidOperationException($"User '{fullUsername}' credentials not found. Ensure user was created in Background.");
        }

        var request = new WorkspaceCreateRequest
        {
            Name = fullWorkspaceName,
            Description = $"Test workspace: {workspaceName}",
            Role = "Owner"
        };

        TenantResultDto? result;
        try
        {
            result = await testControlClient.CreateWorkspaceForUserAsync(fullUsername, request);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating workspace '{fullWorkspaceName}' for user '{fullUsername}': {ex.Message}");
            throw;
        }

        // Store with FULL workspace name (what API returns)
        _workspaceKeys[result!.Name] = result.Key;
        _objectStore.Add(KEY_CURRENT_WORKSPACE, fullWorkspaceName);

        // Store pending user context for steps that need it before login
        _objectStore.Add(KEY_PENDING_USER_CONTEXT, fullUsername);
    }

    /// <summary>
    /// Given: {username} has access to these workspaces
    /// </summary>
    protected async Task GivenUserHasAccessToTheseWorkspaces(string username, DataTable workspacesTable)
    {
        var fullUsername = AddTestPrefix(username);

        if (!_userCredentials.TryGetValue(fullUsername, out var cred))
        {
            throw new InvalidOperationException($"User '{fullUsername}' credentials not found. Ensure user was created in Background.");
        }

        // Add __TEST__ prefix to workspace names before API call
        var requests = workspacesTable.Select(row => new WorkspaceSetupRequest
        {
            Name = AddTestPrefix(row["Workspace Name"]),
            Description = $"Test workspace: {row["Workspace Name"]}",
            Role = row["My Role"]
        }).ToList();

        var results = await testControlClient.BulkWorkspaceSetupAsync(fullUsername, requests);

        // Store with FULL workspace names (what API returns)
        foreach (var result in results)
        {
            _workspaceKeys[result.Name] = result.Key;
        }

        // Store pending user context for steps that need it before login
        _objectStore.Add(KEY_PENDING_USER_CONTEXT, fullUsername);
    }

    /// <summary>
    /// Given: {username} has access to {workspaceName}
    /// </summary>
    protected async Task GivenUserHasAccessTo(string username, string workspaceName)
    {
        var fullUsername = AddTestPrefix(username);
        var fullWorkspaceName = AddTestPrefix(workspaceName);

        if (!_userCredentials.TryGetValue(fullUsername, out var cred))
        {
            throw new InvalidOperationException($"User '{fullUsername}' credentials not found. Ensure user was created in Background.");
        }

        var request = new WorkspaceSetupRequest
        {
            Name = fullWorkspaceName,
            Description = $"Test workspace: {workspaceName}",
            Role = "Viewer" // Default to minimum access level
        };

        var results = await testControlClient.BulkWorkspaceSetupAsync(fullUsername, new[] { request });
        var result = results.First();

        // Store with FULL workspace name (what API returns)
        _workspaceKeys[result.Name] = result.Key;

        // Store pending user context for steps that need it before login
        _objectStore.Add(KEY_PENDING_USER_CONTEXT, fullUsername);
    }

    /// <summary>
    /// Given: {username} can edit data in {workspaceName}
    /// </summary>
    protected async Task GivenUserCanEditDataIn(string username, string workspaceName)
    {
        var fullUsername = AddTestPrefix(username);
        var fullWorkspaceName = AddTestPrefix(workspaceName);

        if (!_userCredentials.TryGetValue(fullUsername, out var cred))
        {
            throw new InvalidOperationException($"User '{fullUsername}' credentials not found. Ensure user was created in Background.");
        }

        var request = new WorkspaceSetupRequest
        {
            Name = fullWorkspaceName,
            Description = $"Test workspace: {workspaceName}",
            Role = "Editor"
        };

        var results = await testControlClient.BulkWorkspaceSetupAsync(fullUsername, new[] { request });
        var result = results.First();
        _workspaceKeys[result.Name] = result.Key;

        // Store current workspace for later reference
        _objectStore.Add(KEY_CURRENT_WORKSPACE, fullWorkspaceName);

        // Store pending user context for steps that need it before login
        _objectStore.Add(KEY_PENDING_USER_CONTEXT, fullUsername);
    }

    /// <summary>
    /// Given: {username} can view data in {workspaceName}
    /// </summary>
    protected async Task GivenUserCanViewDataIn(string username, string workspaceName)
    {
        var fullUsername = AddTestPrefix(username);
        var fullWorkspaceName = AddTestPrefix(workspaceName);

        if (!_userCredentials.TryGetValue(fullUsername, out var cred))
        {
            throw new InvalidOperationException($"User '{fullUsername}' credentials not found. Ensure user was created in Background.");
        }

        var request = new WorkspaceSetupRequest
        {
            Name = fullWorkspaceName,
            Description = $"Test workspace: {workspaceName}",
            Role = "Viewer"
        };

        var results = await testControlClient.BulkWorkspaceSetupAsync(fullUsername, new[] { request });
        var result = results.First();
        _workspaceKeys[result.Name] = result.Key;

        // Store current workspace for later reference
        _objectStore.Add(KEY_CURRENT_WORKSPACE, fullWorkspaceName);

        // Store pending user context for steps that need it before login
        _objectStore.Add(KEY_PENDING_USER_CONTEXT, fullUsername);
    }

    /// <summary>
    /// Given: there is a workspace called {workspaceName} that {username} doesn't have access to
    /// </summary>
    protected async Task GivenThereIsAWorkspaceCalledThatUserDoesntHaveAccessTo(string workspaceName, string username)
    {
        var fullUsername = AddTestPrefix(username);

        // Get a different user from the credentials dictionary (not the specified user)
        var otherUser = _userCredentials.FirstOrDefault(kvp => kvp.Key != fullUsername);

        if (otherUser.Key == null)
        {
            throw new InvalidOperationException($"No other test users available besides '{fullUsername}'. Need at least one user besides the specified user.");
        }

        var fullWorkspaceName = AddTestPrefix(workspaceName);

        // Create workspace for the other user
        var request = new WorkspaceCreateRequest
        {
            Name = fullWorkspaceName,
            Description = $"Test workspace (no access for {username}): {workspaceName}",
            Role = "Owner"
        };

        var result = await testControlClient.CreateWorkspaceForUserAsync(otherUser.Value.Username, request);

        // Store with FULL workspace name (what API returns)
        _workspaceKeys[result.Name] = result.Key;
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
                Description = $"Test workspace for {owner}: {workspaceName}",
                Role = "Owner"
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
        var registerPage = new RegisterPage(Page);
        await registerPage.NavigateAsync();

        await registerPage.RegisterAsync(email, fullUsername, password);

        // Store credentials with FULL username
        _userCredentials[fullUsername] = new TestUserCredentials { Username = fullUsername, Email = email, Password = password };

        // New users get a workspace with their name
        _objectStore.Add(KEY_CURRENT_WORKSPACE, fullUsername);

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

        // Wait for the new workspace card to appear in the list
        // The loading spinner being hidden doesn't guarantee the list is fully rendered
        await workspacesPage.WaitForWorkspaceAsync(workspaceName);

        // Store the workspace name for future reference
        _objectStore.Add(KEY_CURRENT_WORKSPACE, workspaceName);
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

        _objectStore.Add(KEY_CURRENT_WORKSPACE, fullWorkspaceName);
    }

    /// <summary>
    /// When: I rename it to {newName}
    /// </summary>
    protected async Task WhenIRenameItTo(string newName)
    {
        var fullNewName = AddTestPrefix(newName);

        // Store the new name for assertions
        _objectStore.Add(KEY_NEW_WORKSPACE_NAME, fullNewName);

        // Get the current workspace name from object store
        var oldName = GetRequiredFromStore(KEY_CURRENT_WORKSPACE);

        var workspacesPage = GetOrCreateWorkspacesPage();
        await workspacesPage.NavigateAsync();
        await workspacesPage.UpdateWorkspaceAsync(oldName, fullNewName);

        _objectStore.Add(KEY_CURRENT_WORKSPACE, fullNewName);
    }

    /// <summary>
    /// When: I update the description to {newDescription}
    /// </summary>
    protected async Task WhenIUpdateTheDescriptionTo(string newDescription)
    {
        var workspaceName = GetCurrentOrNewWorkspaceName();

        var workspacesPage = GetOrCreateWorkspacesPage();
        await workspacesPage.NavigateAsync();
        // Update with same name but new description
        await workspacesPage.UpdateWorkspaceAsync(workspaceName, workspaceName, newDescription);

        // Wait for the workspace card to be updated in the list
        // The loading spinner being hidden doesn't guarantee the list is fully rendered
        await workspacesPage.WaitForWorkspaceAsync(workspaceName);
    }

    /// <summary>
    /// When: I try to change the workspace name or description
    /// </summary>
    protected async Task WhenITryToChangeTheWorkspaceNameOrDescription()
    {
        var workspaceName = GetRequiredFromStore(KEY_CURRENT_WORKSPACE);

        var workspacesPage = GetOrCreateWorkspacesPage();
        await workspacesPage.NavigateAsync();

        // Check if edit button is available
        var canEdit = await workspacesPage.IsEditAvailableAsync(workspaceName);
        _objectStore.Add(KEY_CAN_MAKE_DESIRED_CHANGES, (object)canEdit);
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
        _objectStore.Add(KEY_CAN_DELETE_WORKSPACE, (object)canDelete);
        _objectStore.Add(KEY_CURRENT_WORKSPACE, fullWorkspaceName);
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

        _objectStore.Add(KEY_CURRENT_WORKSPACE, fullWorkspaceName);
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
        _objectStore.Add(KEY_HAS_WORKSPACE_ACCESS, (object)hasAccess);
    }

    /// <summary>
    /// When: I try to add or edit transactions
    /// </summary>
    protected async Task WhenITryToAddOrEditTransactions()
    {
        var transactionsPage = GetOrCreateTransactionsPage();

        // Check if New Transaction button is available
        var canCreate = await transactionsPage.IsNewTransactionAvailableAsync();
        _objectStore.Add(KEY_CAN_MAKE_DESIRED_CHANGES, (object)canCreate);

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

        _objectStore.Add(KEY_LAST_TRANSACTION_PAYEE, testPayee);
    }

    /// <summary>
    /// When: I update that transaction
    /// </summary>
    protected async Task WhenIUpdateThatTransaction()
    {
        var payee = GetLastTransactionPayee();

        var transactionsPage = GetOrCreateTransactionsPage();
        var newDate = DateTime.Today.ToString("yyyy-MM-dd");
        var newPayee = "Updated " + payee;
        await transactionsPage.UpdateTransactionAsync(payee, newDate, newPayee, 200.00m);

        // Wait for the updated transaction to appear in the list
        // The loading spinner being hidden doesn't guarantee the list is fully rendered
        await transactionsPage.WaitForTransactionAsync(newPayee);

        _objectStore.Add(KEY_LAST_TRANSACTION_PAYEE, newPayee);
    }

    /// <summary>
    /// When: I delete that transaction
    /// </summary>
    protected async Task WhenIDeleteThatTransaction()
    {
        var payee = GetLastTransactionPayee();

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

        // Wait for the workspace selector to show a workspace name
        // After registration/login, the workspace might not be immediately visible
        await transactionsPage.WorkspaceSelector.CurrentWorkspaceName.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 5000 });

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

        // FAILS here (sometimes). We await the spinner being hidden, but maybe the list isn't updated yet.
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

        // Get the current workspace name
        var workspaceName = _objectStore.Get<string>(KEY_CURRENT_WORKSPACE);

        if (workspaceName != null)
        {
            await workspacesPage.NavigateAsync();
            var role = await workspacesPage.GetWorkspaceRoleAsync(workspaceName);
            Assert.That(role, Is.EqualTo("Owner"), "User should be Owner of the workspace");
        }
    }

    /// <summary>
    /// Then: I should have {expectedCount} workspaces available
    /// Then: I should see all {expectedCount} workspaces
    /// Then: the workspace count should be {expectedCount}
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
    /// Then: the workspace count should be {expectedCount}
    /// </summary>
    protected async Task ThenIShouldSeeAllWorkspaces(int expectedCount)
        => await ThenIShouldHaveWorkspacesAvailable(expectedCount);

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

        var expected = _objectStore.Get<string>(KEY_CURRENT_WORKSPACE);

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

        var currentWorkspaceName = _objectStore.Get<string>(KEY_CURRENT_WORKSPACE);
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
        var newName = GetRequiredFromStore(KEY_NEW_WORKSPACE_NAME);

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
        AssertCannotPerformAction(KEY_CAN_MAKE_DESIRED_CHANGES, "User should not be able to make desired changes");
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
        AssertCannotPerformAction(KEY_CAN_DELETE_WORKSPACE, "User should not be able to delete the workspace");
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
        AssertCannotPerformAction(KEY_HAS_WORKSPACE_ACCESS, "User should not have access to the workspace");
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
        var payee = GetLastTransactionPayee();

        var transactionsPage = GetOrCreateTransactionsPage();
        var hasTransaction = await transactionsPage.HasTransactionAsync(payee);
        Assert.That(hasTransaction, Is.True, "Transaction should be visible in the list");
    }

    /// <summary>
    /// Then: my changes should be saved
    /// </summary>
    protected async Task ThenMyChangesShouldBeSaved()
    {
        var payee = GetLastTransactionPayee();

        var transactionsPage = GetOrCreateTransactionsPage();

        // Prior operation awaits the loading spinner being being hidden.
        // Maybe that's not enough time for the updated transaction to appear.
        var hasTransaction = await transactionsPage.HasTransactionAsync(payee);

        Assert.That(hasTransaction, Is.True, "Updated transaction should be visible");
    }

    /// <summary>
    /// Then: it should be removed
    /// </summary>
    protected async Task ThenItShouldBeRemoved()
    {
        var payee = GetLastTransactionPayee();

        var transactionsPage = GetOrCreateTransactionsPage();
        var hasTransaction = await transactionsPage.HasTransactionAsync(payee);
        Assert.That(hasTransaction, Is.False, "Transaction should be removed from the list");
    }

    /// <summary>
    /// Then: I can add, edit, and delete transactions
    /// </summary>
    protected async Task ThenICanAddEditAndDeleteTransactions()
    {
        var workspaceName = GetRequiredFromStore(KEY_CURRENT_WORKSPACE);

        var transactionsPage = GetOrCreateTransactionsPage();
        await transactionsPage.NavigateAsync();

        // Select the workspace
        await transactionsPage.WorkspaceSelector.SelectWorkspaceAsync(workspaceName);
        await transactionsPage.WaitForLoadingCompleteAsync();

        // Check if New Transaction button is available
        var canCreate = await transactionsPage.IsNewTransactionAvailableAsync();
        Assert.That(canCreate, Is.True, "Owner should be able to create transactions");

        // Get the first transaction to check edit and delete permissions
        var transactionCount = await transactionsPage.GetTransactionCountAsync();
        Assert.That(transactionCount, Is.GreaterThan(0), "Should have at least one transaction to check permissions");

        // Get the payee name of the first transaction
        var payeeName = await transactionsPage.GetFirstTransactionPayeeAsync();
        Assert.That(payeeName, Is.Not.Null.And.Not.Empty, "First transaction should have a payee name");

        // Check if Edit button is available
        var canEdit = await transactionsPage.IsEditAvailableAsync(payeeName!);
        Assert.That(canEdit, Is.True, "Owner should be able to edit transactions");

        // Check if Delete button is available
        var canDelete = await transactionsPage.IsDeleteAvailableAsync(payeeName!);
        Assert.That(canDelete, Is.True, "Owner should be able to delete transactions");
    }

    /// <summary>
    /// Then: I can change workspace settings
    /// </summary>
    protected async Task ThenICanChangeWorkspaceSettings()
    {
        var workspaceName = GetRequiredFromStore(KEY_CURRENT_WORKSPACE);

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
        var workspaceName = GetRequiredFromStore(KEY_CURRENT_WORKSPACE);

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

    #endregion

    #region Helpers

    /// <summary>
    /// Get or create WorkspacesPage and store it in the object store
    /// </summary>
    private WorkspacesPage GetOrCreateWorkspacesPage() => GetOrCreatePage<WorkspacesPage>();

    /// <summary>
    /// Get or create TransactionsPage and store it in the object store
    /// </summary>
    private TransactionsPage GetOrCreateTransactionsPage() => GetOrCreatePage<TransactionsPage>();

    /// <summary>
    /// Get the current test username (with __TEST__ prefix)
    /// </summary>
    /// <remarks>
    /// Returns the FULL username with prefix. Priority order:
    /// 1. KEY_LOGGED_IN_AS (set after login)
    /// 2. KEY_PENDING_USER_CONTEXT (set by pre-login entitlement steps)
    /// 3. First user in _userCredentials (fallback)
    /// </remarks>
    private string GetCurrentTestUsername()
    {
        // Check if we have a logged-in user in object store (highest priority)
        if (_objectStore.Contains<string>(KEY_LOGGED_IN_AS))
        {
            return _objectStore.Get<string>(KEY_LOGGED_IN_AS);
        }

        // Check if we have a pending user context (for pre-login steps)
        if (_objectStore.Contains<string>(KEY_PENDING_USER_CONTEXT))
        {
            return _objectStore.Get<string>(KEY_PENDING_USER_CONTEXT)!;
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
