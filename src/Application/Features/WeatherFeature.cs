namespace YoFi.V3.Application.Features;

using YoFi.V3.Entities.Models;

public class WeatherFeature
{
    private static readonly string[] Summaries = new[]
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

    public WeatherForecast[] GetWeatherForecasts(int days)
    {
        return Enumerable.Range(1, days).Select(index =>
            new WeatherForecast
            (
                index,
                DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                Random.Shared.Next(-20, 55),
                Summaries[Random.Shared.Next(Summaries.Length)]
            )).ToArray();
    }
}
