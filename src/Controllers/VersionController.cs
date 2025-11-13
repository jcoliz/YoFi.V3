using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace YoFi.V3.Controllers;

[Route("[controller]")]
[ApiController]
[Produces("application/json")]
public partial class VersionController(IConfiguration configuration, ILogger<VersionController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    public IActionResult GetVersion()
    {
        try
        {
            var version = configuration["Startup:Version"];
            if (version is null)
            {
                return NotFound();
            }
            LogSuccessfullyFetchedVersion(version);
            return Ok(version);
        }
        catch (Exception ex)
        {
            LogErrorFetchingVersion(ex);
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }

    [LoggerMessage(1, LogLevel.Error, "{Location}: Failed fetching version")]
    private partial void LogErrorFetchingVersion(Exception ex, [CallerMemberName] string location = "");

    [LoggerMessage(2, LogLevel.Information, "{Location}: Successfully fetched version {Version}")]
    private partial void LogSuccessfullyFetchedVersion(string version, [CallerMemberName] string location = "");
}
