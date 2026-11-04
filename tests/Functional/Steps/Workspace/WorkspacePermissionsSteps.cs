using Microsoft.Playwright;
using YoFi.V3.Tests.Functional.Infrastructure;
using YoFi.V3.Tests.Functional.Pages;

namespace YoFi.V3.Tests.Functional.Steps.Workspace;

/// <summary>
/// Step definitions for workspace permission checks and role-based access control.
/// </summary>
/// <param name="context">Test context providing access to test infrastructure.</param>
/// <remarks>
/// Handles permission verification for workspace operations:
/// - Checking if user can perform actions (edit, delete, etc.)
/// - Verifying frontend access-denied messaging and hidden management controls
/// - Role-based access control assertions (Owner/Editor/Viewer)
///
/// Negative workspace tests verify access denial without submitting backend writes.
/// </remarks>
public class WorkspacePermissionsSteps(ITestContext context) : WorkspaceStepsBase(context)
{
    #region Steps: WHEN

    /// <summary>
    /// Attempts to change workspace name or description (permission check).
    /// </summary>
    /// <remarks>
    /// Navigates directly to management and records whether workspace editing is available.
    /// </remarks>
    [When("I try to change the workspace name or description")]
    [Requires(ObjectStoreKeys.CurrentWorkspace)]
    [Provides(ObjectStoreKeys.CanMakeDesiredChanges)]
    public async Task WhenITryToChangeTheWorkspaceNameOrDescription()
    {
        var workspaceName = _context.ObjectStore.Get<string>(ObjectStoreKeys.CurrentWorkspace);

        var managementPage = _context.GetOrCreatePage<WorkspaceManagementPage>();
        await managementPage.NavigateAsync(_context.GetWorkspaceKey(workspaceName));
        var canEdit = await managementPage.IsEditAvailableAsync();
        _context.ObjectStore.Add(ObjectStoreKeys.CanMakeDesiredChanges, (object)canEdit);
    }

    /// <summary>
    /// Attempts to delete a workspace (permission check).
    /// </summary>
    /// <param name="workspaceName">The workspace name (without __TEST__ prefix).</param>
    /// <remarks>
    /// Navigates directly to management and records whether deletion is available.
    /// Used for negative test cases where deletion should be blocked.
    /// </remarks>
    [When("I try to delete {workspaceName}")]
    [Provides(ObjectStoreKeys.CanDeleteWorkspace)]
    [Provides(ObjectStoreKeys.CurrentWorkspace)]
    public async Task WhenITryToDelete(string workspaceName)
    {
        var fullWorkspaceName = AddTestPrefix(workspaceName);

        var managementPage = _context.GetOrCreatePage<WorkspaceManagementPage>();
        await managementPage.NavigateAsync(_context.GetWorkspaceKey(fullWorkspaceName));
        var canDelete = await managementPage.IsDeleteAvailableAsync();
        _context.ObjectStore.Add(ObjectStoreKeys.CanDeleteWorkspace, (object)canDelete);
        _context.ObjectStore.Add(ObjectStoreKeys.CurrentWorkspace, fullWorkspaceName);
    }

    /// <summary>
    /// Attempts to add or edit transactions (permission check).
    /// </summary>
    /// <remarks>
    /// Checks if New Transaction button is available on transactions page. Stores
    /// permission check result for later assertion. Used for role-based access tests.
    /// TODO: Add edit button availability check for existing transactions.
    ///
    /// Provides Objects:
    /// - CanMakeDesiredChanges
    /// </remarks>
    [When("I try to add or edit transactions")]
    [Provides(ObjectStoreKeys.CanMakeDesiredChanges)]
    public async Task WhenITryToAddOrEditTransactions()
    {
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();

        // Check if New Transaction button is available
        var canCreate = await transactionsPage.IsNewTransactionAvailableAsync();
        _context.ObjectStore.Add(ObjectStoreKeys.CanMakeDesiredChanges, (object)canCreate);

        // TODO: Check if edit buttons are available on existing transactions
        // This would require knowing which transactions exist
    }

    #endregion

    #region Steps: THEN

    [Then("I should see workspace management access denied")]
    public async Task ThenIShouldSeeWorkspaceManagementAccessDenied()
    {
        await _context.GetOrCreatePage<WorkspaceManagementPage>().AssertAccessDeniedAsync();
    }

