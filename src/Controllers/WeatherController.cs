using System.Runtime.CompilerServices;
using System.Threading.Tasks;
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
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetWeatherForecasts()
    {
        LogStarting();

        const int numberOfDays = 5;
        var weather = await weatherFeature.GetWeatherForecasts(numberOfDays);
        LogOkCount(weather.Length);
        return Ok(weather);
    }

    [LoggerMessage(0, LogLevel.Debug, "{Location}: Starting")]
    private partial void LogStarting([CallerMemberName] string location = "");

    [LoggerMessage(2, LogLevel.Information, "{Location}: OK {Count} items")]
    private partial void LogOkCount(int count, [CallerMemberName] string location = "");
}
