namespace YoFi.V3.Application.Features;

using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Providers;

#pragma warning disable CS9113 // Parameter is unread: Work in progress.
public class WeatherFeature(IDataProvider dataProvider)
#pragma warning restore CS9113 // Parameter is unread.
{
    private static readonly string[] Summaries = new[]
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

    public WeatherForecast[] GetWeatherForecasts(int days)
    {
        return Enumerable.Range(1, days).Select(index =>
            new WeatherForecast
            {
                Id = index,
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            }).ToArray();
    }
}
