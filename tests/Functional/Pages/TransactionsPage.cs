using System.Text.RegularExpressions;
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
public partial class TransactionsPage(IPage page) : BasePage(page)
{
    // POST api/tenant/{tenantKey:guid}/Transactions
    [GeneratedRegex(@"/api/tenant/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}/Transactions")]
    private static partial Regex TransactionsApiRegex();

    [GeneratedRegex(@"/api/tenant/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}/Transactions/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
    private static partial Regex SingleTransactionApiRegex();

    /// <summary>
    /// Cached transaction table data to avoid reloading on every query
    /// </summary>
    private TransactionTableData? _cachedTableData;

    #region Components

    /// <summary>
    /// Workspace selector component at the top of the page
    /// </summary>
    public WorkspaceSelector WorkspaceSelector => new WorkspaceSelector(this, Page!.Locator("body"));

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
    public ILocator TransactionRows => TransactionsTable.Locator("tbody tr[data-test-id^='row-']");

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
    /// Memo textarea in create modal
    /// </summary>
    public ILocator CreateMemoInput => Page!.GetByTestId("create-transaction-memo");

    /// <summary>
    /// Source input in create modal
    /// </summary>
    public ILocator CreateSourceInput => Page!.GetByTestId("create-transaction-source");

    /// <summary>
    /// External ID input in create modal
    /// </summary>
    public ILocator CreateExternalIdInput => Page!.GetByTestId("create-transaction-external-id");

    /// <summary>
    /// Category input in create modal
    /// </summary>
    public ILocator CreateCategoryInput => Page!.GetByLabel("Category");

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
    /// Category input in edit modal
    /// </summary>
    public ILocator EditCategoryInput => Page!.GetByLabel("Category");

    /// <summary>
    /// Memo textarea in edit modal
    /// </summary>
    public ILocator EditMemoInput => Page!.GetByTestId("edit-transaction-memo");

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
    /// <param name="waitForReady">Whether to wait for the page to be ready after navigation</param>
    public async Task NavigateAsync(bool waitForReady = true)
    {
        // AB#1981: Call stack here. API wait will never return because we are now on the login page.
        await WaitForApi(async () =>
        {
            await Page!.GotoAsync("/transactions");
        }, TransactionsApiRegex());

        if (waitForReady)
        {
            await WaitForPageReadyAsync();
        }
    }

    /// <summary>
    /// Waits for the page to be ready for interaction
    /// </summary>
    /// <param name="timeout">Timeout in milliseconds (default: 5000)</param>
    /// <remarks>
    /// Waits for the Clear Filters button to become enabled, indicating client hydration is complete.
    /// The Clear Filters button is always present regardless of user permissions, making it a reliable
    /// ready indicator for all roles (Owner, Editor, Viewer). This ensures the Vue client has finished
    /// hydrating and the page is interactive.
    /// See: tests/Functional/NUXT-SSR-TESTING-PATTERN.md
    /// </remarks>
    public async Task WaitForPageReadyAsync(float timeout = 5000)
    {
        await WaitForClearFiltersButtonEnabledAsync(timeout);
    }

    /// <summary>
    /// Waits until the Clear Filters button becomes enabled
    /// </summary>
    /// <param name="timeout">Timeout in milliseconds (default: 5000)</param>
    /// <remarks>
    /// Waits for the button to transition from disabled (SSR/hydration) to enabled (client-ready).
    /// This button is always present regardless of permissions, making it suitable as a ready indicator
    /// for all user roles. This ensures the Vue client has finished hydrating and the page is interactive.
    /// </remarks>
    public async Task WaitForClearFiltersButtonEnabledAsync(float timeout = 5000)
    {
        await ClearFiltersButton.WaitForAsync(new() { State = WaitForSelectorState.Attached, Timeout = timeout });
        await ClearFiltersButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeout });

        // Poll until the button is enabled
        var deadline = DateTime.UtcNow.AddMilliseconds(timeout);
        while (DateTime.UtcNow < deadline)
        {
            var isDisabled = await ClearFiltersButton.IsDisabledAsync();
            if (!isDisabled)
            {
                return; // Button is now enabled - page is ready
            }
            await Task.Delay(50);
        }

        throw new TimeoutException($"Clear Filters button did not become enabled within {timeout}ms");
    }

