using System.Text.RegularExpressions;
using Microsoft.Playwright;
using YoFi.V3.Tests.Functional.Components;

namespace YoFi.V3.Tests.Functional.Pages;

/// <summary>
/// Page Object Model for the Import page
/// </summary>
/// <param name="page">The Playwright page instance</param>
/// <remarks>
/// Represents the import review page where users upload OFX/QFX files,
/// review transactions, and manage import selections.
/// </remarks>
public partial class ImportPage(IPage page) : BasePage(page)
{
    // GET api/tenant/{tenantKey:guid}/import/review
    [GeneratedRegex(@"/api/tenant/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}/import/review")]
    private static partial Regex ImportReviewApiRegex();

    // POST api/tenant/{tenantKey:guid}/import/upload
    [GeneratedRegex(@"/api/tenant/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}/import/upload")]
    private static partial Regex ImportUploadApiRegex();

    // POST api/tenant/{tenantKey:guid}/import/review/complete
    [GeneratedRegex(@"/api/tenant/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}/import/review/complete")]
    private static partial Regex ImportCompleteApiRegex();

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
    /// File input for uploading OFX/QFX files
    /// </summary>
    public ILocator FileInput => Page!.GetByTestId("file-input");

    /// <summary>
    /// Upload button
    /// </summary>
    public ILocator UploadButton => Page!.GetByTestId("upload-button");

    /// <summary>
    /// No workspace warning message
    /// </summary>
    public ILocator NoWorkspaceWarning => Page!.GetByTestId("no-workspace-warning");

    /// <summary>
    /// Permission denied error message
    /// </summary>
    public ILocator PermissionDeniedError => Page!.GetByTestId("permission-denied-error");

    /// <summary>
    /// Empty state display when no pending imports exist
    /// </summary>
    public ILocator EmptyState => Page!.GetByTestId("empty-state");

    /// <summary>
    /// Loading state display
    /// </summary>
    public ILocator LoadingState => Page!.GetByTestId("loading-state");

    /// <summary>
    /// Duplicate review alert
    /// </summary>
    public ILocator DuplicateReviewAlert => Page!.GetByTestId("duplicate-review-alert");

    #endregion

    #region Transaction Table Elements

    /// <summary>
    /// Import review table
    /// </summary>
    public ILocator ImportReviewTable => Page!.GetByTestId("import-review-table");

    /// <summary>
    /// All transaction rows in the import review table
    /// </summary>
    public ILocator TransactionRows => ImportReviewTable.Locator("tbody tr[data-test-id^='row-']");

    /// <summary>
    /// Gets a transaction row by key
    /// </summary>
    /// <param name="key">The transaction key (GUID)</param>
    public ILocator GetTransactionRow(string key) => Page!.GetByTestId($"row-{key}");

    /// <summary>
    /// Gets a transaction checkbox by key
    /// </summary>
    /// <param name="key">The transaction key (GUID)</param>
    public ILocator GetTransactionCheckbox(string key) => Page!.GetByTestId($"transaction-checkbox-{key}");

    #endregion

    #region Selection Control Elements

    /// <summary>
    /// Select All button
    /// </summary>
    public ILocator SelectAllButton => Page!.GetByTestId("select-all-button");

    /// <summary>
    /// Deselect All button
    /// </summary>
    public ILocator DeselectAllButton => Page!.GetByTestId("deselect-all-button");

    #endregion

    #region Action Button Elements

    /// <summary>
    /// Import button (opens confirmation modal)
    /// </summary>
    public ILocator ImportButton => Page!.GetByTestId("import-button");

    /// <summary>
    /// Delete All button
    /// </summary>
    public ILocator DeleteAllButton => Page!.GetByTestId("delete-all-button");

    #endregion

    #region Modal Elements

    /// <summary>
    /// Import confirmation modal
    /// </summary>
    public ILocator ImportConfirmModal => Page!.GetByTestId("import-confirm-modal");

    /// <summary>
    /// Import confirmation button (in modal)
    /// </summary>
    public ILocator ImportConfirmButton => Page!.GetByTestId("import-confirm-button");

    /// <summary>
    /// Import cancel button (in modal)
    /// </summary>
    public ILocator ImportCancelButton => Page!.GetByTestId("import-cancel-button");

    /// <summary>
    /// Delete All confirmation modal
    /// </summary>
    public ILocator DeleteAllModal => Page!.GetByTestId("delete-all-modal");

    /// <summary>
    /// Delete All confirmation button (in modal)
    /// </summary>
    public ILocator DeleteAllConfirmButton => Page!.GetByTestId("delete-all-submit-button");

