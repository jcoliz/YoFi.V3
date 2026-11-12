using YoFi.V3.Tests.Functional.Infrastructure;
using YoFi.V3.Tests.Functional.Pages;

namespace YoFi.V3.Tests.Functional.Steps.Transaction;

/// <summary>
/// Step definitions for transaction list viewing and navigation operations.
/// </summary>
/// <param name="context">Test context providing access to test infrastructure.</param>
public class TransactionFilterSteps(ITestContext context) : TransactionStepsBase(context)
{
    private readonly TransactionListSteps _listSteps = new(context);

    #region Steps: GIVEN and WHEN

    /// <summary>
    /// Given the transaction filter panel is expanded
    /// </summary>
    [Given("the transaction filter panel is expanded")]
    public async Task TheTransactionFilterPanelIsExpanded()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// When I filter transactions by:
    /// </summary>
    [When("I filter transactions by:")]
    [Given("my transaction filters are applied:")]
    public async Task IFilterTransactionsBy(DataTable table)
    {
        var page = _context.GetOrCreatePage<TransactionsPage>();
        await EnsureTransactionsPageAsync(page);

        foreach (var row in table.Rows)
        {
            var filter = row.GetRequired("Filter");
            var value = row.GetRequired("Value");

            switch (filter)
            {
                case "Payee":
                    await page.SetPayeeFilterAsync(value);
                    break;
                case "Category":
                    await page.SetCategoryFilterAsync(value);
                    break;
                case "Memo":
                    await page.SetMemoFilterAsync(value);
                    break;
                case "Amount":
                    await page.SetAmountFilterAsync(value);
                    break;
                case "From Date":
                    await page.SetFromDateAsync(value);
                    break;
                case "To Date":
                    await page.SetToDateAsync(value);
                    break;
                default:
                    throw new ArgumentException($"Unsupported transaction filter '{filter}'.", nameof(table));
            }
        }
    }

    /// <summary>
    /// When I filter transactions to show uncategorized only
    /// </summary>
    [When("I filter transactions to show uncategorized only")]
    [Requires(ObjectStoreKeys.CurrentWorkspace)]
    public async Task IFilterTransactionsToShowUncategorizedOnly()
    {
        await _listSteps.IAmOnTheTransactionsPage();

        var page = _context.GetOrCreatePage<TransactionsPage>();
        await page.SetUncategorizedOnlyAsync();
    }

    /// <summary>
    /// When I choose the {string1} date filter preset
    /// </summary>
    [When("I choose the {string1} date filter preset")]
    public async Task IChooseTheDateFilterPreset(string string1)
    {
        var page = _context.GetOrCreatePage<TransactionsPage>();
        await EnsureTransactionsPageAsync(page);
        await page.SetDatePresetAsync(string1);
    }

    /// <summary>
    /// When I click Clear All in the transaction filters
    /// </summary>
    [When("I clear the transaction filters")]
    public async Task IClickClearAllInTheTransactionFilters()
    {
        throw new NotImplementedException();
    }

    #endregion

    #region Steps: THEN

    /// <summary>
    /// Then the transaction filter panel should remain open
    /// </summary>
    [Then("the transaction filter panel should remain open")]
    public async Task TheTransactionFilterPanelShouldRemainOpen()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Then the search and advanced filter fields should be empty
    /// </summary>
    [Then("the search and advanced filter fields should be empty")]
    public async Task TheSearchAndAdvancedFilterFieldsShouldBeEmpty()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Then the search and advanced filter fields should retain their applied values
    /// </summary>
    [Then("the search and advanced filter fields should retain their applied values")]
    public async Task TheSearchAndAdvancedFilterFieldsShouldRetainTheirAppliedValues()
    {
        throw new NotImplementedException();
    }

    #endregion

    private async Task EnsureTransactionsPageAsync(TransactionsPage page)
    {
        if (!page.IsOnTransactionsPage)
        {
            await _listSteps.IAmOnTheTransactionsPage();
        }
    }
}