    /// <summary>
    /// Verifies that the user cannot make desired changes (negative permission check).
    /// </summary>
    /// <remarks>
    /// Retrieves permission check result from object store (KEY_CAN_MAKE_DESIRED_CHANGES)
    /// and asserts it's false. Used for role-based access control tests.
    ///
    /// Requires Objects:
    /// - CanMakeDesiredChanges
    /// </remarks>
    [Then("I should not be able to make those changes")]
    [Requires(ObjectStoreKeys.CanMakeDesiredChanges)]
    public async Task ThenIShouldNotBeAbleToMakeThoseChanges()
    {
        AssertCannotPerformAction(ObjectStoreKeys.CanMakeDesiredChanges, "User should not be able to make desired changes");
        await Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the user cannot delete the workspace (negative permission check).
    /// </summary>
    /// <remarks>
    /// Retrieves permission check result from object store (KEY_CAN_DELETE_WORKSPACE)
    /// and asserts it's false. Used for role-based deletion tests.
    ///
    /// Requires Objects:
    /// - CanDeleteWorkspace
    /// </remarks>
    [Then("the workspace should remain intact")]
    [Requires(ObjectStoreKeys.CanDeleteWorkspace)]
    [Requires(ObjectStoreKeys.CurrentWorkspace)]
    public async Task ThenTheWorkspaceShouldRemainIntact()
    {
        AssertCannotPerformAction(ObjectStoreKeys.CanDeleteWorkspace, "User should not be able to delete the workspace");
        var workspacesPage = _context.GetOrCreatePage<WorkspacesPage>();
        await workspacesPage.NavigateAsync();
        var workspaceName = _context.ObjectStore.Get<string>(ObjectStoreKeys.CurrentWorkspace);
        await workspacesPage.WaitForWorkspaceAsync(workspaceName);
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
    [Requires(ObjectStoreKeys.CurrentWorkspace)]
    public async Task ThenICanAddEditAndDeleteTransactions()
    {
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();
        await transactionsPage.NavigateAsync();

        var workspaceName = _context.ObjectStore.Get<string>(ObjectStoreKeys.CurrentWorkspace);

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

        // TODO: Update this test. Delete has been removed from this page.
        // Click into the details page and delete there.
#if false
        // Check if Delete button is available
        var canDelete = await transactionsPage.IsDeleteAvailableAsync(payeeName!);
        Assert.That(canDelete, Is.True, "Owner should be able to delete transactions");
#endif
    }

    /// <summary>
    /// Verifies that Owner role can change workspace settings.
    /// </summary>
    /// <remarks>
    /// Opens management for the current workspace and verifies its edit form.
    /// </remarks>
    [Then("I can change workspace settings")]
    [Requires(ObjectStoreKeys.CurrentWorkspace)]
    public async Task ThenICanChangeWorkspaceSettings()
    {
        var workspacesPage = _context.GetOrCreatePage<WorkspacesPage>();
        await workspacesPage.NavigateAsync();

        var workspaceName = _context.ObjectStore.Get<string>(ObjectStoreKeys.CurrentWorkspace);

        await workspacesPage.OpenManagementAsync(workspaceName);
        var managementPage = _context.GetOrCreatePage<WorkspaceManagementPage>();
        await Assertions.Expect(managementPage.EditButton).ToBeEnabledAsync();
        await managementPage.StartEditAsync();
        await Assertions.Expect(managementPage.EditName).ToHaveValueAsync(workspaceName);
    }

    /// <summary>
    /// Verifies that Owner role can delete the workspace.
    /// </summary>
    /// <remarks>
    /// Opens management, verifies the deletion target, and cancels the dialog.
    /// </remarks>
    [Then("I can remove the workspace if needed")]
    [Requires(ObjectStoreKeys.CurrentWorkspace)]
    public async Task ThenICanRemoveTheWorkspaceIfNeeded()
    {
        var workspacesPage = _context.GetOrCreatePage<WorkspacesPage>();
        await workspacesPage.NavigateAsync();

        var workspaceName = _context.ObjectStore.Get<string>(ObjectStoreKeys.CurrentWorkspace);

        await workspacesPage.OpenManagementAsync(workspaceName);
        var managementPage = _context.GetOrCreatePage<WorkspaceManagementPage>();
        await managementPage.OpenDeleteModalAsync();
        await Assertions.Expect(managementPage.DeletingWorkspaceName)
            .ToHaveTextAsync(workspaceName);
        await managementPage.CancelDeleteAsync();
    }

    #endregion
}
