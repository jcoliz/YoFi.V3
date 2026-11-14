using System.Text.RegularExpressions;
using Microsoft.Playwright;
using YoFi.V3.Tests.Functional.Components;

namespace YoFi.V3.Tests.Functional.Pages;

/// <summary>
/// Page Object Model for the Transaction Details page
/// </summary>
/// <param name="page">The Playwright page instance</param>
/// <remarks>
/// Represents the transaction details page that displays full information for a single
/// transaction with inline editing capability.
/// </remarks>
public partial class TransactionDetailsPage(IPage page) : BasePage(page)
{
    // GET/PUT/DELETE api/tenant/{tenantKey:guid}/Transactions/{key:guid}
    [GeneratedRegex(@"/api/tenant/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}/Transactions/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}")]
    private static partial Regex TransactionApiRegex();

    /// <summary>
    /// Display text shown for empty optional fields (Category, Memo, Source, ExternalId)
    /// </summary>
    public static string EmptyFieldDisplay => "(none)";

    #region Components

    /// <summary>
    /// Workspace selector component at the top of the page
    /// </summary>
    public WorkspaceSelector WorkspaceSelector => new WorkspaceSelector(Page!.Locator("body"));

    /// <summary>
    /// Error display component for page-level errors
    /// </summary>
    public ErrorDisplay ErrorDisplay => new ErrorDisplay(Page!.Locator("body"));

    #endregion

    #region Page Elements

    /// <summary>
    /// Transactions breadcrumb link
    /// </summary>
    public ILocator BackButton => Page!.Locator("nav[aria-label='breadcrumb'] a");

    /// <summary>
    /// Edit button (display mode)
    /// </summary>
    public ILocator EditButton => Page!.GetByTestId("edit-button");

    /// <summary>
    /// Delete button (display mode)
    /// </summary>
    public ILocator DeleteButton => Page!.GetByTestId("delete-button");

    /// <summary>
    /// Save button (edit mode)
    /// </summary>
    public ILocator SaveButton => Page!.GetByTestId("save-button");

    /// <summary>
    /// Cancel Edit button (edit mode)
    /// </summary>
    public ILocator CancelEditButton => Page!.GetByTestId("cancel-edit-button");

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
    public ILocator LoadingText => Page!.GetByTestId("loading-transaction-text");

    /// <summary>
    /// Loading state container
    /// </summary>
    public ILocator LoadingState => Page!.GetByTestId("loading-state");

    #endregion

    #region Display Mode Elements

    /// <summary>
    /// Transaction details card (display mode)
    /// </summary>
    public ILocator DetailsCard => Page!.GetByTestId("transaction-details-card");

    /// <summary>
    /// Transaction payee heading (display mode)
    /// </summary>
    public ILocator PayeeHeading => Page!.GetByTestId("transaction-payee");

    /// <summary>
    /// Transaction date field (display mode)
    /// </summary>
    public ILocator DateDisplay => Page!.GetByTestId("transaction-date");

    /// <summary>
    /// Transaction amount field (display mode)
    /// </summary>
    public ILocator AmountDisplay => Page!.GetByTestId("transaction-amount");

    /// <summary>
    /// Transaction memo field (display mode)
    /// </summary>
    public ILocator MemoDisplay => Page!.GetByTestId("transaction-memo");

    /// <summary>
    /// Transaction category field (display mode)
    /// </summary>
    public ILocator CategoryDisplay => Page!.GetByTestId("transaction-category");

    /// <summary>
    /// Transaction source field (display mode)
    /// </summary>
    public ILocator SourceDisplay => Page!.GetByTestId("transaction-source");

    /// <summary>
    /// Transaction external ID field (display mode)
    /// </summary>
    public ILocator ExternalIdDisplay => Page!.GetByTestId("transaction-external-id");

    #endregion
    #region Transaction Splits Elements

    /// <summary>
    /// Transaction splits pane container
    /// </summary>
    public ILocator SplitsPane => Page!.GetByTestId("transaction-splits");

