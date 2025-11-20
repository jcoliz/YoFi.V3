using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace YoFi.V3.Controllers;

public record TestUser(int Id)
{
    public string Username { get; init; } = $"__TEST__{Id:X4}";
    public string Email { get; init; } = $"__TEST__{Id:X4}@test.com";
    public string Password { get; init; } = Convert.ToBase64String(Guid.NewGuid().ToByteArray())[..12] + "!1";
}

/// <summary>
/// Controller for test user management
/// </summary>
/// <param name="logger"></param>
[Route("[controller]")]
[ApiController]
[Produces("application/json")]
public partial class TestControlController(ILogger<TestControlController> logger) : ControllerBase
{
    /// <summary>
    /// Create a test user
    /// </summary>
    /// <remarks>
    /// Also will delete any existing test users.
    /// </remarks>
    /// <returns></returns>
    [HttpPost("users")]
    [ProducesResponseType(typeof(TestUser), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(string), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    public IActionResult CreateUser()
    {
        try
        {
            var result = new TestUser(new Random().Next(1, 0x10000));
            LogOk(result.Username);
            return Ok(result);
        }
        catch (Exception ex)
        {
            LogFailed(ex);
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
        }
    }

    [LoggerMessage(1, LogLevel.Error, "{Location}: Failed")]
    private partial void LogFailed(Exception ex, [CallerMemberName] string location = "");

    [LoggerMessage(2, LogLevel.Information, "{Location}: OK. Returning user {Name}")]    
    private partial void LogOk(string name, [CallerMemberName] string location = "");

}
