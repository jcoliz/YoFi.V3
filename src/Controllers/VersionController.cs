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
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public partial class VersionController(IOptions<ApplicationOptions> options, ILogger<VersionController> logger) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public IActionResult GetVersion()
    {
        var version = options.Value.Version;
        if (version is null)
        {
            LogNotFound();
            return Problem(
                title: "Version not found",
                detail: "Application version information is not available",
                statusCode: StatusCodes.Status404NotFound
            );
        }

        var versionWithEnv = options.Value.Environment switch
        {
            EnvironmentType.Local => $"{version} (Local)",
            EnvironmentType.Container => $"{version} (Container)",
            _ => version
        };

        LogOkVersion(versionWithEnv);
        return Ok(versionWithEnv);
    }

    [LoggerMessage(2, LogLevel.Information, "{Location}: OK. version {Version}")]
    private partial void LogOkVersion(string version, [CallerMemberName] string location = "");

    [LoggerMessage(3, LogLevel.Warning, "{Location}: Version not found")]
    private partial void LogNotFound([CallerMemberName] string location = "");
}
