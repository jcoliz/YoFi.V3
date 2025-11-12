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

            Assert.That(data.ParsedCelsius, Is.Not.Null,
                $"Temperature should include Celsius: {data.Temperature}");
            Assert.That(data.ParsedFahrenheit, Is.Not.Null,
                $"Temperature should include Fahrenheit: {data.Temperature}");
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

            if (data.ParsedCelsius.HasValue && data.ParsedFahrenheit.HasValue)
            {
                var celsius = data.ParsedCelsius.Value;
                var fahrenheit = data.ParsedFahrenheit.Value;

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
        var dates = await weatherPage.GetParsedDatesAsync();

        Assert.That(dates.Count, Is.GreaterThan(1), "Need at least 2 forecasts to verify chronological order");

        for (int i = 1; i < dates.Count; i++)
        {
            Assert.That(dates[i], Is.GreaterThan(dates[i - 1]),
                $"Forecasts should be in chronological order. Found {dates[i]} after {dates[i - 1]}");
        }
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
