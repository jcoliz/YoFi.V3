using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Features;
using YoFi.V3.Application.Tenancy.Dto;
using YoFi.V3.Application.Tenancy.Features;
using YoFi.V3.Controllers.Tenancy.Context;
using YoFi.V3.Entities.Tenancy.Exceptions;
using YoFi.V3.Entities.Tenancy.Models;

namespace YoFi.V3.Controllers;

#region DTOs and Records

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
/// Request to create a workspace for a test user.
/// </summary>
/// <param name="Name">The name of the workspace (must include __TEST__ prefix).</param>
/// <param name="Description">A description of the workspace.</param>
/// <param name="Role">The role to assign to the user (default: Owner).</param>
public record WorkspaceCreateRequest(string Name, string Description, string Role = "Owner");

/// <summary>
/// Request to assign a user to an existing workspace.
/// </summary>
/// <param name="Role">The role to assign to the user in the workspace.</param>
public record UserRoleAssignment(string Role);

/// <summary>
/// Request to seed transactions in a workspace.
/// </summary>
/// <param name="Count">Number of transactions to create.</param>
/// <param name="PayeePrefix">Prefix for payee names (default: "Test Transaction").</param>
public record TransactionSeedRequest(int Count, string PayeePrefix = "Test Transaction");

/// <summary>
/// Request for setting up a workspace with a specific role.
/// </summary>
/// <param name="Name">The name of the workspace.</param>
/// <param name="Description">A description of the workspace.</param>
/// <param name="Role">The role to assign to the user.</param>
public record WorkspaceSetupRequest(string Name, string Description, string Role);

/// <summary>
/// Result of workspace setup including key, name, and assigned role.
/// </summary>
/// <param name="Key">The unique identifier of the created workspace.</param>
/// <param name="Name">The name of the workspace.</param>
/// <param name="Role">The role assigned to the user.</param>
public record WorkspaceSetupResult(Guid Key, string Name, string Role);

/// <summary>
/// Information about an error code available for testing
/// </summary>
/// <param name="Code">The error code to use in the query parameter</param>
/// <param name="Description">Description of what error will be generated</param>
public record ErrorCodeInfo(string Code, string Description);