    /// <summary>
    /// Waits until the New Transaction button becomes enabled
    /// </summary>
    /// <param name="timeout">Timeout in milliseconds (default: 5000)</param>
    /// <remarks>
    /// Waits for the button to transition from disabled (SSR/hydration) to enabled (client-ready).
    /// Note: This method is only useful for users with Editor or Owner roles who have the button.
    /// For general page readiness, use WaitForPageReadyAsync() which works for all permission levels.
    /// </remarks>
    public async Task WaitForNewTransactionButtonEnabledAsync(float timeout = 5000)
    {
        await NewTransactionButton.WaitForAsync(new() { State = WaitForSelectorState.Attached, Timeout = timeout });
        await NewTransactionButton.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeout });

        // Poll until the button is enabled
        var deadline = DateTime.UtcNow.AddMilliseconds(timeout);
        while (DateTime.UtcNow < deadline)
        {
            var isDisabled = await NewTransactionButton.IsDisabledAsync();
            if (!isDisabled)
            {
                return; // Button is now enabled - page is ready
            }
            await Task.Delay(50);
        }

        throw new TimeoutException($"New Transaction button did not become enabled within {timeout}ms");
    }

    #endregion

    #region Create Operations - Single Actions

    /// <summary>
    /// Opens the create transaction modal
    /// </summary>
    /// <remarks>
    /// Single action method. Use this when you need fine-grained control over the create workflow,
    /// such as testing form validation or cancellation flows.
    /// </remarks>
    public async Task OpenCreateModalAsync()
    {
        await NewTransactionButton.ClickAsync();
        await CreateModal.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    /// <summary>
    /// Fills the date field in the create transaction modal
    /// </summary>
    /// <param name="date">Transaction date in YYYY-MM-DD format</param>
    /// <remarks>
    /// Single action method. Use this when you need to test partial form submission or validation.
    /// </remarks>
    public async Task FillCreateDateAsync(string date)
    {
        await CreateDateInput.FillAsync(date);
    }

    /// <summary>
    /// Fills the payee field in the create transaction modal
    /// </summary>
    /// <param name="payee">Payee name</param>
    /// <remarks>
    /// Single action method. Use this when you need to test partial form submission or validation.
    /// </remarks>
    public async Task FillCreatePayeeAsync(string payee)
    {
        await CreatePayeeInput.FillAsync(payee);
    }

    /// <summary>
    /// Fills the amount field in the create transaction modal
    /// </summary>
    /// <param name="amount">Transaction amount</param>
    /// <remarks>
    /// Single action method. Use this when you need to test partial form submission or validation.
    /// </remarks>
    public async Task FillCreateAmountAsync(decimal amount)
    {
        await CreateAmountInput.FillAsync(amount.ToString("F2"));
    }

    /// <summary>
    /// Fills the memo field in the create transaction modal
    /// </summary>
    /// <param name="memo">Transaction memo</param>
    /// <remarks>
    /// Single action method. Use this when you need to test partial form submission or validation.
    /// </remarks>
    public async Task FillCreateMemoAsync(string memo)
    {
        await CreateMemoInput.FillAsync(memo);
    }

    /// <summary>
    /// Fills the source field in the create transaction modal
    /// </summary>
    /// <param name="source">Transaction source (bank account)</param>
    /// <remarks>
    /// Single action method. Use this when you need to test partial form submission or validation.
    /// </remarks>
    public async Task FillCreateSourceAsync(string source)
    {
        await CreateSourceInput.FillAsync(source);
    }

    /// <summary>
    /// Fills the external ID field in the create transaction modal
    /// </summary>
    /// <param name="externalId">Bank's unique transaction identifier</param>
    /// <remarks>
    /// Single action method. Use this when you need to test partial form submission or validation.
    /// </remarks>
    public async Task FillCreateExternalIdAsync(string externalId)
    {
        await CreateExternalIdInput.FillAsync(externalId);
    }

    /// <summary>
    /// Fills the category field in the create transaction modal
    /// </summary>
    /// <param name="category">Transaction category</param>
    /// <remarks>
    /// Single action method. Use this when you need to test partial form submission or validation.
    /// </remarks>
    public async Task FillCreateCategoryAsync(string category)
    {
        await CreateCategoryInput.FillAsync(category);
    }

    /// <summary>
    /// Clicks the create button and waits for the create transaction API call
    /// </summary>
    /// <remarks>
    /// Single action method. Use this when you need to control when the form is submitted,
    /// such as after taking a screenshot or checking form state.
    /// </remarks>
    public async Task SubmitCreateFormAsync()
    {
        await WaitForApi(async () =>
        {
            await CreateButton.ClickAsync();
        }, TransactionsApiRegex());

        // Wait for the loading spinner to disappear, indicating UI has updated
        await WaitForLoadingCompleteAsync();
    }

    /// <summary>
    /// Cancels the create transaction modal
    /// </summary>
    /// <remarks>
    /// Single action method. Use this when testing cancel workflows.
    /// </remarks>
    public async Task CancelCreateAsync()
    {
        await CreateCancelButton.ClickAsync();
        await CreateModal.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }

    #endregion

    #region Create Operations - Common Workflows

    /// <summary>
    /// Creates a new transaction
    /// </summary>
    /// <param name="date">Transaction date in YYYY-MM-DD format</param>
    /// <param name="payee">Payee name</param>
    /// <param name="amount">Transaction amount</param>
    /// <remarks>
    /// High-level workflow method for the common "happy path" create scenario.
    /// For fine-grained control (e.g., testing validation, cancellation, or taking screenshots mid-flow),
    /// use the individual action methods: OpenCreateModalAsync, FillCreateDateAsync,
    /// FillCreatePayeeAsync, FillCreateAmountAsync, and SubmitCreateFormAsync.
    /// </remarks>
    public async Task CreateTransactionAsync(string date, string payee, decimal amount)
    {
        await OpenCreateModalAsync();
        await FillCreateDateAsync(date);
        await FillCreatePayeeAsync(payee);
        await FillCreateAmountAsync(amount);
        await SubmitCreateFormAsync();
    }

    #endregion

    #region Transaction Row Helpers

    /// <summary>
    /// Represents a row in the transactions table with column data indexed by data-test-id
    /// </summary>
    public class TransactionRowData
    {
        /// <summary>
        /// Dictionary of column data-test-id to cell text content
        /// </summary>
        public Dictionary<string, string> Columns { get; } = new();

        /// <summary>
        /// The row index (0-based) in the table
        /// </summary>
        public int RowIndex { get; set; }

        /// <summary>
        /// Gets the Playwright locator for this specific row
        /// </summary>
        public required ILocator RowLocator { get; init; }
    }

    /// <summary>
    /// Represents the loaded transaction table data with column index mapping
    /// </summary>
    public class TransactionTableData
    {
        /// <summary>
        /// List of all transaction rows
        /// </summary>
        public List<TransactionRowData> Rows { get; init; } = new();

        /// <summary>
        /// Mapping from column data-test-id to column index (0-based)
        /// </summary>
        public Dictionary<string, int> ColumnIndexMap { get; init; } = new();
    }

    /// <summary>
    /// Loads all transaction table data into memory for LINQ querying
    /// </summary>
    /// <param name="forceReload">If true, bypasses cache and reloads from DOM</param>
    /// <returns>Transaction table data with rows and column mappings</returns>
    private async Task<TransactionTableData> LoadTransactionTableDataAsync(bool forceReload = false)
    {
        if (!forceReload && _cachedTableData != null)
        {
            return _cachedTableData;
        }

        var result = new TransactionTableData();

        // Get column mapping: data-test-id -> index and index -> data-test-id
        var headers = TransactionsTable.Locator("thead th");
        var headerCount = await headers.CountAsync();
        var indexToTestId = new Dictionary<int, string>(); // index -> data-test-id

        for (int i = 0; i < headerCount; i++)
        {
            var header = headers.Nth(i);
            var testId = await header.GetAttributeAsync("data-test-id");
            if (!string.IsNullOrEmpty(testId))
            {
                indexToTestId[i] = testId;
                result.ColumnIndexMap[testId] = i;
            }
        }

        // Load all row data
        var rows = TransactionRows;
        var rowCount = await rows.CountAsync();

        for (int rowIdx = 0; rowIdx < rowCount; rowIdx++)
        {
            var row = rows.Nth(rowIdx);
            var rowData = new TransactionRowData
            {
                RowIndex = rowIdx,
                RowLocator = row
            };

            var cells = row.Locator("td");
            var cellCount = await cells.CountAsync();

            for (int colIdx = 0; colIdx < cellCount; colIdx++)
            {
                if (indexToTestId.TryGetValue(colIdx, out var columnTestId))
                {
                    var cell = cells.Nth(colIdx);
                    var cellText = await cell.TextContentAsync();
                    rowData.Columns[columnTestId] = cellText?.Trim() ?? "";
                }
            }

            result.Rows.Add(rowData);
        }

        _cachedTableData = result;
        return result;
    }

    /// <summary>
    /// Reloads the transaction table data from the DOM, bypassing the cache
    /// </summary>
    /// <remarks>
    /// Call this method after operations that modify the table (create, update, delete)
    /// to ensure subsequent queries reflect the current state of the table.
    /// </remarks>
    public async Task ReloadTransactionTableDataAsync()
    {
        await LoadTransactionTableDataAsync(forceReload: true);
    }

    /// <summary>
    /// Gets a transaction row by transaction key
    /// </summary>
    /// <param name="transactionKey">The key (GUID) of the transaction</param>
    /// <returns>Locator for the transaction row</returns>
    public ILocator GetTransactionRow(string transactionKey)
    {
        // TODO: Better would be to start from TransactionTable not Page
        return Page!.GetByTestId($"row-{transactionKey}");
    }

    public async Task WaitForTransactionRowByKeyAsync(Guid transactionKey, float timeout = 5000)
    {
        var row = GetTransactionRow(transactionKey.ToString());
        await row.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeout });

        // Clear cached table data to ensure fresh data on next query
        _cachedTableData = null;
    }

    public async Task<Guid> GetTransactionKeyByPayeeAsync(string payeeName)
    {
        var row = await GetTransactionRowByPayeeAsync(payeeName);
        var testId = await row.GetAttributeAsync("data-test-id") ?? throw new InvalidOperationException("Transaction row missing data-test-id attribute");

        // TODO: Compiled regex for consistency
        var match = Regex.Match(testId, @"row-([0-9a-fA-F\-]{36})");
        if (match.Success && Guid.TryParse(match.Groups[1].Value, out var transactionKey))
        {
            return transactionKey;
        }
        throw new ArgumentException($"Transaction key not found or invalid for payee '{payeeName}'");
    }

    /// <summary>
    /// Gets a transaction row by payee name (searches through all rows)
    /// </summary>
    /// <param name="payeeName">The payee name to search for</param>
    /// <returns>Locator for the transaction row</returns>
    public async Task<ILocator> GetTransactionRowByPayeeAsync(string payeeName)
    {
        var tableData = await LoadTransactionTableDataAsync();
        var rowData = tableData.Rows.FirstOrDefault(r => r.Columns.TryGetValue("payee", out var payee) && payee == payeeName);

        if (rowData == null)
        {
            // Return a locator that won't match anything
            return TransactionRows.Filter(new() { HasText = $"__PAYEE_NOT_FOUND_{payeeName}__" });
        }

        return rowData.RowLocator;
    }

    /// <summary>
    /// Gets a specific cell in the transactions table by row (payee) and column (data-test-id)
    /// </summary>
    /// <param name="payee">The payee name to identify the row</param>
    /// <param name="column">The data-test-id of the column header (e.g., "category", "amount")</param>
    /// <returns>Locator for the table cell at the intersection of the specified row and column</returns>
    /// <remarks>
    /// This method loads the table data and finds the correct cell by matching payee in the payee column,
    /// then uses the pre-loaded column index mapping to efficiently locate the cell.
    /// Example: TransactionsTableCell("Acme Corp", "category") returns the category cell for Acme Corp's row.
    /// </remarks>
    public async Task<ILocator> TransactionsTableCell(string payee, string column)
    {
        var tableData = await LoadTransactionTableDataAsync();
        var rowData = tableData.Rows.FirstOrDefault(r => r.Columns.TryGetValue("payee", out var p) && p == payee);

        if (rowData == null)
        {
            throw new ArgumentException($"Transaction row with payee '{payee}' not found");
        }

        if (!tableData.ColumnIndexMap.TryGetValue(column, out var columnIndex))
        {
            throw new ArgumentException($"Column with data-test-id '{column}' not found in table headers");
        }

        return rowData.RowLocator.Locator("td").Nth(columnIndex);
    }

    public async Task<string> TransactionsTableCellText(string payee, string column)
    {
        var tableData = await LoadTransactionTableDataAsync();
        var rowData = tableData.Rows.FirstOrDefault(r => r.Columns.TryGetValue("payee", out var p) && p == payee);

        if (rowData == null)
        {
            throw new ArgumentException($"Transaction row with payee '{payee}' not found");
        }

        return rowData.Columns.TryGetValue(column, out var cellText) ? cellText : throw new ArgumentException($"Column with data-test-id '{column}' not found in row for payee '{payee}'");
    }

    public async Task<TransactionRowData?> GetTransactionRowDataByPayeeAsync(string payeeName)
    {
        var tableData = await LoadTransactionTableDataAsync();
        return tableData.Rows.FirstOrDefault(r => r.Columns.TryGetValue("payee", out var payee) && payee == payeeName);
    }

    /// <summary>
    /// Checks if a transaction exists in the table by payee name
    /// </summary>
    /// <param name="payeeName">The payee name to search for</param>
    /// <returns>True if the transaction is visible, false otherwise</returns>
    public async Task<bool> HasTransactionAsync(string payeeName)
    {
        var row = await GetTransactionRowByPayeeAsync(payeeName);
        return await row.IsVisibleAsync();
    }

    /// <summary>
    /// Gets the Edit button for a specific transaction by payee name
    /// </summary>
    /// <param name="payeeName">The payee name of the transaction</param>
    /// <returns>Locator for the Edit button</returns>
    public async Task<ILocator> GetEditButton(string payeeName)
    {
        var row = await GetTransactionRowByPayeeAsync(payeeName);
        return row.GetByTestId("edit-transaction-button");
    }

    /// <summary>
    /// Gets the Delete button for a specific transaction by payee name
    /// </summary>
    /// <param name="payeeName">The payee name of the transaction</param>
    /// <returns>Locator for the Delete button</returns>
    public async Task<ILocator> GetDeleteButton(string payeeName)
    {
        var row = await GetTransactionRowByPayeeAsync(payeeName);
        return row.GetByTestId("delete-transaction-button");
    }

    #endregion

    #region Edit Operations - Single Actions

    /// <summary>
    /// Opens the edit modal for a specific transaction
    /// </summary>
    /// <param name="payeeName">The payee name of the transaction to edit</param>
    /// <remarks>
    /// Single action method. Use this when you need fine-grained control over the edit workflow,
    /// such as testing form validation, checking intermediate states, or cancellation flows.
    /// </remarks>
    public async Task OpenEditModalAsync(string payeeName)
    {
        var editButton = await GetEditButton(payeeName);
        await editButton.ClickAsync();
        await EditModal.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    /// <summary>
    /// Fills the date field in the edit transaction modal
    /// </summary>
    /// <param name="newDate">New transaction date in YYYY-MM-DD format</param>
    /// <remarks>
    /// Single action method. Use this when you need to test partial form updates or validation.
    /// Must be called after OpenEditModalAsync.
    /// </remarks>
    public async Task FillEditDateAsync(string newDate)
    {
        await EditDateInput.FillAsync(newDate);
    }

    /// <summary>
    /// Fills the payee field in the edit transaction modal
    /// </summary>
    /// <param name="newPayee">New payee name</param>
    /// <remarks>
    /// Single action method. Use this when you need to test partial form updates or validation.
    /// Must be called after OpenEditModalAsync.
    /// </remarks>
    public async Task FillEditPayeeAsync(string newPayee)
    {
        await EditPayeeInput.FillAsync(newPayee);
    }

    /// <summary>
    /// Fills the memo field in the edit transaction modal
    /// </summary>
    /// <param name="newMemo">New memo text</param>
    /// <remarks>
    /// Single action method. Use this when you need to test partial form updates or validation.
    /// Must be called after OpenEditModalAsync.
    /// </remarks>
    public async Task FillEditMemoAsync(string newMemo)
    {
        await EditMemoInput.FillAsync(newMemo);
    }

    /// <summary>
    /// Fills the amount field in the edit transaction modal
    /// </summary>
    /// <param name="newAmount">New transaction amount</param>
    /// <remarks>
    /// Single action method. Use this when you need to test partial form updates or validation.
    /// Must be called after OpenEditModalAsync.
    /// </remarks>
    public async Task FillEditAmountAsync(decimal newAmount)
    {
        await EditAmountInput.FillAsync(newAmount.ToString("F2"));
    }

    /// <summary>
    /// Fills the category field in the edit transaction modal
    /// </summary>
    /// <param name="newCategory">New category name</param>
    /// <remarks>
    /// Single action method. Use this when you need to test partial form updates or validation.
    /// Must be called after OpenEditModalAsync.
    /// </remarks>
    public async Task FillEditCategoryAsync(string newCategory)
    {
        await EditCategoryInput.FillAsync(newCategory);
    }

    /// <summary>
    /// Clicks the update button and waits for the update transaction API call
    /// </summary>
    /// <remarks>
    /// Single action method. Use this when you need to control when the form is submitted,
    /// such as after taking a screenshot or simulating network conditions.
    /// Must be called after OpenEditModalAsync.
    /// </remarks>
    public async Task SubmitEditFormAsync()
    {
        await WaitForApi(async () =>
        {
            await UpdateButton.ClickAsync();
        }, SingleTransactionApiRegex());

        // Wait for the loading spinner to disappear, indicating UI has updated
        await WaitForLoadingCompleteAsync();
    }

    /// <summary>
    /// Cancels the edit transaction modal
    /// </summary>
    /// <remarks>
    /// Single action method. Use this when testing cancel workflows.
    /// </remarks>
    public async Task CancelEditAsync()
    {
        await EditCancelButton.ClickAsync();
        await EditModal.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }

    #endregion

    #region Edit Operations - Common Workflows

    /// <summary>
    /// Updates a transaction
    /// </summary>
    /// <param name="originalPayeeName">The original payee name to identify the transaction</param>
    /// <param name="newDate">New transaction date in YYYY-MM-DD format</param>
    /// <param name="newPayee">New payee name</param>
    /// <param name="newAmount">New transaction amount</param>
    /// <remarks>
    /// High-level workflow method for the common "happy path" update scenario.
    /// For fine-grained control (e.g., testing validation, cancellation, network errors, or taking screenshots mid-flow),
    /// use the individual action methods: OpenEditModalAsync, FillEditDateAsync,
    /// FillEditPayeeAsync, FillEditAmountAsync, and SubmitEditFormAsync.
    /// </remarks>
    public async Task UpdateTransactionAsync(string originalPayeeName, string newDate, string newPayee, decimal newAmount)
    {
        await OpenEditModalAsync(originalPayeeName);
        await FillEditDateAsync(newDate);
        await FillEditPayeeAsync(newPayee);
        await FillEditAmountAsync(newAmount);
        await SubmitEditFormAsync();
    }

    #endregion

    #region Delete Operations - Single Actions

    /// <summary>
    /// Opens the delete confirmation modal for a transaction
    /// </summary>
    /// <param name="payeeName">The payee name of the transaction to delete</param>
    /// <remarks>
    /// Single action method. Use this when you need fine-grained control over the delete workflow,
    /// such as testing modal display, reading deletion warnings, or cancellation flows.
    /// </remarks>
    public async Task OpenDeleteModalAsync(string payeeName)
    {
        var deleteButton = await GetDeleteButton(payeeName);
        await deleteButton.ClickAsync();
        await DeleteModal.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    /// <summary>
    /// Clicks the delete button in the modal and waits for the delete transaction API call
    /// </summary>
    /// <remarks>
    /// Single action method. Use this when you need to control when the deletion is confirmed,
    /// such as after taking a screenshot or checking modal content.
    /// Must be called after OpenDeleteModalAsync.
    /// </remarks>
    public async Task ConfirmDeleteAsync()
    {
        await WaitForApi(async () =>
        {
            await DeleteButton.ClickAsync();
        }, SingleTransactionApiRegex());

        // Wait for the loading spinner to disappear, indicating UI has updated
        await WaitForLoadingCompleteAsync();
    }

    /// <summary>
    /// Cancels the delete operation by closing the modal
    /// </summary>
    /// <remarks>
    /// Single action method. Use this when testing cancel workflows.
    /// </remarks>
    public async Task CancelDeleteAsync()
    {
        await DeleteCancelButton.ClickAsync();
        await DeleteModal.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }

    #endregion

    #region Delete Operations - Common Workflows

    /// <summary>
    /// Deletes a transaction
    /// </summary>
    /// <param name="payeeName">The payee name of the transaction to delete</param>
    /// <remarks>
    /// High-level workflow method for the common "happy path" delete scenario.
    /// For fine-grained control (e.g., testing confirmation modal, cancellation, or taking screenshots),
    /// use the individual action methods: OpenDeleteModalAsync, ConfirmDeleteAsync, and CancelDeleteAsync.
    /// </remarks>
    public async Task DeleteTransactionAsync(string payeeName)
    {
        await OpenDeleteModalAsync(payeeName);
        await ConfirmDeleteAsync();
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
        // Don't need to wait here. Nothing happens when we modify filters
    }

    /// <summary>
    /// Sets the to date filter
    /// </summary>
    /// <param name="date">Date in YYYY-MM-DD format</param>
    public async Task SetToDateAsync(string date)
    {
        await ToDateInput.FillAsync(date);
        // Don't need to wait here. Nothing happens when we modify filters
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
        // Don't need to wait here. Nothing happens when we modify filters
    }

    /// <summary>
    /// Clears all date filters
    /// </summary>
    public async Task ClearFiltersAsync()
    {
        await ClearFiltersButton.ClickAsync();

        // Don't need to wait here. Nothing happens when we modify filters
    }

    #endregion

    #region Query Methods

    /// <summary>
    /// Gets the count of transaction rows displayed
    /// </summary>
    /// <returns>Number of transaction rows</returns>
    public async Task<int> GetTransactionCountAsync()
    {
        return await Page!.Locator("[data-test-id^='row-']").CountAsync();
    }

    /// <summary>
    /// Gets the date text from a transaction row by payee name
    /// </summary>
    /// <param name="payeeName">The payee name of the transaction</param>
    /// <returns>The date text as displayed in the table</returns>
    public async Task<string?> GetTransactionDateAsync(string payeeName)
    {
        var row = await GetTransactionRowByPayeeAsync(payeeName);
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
        var row = await GetTransactionRowByPayeeAsync(payeeName);
        var amountCell = row.Locator("td.text-end").First;
        return await amountCell.TextContentAsync();
    }

    /// <summary>
    /// Gets the memo text from a transaction row by payee name
    /// </summary>
    /// <param name="payeeName">The payee name of the transaction</param>
    /// <returns>The memo text as displayed in the table (may be truncated)</returns>
    public async Task<string?> GetTransactionMemoAsync(string payeeName)
    {
        var row = await GetTransactionRowByPayeeAsync(payeeName);
        var memoCell = row.Locator("td.memo-cell");
        return await memoCell.TextContentAsync();
    }

    /// <summary>
    /// Gets the category text from a transaction row by payee name
    /// </summary>
    /// <param name="payeeName">The payee name of the transaction</param>
    /// <returns>The category text as displayed in the table</returns>
    public async Task<string?> GetTransactionCategoryAsync(string payeeName)
    {
        return await TransactionsTableCellText(payeeName, "category");
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
    /// Waits for a transaction with the specified payee name to appear in the list
    /// </summary>
    /// <param name="payeeName">The payee name to wait for</param>
    /// <param name="timeout">Timeout in milliseconds (default: 5000)</param>
    /// <remarks>
    /// Use this after create or update operations to ensure the transaction list has been fully rendered
    /// </remarks>
    public async Task WaitForTransactionAsync(string payeeName, float timeout = 5000)
    {
        // We can't use GetTransactionCountAsync here because that now presumes the table is loaded.
        // We need to make a locator for the specific row and wait for it to appear.

        // The locator is TransactionRows filtered where 2nd TD has exact match on payeeName
        var row = TransactionsTable.Locator($"tbody tr:has(td:nth-child(2):text-is('{payeeName}'))");

        await row.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeout });

        // We only call this when we're loading. So clear the cached table to force
        // a reload next time.
        _cachedTableData = null;
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

    /// <summary>
    /// Gets the payee name from the first transaction in the table
    /// </summary>
    /// <returns>The payee name of the first transaction, or null if no transactions exist</returns>
    public async Task<string?> GetFirstTransactionPayeeAsync()
    {
        var count = await GetTransactionCountAsync();
        if (count == 0)
            return null;

        var firstRow = TransactionRows.First;
        var payeeCell = firstRow.Locator("td").Nth(1); // Second column is payee
        return await payeeCell.TextContentAsync();
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
    public async Task<bool> IsEditAvailableAsync(string payeeName)
    {
        var editButton = await GetEditButton(payeeName);
        return await IsAvailableAsync(editButton);
    }

    /// <summary>
    /// Checks if the Delete button for a specific transaction is available for interaction.
    /// </summary>
    /// <param name="payeeName">The payee name of the transaction</param>
    /// <returns>True if the button is visible and enabled, false otherwise</returns>
    /// <remarks>
    /// This button should only be available to users with Owner role.
    /// Editors and Viewers should not have access to delete transactions.
    /// </remarks>
    public async Task<bool> IsDeleteAvailableAsync(string payeeName)
    {
        var deleteButton = await GetDeleteButton(payeeName);
        return await IsAvailableAsync(deleteButton);
    }

    #endregion
}
