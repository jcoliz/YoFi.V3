using YoFi.V3.Application.Features;
using YoFi.V3.Entities.Models;
using YoFi.V3.Tests.Integration.Application.TestHelpers;

namespace YoFi.V3.Tests.Integration.Application;

/// <summary>
/// Integration tests for WeatherFeature.
/// </summary>
/// <remarks>
/// Tests WeatherFeature methods with real ApplicationDbContext
/// to verify IDataProvider usage and data persistence.
/// This serves as a reference implementation for Application Integration tests.
/// </remarks>
public class WeatherFeatureTests : FeatureTestBase
{
    private WeatherFeature _weatherFeature = null!;

    [SetUp]
    public new async Task SetUp()
    {
        await base.SetUp();
        _weatherFeature = new WeatherFeature(_dataProvider);
    }

    [Test]
    public async Task GetWeatherForecasts_NewRequest_GeneratesForecasts()
    {
        // Given: Empty database (no existing forecasts)

        // When: Requesting 5 days of forecasts
        var result = await _weatherFeature.GetWeatherForecasts(5);

        // Then: Should return 5 forecasts
        Assert.That(result, Has.Count.EqualTo(5));

        // And: Forecasts should be for consecutive days starting today
        var today = DateOnly.FromDateTime(DateTime.Now);
        var dates = result.Select(f => f.Date).OrderBy(d => d).ToList();
        Assert.That(dates[0], Is.EqualTo(today));
        Assert.That(dates[4], Is.EqualTo(today.AddDays(4)));

        // And: Each forecast should have valid data
        foreach (var forecast in result)
        {
            Assert.That(forecast.TemperatureC, Is.InRange(-20, 55));
            Assert.That(forecast.Summary, Is.Not.Null.And.Not.Empty);
        }
    }






}
