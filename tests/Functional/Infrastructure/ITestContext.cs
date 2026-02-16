using jcoliz.FunctionalTests;
using Microsoft.Playwright;
using YoFi.V3.Tests.Functional.Generated;

namespace YoFi.V3.Tests.Functional.Infrastructure;

/// <summary>
/// Defines the test context contract that step definition classes require.
/// </summary>
/// <remarks>
/// This interface provides step classes with access to test infrastructure
/// without exposing mutable internal state. FunctionalTestBase implements
/// this interface, allowing step classes to use dependency injection rather
/// than inheritance.
///
/// Key design decisions:
/// - Credentials and workspace keys are read-only (via methods, not dictionaries)
/// - Helper methods (Create/Track) provide controlled modification
/// - Enables unit testing of step logic by mocking this interface
/// </remarks>
public interface ITestContext
{
    #region Test Infrastructure Access

    /// <summary>
    /// Gets the object store for sharing data between test steps.
    /// </summary>
    /// <remarks>
    /// Used to store and retrieve page object models and other test data
    /// that needs to be shared across multiple step implementations.
    /// </remarks>
    ObjectStore ObjectStore { get; }

    /// <summary>
    /// Gets the Test Control API client for test data setup and cleanup.
    /// </summary>
    /// <remarks>
    /// Provides access to backend test control endpoints for creating
    /// and managing test data (users, workspaces, transactions).
    /// </remarks>
    TestControlClient TestControlClient { get; }

    /// <summary>
    /// Gets the Playwright page instance for browser automation.
    /// </summary>
    /// <remarks>
    /// Provides access to the browser page for UI interactions.
    /// Used by page object models and step implementations.
    /// </remarks>
    IPage Page { get; }

    #endregion

    #region Read-Only Resource Access

    /// <summary>
    /// Gets test user credentials by friendly name.
    /// </summary>
    /// <param name="friendlyName">The friendly name used when creating credentials (e.g., "alice", "bob", "I").</param>
    /// <returns>The test user credentials associated with the friendly name.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the friendly name is not found.</exception>
    /// <remarks>
    /// Provides read-only access to credential tracking. Steps cannot modify
    /// the credentials dictionary, preventing accidental corruption of test state.
    /// </remarks>
    TestUserCredentials GetUserCredentials(string friendlyName);

    /// <summary>
    /// Gets credentials for a different user (not the specified user).
    /// </summary>
    /// <param name="excludeFriendlyName">The friendly name to exclude from selection.</param>
    /// <returns>Credentials for a different user from the tracked users.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no other users are available.</exception>
    /// <remarks>
    /// Finds a user from tracked credentials that isn't the specified user.
    /// Used for negative test scenarios where we need to test access denial.
    /// Searches all users created via CreateTestUserCredentials or in Background.
    /// </remarks>
    TestUserCredentials GetOtherUserCredentials(string excludeFriendlyName);

    /// <summary>
    /// Gets a workspace key by full workspace name.
    /// </summary>
    /// <param name="workspaceName">The full workspace name (including __TEST__ prefix).</param>
    /// <returns>The workspace key (GUID) associated with the workspace name.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the workspace name is not found.</exception>
    /// <remarks>
    /// Provides read-only access to workspace key tracking. Steps cannot modify
    /// the workspace keys dictionary, preventing accidental corruption of test state.
    /// </remarks>
    Guid GetWorkspaceKey(string workspaceName);

    #endregion

    #region Helper Methods

    /// <summary>
    /// Gets or creates a page object model of the specified type.
    /// </summary>
    /// <typeparam name="T">The type of page object model to retrieve or create.</typeparam>
    /// <returns>An instance of the specified page object model type.</returns>
    /// <remarks>
    /// This method implements a caching pattern:
    /// - If the page object already exists in the ObjectStore, returns the existing instance
    /// - Otherwise, creates a new instance using the constructor that takes IPage
    /// - Stores the new instance in the ObjectStore for reuse
    ///
    /// This ensures page objects are created once per test and reused across steps.
    /// </remarks>
    T GetOrCreatePage<T>() where T : PageObjectModel;

    /// <summary>
    /// Creates unique test user credentials for the current test.
    /// </summary>
    /// <param name="friendlyName">A friendly name for the user (e.g., "alice", "bob", "I").</param>
    /// <returns>Test user credentials with unique username, email, and password.</returns>
    /// <remarks>
    /// Generates credentials in the format:
    /// - Username: __TEST__{friendlyName}_{testId:X8}
    /// - Email: {username}@test.local
    /// - Password: Test_{testId:X8}!
    ///
    /// The credentials are automatically tracked for cleanup in test TearDown.
    /// </remarks>
    TestUserCredentials CreateTestUserCredentials(string friendlyName);

    /// <summary>
    /// Creates test user credentials and registers them on the server.
    /// </summary>
    /// <param name="friendlyName">A friendly name for the user (e.g., "alice", "bob", "I").</param>
    /// <returns>Test user credentials with server-populated ID.</returns>
    /// <remarks>
    /// This method:
    /// 1. Generates unique credentials using CreateTestUserCredentials
    /// 2. Registers the user on the server via TestControlClient
    /// 3. Updates the tracked credentials with the server-assigned ID
    /// 4. Automatically tracks for cleanup in test TearDown
    /// </remarks>
    Task<TestUserCredentials> CreateTestUserCredentialsOnServer(string friendlyName);

    /// <summary>
    /// Tracks a created workspace for cleanup in TearDown.
    /// </summary>
    /// <param name="workspaceName">The full workspace name (including __TEST__ prefix).</param>
    /// <param name="workspaceKey">The workspace key (GUID) returned by the server.</param>
    /// <remarks>
    /// Workspaces tracked via this method are automatically deleted during test TearDown,
    /// ensuring test isolation and preventing database pollution.
    /// </remarks>
    void TrackCreatedWorkspace(string workspaceName, Guid workspaceKey);

    /// <summary>
    /// Removes a workspace from cleanup tracking.
    /// </summary>
    /// <param name="workspaceName">The full workspace name (including __TEST__ prefix).</param>
    /// <remarks>
    /// Use this when a test explicitly deletes a workspace to prevent "already deleted"
    /// errors during TearDown cleanup. If the workspace was not being tracked, this
    /// method does nothing (idempotent operation).
    /// </remarks>
    void UntrackWorkspace(string workspaceName);

    #endregion
}
