using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using YoFi.V3.Application.Features;
using YoFi.V3.Entities.Models;

namespace YoFi.V3.Controllers;

/// <summary>
/// Provides weather forecast data for demonstration and testing purposes.
/// </summary>
/// <param name="weatherFeature">Feature providing weather forecast operations.</param>
/// <param name="logger">Logger for diagnostic output.</param>
/// <remarks>
/// This controller demonstrates basic CRUD operations and data persistence.
/// Weather forecasts are stored in the database and reused across requests.
/// </remarks>
[Route("api/[controller]")]
[ApiController]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public partial class WeatherController(WeatherFeature weatherFeature, ILogger<WeatherController> logger) : ControllerBase
{
    /// <summary>
    /// Retrieves weather forecasts for the next 5 days.
    /// </summary>
    /// <returns>A collection of weather forecasts starting from today.</returns>
    /// <remarks>
    /// Forecasts are generated once and cached in the database for subsequent requests.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<WeatherForecast>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWeatherForecasts()
    {
        LogStarting();

        const int numberOfDays = 5;
        var weather = await weatherFeature.GetWeatherForecasts(numberOfDays);
        LogOkCount(weather.Count);
        return Ok(weather);
    }

    [LoggerMessage(1, LogLevel.Debug, "{Location}: Starting")]
    private partial void LogStarting([CallerMemberName] string location = "");

    [LoggerMessage(2, LogLevel.Information, "{Location}: OK {Count} items")]
    private partial void LogOkCount(int count, [CallerMemberName] string location = "");
}
