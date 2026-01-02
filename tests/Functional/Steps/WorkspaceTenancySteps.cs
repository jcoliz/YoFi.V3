using Microsoft.Playwright;
using YoFi.V3.Tests.Functional.Attributes;
using YoFi.V3.Tests.Functional.Pages;
using YoFi.V3.Tests.Functional.Helpers;
using YoFi.V3.Tests.Functional.Generated;
using YoFi.V3.Tests.Functional.Steps.Common;
using NUnit.Framework.Internal;

namespace YoFi.V3.Tests.Functional.Steps;

/// <summary>
/// Step definitions for Workspace Tenancy feature tests.
/// </summary>
/// <remarks>
/// <para><strong>TEST PREFIX HANDLING PATTERN:</strong></para>
/// <list type="bullet">
/// <item>DataTable values in feature files are user-readable (e.g., "alice", "Personal Budget")</item>
/// <item>The __TEST__ prefix is added immediately upon entering step methods using AddTestPrefix()</item>
/// <item>All internal storage (_userCredentials, _workspaceKeys, _objectStore) uses FULL names with prefix</item>
/// <item>This ensures consistency with what the Test Controller API and UI expect</item>
/// </list>
/// </remarks>
public abstract class WorkspaceTenancySteps : CommonThenSteps
{
    #region Object Store Keys

    protected const string KEY_LOGGED_IN_AS = "LoggedInAs";
    protected const string KEY_PENDING_USER_CONTEXT = "PendingUserContext"; // User context for pre-login steps
    protected const string KEY_CURRENT_WORKSPACE = "CurrentWorkspaceName";
    protected const string KEY_NEW_WORKSPACE_NAME = "NewWorkspaceName";
    protected const string KEY_LAST_TRANSACTION_PAYEE = "LastTransactionPayee";
    protected const string KEY_CAN_DELETE_WORKSPACE = "CanDeleteWorkspace";
    protected const string KEY_CAN_MAKE_DESIRED_CHANGES = "CanMakeDesiredChanges";
    protected const string KEY_HAS_WORKSPACE_ACCESS = "HasWorkspaceAccess";
    protected const string KEY_TRANSACTION_KEY = "TransactionKey";

    #endregion

    #region Helpers

    /// <summary>
    /// Adds the __TEST__ prefix to a name for test controller API calls
    /// </summary>
    protected static string AddTestPrefix(string name) => $"__TEST__{name}";

    /// <summary>
    /// Get or create a page object of type T from the object store
    /// </summary>
    protected T GetOrCreatePage<T>() where T : class
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
    protected string GetRequiredFromStore(string key)
    {
        return _objectStore.Get<string>(key)
            ?? throw new InvalidOperationException($"Required value '{key}' not found in object store");
    }

    /// <summary>
    /// Gets the current or newly renamed workspace name from object store
    /// </summary>
    protected string GetCurrentOrNewWorkspaceName()
    {
        return _objectStore.Contains<string>(KEY_NEW_WORKSPACE_NAME)
            ? _objectStore.Get<string>(KEY_NEW_WORKSPACE_NAME)!
            : _objectStore.Get<string>(KEY_CURRENT_WORKSPACE)!;
    }

    /// <summary>
    /// Gets the last transaction payee from object store
    /// </summary>
    protected string GetLastTransactionPayee()
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
    /// Creates multiple test users and clears existing test data.
    /// </summary>
    /// <param name="usersTable">DataTable containing user names (single column).</param>
    /// <remarks>
    /// Clears all existing test data first to ensure clean state. Stores full credentials in _userCredentials
    /// dictionary using raw usernames as keys.
    /// </remarks>
    [Given("these users exist")]
    protected async Task GivenTheseUsersExist(DataTable usersTable)
    {
        var friendlyNames = usersTable.ToSingleColumnList().ToList();

        // Generate credentials for all users (auto-tracked)
        var credentialsList = friendlyNames.Select(name => CreateTestUserCredentials(name)).ToList();

        // Create all users in bulk
        var created = await testControlClient.CreateUsersV2Async(credentialsList);

        // Update dictionary with server-populated IDs
        foreach (var createdUser in created)
        {
            _userCredentials[createdUser.ShortName] = createdUser;
        }
    }

    /// <summary>
    /// Logs in as the specified user.
    /// </summary>
    /// <param name="shortName">The username (without __TEST__ prefix).</param>
    /// <remarks>
    /// Adds __TEST__ prefix to match stored credentials. Navigates to login page,
    /// performs login, waits for redirect, and stores full username in object store.
    /// Requires user to have been created in Background section.
    /// </remarks>
    [Given("I am logged in as {username}")]
    protected async Task GivenIAmLoggedInAs(string shortName)
    {
        if (!_userCredentials.TryGetValue(shortName, out var cred))
        {
            throw new InvalidOperationException($"User '{shortName}' credentials not found. Ensure user was created in Background.");
        }

        var loginPage = new LoginPage(Page);
        await loginPage.NavigateAsync();

        await loginPage.LoginAsync(cred.Username, cred.Password);

        // Wait for redirect after successful login
        await Page.WaitForURLAsync(url => !url.Contains("/login"), new() { Timeout = 10000 });

        // Store FULL username for future reference
        _objectStore.Add(KEY_LOGGED_IN_AS, cred.Username);
    }

