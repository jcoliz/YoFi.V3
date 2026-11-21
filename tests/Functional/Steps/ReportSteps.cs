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
    [Provides(ObjectStoreKeys.SelectedReport)]
    public async Task ISelectTheReport(string Report)
    {
        var page = _context.GetOrCreatePage<ReportsPage>();
        await page.SelectReportAsync(Report);
        _context.ObjectStore.Add(ObjectStoreKeys.SelectedReport, Report);
    }

    /// <summary>
    /// When I select report year {Year}
    /// </summary>
    [When("I select report year {Year}")]
    [Provides(ObjectStoreKeys.SelectedReportYear)]
    public async Task ISelectReportYear(int Year)
    {
        var page = _context.GetOrCreatePage<ReportsPage>();
        await page.SelectYearAsync(Year);
        _context.ObjectStore.Add(ObjectStoreKeys.SelectedReportYear, Year.ToString(CultureInfo.InvariantCulture));
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

    /// <summary>
    /// When I select annual-only report display
    /// </summary>
    [When("I select annual-only report display")]
    [Provides(ObjectStoreKeys.SelectedReportGranularity)]
    public async Task ISelectAnnualOnlyReportDisplay()
    {
        var page = _context.GetOrCreatePage<ReportsPage>();
        await page.SelectReportPeriod(false);
        _context.ObjectStore.Add(ObjectStoreKeys.SelectedReportGranularity, "annual");
    }

    /// <summary>
    /// When I select monthly report display
    /// </summary>
    [When("I select monthly report display")]
    [Provides(ObjectStoreKeys.SelectedReportGranularity)]
    public async Task ISelectMonthlyReportDisplay()
    {
        var page = _context.GetOrCreatePage<ReportsPage>();
        await page.SelectReportPeriod(true);
        _context.ObjectStore.Add(ObjectStoreKeys.SelectedReportGranularity, "monthly");
    }

    /// <summary>
    /// When I select report depth {value1}
    /// </summary>
    [When("I select report depth {value1}")]
    [Provides(ObjectStoreKeys.SelectedReportDepth)]
    public async Task ISelectReportDepth(int value1)
    {
        var page = _context.GetOrCreatePage<ReportsPage>();
        await page.SelectReportDepthAsync(value1);
        _context.ObjectStore.Add(ObjectStoreKeys.SelectedReportDepth, value1.ToString(CultureInfo.InvariantCulture));
    }

    #endregion

    #region THEN

    /// <summary>
    /// Then the report year should be {Year}
    /// </summary>
    [Then("the report year should be {Year}")]
    public async Task TheReportYearShouldBe(int Year)
    {
        var page = _context.GetOrCreatePage<ReportsPage>();
        await page.ExpectYearAsync(Year);
    }

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
    [Requires(ObjectStoreKeys.SelectedReport)]
    public async Task TheReportShouldIncludeTheCategory(string Category)
    {
        var page = _context.GetOrCreatePage<ReportsPage>();

        // Make sure the report is fully loaded first
        var selectedReport = _context.ObjectStore.Get<string>(ObjectStoreKeys.SelectedReport);
        await TheDisplayedReportShouldBe(selectedReport);

        await page.ExpectCategoryAsync(Category);
    }

    /// <summary>
    /// Then the report should not include the category {Category}
    /// </summary>
    [Then("the report should not include the category {Category}")]
    [Requires(ObjectStoreKeys.SelectedReport)]
    public async Task TheReportShouldNotIncludeTheCategory(string Category)
    {
        var page = _context.GetOrCreatePage<ReportsPage>();

        // Make sure the report is fully loaded first
        var selectedReport = _context.ObjectStore.Get<string>(ObjectStoreKeys.SelectedReport);
        await TheDisplayedReportShouldBe(selectedReport);

        await page.ExpectNotCategoryAsync(Category);
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
    public Task TheReportShouldShowMonthlyAmounts() =>
        TheReportShouldShowAmounts("monthly");
    #endregion

    /// <summary>
    /// Then the report configuration should match what I set earlier
    /// </summary>
    [Then("the report configuration should match what I set earlier")]
    [Requires(ObjectStoreKeys.SelectedReportDepth)]
    [Requires(ObjectStoreKeys.SelectedReportGranularity)]
    [Requires(ObjectStoreKeys.SelectedReportYear)]
    public async Task TheReportConfigurationShouldMatchWhatISetEarlier()
    {
        var page = _context.GetOrCreatePage<ReportsPage>();

        var selectedReportDepth = int.Parse(_context.ObjectStore.Get<string>(ObjectStoreKeys.SelectedReportDepth), CultureInfo.InvariantCulture);
        await page.ExpectDepthAsync(selectedReportDepth);

        var selectedReportGranularity = _context.ObjectStore.Get<string>(ObjectStoreKeys.SelectedReportGranularity);
        Assert.That(selectedReportGranularity, Is.AnyOf("annual", "monthly"), "Unsupported report granularity.");
        await page.ExpectMonthlyAmountsAsync(selectedReportGranularity == "monthly");

        var selectedReportYear = int.Parse(_context.ObjectStore.Get<string>(ObjectStoreKeys.SelectedReportYear), CultureInfo.InvariantCulture);
        await page.ExpectYearAsync(selectedReportYear);
    }
}
