using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace YoFi.V3.Tests.Functional.Pages;

/// <summary>
/// Page model for selecting reports and checking their displayed content.
/// </summary>
public class ReportsPage(IPage page) : BasePage(page)
{
    private ILocator ReportSelector => Page.GetByTestId("report-selector");
    private ILocator YearSelector => Page.GetByTestId("report-year-selector");
    private ILocator ReportResult => Page.GetByTestId("report-result");
    private ILocator ReportTable => ReportResult.GetByTestId("report-table");
    private ILocator SelectedReport => ReportSelector.Locator("option:checked");
    private ILocator ReportHeading => ReportResult.GetByRole(AriaRole.Heading);
    private ILocator CategoryLabels => ReportTable.GetByTestId("report-row").GetByRole(AriaRole.Rowheader);
    private ILocator GrandTotal => ReportTable.GetByTestId("report-grand-total");
    private ILocator JanuaryHeader => ReportTable.GetByRole(AriaRole.Columnheader, new() { Name = "January", Exact = true });
    private ILocator DecemberHeader => ReportTable.GetByRole(AriaRole.Columnheader, new() { Name = "December", Exact = true });

    private ILocator ReportDisplayControls => Page.GetByTestId("report-display-controls");
    private ILocator ReportPeriodSelector => ReportDisplayControls.GetByTestId("report-period-selector");
    private ILocator ReportDepthSelector => ReportDisplayControls.GetByTestId("report-depth-selector");

    /// <summary>
    /// Reloads the page and waits for the restored report to finish loading.
    /// </summary>
    public async Task ReloadAsync()
    {
        await Page.ReloadAsync();
        await ReportTable.WaitForAsync(new() { State = WaitForSelectorState.Visible });
    }

    /// <summary>
    /// Selects a report by its displayed name and waits for its result.
    /// </summary>
    public async Task SelectReportAsync(string name)
    {
        await ReportTable.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await ReportSelector.SelectOptionAsync(new SelectOptionValue { Label = name });
        await ExpectDisplayedReportAsync(name);
    }

    /// <summary>
    /// Selects a calendar year and waits for the matching report result.
    /// </summary>
    public async Task SelectYearAsync(int year)
    {
        await ReportTable.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await YearSelector.SelectOptionAsync(year.ToString(CultureInfo.InvariantCulture));
        await ExpectYearAsync(year);
    }

    /// <summary>
    /// Selects the report display period (monthly or annual) and waits for the report to update.
    /// </summary>
    /// <param name="monthly">True for monthly report display, false for annual-only display</param>
    /// <returns></returns>
    public async Task SelectReportPeriod(bool monthly)
    {
        await ReportPeriodSelector.SelectOptionAsync(monthly.ToString().ToLowerInvariant());
    }

    /// <summary>
    /// Selects the report depth and waits for the report to update.
    /// </summary>
    /// <param name="depth">The desired report depth, from 1-4</param>
    /// <returns></returns>
    public async Task SelectReportDepthAsync(int depth)
    {
        await ReportDepthSelector.SelectOptionAsync(depth.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Verifies the selected year and the loaded report's year.
    /// </summary>
    public async Task ExpectYearAsync(int year)
    {
        await Assertions.Expect(YearSelector).ToHaveValueAsync(year.ToString(CultureInfo.InvariantCulture));
        await Assertions.Expect(ReportHeading).ToHaveTextAsync(new Regex($@"\u2014\s+{year}$"));
        await Assertions.Expect(ReportTable).ToBeVisibleAsync();
    }

    /// <summary>
    /// Verifies the selected option after the initial report has loaded.
    /// </summary>
    public async Task ExpectSelectedReportAsync(string name)
    {
        await ReportTable.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await Assertions.Expect(SelectedReport).ToHaveTextAsync(name);
    }

    /// <summary>
    /// Waits for the requested report heading and its loaded table.
    /// </summary>
    public async Task ExpectDisplayedReportAsync(string name)
    {
        await Assertions.Expect(ReportHeading)
            .ToHaveTextAsync(new Regex($@"^{Regex.Escape(name)}\s+\u2014\s+\d{{4}}$"));
        await Assertions.Expect(ReportTable).ToBeVisibleAsync();
    }

    /// <summary>
    /// Verifies an exact category label among the displayed report rows.
    /// </summary>
    public Task ExpectCategoryAsync(string category) =>
        Assertions.Expect(CategoryLabels.Filter(new()
            { HasTextRegex = new Regex($@"^\s*{Regex.Escape(category)}\s*$") }).First)
            .ToBeVisibleAsync();

    /// <summary>
    /// Verifies an exact category label among the displayed report rows.
    /// </summary>
    public Task ExpectNotCategoryAsync(string category) =>
        Assertions.Expect(CategoryLabels.Filter(new()
            { HasTextRegex = new Regex($@"^\s*{Regex.Escape(category)}\s*$") }).First)
            .ToHaveCountAsync(0);

    /// <summary>
    /// Reads the explicit annual grand total as a number, independent of currency decoration.
    /// </summary>
    public async Task<decimal> GetGrandTotalAsync()
    {
        var text = await GrandTotal.InnerTextAsync();
        return decimal.Parse(text, NumberStyles.Currency, CultureInfo.GetCultureInfo("en-US"));
    }

    /// <summary>
    /// Verifies whether the loaded report includes monthly amount columns.
    /// </summary>
    public async Task ExpectMonthlyAmountsAsync(bool visible)
    {
        foreach (var header in new[] { JanuaryHeader, DecemberHeader })
        {
            if (visible)
            {
                await Assertions.Expect(header).ToBeVisibleAsync();
            }
            else
            {
                await Assertions.Expect(header).ToHaveCountAsync(0);
            }
        }
    }

}
