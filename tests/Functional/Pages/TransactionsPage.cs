using Microsoft.Playwright;
using YoFi.V3.Tests.Functional.Components;

namespace YoFi.V3.Tests.Functional.Pages;

/// <summary>
/// Page Object Model for the Transactions page
/// </summary>
/// <param name="page">The Playwright page instance</param>
/// <remarks>
/// Represents the transactions page that displays and manages transactions for the selected
/// workspace with full CRUD functionality.
/// </remarks>
public class TransactionsPage(IPage page) : BasePage(page)
{
    #region Components

    /// <summary>
    /// Workspace selector component at the top of the page
    /// </summary>
    public WorkspaceSelector WorkspaceSelector => new WorkspaceSelector(Page!, Page!.Locator("body"));

    /// <summary>
    /// Error display component for page-level errors
    /// </summary>
    public ErrorDisplay ErrorDisplay => new ErrorDisplay(Page!.Locator("body"));

    #endregion

    #region Page Elements

    /// <summary>
    /// Main page heading
    /// </summary>
    public ILocator PageHeading => Page!.GetByTestId("page-heading");

    /// <summary>
    /// New Transaction button in the header
    /// </summary>
    public ILocator NewTransactionButton => Page!.GetByTestId("new-transaction-button");

    /// <summary>
    /// Warning message when no workspace is selected
    /// </summary>
    public ILocator NoWorkspaceWarning => Page!.GetByTestId("no-workspace-warning");

    /// <summary>
    /// Loading spinner
    /// </summary>
    public ILocator LoadingSpinner => Page!.GetByTestId("BaseSpinner");

    /// <summary>
    /// Loading text display
    /// </summary>
    public ILocator LoadingText => Page!.GetByTestId("loading-transactions-text");

    /// <summary>
    /// Loading state container
    /// </summary>
    public ILocator LoadingState => Page!.GetByTestId("loading-state");

    #endregion

    #region Date Range Filter Elements

    /// <summary>
    /// Date range filters card
    /// </summary>
    public ILocator DateRangeFilters => Page!.GetByTestId("date-range-filters");

    /// <summary>
    /// From date filter input
    /// </summary>
    public ILocator FromDateInput => Page!.Locator("#fromDate");

    /// <summary>
    /// To date filter input
    /// </summary>
    public ILocator ToDateInput => Page!.Locator("#toDate");

    /// <summary>
    /// Clear Filters button
    /// </summary>
    public ILocator ClearFiltersButton => Page!.GetByTestId("clear-filters-button");

    #endregion

    #region Transactions Table Elements

    /// <summary>
    /// Transactions card container
    /// </summary>
    public ILocator TransactionsCard => Page!.GetByTestId("transactions-card");

    /// <summary>
    /// Transactions table
    /// </summary>
    public ILocator TransactionsTable => Page!.GetByTestId("transactions-table");

    /// <summary>
    /// All transaction rows in the table
    /// </summary>
    public ILocator TransactionRows => TransactionsTable.Locator("tbody tr[data-test-id^='transaction-row-']");

    /// <summary>
    /// Empty state display when no transactions exist
    /// </summary>
    public ILocator EmptyState => Page!.GetByTestId("empty-state");

    /// <summary>
    /// Create first transaction button in empty state
    /// </summary>
    public ILocator EmptyStateCreateButton => Page!.GetByTestId("empty-state-create-button");

    #endregion

    #region Create Modal Elements

    /// <summary>
    /// Create transaction modal
    /// </summary>
    public ILocator CreateModal => Page!.GetByTestId("create-transaction-modal");

    /// <summary>
    /// Date input in create modal
    /// </summary>
    public ILocator CreateDateInput => Page!.GetByTestId("create-transaction-date");

    /// <summary>
    /// Payee input in create modal
    /// </summary>
    public ILocator CreatePayeeInput => Page!.GetByTestId("create-transaction-payee");

    /// <summary>
    /// Amount input in create modal
    /// </summary>
    public ILocator CreateAmountInput => Page!.GetByTestId("create-transaction-amount");

