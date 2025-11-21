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
        try
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
                var resultDetails = new ProblemDetails
                {
                    Title = "Forbidden",
                    Detail = string.Join("; ", result.Errors.Select(e => e.Description)),
                    Status = StatusCodes.Status403Forbidden
                };
                return StatusCode(StatusCodes.Status403Forbidden, resultDetails);
            }

            LogOkUsername(newUser.Username);
            return Created(nameof(CreateUser), newUser);
        }
        catch (Exception ex)
        {
            LogFailed(ex);
            var result = new ProblemDetails
            {
                Title = "Failed to create test user",
                Detail = ex.Message,
                Status = StatusCodes.Status500InternalServerError
            };
            return StatusCode(StatusCodes.Status500InternalServerError, result);
        }
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
        try
        {
            if (!username.Contains("__TEST__"))
            {
                var result = new ProblemDetails
                {
                    Title = "Forbidden",
                    Detail = "Only test users can be approved via this method",
                    Status = StatusCodes.Status403Forbidden
                };
                return StatusCode(StatusCodes.Status403Forbidden, result);
            }
            // TODO: Actually do the approval!!
            LogOkUsername(username);
            return NoContent();
        }
        catch (Exception ex)
        {
            LogFailed(ex);
            var result = new ProblemDetails
            {
                Title = "Failed to approve test user",
                Detail = ex.Message,
                Status = StatusCodes.Status500InternalServerError
            };
            return StatusCode(StatusCodes.Status500InternalServerError, result);
        }
    }

    [HttpDelete("users")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public IActionResult DeleteUsers()
    {
        try
        {
            userManager
                .Users
                .Where(u => (u.UserName != null) && u.UserName.Contains("__TEST__"))
                .ToList()
                .ForEach(u => userManager.DeleteAsync(u));

            LogOkUsername("all users");
            return NoContent();
        }
        catch (Exception ex)
        {
            LogFailed(ex);
            var result = new ProblemDetails
            {
                Title = "Failed to delete test users",
                Detail = ex.Message,
                Status = StatusCodes.Status500InternalServerError
            };
            return StatusCode(StatusCodes.Status500InternalServerError, result);
        }
    }

    [LoggerMessage(1, LogLevel.Error, "{Location}: Failed")]
    private partial void LogFailed(Exception ex, [CallerMemberName] string location = "");

    [LoggerMessage(2, LogLevel.Information, "{Location}: OK. User {Name}")]
    private partial void LogOkUsername(string name, [CallerMemberName] string location = "");
}
