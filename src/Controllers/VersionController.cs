using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YoFi.V3.Entities.Options;

namespace YoFi.V3.Controllers;

/// <summary>
/// Provides application version information.
/// </summary>
/// <param name="env">The web host environment for environment-specific behavior.</param>
/// <param name="options">Application options containing version information.</param>
/// <param name="logger">Logger for diagnostic output.</param>
/// <remarks>
/// Returns the application version string. In non-production environments, the environment
/// name is appended to the version (e.g., "1.0.0 (Development)").
/// </remarks>
[Route("[controller]")]
[ApiController]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public partial class VersionController(IWebHostEnvironment env, IOptions<ApplicationOptions> options, ILogger<VersionController> logger) : ControllerBase
{
    /// <summary>
    /// Retrieves the current application version.
    /// </summary>
    /// <returns>The version string, optionally with environment name in non-production environments.</returns>
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

        var versionWithEnv = env.IsProduction()
            ? version
            : $"{version} ({env.EnvironmentName})";

        LogOkVersion(versionWithEnv);
        return Ok(versionWithEnv);
    }

    [LoggerMessage(2, LogLevel.Information, "{Location}: OK. version {Version}")]
    private partial void LogOkVersion(string version, [CallerMemberName] string location = "");

    [LoggerMessage(3, LogLevel.Warning, "{Location}: Version not found")]
    private partial void LogNotFound([CallerMemberName] string location = "");
}
