using Microsoft.Playwright;

namespace YoFi.V3.Tests.Functional.Pages;

public class WeatherPage(IPage? _page): BasePage(_page)
{
    public ILocator ForecastTableBody => Page!.GetByTestId("forecast-table-body");
    public ILocator ForecastRows => ForecastTableBody.Locator("tr");

    public async Task<int> GetForecastCountAsync()
    {
        return await ForecastRows.CountAsync();
    }

    public async Task<IReadOnlyList<ILocator>> GetAllForecastRowsAsync()
    {
        return await ForecastRows.AllAsync();
    }

    public async Task<ForecastRowData> GetForecastRowDataAsync(ILocator row)
    {
        var cells = await row.Locator("td").AllAsync();

        if (cells.Count < 3)
        {
            return new ForecastRowData(null, null, null, cells.Count);
        }

        return new ForecastRowData(
            Date: await cells[0].InnerTextAsync(),
            Temperature: await cells[1].InnerTextAsync(),
            Conditions: await cells[2].InnerTextAsync(),
            CellCount: cells.Count
        );
    }
}

public record ForecastRowData(string? Date, string? Temperature, string? Conditions, int CellCount);
