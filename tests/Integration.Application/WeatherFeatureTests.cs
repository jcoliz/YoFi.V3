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
[TestFixture]
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

    [Test]
    public async Task GetWeatherForecasts_ExistingForecasts_ReturnsExisting()
    {
        // Given: Existing forecasts in database
        var today = DateOnly.FromDateTime(DateTime.Now);
        var existingForecasts = new List<WeatherForecast>
        {
            new() { Date = today, TemperatureC = 20, Summary = "Sunny" },
            new() { Date = today.AddDays(1), TemperatureC = 22, Summary = "Cloudy" },
            new() { Date = today.AddDays(2), TemperatureC = 18, Summary = "Rainy" }
        };
        _context.WeatherForecasts.AddRange(existingForecasts);
        await _context.SaveChangesAsync();

        // When: Requesting 3 days of forecasts
        var result = await _weatherFeature.GetWeatherForecasts(3);

        // Then: Should return the existing 3 forecasts
        Assert.That(result, Has.Count.EqualTo(3));
        Assert.That(result.First(f => f.Date == today).Summary, Is.EqualTo("Sunny"));
        Assert.That(result.First(f => f.Date == today.AddDays(1)).Summary, Is.EqualTo("Cloudy"));
        Assert.That(result.First(f => f.Date == today.AddDays(2)).Summary, Is.EqualTo("Rainy"));
    }

    [Test]
    public async Task GetWeatherForecasts_PartialExisting_FillsMissingDays()
    {
        // Given: Some existing forecasts (day 0 and 2, missing day 1)
        var today = DateOnly.FromDateTime(DateTime.Now);
        var existingForecasts = new List<WeatherForecast>
        {
            new() { Date = today, TemperatureC = 20, Summary = "Sunny" },
            new() { Date = today.AddDays(2), TemperatureC = 18, Summary = "Rainy" }
        };
        _context.WeatherForecasts.AddRange(existingForecasts);
        await _context.SaveChangesAsync();

        // When: Requesting 3 days of forecasts
        var result = await _weatherFeature.GetWeatherForecasts(3);

        // Then: Should return 3 forecasts
        Assert.That(result, Has.Count.EqualTo(3));

        // And: Day 0 should be existing forecast
        Assert.That(result.First(f => f.Date == today).Summary, Is.EqualTo("Sunny"));

        // And: Day 1 should be newly generated
        var day1 = result.First(f => f.Date == today.AddDays(1));
        Assert.That(day1.TemperatureC, Is.InRange(-20, 55));
        Assert.That(day1.Summary, Is.Not.Null.And.Not.Empty);

        // And: Day 2 should be existing forecast
        Assert.That(result.First(f => f.Date == today.AddDays(2)).Summary, Is.EqualTo("Rainy"));
    }

    [Test]
    public async Task GetWeatherForecasts_RequestMoreDays_GeneratesAdditional()
    {
        // Given: Existing forecasts for 3 days
        var today = DateOnly.FromDateTime(DateTime.Now);
        var existingForecasts = new List<WeatherForecast>
        {
            new() { Date = today, TemperatureC = 20, Summary = "Sunny" },
            new() { Date = today.AddDays(1), TemperatureC = 22, Summary = "Cloudy" },
            new() { Date = today.AddDays(2), TemperatureC = 18, Summary = "Rainy" }
        };
        _context.WeatherForecasts.AddRange(existingForecasts);
        await _context.SaveChangesAsync();

        // When: Requesting 5 days of forecasts
        var result = await _weatherFeature.GetWeatherForecasts(5);

        // Then: Should return 5 forecasts
        Assert.That(result, Has.Count.EqualTo(5));

        // And: First 3 days should be existing forecasts
        Assert.That(result.First(f => f.Date == today).Summary, Is.EqualTo("Sunny"));
        Assert.That(result.First(f => f.Date == today.AddDays(1)).Summary, Is.EqualTo("Cloudy"));
        Assert.That(result.First(f => f.Date == today.AddDays(2)).Summary, Is.EqualTo("Rainy"));

        // And: Days 3 and 4 should be newly generated
        var day3 = result.First(f => f.Date == today.AddDays(3));
        var day4 = result.First(f => f.Date == today.AddDays(4));
        Assert.That(day3.TemperatureC, Is.InRange(-20, 55));
        Assert.That(day4.TemperatureC, Is.InRange(-20, 55));
    }

    [Test]
    public async Task GetWeatherForecasts_MultipleCalls_DoesNotDuplicate()
    {
        // Given: First call generates forecasts
        await _weatherFeature.GetWeatherForecasts(3);

        // When: Calling again with same parameters
        var result = await _weatherFeature.GetWeatherForecasts(3);

        // Then: Should still return 3 forecasts (not duplicated)
        Assert.That(result, Has.Count.EqualTo(3));

        // And: Database should only have 3 forecasts
        var dbCount = _context.WeatherForecasts.Count();
        Assert.That(dbCount, Is.EqualTo(3));
    }

    [Test]
    public async Task GetWeatherForecasts_ReturnsOrderedByDate()
    {
        // Given: Empty database

        // When: Requesting forecasts
        var result = await _weatherFeature.GetWeatherForecasts(5);

        // Then: Results should be ordered by date
        var dates = result.Select(f => f.Date).ToList();
        Assert.That(dates, Is.Ordered);
    }

    [Test]
    public async Task GetWeatherForecasts_SingleDay_Works()
    {
        // Given: Empty database

        // When: Requesting just 1 day
        var result = await _weatherFeature.GetWeatherForecasts(1);

        // Then: Should return 1 forecast for today
        Assert.That(result, Has.Count.EqualTo(1));
        var today = DateOnly.FromDateTime(DateTime.Now);
        Assert.That(result.First().Date, Is.EqualTo(today));
    }
}
