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
    private ILocator ReportResult => Page.GetByTestId("report-result");
    private ILocator ReportTable => ReportResult.GetByTestId("report-table");
    private ILocator SelectedReport => ReportSelector.Locator("option:checked");
    private ILocator ReportHeading => ReportResult.GetByRole(AriaRole.Heading);
    private ILocator CategoryLabels => ReportTable.GetByTestId("report-row").GetByRole(AriaRole.Rowheader);
    private ILocator GrandTotal => ReportTable.GetByTestId("report-grand-total");
    private ILocator JanuaryHeader => ReportTable.GetByRole(AriaRole.Columnheader, new() { Name = "January", Exact = true });
    private ILocator DecemberHeader => ReportTable.GetByRole(AriaRole.Columnheader, new() { Name = "December", Exact = true });

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