    /// <summary>
    /// Add split form within the transaction splits pane
    /// </summary>
    public ILocator AddSplitForm => SplitsPane.GetByTestId("add-split-form");

    /// <summary>
    /// Add button in the transaction splits pane
    /// </summary>
    public ILocator AddSplitButton => SplitsPane.GetByTestId("add-split-button");

    /// <summary>
    /// Category input in the add-split form
    /// </summary>
    public ILocator AddSplitCategoryInput => AddSplitForm.GetByTestId("add-split-category");

    /// <summary>
    /// Amount input in the add-split form
    /// </summary>
    public ILocator AddSplitAmountInput => AddSplitForm.GetByTestId("add-split-amount");

    /// <summary>
    /// Memo input in the add-split form
    /// </summary>
    public ILocator AddSplitMemoInput => AddSplitForm.GetByTestId("add-split-memo");

    /// <summary>
    /// Submit button in the add-split form
    /// </summary>
    public ILocator SaveSplitButton => AddSplitForm.GetByTestId("save-split-button");

    /// <summary>
    /// Displayed split rows, in transaction order
    /// </summary>
    public ILocator SplitRows => SplitsPane.GetByTestId("transaction-splits-table").Locator("tbody tr");

    #endregion

    #region Edit Mode Elements

    /// <summary>
    /// Transaction edit card (edit mode)
    /// </summary>
    public ILocator EditCard => Page!.GetByTestId("transaction-edit-card");

    /// <summary>
    /// Date input (edit mode)
    /// </summary>
    public ILocator EditDateInput => Page!.GetByTestId("edit-date");

    /// <summary>
    /// Amount input (edit mode)
    /// </summary>
    public ILocator EditAmountInput => Page!.GetByTestId("edit-amount");

    /// <summary>
    /// Payee input (edit mode)
    /// </summary>
    public ILocator EditPayeeInput => Page!.GetByTestId("edit-payee");

    /// <summary>
    /// Memo textarea (edit mode)
    /// </summary>
    public ILocator EditMemoInput => Page!.GetByTestId("edit-memo");

    /// <summary>
    /// Source input (edit mode)
    /// </summary>
    public ILocator EditSourceInput => Page!.GetByTestId("edit-source");

    /// <summary>
    /// External ID input (edit mode)
    /// </summary>
    public ILocator EditExternalIdInput => Page!.GetByTestId("edit-external-id");

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
    /// Delete confirm button in modal
    /// </summary>
    public ILocator DeleteConfirmButton => DeleteModal.GetByTestId("delete-confirm-button");

    /// <summary>
    /// Delete cancel button in modal
    /// </summary>
    public ILocator DeleteCancelButton => DeleteModal.GetByTestId("delete-cancel-button");

    #endregion

    #region Navigation

