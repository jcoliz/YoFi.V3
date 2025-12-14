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

/// <summary>
/// Data transfer object for test user credentials including unique identifier
/// </summary>
/// <param name="Id">The unique identifier (GUID) of the created user</param>
/// <param name="Username">The username for authentication</param>
/// <param name="Email">The email address for authentication</param>
/// <param name="Password">The generated password for authentication</param>
public record TestUserCredentials(Guid Id, string Username, string Email, string Password);

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
    /// Create a test user with auto-generated username
    /// </summary>
    /// <returns>Created user credentials including ID and password</returns>
    /// <remarks>
    /// User is automatically approved (email confirmed) for immediate use in tests.
    /// Username is auto-generated with format __TEST__XXXX where XXXX is a random hex value.
    /// </remarks>
    [HttpPost("users")]
    [ProducesResponseType(typeof(TestUserCredentials), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateUser()
    {
        // Generate random username and delegate to bulk creation
        var randomId = new Random().Next(1, 0x10000);
        var username = $"{randomId:X4}";

        var result = await CreateBulkUsers(new[] { username });

        if (result is CreatedResult createdResult && createdResult.Value is IReadOnlyCollection<TestUserCredentials> credentials)
        {
            return Created(nameof(CreateUser), credentials.First());
        }

        return result;
    }

    /// <summary>
    /// Create multiple test users in bulk with credentials
    /// </summary>
    /// <param name="usernames">Collection of usernames to create (will be prefixed with __TEST__)</param>
    /// <returns>Collection of created user credentials including IDs and passwords</returns>
    /// <remarks>
    /// All users are automatically approved (email confirmed) for immediate use in tests.
    /// Usernames are automatically prefixed with __TEST__ for consistency.
    /// Each user receives a unique, secure random password.
    /// </remarks>
    [HttpPost("users/bulk")]
    [ProducesResponseType(typeof(IReadOnlyCollection<TestUserCredentials>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateBulkUsers([FromBody] IReadOnlyCollection<string> usernames)
    {
        LogStartingCount(usernames.Count);

        var credentials = new List<TestUserCredentials>();

        foreach (var username in usernames)
        {
            var fullUsername = $"{TestUser.Prefix}{username}";
            var email = $"{TestUser.Prefix}{username}@test.com";
            var password = Convert.ToBase64String(Guid.NewGuid().ToByteArray())[..12] + "!1";

            var identityUser = new IdentityUser
            {
                UserName = fullUsername,
                Email = email,
                EmailConfirmed = true // Auto-approve for test users
            };

            var result = await userManager.CreateAsync(identityUser, password);

            if (!result.Succeeded)
            {
                return Problem(
                    title: $"Unable to create user {fullUsername}",
                    detail: string.Join("; ", result.Errors.Select(e => e.Description)),
                    statusCode: StatusCodes.Status403Forbidden
                );
            }

            // Get the created user to obtain the GUID
            var createdUser = await userManager.FindByNameAsync(fullUsername);
            if (createdUser?.Id == null)
            {
                return Problem(
                    title: $"Unable to retrieve created user {fullUsername}",
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }

            credentials.Add(new TestUserCredentials(
                Id: Guid.Parse(createdUser.Id),
                Username: fullUsername,
                Email: email,
                Password: password
            ));
        }

        LogOkCount(credentials.Count);
        return Created(nameof(CreateBulkUsers), credentials);
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

    [LoggerMessage(0, LogLevel.Debug, "{Location}: Starting {Count}")]
    private partial void LogStartingCount(int count, [CallerMemberName] string? location = null);

    [LoggerMessage(1, LogLevel.Error, "{Location}: Failed")]
    private partial void LogFailed(Exception ex, [CallerMemberName] string? location = null);

    [LoggerMessage(2, LogLevel.Information, "{Location}: OK. User {Name}")]
    private partial void LogOkUsername(string name, [CallerMemberName] string? location = null);

    [LoggerMessage(3, LogLevel.Information, "{Location}: OK {Count}")]
    private partial void LogOkCount(int count, [CallerMemberName] string? location = null);
}
