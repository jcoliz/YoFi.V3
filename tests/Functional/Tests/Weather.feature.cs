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
    // WARNING: This is a conversion in progress. Does not match the INSTRUCTIONS.md
    // exactly!
    protected NavigationSteps NavigationSteps => _navigationSteps ??= new(this);
    private NavigationSteps? _navigationSteps;

    [SetUp]
    public async Task Background()
    {
        // Given the application is running
        await NavigationSteps.GivenLaunchedSite();

        // And I am logged in
        await GivenIAmLoggedIn();
    }

    /// <summary>
    /// User views the weather forecast
    /// </summary>
    [Test]
    public async Task UserViewsTheWeatherForecast()
    {
        // When I navigate to view the weather forecast
        await WhenINavigateToViewTheWeatherForecast();

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
        await WhenINavigateToViewTheWeatherForecast();

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
        await WhenINavigateToViewTheWeatherForecast();

        // Then I should see forecasts for at least the next 5 days
        await ThenIShouldSeeForecastsForAtLeastTheNext5Days();

        // And forecasts should be ordered chronologically
        await ThenForecastsShouldBeOrderedChronologically();
    }
}