    /// <summary>
    /// Delete All cancel button (in modal)
    /// </summary>
    public ILocator DeleteAllCancelButton => Page!.GetByTestId("delete-all-cancel-button");

    #endregion

    #region Navigation

    /// <summary>
    /// Navigates to the import page
    /// </summary>
    /// <param name="waitForReady">Whether to wait for the page to be ready after navigation</param>
    public async Task NavigateAsync(bool waitForReady = true)
    {
        await WaitForApi(async () =>
        {
            await Page!.GotoAsync("/import");
        }, ImportReviewApiRegex());

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
    /// Waits for the upload button to become enabled, indicating client hydration is complete.
    /// The upload button is always present for users with Editor/Owner roles.
    /// </remarks>
    public async Task WaitForPageReadyAsync(float timeout = 5000)
    {
        await WaitForFileInputEnabledAsync(timeout);
    }

    /// <summary>
    /// Waits until the file input becomes enabled
    /// </summary>
    /// <param name="timeout">Timeout in milliseconds (default: 5000)</param>
    public async Task WaitForFileInputEnabledAsync(float timeout = 5000)
    {
        await WaitForEnabled(FileInput, timeout);
    }

    #endregion

    #region Upload Operations

    /// <summary>
    /// Uploads an OFX file from the test sample data directory
    /// </summary>
    /// <param name="filename">The filename (e.g., "checking-jan-2024.ofx")</param>
    public async Task UploadFileAsync(string filename)
    {
        // Get absolute path to test data file
        var projectRoot = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", ".."));
        var filePath = Path.Combine(projectRoot, "SampleData", "Ofx", filename);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Test data file not found: {filePath}");
        }

        // Set the file input (this doesn't trigger the change event or upload)
        await FileInput.SetInputFilesAsync(filePath);

        // Click the upload button and wait for the upload API to complete
        await WaitForApi(async () =>
        {
            await UploadButton.ClickAsync();
        }, ImportUploadApiRegex());
    }

    /// <summary>
    /// Waits for the upload operation to complete
    /// </summary>
    public async Task WaitForUploadCompleteAsync(float timeout = 10000)
    {
        // Wait for upload button to be enabled again (it's disabled during upload)
        await WaitForFileInputEnabledAsync(timeout);
    }

    #endregion

    #region Transaction Query Operations

    /// <summary>
    /// Gets the count of transactions in the import review table
    /// </summary>
    public async Task<int> GetTransactionCountAsync()
    {
        return await TransactionRows.CountAsync();
    }

    /// <summary>
    /// Gets the count of selected transactions
    /// </summary>
    public async Task<int> GetSelectedCountAsync()
    {
        int selectedCount = 0;
        var count = await TransactionRows.CountAsync();

        for (int i = 0; i < count; i++)
        {
            var row = TransactionRows.Nth(i);
            var checkbox = row.Locator("input[type='checkbox']");
            if (await checkbox.IsCheckedAsync())
            {
                selectedCount++;
            }
        }

        return selectedCount;
    }

    /// <summary>
    /// Gets the count of deselected transactions
    /// </summary>
    public async Task<int> GetDeselectedCountAsync()
    {
        var totalCount = await GetTransactionCountAsync();
        var selectedCount = await GetSelectedCountAsync();
        return totalCount - selectedCount;
    }

    /// <summary>
    /// Gets the count of warning transactions (potential duplicates)
    /// </summary>
    /// <remarks>
    /// Warning transactions are rows with the 'table-warning' CSS class,
    /// indicating they are potential duplicates.
    /// </remarks>
    public async Task<int> GetWarningTransactionCountAsync()
    {
        var warningRows = ImportReviewTable.Locator("tbody tr.table-warning");
        return await warningRows.CountAsync();
    }

    /// <summary>
    /// Checks if a transaction is selected
    /// </summary>
    /// <param name="index">Zero-based index of the transaction row</param>
    public async Task<bool> IsTransactionSelectedAsync(int index)
    {
        var row = TransactionRows.Nth(index);
        var checkbox = row.Locator("input[type='checkbox']");
        return await checkbox.IsCheckedAsync();
    }

    /// <summary>
    /// Checks if the empty state is displayed
    /// </summary>
    public async Task<bool> IsEmptyStateAsync()
    {
        return await EmptyState.IsVisibleAsync();
    }

    /// <summary>
    /// Gets all payee names for transactions in the import review table
    /// </summary>
    /// <returns>List of payee names from all transaction rows</returns>
    public async Task<List<string>> GetAllTransactionPayeesAsync()
    {
        var payees = new List<string>();
        var rows = TransactionRows;
        var rowCount = await rows.CountAsync();

        for (int i = 0; i < rowCount; i++)
        {
            var row = rows.Nth(i);
            // Payee is in the 3rd column (index 2) - after the checkbox column
            var payeeCell = row.Locator("td").Nth(2);
            var payeeText = await payeeCell.TextContentAsync();

            if (!string.IsNullOrEmpty(payeeText))
            {
                payees.Add(payeeText.Trim());
            }
        }

        return payees;
    }