    /// <summary>
    /// Creates a workspace owned by the specified user.
    /// </summary>
    /// <param name="shortName">The username (without __TEST__ prefix).</param>
    /// <param name="workspaceName">The workspace name (without __TEST__ prefix).</param>
    /// <remarks>
    /// Adds __TEST__ prefix to both username and workspace name. Creates workspace
    /// via Test Control API with Owner role. Stores workspace key and sets current
    /// workspace context and pending user context in object store.
    /// </remarks>
    [Given("{username} owns a workspace called {workspaceName}")]
    [Given("{username} owns {workspaceName}")]
    protected async Task GivenUserOwnsAWorkspaceCalled(string shortName, string workspaceName)
    {
        var fullWorkspaceName = AddTestPrefix(workspaceName);

        if (!_userCredentials.TryGetValue(shortName, out var cred))
        {
            throw new InvalidOperationException($"User '{shortName}' credentials not found. Ensure user was created in Background.");
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
            result = await testControlClient.CreateWorkspaceForUserAsync(cred.Username, request);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating workspace '{fullWorkspaceName}' for user '{cred.Username}': {ex.Message}");
            throw;
        }

        // Store with FULL workspace name (what API returns)
        _workspaceKeys[result!.Name] = result.Key;
        _objectStore.Add(KEY_CURRENT_WORKSPACE, fullWorkspaceName);

        // Store pending user context for steps that need it before login
        _objectStore.Add(KEY_PENDING_USER_CONTEXT, cred.Username);
    }

    /// <summary>
    /// Sets up multiple workspaces with specified roles for a user.
    /// </summary>
    /// <param name="shortName">The username (without __TEST__ prefix).</param>
    /// <param name="workspacesTable">DataTable with columns: Workspace Name, My Role.</param>
    /// <remarks>
    /// Adds __TEST__ prefix to username and all workspace names. Creates workspaces
    /// via bulk setup API. Stores all workspace keys and sets pending user context.
    /// </remarks>
    [Given("{username} has access to these workspaces:")]
    protected async Task GivenUserHasAccessToTheseWorkspaces(string shortName, DataTable workspacesTable)
    {
        if (!_userCredentials.TryGetValue(shortName, out var cred))
        {
            throw new InvalidOperationException($"User '{shortName}' credentials not found. Ensure user was created in Background.");
        }

        // Add __TEST__ prefix to workspace names before API call
        var requests = workspacesTable.Select(row => new WorkspaceSetupRequest
        {
            Name = AddTestPrefix(row["Workspace Name"]),
            Description = $"Test workspace: {row["Workspace Name"]}",
            Role = row["My Role"]
        }).ToList();

        var results = await testControlClient.BulkWorkspaceSetupAsync(cred.Username, requests);

        // Store with FULL workspace names (what API returns)
        foreach (var result in results)
        {
            _workspaceKeys[result.Name] = result.Key;
        }

        // Store pending user context for steps that need it before login
        _objectStore.Add(KEY_PENDING_USER_CONTEXT, cred.Username);
    }

    /// <summary>
    /// Grants a user access to a workspace with Viewer role (default minimum access).
    /// </summary>
    /// <param name="shortName">The username (without __TEST__ prefix).</param>
    /// <param name="workspaceName">The workspace name (without __TEST__ prefix).</param>
    /// <remarks>
    /// Adds __TEST__ prefix to both parameters. Creates workspace access via bulk
    /// setup API with Viewer role. Stores workspace key and sets pending user context.
    /// </remarks>
    [Given("{username} has access to {workspaceName}")]
    protected async Task GivenUserHasAccessTo(string shortName, string workspaceName)
    {
        var fullWorkspaceName = AddTestPrefix(workspaceName);

        if (!_userCredentials.TryGetValue(shortName, out var cred))
        {
            throw new InvalidOperationException($"User '{shortName}' credentials not found. Ensure user was created in Background.");
        }

        var request = new WorkspaceSetupRequest
        {
            Name = fullWorkspaceName,
            Description = $"Test workspace: {workspaceName}",
            Role = "Viewer" // Default to minimum access level
        };

        var results = await testControlClient.BulkWorkspaceSetupAsync(cred.Username, new[] { request });
        var result = results.First();

        // Store with FULL workspace name (what API returns)
        _workspaceKeys[result.Name] = result.Key;

        // Store pending user context for steps that need it before login
        _objectStore.Add(KEY_PENDING_USER_CONTEXT, cred.Username);
    }

    /// <summary>
    /// Grants a user Editor role access to a workspace.
    /// </summary>
    /// <param name="shortName">The username (without __TEST__ prefix).</param>
    /// <param name="workspaceName">The workspace name (without __TEST__ prefix).</param>
    /// <remarks>
    /// Adds __TEST__ prefix to both parameters. Creates workspace access via bulk
    /// setup API with Editor role. Stores workspace key, sets current workspace,
    /// and sets pending user context.
    /// </remarks>
    [Given("{username} can edit data in {workspaceName}")]
    protected async Task GivenUserCanEditDataIn(string shortName, string workspaceName)
    {
        var fullWorkspaceName = AddTestPrefix(workspaceName);

        if (!_userCredentials.TryGetValue(shortName, out var cred))
        {
            throw new InvalidOperationException($"User '{shortName}' credentials not found. Ensure user was created in Background.");
        }

        var request = new WorkspaceSetupRequest
        {
            Name = fullWorkspaceName,
            Description = $"Test workspace: {workspaceName}",
            Role = "Editor"
        };

        var results = await testControlClient.BulkWorkspaceSetupAsync(cred.Username, new[] { request });
        var result = results.First();
        _workspaceKeys[result.Name] = result.Key;

        // Store current workspace for later reference
        _objectStore.Add(KEY_CURRENT_WORKSPACE, fullWorkspaceName);

        // Store pending user context for steps that need it before login
        _objectStore.Add(KEY_PENDING_USER_CONTEXT, cred.Username);
    }

