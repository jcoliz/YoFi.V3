using YoFi.V3.Tests.Functional.Infrastructure;
using YoFi.V3.Tests.Functional.Pages;

namespace YoFi.V3.Tests.Functional.Steps.Workspace;

/// <summary>
/// Base class for workspace-related step definition classes.
/// </summary>
/// <param name="_context">Test context providing access to test infrastructure.</param>
/// <remarks>
/// Provides shared infrastructure for all workspace step classes:
/// - Common helper methods (prefix handling, object store access)
/// - Object store key constants
/// - Page object factory methods
/// - Composition of shared step classes
///
/// All workspace step classes should inherit from this base to maintain consistency.
/// </remarks>
public abstract class WorkspaceStepsBase(ITestContext _context)
{
    #region Object Store Keys

    protected const string KEY_LOGGED_IN_AS = "LoggedInAs";
    protected const string KEY_PENDING_USER_CONTEXT = "PendingUserContext";
    protected const string KEY_CURRENT_WORKSPACE = "CurrentWorkspaceName";
    protected const string KEY_NEW_WORKSPACE_NAME = "NewWorkspaceName";
    protected const string KEY_LAST_TRANSACTION_PAYEE = "LastTransactionPayee";
    protected const string KEY_CAN_DELETE_WORKSPACE = "CanDeleteWorkspace";
    protected const string KEY_CAN_MAKE_DESIRED_CHANGES = "CanMakeDesiredChanges";
    protected const string KEY_HAS_WORKSPACE_ACCESS = "HasWorkspaceAccess";
    protected const string KEY_TRANSACTION_KEY = "TransactionKey";

    #endregion

    #region Composed Step Classes

    protected readonly NavigationSteps NavigationSteps = new(_context);
    protected readonly AuthSteps AuthSteps = new(_context);

    #endregion

    #region Helper Methods

    /// <summary>
    /// Adds the __TEST__ prefix to a name for test controller API calls.
    /// </summary>
    /// <param name="name">The name without prefix.</param>
    /// <returns>The name with __TEST__ prefix.</returns>
    protected static string AddTestPrefix(string name) => $"__TEST__{name}";

    /// <summary>
    /// Gets a required value from the object store, throwing if not found.
    /// </summary>
    /// <param name="key">The object store key.</param>
    /// <returns>The stored value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the key is not found.</exception>
    protected string GetRequiredFromStore(string key)
    {
        return _context.ObjectStore.Get<string>(key)
            ?? throw new InvalidOperationException($"Required value '{key}' not found in object store");
    }

    /// <summary>
    /// Gets the current or newly renamed workspace name from object store.
    /// </summary>
    /// <returns>The workspace name with __TEST__ prefix.</returns>
    /// <remarks>
    /// Checks for KEY_NEW_WORKSPACE_NAME first (after rename), then falls back to KEY_CURRENT_WORKSPACE.
    /// </remarks>
    protected string GetCurrentOrNewWorkspaceName()
    {
        return _context.ObjectStore.Contains<string>(KEY_NEW_WORKSPACE_NAME)
            ? _context.ObjectStore.Get<string>(KEY_NEW_WORKSPACE_NAME)!
            : _context.ObjectStore.Get<string>(KEY_CURRENT_WORKSPACE)!;
    }

    /// <summary>
    /// Gets the last transaction payee from object store.
    /// </summary>
    /// <returns>The payee name of the last transaction.</returns>
    protected string GetLastTransactionPayee()
    {
        return GetRequiredFromStore(KEY_LAST_TRANSACTION_PAYEE);
    }

    /// <summary>
    /// Asserts that a user cannot perform a specific action.
    /// </summary>
    /// <param name="actionKey">The object store key for the permission check.</param>
    /// <param name="message">The assertion failure message.</param>
    /// <exception cref="InvalidOperationException">Thrown when the permission check key is not found.</exception>
    protected void AssertCannotPerformAction(string actionKey, string message)
    {
        var canPerform = _context.ObjectStore.Get<object>(actionKey) as bool?
            ?? throw new InvalidOperationException($"Permission check '{actionKey}' not found");
        Assert.That(canPerform, Is.False, message);
    }

    /// <summary>
    /// Gets the current test username (with __TEST__ prefix).
    /// </summary>
    /// <returns>The full username with __TEST__ prefix.</returns>
    /// <remarks>
    /// Priority order:
    /// 1. KEY_LOGGED_IN_AS (set after login)
    /// 2. KEY_PENDING_USER_CONTEXT (set by pre-login entitlement steps)
    /// 3. First user in credentials (fallback - conditionally compiled)
    /// </remarks>
    protected string GetCurrentTestUsername()
    {
        // Check if we have a logged-in user in object store (highest priority)
        if (_context.ObjectStore.Contains<string>(KEY_LOGGED_IN_AS))
        {
            return _context.ObjectStore.Get<string>(KEY_LOGGED_IN_AS)!;
        }

        // Check if we have a pending user context (for pre-login steps)
        if (_context.ObjectStore.Contains<string>(KEY_PENDING_USER_CONTEXT))
        {
            return _context.ObjectStore.Get<string>(KEY_PENDING_USER_CONTEXT)!;
        }

#if NEEDS_FIRST_USER_FALLBACK
        // Fall back to first user from credentials
        // NOTE: This fallback requires adding GetFirstUserCredentials() to ITestContext
        // Conditionally compiled until we determine if this is actually needed
        var firstUsername = _context.GetFirstUserCredentials()?.Username;
        if (firstUsername == null)
        {
            throw new InvalidOperationException("No test users found. Ensure users are created in Background.");
        }

        return firstUsername;
#else
        throw new InvalidOperationException(
            "No logged-in user or pending user context found in object store. " +
            "Ensure either KEY_LOGGED_IN_AS or KEY_PENDING_USER_CONTEXT is set before calling this method.");
#endif
    }

    #endregion
}