    /// <summary>
    /// Create button in modal
    /// </summary>
    public ILocator CreateButton => CreateModal.GetByTestId("create-submit-button");

    /// <summary>
    /// Cancel button in create modal
    /// </summary>
    public ILocator CreateCancelButton => CreateModal.GetByTestId("create-cancel-button");

    #endregion

    #region Edit Modal Elements

    /// <summary>
    /// Edit transaction modal
    /// </summary>
    public ILocator EditModal => Page!.GetByTestId("edit-transaction-modal");

    /// <summary>
    /// Date input in edit modal
    /// </summary>
    public ILocator EditDateInput => Page!.GetByTestId("edit-transaction-date");

    /// <summary>
    /// Payee input in edit modal
    /// </summary>
    public ILocator EditPayeeInput => Page!.GetByTestId("edit-transaction-payee");

    /// <summary>
    /// Amount input in edit modal
    /// </summary>
    public ILocator EditAmountInput => Page!.GetByTestId("edit-transaction-amount");

    /// <summary>
    /// Update button in edit modal
    /// </summary>
    public ILocator UpdateButton => EditModal.GetByTestId("edit-submit-button");

    /// <summary>
    /// Cancel button in edit modal
    /// </summary>
    public ILocator EditCancelButton => EditModal.GetByTestId("edit-cancel-button");

    #endregion

    #region Delete Modal Elements

    /// <summary>
    /// Delete confirmation modal
    /// </summary>
    public ILocator DeleteModal => Page!.GetByTestId("delete-transaction-modal");

    /// <summary>
    /// Delete transaction details in modal
    /// </summary>
    public ILocator DeleteTransactionDetails => Page!.GetByTestId("delete-transaction-details");

    /// <summary>
    /// Delete button in modal
    /// </summary>
    public ILocator DeleteButton => DeleteModal.GetByTestId("delete-submit-button");

    /// <summary>
    /// Cancel button in delete modal
    /// </summary>
    public ILocator DeleteCancelButton => DeleteModal.GetByTestId("delete-cancel-button");

    #endregion

    #region Navigation

