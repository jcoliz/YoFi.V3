using YoFi.V3.Tests.Functional.Steps;
using NUnit.Framework;

namespace YoFi.V3.Tests.Functional.Features;

/// <summary>
/// Weather Forecasts
/// </summary>
/// <remarks>
/// As a user planning my activities
/// I want to view upcoming weather forecasts
/// So that I can plan accordingly
/// </remarks>
public class WeatherForecasts : WeatherSteps
{
    /// <summary>
    /// User views the weather forecast
    /// </summary>
    [Test]
    public async Task UserViewsTheWeatherForecast()
    {
        // Given I am on the home page
        await GivenIAmOnTheHomePage();

        // When I navigate to view the weather forecast
        await WhenINavigateToViewTheWeatherForecast();

        // Hook: Before first Then Step
        await SaveScreenshotAsync();

        // Then I should see upcoming weather predictions
        await ThenIShouldSeeUpcomingWeatherPredictions();

        // And each forecast should show the date, temperature, and conditions
        await ThenEachForecastShouldShowTheDateTemperatureAndConditions();
    }

    /// <summary>
    /// Forecasts show both Celsius and Fahrenheit
    /// </summary>
    [Test]
    public async Task ForecastsShowBothCelsiusAndFahrenheit()
    {
        // Given I am viewing weather forecasts
        await GivenIAmViewingWeatherForecasts();

        // Hook: Before first Then Step
        await SaveScreenshotAsync();

        // Then each forecast should display temperature in both Celsius and Fahrenheit
        await ThenEachForecastShouldDisplayTemperatureInBothCelsiusAndFahrenheit();

        // And the temperature conversions should be accurate
        await ThenTheTemperatureConversionsShouldBeAccurate();
    }

    /// <summary>
    /// Multi-day forecast is available
    /// </summary>
    [Test]
    public async Task MultiDayForecastIsAvailable()
    {
        // Given I am viewing weather forecasts
        await GivenIAmViewingWeatherForecasts();

        // Hook: Before first Then Step
        await SaveScreenshotAsync();

        // Then I should see forecasts for at least the next 5 days
        await ThenIShouldSeeForecastsForAtLeastTheNext5Days();

        // And forecasts should be ordered chronologically
        await ThenForecastsShouldBeOrderedChronologically();
    }
}
