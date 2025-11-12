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
            return new ForecastRowData(null, null, null, null, cells.Count);
        }

        var dateText = await cells[0].InnerTextAsync();
        DateTime? parsedDate = null;
        
        if (DateTime.TryParse(dateText, out var date))
        {
            parsedDate = date;
        }

        return new ForecastRowData(
            Date: dateText,
            ParsedDate: parsedDate,
            Temperature: await cells[1].InnerTextAsync(),
            Conditions: await cells[2].InnerTextAsync(),
            CellCount: cells.Count
        );
    }

    public async Task<List<DateTime>> GetParsedDatesAsync()
    {
        var rows = await GetAllForecastRowsAsync();
        var dates = new List<DateTime>();

        foreach (var row in rows)
        {
            var data = await GetForecastRowDataAsync(row);
            if (!data.ParsedDate.HasValue)
            {
                throw new InvalidOperationException($"Unable to parse date: '{data.Date}'");
            }
            dates.Add(data.ParsedDate.Value);
        }

        return dates;
    }
}

public record ForecastRowData(string? Date, DateTime? ParsedDate, string? Temperature, string? Conditions, int CellCount);
