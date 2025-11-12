using System.Text.RegularExpressions;
using Microsoft.Playwright;
using YoFi.V3.Tests.Functional.Pages;

namespace YoFi.V3.Tests.Functional.Steps;

/// <summary>
/// Step definitions for Weather feature tests
/// </summary>
public abstract class WeatherSteps : FunctionalTest
{
    #region Steps: GIVEN

    /// <summary>
    /// Given: I am on the home page
    /// </summary>
    protected async Task GivenIAmOnTheHomePage()
    {
        await GivenLaunchedSite();
    }

    /// <summary>
    /// Given: I am viewing weather forecasts
    /// </summary>
    protected async Task GivenIAmViewingWeatherForecasts()
    {
        await GivenIAmOnTheHomePage();
        await WhenINavigateToViewTheWeatherForecast();
    }

    #endregion

    #region Steps: WHEN

    /// <summary>
    /// When: I navigate to view the weather forecast
    /// </summary>
    protected async Task WhenINavigateToViewTheWeatherForecast()
    {
        await VisitPage("Weather");
    }

    #endregion

    #region Steps: THEN

    /// <summary>
    /// Then: I should see upcoming weather predictions
    /// </summary>
    protected async Task ThenIShouldSeeUpcomingWeatherPredictions()
    {
        var weatherPage = GetOrCreateWeatherPage();
        var actualCount = await weatherPage.GetForecastCountAsync();
        Assert.That(actualCount, Is.GreaterThan(0), "Expected to see at least one weather forecast");
    }

    /// <summary>
    /// Then: each forecast should show the date, temperature, and conditions
    /// </summary>
    protected async Task ThenEachForecastShouldShowTheDateTemperatureAndConditions()
    {
        var weatherPage = GetOrCreateWeatherPage();
        var rows = await weatherPage.GetAllForecastRowsAsync();

        Assert.That(rows.Count, Is.GreaterThan(0), "Expected at least one forecast row");

        foreach (var row in rows)
        {
            var data = await weatherPage.GetForecastRowDataAsync(row);

            Assert.That(data.CellCount, Is.GreaterThanOrEqualTo(3),
                "Each forecast row should have at least 3 cells (date, temperature, conditions)");

            Assert.That(data.Date, Is.Not.Empty, "Date cell should not be empty");
            Assert.That(data.Temperature, Is.Not.Empty, "Temperature cell should not be empty");
            Assert.That(data.Conditions, Is.Not.Empty, "Conditions cell should not be empty");
        }
    }

    /// <summary>
    /// Then: each forecast should display temperature in both Celsius and Fahrenheit
    /// </summary>
    protected async Task ThenEachForecastShouldDisplayTemperatureInBothCelsiusAndFahrenheit()
    {
        var weatherPage = GetOrCreateWeatherPage();
        var rows = await weatherPage.GetAllForecastRowsAsync();

        Assert.That(rows.Count, Is.GreaterThan(0), "Expected at least one forecast row");

        foreach (var row in rows)
        {
            var data = await weatherPage.GetForecastRowDataAsync(row);

            // Temperature should contain both C and F (e.g., "20°C / 68°F" or similar format)
            Assert.That(data.Temperature, Does.Contain("C").Or.Contains("°C"),
                "Temperature should include Celsius");
            Assert.That(data.Temperature, Does.Contain("F").Or.Contains("°F"),
                "Temperature should include Fahrenheit");
        }
    }

    /// <summary>
    /// Then: the temperature conversions should be accurate
    /// </summary>
    protected async Task ThenTheTemperatureConversionsShouldBeAccurate()
    {
        var weatherPage = GetOrCreateWeatherPage();
        var rows = await weatherPage.GetAllForecastRowsAsync();

        Assert.That(rows.Count, Is.GreaterThan(0), "Expected at least one forecast row");

        foreach (var row in rows)
        {
            var data = await weatherPage.GetForecastRowDataAsync(row);

            // Extract Celsius and Fahrenheit values using regex
            var celsiusMatch = Regex.Match(data.Temperature ?? "", @"(-?\d+(?:\.\d+)?)\s*°?C");
            var fahrenheitMatch = Regex.Match(data.Temperature ?? "", @"(-?\d+(?:\.\d+)?)\s*°?F");

            if (celsiusMatch.Success && fahrenheitMatch.Success)
            {
                var celsius = double.Parse(celsiusMatch.Groups[1].Value);
                var fahrenheit = double.Parse(fahrenheitMatch.Groups[1].Value);

                // Formula: F = C * 9/5 + 32
                var expectedFahrenheit = celsius * 9.0 / 5.0 + 32.0;

                // Allow for rounding differences (within 1 degree)
                Assert.That(Math.Abs(fahrenheit - expectedFahrenheit), Is.LessThanOrEqualTo(1.0),
                    $"Temperature conversion should be accurate: {celsius}°C should be approximately {expectedFahrenheit}°F, but got {fahrenheit}°F");
            }
        }
    }

    /// <summary>
    /// Then: I should see forecasts for at least the next 5 days
    /// </summary>
    protected async Task ThenIShouldSeeForecastsForAtLeastTheNext5Days()
    {
        var weatherPage = GetOrCreateWeatherPage();
        var actualCount = await weatherPage.GetForecastCountAsync();
        Assert.That(actualCount, Is.GreaterThanOrEqualTo(5),
            $"Expected at least 5 days of forecasts, but found {actualCount}");
    }

    /// <summary>
    /// Then: forecasts should be ordered chronologically
    /// </summary>
    protected async Task ThenForecastsShouldBeOrderedChronologically()
    {
        var weatherPage = GetOrCreateWeatherPage();
        var rows = await weatherPage.GetAllForecastRowsAsync();

        Assert.That(rows.Count, Is.GreaterThan(1), "Need at least 2 forecasts to verify chronological order");

        DateTime? previousDate = null;

        foreach (var row in rows)
        {
            var data = await weatherPage.GetForecastRowDataAsync(row);

            // Try to parse the date (adjust format as needed based on actual format)
            if (DateTime.TryParse(data.Date, out var currentDate))
            {
                if (previousDate.HasValue)
                {
                    Assert.That(currentDate, Is.GreaterThan(previousDate.Value),
                        $"Forecasts should be in chronological order. Found {currentDate} after {previousDate}");
                }
                previousDate = currentDate;
            }
        }

        Assert.That(previousDate, Is.Not.Null, "At least one date should be parseable");
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Get or create WeatherPage and store it in the object store
    /// </summary>
    private WeatherPage GetOrCreateWeatherPage()
    {
        if (!_objectStore.Contains<WeatherPage>())
        {
            var weatherPage = new WeatherPage(Page);
            _objectStore.Add(weatherPage);
        }
        return It<WeatherPage>();
    }

    #endregion
}
