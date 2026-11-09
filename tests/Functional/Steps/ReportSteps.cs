using System.Globalization;
using YoFi.V3.Tests.Functional.Infrastructure;
using YoFi.V3.Tests.Functional.Pages;

namespace YoFi.V3.Tests.Functional.Steps;

/// <summary>
/// Step definitions for reports.
/// </summary>
/// <param name="_context">The test context providing access to test infrastructure.</param>
/// <remarks>
/// </remarks>
public class ReportSteps(ITestContext _context)
{
    #region WHEN

    /// <summary>
    /// When I select the {Report} report
    /// </summary>
    [When("I select the {Report} report")]
    public async Task ISelectTheReport(string Report)
    {
        var page = _context.GetOrCreatePage<ReportsPage>();
        await page.SelectReportAsync(Report);
    }

    /// <summary>
    /// When I reload the reports page
    /// </summary>
    [When("I reload the reports page")]
    public async Task IReloadTheReportsPage()
    {
        var page = _context.GetOrCreatePage<ReportsPage>();
        await page.ReloadAsync();
    }

    #endregion

    #region THEN

    /// <summary>
    /// Then the selected report should be {string1}
    /// </summary>
    [Then("the selected report should be {string1}")]
    public async Task TheSelectedReportShouldBe(string string1)
    {
        var page = _context.GetOrCreatePage<ReportsPage>();
        await page.ExpectSelectedReportAsync(string1);
    }


    /// <summary>
    /// Then the displayed report should be {Report}
    /// </summary>
    [Then("the displayed report should be {Report}")]
    public async Task TheDisplayedReportShouldBe(string Report)
    {
        var page = _context.GetOrCreatePage<ReportsPage>();
        await page.ExpectDisplayedReportAsync(Report);
    }

    /// <summary>
    /// Then the report should include the category {Category}
    /// </summary>
    [Then("the report should include the category {Category}")]
    public async Task TheReportShouldIncludeTheCategory(string Category)
    {
        var page = _context.GetOrCreatePage<ReportsPage>();
        await page.ExpectCategoryAsync(Category);
    }

    /// <summary>
    /// Then the report grand total should be {Total}
    /// </summary>
    /// <param name="Total">The expected grand total of the report, as a stringified decimal value</param>
    [Then("the report grand total should be {Total}")]
    public async Task TheReportGrandTotalShouldBe(string Total)
    {
        var page = _context.GetOrCreatePage<ReportsPage>();
        var actual = await page.GetGrandTotalAsync();
        Assert.That(actual, Is.EqualTo(decimal.Parse(Total, CultureInfo.InvariantCulture)),
            "The report grand total should match the seeded transactions.");
    }

    /// <summary>
    /// Then the report should show {Periods} amounts
    /// </summary>
    [Then("the report should show {Periods} amounts")]
    public async Task TheReportShouldShowAmounts(string Periods)
    {
        Assert.That(Periods, Is.AnyOf("annual", "monthly"), "Unsupported report period.");
        var page = _context.GetOrCreatePage<ReportsPage>();
        await page.ExpectMonthlyAmountsAsync(Periods == "monthly");
    }

    /// <summary>
    /// Then the report should include these categories:
    /// </summary>
    [Then("the report should include these categories:")]
    public async Task TheReportShouldIncludeTheseCategories(DataTable table)
    {
        var page = _context.GetOrCreatePage<ReportsPage>();
        foreach (var row in table.Rows)
        {
            await page.ExpectCategoryAsync(row.GetRequired("Category"));
        }
    }

    /// <summary>
    /// Then the report should show monthly amounts
    /// </summary>
    [Then("the report should show monthly amounts")]
    public async Task TheReportShouldShowMonthlyAmounts()
    {
        throw new NotImplementedException();
    }
    #endregion
}
