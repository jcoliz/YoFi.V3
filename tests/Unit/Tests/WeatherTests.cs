namespace YoFi.V3.Tests.Unit.Tests;

using NUnit.Framework;
using YoFi.V3.Application.Features;
using YoFi.V3.Entities.Models;

[TestFixture]
public class WeatherTests
{
    private WeatherFeature _weatherFeature;

    [SetUp]
    public void Setup()
    {
        _weatherFeature = new WeatherFeature();
    }

    [Test]
    public void GetWeatherForecasts_ReturnsExpectedResults()
    {
        // When: Retrieving weather forecasts
        var result = _weatherFeature.GetWeatherForecasts(5);

        // Then: The result should be an array of WeatherForecast with the expected length
        Assert.That(result, Is.TypeOf<WeatherForecast[]>());
        Assert.That(result.Length, Is.EqualTo(5));
    }
}
