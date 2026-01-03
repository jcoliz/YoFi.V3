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
    #region Steps: GIVEN

    /// <summary>
    /// Navigates to the transactions page with a workspace selected.
    /// </summary>
    /// <remarks>
    /// Sets up minimal state to be on the transactions page: navigates to the page,
    /// then selects the current workspace from object store. Does not seed any transactions.
    /// Requires KEY_CURRENT_WORKSPACE to be set in object store.
    ///
    /// Requires Objects
    /// - CurrentWorkspace
    /// </remarks>
    [Given("I am on the transactions page")]
    [RequiresObjects(ObjectStoreKeys.CurrentWorkspace)]
    public async Task GivenIAmOnTheTransactionsPage()
    {
        // Given: Navigate to transactions page first
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();
        await transactionsPage.NavigateAsync();
        await transactionsPage.WaitForLoadingCompleteAsync();

        // And: Get workspace name from object store
        var workspaceName = _context.ObjectStore.Get<string>(ObjectStoreKeys.CurrentWorkspace)
            ?? throw new InvalidOperationException($"{ObjectStoreKeys.CurrentWorkspace} not found in object store. Ensure workspace is set up before calling this step.");

        // And: Select the workspace
        await transactionsPage.WorkspaceSelector.SelectWorkspaceAsync(workspaceName);
        await transactionsPage.WaitForLoadingCompleteAsync();
    }

    #endregion

    #region Steps: WHEN

    /// <summary>
    /// Clicks on the transaction row to navigate to the details page.
    /// </summary>
    /// <remarks>
    /// Retrieves transaction payee from object store (stored by data seeding steps),
    /// waits for the transaction row to appear, and clicks it to navigate to the
    /// full details page. Requires TransactionPayee in object store.
    ///
    /// Requires Objects
    /// - TransactionPayee
    /// </remarks>
    [When("I click on the transaction row")]
    [RequiresObjects(ObjectStoreKeys.TransactionPayee)]
    public async Task WhenIClickOnTheTransactionRow()
    {
        // When: Get the payee from object store
        var payee = _context.ObjectStore.Get<string>(ObjectStoreKeys.TransactionPayee)
            ?? throw new InvalidOperationException($"{ObjectStoreKeys.TransactionPayee} not found in object store");

        // And: Click on the transaction row to navigate to details
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();

        // And: Wait for row to be loaded
        await transactionsPage.WaitForTransactionAsync(payee);

        // And: Get the row data and click it
        var row = await transactionsPage.GetTransactionRowByPayeeAsync(payee);
        await row.ClickAsync();
    }


    /// <summary>
    /// Views transactions in a specific workspace.
    /// </summary>
    /// <param name="workspaceName">The workspace name (without __TEST__ prefix).</param>
    /// <remarks>
    /// Adds __TEST__ prefix. Navigates to transactions page, selects workspace,
    /// waits for loading to complete, and stores as current workspace.
    ///
    /// Provides Objects
    /// - CurrentWorkspace
    /// </remarks>
    [When("I view transactions in {workspaceName}")]
    [ProvidesObjects(ObjectStoreKeys.CurrentWorkspace)]
    public async Task WhenIViewTransactionsIn(string workspaceName)
    {
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();
        await transactionsPage.NavigateAsync();

        var fullWorkspaceName = AddTestPrefix(workspaceName);

        // Select the workspace
        await transactionsPage.WorkspaceSelector.SelectWorkspaceAsync(fullWorkspaceName);
        await transactionsPage.WaitForLoadingCompleteAsync();

        _context.ObjectStore.Add(ObjectStoreKeys.CurrentWorkspace, fullWorkspaceName);
    }

    /// <summary>
    /// Attempts to view transactions in a workspace (permission check).
    /// </summary>
    /// <param name="workspaceName">The workspace name (without __TEST__ prefix).</param>
    /// <remarks>
    /// Adds __TEST__ prefix. Navigates to transactions page and checks if workspace
    /// is in the available workspaces list. Stores access check result for later
    /// assertion. Used for negative test cases.
    ///
    /// Provides Objects
    /// - HasWorkspaceAccess
    /// </remarks>
    [When("I try to view transactions in {workspaceName}")]
    [ProvidesObjects(ObjectStoreKeys.HasWorkspaceAccess)]
    public async Task WhenITryToViewTransactionsIn(string workspaceName)
    {
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();
        await transactionsPage.NavigateAsync();

        var fullWorkspaceName = AddTestPrefix(workspaceName);

        // Check if workspace is in available list
        var availableWorkspaces = await transactionsPage.WorkspaceSelector.GetAvailableWorkspacesAsync();
        var hasAccess = availableWorkspaces.Contains(fullWorkspaceName);
        _context.ObjectStore.Add(ObjectStoreKeys.HasWorkspaceAccess, (object)hasAccess);
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
    ///
    /// Requires Objects
    /// - HasWorkspaceAccess
    /// </remarks>
    [Then("I should not be able to access that data")]
    [RequiresObjects(ObjectStoreKeys.HasWorkspaceAccess)]
    public async Task ThenIShouldNotBeAbleToAccessThatData()
    {
        AssertCannotPerformAction(ObjectStoreKeys.HasWorkspaceAccess, "User should not have access to the workspace");
        await Task.CompletedTask;
    }

    /// <summary>
    /// Verifies that the updated memo appears in the transaction list.
    /// </summary>
    /// <remarks>
    /// Retrieves the payee and new memo from object store, waits for page to update,
    /// and verifies the memo in the transaction list matches the updated value.
    ///
    /// Requires Objects
    /// - TransactionPayee
    /// - TransactionMemo
    /// </remarks>
    [Then("I should see the updated memo in the transaction list")]
    [RequiresObjects(ObjectStoreKeys.TransactionPayee, ObjectStoreKeys.TransactionMemo)]
    public async Task ThenIShouldSeeTheUpdatedMemoInTheTransactionList()
    {
        // Then: Get the payee and new memo from object store
        var payee = _context.ObjectStore.Get<string>(ObjectStoreKeys.TransactionPayee)
            ?? throw new InvalidOperationException($"{ObjectStoreKeys.TransactionPayee} not found in object store");
        var expectedMemo = _context.ObjectStore.Get<string>(ObjectStoreKeys.TransactionMemo)
            ?? throw new InvalidOperationException($"{ObjectStoreKeys.TransactionMemo} not found in object store");

        // And: Wait for page to update (loading spinner to hide)
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();
        await transactionsPage.WaitForLoadingCompleteAsync();

        // And: Verify the memo in the transaction list
        var actualMemo = await transactionsPage.GetTransactionMemoAsync(payee);

        Assert.That(actualMemo?.Trim(), Is.EqualTo(expectedMemo),
            $"Expected memo to be '{expectedMemo}' but was '{actualMemo}'");
    }

    /// <summary>
    /// Verifies that the updated category appears in the transaction list.
    /// </summary>
    /// <remarks>
    /// Retrieves the payee and new category from object store, waits for page to update,
    /// and verifies the category in the transaction list matches the updated value.
    ///
    /// Requires Objects
    /// - TransactionPayee
    /// - TransactionCategory
    /// </remarks>
    [Then("I should see the updated category in the transaction list")]
    [RequiresObjects(ObjectStoreKeys.TransactionPayee, ObjectStoreKeys.TransactionCategory)]
    public async Task ThenIShouldSeeTheUpdatedCategoryInTheTransactionList()
    {
        // Then: Get the payee and new category from object store
        var payee = _context.ObjectStore.Get<string>(ObjectStoreKeys.TransactionPayee)
            ?? throw new InvalidOperationException($"{ObjectStoreKeys.TransactionPayee} not found in object store");
        var expectedCategory = _context.ObjectStore.Get<string>(ObjectStoreKeys.TransactionCategory)
            ?? throw new InvalidOperationException($"{ObjectStoreKeys.TransactionCategory} not found in object store");

        // And: Wait for page to update (loading spinner to hide)
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();
        await transactionsPage.WaitForLoadingCompleteAsync();
        await transactionsPage.WaitForTransactionAsync(payee);

        // And: Verify the category in the transaction list
        var actualCategory = await transactionsPage.GetTransactionCategoryAsync(payee);

        Assert.That(actualCategory?.Trim(), Is.EqualTo(expectedCategory),
            $"Expected category to be '{expectedCategory}' but was '{actualCategory}'");
    }

    #endregion
}
