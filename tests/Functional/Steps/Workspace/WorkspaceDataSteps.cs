using YoFi.V3.Tests.Functional.Attributes;
using YoFi.V3.Tests.Functional.Helpers;
using YoFi.V3.Tests.Functional.Infrastructure;
using YoFi.V3.Tests.Functional.Generated;

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
    /// Creates an active workspace with Editor role for the current user.
    /// </summary>
    /// <param name="workspaceName">The workspace name (without __TEST__ prefix).</param>
    /// <remarks>
    /// Adds __TEST__ prefix to workspace name. Creates workspace via Test Control API
    /// with Editor role. Stores workspace key and sets current workspace context.
    /// </remarks>
    [Given("I have an active workspace {workspaceName}")]
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
        _context.ObjectStore.Add(KEY_CURRENT_WORKSPACE, fullWorkspaceName);
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
    public async Task GivenUserOwnsAWorkspaceCalled(string shortName, string workspaceName)
    {
        var fullWorkspaceName = AddTestPrefix(workspaceName);

        var cred = _context.GetUserCredentials(shortName);

        var request = new Generated.WorkspaceCreateRequest
        {
            Name = fullWorkspaceName,
            Description = $"Test workspace: {workspaceName}",
            Role = "Owner"
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
        _context.ObjectStore.Add(KEY_CURRENT_WORKSPACE, fullWorkspaceName);

        // Store pending user context for steps that need it before login
        _context.ObjectStore.Add(KEY_PENDING_USER_CONTEXT, cred.Username);
    }

    /// <summary>
    /// Sets up multiple workspaces with specified roles for a user.
    /// </summary>
    /// <param name="shortName">The username (without __TEST__ prefix).</param>
    /// <param name="workspacesTable">DataTable with columns: Workspace Name, My Role.</param>
    /// <remarks>
    /// Adds __TEST__ prefix to username and all workspace names. Creates workspaces
    /// via bulk setup API. Tracks all workspace keys and sets pending user context.
    /// </remarks>
    [Given("{username} has access to these workspaces:")]
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
        _context.ObjectStore.Add(KEY_PENDING_USER_CONTEXT, cred.Username);
    }

    /// <summary>
    /// Grants a user access to a workspace with Viewer role (default minimum access).
    /// </summary>
    /// <param name="shortName">The username (without __TEST__ prefix).</param>
    /// <param name="workspaceName">The workspace name (without __TEST__ prefix).</param>
    /// <remarks>
    /// Adds __TEST__ prefix to both parameters. Creates workspace access via bulk
    /// setup API with Viewer role. Tracks workspace and sets pending user context.
    /// </remarks>
    [Given("{username} has access to {workspaceName}")]
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
        _context.ObjectStore.Add(KEY_PENDING_USER_CONTEXT, cred.Username);
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
    /// </remarks>
    [Given("{username} can edit data in {workspaceName}")]
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
        _context.ObjectStore.Add(KEY_CURRENT_WORKSPACE, fullWorkspaceName);

        // Store pending user context for steps that need it before login
        _context.ObjectStore.Add(KEY_PENDING_USER_CONTEXT, cred.Username);
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
    /// </remarks>
    [Given("{username} can view data in {workspaceName}")]
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
        _context.ObjectStore.Add(KEY_CURRENT_WORKSPACE, fullWorkspaceName);

        // Store pending user context for steps that need it before login
        _context.ObjectStore.Add(KEY_PENDING_USER_CONTEXT, cred.Username);
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
        var otherUser = GetOtherUser(shortName);

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
    [Given("there are other workspaces in the system")]
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

    #region Helper Methods

    /// <summary>
    /// Gets credentials for a different user (not the specified user).
    /// </summary>
    /// <param name="excludeShortName">The short name to exclude from selection.</param>
    /// <returns>Credentials for a different user.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no other users are available.</exception>
    /// <remarks>
    /// Attempts to find a user in the Background users list that isn't the specified user.
    /// Used for negative test scenarios where we need to test access denial.
    /// </remarks>
    private TestUserCredentials GetOtherUser(string excludeShortName)
    {
        // TODO: This is too brittle. Just add a method to ITestContext to get a different user.
        // Try common Background user names first (alice, bob, charlie)
        var commonUsers = new[] { "alice", "bob", "charlie" };

        foreach (var userName in commonUsers)
        {
            if (userName != excludeShortName)
            {
                try
                {
                    return _context.GetUserCredentials(userName);
                }
                catch (KeyNotFoundException)
                {
                    // This user doesn't exist, try next one
                }
            }
        }

        throw new InvalidOperationException(
            $"No other test users available besides '{excludeShortName}'. " +
            "Ensure at least two users are created in Background (e.g., alice, bob, charlie).");
    }

    #endregion
}