    #endregion

    #region Selection Operations

    /// <summary>
    /// Selects a transaction by index
    /// </summary>
    /// <param name="index">Zero-based index of the transaction row</param>
    public async Task SelectTransactionAsync(int index)
    {
        var row = TransactionRows.Nth(index);
        var checkbox = row.Locator("input[type='checkbox']");

        if (!await checkbox.IsCheckedAsync())
        {
            await checkbox.ClickAsync();
        }
    }

    /// <summary>
    /// Deselects a transaction by index
    /// </summary>
    /// <param name="index">Zero-based index of the transaction row</param>
    public async Task DeselectTransactionAsync(int index)
    {
        var row = TransactionRows.Nth(index);
        var checkbox = row.Locator("input[type='checkbox']");

        if (await checkbox.IsCheckedAsync())
        {
            await checkbox.ClickAsync();
        }
    }

    /// <summary>
    /// Clicks the Select All button
    /// </summary>
    public async Task ClickSelectAllAsync()
    {
        await SelectAllButton.ClickAsync();
        // Give UI time to update optimistically
        await Task.Delay(100);
    }

    /// <summary>
    /// Clicks the Deselect All button
    /// </summary>
    public async Task ClickDeselectAllAsync()
    {
        await DeselectAllButton.ClickAsync();
        // Give UI time to update optimistically
        await Task.Delay(100);
    }

    #endregion

    #region Import Operations

    /// <summary>
    /// Clicks the Import button (opens confirmation modal)
    /// </summary>
    public async Task ClickImportButtonAsync()
    {
        await ImportButton.ClickAsync();
        await ImportConfirmModal.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    /// <summary>
    /// Confirms the import in the modal dialog
    /// </summary>
    public async Task ConfirmImportAsync()
    {
        await WaitForApi(async () =>
        {
            await ImportConfirmButton.ClickAsync();
        }, ImportCompleteApiRegex());

        // Wait for navigation to transactions page
        await Page!.WaitForURLAsync("**/transactions");
    }

    /// <summary>
    /// Cancels the import in the modal dialog
    /// </summary>
    public async Task CancelImportAsync()
    {
        await ImportCancelButton.ClickAsync();
        await ImportConfirmModal.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
    }

    /// <summary>
    /// Checks if the import confirmation modal is visible
    /// </summary>
    public async Task<bool> IsImportConfirmModalVisibleAsync()
    {
        return await ImportConfirmModal.IsVisibleAsync();
    }

    #endregion

    #region Delete Operations

    /// <summary>
    /// Clicks the Delete All button (opens confirmation modal)
    /// </summary>
    public async Task ClickDeleteAllAsync()
    {
        await DeleteAllButton.ClickAsync();
        await DeleteAllModal.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    /// <summary>
    /// Gets the error message text
    /// </summary>
    public async Task<string?> GetErrorMessageAsync()
    {
        var errorElement = Page!.Locator("[data-test-id='permission-denied-error']");
        if (await errorElement.IsVisibleAsync())
        {
            return await errorElement.TextContentAsync();
        }
        return null;
    }

    /// <summary>
    /// Checks if the upload button is visible
    /// </summary>
    public async Task<bool> IsUploadButtonVisibleAsync()
    {
        return await UploadButton.IsVisibleAsync();
    }

    /// <summary>
    /// Checks if the upload button is enabled
    /// </summary>
    public async Task<bool> IsUploadButtonEnabledAsync()
    {
        return !await UploadButton.IsDisabledAsync();
    }

    /// <summary>
    /// Checks if a transaction has a duplicate badge
    /// </summary>
    /// <param name="index">Zero-based index of the transaction row</param>
    public async Task<bool> HasDuplicateBadgeAsync(int index)
    {
        var row = TransactionRows.Nth(index);
        var badge = row.Locator("[data-test-id='duplicate-badge']");
        return await badge.IsVisibleAsync();
    }

    /// <summary>
    /// Gets the duplicate badge type (Exact/Potential)
    /// </summary>
    /// <param name="index">Zero-based index of the transaction row</param>
    public async Task<string?> GetDuplicateBadgeTypeAsync(int index)
    {
        var row = TransactionRows.Nth(index);
        var badge = row.Locator("[data-test-id='duplicate-badge']");
        if (await badge.IsVisibleAsync())
        {
            return await badge.TextContentAsync();
        }
        return null;
    }

    #endregion
}
