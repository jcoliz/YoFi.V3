using Microsoft.Playwright;
using YoFi.V3.Tests.Functional.Infrastructure;

namespace YoFi.V3.Tests.Functional.Pages;

[TestFixture]
public class TransactionFilterAssertionsTests : FunctionalTestBase
{
    [TestCase("search-input")]
    [TestCase("category-filter")]
    [TestCase("memo-filter")]
    [TestCase("amount-filter")]
    [TestCase("fromDate")]
    [TestCase("toDate")]
    [TestCase("uncategorized-filter")]
    public async Task UnspecifiedNonemptyFilterFailsAssertion(string unexpectedFilter)
    {
        await Page.SetContentAsync("""
            <div data-test-id="search-filter-bar">
              <input data-test-id="search-input">
            </div>
            <div data-test-id="date-range-filters">
              <input data-test-id="payee-filter" value="Cafe">
              <input data-test-id="category-filter">
              <input data-test-id="memo-filter">
              <input data-test-id="amount-filter">
              <input id="fromDate" type="date">
              <input id="toDate" type="date">
              <input data-test-id="uncategorized-filter" type="checkbox">
            </div>
            """);

        var input = unexpectedFilter switch
        {
            "fromDate" or "toDate" => Page.Locator($"#{unexpectedFilter}"),
            _ => Page.GetByTestId(unexpectedFilter)
        };
        if (unexpectedFilter == "uncategorized-filter")
        {
            await input.CheckAsync();
        }
        else
        {
            await input.FillAsync(unexpectedFilter is "fromDate" or "toDate" ? "2026-04-01" : "unexpected");
        }

        var page = new TransactionsPage(Page);
        var expectedFilters = new Dictionary<string, string> { ["Payee"] = "Cafe" };

        var exception = await Assert.ThrowsAsync<PlaywrightException>(
            () => page.ExpectSearchAndFiltersAsync(expectedFilters));
        Assert.That(exception!.Message, Does.Contain(unexpectedFilter));
    }
}
