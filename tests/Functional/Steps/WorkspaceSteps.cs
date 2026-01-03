using YoFi.V3.Tests.Functional.Infrastructure;

namespace YoFi.V3.Tests.Functional.Steps;

/// <summary>
/// Step definitions for workspace operations in composition architecture.
/// </summary>
/// <param name="_context">Test context providing access to test infrastructure.</param>
/// <remarks>
/// Provides workspace-related operations for functional tests using composition pattern.
/// Methods handle workspace creation with specific roles for testing scenarios.
/// </remarks>
public class WorkspaceSteps(ITestContext _context)
{
    /// <summary>
    /// Creates an active workspace with Editor role for the current user.
    /// </summary>
    /// <param name="workspaceName">The workspace name (without __TEST__ prefix).</param>
    /// <remarks>
    /// Adds __TEST__ prefix to workspace name. Creates workspace via Test Control API
    /// with Editor role. Stores workspace key and sets current workspace context.
    /// </remarks>
    // [Given("I have an active workspace {workspaceName}")]
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
        _context.ObjectStore.Add("CurrentWorkspaceName", fullWorkspaceName);
    }

    /// <summary>
    /// Adds the __TEST__ prefix to a name for test controller API calls.
    /// </summary>
    private static string AddTestPrefix(string name) => $"__TEST__{name}";
}
