using YoFi.V3.Tests.Functional.Steps;

namespace YoFi.V3.Tests.Functional.Features;

/// <summary>
/// (Weather) Forecasts load and displays successfully
/// </summary>
/// <remarks>
/// </remarks>
public partial class WeatherFeature_Tests : FunctionalTest
{
    /// <summary>
    /// Scenario: Forecasts load OK
    /// </summary>
    [Test]
    public async Task ForecastsLoadOK()
    {
        // Given user has launched site
        await GivenLaunchedSite();

        // And user selected option "Weather" in nav bar
        await SelectOptionInNavbar("Weather");

        // Hook Before first Then Step
        await SaveScreenshotAsync();

        // Then page contains 5 forecasts
        await WeatherPageDisplaysForecasts(5);
    }
}
