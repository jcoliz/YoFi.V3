namespace YoFi.V3.Tests.Unit.Tests;

using NUnit.Framework;
using YoFi.V3.Application.Features;
using YoFi.V3.Entities.Models;
using YoFi.V3.Tests.Unit.TestHelpers;

[TestFixture]
public class WeatherTests
{
    private WeatherFeature _weatherFeature;
    private InMemoryDataProvider _dataProvider;

    [SetUp]
    public void Setup()
    {
        _dataProvider = new InMemoryDataProvider();
        _weatherFeature = new WeatherFeature(_dataProvider);
    }

    [Test]
    public async Task GetWeatherForecasts_ReturnsExpectedResults()
    {
        // When: Retrieving weather forecasts
        var result = await _weatherFeature.GetWeatherForecasts(5);

        // Then: The result should be an array of WeatherForecast with the expected length
        Assert.That(result, Is.TypeOf<WeatherForecast[]>());
        Assert.That(result.Length, Is.EqualTo(5));

        // Verify that forecasts were actually stored in the data provider
        Assert.That(_dataProvider.WeatherForecasts.Count(), Is.EqualTo(5));
    }

    [Test]
    public async Task GetWeatherForecasts_TemperatureF_CalculatedCorrectly()
    {
        // Arrange: Get some forecasts
        var result = await _weatherFeature.GetWeatherForecasts(1);
        var forecast = result[0];

        // Act: Access the calculated TemperatureF property
        var temperatureF = forecast.TemperatureF;

        // Assert: Verify the Fahrenheit conversion is correct
        var expectedF = 32 + (forecast.TemperatureC * 9 / 5);
        Assert.That(temperatureF, Is.EqualTo(expectedF));
    }
}
