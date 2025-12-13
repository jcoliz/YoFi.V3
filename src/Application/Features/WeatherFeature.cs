namespace YoFi.V3.Application.Features;

using System.Threading.Tasks;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Providers;

public class WeatherFeature(IDataProvider dataProvider)
{
    private static readonly string[] Summaries = new[]
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

    /// <summary>
    /// Returns weather forecasts for the specified number of days into the future.
    /// </summary>
    /// <remarks>
    /// The earliest forecast is for today.
    /// </remarks>
    /// <param name="days">Number of days into the future for which to generate forecasts.</param>
    /// <returns>A collection of weather forecasts.</returns>
    public async Task<IReadOnlyCollection<WeatherForecast>> GetWeatherForecasts(int days)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var endDate = today.AddDays(days - 1);

        // Step 1: Check to see if there are existing forecasts in the database for the requested days.
        var existingForecastsQuery = dataProvider.Get<WeatherForecast>()
            .Where(wf => wf.Date >= today && wf.Date <= endDate)
            .OrderBy(wf => wf.Date);
        var existingForecasts = await dataProvider.ToListNoTrackingAsync(existingForecastsQuery);

        // Step 2: If not, generate new forecasts and store them in the database.
        if (existingForecasts.Count < days)
        {
            var existingDates = existingForecasts.Select(f => f.Date).ToHashSet();
            var newForecasts = new List<WeatherForecast>();

            for (int i = 0; i < days; i++)
            {
                var date = today.AddDays(i);
                if (!existingDates.Contains(date))
                {
                    var forecast = new WeatherForecast
                    {
                        Date = date,
                        TemperatureC = Random.Shared.Next(-20, 55),
                        Summary = Summaries[Random.Shared.Next(Summaries.Length)]
                    };
                    newForecasts.Add(forecast);
                }
            }

            if (newForecasts.Count > 0)
            {
                dataProvider.AddRange(newForecasts);
                await dataProvider.SaveChangesAsync();
            }

            // Refresh the existing forecasts to include the new ones
            existingForecastsQuery = dataProvider.Get<WeatherForecast>()
                .Where(wf => wf.Date >= today && wf.Date <= endDate)
                .OrderBy(wf => wf.Date);
            existingForecasts = await dataProvider.ToListNoTrackingAsync(existingForecastsQuery);
        }

        // Step 3: Return the forecasts.
        return existingForecasts;
    }
}
