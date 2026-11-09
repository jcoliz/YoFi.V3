using System.Net;
using YoFi.V3.Tests.Functional.Infrastructure;
using YoFi.V3.Tests.Functional.Infrastructure.TestControl;
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
        throw new NotImplementedException();
    }

    /// <summary>
    /// When I reload the reports page
    /// </summary>
    [When("I reload the reports page")]
    public async Task IReloadTheReportsPage()
    {
        throw new NotImplementedException();
    }

    #endregion

    #region THEN

    /// <summary>
    /// Then the selected report should be {string1}
    /// </summary>
    [Then("the selected report should be {string1}")]
    public async Task TheSelectedReportShouldBe(string string1)
    {
        throw new NotImplementedException();
    }


    /// <summary>
    /// Then the displayed report should be {Report}
    /// </summary>
    [Then("the displayed report should be {Report}")]
    public async Task TheDisplayedReportShouldBe(string Report)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Then the report should include the category {Category}
    /// </summary>
    [Then("the report should include the category {Category}")]
    public async Task TheReportShouldIncludeTheCategory(string Category)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Then the report grand total should be {Total}
    /// </summary>
    /// <param name="Total">The expected grand total of the report, as a stringified decimal value</param>
    [Then("the report grand total should be {Total}")]
    public async Task TheReportGrandTotalShouldBe(string Total)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Then the report should show {Periods} amounts
    /// </summary>
    [Then("the report should show {Periods} amounts")]
    public async Task TheReportShouldShowAmounts(string Periods)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Then the report should include these categories:
    /// </summary>
    [Then("the report should include these categories:")]
    public async Task TheReportShouldIncludeTheseCategories(DataTable table)
    {
        throw new NotImplementedException();
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
