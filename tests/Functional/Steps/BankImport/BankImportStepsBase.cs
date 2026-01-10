using YoFi.V3.Tests.Functional.Infrastructure;

namespace YoFi.V3.Tests.Functional.Steps.BankImport;

/// <summary>
/// Base class for bank import step definitions providing common functionality.
/// </summary>
/// <param name="context">Test context providing access to test infrastructure.</param>
/// <remarks>
/// Provides common helper methods for bank import operations including:
/// - Object store access
/// - Workspace key resolution
/// </remarks>
public abstract class BankImportStepsBase(ITestContext context)
{
    /// <summary>
    /// Test context providing access to test infrastructure.
    /// </summary>
    protected readonly ITestContext _context = context;

    #region Helper Methods

    /// <summary>
    /// Gets the current workspace name from the object store.
    /// </summary>
    /// <exception cref="InvalidOperationException">If CurrentWorkspace is not found in object store</exception>
    protected string GetCurrentWorkspace()
    {
        return _context.ObjectStore.Get<string>(ObjectStoreKeys.CurrentWorkspace)
            ?? throw new InvalidOperationException($"{ObjectStoreKeys.CurrentWorkspace} not found in object store");
    }

    /// <summary>
    /// Gets the logged in username from the object store.
    /// </summary>
    /// <exception cref="InvalidOperationException">If LoggedInAs is not found in object store</exception>
    protected string GetLoggedInUsername()
    {
        return _context.ObjectStore.Get<string>(ObjectStoreKeys.LoggedInAs)
            ?? throw new InvalidOperationException($"{ObjectStoreKeys.LoggedInAs} not found in object store");
    }

    /// <summary>
    /// Resolves a workspace name to its workspace key using the test context.
    /// </summary>
    protected Guid GetWorkspaceKey(string workspaceName) => _context.GetWorkspaceKey(workspaceName);

    #endregion
}
