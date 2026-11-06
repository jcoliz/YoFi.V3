using YoFi.V3.Tests.Functional.Infrastructure;
using YoFi.V3.Tests.Functional.Pages;

namespace YoFi.V3.Tests.Functional.Steps.Transaction;

/// <summary>
/// Step definitions for transaction list viewing and navigation operations.
/// </summary>
/// <param name="context">Test context providing access to test infrastructure.</param>
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
    /// </remarks>
    [Given("I am on the transactions page")]
    [Requires(ObjectStoreKeys.CurrentWorkspace)]
    public async Task IAmOnTheTransactionsPage()
    {
        // Given: Navigate to transactions page first
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();
        await transactionsPage.NavigateAsync();
        await transactionsPage.WaitForLoadingCompleteAsync();

        // And: Get workspace name from object store
        var workspaceName = _context.ObjectStore.Get<string>(ObjectStoreKeys.CurrentWorkspace);

        // And: Select the workspace
        await transactionsPage.WorkspaceSelector.SelectWorkspaceAsync(workspaceName);
        await transactionsPage.WaitForLoadingCompleteAsync();
    }

    /// <summary>
    /// Given I am on the transactions detail page for that transaction
    /// </summary>
    [Given("I am on the transactions detail page for that transaction")]
    [Requires(ObjectStoreKeys.TransactionPayee)]
    [Requires(ObjectStoreKeys.CurrentWorkspace)]
    public async Task IAmOnTheTransactionsDetailPageForThatTransaction()
    {
        await IAmOnTheTransactionsPage();
        await WhenIClickOnTheTransactionRow();

        var detailsPage = _context.GetOrCreatePage<TransactionDetailsPage>();
        await detailsPage.WaitForPageReadyAsync();
    }

    #endregion

    #region Steps: WHEN

    [When("I view my transactions")]
    [When("I navigate to the Transactions page")]
    public async Task INavigateToTheTransactionsPage()
    {
        // When: Navigate to transactions page
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();
        await transactionsPage.NavigateAsync();
        await transactionsPage.WaitForLoadingCompleteAsync();

        // Don't need to select workspace because it's already selected
    }

    /// <summary>
    /// Clicks on the transaction row to navigate to the details page.
    /// </summary>
    /// <remarks>
    /// Retrieves transaction payee from object store (stored by data seeding steps),
    /// waits for the transaction row to appear, and clicks it to navigate to the
    /// full details page. Requires TransactionPayee in object store.
    /// </remarks>
    [When("I click on the transaction row")]
    [When("I click on the first seeded transaction")]
    [Requires(ObjectStoreKeys.TransactionPayee)]
    public async Task WhenIClickOnTheTransactionRow()
    {
        // When: Get the payee from object store
        var payee = _context.ObjectStore.Get<string>(ObjectStoreKeys.TransactionPayee);

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
    /// </remarks>
    [Given("I am viewing transactions in {workspaceName}")]
    [When("I view transactions in {workspaceName}")]
    [Provides(ObjectStoreKeys.CurrentWorkspace)]
    public async Task NavigateToTransactionsPageAndSelectWorkspace(string workspaceName)
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
    [Provides(ObjectStoreKeys.HasWorkspaceAccess)]
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
    [Then("I should see {expectedCount} new transactions in the transaction list")]
    public async Task ThenIShouldSeeExactlyTransactions(int expectedCount)
    {
        // Problem: The transactions may not have all loaded yet if we just got redirected here
        // So we will loop and wait for the expected count to appear, up to a timeout

        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();

        var actualCount = await transactionsPage.GetTransactionCountAsync();
        const int maxRetries = 10;
        const int retryDelayMs = 500;
        int retries = 0;
        while (actualCount < expectedCount && retries < maxRetries)
        {
            await Task.Delay(retryDelayMs);
            actualCount = await transactionsPage.GetTransactionCountAsync();
            retries++;
        }

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
    [Requires(ObjectStoreKeys.HasWorkspaceAccess)]
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
    [Requires(ObjectStoreKeys.TransactionPayee)]
    [Requires(ObjectStoreKeys.TransactionMemo)]
    public async Task ThenIShouldSeeTheUpdatedMemoInTheTransactionList()
    {
        // Then: Get the payee and new memo from object store
        var payee = _context.ObjectStore.Get<string>(ObjectStoreKeys.TransactionPayee);
        var expectedMemo = _context.ObjectStore.Get<string>(ObjectStoreKeys.TransactionMemo);

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
    [Requires(ObjectStoreKeys.TransactionPayee)]
    [Requires(ObjectStoreKeys.TransactionCategory)]
    public async Task ThenIShouldSeeTheUpdatedCategoryInTheTransactionList()
    {
        // Then: Get the payee and new category from object store
        var payee = _context.ObjectStore.Get<string>(ObjectStoreKeys.TransactionPayee);
        var expectedCategory = _context.ObjectStore.Get<string>(ObjectStoreKeys.TransactionCategory);

        // And: Wait for page to update (loading spinner to hide)
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();
        await transactionsPage.WaitForLoadingCompleteAsync();
        await transactionsPage.WaitForTransactionAsync(payee);

        // And: Verify the category in the transaction list
        var actualCategory = await transactionsPage.GetTransactionCategoryAsync(payee);

        Assert.That(actualCategory?.Trim(), Is.EqualTo(expectedCategory),
            $"Expected category to be '{expectedCategory}' but was '{actualCategory}'");
    }

    /// <summary>
    /// Verifies that the user has been redirected to the transactions page.
    /// </summary>
    /// <remarks>
    /// Verifies URL contains "/transactions" after an operation that should navigate there.
    /// Does not perform full page setup (workspace selection) as the operation already
    /// set the context. Just confirms we landed on the transactions page and waits for ready.
    /// </remarks>
    [Then("I should be redirected to transactions page")]
    public async Task IShouldBeRedirectedToTransactionsPage()
    {
        // Then: Verify we're on the transactions page
        var expectedUrl = "/transactions";
        var currentUrl = _context.Page.Url;

        Assert.That(currentUrl, Does.Contain(expectedUrl),
            $"Expected to be redirected to transactions page but current URL is {currentUrl}");

        // And: Wait for transactions page to be ready
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();
        await transactionsPage.WaitForPageReadyAsync();
    }

    /// <summary>
    /// Then the transaction should have category {string1}
    /// </summary>
    /// <remarks>
    /// Verifies the category of the single transaction uploaded through bank import.
    /// </remarks>
    [Then("the transaction should have category {string1}")]
    [Requires(ObjectStoreKeys.UploadedTransactionPayees)]
    public async Task TheTransactionShouldHaveCategory(string category)
    {
        var payees = _context.ObjectStore.Get<List<string>>(ObjectStoreKeys.UploadedTransactionPayees);
        Assert.That(payees, Has.Count.EqualTo(1),
            "Expected exactly one uploaded transaction for this singular category assertion");

        var payee = payees.Single();
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();
        await transactionsPage.WaitForTransactionAsync(payee);
        var actualCategory = await transactionsPage.GetTransactionCategoryAsync(payee);
        Assert.That(actualCategory?.Trim(), Is.EqualTo(category),
            $"Imported transaction for '{payee}' should retain category '{category}'");
    }

    /// <summary>
    /// Then I can see many pages of transactions (in the currently-selected workspace)
    /// </summary>
    [Then("I can see many pages of transactions")]
    public async Task ICanSeeManyPagesOfTransactions()
    {
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();
        await transactionsPage.NavigateAsync();

        var totalItems = await transactionsPage.GetTotalItemsAsync();
        Assert.That(totalItems, Is.GreaterThan(100),
            "Expected to see many pages of transactions, but total items count is not greater than 100");
    }

    /// <summary>
    /// Then I can see many pages of transactions in {workspace}
    /// </summary>
    [Then("I can see many pages of transactions in {workspace}")]
    [Provides(ObjectStoreKeys.CurrentWorkspace)]
    public async Task ICanSeeManyPagesOfTransactionsIn(string workspace)
    {
        await NavigateToTransactionsPageAndSelectWorkspace(workspace);
        // * Provides CurrentWorkspaceName

        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();

        // Check the number of items
        var totalItems = await transactionsPage.GetTotalItemsAsync();
        Assert.That(totalItems, Is.GreaterThan(100),
            $"Expected to see many pages of transactions in workspace '{workspace}', but total items count is not greater than 100");
    }

    /// <summary>
    /// Then the transactions for {workspace} is empty
    /// </summary>
    [Then("the transactions for {workspace} is empty")]
    [Provides(ObjectStoreKeys.CurrentWorkspace)]
    public async Task TheTransactionsForIsEmpty(string workspace)
    {
        await NavigateToTransactionsPageAndSelectWorkspace(workspace);
        // * Provides CurrentWorkspaceName

        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();

        await transactionsPage.ExpectEmptyStateAsync();
    }

    [Then("the transactions list is empty")]
    public async Task TheTransactionsListIsEmpty()
    {
        var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();
        await transactionsPage.NavigateAsync();
        await transactionsPage.ExpectEmptyStateAsync();
    }

    #endregion

}
