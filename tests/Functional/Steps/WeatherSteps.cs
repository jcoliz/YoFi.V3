using Gherkin.Generator.Utils;
using YoFi.V3.Tests.Functional.Infrastructure;
using YoFi.V3.Tests.Functional.Pages;

namespace YoFi.V3.Tests.Functional.Steps;

/// <summary>
/// Step definitions for Weather feature tests.
/// </summary>
/// <param name="_context">The test context providing access to test infrastructure.</param>
/// <remarks>
/// Handles weather forecast page operations including navigation, forecast display verification,
/// temperature unit conversions, and chronological ordering.
/// </remarks>
public class WeatherSteps(ITestContext _context)
{
    #region Steps: WHEN

    /// <summary>
    /// Navigates to the weather forecast page.
    /// </summary>
    /// <remarks>
    /// Creates or retrieves the WeatherPage instance from the object store
    /// and navigates to the weather forecast view.
    /// </remarks>
    [When("I navigate to view the weather forecast")]
    [Given("I am viewing weather forecasts")]
    public async Task WhenINavigateToViewTheWeatherForecast()
    {
        var weatherPage = _context.GetOrCreatePage<WeatherPage>();
        await weatherPage.NavigateAsync();
    }

    #endregion

    #region Steps: THEN

    /// <summary>
    /// Verifies that the weather page displays at least one weather forecast.
    /// </summary>
    /// <remarks>
    /// Retrieves the forecast count from the WeatherPage and asserts
    /// that at least one forecast is visible to the user.
    /// </remarks>
    [Then("I should see upcoming weather predictions")]
    public async Task ThenIShouldSeeUpcomingWeatherPredictions()
    {
        var weatherPage = _context.GetOrCreatePage<WeatherPage>();
        var actualCount = await weatherPage.GetForecastCountAsync();
        Assert.That(actualCount, Is.GreaterThan(0), "Expected to see at least one weather forecast");
    }

    /// <summary>
    /// Verifies that each forecast row displays date, temperature, and weather conditions.
    /// </summary>
    /// <remarks>
    /// Iterates through all forecast rows and validates that each row contains
    /// at least 3 cells with non-empty date, temperature, and conditions data.
    /// </remarks>
    [Then("each forecast should show the date, temperature, and conditions")]
    public async Task ThenEachForecastShouldShowTheDateTemperatureAndConditions()
    {
        var weatherPage = _context.GetOrCreatePage<WeatherPage>();
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
    /// Verifies that each forecast displays temperature in both Celsius and Fahrenheit.
    /// </summary>
    /// <remarks>
    /// Checks that each forecast row contains both °C and °F temperature values.
    /// Validates that the temperature string can be parsed for both units.
    /// </remarks>
    [Then("each forecast should display temperature in both Celsius and Fahrenheit")]
    public async Task ThenEachForecastShouldDisplayTemperatureInBothCelsiusAndFahrenheit()
    {
        var weatherPage = _context.GetOrCreatePage<WeatherPage>();
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
    /// Verifies that the Celsius to Fahrenheit temperature conversions are mathematically correct.
    /// </summary>
    /// <remarks>
    /// Applies the formula F = C * 9/5 + 32 to validate conversions.
    /// Allows for rounding differences within 1 degree to account for display precision.
    /// </remarks>
    [Then("the temperature conversions should be accurate")]
    public async Task ThenTheTemperatureConversionsShouldBeAccurate()
    {
        var weatherPage = _context.GetOrCreatePage<WeatherPage>();
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
    /// Verifies that forecasts are displayed for at least the next 5 days.
    /// </summary>
    /// <remarks>
    /// Waits for at least 5 forecast rows to render before checking the count.
    /// This ensures the page has fully loaded before making the assertion.
    /// </remarks>
    [Then("I should see forecasts for at least the next 5 days")]
    public async Task ThenIShouldSeeForecastsForAtLeastTheNext5Days()
    {
        var weatherPage = _context.GetOrCreatePage<WeatherPage>();

        // Wait for at least 5 forecast rows to be rendered
        await weatherPage.WaitForForecastRowsAsync(minCount: 5);

        var actualCount = await weatherPage.GetForecastCountAsync();
        Assert.That(actualCount, Is.GreaterThanOrEqualTo(5),
            $"Expected at least 5 days of forecasts, but found {actualCount}");
    }

    /// <summary>
    /// Verifies that forecast dates are displayed in chronological order (earliest to latest).
    /// </summary>
    /// <remarks>
    /// Parses the dates from all forecast rows and verifies each date is later
    /// than the previous one in the sequence.
    /// </remarks>
    [Then("forecasts should be ordered chronologically")]
    public async Task ThenForecastsShouldBeOrderedChronologically()
    {
        var weatherPage = _context.GetOrCreatePage<WeatherPage>();
        var dates = await weatherPage.GetParsedDatesAsync();

        Assert.That(dates.Count, Is.GreaterThan(1), "Need at least 2 forecasts to verify chronological order");

        for (int i = 1; i < dates.Count; i++)
        {
            Assert.That(dates[i], Is.GreaterThan(dates[i - 1]),
                $"Forecasts should be in chronological order. Found {dates[i]} after {dates[i - 1]}");
        }
    }

    #endregion
}