    /// <summary>
    /// Navigates to the transaction details page for a specific transaction
    /// </summary>
    /// <param name="transactionKey">The transaction key (GUID)</param>
    /// <param name="waitForReady">Whether to wait for the page to be ready after navigation</param>
    public async Task NavigateAsync(string transactionKey, bool waitForReady = true)
    {
        await WaitForApi(async () =>
        {
            await Page!.GotoAsync($"/transactions/{transactionKey}");
        }, TransactionApiRegex());

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
    /// Waits for the Edit button to become enabled, indicating client hydration is complete.
    /// This ensures the Vue client has finished hydrating and the page is interactive.
    /// </remarks>
    public async Task WaitForPageReadyAsync(float timeout = 5000)
    {
        await WaitForEditButtonEnabledAsync(timeout);
    }

    /// <summary>
    /// Waits until the Edit button becomes enabled
    /// </summary>
    /// <param name="timeout">Timeout in milliseconds (default: 5000)</param>
    /// <remarks>
    /// Waits for the button to transition from disabled (SSR/hydration) to enabled (client-ready).
    /// This ensures the Vue client has finished hydrating and the page is interactive.
    /// </remarks>
    public Task WaitForEditButtonEnabledAsync(float timeout = 5000)
        => WaitForButtonToBeEnabledAsync(EditButton, timeout);

    /// <summary>
    /// Navigates back to the transactions list
    /// </summary>
    /// <remarks>
    /// Uses client-side routing (NuxtLink), so checks URL change rather than waiting for navigation event.
    /// </remarks>
    public async Task GoBackAsync()
    {
        await BackButton.ClickAsync();
        await Page!.WaitForURLAsync("**/transactions");
    }

    #endregion

    #region Display Mode Operations

    /// <summary>
    /// Starts editing the transaction
    /// </summary>
    /// <remarks>
    /// Transitions from display mode to edit mode.
    /// </remarks>
    public async Task StartEditingAsync()
    {
        await EditButton.ClickAsync();
        await EditCard.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    /// <summary>
    /// Opens the delete confirmation modal
    /// </summary>
    public async Task OpenDeleteModalAsync()
    {
        await DeleteButton.ClickAsync();
        await DeleteModal.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    #endregion

    #region Edit Mode Operations

    /// <summary>
    /// Fills the date field in edit mode
    /// </summary>
    /// <param name="date">Transaction date in YYYY-MM-DD format</param>
    public async Task FillDateAsync(string date)
    {
        await EditDateInput.FillAsync(date);
    }

    /// <summary>
    /// Fills the amount field in edit mode
    /// </summary>
    /// <param name="amount">Transaction amount</param>
    public async Task FillAmountAsync(decimal amount)
    {
        await EditAmountInput.FillAsync(amount.ToString("F2"));
    }

    /// <summary>
    /// Fills the payee field in edit mode
    /// </summary>
    /// <param name="payee">Payee name</param>
    public async Task FillPayeeAsync(string payee)
    {
        await EditPayeeInput.FillAsync(payee);
    }

    /// <summary>
    /// Fills the memo field in edit mode
    /// </summary>
    /// <param name="memo">Memo text</param>
    public async Task FillMemoAsync(string memo)
    {
        await EditMemoInput.FillAsync(memo);
    }

    /// <summary>
    /// Fills the source field in edit mode
    /// </summary>
    /// <param name="source">Source (bank account)</param>
    public async Task FillSourceAsync(string source)
    {
        await EditSourceInput.FillAsync(source);
    }

    /// <summary>
    /// Fills the external ID field in edit mode
    /// </summary>
    /// <param name="externalId">External ID (bank's unique identifier)</param>
    public async Task FillExternalIdAsync(string externalId)
    {
        await EditExternalIdInput.FillAsync(externalId);
    }

    /// <summary>
    /// Saves the transaction changes
    /// </summary>
    /// <remarks>
    /// Submits the edit form and waits for the API call to complete.
    /// </remarks>
    public async Task SaveAsync()
    {
        await WaitForApi(async () =>
        {
            await SaveButton.ClickAsync();
        }, TransactionApiRegex());

        // Wait for edit mode to close and display mode to appear
        await DetailsCard.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    /// <summary>
    /// Cancels editing and returns to display mode
    /// </summary>
    public async Task CancelEditingAsync()
    {
        await CancelEditButton.ClickAsync();
        await DetailsCard.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    /// <summary>
    /// Adds a split using the form and waits for the update request to complete.
    /// </summary>
    public async Task AddSplitAsync(decimal amount, string category, string memo)
    {
        await AddSplitButton.ClickAsync();
        await AddSplitCategoryInput.FillAsync(category);
        await AddSplitAmountInput.FillAsync(amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
        await AddSplitMemoInput.FillAsync(memo);

        await WaitForApi(async () =>
        {
            await SaveSplitButton.ClickAsync();
        }, TransactionApiRegex());
    }

    #endregion

    #region Delete Operations

    /// <summary>
    /// Confirms the deletion of the transaction
    /// </summary>
    /// <remarks>
    /// Submits the delete confirmation and waits for navigation back to the transactions list.
    /// </remarks>
    public async Task ConfirmDeleteAsync()
    {
        await WaitForApi(async () =>
        {
            await DeleteConfirmButton.ClickAsync();
        }, TransactionApiRegex());

        // Wait for navigation back to transactions list
        await Page!.WaitForURLAsync("**/transactions");
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

    #region Query Methods

    /// <summary>
    /// Gets the payee text from the details display
    /// </summary>
    /// <returns>The payee name</returns>
    public async Task<string?> GetPayeeAsync()
    {
        return await PayeeHeading.TextContentAsync();
    }

    /// <summary>
    /// Gets the date text from the details display
    /// </summary>
    /// <returns>The date text as displayed</returns>
    public async Task<string?> GetDateAsync()
    {
        return await DateDisplay.TextContentAsync();
    }

    /// <summary>
    /// Gets the amount text from the details display
    /// </summary>
    /// <returns>The amount text as displayed</returns>
    public async Task<string?> GetAmountAsync()
    {
        return await AmountDisplay.TextContentAsync();
    }

    /// <summary>
    /// Gets the memo text from the details display
    /// </summary>
    /// <returns>The memo text</returns>
    public async Task<string?> GetMemoAsync()
    {
        return await MemoDisplay.TextContentAsync();
    }

    /// <summary>
    /// Gets the category text from the details display
    /// </summary>
    /// <returns>The category text</returns>
    public async Task<string?> GetCategoryAsync()
    {
        return await CategoryDisplay.TextContentAsync();
    }

    /// <summary>
    /// Gets the source text from the details display
    /// </summary>
    /// <returns>The source text</returns>
    public async Task<string?> GetSourceAsync()
    {
        return await SourceDisplay.TextContentAsync();
    }

    /// <summary>
    /// Gets the external ID text from the details display
    /// </summary>
    /// <returns>The external ID text</returns>
    public async Task<string?> GetExternalIdAsync()
    {
        return await ExternalIdDisplay.TextContentAsync();
    }

    /// <summary>
    /// Gets the displayed category, amount, and memo for each split, in transaction order.
    /// </summary>
    public async Task<IReadOnlyList<(string Category, string Amount, string Memo)>> GetDisplayedSplitsAsync()
    {
        var splits = new List<(string Category, string Amount, string Memo)>();
        var count = await SplitRows.CountAsync();
        for (var index = 0; index < count; index++)
        {
            var cells = await SplitRows.Nth(index).Locator("td").AllInnerTextsAsync();
            if (cells.Count != 3)
            {
                throw new InvalidOperationException($"Expected three cells in split row {index}, found {cells.Count}.");
            }

            splits.Add((cells[0].Trim(), cells[1].Trim(), cells[2].Trim()));
        }

        return splits;
    }

    /// <summary>
    /// Waits until exactly the expected number of splits is displayed.
    /// </summary>
    public Task ExpectSplitCountAsync(int expectedCount) =>
        Assertions.Expect(SplitRows).ToHaveCountAsync(expectedCount);

    /// <summary>
    /// Checks if the page is in display mode
    /// </summary>
    /// <returns>True if in display mode, false if in edit mode</returns>
    public async Task<bool> IsDisplayModeAsync()
    {
        return await DetailsCard.IsVisibleAsync();
    }

    /// <summary>
    /// Checks if the page is in edit mode
    /// </summary>
    /// <returns>True if in edit mode, false if in display mode</returns>
    public async Task<bool> IsEditModeAsync()
    {
        return await EditCard.IsVisibleAsync();
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
    /// Checks if a workspace is currently selected
    /// </summary>
    /// <returns>True if no workspace warning is not visible, false otherwise</returns>
    public async Task<bool> HasWorkspaceSelectedAsync()
    {
        return !(await NoWorkspaceWarning.IsVisibleAsync());
    }

    #endregion
}
