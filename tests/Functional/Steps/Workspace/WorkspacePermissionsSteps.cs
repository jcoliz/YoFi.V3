using Microsoft.Playwright;
using YoFi.V3.Tests.Functional.Attributes;
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
/// - Verifying permission-based UI element visibility
/// - Role-based access control assertions (Owner/Editor/Viewer)
///
/// These steps perform UI checks to verify that buttons/actions are available
/// or unavailable based on the user's role in the workspace.
/// </remarks>
public class WorkspacePermissionsSteps(ITestContext context) : WorkspaceStepsBase(context)
{
    #region Steps: WHEN

    /// <summary>
    /// Attempts to change workspace name or description (permission check).
    /// </summary>
    /// <remarks>
    /// Navigates to workspaces page and checks if edit button is available for
    /// current workspace. Stores permission check result in object store for
    /// later assertion.
    /// </remarks>
    [When("I try to change the workspace name or description")]
    public async Task WhenITryToChangeTheWorkspaceNameOrDescription()
    {
        var workspacesPage = _context.GetOrCreatePage<WorkspacesPage>();
        await workspacesPage.NavigateAsync();

        var workspaceName = GetRequiredFromStore(ObjectStoreKeys.CurrentWorkspace);

        // Check if edit button is available
        var canEdit = await workspacesPage.IsEditAvailableAsync(workspaceName);
        _context.ObjectStore.Add(ObjectStoreKeys.CanMakeDesiredChanges, (object)canEdit);
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
    public async Task WhenITryToDelete(string workspaceName)
    {
        var fullWorkspaceName = AddTestPrefix(workspaceName);

        var workspacesPage = _context.GetOrCreatePage<WorkspacesPage>();
        await workspacesPage.NavigateAsync();

        // Check if delete button is available
        var canDelete = await workspacesPage.IsDeleteAvailableAsync(fullWorkspaceName);
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
    /// </remarks>
    [When("I try to add or edit transactions")]
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

    /// <summary>
    /// Verifies that the user cannot make desired changes (negative permission check).
    /// </summary>
    /// <remarks>
    /// Retrieves permission check result from object store (KEY_CAN_MAKE_DESIRED_CHANGES)
    /// and asserts it's false. Used for role-based access control tests.
    /// </remarks>
    [Then("I should not be able to make those changes")]
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
    /// </remarks>
    [Then("the workspace should remain intact")]
    public async Task ThenTheWorkspaceShouldRemainIntact()
    {
        AssertCannotPerformAction(ObjectStoreKeys.CanDeleteWorkspace, "User should not be able to delete the workspace");
        await Task.CompletedTask;
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
    public async Task ThenICanAddEditAndDeleteTransactions()
    {
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();
        await transactionsPage.NavigateAsync();

        var workspaceName = GetRequiredFromStore(ObjectStoreKeys.CurrentWorkspace);

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
    public async Task ThenICanChangeWorkspaceSettings()
    {
        var workspacesPage = _context.GetOrCreatePage<WorkspacesPage>();
        await workspacesPage.NavigateAsync();

        var workspaceName = GetRequiredFromStore(ObjectStoreKeys.CurrentWorkspace);

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
    public async Task ThenICanRemoveTheWorkspaceIfNeeded()
    {
        var workspacesPage = _context.GetOrCreatePage<WorkspacesPage>();
        await workspacesPage.NavigateAsync();

        var workspaceName = GetRequiredFromStore(ObjectStoreKeys.CurrentWorkspace);

        var canDelete = await workspacesPage.IsDeleteAvailableAsync(workspaceName);
        Assert.That(canDelete, Is.True, "Owner should be able to delete workspace");
    }

    #endregion
}
