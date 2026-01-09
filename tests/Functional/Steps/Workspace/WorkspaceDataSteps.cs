using YoFi.V3.Tests.Functional.Attributes;
using YoFi.V3.Tests.Functional.Generated;
using YoFi.V3.Tests.Functional.Infrastructure;

namespace YoFi.V3.Tests.Functional.Steps.Workspace;

/// <summary>
/// Step definitions for workspace test data setup and entitlements.
/// </summary>
/// <remarks>
/// Handles test data preparation via Test Control API:
/// - Setting up workspaces with specific roles for users
/// - Seeding transactions into workspaces
/// - Creating multi-workspace test scenarios
/// - Bulk workspace setup operations
///
/// These steps use the Test Control API (not UI) to prepare test state
/// before executing the actual test scenario.
/// </remarks>
public class WorkspaceDataSteps : WorkspaceStepsBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceDataSteps"/> class.
    /// </summary>
    /// <param name="context">Test context providing access to test infrastructure.</param>
    public WorkspaceDataSteps(ITestContext context) : base(context)
    {
    }

    #region Steps: GIVEN

    /// <summary>
    /// Sets up a logged-in user with Editor role in a test workspace.
    /// </summary>
    /// <remarks>
    /// Comprehensive setup step that: clears existing test data, creates an editor user,
    /// creates a test workspace with Editor role, stores credentials and workspace key,
    /// and performs login. Used as the standard starting point for transaction record tests.
    ///
    /// Provides Objects:
    /// - CurrentWorkspace
    /// </remarks>
    [Given("I am logged in as a user with \"Editor\" role")]
    [ProvidesObjects(ObjectStoreKeys.CurrentWorkspace)]
    public async Task GivenIAmLoggedInAsAUserWithEditorRole()
    {
        // Given: Clear existing test data
        // TODO: Remove! We are not clearing all test data between tests anymore.
        await _context.TestControlClient.DeleteAllTestDataAsync();

        // And: Create user context for an Editor user
        var friendlyName = "editor-user";

        // And: Create test user credentials on server (auto-tracked for cleanup)
        var credentials = await _context.CreateTestUserCredentialsOnServer(friendlyName);

        // And: Create the workspace for the user via test control API
        var workspaceName = "Test Workspace";
        var fullWorkspaceName = AddTestPrefix(workspaceName);

        var request = new Generated.WorkspaceSetupRequest
        {
            Name = fullWorkspaceName,
            Description = $"Test workspace: {workspaceName}",
            Role = "Editor"
        };

        var results = await _context.TestControlClient.BulkWorkspaceSetupAsync(credentials.Username, new[] { request });
        var result = results.First();

        // And: Track workspace for cleanup
        _context.TrackCreatedWorkspace(result.Name, result.Key);
        _context.ObjectStore.Add(ObjectStoreKeys.CurrentWorkspace, fullWorkspaceName);

        // When: Login as the user (requires AuthSteps)
        var authSteps = new AuthSteps(_context);
        await authSteps.GivenIAmLoggedInAs(friendlyName);
    }

    /// <summary>
    /// Creates an active workspace with Editor role for the current user.
    /// </summary>
    /// <param name="workspaceName">The workspace name (without __TEST__ prefix).</param>
    /// <remarks>
    /// Adds __TEST__ prefix to workspace name. Creates workspace via Test Control API
    /// with Editor role. Stores workspace key and sets current workspace context.
    ///
    /// Provides Objects:
    /// - CurrentWorkspace
    /// </remarks>
    [Given("I have an active workspace {workspaceName}")]
    [ProvidesObjects(ObjectStoreKeys.CurrentWorkspace)]
    public async Task GivenIHaveAnActiveWorkspace(string workspaceName)
    {
        var fullWorkspaceName = AddTestPrefix(workspaceName);

        // Get user credentials from context (created by GivenIHaveAnExistingAccount)
        var cred = _context.GetUserCredentials("I");

        var request = new Generated.WorkspaceCreateRequest
        {
            Name = fullWorkspaceName,
            Description = $"Test workspace: {workspaceName}",
            Role = "Editor"
        };

        Generated.TenantResultDto? result;
        try
        {
            result = await _context.TestControlClient.CreateWorkspaceForUserAsync(cred.Username, request);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating workspace '{fullWorkspaceName}' for user '{cred.Username}': {ex.Message}");
            throw;
        }

        // Track workspace for cleanup and store current workspace name
        _context.TrackCreatedWorkspace(result!.Name, result.Key);
        _context.ObjectStore.Add(ObjectStoreKeys.CurrentWorkspace, fullWorkspaceName);
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
    ///
    /// Provides Objects:
    /// - CurrentWorkspace
    /// - PendingUserContext
    /// </remarks>
    [Given("{username} owns a workspace called {workspaceName}")]
    [Given("{username} owns {workspaceName}")]
    [ProvidesObjects(ObjectStoreKeys.CurrentWorkspace, ObjectStoreKeys.PendingUserContext)]
    public async Task GivenUserOwnsAWorkspaceCalled(string shortName, string workspaceName)
    {
        var fullWorkspaceName = AddTestPrefix(workspaceName);

        TestUserCredentials? cred;
        try
        {
            // Ensure user credentials exist in context
            cred = _context.GetUserCredentials(shortName);
        }
        catch (KeyNotFoundException)
        {
            // Create user credentials on server if not found
            cred = await _context.CreateTestUserCredentialsOnServer(shortName);
        }

        var request = new Generated.WorkspaceCreateRequest
        {
            Name = fullWorkspaceName,
            Description = $"Test workspace: {workspaceName}",
            Role = "Owner"
        };

        TenantResultDto? result;
        try
        {
            result = await _context.TestControlClient.CreateWorkspaceForUserAsync(cred.Username, request);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating workspace '{fullWorkspaceName}' for user '{cred.Username}': {ex.Message}");
            throw;
        }

        // Track workspace for cleanup and store current workspace name
        _context.TrackCreatedWorkspace(result!.Name, result.Key);
        _context.ObjectStore.Add(ObjectStoreKeys.CurrentWorkspace, fullWorkspaceName);

        // Store pending user context for steps that need it before login
        _context.ObjectStore.Add(ObjectStoreKeys.PendingUserContext, cred.Username);
    }

    /// <summary>
    /// Sets up multiple workspaces with specified roles for a user.
    /// </summary>
    /// <param name="shortName">The username (without __TEST__ prefix).</param>
    /// <param name="workspacesTable">DataTable with columns: Workspace Name, My Role.</param>
    /// <remarks>
    /// Adds __TEST__ prefix to username and all workspace names. Creates workspaces
    /// via bulk setup API. Tracks all workspace keys and sets pending user context.
    ///
    /// Provides Objects:
    /// - PendingUserContext
    /// </remarks>
    [Given("{username} has access to these workspaces:")]
    [ProvidesObjects(ObjectStoreKeys.PendingUserContext)]
    public async Task GivenUserHasAccessToTheseWorkspaces(string shortName, DataTable workspacesTable)
    {
        var cred = _context.GetUserCredentials(shortName);

        // Add __TEST__ prefix to workspace names before API call
        var requests = workspacesTable.Select(row => new Generated.WorkspaceSetupRequest
        {
            Name = AddTestPrefix(row["Workspace Name"]),
            Description = $"Test workspace: {row["Workspace Name"]}",
            Role = row["My Role"]
        }).ToList();

        var results = await _context.TestControlClient.BulkWorkspaceSetupAsync(cred.Username, requests);

        // Track all workspaces for cleanup
        foreach (var result in results)
        {
            _context.TrackCreatedWorkspace(result.Name, result.Key);
        }

        // Store pending user context for steps that need it before login
        _context.ObjectStore.Add(ObjectStoreKeys.PendingUserContext, cred.Username);
    }

    /// <summary>
    /// Grants a user access to a workspace with Viewer role (default minimum access).
    /// </summary>
    /// <param name="shortName">The username (without __TEST__ prefix).</param>
    /// <param name="workspaceName">The workspace name (without __TEST__ prefix).</param>
    /// <remarks>
    /// Adds __TEST__ prefix to both parameters. Creates workspace access via bulk
    /// setup API with Viewer role. Tracks workspace and sets pending user context.
    ///
    /// Provides Objects:
    /// - PendingUserContext
    /// </remarks>
    [Given("{username} has access to {workspaceName}")]
    [ProvidesObjects(ObjectStoreKeys.PendingUserContext)]
    public async Task GivenUserHasAccessTo(string shortName, string workspaceName)
    {
        var fullWorkspaceName = AddTestPrefix(workspaceName);

        var cred = _context.GetUserCredentials(shortName);

        var request = new Generated.WorkspaceSetupRequest
        {
            Name = fullWorkspaceName,
            Description = $"Test workspace: {workspaceName}",
            Role = "Viewer" // Default to minimum access level
        };

        var results = await _context.TestControlClient.BulkWorkspaceSetupAsync(cred.Username, new[] { request });
        var result = results.First();

        // Track workspace for cleanup
        _context.TrackCreatedWorkspace(result.Name, result.Key);

        // Store pending user context for steps that need it before login
        _context.ObjectStore.Add(ObjectStoreKeys.PendingUserContext, cred.Username);
    }

    /// <summary>
    /// Grants a user Editor role access to a workspace.
    /// </summary>
    /// <param name="shortName">The username (without __TEST__ prefix).</param>
    /// <param name="workspaceName">The workspace name (without __TEST__ prefix).</param>
    /// <remarks>
    /// Adds __TEST__ prefix to both parameters. Creates workspace access via bulk
    /// setup API with Editor role. Tracks workspace, sets current workspace,
    /// and sets pending user context.
    ///
    /// Provides Objects:
    /// - CurrentWorkspace
    /// - PendingUserContext
    /// </remarks>
    [Given("{username} can edit data in {workspaceName}")]
    [ProvidesObjects(ObjectStoreKeys.CurrentWorkspace, ObjectStoreKeys.PendingUserContext)]
    public async Task GivenUserCanEditDataIn(string shortName, string workspaceName)
    {
        var fullWorkspaceName = AddTestPrefix(workspaceName);

        var cred = _context.GetUserCredentials(shortName);

        var request = new Generated.WorkspaceSetupRequest
        {
            Name = fullWorkspaceName,
            Description = $"Test workspace: {workspaceName}",
            Role = "Editor"
        };

        var results = await _context.TestControlClient.BulkWorkspaceSetupAsync(cred.Username, new[] { request });
        var result = results.First();

        // Track workspace for cleanup
        _context.TrackCreatedWorkspace(result.Name, result.Key);

        // Store current workspace for later reference
        _context.ObjectStore.Add(ObjectStoreKeys.CurrentWorkspace, fullWorkspaceName);

        // Store pending user context for steps that need it before login
        _context.ObjectStore.Add(ObjectStoreKeys.PendingUserContext, cred.Username);
    }

    /// <summary>
    /// Grants a user Viewer role access to a workspace.
    /// </summary>
    /// <param name="shortName">The username (without __TEST__ prefix).</param>
    /// <param name="workspaceName">The workspace name (without __TEST__ prefix).</param>
    /// <remarks>
    /// Adds __TEST__ prefix to both parameters. Creates workspace access via bulk
    /// setup API with Viewer role. Tracks workspace, sets current workspace,
    /// and sets pending user context.
    ///
    /// Provides Objects:
    /// - CurrentWorkspace
    /// - PendingUserContext
    /// </remarks>
    [Given("{username} can view data in {workspaceName}")]
    [ProvidesObjects(ObjectStoreKeys.CurrentWorkspace, ObjectStoreKeys.PendingUserContext)]
    public async Task GivenUserCanViewDataIn(string shortName, string workspaceName)
    {
        var fullWorkspaceName = AddTestPrefix(workspaceName);

        var cred = _context.GetUserCredentials(shortName);

        var request = new Generated.WorkspaceSetupRequest
        {
            Name = fullWorkspaceName,
            Description = $"Test workspace: {workspaceName}",
            Role = "Viewer"
        };

        var results = await _context.TestControlClient.BulkWorkspaceSetupAsync(cred.Username, new[] { request });
        var result = results.First();

        // Track workspace for cleanup
        _context.TrackCreatedWorkspace(result.Name, result.Key);

        // Store current workspace for later reference
        _context.ObjectStore.Add(ObjectStoreKeys.CurrentWorkspace, fullWorkspaceName);

        // Store pending user context for steps that need it before login
        _context.ObjectStore.Add(ObjectStoreKeys.PendingUserContext, cred.Username);
    }

    /// <summary>
    /// Creates a workspace owned by a different user to test access denial.
    /// </summary>
    /// <param name="workspaceName">The workspace name (without __TEST__ prefix).</param>
    /// <param name="shortName">The username that should NOT have access (without __TEST__ prefix).</param>
    /// <remarks>
    /// Adds __TEST__ prefix to both parameters. Gets first user from context that
    /// isn't the specified user and creates workspace for them. Used to test scenarios
    /// where a user attempts to access workspaces they don't own.
    /// </remarks>
    [Given("there is a workspace called {workspaceName} that {username} doesn't have access to")]
    public async Task GivenThereIsAWorkspaceCalledThatUserDoesntHaveAccessTo(string workspaceName, string shortName)
    {
        // Get a different user (not the specified user)
        var otherUser = _context.GetOtherUserCredentials(shortName);

        var fullWorkspaceName = AddTestPrefix(workspaceName);

        // Create workspace for the other user
        var request = new Generated.WorkspaceCreateRequest
        {
            Name = fullWorkspaceName,
            Description = $"Test workspace (no access for {shortName}): {workspaceName}",
            Role = "Owner"
        };

        var result = await _context.TestControlClient.CreateWorkspaceForUserAsync(otherUser.Username, request);

        // Track workspace for cleanup
        _context.TrackCreatedWorkspace(result.Name, result.Key);
    }

    /// <summary>
    /// Seeds a workspace with test transactions.
    /// </summary>
    /// <param name="workspaceName">The workspace name (without __TEST__ prefix).</param>
    /// <param name="transactionCount">Number of transactions to create.</param>
    /// <remarks>
    /// Adds __TEST__ prefix to workspace name. Uses Test Control API to seed
    /// transactions with "Test Transaction" payee prefix. Requires workspace key
    /// to be tracked via prior workspace creation step.
    /// </remarks>
    [Given("{workspaceName} contains {transactionCount} transactions")]
    public async Task GivenWorkspaceContainsTransactions(string workspaceName, int transactionCount)
    {
        var currentUsername = GetCurrentTestUsername();
        var fullWorkspaceName = AddTestPrefix(workspaceName);

        var workspaceKey = _context.GetWorkspaceKey(fullWorkspaceName);

        var request = new Generated.TransactionSeedRequest
        {
            Count = transactionCount,
            PayeePrefix = "Test Transaction"
        };

        await _context.TestControlClient.SeedTransactionsAsync(currentUsername, workspaceKey, request);
    }

    /// <summary>
    /// Creates multiple workspaces owned by different users.
    /// </summary>
    /// <param name="workspacesTable">DataTable with columns: Workspace Name, Owner.</param>
    /// <remarks>
    /// Adds __TEST__ prefix to all usernames and workspace names. Creates workspaces
    /// via Test Control API for each owner. Tracks all workspace keys.
    /// Used to set up multi-tenant scenarios.
    /// </remarks>
    [Given("there are other workspaces in the system:")]
    public async Task GivenThereAreOtherWorkspacesInTheSystem(DataTable workspacesTable)
    {
        foreach (var row in workspacesTable)
        {
            var workspaceName = row["Workspace Name"];
            var owner = row["Owner"];
            var fullWorkspaceName = AddTestPrefix(workspaceName);

            var ownerCred = _context.GetUserCredentials(owner);

            var request = new Generated.WorkspaceCreateRequest
            {
                Name = fullWorkspaceName,
                Description = $"Test workspace for {owner}: {workspaceName}",
                Role = "Owner"
            };

            var result = await _context.TestControlClient.CreateWorkspaceForUserAsync(ownerCred.Username, request);

            // Track workspace for cleanup
            _context.TrackCreatedWorkspace(result.Name, result.Key);
        }
    }

    #endregion

}