    /// <summary>
    /// Navigates to the transactions page
    /// </summary>
    public async Task NavigateAsync()
    {
        await Page!.GotoAsync("/transactions");
        await Page!.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    #endregion

    #region Create Operations

    /// <summary>
    /// Opens the create transaction modal
    /// </summary>
    public async Task OpenCreateModalAsync()
    {
        await NewTransactionButton.ClickAsync();
        await CreateModal.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    /// <summary>
    /// Creates a new transaction
    /// </summary>
    /// <param name="date">Transaction date in YYYY-MM-DD format</param>
    /// <param name="payee">Payee name</param>
    /// <param name="amount">Transaction amount</param>
    public async Task CreateTransactionAsync(string date, string payee, decimal amount)
    {
        await OpenCreateModalAsync();
        await CreateDateInput.FillAsync(date);
        await CreatePayeeInput.FillAsync(payee);
        await CreateAmountInput.FillAsync(amount.ToString("F2"));
        await CreateButton.ClickAsync();
        await Page!.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Cancels the create transaction modal
    /// </summary>
    public async Task CancelCreateAsync()
    {
        await CreateCancelButton.ClickAsync();
        await CreateModal.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }

    #endregion

    #region Transaction Row Helpers

    /// <summary>
    /// Gets a transaction row by transaction key
    /// </summary>
    /// <param name="transactionKey">The key (GUID) of the transaction</param>
    /// <returns>Locator for the transaction row</returns>
    public ILocator GetTransactionRow(string transactionKey)
    {
        return Page!.GetByTestId($"transaction-row-{transactionKey}");
    }

    /// <summary>
    /// Gets a transaction row by payee name (searches through all rows)
    /// </summary>
    /// <param name="payeeName">The payee name to search for</param>
    /// <returns>Locator for the transaction row</returns>
    public ILocator GetTransactionRowByPayee(string payeeName)
    {
        return TransactionRows.Filter(new() { HasText = payeeName });
    }

    /// <summary>
    /// Checks if a transaction exists in the table by payee name
    /// </summary>
    /// <param name="payeeName">The payee name to search for</param>
    /// <returns>True if the transaction is visible, false otherwise</returns>
    public async Task<bool> HasTransactionAsync(string payeeName)
    {
        return await GetTransactionRowByPayee(payeeName).IsVisibleAsync();
    }

    /// <summary>
    /// Gets the Edit button for a specific transaction by payee name
    /// </summary>
    /// <param name="payeeName">The payee name of the transaction</param>
    /// <returns>Locator for the Edit button</returns>
    public ILocator GetEditButton(string payeeName)
    {
        return GetTransactionRowByPayee(payeeName).GetByTestId("edit-transaction-button");
    }

    /// <summary>
    /// Gets the Delete button for a specific transaction by payee name
    /// </summary>
    /// <param name="payeeName">The payee name of the transaction</param>
    /// <returns>Locator for the Delete button</returns>
    public ILocator GetDeleteButton(string payeeName)
    {
        return GetTransactionRowByPayee(payeeName).GetByTestId("delete-transaction-button");
    }

    #endregion

    #region Edit Operations

    /// <summary>
    /// Opens the edit modal for a specific transaction
    /// </summary>
    /// <param name="payeeName">The payee name of the transaction to edit</param>
    public async Task OpenEditModalAsync(string payeeName)
    {
        await GetEditButton(payeeName).ClickAsync();
        await EditModal.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    /// <summary>
    /// Updates a transaction
    /// </summary>
    /// <param name="originalPayeeName">The original payee name to identify the transaction</param>
    /// <param name="newDate">New transaction date in YYYY-MM-DD format</param>
    /// <param name="newPayee">New payee name</param>
    /// <param name="newAmount">New transaction amount</param>
    public async Task UpdateTransactionAsync(string originalPayeeName, string newDate, string newPayee, decimal newAmount)
    {
        await OpenEditModalAsync(originalPayeeName);
        await EditDateInput.FillAsync(newDate);
        await EditPayeeInput.FillAsync(newPayee);
        await EditAmountInput.FillAsync(newAmount.ToString("F2"));
        await UpdateButton.ClickAsync();
        await Page!.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Cancels the edit transaction modal
    /// </summary>
    public async Task CancelEditAsync()
    {
        await EditCancelButton.ClickAsync();
        await EditModal.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }

    #endregion

    #region Delete Operations

    /// <summary>
    /// Deletes a transaction
    /// </summary>
    /// <param name="payeeName">The payee name of the transaction to delete</param>
    public async Task DeleteTransactionAsync(string payeeName)
    {
        await GetDeleteButton(payeeName).ClickAsync();
        await DeleteModal.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await DeleteButton.ClickAsync();
        await Page!.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Opens the delete modal but doesn't confirm
    /// </summary>
    /// <param name="payeeName">The payee name of the transaction to delete</param>
    public async Task OpenDeleteModalAsync(string payeeName)
    {
        await GetDeleteButton(payeeName).ClickAsync();
        await DeleteModal.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    /// <summary>
    /// Cancels the delete operation
    /// </summary>
    public async Task CancelDeleteAsync()
    {
        await DeleteCancelButton.ClickAsync();
        await DeleteModal.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }

    #endregion

    #region Filter Operations

    /// <summary>
    /// Sets the from date filter
    /// </summary>
    /// <param name="date">Date in YYYY-MM-DD format</param>
    public async Task SetFromDateAsync(string date)
    {
        await FromDateInput.FillAsync(date);
        await Page!.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Sets the to date filter
    /// </summary>
    /// <param name="date">Date in YYYY-MM-DD format</param>
    public async Task SetToDateAsync(string date)
    {
        await ToDateInput.FillAsync(date);
        await Page!.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Sets both from and to date filters
    /// </summary>
    /// <param name="fromDate">From date in YYYY-MM-DD format</param>
    /// <param name="toDate">To date in YYYY-MM-DD format</param>
    public async Task SetDateRangeAsync(string fromDate, string toDate)
    {
        await FromDateInput.FillAsync(fromDate);
        await ToDateInput.FillAsync(toDate);
        await Page!.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    /// Clears all date filters
    /// </summary>
    public async Task ClearFiltersAsync()
    {
        await ClearFiltersButton.ClickAsync();
        await Page!.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    #endregion

    #region Query Methods

    /// <summary>
    /// Gets the count of transaction rows displayed
    /// </summary>
    /// <returns>Number of transaction rows</returns>
    public async Task<int> GetTransactionCountAsync()
    {
        return await Page!.Locator("[data-test-id^='transaction-row-']").CountAsync();
    }

    /// <summary>
    /// Gets the date text from a transaction row by payee name
    /// </summary>
    /// <param name="payeeName">The payee name of the transaction</param>
    /// <returns>The date text as displayed in the table</returns>
    public async Task<string?> GetTransactionDateAsync(string payeeName)
    {
        var row = GetTransactionRowByPayee(payeeName);
        var dateCell = row.Locator("td").First;
        return await dateCell.TextContentAsync();
    }

    /// <summary>
    /// Gets the amount text from a transaction row by payee name
    /// </summary>
    /// <param name="payeeName">The payee name of the transaction</param>
    /// <returns>The amount text as displayed in the table (e.g., "$100.00")</returns>
    public async Task<string?> GetTransactionAmountAsync(string payeeName)
    {
        var row = GetTransactionRowByPayee(payeeName);
        var amountCell = row.Locator("td.text-end").First;
        return await amountCell.TextContentAsync();
    }

    /// <summary>
    /// Checks if the page is in loading state
    /// </summary>
    /// <returns>True if loading spinner is visible, false otherwise</returns>
    public async Task<bool> IsLoadingAsync()
    {
        return await LoadingSpinner.IsVisibleAsync();
    }

    /// <summary>
    /// Waits for the page to finish loading
    /// </summary>
    public async Task WaitForLoadingCompleteAsync()
    {
        await LoadingSpinner.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }

    /// <summary>
    /// Checks if a workspace is currently selected
    /// </summary>
    /// <returns>True if no workspace warning is not visible, false otherwise</returns>
    public async Task<bool> HasWorkspaceSelectedAsync()
    {
        return !(await NoWorkspaceWarning.IsVisibleAsync());
    }

    /// <summary>
    /// Checks if the empty state is displayed
    /// </summary>
    /// <returns>True if empty state message is visible, false otherwise</returns>
    public async Task<bool> IsEmptyStateAsync()
    {
        return await EmptyState.IsVisibleAsync();
    }

    #endregion

    #region Role-based Permission Checks

    /// <summary>
    /// Checks if the New Transaction button is available for interaction.
    /// </summary>
    /// <returns>True if the button is visible and enabled, false otherwise</returns>
    /// <remarks>
    /// This button should only be available to users with Editor or Owner roles.
    /// Viewers should not have access to create transactions.
    /// </remarks>
    public Task<bool> IsNewTransactionAvailableAsync() => IsAvailableAsync(NewTransactionButton);

    /// <summary>
    /// Checks if the Edit button for a specific transaction is available for interaction.
    /// </summary>
    /// <param name="payeeName">The payee name of the transaction</param>
    /// <returns>True if the button is visible and enabled, false otherwise</returns>
    /// <remarks>
    /// This button should only be available to users with Editor or Owner roles.
    /// Viewers should not have access to edit transactions.
    /// </remarks>
    public Task<bool> IsEditAvailableAsync(string payeeName) => IsAvailableAsync(GetEditButton(payeeName));

    /// <summary>
    /// Checks if the Delete button for a specific transaction is available for interaction.
    /// </summary>
    /// <param name="payeeName">The payee name of the transaction</param>
    /// <returns>True if the button is visible and enabled, false otherwise</returns>
    /// <remarks>
    /// This button should only be available to users with Owner role.
    /// Editors and Viewers should not have access to delete transactions.
    /// </remarks>
    public Task<bool> IsDeleteAvailableAsync(string payeeName) => IsAvailableAsync(GetDeleteButton(payeeName));

    #endregion
}
