using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
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
public partial class TestControlController(
    UserManager<IdentityUser> userManager,
    ILogger<TestControlController> logger
) : ControllerBase
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
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateUser()
    {
        var newUser = new TestUser(new Random().Next(1, 0x10000));

        var result = await userManager.CreateAsync(new IdentityUser
        {
            UserName = newUser.Username,
            Email = newUser.Email,
            EmailConfirmed = false
        }, newUser.Password);

        if (!result.Succeeded)
        {
            return Problem(
                title: "Unable to create user",
                detail: string.Join("; ", result.Errors.Select(e => e.Description)),
                statusCode: StatusCodes.Status403Forbidden
            );
        }

        LogOkUsername(newUser.Username);
        return Created(nameof(CreateUser), newUser);
    }

    /// <summary>
    /// Approve a test user
    /// </summary>
    /// <remarks>
    /// This simulates the email approval step. When a user is created, they have to be
    /// approved before they can log in. Only users with __TEST__ in their username can be approved
    /// via this method.
    /// </remarks>
    /// <param name="username"></param>
    /// <returns></returns>
    [HttpPut("users/{username}/approve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public IActionResult ApproveUser(string username)
    {
        if (!username.Contains("__TEST__"))
        {
            return Problem(
                detail: "Only test users can be approved via this method",
                statusCode: StatusCodes.Status403Forbidden
            );
        }

        // TODO: Actually do the approval!!
        LogOkUsername(username);
        return NoContent();
    }

    [HttpDelete("users")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteUsers()
    {
        var testUsers = userManager.Users
            .Where(u => u.UserName != null && u.UserName.Contains("__TEST__"))
            .ToList();

        foreach (var user in testUsers)
        {
            await userManager.DeleteAsync(user);
        }

        LogOkUsername("all users");
        return NoContent();
    }

    /// <summary>
    /// Generate various error codes for testing purposes
    /// </summary>
    /// <param name="code">Kind of error desired</param>
    /// <returns></returns>
    [HttpGet("errors")]
    public IActionResult Errors(string? code)
    {
        switch (code?.ToLowerInvariant())
        {
            case "400":
                return BadRequest();
            case "400m":
                return BadRequest("This is a test 400 error with a message");
            case "400p":
                return Problem(
                    detail: "This is a test 400 error with a message",
                    statusCode: StatusCodes.Status400BadRequest
                );
            case "401":
                return Unauthorized();
            case "403":
                return Forbid();
            case "403p":
                return Problem(
                    detail: "This is a test 403 error with a message",
                    statusCode: StatusCodes.Status403Forbidden
                );
            case "404":
                return NotFound();
            case "500":
                throw new Exception("This is a test 500 error");
            default:
                throw new NotImplementedException();
        }
    }

    [LoggerMessage(1, LogLevel.Error, "{Location}: Failed")]
    private partial void LogFailed(Exception ex, [CallerMemberName] string location = "");

    [LoggerMessage(2, LogLevel.Information, "{Location}: OK. User {Name}")]
    private partial void LogOkUsername(string name, [CallerMemberName] string location = "");
}
