using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using YoFi.V3.Entities.Tenancy.Exceptions;

namespace YoFi.V3.Controllers;

/// <summary>
/// Information about an error code available for testing
/// </summary>
/// <param name="Code">The error code to use in the query parameter</param>
/// <param name="Description">Description of what error will be generated</param>
public record ErrorCodeInfo(string Code, string Description);

public record TestUser(int Id)
{
    /// <summary>
    /// Prefix for test users
    /// </summary>
    /// <remarks>
    /// A user with this value in their username is being used during functional testing.
    /// </remarks>
    internal const string Prefix = "__TEST__";

    public string Username { get; init; } = $"{Prefix}{Id:X4}";
    public string Email { get; init; } = $"{Prefix}{Id:X4}@test.com";
    public string Password { get; init; } = Convert.ToBase64String(Guid.NewGuid().ToByteArray())[..12] + "!1";
}

/// <summary>
/// Controller for test user management
/// </summary>
/// <param name="logger"></param>
[Route("[controller]")]
[ApiController]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
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
    public IActionResult ApproveUser(string username)
    {
        if (!username.Contains(TestUser.Prefix))
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
    public async Task<IActionResult> DeleteUsers()
    {
        var testUsers = userManager.Users
            .Where(u => u.UserName != null && u.UserName.Contains(TestUser.Prefix))
            .ToList();

        foreach (var user in testUsers)
        {
            await userManager.DeleteAsync(user);
        }

        LogOkUsername("all users");
        return NoContent();
    }

    /// <summary>
    /// List available error codes that can be generated for testing
    /// </summary>
    /// <returns>A collection of error code descriptions</returns>
    [HttpGet("errors")]
    [ProducesResponseType(typeof(IReadOnlyCollection<ErrorCodeInfo>), StatusCodes.Status200OK)]
    public IActionResult ListErrors()
    {
        var errorCodes = new[]
        {
            new ErrorCodeInfo("400", "Bad Request (empty body)"),
            new ErrorCodeInfo("400m", "Bad Request with message"),
            new ErrorCodeInfo("400p", "Bad Request with Problem Details"),
            new ErrorCodeInfo("400a", "Bad Request from ArgumentException"),
            new ErrorCodeInfo("401", "Unauthorized"),
            new ErrorCodeInfo("403", "Forbidden"),
            new ErrorCodeInfo("403p", "Forbidden with Problem Details"),
            new ErrorCodeInfo("403etnf", "Forbidden from TenantNotFoundException"),
            new ErrorCodeInfo("403etad", "Forbidden from TenantAccessDeniedException"),
            new ErrorCodeInfo("404", "Not Found"),
            new ErrorCodeInfo("404etr", "Not Found from TransactionNotFoundException"),
            new ErrorCodeInfo("404etrnf", "Not Found from UserTenantRoleNotFoundException"),
            new ErrorCodeInfo("409", "Conflict"),
            new ErrorCodeInfo("409edur", "Conflict from DuplicateUserTenantRoleException"),
            new ErrorCodeInfo("500", "Internal Server Error (throws exception)"),
            new ErrorCodeInfo("500etcns", "Internal Server Error from TenantContextNotSetException")
        };

        LogOkCount(errorCodes.Length);
        return Ok(errorCodes);
    }

    /// <summary>
    /// Generate various error codes for testing purposes
    /// </summary>
    /// <param name="code">Kind of error desired</param>
    /// <returns></returns>
    [HttpGet("errors/{code}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public IActionResult ReturnError(string code)
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
            case "400a": // ArgumentException
                throw new ArgumentException("This is a test 400 error from an ArgumentException", nameof(code));
            case "401":
                return Unauthorized();
            case "403":
                return Forbid();
            case "403p":
                return Problem(
                    detail: "This is a test 403 error with a message",
                    statusCode: StatusCodes.Status403Forbidden
                );
            case "403etnf": // TenantNotFoundException
                throw new TenantNotFoundException(Guid.NewGuid());
            case "403etad": // TenantAccessDeniedException
                throw new TenantAccessDeniedException(Guid.NewGuid(), "test-user", Guid.NewGuid());
            case "404":
                return NotFound();
            case "404etr":
                throw new Entities.Exceptions.TransactionNotFoundException(Guid.NewGuid());
            case "404etrnf": // UserTenantRoleNotFoundException
                throw new UserTenantRoleNotFoundException(Guid.NewGuid().ToString(), "test-user", Guid.NewGuid());
            case "409":
                return Conflict();
            case "409edur": // DuplicateUserTenantRoleException
                throw new DuplicateUserTenantRoleException(Guid.NewGuid().ToString(), "test-user", Guid.NewGuid());
            case "500":
#pragma warning disable CA2201 // Do not raise reserved exception types
#pragma warning disable S112 // General exceptions should never be thrown
                // This is only for testing purposes, it's OK here!
                throw new Exception("This is a test 500 error");
#pragma warning restore S112
#pragma warning restore CA2201
            case "500etcns": // TenantContextNotSetException
                throw new TenantContextNotSetException();
            default:
                throw new NotImplementedException();
        }
    }

    [LoggerMessage(1, LogLevel.Error, "{Location}: Failed")]
    private partial void LogFailed(Exception ex, [CallerMemberName] string? location = null);

    [LoggerMessage(2, LogLevel.Information, "{Location}: OK. User {Name}")]
    private partial void LogOkUsername(string name, [CallerMemberName] string? location = null);

    [LoggerMessage(3, LogLevel.Information, "{Location}: OK {Count}")]
    private partial void LogOkCount(int count, [CallerMemberName] string? location = null);
}
