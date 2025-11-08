using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using YoFi.V3.Application.Features;
using YoFi.V3.Entities.Models;

namespace MyApp.Namespace;
[Route("api/[controller]")]
[ApiController]
[Produces("application/json")]
public class WeatherController(WeatherFeature weatherFeature) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(WeatherForecast[]), StatusCodes.Status200OK)]
    public IActionResult GetWeatherForecasts()
    {
        var weather = weatherFeature.GetWeatherForecasts(5);
        return Ok(weather);
    }
}
