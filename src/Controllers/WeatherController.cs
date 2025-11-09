using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using YoFi.V3.Application.Features;
using YoFi.V3.Entities.Models;

namespace YoFi.V3.Controllers;

[Route("api/[controller]")]
[ApiController]
[Produces("application/json")]
public partial class WeatherController(WeatherFeature weatherFeature, ILogger<WeatherController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(WeatherForecast[]), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    public IActionResult GetWeatherForecasts()
    {
        try
        {
            LogFetchingWeatherForecasts();

            const int numberOfDays = 5;
            var weather = weatherFeature.GetWeatherForecasts(numberOfDays);
            LogSuccessfullyFetchedWeatherForecasts(numberOfDays);
            return Ok(weather);
        }
        catch (Exception ex)
        {
            LogErrorFetchingWeatherForecasts(ex);
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }

    [LoggerMessage(0, LogLevel.Debug, "{Location}: Fetching weather forecasts")]
    private partial void LogFetchingWeatherForecasts([CallerMemberName] string location = "");

    [LoggerMessage(1, LogLevel.Error, "{Location}: Failed fetching weather forecasts")]
    private partial void LogErrorFetchingWeatherForecasts(Exception ex, [CallerMemberName] string location = "");

    [LoggerMessage(2, LogLevel.Information, "{Location}: Successfully fetched {Count} weather forecasts")]
    private partial void LogSuccessfullyFetchedWeatherForecasts(int count, [CallerMemberName] string location = "");
}
