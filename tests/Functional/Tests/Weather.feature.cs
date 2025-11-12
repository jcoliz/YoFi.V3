using YoFi.V3.Tests.Functional.Steps;

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
        await GivenIAmOnTheHomePage();
        await WhenINavigateToViewTheWeatherForecast();
        // Hook: Before first Then Step
        await SaveScreenshotAsync();
        await ThenIShouldSeeUpcomingWeatherPredictions();
        await ThenEachForecastShouldShowTheDateTemperatureAndConditions();
    }

    /// <summary>
    /// Forecasts show both Celsius and Fahrenheit
    /// </summary>
    [Test]
    public async Task ForecastsShowBothCelsiusAndFahrenheit()
    {
        await GivenIAmViewingWeatherForecasts();
        // Hook: Before first Then Step
        await SaveScreenshotAsync();
        await ThenEachForecastShouldDisplayTemperatureInBothCelsiusAndFahrenheit();
        await ThenTheTemperatureConversionsShouldBeAccurate();
    }

    /// <summary>
    /// Multi-day forecast is available
    /// </summary>
    [Test]
    public async Task MultiDayForecastIsAvailable()
    {
        await GivenIAmViewingWeatherForecasts();
        // Hook: Before first Then Step
        await SaveScreenshotAsync();
        await ThenIShouldSeeForecastsForAtLeastTheNext5Days();
        await ThenForecastsShouldBeOrderedChronologically();
    }
}