#endregion

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
    TenantFeature tenantFeature,
    ILogger<TestControlController> logger
) : ControllerBase
{
    #region User Management

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
        // Generate random username with test prefix
        var randomId = new Random().Next(1, 0x10000);
        var username = $"{TestUser.Prefix}{randomId:X4}";

        LogStarting();

        var credentialsResult = await CreateUsersInternalAsync(new[] { username });
        if (credentialsResult.Error != null)
        {
            return credentialsResult.Error;
        }

        LogOk();
        return CreatedAtAction(nameof(CreateUser), credentialsResult.Credentials!.First());
    }

    /// <summary>
    /// Create multiple test users in bulk with credentials
    /// </summary>
    /// <param name="usernames">Collection of usernames to create (must include __TEST__ prefix)</param>
    /// <returns>Collection of created user credentials including IDs and passwords</returns>
    /// <remarks>
    /// All users are automatically approved (email confirmed) for immediate use in tests.
    /// All usernames MUST start with __TEST__ prefix for safety - 403 returned otherwise.
    /// Each user receives a unique, secure random password.
    /// </remarks>
    [HttpPost("users/bulk")]
    [ProducesResponseType(typeof(IReadOnlyCollection<TestUserCredentials>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateBulkUsers([FromBody] IReadOnlyCollection<string> usernames)
    {
        LogStartingCount(usernames.Count);

        var credentialsResult = await CreateUsersInternalAsync(usernames);
        if (credentialsResult.Error != null)
        {
            return credentialsResult.Error;
        }

        LogOkCount(credentialsResult.Credentials!.Count);
        return CreatedAtAction(nameof(CreateBulkUsers), credentialsResult.Credentials);
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
            return ProblemWithLog(
                StatusCodes.Status403Forbidden,
                "Forbidden",
                "Only test users can be approved via this method"
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

    #endregion

    #region Workspace Management

    /// <summary>
    /// Create a workspace for a test user with specified role.
    /// </summary>
    /// <param name="username">The username (must include __TEST__ prefix) of the user.</param>
    /// <param name="request">The workspace creation details.</param>
    /// <returns>The created workspace information.</returns>
    /// <remarks>
    /// Validates that both username and workspace name have __TEST__ prefix for safety.
    /// Returns 403 if either username or workspace name lacks the prefix.
    /// </remarks>
    [HttpPost("users/{username}/workspaces")]
    [ProducesResponseType(typeof(TenantResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateWorkspaceForUser(
        string username,
        [FromBody] WorkspaceCreateRequest request)
    {
        LogStartingKey(username);

        // Validate username has test prefix
        if (!username.StartsWith(TestUser.Prefix, StringComparison.Ordinal))
        {
            return ProblemWithLog(
                StatusCodes.Status403Forbidden,
                "Username must have test prefix",
                $"Username '{username}' must start with {TestUser.Prefix} for test safety"
            );
        }

        // Validate workspace name has test prefix
        if (!request.Name.StartsWith(TestUser.Prefix, StringComparison.Ordinal))
        {
            return ProblemWithLog(
                StatusCodes.Status403Forbidden,
                "Workspace name must have test prefix",
                $"Workspace name '{request.Name}' must start with {TestUser.Prefix} for test safety"
            );
        }

        // Find user
        var user = await userManager.FindByNameAsync(username);
        if (user == null)
        {
            return ProblemWithLog(
                StatusCodes.Status404NotFound,
                "User not found",
                $"Test user '{username}' not found"
            );
        }

        // Parse role
        if (!Enum.TryParse<TenantRole>(request.Role, ignoreCase: true, out var role))
        {
            return ProblemWithLog(
                StatusCodes.Status400BadRequest,
                "Invalid role",
                $"Role '{request.Role}' is not valid. Valid roles: Owner, Editor, Viewer"
            );
        }

        var userId = Guid.Parse(user.Id);
        var tenantDto = new TenantEditDto(request.Name, request.Description);

        // Create tenant without any role assignments (administrative creation)
        var result = await tenantFeature.CreateTenantAsync(tenantDto);

        // Get the created tenant to obtain its ID for role assignment
        var tenant = await tenantFeature.GetTenantByKeyAsync(result.Key);

        // Assign the requested role to the user
        await tenantFeature.AddUserTenantRoleAsync(userId, tenant!.Id, role);

        LogOkKey(result.Key);
        return CreatedAtAction(nameof(CreateWorkspaceForUser), new { username }, result);
    }

    /// <summary>
    /// Assign a user to an existing workspace with a specific role.
    /// </summary>
    /// <param name="username">The username (must include __TEST__ prefix) of the user.</param>
    /// <param name="workspaceKey">The unique key of the workspace.</param>
    /// <param name="assignment">The role assignment details.</param>
    /// <returns>204 No Content on success.</returns>
    /// <remarks>
    /// Validates that both user and workspace have __TEST__ prefix for safety.
    /// Returns 403 if either username or workspace name lacks the prefix.
    /// </remarks>
    [HttpPost("users/{username}/workspaces/{workspaceKey:guid}/assign")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignUserToWorkspace(
        string username,
        Guid workspaceKey,
        [FromBody] UserRoleAssignment assignment)
    {
        LogStartingKey(workspaceKey);

        // Validate username has test prefix
        if (!username.StartsWith(TestUser.Prefix, StringComparison.Ordinal))
        {
            return ProblemWithLog(
                StatusCodes.Status403Forbidden,
                "Username must have test prefix",
                $"Username '{username}' must start with {TestUser.Prefix} for test safety"
            );
        }

        // Find user
        var user = await userManager.FindByNameAsync(username);
        if (user == null)
        {
            return ProblemWithLog(
                StatusCodes.Status404NotFound,
                "User not found",
                $"Test user '{username}' not found"
            );
        }

        // Get workspace and validate it has __TEST__ prefix
        var tenant = await tenantFeature.GetTenantByKeyAsync(workspaceKey);
        if (tenant == null)
        {
            return ProblemWithLog(
                StatusCodes.Status404NotFound,
                "Workspace not found",
                $"Workspace with key '{workspaceKey}' not found"
            );
        }

        if (!tenant.Name.StartsWith(TestUser.Prefix, StringComparison.Ordinal))
        {
            return ProblemWithLog(
                StatusCodes.Status403Forbidden,
                "Workspace is not a test workspace",
                $"Workspace '{tenant.Name}' must start with {TestUser.Prefix} for test safety"
            );
        }

        // Parse role
        if (!Enum.TryParse<TenantRole>(assignment.Role, ignoreCase: true, out var role))
        {
            return ProblemWithLog(
                StatusCodes.Status400BadRequest,
                "Invalid role",
                $"Role '{assignment.Role}' is not valid. Valid roles: Owner, Editor, Viewer"
            );
        }

        var userId = Guid.Parse(user.Id);

        try
        {
            await tenantFeature.AddUserTenantRoleAsync(userId, tenant.Id, role);
        }
        catch (DuplicateUserTenantRoleException ex)
        {
            return ProblemWithLog(
                StatusCodes.Status409Conflict,
                "User already has role in workspace",
                ex.Message
            );
        }

        LogOk();
        return NoContent();
    }

    /// <summary>
    /// Seed test transactions in a workspace for a user.
    /// </summary>
    /// <param name="username">The username (must include __TEST__ prefix) of the user.</param>
    /// <param name="workspaceKey">The unique key of the workspace.</param>
    /// <param name="request">The transaction seeding details.</param>
    /// <returns>The collection of created transactions.</returns>
    /// <remarks>
    /// Validates that user has access to the workspace and both user and workspace have __TEST__ prefix.
    /// Returns 403 if either username or workspace name lacks the prefix.
    /// </remarks>
    [HttpPost("users/{username}/workspaces/{tenantKey:guid}/transactions/seed")]
    [ProducesResponseType(typeof(IReadOnlyCollection<TransactionResultDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SeedTransactions(
        string username,
        Guid tenantKey,
        [FromBody] TransactionSeedRequest request,
        [FromServices] TenantContext tenantContext,
        [FromServices] TransactionsFeature transactionsFeature)
    {
        LogStartingCount(request.Count);

        // Validate username has test prefix
        if (!username.StartsWith(TestUser.Prefix, StringComparison.Ordinal))
        {
            return ProblemWithLog(
                StatusCodes.Status403Forbidden,
                "Username must have test prefix",
                $"Username '{username}' must start with {TestUser.Prefix} for test safety"
            );
        }

        // Find user
        var user = await userManager.FindByNameAsync(username);
        if (user == null)
        {
            return ProblemWithLog(
                StatusCodes.Status404NotFound,
                "User not found",
                $"Test user '{username}' not found"
            );
        }

        // Get workspace and validate it has __TEST__ prefix
        var tenant = await tenantFeature.GetTenantByKeyAsync(tenantKey);
        if (tenant == null)
        {
            return ProblemWithLog(
                StatusCodes.Status404NotFound,
                "Workspace not found",
                $"Workspace with key '{tenantKey}' not found"
            );
        }

        if (!tenant.Name.StartsWith(TestUser.Prefix, StringComparison.Ordinal))
        {
            return ProblemWithLog(
                StatusCodes.Status403Forbidden,
                "Workspace is not a test workspace",
                $"Workspace '{tenant.Name}' must start with {TestUser.Prefix} for test safety"
            );
        }

        var userId = Guid.Parse(user.Id);

        // Verify user has access to this workspace
        var hasAccess = await tenantFeature.HasUserTenantRoleAsync(userId, tenant.Id);
        if (!hasAccess)
        {
            return ProblemWithLog(
                StatusCodes.Status403Forbidden,
                "User does not have access to workspace",
                $"User '{username}' must have a role in workspace '{tenant.Name}' to seed transactions"
            );
        }

        await tenantContext.SetCurrentTenantAsync(tenantKey);

        // Create transactions with realistic test data
        var random = new Random();
        var createdTransactions = new List<TransactionResultDto>();
        var baseDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));

        for (int i = 1; i <= request.Count; i++)
        {
            var transaction = new TransactionEditDto(
                Date: baseDate.AddDays(random.Next(0, 30)),
                Amount: Math.Round((decimal)(random.NextDouble() * 490 + 10), 2),
                Payee: $"{request.PayeePrefix} {i}"
            );

            var result = await transactionsFeature.AddTransactionAsync(transaction);
            createdTransactions.Add(result);
        }

        LogOkCount(createdTransactions.Count);
        return CreatedAtAction(nameof(SeedTransactions), new { username, tenantKey }, createdTransactions);
    }

    /// <summary>
    /// Delete all test data including test users and test workspaces.
    /// </summary>
    /// <returns>204 No Content on success.</returns>
    /// <remarks>
    /// Deletes all workspaces with __TEST__ prefix and all users with __TEST__ prefix.
    /// Cascade deletes will remove associated role assignments and transactions.
    /// </remarks>
    [HttpDelete("data")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAllTestData()
    {
        LogStarting();

        // Delete all test workspaces
        var testTenants = await tenantFeature.GetTenantsByNamePrefixAsync(TestUser.Prefix);
        var tenantKeys = testTenants.Select(t => t.Key).ToList();
        if (tenantKeys.Count > 0)
        {
            await tenantFeature.DeleteTenantsByKeysAsync(tenantKeys);
        }

        // Delete all test users (reuse existing functionality)
        var testUsers = userManager.Users
            .Where(u => u.UserName != null && u.UserName.Contains(TestUser.Prefix))
            .ToList();

        foreach (var user in testUsers)
        {
            await userManager.DeleteAsync(user);
        }

        LogOk();
        return NoContent();
    }

    /// <summary>
    /// Create multiple workspaces for a user in a single request.
    /// </summary>
    /// <param name="username">The username (must include __TEST__ prefix) of the user.</param>
    /// <param name="workspaces">The collection of workspace setup requests.</param>
    /// <returns>The collection of created workspace results with keys and roles.</returns>
    /// <remarks>
    /// Validates that username and all workspace names have __TEST__ prefix before creating any.
    /// Returns 403 if username or any workspace name lacks the prefix.
    /// </remarks>
    [HttpPost("users/{username}/workspaces/bulk")]
    [ProducesResponseType(typeof(IReadOnlyCollection<WorkspaceSetupResult>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> BulkWorkspaceSetup(
        string username,
        [FromBody] IReadOnlyCollection<WorkspaceSetupRequest> workspaces)
    {
        LogStartingCount(workspaces.Count);

        // Validate username has test prefix
        if (!username.StartsWith(TestUser.Prefix, StringComparison.Ordinal))
        {
            return ProblemWithLog(
                StatusCodes.Status403Forbidden,
                "Username must have test prefix",
                $"Username '{username}' must start with {TestUser.Prefix} for test safety"
            );
        }

        // Find user
        var user = await userManager.FindByNameAsync(username);
        if (user == null)
        {
            return ProblemWithLog(
                StatusCodes.Status404NotFound,
                "User not found",
                $"Test user '{username}' not found"
            );
        }

        // Validate all workspace names have test prefix
        var invalidWorkspaces = workspaces.Where(w => !w.Name.StartsWith(TestUser.Prefix, StringComparison.Ordinal)).ToList();
        if (invalidWorkspaces.Count > 0)
        {
            return ProblemWithLog(
                StatusCodes.Status403Forbidden,
                "Invalid workspace names",
                $"All workspace names must start with {TestUser.Prefix}. Invalid: {string.Join(", ", invalidWorkspaces.Select(w => w.Name))}"
            );
        }

        var userId = Guid.Parse(user.Id);
        var results = new List<WorkspaceSetupResult>();

        foreach (var workspace in workspaces)
        {
            // Parse role
            if (!Enum.TryParse<TenantRole>(workspace.Role, ignoreCase: true, out var role))
            {
                return ProblemWithLog(
                    StatusCodes.Status400BadRequest,
                    "Invalid role",
                    $"Role '{workspace.Role}' is not valid. Valid roles: Owner, Editor, Viewer"
                );
            }

            var tenantDto = new TenantEditDto(workspace.Name, workspace.Description);
            var created = await tenantFeature.CreateTenantForUserAsync(userId, tenantDto);

            results.Add(new WorkspaceSetupResult(created.Key, created.Name, workspace.Role));
        }

        LogOkCount(results.Count);
        return CreatedAtAction(nameof(BulkWorkspaceSetup), new { username }, results);
    }

    #endregion

    #region Error Testing

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
                return ProblemWithLog(
                    StatusCodes.Status400BadRequest,
                    "Bad Request",
                    "This is a test 400 error with a message"
                );
            case "400a": // ArgumentException
                throw new ArgumentException("This is a test 400 error from an ArgumentException", nameof(code));
            case "401":
                return Unauthorized();
            case "403":
                return Forbid();
            case "403p":
                return ProblemWithLog(
                    StatusCodes.Status403Forbidden,
                    "Forbidden",
                    "This is a test 403 error with a message"
                );
            case "403etnf": // TenantNotFoundException
                throw new TenantNotFoundException(Guid.NewGuid());
            case "403etad": // TenantAccessDeniedException
                throw new TenantAccessDeniedException(Guid.NewGuid(), Guid.NewGuid());
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

    #endregion

    #region Helper Methods

    /// <summary>
    /// Result of internal user creation operation.
    /// </summary>
    /// <param name="Credentials">The created user credentials if successful.</param>
    /// <param name="Error">The error result if creation failed.</param>
    private record UserCreationResult(
        IReadOnlyCollection<TestUserCredentials>? Credentials,
        ObjectResult? Error);

    /// <summary>
    /// Internal helper method to create test users with validation and error handling.
    /// </summary>
    /// <param name="usernames">Collection of usernames to create (must include __TEST__ prefix).</param>
    /// <returns>A result containing either the created credentials or an error response.</returns>
    private async Task<UserCreationResult> CreateUsersInternalAsync(IReadOnlyCollection<string> usernames)
    {
        // Validate all usernames have test prefix
        var invalidUsernames = usernames.Where(u => !u.StartsWith(TestUser.Prefix, StringComparison.Ordinal)).ToList();
        if (invalidUsernames.Count > 0)
        {
            return new UserCreationResult(
                null,
                ProblemWithLog(
                    StatusCodes.Status403Forbidden,
                    "Invalid usernames",
                    $"All usernames must start with {TestUser.Prefix}. Invalid: {string.Join(", ", invalidUsernames)}"
                )
            );
        }

        var credentials = new List<TestUserCredentials>();

        foreach (var username in usernames)
        {
            var email = $"{username}@test.com";
            var password = Convert.ToBase64String(Guid.NewGuid().ToByteArray())[..12] + "!1";

            var identityUser = new IdentityUser
            {
                UserName = username,
                Email = email,
                EmailConfirmed = true // Auto-approve for test users
            };

            var result = await userManager.CreateAsync(identityUser, password);

            if (!result.Succeeded)
            {
                return new UserCreationResult(
                    null,
                    ProblemWithLog(
                        StatusCodes.Status403Forbidden,
                        $"Unable to create user {username}",
                        string.Join("; ", result.Errors.Select(e => e.Description))
                    )
                );
            }

            // Get the created user to obtain the GUID
            var createdUser = await userManager.FindByNameAsync(username);
            if (createdUser?.Id == null)
            {
                return new UserCreationResult(
                    null,
                    ProblemWithLog(
                        StatusCodes.Status500InternalServerError,
                        $"Unable to retrieve created user {username}"
                    )
                );
            }

            credentials.Add(new TestUserCredentials(
                Id: Guid.Parse(createdUser.Id),
                Username: username,
                Email: email,
                Password: password
            ));
        }

        return new UserCreationResult(credentials, null);
    }

    /// <summary>
    /// Returns a ProblemDetails response with automatic logging.
    /// </summary>
    /// <param name="statusCode">HTTP status code (e.g., StatusCodes.Status404NotFound)</param>
    /// <param name="title">Brief error title</param>
    /// <param name="detail">Detailed error message (optional)</param>
    /// <returns>ObjectResult containing ProblemDetails with logging side effect</returns>
    /// <remarks>
    /// Automatically logs at Warning level for 4xx errors and Error level for 5xx errors.
    /// Use this method instead of Problem() to ensure all error responses are logged.
    /// </remarks>
    private ObjectResult ProblemWithLog(
        int statusCode,
        string title,
        string? detail = null,
        [CallerMemberName] string? location = null)
    {
        if (statusCode >= 500)
        {
            LogProblemError(statusCode, title, detail ?? string.Empty, location);
        }
        else if (statusCode >= 400)
        {
            LogProblemWarning(statusCode, title, detail ?? string.Empty, location);
        }

        return Problem(title: title, detail: detail, statusCode: statusCode);
    }

    #endregion

    #region Logging

    [LoggerMessage(1, LogLevel.Debug, "{Location}: Starting {Count}")]
    private partial void LogStartingCount(int count, [CallerMemberName] string? location = null);

    [LoggerMessage(2, LogLevel.Error, "{Location}: Failed")]
    private partial void LogFailed(Exception ex, [CallerMemberName] string? location = null);

    [LoggerMessage(3, LogLevel.Information, "{Location}: OK. User {Name}")]
    private partial void LogOkUsername(string name, [CallerMemberName] string? location = null);

    [LoggerMessage(4, LogLevel.Information, "{Location}: OK {Count}")]
    private partial void LogOkCount(int count, [CallerMemberName] string? location = null);

    [LoggerMessage(5, LogLevel.Debug, "{Location}: Starting {Key}")]
    private partial void LogStartingKey(object key, [CallerMemberName] string? location = null);

    [LoggerMessage(6, LogLevel.Information, "{Location}: OK {Key}")]
    private partial void LogOkKey(Guid key, [CallerMemberName] string? location = null);

    [LoggerMessage(7, LogLevel.Debug, "{Location}: Starting")]
    private partial void LogStarting([CallerMemberName] string? location = null);

    [LoggerMessage(8, LogLevel.Information, "{Location}: OK")]
    private partial void LogOk([CallerMemberName] string? location = null);

    [LoggerMessage(9, LogLevel.Warning, "{Location}: Failed. {Error}")]
    private partial void LogError(string error, [CallerMemberName] string? location = null);

    [LoggerMessage(10, LogLevel.Warning, "{Location}: Problem {StatusCode} - {Title}: {Detail}")]
    private partial void LogProblemWarning(int statusCode, string title, string detail, [CallerMemberName] string? location = null);

    [LoggerMessage(11, LogLevel.Error, "{Location}: Problem {StatusCode} - {Title}: {Detail}")]
    private partial void LogProblemError(int statusCode, string title, string detail, [CallerMemberName] string? location = null);

    #endregion
}
