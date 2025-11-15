using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YoFi.V3.Entities.Options;

namespace YoFi.V3.Controllers;

[Route("[controller]")]
[ApiController]
[Produces("application/json")]
public partial class VersionController(IOptions<ApplicationOptions> options, ILogger<VersionController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    public IActionResult GetVersion()
    {
        try
        {
            var version = options.Value.Version;
            if (version is null)
            {
                LogVersionNotFound();
                return NotFound();
            }
            var environment = options.Value.Environment;
            if (environment == EnvironmentType.Local)
            {
                version += " (Local)";
            }
            else if (environment == EnvironmentType.Container)
            {
                version += " (Container)";
            }

            LogOk(version);
            return Ok(version);
        }
        catch (Exception ex)
        {
            LogFailed(ex);
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }

    [LoggerMessage(1, LogLevel.Error, "{Location}: Failed")]
    private partial void LogFailed(Exception ex, [CallerMemberName] string location = "");

    [LoggerMessage(2, LogLevel.Information, "{Location}: OK. version {Version}")]    
    private partial void LogOk(string version, [CallerMemberName] string location = "");

    [LoggerMessage(3, LogLevel.Warning, "{Location}: Version not found")]
    private partial void LogVersionNotFound([CallerMemberName] string location = "");
}