    /// <summary>
    /// Grants a user Viewer role access to a workspace.
    /// </summary>
    /// <param name="shortName">The username (without __TEST__ prefix).</param>
    /// <param name="workspaceName">The workspace name (without __TEST__ prefix).</param>
    /// <remarks>
    /// Adds __TEST__ prefix to both parameters. Creates workspace access via bulk
    /// setup API with Viewer role. Stores workspace key, sets current workspace,
    /// and sets pending user context.
    /// </remarks>
    [Given("{username} can view data in {workspaceName}")]
    protected async Task GivenUserCanViewDataIn(string shortName, string workspaceName)
    {
        var fullWorkspaceName = AddTestPrefix(workspaceName);

        if (!_userCredentials.TryGetValue(shortName, out var cred))
        {
            throw new InvalidOperationException($"User '{shortName}' credentials not found. Ensure user was created in Background.");
        }

        var request = new WorkspaceSetupRequest
        {
            Name = fullWorkspaceName,
            Description = $"Test workspace: {workspaceName}",
            Role = "Viewer"
        };

        var results = await testControlClient.BulkWorkspaceSetupAsync(cred.Username, new[] { request });
        var result = results.First();
        _workspaceKeys[result.Name] = result.Key;

        // Store current workspace for later reference
        _objectStore.Add(KEY_CURRENT_WORKSPACE, fullWorkspaceName);

        // Store pending user context for steps that need it before login
        _objectStore.Add(KEY_PENDING_USER_CONTEXT, cred.Username);
    }

    /// <summary>
    /// Creates a workspace owned by a different user to test access denial.
    /// </summary>
    /// <param name="workspaceName">The workspace name (without __TEST__ prefix).</param>
    /// <param name="shortName">The username that should NOT have access (without __TEST__ prefix).</param>
    /// <remarks>
    /// Adds __TEST__ prefix to both parameters. Finds a different user from
    /// _userCredentials and creates the workspace for them. Used to test scenarios
    /// where a user attempts to access workspaces they don't own.
    /// </remarks>
    [Given("there is a workspace called {workspaceName} that {username} doesn't have access to")]
    protected async Task GivenThereIsAWorkspaceCalledThatUserDoesntHaveAccessTo(string workspaceName, string shortName)
    {
        // Get a different user from the credentials dictionary (not the specified user)
        var otherUser = _userCredentials.FirstOrDefault(kvp => kvp.Key != shortName);

        if (otherUser.Key == null)
        {
            throw new InvalidOperationException($"No other test users available besides '{shortName}'. Need at least one user besides the specified user.");
        }

        var fullWorkspaceName = AddTestPrefix(workspaceName);

        // Create workspace for the other user
        var request = new WorkspaceCreateRequest
        {
            Name = fullWorkspaceName,
            Description = $"Test workspace (no access for {shortName}): {workspaceName}",
            Role = "Owner"
        };

        var result = await testControlClient.CreateWorkspaceForUserAsync(otherUser.Value.Username, request);

        // Store with FULL workspace name (what API returns)
        _workspaceKeys[result.Name] = result.Key;
    }

    /// <summary>
    /// Seeds a workspace with test transactions.
    /// </summary>
    /// <param name="workspaceName">The workspace name (without __TEST__ prefix).</param>
    /// <param name="transactionCount">Number of transactions to create.</param>
    /// <remarks>
    /// Adds __TEST__ prefix to workspace name. Uses Test Control API to seed
    /// transactions with "Test Transaction" payee prefix. Requires workspace key
    /// to exist in _workspaceKeys dictionary.
    /// </remarks>
    [Given("{workspaceName} contains {transactionCount} transactions")]
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
    /// Creates multiple workspaces owned by different users.
    /// </summary>
    /// <param name="workspacesTable">DataTable with columns: Workspace Name, Owner.</param>
    /// <remarks>
    /// Adds __TEST__ prefix to all usernames and workspace names. Creates workspaces
    /// via Test Control API for each owner. Stores all workspace keys in _workspaceKeys.
    /// Used to set up multi-tenant scenarios.
    /// </remarks>
    [Given("there are other workspaces in the system")]
    protected async Task GivenThereAreOtherWorkspacesInTheSystem(DataTable workspacesTable)
    {
        foreach (var row in workspacesTable)
        {
            var workspaceName = row["Workspace Name"];
            var owner = row["Owner"];
            var fullWorkspaceName = AddTestPrefix(workspaceName);

            if (!_userCredentials.ContainsKey(owner))
            {
                throw new InvalidOperationException($"Owner '{owner}' credentials not found.");
            }

            var request = new WorkspaceCreateRequest
            {
                Name = fullWorkspaceName,
                Description = $"Test workspace for {owner}: {workspaceName}",
                Role = "Owner"
            };

            var result = await testControlClient.CreateWorkspaceForUserAsync(_userCredentials[owner].Username, request);

            // Store with FULL workspace name (what API returns)
            _workspaceKeys[result.Name] = result.Key;
        }
    }

    #endregion

    #region Steps: WHEN

