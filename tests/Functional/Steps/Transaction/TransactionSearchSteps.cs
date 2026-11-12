using YoFi.V3.Tests.Functional.Infrastructure;
using YoFi.V3.Tests.Functional.Pages;

namespace YoFi.V3.Tests.Functional.Steps.Transaction;

/// <summary>
/// Step definitions for searching the transaction list.
/// </summary>
/// <param name="context">Test context providing access to test infrastructure.</param>
public class TransactionSearchSteps(ITestContext context) : TransactionStepsBase(context)
{
    private readonly TransactionListSteps _listSteps = new(context);

    #region Steps: GIVEN/WHEN

    /// <summary>
    /// Given I have searched for transactions matching {term}
    /// </summary>
    /// <remarks>
    /// Navigates to the transactions page with the current workspace selected,
    /// then applies the search term.
    /// </remarks>
    [When("I search for transactions matching {term}")]
    [Given("I have searched for transactions matching {term}")]
    [Requires(ObjectStoreKeys.CurrentWorkspace)]
    public async Task IHaveSearchedForTransactionsMatching(string term)
    {
        await _listSteps.IAmOnTheTransactionsPage();

        var page = _context.GetOrCreatePage<TransactionsPage>();
        await page.SearchAsync(term);
    }

    /// <summary>
    /// When I clear the search
    /// </summary>
    [When("I clear the search")]
    public async Task IClearTheSearch()
    {
        var page = _context.GetOrCreatePage<TransactionsPage>();
        await page.ClearSearchAsync();
    }

    /// <summary>
    /// When I filter transactions by dates {string1} through {string2}
    /// </summary>
    [When("I filter transactions by dates {string1} through {string2}")]
    [Requires(ObjectStoreKeys.CurrentWorkspace)]
    public async Task IFilterTransactionsByDatesThrough(string string1, string string2)
    {
        await _listSteps.IAmOnTheTransactionsPage();

        var page = _context.GetOrCreatePage<TransactionsPage>();
        await page.SetDateRangeAsync(string1, string2);
    }

    #endregion

    #region Steps: THEN

    /// <summary>
    /// Then the following transactions should be shown:
    /// </summary>
    /// <remarks>
    /// The table must contain a "Payee" column and may include "Category" and
    /// "Split Indicator" columns for additional assertions.
    /// </remarks>
    [Then("the following transactions should be shown:")]
    public async Task TheFollowingTransactionsShouldBeShown(DataTable table)
    {
        var page = _context.GetOrCreatePage<TransactionsPage>();

        await page.ExpectTransactionCountAsync(table.Rows.Count);

        foreach (var row in table.Rows)
        {
            var expectedPayee = row.GetRequired("Payee");
            await page.ExpectTransactionForAsync(expectedPayee);

            var expectedCategory = row.GetOptional("Category");
            if (expectedCategory is not null)
            {
                var actualCategory = await page.GetTransactionCategoryAsync(expectedPayee);
                Assert.That(actualCategory?.Trim(), Is.EqualTo(expectedCategory.Trim()),
                    $"Expected category for '{expectedPayee}' to be '{expectedCategory}' but was '{actualCategory}'");
            }

            var expectedSplitIndicator = row.GetOptional("Split Indicator");
            if (expectedSplitIndicator is not null)
            {
                var actualSplitIndicator = await page.GetTransactionSplitIndicatorAsync(expectedPayee);
                Assert.That(actualSplitIndicator, Is.EqualTo(expectedSplitIndicator.Trim()),
                    $"Expected split indicator for '{expectedPayee}' to be '{expectedSplitIndicator}' but was '{actualSplitIndicator}'");
            }
        }
    }

    /// <summary>
    /// Then I should see that no transactions match my search
    /// </summary>
    [Then("I should see that no transactions match my search")]
    public async Task IShouldSeeThatNoTransactionsMatchMySearch()
    {
        var page = _context.GetOrCreatePage<TransactionsPage>();
        await page.ExpectNoResultsAsync();
    }

    /// <summary>
    /// Then the search box should contain {term}
    /// </summary>
    [Then("the search box should contain {term}")]
    public async Task TheSearchBoxShouldContain(string term)
    {
        var page = _context.GetOrCreatePage<TransactionsPage>();
        await page.ExpectSearchTermAsync(term);
    }

    #endregion
}
