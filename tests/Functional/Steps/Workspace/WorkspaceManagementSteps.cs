using YoFi.V3.Tests.Functional.Attributes;
using YoFi.V3.Tests.Functional.Infrastructure;

namespace YoFi.V3.Tests.Functional.Steps.Workspace;

/// <summary>
/// Step definitions for workspace CRUD operations (Create, Rename, Update, Delete).
/// </summary>
/// <remarks>
/// Handles workspace management operations:
/// - Creating new workspaces
/// - Viewing workspace lists and details
/// - Renaming workspaces
/// - Updating workspace descriptions
/// - Deleting workspaces
///
/// All operations work with user-readable workspace names (without __TEST__ prefix).
/// The prefix is added automatically when needed via AddTestPrefix().
/// </remarks>
public class WorkspaceManagementSteps : WorkspaceStepsBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceManagementSteps"/> class.
    /// </summary>
    /// <param name="context">Test context providing access to test infrastructure.</param>
    public WorkspaceManagementSteps(ITestContext context) : base(context)
    {
    }

    #region Steps: WHEN

    /// <summary>
    /// Creates a new workspace with specified name and optional description.
    /// </summary>
    /// <param name="name">The workspace name (without __TEST__ prefix).</param>
    /// <param name="description">The workspace description (optional, defaults to generated description).</param>
    /// <remarks>
    /// Adds __TEST__ prefix to name. Navigates to workspaces page, creates workspace,
    /// waits for it to appear in the list, and stores it as current workspace.
    /// If description is null, generates a default description.
    ///
    /// Provides Objects:
    /// - CurrentWorkspace
    /// </remarks>
    [When("I create a new workspace called {name} for {description}")]
    [When("I create a workspace called {name}")]
    [ProvidesObjects(ObjectStoreKeys.CurrentWorkspace)]
    public async Task WhenICreateANewWorkspaceCalled(string name, string? description = null)
    {
        var workspacesPage = _context.GetOrCreatePage<Pages.WorkspacesPage>();
        await workspacesPage.NavigateAsync();

        var workspaceName = AddTestPrefix(name);
        var finalDescription = description ?? $"__TEST__ Test workspace: {name}";
        await workspacesPage.CreateWorkspaceAsync(workspaceName, finalDescription);

        // Wait for the new workspace card to appear in the list
        // The loading spinner being hidden doesn't guarantee the list is fully rendered
        await workspacesPage.WaitForWorkspaceAsync(workspaceName);

        // Store the workspace name for future reference
        _context.ObjectStore.Add(ObjectStoreKeys.CurrentWorkspace, workspaceName);
    }

    /// <summary>
    /// Navigates to the workspaces list page.
    /// </summary>
    /// <remarks>
    /// Simply navigates to the workspaces page to view available workspaces.
    /// </remarks>
    [When("I view my workspace list")]
    public async Task WhenIViewMyWorkspaceList()
    {
        var workspacesPage = _context.GetOrCreatePage<Pages.WorkspacesPage>();
        await workspacesPage.NavigateAsync();
    }

    /// <summary>
    /// Views the details of a specific workspace.
    /// </summary>
    /// <param name="workspaceName">The workspace name (without __TEST__ prefix).</param>
    /// <remarks>
    /// Adds __TEST__ prefix. Navigates to workspaces page, selects the workspace
    /// in the workspace selector, and stores it as current workspace.
    ///
    /// Provides Objects:
    /// - CurrentWorkspace
    /// </remarks>
    [When("I view the details of {workspaceName}")]
    [ProvidesObjects(ObjectStoreKeys.CurrentWorkspace)]
    public async Task WhenIViewTheDetailsOf(string workspaceName)
    {
        var workspacesPage = _context.GetOrCreatePage<Pages.WorkspacesPage>();
        await workspacesPage.NavigateAsync();

        // Open workspace selector dropdown to view details
        var fullWorkspaceName = AddTestPrefix(workspaceName);
        await workspacesPage.WorkspaceSelector.SelectWorkspaceAsync(fullWorkspaceName);

        _context.ObjectStore.Add(ObjectStoreKeys.CurrentWorkspace, fullWorkspaceName);
    }

    /// <summary>
    /// Renames the current workspace.
    /// </summary>
    /// <param name="newName">The new workspace name (without __TEST__ prefix).</param>
    /// <remarks>
    /// Adds __TEST__ prefix to new name. Gets current workspace from object store,
    /// performs rename via workspaces page, waits for updated name to appear, and
    /// updates current workspace context.
    ///
    /// Requires Objects:
    /// - CurrentWorkspace
    ///
    /// Provides Objects:
    /// - NewWorkspaceName
    /// - CurrentWorkspace (updated)
    /// </remarks>
    [When("I rename it to {newName}")]
    [RequiresObjects(ObjectStoreKeys.CurrentWorkspace)]
    [ProvidesObjects(ObjectStoreKeys.NewWorkspaceName, ObjectStoreKeys.CurrentWorkspace)]
    public async Task WhenIRenameItTo(string newName)
    {
        var workspacesPage = _context.GetOrCreatePage<Pages.WorkspacesPage>();
        await workspacesPage.NavigateAsync();

        // Get the current workspace name from object store
        var oldName = GetRequiredFromStore(ObjectStoreKeys.CurrentWorkspace);

        // Store the new name for assertions
        var fullNewName = AddTestPrefix(newName);
        _context.ObjectStore.Add(ObjectStoreKeys.NewWorkspaceName, fullNewName);

        await workspacesPage.UpdateWorkspaceAsync(oldName, fullNewName);

        // Wait for the renamed workspace card to appear in the list
        // The loading spinner being hidden doesn't guarantee the list is fully rendered
        await workspacesPage.WaitForWorkspaceAsync(fullNewName);

        _context.ObjectStore.Add(ObjectStoreKeys.CurrentWorkspace, fullNewName);
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
    public async Task WhenIUpdateTheDescriptionTo(string newDescription)
    {
        var workspacesPage = _context.GetOrCreatePage<Pages.WorkspacesPage>();
        await workspacesPage.NavigateAsync();

        // Update with same name but new description
        var workspaceName = GetCurrentOrNewWorkspaceName();
        await workspacesPage.UpdateWorkspaceAsync(workspaceName, workspaceName, newDescription);

        // Wait for the workspace card to be updated in the list
        // The loading spinner being hidden doesn't guarantee the list is fully rendered
        await workspacesPage.WaitForWorkspaceAsync(workspaceName);
    }

    /// <summary>
    /// Deletes a workspace.
    /// </summary>
    /// <param name="workspaceName">The workspace name (without __TEST__ prefix).</param>
    /// <remarks>
    /// Adds __TEST__ prefix. Navigates to workspaces page and performs delete
    /// operation. Used for positive test cases where deletion is expected to succeed.
    /// Also removes workspace from tracking to prevent cleanup attempts.
    /// </remarks>
    [When("I delete {workspaceName}")]
    public async Task WhenIDelete(string workspaceName)
    {
        var workspacesPage = _context.GetOrCreatePage<Pages.WorkspacesPage>();
        await workspacesPage.NavigateAsync();

        var fullWorkspaceName = AddTestPrefix(workspaceName);
        await workspacesPage.DeleteWorkspaceAsync(fullWorkspaceName);

        // Also remove this from workspace tracking, so we don't try to clean it up later
        _context.UntrackWorkspace(fullWorkspaceName);
    }

    #endregion
}