    /// <summary>
    /// Registers a new user and logs them in.
    /// </summary>
    /// <param name="shortName">The username (without __TEST__ prefix).</param>
    /// <remarks>
    /// Adds __TEST__ prefix, generates credentials, navigates to registration page,
    /// completes registration, stores credentials, and performs login. New users
    /// automatically get a personalized workspace named after their username.
    /// </remarks>
    [When("a new user {username} registers and logs in")]
    protected async Task WhenANewUserRegistersAndLogsIn(string shortName)
    {
        // Generate credentials using the shared helper
        var credentials = CreateTestUserCredentials(shortName);

        // Navigate to register page
        var registerPage = new RegisterPage(Page);
        await registerPage.NavigateAsync();

        await registerPage.RegisterAsync(credentials.Email, credentials.Username, credentials.Password);

        // Store credentials with short username as key
        _userCredentials[shortName] = credentials;

        // New users get a workspace containing their name
        // NOTE: This "works" because workspace lookups are substring lookups
        _objectStore.Add(KEY_CURRENT_WORKSPACE, credentials.Username);

        // Click the "Continue" button to proceed after registration
        await registerPage.ContinueButton.ClickAsync();

        // Now we are on the login page and we should login
        var loginPage = new LoginPage(Page);
        await loginPage.LoginAsync(credentials.Username, credentials.Password);
    }

    /// <summary>
    /// Creates a new workspace with specified name and description.
    /// </summary>
    /// <param name="name">The workspace name (without __TEST__ prefix).</param>
    /// <param name="description">The workspace description.</param>
    /// <remarks>
    /// Adds __TEST__ prefix to name. Navigates to workspaces page, creates workspace,
    /// waits for it to appear in the list, and stores it as current workspace.
    /// </remarks>
    [When("I create a new workspace called {name} for {description}")]
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
    /// Creates a new workspace with specified name and default description.
    /// </summary>
    /// <param name="name">The workspace name (without __TEST__ prefix).</param>
    /// <remarks>
    /// Convenience method that calls WhenICreateANewWorkspaceCalledFor with a
    /// generated description. Adds __TEST__ prefix to name.
    /// </remarks>
    [When("I create a workspace called {name}")]
    protected async Task WhenICreateAWorkspaceCalled(string name)
    {
        await WhenICreateANewWorkspaceCalledFor(name, $"__TEST__ Test workspace: {name}");
    }

    /// <summary>
    /// Navigates to the workspaces list page.
    /// </summary>
    /// <remarks>
    /// Simply navigates to the workspaces page to view available workspaces.
    /// TODO: Add verification that the page loaded successfully.
    /// </remarks>
    [When("I view my workspace list")]
    protected async Task WhenIViewMyWorkspaceList()
    {
        // TODO: Use WorkspacesPage page object
        var workspacesPage = GetOrCreateWorkspacesPage();
        await workspacesPage.NavigateAsync();
    }

    /// <summary>
    /// Views the details of a specific workspace.
    /// </summary>
    /// <param name="workspaceName">The workspace name (without __TEST__ prefix).</param>
    /// <remarks>
    /// Adds __TEST__ prefix. Navigates to workspaces page, selects the workspace
    /// in the workspace selector, and stores it as current workspace.
    /// </remarks>
    [When("I view the details of {workspaceName}")]
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
    /// Renames the current workspace.
    /// </summary>
    /// <param name="newName">The new workspace name (without __TEST__ prefix).</param>
    /// <remarks>
    /// Adds __TEST__ prefix to new name. Gets current workspace from object store,
    /// performs rename via workspaces page, waits for updated name to appear, and
    /// updates current workspace context.
    /// </remarks>
    [When("I rename it to {newName}")]
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

        // Wait for the renamed workspace card to appear in the list
        // The loading spinner being hidden doesn't guarantee the list is fully rendered
        await workspacesPage.WaitForWorkspaceAsync(fullNewName);

