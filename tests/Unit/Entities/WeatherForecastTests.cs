using YoFi.V3.Entities.Models;

namespace YoFi.V3.Tests.Unit.Entities;

/// <summary>
/// Unit tests for WeatherForecast entity.
/// </summary>
[TestFixture]
public class WeatherForecastTests
{
    [Test]
    public void TemperatureF_CalculatedCorrectly_ForPositiveCelsius()
    {
        // Given: A weather forecast with positive Celsius temperature
        var forecast = new WeatherForecast
        {
            Date = DateOnly.FromDateTime(DateTime.Now),
            TemperatureC = 20,
            Summary = "Sunny"
        };

        // When: Accessing the Fahrenheit property
        var fahrenheit = forecast.TemperatureF;

        // Then: It should be correctly calculated (20°C = 68°F)
        Assert.That(fahrenheit, Is.EqualTo(68));
    }

    [Test]
    public void TemperatureF_CalculatedCorrectly_ForNegativeCelsius()
    {
        // Given: A weather forecast with negative Celsius temperature
        var forecast = new WeatherForecast
        {
            Date = DateOnly.FromDateTime(DateTime.Now),
            TemperatureC = -10,
            Summary = "Freezing"
        };

        // When: Accessing the Fahrenheit property
        var fahrenheit = forecast.TemperatureF;

        // Then: It should be correctly calculated (-10°C = 14°F)
        Assert.That(fahrenheit, Is.EqualTo(14));
    }

    [Test]
    public void TemperatureF_CalculatedCorrectly_ForZeroCelsius()
    {
        // Given: A weather forecast with zero Celsius temperature
        var forecast = new WeatherForecast
        {
            Date = DateOnly.FromDateTime(DateTime.Now),
            TemperatureC = 0,
            Summary = "Cold"
        };

        // When: Accessing the Fahrenheit property
        var fahrenheit = forecast.TemperatureF;

        // Then: It should be correctly calculated (0°C = 32°F)
        Assert.That(fahrenheit, Is.EqualTo(32));
    }

    [TestCase(100, 212)]  // Boiling point
    [TestCase(-40, -40)]  // Where scales meet
    [TestCase(37, 98)]    // Body temperature (approximately)
    public void TemperatureF_CalculatedCorrectly_ForVariousTemperatures(int celsius, int expectedFahrenheit)
    {
        // Given: A weather forecast with specified Celsius temperature
        var forecast = new WeatherForecast
        {
            Date = DateOnly.FromDateTime(DateTime.Now),
            TemperatureC = celsius,
            Summary = "Test"
        };

        // When: Accessing the Fahrenheit property
        var fahrenheit = forecast.TemperatureF;

        // Then: It should match the expected Fahrenheit value
        Assert.That(fahrenheit, Is.EqualTo(expectedFahrenheit));
    }
}
