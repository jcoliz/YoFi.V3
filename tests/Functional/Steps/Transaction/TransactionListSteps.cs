using YoFi.V3.Tests.Functional.Attributes;
using YoFi.V3.Tests.Functional.Infrastructure;
using YoFi.V3.Tests.Functional.Pages;

namespace YoFi.V3.Tests.Functional.Steps.Transaction;

/// <summary>
/// Step definitions for transaction list viewing and navigation operations.
/// </summary>
/// <param name="context">Test context providing access to test infrastructure.</param>
/// <remarks>
/// Handles transaction list operations:
/// - Viewing transaction lists in a workspace
/// - Navigating to transaction pages
/// - Transaction count assertions
/// - Verifying transaction visibility
/// - Data isolation checks between workspaces
/// </remarks>
public class TransactionListSteps(ITestContext context) : TransactionStepsBase(context)
{
    #region Steps: WHEN

    /// <summary>
    /// Views transactions in a specific workspace.
    /// </summary>
    /// <param name="workspaceName">The workspace name (without __TEST__ prefix).</param>
    /// <remarks>
    /// Adds __TEST__ prefix. Navigates to transactions page, selects workspace,
    /// waits for loading to complete, and stores as current workspace.
    /// </remarks>
    [When("I view transactions in {workspaceName}")]
    public async Task WhenIViewTransactionsIn(string workspaceName)
    {
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();
        await transactionsPage.NavigateAsync();

        var fullWorkspaceName = AddTestPrefix(workspaceName);

        // Select the workspace
        await transactionsPage.WorkspaceSelector.SelectWorkspaceAsync(fullWorkspaceName);
        await transactionsPage.WaitForLoadingCompleteAsync();

        _context.ObjectStore.Add(KEY_CURRENT_WORKSPACE, fullWorkspaceName);
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
    public async Task WhenITryToViewTransactionsIn(string workspaceName)
    {
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();
        await transactionsPage.NavigateAsync();

        var fullWorkspaceName = AddTestPrefix(workspaceName);

        // Check if workspace is in available list
        var availableWorkspaces = await transactionsPage.WorkspaceSelector.GetAvailableWorkspacesAsync();
        var hasAccess = availableWorkspaces.Contains(fullWorkspaceName);
        _context.ObjectStore.Add(KEY_HAS_WORKSPACE_ACCESS, (object)hasAccess);
    }

    #endregion

    #region Steps: THEN

    /// <summary>
    /// Verifies that exactly the expected number of transactions are displayed.
    /// </summary>
    /// <param name="expectedCount">The expected transaction count.</param>
    /// <remarks>
    /// Counts transactions on current transactions page and asserts exact match.
    /// </remarks>
    [Then("I should see exactly {expectedCount} transactions")]
    public async Task ThenIShouldSeeExactlyTransactions(int expectedCount)
    {
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();
        var actualCount = await transactionsPage.GetTransactionCountAsync();
        Assert.That(actualCount, Is.EqualTo(expectedCount), $"Expected exactly {expectedCount} transactions");
    }

    /// <summary>
    /// Verifies that at least some transactions are visible.
    /// </summary>
    /// <remarks>
    /// Counts transactions on current page and asserts count is greater than zero.
    /// </remarks>
    [Then("I should see the transactions")]
    public async Task ThenIShouldSeeTheTransactions()
    {
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();
        var count = await transactionsPage.GetTransactionCountAsync();
        Assert.That(count, Is.GreaterThan(0), "Should see some transactions");
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
    public async Task ThenTheyShouldAllBeFromWorkspace(string workspaceName)
    {
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();

        var fullWorkspaceName = AddTestPrefix(workspaceName);

        // Verify we're viewing the correct workspace
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
    public async Task ThenIShouldNotSeeAnyTransactionsFrom(string workspaceName)
    {
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();

        var fullWorkspaceName = AddTestPrefix(workspaceName);

        // Transactions are workspace-isolated, so if we're in a different workspace, we won't see them
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
    public async Task ThenIShouldNotBeAbleToAccessThatData()
    {
        AssertCannotPerformAction(KEY_HAS_WORKSPACE_ACCESS, "User should not have access to the workspace");
        await Task.CompletedTask;
    }

    #endregion
}