        _objectStore.Add(KEY_CURRENT_WORKSPACE, fullNewName);
    }

    /// <summary>
    /// Updates the description of the current workspace.
    /// </summary>
    /// <param name="newDescription">The new workspace description.</param>
    /// <remarks>
    /// Gets current or newly renamed workspace name from object store and updates
    /// its description while keeping the same name. Waits for update to complete.
    /// </remarks>
    [When("I update the description to {newDescription}")]
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
    /// Attempts to change workspace name or description (permission check).
    /// </summary>
    /// <remarks>
    /// Navigates to workspaces page and checks if edit button is available for
    /// current workspace. Stores permission check result in object store for
    /// later assertion.
    /// </remarks>
    [When("I try to change the workspace name or description")]
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
    /// Deletes a workspace.
    /// </summary>
    /// <param name="workspaceName">The workspace name (without __TEST__ prefix).</param>
    /// <remarks>
    /// Adds __TEST__ prefix. Navigates to workspaces page and performs delete
    /// operation. Used for positive test cases where deletion is expected to succeed.
    /// </remarks>
    [When("I delete {workspaceName}")]
    protected async Task WhenIDelete(string workspaceName)
    {
        var fullWorkspaceName = AddTestPrefix(workspaceName);
        var workspacesPage = GetOrCreateWorkspacesPage();

        await workspacesPage.NavigateAsync();
        await workspacesPage.DeleteWorkspaceAsync(fullWorkspaceName);
    }

    /// <summary>
    /// Attempts to delete a workspace (permission check).
    /// </summary>
    /// <param name="workspaceName">The workspace name (without __TEST__ prefix).</param>
    /// <remarks>
    /// Adds __TEST__ prefix. Navigates to workspaces page and checks if delete
    /// button is available. Stores permission check result for later assertion.
    /// Used for negative test cases where deletion should be blocked.
    /// </remarks>
    [When("I try to delete {workspaceName}")]
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
    /// Views transactions in a specific workspace.
    /// </summary>
    /// <param name="workspaceName">The workspace name (without __TEST__ prefix).</param>
    /// <remarks>
    /// Adds __TEST__ prefix. Navigates to transactions page, selects workspace,
    /// waits for loading to complete, and stores as current workspace.
    /// </remarks>
    [When("I view transactions in {workspaceName}")]
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
    /// Attempts to view transactions in a workspace (permission check).
    /// </summary>
    /// <param name="workspaceName">The workspace name (without __TEST__ prefix).</param>
    /// <remarks>
    /// Adds __TEST__ prefix. Navigates to transactions page and checks if workspace
    /// is in the available workspaces list. Stores access check result for later
    /// assertion. Used for negative test cases.
    /// </remarks>
    [When("I try to view transactions in {workspaceName}")]
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
    /// Attempts to add or edit transactions (permission check).
    /// </summary>
    /// <remarks>
    /// Checks if New Transaction button is available on transactions page. Stores
    /// permission check result for later assertion. Used for role-based access tests.
    /// TODO: Add edit button availability check for existing transactions.
    /// </remarks>
    [When("I try to add or edit transactions")]
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
    /// Adds a test transaction to a workspace.
    /// </summary>
    /// <param name="workspaceName">The workspace name (without __TEST__ prefix).</param>
    /// <remarks>
    /// Adds __TEST__ prefix. Navigates to transactions page, selects workspace,
    /// creates transaction with today's date, unique payee name, and $100 amount.
    /// Stores payee name in object store for later reference.
    /// </remarks>
    [When("I add a transaction to {workspaceName}")]
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
    /// Updates the previously added transaction.
    /// </summary>
    /// <remarks>
    /// Retrieves last transaction payee from object store, opens edit modal,
    /// updates payee name by prepending "Updated ", submits form, waits for
    /// transaction to appear, and stores new payee name.
    /// </remarks>
    [When("I update that transaction")]
    protected async Task WhenIUpdateThatTransaction()
    {
        var payee = GetLastTransactionPayee();

        var transactionsPage = GetOrCreateTransactionsPage();
        var newPayee = "Updated " + payee;

        // Use quick edit workflow (only Payee and Memo fields available in modal from transactions page)
        await transactionsPage.OpenEditModalAsync(payee);
        await transactionsPage.FillEditPayeeAsync(newPayee);
        await transactionsPage.SubmitEditFormAsync();

        // Wait for the updated transaction to appear in the list
        // The loading spinner being hidden doesn't guarantee the list is fully rendered

        var key = _objectStore.Get<string>(KEY_TRANSACTION_KEY);
        await transactionsPage.WaitForTransactionRowByKeyAsync(Guid.Parse(key));

        // Update the stored payee name
        _objectStore.Add(KEY_LAST_TRANSACTION_PAYEE, newPayee);
    }

    /// <summary>
    /// Deletes the previously added/updated transaction.
    /// </summary>
    /// <remarks>
    /// Retrieves last transaction payee from object store and performs delete
    /// operation via transactions page.
    /// </remarks>
    [When("I delete that transaction")]
    protected async Task WhenIDeleteThatTransaction()
    {
        var payee = GetLastTransactionPayee();

        var transactionsPage = GetOrCreateTransactionsPage();

        // Wait for the transaction to appear before attempting deletion
        // The loading spinner being hidden doesn't guarantee the list is fully rendered
        var key = _objectStore.Get<string>(KEY_TRANSACTION_KEY);
        await transactionsPage.WaitForTransactionRowByKeyAsync(Guid.Parse(key));

        // Now it should be safe to do the delete
        await transactionsPage.DeleteTransactionAsync(payee);
    }

    #endregion

    #region Steps: THEN

    /// <summary>
    /// Verifies that the user has at least one workspace available.
    /// </summary>
    /// <remarks>
    /// Navigates to transactions page and checks that the "no workspace" message
    /// is not visible, indicating at least one workspace exists.
    /// </remarks>
    [Then("user should have a workspace ready to use")]
    protected async Task ThenUserShouldHaveAWorkspaceReadyToUse()
    {
        var transactionsPage = GetOrCreateTransactionsPage();

        // AB#1981: Call stack here
        await transactionsPage.NavigateAsync();

        // Get the current workspace name
        var noWorkspaceMessage = await transactionsPage.WorkspaceSelector.NoWorkspaceMessage.IsVisibleAsync();

        Assert.That(noWorkspaceMessage, Is.False, "User should have at least one workspace available");
    }

    /// <summary>
    /// Verifies that the workspace name contains the expected personalized name.
    /// </summary>
    /// <param name="expectedName">The expected name portion (without __TEST__ prefix).</param>
    /// <remarks>
    /// Adds __TEST__ prefix. Navigates to transactions page, waits for workspace
    /// selector to show a name (up to 5 seconds), and verifies name contains
    /// expected value. Used after registration to verify personalized workspace creation.
    /// </remarks>
    [Then("the workspace should be personalized with the name {expectedName}")]
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
    /// Verifies that a workspace appears in the user's workspace list.
    /// </summary>
    /// <param name="workspaceName">The workspace name (without __TEST__ prefix).</param>
    /// <remarks>
    /// Adds __TEST__ prefix. Checks workspaces page for presence of workspace.
    /// Note: May occasionally fail if list hasn't fully updated after spinner hides.
    /// </remarks>
    [Then("I should see {workspaceName} in my workspace list")]
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
    /// Verifies that the user has Owner permissions on the current workspace.
    /// </summary>
    /// <remarks>
    /// Retrieves current workspace from object store, navigates to workspaces page,
    /// and verifies the user's role is "Owner".
    /// </remarks>
    [Then("I should be able to manage that workspace")]
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
    /// Verifies that the user has exactly the expected number of workspaces.
    /// </summary>
    /// <param name="expectedCount">The expected workspace count.</param>
    /// <remarks>
    /// Navigates to workspaces page and counts visible workspaces.
    /// Supports multiple step phrasings via additional attributes on
    /// ThenIShouldSeeAllWorkspaces.
    /// </remarks>
    [Then("I should have {expectedCount} workspaces available")]
    [Then("the workspace count should be {expectedCount}")]
    protected async Task ThenIShouldHaveWorkspacesAvailable(int expectedCount)
    {
        var workspacesPage = GetOrCreateWorkspacesPage();
        await workspacesPage.NavigateAsync();

        var actualCount = await workspacesPage.GetWorkspaceCountAsync();
        Assert.That(actualCount, Is.EqualTo(expectedCount),
            $"Should have exactly {expectedCount} workspaces");
    }

    /// <summary>
    /// Verifies that the user can switch between at least 2 workspaces.
    /// </summary>
    /// <remarks>
    /// Navigates to transactions page and checks that workspace selector shows
    /// at least 2 available workspaces, confirming workspace independence.
    /// </remarks>
    [Then("I can work with either workspace independently")]
    protected async Task ThenICanWorkWithEitherWorkspaceIndependently()
    {
        // Verify we can switch between workspaces
        var transactionsPage = GetOrCreateTransactionsPage();
        await transactionsPage.NavigateAsync();

        var availableWorkspaces = await transactionsPage.WorkspaceSelector.GetAvailableWorkspacesAsync();
        Assert.That(availableWorkspaces.Length, Is.GreaterThanOrEqualTo(2), "Should have at least 2 workspaces available");
    }

    /// <summary>
    /// Alias for ThenIShouldHaveWorkspacesAvailable - verifies workspace count.
    /// </summary>
    /// <param name="expectedCount">The expected workspace count.</param>
    [Then("I should see all {expectedCount} workspaces")]
    protected async Task ThenIShouldSeeAllWorkspaces(int expectedCount)
        => await ThenIShouldHaveWorkspacesAvailable(expectedCount);

    /// <summary>
    /// Verifies that role badges are displayed for all workspaces.
    /// </summary>
    /// <remarks>
    /// Navigates to workspaces page and verifies each workspace in _workspaceKeys
    /// has a valid role badge (Owner, Editor, or Viewer). Confirms at least one
    /// workspace exists.
    /// </remarks>
    [Then("I should see what I can do in each workspace")]
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
    /// Verifies that the workspace selector shows the expected workspace.
    /// </summary>
    /// <remarks>
    /// Retrieves expected workspace name from object store (KEY_CURRENT_WORKSPACE)
    /// and verifies the workspace selector displays it correctly.
    /// </remarks>
    [Then("I should see the workspace information")]
    protected async Task ThenIShouldSeeTheWorkspaceInformation()
    {
        var workspacesPage = GetOrCreateWorkspacesPage();

        var expected = _objectStore.Get<string>(KEY_CURRENT_WORKSPACE);

        // Verify workspace selector shows expected information
        var workspaceName = await workspacesPage.WorkspaceSelector.GetCurrentWorkspaceNameAsync();
        Assert.That(workspaceName, Is.EqualTo(expected), "Workspace information should be visible");
    }

    /// <summary>
    /// Verifies that the workspace displays a valid creation date.
    /// </summary>
    /// <remarks>
    /// Retrieves current workspace from object store, navigates to workspaces page,
    /// gets the created date from workspace card, and verifies it's a valid DateTime.
    /// </remarks>
    [Then("I should see when it was created")]
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
    /// Verifies that workspace rename or description update was successful.
    /// </summary>
    /// <remarks>
    /// Retrieves new workspace name from object store (KEY_NEW_WORKSPACE_NAME),
    /// navigates to workspaces page, and verifies the updated workspace is visible.
    /// </remarks>
    [Then("the workspace should reflect the changes")]
    protected async Task ThenTheWorkspaceShouldReflectTheChanges()
    {
        var newName = GetRequiredFromStore(KEY_NEW_WORKSPACE_NAME);

        var workspacesPage = GetOrCreateWorkspacesPage();
        await workspacesPage.NavigateAsync();

        var hasWorkspace = await workspacesPage.HasWorkspaceAsync(newName);
        Assert.That(hasWorkspace, Is.True, $"Updated workspace '{newName}' should be visible");
    }

    /// <summary>
    /// Verifies that the user cannot make desired changes (negative permission check).
    /// </summary>
    /// <remarks>
    /// Retrieves permission check result from object store (KEY_CAN_MAKE_DESIRED_CHANGES)
    /// and asserts it's false. Used for role-based access control tests.
    /// </remarks>
    [Then("I should not be able to make those changes")]
    protected async Task ThenIShouldNotBeAbleToMakeThoseChanges()
    {
        AssertCannotPerformAction(KEY_CAN_MAKE_DESIRED_CHANGES, "User should not be able to make desired changes");
    }

    /// <summary>
    /// Verifies that a workspace no longer appears in the user's list after deletion.
    /// </summary>
    /// <param name="workspaceName">The workspace name (without __TEST__ prefix).</param>
    /// <remarks>
    /// Adds __TEST__ prefix. Checks workspaces page and verifies workspace is absent.
    /// </remarks>
    [Then("{workspaceName} should no longer appear in my list")]
    protected async Task ThenShouldNoLongerAppearInMyList(string workspaceName)
    {
        var fullWorkspaceName = AddTestPrefix(workspaceName);

        var workspacesPage = GetOrCreateWorkspacesPage();
        var hasWorkspace = await workspacesPage.HasWorkspaceAsync(fullWorkspaceName);
        Assert.That(hasWorkspace, Is.False,
            $"Workspace '{fullWorkspaceName}' should not be in the list");
    }

    /// <summary>
    /// Verifies that the user cannot delete the workspace (negative permission check).
    /// </summary>
    /// <remarks>
    /// Retrieves permission check result from object store (KEY_CAN_DELETE_WORKSPACE)
    /// and asserts it's false. Used for role-based deletion tests.
    /// </remarks>
    [Then("the workspace should remain intact")]
    protected async Task ThenTheWorkspaceShouldRemainIntact()
    {
        AssertCannotPerformAction(KEY_CAN_DELETE_WORKSPACE, "User should not be able to delete the workspace");
    }

    /// <summary>
    /// Verifies that exactly the expected number of transactions are displayed.
    /// </summary>
    /// <param name="expectedCount">The expected transaction count.</param>
    /// <remarks>
    /// Counts transactions on current transactions page and asserts exact match.
    /// </remarks>
    [Then("I should see exactly {expectedCount} transactions")]
    protected async Task ThenIShouldSeeExactlyTransactions(int expectedCount)
    {
        var transactionsPage = GetOrCreateTransactionsPage();
        var actualCount = await transactionsPage.GetTransactionCountAsync();
        Assert.That(actualCount, Is.EqualTo(expectedCount), $"Expected exactly {expectedCount} transactions");
    }

    /// <summary>
    /// Verifies that the currently viewed transactions belong to the specified workspace.
    /// </summary>
    /// <param name="workspaceName">The workspace name (without __TEST__ prefix).</param>
    /// <remarks>
    /// Adds __TEST__ prefix. Checks that workspace selector shows the expected
    /// workspace, confirming transaction-workspace association.
    /// </remarks>
    [Then("they should all be from {workspaceName} workspace")]
    protected async Task ThenTheyShouldAllBeFromWorkspace(string workspaceName)
    {
        var fullWorkspaceName = AddTestPrefix(workspaceName);

        // Verify we're viewing the correct workspace
        var transactionsPage = GetOrCreateTransactionsPage();
        var currentWorkspace = await transactionsPage.WorkspaceSelector.GetCurrentWorkspaceNameAsync();
        Assert.That(currentWorkspace, Is.EqualTo(fullWorkspaceName), $"Should be viewing '{fullWorkspaceName}' workspace");
    }

    /// <summary>
    /// Verifies that no transactions from the specified workspace are visible.
    /// </summary>
    /// <param name="workspaceName">The workspace name (without __TEST__ prefix).</param>
    /// <remarks>
    /// Adds __TEST__ prefix. Verifies workspace selector does NOT show the specified
    /// workspace, confirming workspace isolation.
    /// </remarks>
    [Then("I should not see any transactions from {workspaceName}")]
    protected async Task ThenIShouldNotSeeAnyTransactionsFrom(string workspaceName)
    {
        var fullWorkspaceName = AddTestPrefix(workspaceName);

        // Transactions are workspace-isolated, so if we're in a different workspace, we won't see them
        var transactionsPage = GetOrCreateTransactionsPage();
        var currentWorkspace = await transactionsPage.WorkspaceSelector.GetCurrentWorkspaceNameAsync();
        Assert.That(currentWorkspace, Is.Not.EqualTo(fullWorkspaceName), $"Should not be viewing '{fullWorkspaceName}' workspace");
    }

    /// <summary>
    /// Verifies that the user cannot access workspace data (negative permission check).
    /// </summary>
    /// <remarks>
    /// Retrieves permission check result from object store (KEY_HAS_WORKSPACE_ACCESS)
    /// and asserts it's false. Used for workspace access control tests.
    /// </remarks>
    [Then("I should not be able to access that data")]
    protected async Task ThenIShouldNotBeAbleToAccessThatData()
    {
        AssertCannotPerformAction(KEY_HAS_WORKSPACE_ACCESS, "User should not have access to the workspace");
    }

    /// <summary>
    /// Verifies that at least some transactions are visible.
    /// </summary>
    /// <remarks>
    /// Counts transactions on current page and asserts count is greater than zero.
    /// </remarks>
    [Then("I should see the transactions")]
    protected async Task ThenIShouldSeeTheTransactions()
    {
        var transactionsPage = GetOrCreateTransactionsPage();
        var count = await transactionsPage.GetTransactionCountAsync();
        Assert.That(count, Is.GreaterThan(0), "Should see some transactions");
    }

    /// <summary>
    /// Verifies that the last added transaction is visible in the list.
    /// </summary>
    /// <remarks>
    /// Retrieves last transaction payee from object store, waits for it to appear
    /// in the list, and verifies visibility. Includes explicit wait to handle
    /// UI update timing.
    /// </remarks>
    [Then("the transaction should be saved successfully")]
    protected async Task ThenTheTransactionShouldBeSavedSuccessfully()
    {
        var payee = GetLastTransactionPayee();

        var transactionsPage = GetOrCreateTransactionsPage();

        // Wait for the transaction to appear in the list
        // The loading spinner being hidden doesn't guarantee the list is fully rendered
        await transactionsPage.WaitForTransactionAsync(payee);

        // Store the transaction's test ID for later reference. This makes it much
        // more straightforward to wait for the updated transaction in future steps.
        var transactionKey = await transactionsPage.GetTransactionKeyByPayeeAsync(payee);
        _objectStore.Add(KEY_TRANSACTION_KEY, transactionKey.ToString());

        // Confirm that the transaction is really there now
        var hasTransaction = await transactionsPage.HasTransactionAsync(payee);
        Assert.That(hasTransaction, Is.True, "Transaction should be visible in the list");
    }

    /// <summary>
    /// Verifies that transaction update was saved successfully.
    /// </summary>
    /// <remarks>
    /// Retrieves last (updated) transaction payee from object store and verifies
    /// it's visible in the list. Note: May need additional wait time for UI updates.
    /// </remarks>
    [Then("my changes should be saved")]
    protected async Task ThenMyChangesShouldBeSaved()
    {
        var payee = GetLastTransactionPayee();

        var transactionsPage = GetOrCreateTransactionsPage();

        // Prior operation awaits the loading spinner being being hidden.
        // We can't rely on that alone to guarantee the updated transaction is visible,
        // so we add an explicit wait here. Last time we interacted with the transaction,
        // we stored its key for easy reference.

        var key = _objectStore.Get<string>(KEY_TRANSACTION_KEY);
        await transactionsPage.WaitForTransactionRowByKeyAsync(Guid.Parse(key));

        // CHECK:Explicitly refresh the cached transaction list to ensure updated data is shown
        // I don't think we need to do this anymore. Wait for transaction clears the cache
        //await transactionsPage.ReloadTransactionTableDataAsync();

        var hasTransaction = await transactionsPage.HasTransactionAsync(payee);

        Assert.That(hasTransaction, Is.True, "Updated transaction should be visible");
    }

    /// <summary>
    /// Verifies that the transaction was removed from the list.
    /// </summary>
    /// <remarks>
    /// Retrieves last transaction payee from object store and verifies it's no
    /// longer visible after deletion.
    /// </remarks>
    [Then("it should be removed")]
    protected async Task ThenItShouldBeRemoved()
    {
        var payee = GetLastTransactionPayee();

        var transactionsPage = GetOrCreateTransactionsPage();
        var hasTransaction = await transactionsPage.HasTransactionAsync(payee);
        Assert.That(hasTransaction, Is.False, "Transaction should be removed from the list");
    }

    /// <summary>
    /// Verifies that Owner role can add, edit, and delete transactions.
    /// </summary>
    /// <remarks>
    /// Comprehensive permission check: navigates to transactions page, selects
    /// current workspace, verifies New Transaction button is available, confirms
    /// at least one transaction exists, and verifies Edit and Delete buttons are
    /// available for the first transaction.
    /// </remarks>
    [Then("I can add, edit, and delete transactions")]
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
    /// Verifies that Owner role can change workspace settings.
    /// </summary>
    /// <remarks>
    /// Retrieves current workspace from object store, navigates to workspaces page,
    /// and verifies Edit button is available.
    /// </remarks>
    [Then("I can change workspace settings")]
    protected async Task ThenICanChangeWorkspaceSettings()
    {
        var workspaceName = GetRequiredFromStore(KEY_CURRENT_WORKSPACE);

        var workspacesPage = GetOrCreateWorkspacesPage();
        await workspacesPage.NavigateAsync();

        var canEdit = await workspacesPage.IsEditAvailableAsync(workspaceName);
        Assert.That(canEdit, Is.True, "Owner should be able to edit workspace settings");
    }

    /// <summary>
    /// Verifies that Owner role can delete the workspace.
    /// </summary>
    /// <remarks>
    /// Retrieves current workspace from object store, navigates to workspaces page,
    /// and verifies Delete button is available.
    /// </remarks>
    [Then("I can remove the workspace if needed")]
    protected async Task ThenICanRemoveTheWorkspaceIfNeeded()
    {
        var workspaceName = GetRequiredFromStore(KEY_CURRENT_WORKSPACE);

        var workspacesPage = GetOrCreateWorkspacesPage();
        await workspacesPage.NavigateAsync();

        var canDelete = await workspacesPage.IsDeleteAvailableAsync(workspaceName);
        Assert.That(canDelete, Is.True, "Owner should be able to delete workspace");
    }

    /// <summary>
    /// Verifies that only one specific workspace is visible in the list.
    /// </summary>
    /// <param name="workspaceName">The workspace name (without __TEST__ prefix).</param>
    /// <remarks>
    /// Adds __TEST__ prefix. Navigates to workspaces page, verifies count is exactly 1,
    /// and confirms it's the expected workspace. Used for access isolation tests.
    /// </remarks>
    [Then("I should see only {workspaceName} in my list")]
    protected async Task ThenIShouldSeeOnlyInMyList(string workspaceName)
    {
        var fullWorkspaceName = AddTestPrefix(workspaceName);

        var workspacesPage = GetOrCreateWorkspacesPage();
        await workspacesPage.NavigateAsync();

        // Verify exactly one workspace is visible
        var count = await workspacesPage.GetWorkspaceCountAsync();
        Assert.That(count, Is.EqualTo(1), "Should see exactly one workspace");

        // Verify it's the expected workspace
        var hasWorkspace = await workspacesPage.HasWorkspaceAsync(fullWorkspaceName);
        Assert.That(hasWorkspace, Is.True,
            $"The only workspace should be '{fullWorkspaceName}'");
    }

    /// <summary>
    /// Verifies that a specific workspace is not visible in the list.
    /// </summary>
    /// <param name="workspaceName">The workspace name (without __TEST__ prefix).</param>
    /// <remarks>
    /// Adds __TEST__ prefix. Checks workspaces page and verifies workspace is absent.
    /// Used for access control and isolation tests.
    /// </remarks>
    [Then("I should not see {workspaceName} in my list")]
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
        return firstUser.Value.Username;
    }

    #endregion
}
