using Microsoft.Playwright;
using System.Text.RegularExpressions;

namespace YoFi.V3.Tests.Functional.Pages;

public partial class WeatherPage(IPage? _page): BasePage(_page)
{
    [GeneratedRegex("/api/Weather")]
    private static partial Regex WeatherApiRegex();

    public ILocator ForecastTableBody => Page!.GetByTestId("forecast-table-body");
    public ILocator ForecastRows => ForecastTableBody.Locator("tr");

    #region Navigation

    /// <summary>
    /// Navigates to the weather page via address bar
    /// </summary>
    public async Task NavigateAsync()
    {
        await WaitForApi(async () =>
        {
            await Page!.GotoAsync("/weather");
        }, WeatherApiRegex());
    }

    #endregion


    public async Task<int> GetForecastCountAsync()
    {
        return await ForecastRows.CountAsync();
    }

    /// <summary>
    /// Waits for at least the specified number of forecast rows to be visible
    /// </summary>
    /// <param name="minCount">Minimum number of rows to wait for</param>
    /// <param name="timeout">Timeout in milliseconds (default: 5000)</param>
    public async Task WaitForForecastRowsAsync(int minCount = 5, float timeout = 5000)
    {
        await Page!.WaitForFunctionAsync(
            $"() => document.querySelectorAll('[data-test-id=\"forecast-table-body\"] tr').length >= {minCount}",
            new PageWaitForFunctionOptions { Timeout = timeout }
        );
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
            return new ForecastRowData(null, null, null, null, null, null, cells.Count);
        }

        var dateText = await cells[0].InnerTextAsync();
        DateTime? parsedDate = null;

        if (DateTime.TryParse(dateText, out var date))
        {
            parsedDate = date;
        }

        var temperatureText = await cells[1].InnerTextAsync();
        var (parsedCelsius, parsedFahrenheit) = ParseTemperature(temperatureText);

        return new ForecastRowData(
            Date: dateText,
            ParsedDate: parsedDate,
            Temperature: temperatureText,
            ParsedCelsius: parsedCelsius,
            ParsedFahrenheit: parsedFahrenheit,
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

    private static (double? celsius, double? fahrenheit) ParseTemperature(string temperatureText)
    {
        var celsiusMatch = Regex.Match(temperatureText ?? "", @"(-?\d+(?:\.\d+)?)\s*°?C");
        var fahrenheitMatch = Regex.Match(temperatureText ?? "", @"(-?\d+(?:\.\d+)?)\s*°?F");

        double? celsius = celsiusMatch.Success ? double.Parse(celsiusMatch.Groups[1].Value) : null;
        double? fahrenheit = fahrenheitMatch.Success ? double.Parse(fahrenheitMatch.Groups[1].Value) : null;

        return (celsius, fahrenheit);
    }
}

public record ForecastRowData(
    string? Date,
    DateTime? ParsedDate,
    string? Temperature,
    double? ParsedCelsius,
    double? ParsedFahrenheit,
    string? Conditions,
    int CellCount);
