using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Features;
using YoFi.V3.Application.Helpers;
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
/// <param name="ShortName">The short username without __TEST__ prefix or unique run ID</param>
/// <param name="Username">The username for authentication</param>
/// <param name="Email">The email address for authentication</param>
/// <param name="Password">The generated password for authentication</param>
public record TestUserCredentials(Guid Id, string ShortName, string Username, string Email, string Password);

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
/// <param name="Memo">Optional memo text to apply to all seeded transactions.</param>
/// <param name="Source">Optional source text to apply to all seeded transactions.</param>
/// <param name="ExternalId">Optional external ID text to apply to all seeded transactions.</param>
/// <param name="Category">Optional category text to apply to all seeded transactions.</param>
public record TransactionSeedRequest(
    int Count,
    string PayeePrefix = "Test Transaction",
    string? Memo = null,
    string? Source = null,
    string? ExternalId = null,
    string? Category = null
);

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
/// Controller for test user and workspace management in functional tests.
/// </summary>
/// <param name="userManager">User manager for identity operations.</param>
/// <param name="tenantFeature">Feature providing tenant management operations.</param>
/// <param name="logger">Logger for diagnostic output.</param>
/// <remarks>
/// This controller provides endpoints for creating and managing test users, workspaces,
/// and test data during functional testing. All operations enforce the __TEST__ prefix
/// on usernames and workspace names for safety.
///
/// <para><strong>Test User Management:</strong></para>
/// <list type="bullet">
/// <item><description>Create test users with auto-generated credentials</description></item>
/// <item><description>Bulk create multiple test users</description></item>
/// <item><description>Delete all test users</description></item>
/// </list>
///
/// <para><strong>Workspace Management:</strong></para>
/// <list type="bullet">
/// <item><description>Create workspaces for test users with specified roles</description></item>
/// <item><description>Assign users to existing workspaces</description></item>
/// <item><description>Seed transactions in workspaces</description></item>
/// <item><description>Bulk workspace setup</description></item>
/// </list>
///
/// <para><strong>Error Testing:</strong></para>
/// <list type="bullet">
/// <item><description>List available error codes</description></item>
/// <item><description>Generate specific HTTP error responses</description></item>
/// </list>
/// </remarks>
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
    /// <summary>
    /// Prefix for test users and workspaces
    /// </summary>
    /// <remarks>
    /// A user or workspace with this value in their name is being used during functional testing.
    /// </remarks>
    public const string TestPrefix = "__TEST__";

    #region Test Identification

    /// <summary>
    /// Identify the current test by logging test correlation metadata from the request context.
    /// </summary>
    /// <returns>204 No Content</returns>
    /// <remarks>
    /// IMPORTANT: Every test should call this endpoint at the start of execution to ensure
    /// that test correlation metadata is properly logged and associated with the test run.
    /// Extracts test name, test ID, and test class from Activity tags (set by TestCorrelationMiddleware)
    /// and logs them explicitly. Useful for verifying test correlation is working correctly.
    /// </remarks>
    [HttpPost("ident")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Identify()
    {
        var activity = Activity.Current;

        if (activity != null)
        {
            var testName = activity.GetTagItem("test.name") as string;
            var testId = activity.GetTagItem("test.id") as string;
            var testClass = activity.GetTagItem("test.class") as string;

            LogIdentifyTest(
                testName ?? "null",
                testId ?? "null",
                testClass ?? "null"
            );
        }
        else
        {
            LogIdentifyNoActivity();
        }

        return NoContent();
    }

    #endregion

    #region User Management

    /// <summary>
    /// Create multiple test users with credentials
    /// </summary>
    /// <param name="usernames">Collection of usernames to create (doesn't need to include __TEST__ prefix, it will be added)</param>
    /// <returns>Collection of created user credentials including IDs and passwords</returns>
    /// <remarks>
    /// All users are automatically approved (email confirmed) for immediate use in tests.
    /// User name is prefixed with __TEST__ if not already present, and a unique suffix is added to avoid collisions.
    /// Each user receives a unique, secure random password.
    /// </remarks>
    [HttpPost("users")]
    [ProducesResponseType(typeof(IReadOnlyCollection<TestUserCredentials>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateUsers([FromBody] IReadOnlyCollection<string> usernames)
    {
        LogStartingCount(usernames.Count);

        var credentials = await CreateUsersInternalAsync(usernames);

        LogOkCount(credentials.Count);
        return CreatedAtAction(nameof(CreateUsers), credentials);
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
        if (!username.Contains(TestPrefix))
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

    /// <summary>
    /// Deletes all test users from the system.
    /// </summary>
    /// <returns>204 No Content on success.</returns>
    /// <remarks>
    /// Deletes all users whose username contains the __TEST__ prefix.
    /// </remarks>
    [HttpDelete("users")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteUsers()
    {
        var testUsers = userManager.Users
            .Where(u => u.UserName != null && u.UserName.Contains(TestPrefix))
            .ToList();

        foreach (var user in testUsers)
        {
            await userManager.DeleteAsync(user);
        }

        LogOkUsername("all users");
        return NoContent();
    }

    #endregion

    #region User Management V2 - Parallel Migration Support

    /// <summary>
    /// Create multiple test users from provided credentials (V2 - accepts full credentials).
    /// </summary>
    /// <param name="credentialsList">Collection of credentials including username, email, password</param>
    /// <returns>Collection of created user credentials with IDs populated</returns>
    /// <remarks>
    /// V2 endpoint that accepts complete credential objects from the client.
    /// Client generates unique usernames and passwords, server just validates and stores in DB.
    /// All usernames must start with __TEST__ prefix for safety.
    /// Supports parallel test execution by allowing client-side credential generation.
    /// </remarks>
    [HttpPost("v2/users")]
    [ProducesResponseType(typeof(IReadOnlyCollection<TestUserCredentials>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateUsersV2([FromBody] IReadOnlyCollection<TestUserCredentials> credentialsList)
    {
        LogStartingCount(credentialsList.Count);

        // Validate input
        if (credentialsList == null || credentialsList.Count == 0)
        {
            return ProblemWithLog(
                StatusCodes.Status400BadRequest,
                "Credentials list required",
                "Must provide at least one credential object"
            );
        }

        var createdCredentials = new List<TestUserCredentials>();

        foreach (var creds in credentialsList)
        {
            // Validate username has test prefix
            if (!creds.Username.StartsWith(TestPrefix, StringComparison.Ordinal))
            {
                return ProblemWithLog(
                    StatusCodes.Status403Forbidden,
                    "Username must have test prefix",
                    $"Username '{creds.Username}' must start with {TestPrefix}"
                );
            }

            // Validate required fields
            if (string.IsNullOrWhiteSpace(creds.Username) ||
                string.IsNullOrWhiteSpace(creds.Email) ||
                string.IsNullOrWhiteSpace(creds.Password))
            {
                return ProblemWithLog(
                    StatusCodes.Status400BadRequest,
                    "Invalid credentials",
                    "Username, Email, and Password are required"
                );
            }

            // Create user with provided credentials
            var identityUser = new IdentityUser
            {
                UserName = creds.Username,
                Email = creds.Email,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(identityUser, creds.Password);
            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                return ProblemWithLog(
                    StatusCodes.Status400BadRequest,
                    "User creation failed",
                    $"Unable to create user {creds.Username}: {errors}"
                );
            }

            // Get created user to populate ID
            var createdUser = await userManager.FindByNameAsync(creds.Username);
            createdCredentials.Add(new TestUserCredentials(
                Id: Guid.Parse(createdUser!.Id),
                ShortName: creds.ShortName,
                Username: creds.Username,
                Email: creds.Email,
                Password: creds.Password
            ));
        }

        LogOkCount(createdCredentials.Count);
        return CreatedAtAction(nameof(CreateUsersV2), createdCredentials);
    }

    /// <summary>
    /// Delete specific test users by username list (V2 - requires explicit list).
    /// </summary>
    /// <param name="usernames">Collection of usernames to delete. Must not be empty.</param>
    /// <returns>204 No Content on success.</returns>
    /// <remarks>
    /// V2 endpoint that requires explicit username list for safety.
    /// Empty or null list returns 400 Bad Request (no "delete all" behavior).
    /// All usernames must start with __TEST__ prefix.
    /// Supports parallel test execution by only deleting specified users.
    /// </remarks>
    [HttpDelete("v2/users")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteUsersV2([FromBody] IReadOnlyCollection<string> usernames)
    {
        LogStartingCount(usernames?.Count ?? 0);

        // Require explicit username list
        if (usernames == null || usernames.Count == 0)
        {
            return ProblemWithLog(
                StatusCodes.Status400BadRequest,
                "Username list required",
                "Must provide explicit list of usernames to delete. Empty list not allowed."
            );
        }

        // Validate all usernames have test prefix
        var invalidUsernames = usernames.Where(u => !u.StartsWith(TestPrefix, StringComparison.Ordinal)).ToList();
        if (invalidUsernames.Count > 0)
        {
            return ProblemWithLog(
                StatusCodes.Status403Forbidden,
                "All usernames must have test prefix",
                $"Invalid usernames: {string.Join(", ", invalidUsernames)}"
            );
        }

        // Delete specified users
        var usersToDelete = userManager.Users
            .Where(u => u.UserName != null && usernames.Contains(u.UserName))
            .ToList();

        foreach (var user in usersToDelete)
        {
            await userManager.DeleteAsync(user);
        }

        LogOkCount(usersToDelete.Count);
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
        LogStartingUsername(username);

        // Validate username has test prefix
        if (!username.StartsWith(TestPrefix, StringComparison.Ordinal))
        {
            return ProblemWithLog(
                StatusCodes.Status403Forbidden,
                "Username must have test prefix",
                $"Username '{username}' must start with {TestPrefix} for test safety"
            );
        }

        // Validate workspace name has test prefix
        if (!request.Name.StartsWith(TestPrefix, StringComparison.Ordinal))
        {
            return ProblemWithLog(
                StatusCodes.Status403Forbidden,
                "Workspace name must have test prefix",
                $"Workspace name '{request.Name}' must start with {TestPrefix} for test safety"
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
        if (!username.StartsWith(TestPrefix, StringComparison.Ordinal))
        {
            return ProblemWithLog(
                StatusCodes.Status403Forbidden,
                "Username must have test prefix",
                $"Username '{username}' must start with {TestPrefix} for test safety"
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

        if (!tenant.Name.StartsWith(TestPrefix, StringComparison.Ordinal))
        {
            return ProblemWithLog(
                StatusCodes.Status403Forbidden,
                "Workspace is not a test workspace",
                $"Workspace '{tenant.Name}' must start with {TestPrefix} for test safety"
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
    /// <param name="tenantKey">The unique key of the workspace.</param>
    /// <param name="request">The transaction seeding details.</param>
    /// <param name="transactionsFeature">Feature providing transaction operations.</param>
    /// <returns>The collection of created transactions.</returns>
    /// <remarks>
    /// Validates that user has access to the workspace and both user and workspace have __TEST__ prefix.
    /// Uses anonymous tenant access policy to allow unauthenticated seeding of test data.
    /// The authorization handler sets the tenant context, and TenantContextMiddleware applies it
    /// before TransactionsFeature is injected via DI.
    /// Returns 403 if either username or workspace name lacks the prefix.
    /// </remarks>
    [HttpPost("users/{username}/workspaces/{tenantKey:guid}/transactions/seed")]
    [Authorize("AllowAnonymousTenantAccess")]
    [ProducesResponseType(typeof(IReadOnlyCollection<TransactionResultDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SeedTransactions(
        string username,
        Guid tenantKey,
        [FromBody] TransactionSeedRequest request,
        [FromServices] TransactionsFeature transactionsFeature)
    {
        LogStartingCount(request.Count);

        // Validate user and workspace access
        var validationResult = await ValidateUserWorkspaceAccessAsync(username, tenantKey);
        if (validationResult != null)
        {
            return validationResult;
        }

        // Generate transactions from the request
        var random = new Random();
        var baseDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var transactions = new List<TransactionEditDto>();

        for (int i = 1; i <= request.Count; i++)
        {
            transactions.Add(new TransactionEditDto(
                Date: baseDate.AddDays(random.Next(0, 30)),
                Amount: Math.Round((decimal)(random.NextDouble() * 490 + 10), 2),
                Payee: $"{request.PayeePrefix} {i}",
                Memo: request.Memo,
                Source: request.Source,
                ExternalId: request.ExternalId,
                Category: request.Category
            ));
        }

        // Create transactions using shared logic
        var createdTransactions = await CreateTransactionsAsync(transactions, transactionsFeature);

        LogOkCount(createdTransactions.Count);
        return CreatedAtAction(nameof(SeedTransactions), new { username, tenantKey }, createdTransactions);
    }

    /// <summary>
    /// Seed precise test transactions in a workspace for a user.
    /// </summary>
    /// <param name="username">The username (must include __TEST__ prefix) of the user.</param>
    /// <param name="tenantKey">The unique key of the workspace.</param>
    /// <param name="transactions">The collection of transactions to create.</param>
    /// <param name="transactionsFeature">Feature providing transaction operations.</param>
    /// <returns>The collection of created transactions.</returns>
    /// <remarks>
    /// Validates that user has access to the workspace and both user and workspace have __TEST__ prefix.
    /// Uses anonymous tenant access policy to allow unauthenticated seeding of test data.
    /// The authorization handler sets the tenant context, and TenantContextMiddleware applies it
    /// before TransactionsFeature is injected via DI.
    /// Returns 403 if either username or workspace name lacks the prefix.
    /// Unlike SeedTransactions which generates random transaction data, this endpoint accepts
    /// precise transaction details provided by the caller.
    /// </remarks>
    [HttpPost("users/{username}/workspaces/{tenantKey:guid}/transactions/seed-precise")]
    [Authorize("AllowAnonymousTenantAccess")]
    [ProducesResponseType(typeof(IReadOnlyCollection<TransactionResultDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SeedTransactionsPrecise(
        string username,
        Guid tenantKey,
        [FromBody] IReadOnlyCollection<TransactionEditDto> transactions,
        [FromServices] TransactionsFeature transactionsFeature)
    {
        LogStartingCount(transactions.Count);

        // Validate user and workspace access
        var validationResult = await ValidateUserWorkspaceAccessAsync(username, tenantKey);
        if (validationResult != null)
        {
            return validationResult;
        }

        // Create transactions using shared logic
        var createdTransactions = await CreateTransactionsAsync(transactions, transactionsFeature);

        LogOkCount(createdTransactions.Count);
        return CreatedAtAction(nameof(SeedTransactionsPrecise), new { username, tenantKey }, createdTransactions);
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
        var testTenants = await tenantFeature.GetTenantsByNamePrefixAsync(TestPrefix);
        var tenantKeys = testTenants.Select(t => t.Key).ToList();
        if (tenantKeys.Count > 0)
        {
            await tenantFeature.DeleteTenantsByKeysAsync(tenantKeys);
        }

        // Delete all test users (reuse existing functionality)
        var testUsers = userManager.Users
            .Where(u => u.UserName != null && u.UserName.Contains(TestPrefix))
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
        if (!username.StartsWith(TestPrefix, StringComparison.Ordinal))
        {
            return ProblemWithLog(
                StatusCodes.Status403Forbidden,
                "Username must have test prefix",
                $"Username '{username}' must start with {TestPrefix} for test safety"
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
        var invalidWorkspaces = workspaces.Where(w => !w.Name.StartsWith(TestPrefix, StringComparison.Ordinal)).ToList();
        if (invalidWorkspaces.Count > 0)
        {
            return ProblemWithLog(
                StatusCodes.Status403Forbidden,
                "Invalid workspace names",
                $"All workspace names must start with {TestPrefix}. Invalid: {string.Join(", ", invalidWorkspaces.Select(w => w.Name))}"
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

            // Create tenant without any role assignments (administrative creation)
            var created = await tenantFeature.CreateTenantAsync(tenantDto);

            // Get the created tenant to obtain its ID for role assignment
            var tenant = await tenantFeature.GetTenantByKeyAsync(created.Key);

            // Assign the requested role to the user
            await tenantFeature.AddUserTenantRoleAsync(userId, tenant!.Id, role);

            results.Add(new WorkspaceSetupResult(created.Key, created.Name, workspace.Role));
        }

        LogOkCount(results.Count);
        return CreatedAtAction(nameof(BulkWorkspaceSetup), new { username }, results);
    }

    /// <summary>
    /// Delete multiple test workspaces by their keys.
    /// </summary>
    /// <param name="workspaceKeys">Collection of workspace keys to delete. Must not be empty.</param>
    /// <returns>204 No Content on success.</returns>
    /// <remarks>
    /// Validates that all workspaces have __TEST__ prefix for safety.
    /// Cascade deletes will remove associated role assignments and transactions.
    /// Returns 403 if any workspace name lacks the prefix.
    /// Empty or null list returns 400 Bad Request (no "delete all" behavior).
    /// </remarks>
    [HttpDelete("workspaces")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteWorkspaces([FromBody] IReadOnlyCollection<Guid> workspaceKeys)
    {
        LogStartingCount(workspaceKeys?.Count ?? 0);

        // Require explicit workspace key list
        if (workspaceKeys == null || workspaceKeys.Count == 0)
        {
            return ProblemWithLog(
                StatusCodes.Status400BadRequest,
                "Workspace key list required",
                "Must provide explicit list of workspace keys to delete. Empty list not allowed."
            );
        }

        // Validate all workspaces have test prefix
        var invalidWorkspaces = new List<string>();
        foreach (var key in workspaceKeys)
        {
            var tenant = await tenantFeature.GetTenantByKeyAsync(key);
            if (tenant == null)
            {
                return ProblemWithLog(
                    StatusCodes.Status404NotFound,
                    "Workspace not found",
                    $"Workspace with key '{key}' not found"
                );
            }

            if (!tenant.Name.StartsWith(TestPrefix, StringComparison.Ordinal))
            {
                invalidWorkspaces.Add($"{tenant.Name} ({key})");
            }
        }

        if (invalidWorkspaces.Count > 0)
        {
            return ProblemWithLog(
                StatusCodes.Status403Forbidden,
                "All workspaces must be test workspaces",
                $"Invalid workspaces (must start with {TestPrefix}): {string.Join(", ", invalidWorkspaces)}"
            );
        }

        // Delete all workspaces (cascade will delete role assignments and transactions)
        await tenantFeature.DeleteTenantsByKeysAsync(workspaceKeys);

        LogOkCount(workspaceKeys.Count);
        return NoContent();
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
            case "400v": // ValidationException
                throw new Entities.Exceptions.ValidationException("This is a test 400 error from a ValidationException");
            case "500a": // ArgumentException
                throw new ArgumentException("This is a test 500 error from an ArgumentException", nameof(code));
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

    /// <summary>
    /// Get a paginated result of test strings.
    /// </summary>
    /// <param name="pageNumber">The page number (1-based). Defaults to 1.</param>
    /// <param name="pageSize">The number of items per page. Defaults to 10.</param>
    /// <returns>A paginated result containing test strings.</returns>
    /// <remarks>
    /// This endpoint is for testing pagination UI components with a simple string data type.
    /// Generates strings in format "Item N" where N is the sequential item number.
    /// </remarks>
    [HttpGet("pagination/strings")]
    [ProducesResponseType(typeof(PaginatedResultDto<string>), StatusCodes.Status200OK)]
    public IActionResult GetPaginatedStrings([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        LogStarting();

        // Validate pagination parameters
        if (pageNumber < 1)
        {
            pageNumber = 1;
        }

        if (pageSize < 1 || pageSize > 100)
        {
            pageSize = 10;
        }

        // Generate total of 50 test items
        const int totalCount = 50;

        // Calculate pagination metadata
        var metadata = PaginationHelper.Calculate(pageNumber, pageSize, totalCount);

        // Calculate skip and take
        var skip = (pageNumber - 1) * pageSize;
        var take = Math.Min(pageSize, totalCount - skip);

        // Generate items for the current page
        var items = Enumerable.Range(skip + 1, take)
            .Select(i => $"Item {i}")
            .ToList();

        var result = new PaginatedResultDto<string>(
            Items: items,
            Metadata: metadata
        );

        LogOk();
        return Ok(result);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Validates that a user has access to a workspace, checking test prefixes and permissions.
    /// </summary>
    /// <param name="username">The username to validate.</param>
    /// <param name="tenantKey">The workspace key to validate.</param>
    /// <returns>An IActionResult if validation fails, null if validation succeeds.</returns>
    private async Task<IActionResult?> ValidateUserWorkspaceAccessAsync(string username, Guid tenantKey)
    {
        // Validate username has test prefix
        if (!username.StartsWith(TestPrefix, StringComparison.Ordinal))
        {
            return ProblemWithLog(
                StatusCodes.Status403Forbidden,
                "Username must have test prefix",
                $"Username '{username}' must start with {TestPrefix} for test safety"
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

        if (!tenant.Name.StartsWith(TestPrefix, StringComparison.Ordinal))
        {
            return ProblemWithLog(
                StatusCodes.Status403Forbidden,
                "Workspace is not a test workspace",
                $"Workspace '{tenant.Name}' must start with {TestPrefix} for test safety"
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

        return null; // Validation passed
    }

    /// <summary>
    /// Creates transactions using the transactions feature and returns result DTOs.
    /// </summary>
    /// <param name="transactions">The collection of transactions to create.</param>
    /// <param name="transactionsFeature">Feature providing transaction operations.</param>
    /// <returns>A collection of created transaction result DTOs.</returns>
    /// <remarks>
    /// Tenant context must already be set by AnonymousTenantAccessHandler + TenantContextMiddleware
    /// before calling this method.
    /// </remarks>
    private static async Task<IReadOnlyCollection<TransactionResultDto>> CreateTransactionsAsync(
        IReadOnlyCollection<TransactionEditDto> transactions,
        TransactionsFeature transactionsFeature)
    {
        var createdTransactions = new List<TransactionResultDto>();

        foreach (var transaction in transactions)
        {
            var result = await transactionsFeature.AddTransactionAsync(transaction);
            createdTransactions.Add(new TransactionResultDto(
                result.Key,
                result.Date,
                result.Amount,
                result.Payee,
                result.Memo,
                result.Category
            ));
        }

        return createdTransactions;
    }

    /// <summary>
    /// Internal helper method to create test users with validation and error handling.
    /// </summary>
    /// <param name="usernames">Collection of usernames to create (__TEST__ prefix not required, will be added).</param>
    /// <returns>A collection of created user credentials.</returns>
    /// <exception cref="InvalidOperationException">Thrown when user creation fails or user cannot be retrieved.</exception>
    private async Task<IReadOnlyCollection<TestUserCredentials>> CreateUsersInternalAsync(IReadOnlyCollection<string> usernames)
    {
        // Choose a unique suffix for this run to avoid collisions
        var runSuffix = Guid.NewGuid().ToString("N")[..8];

        var credentials = new List<TestUserCredentials>();
        foreach (var username in usernames)
        {
            // Add __TEST__ prefix if not present
            var finalUsername = username.StartsWith(TestPrefix, StringComparison.Ordinal) ? username : TestPrefix + username;
            finalUsername += "_" + runSuffix;

            var email = $"{finalUsername}@test.com";

            // Generate a secure random password
            var password = GenerateSecurePassword(16);

            var identityUser = new IdentityUser
            {
                UserName = finalUsername,
                Email = email,
                EmailConfirmed = true // Auto-approve for test users
            };

            var result = await userManager.CreateAsync(identityUser, password);

            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                LogError($"Unable to create user {finalUsername}: {errors}");
                throw new InvalidOperationException($"Unable to create user {finalUsername}: {errors}");
            }

            // Get the created user to obtain the GUID
            var createdUser = await userManager.FindByNameAsync(finalUsername);
            if (createdUser?.Id == null)
            {
                LogError($"Unable to retrieve created user {finalUsername}");
                throw new InvalidOperationException($"Unable to retrieve created user {finalUsername}");
            }

            credentials.Add(new TestUserCredentials(
                Id: Guid.Parse(createdUser.Id),
                ShortName: username,
                Username: finalUsername,
                Email: email,
                Password: password
            ));
        }

        return credentials;
    }

    /// <summary>
    /// Generates a cryptographically secure random password.
    /// </summary>
    /// <param name="length">The desired password length (minimum 12).</param>
    /// <returns>A secure random password containing uppercase, lowercase, digits, and special characters.</returns>
    private static string GenerateSecurePassword(int length = 16)
    {
        if (length < 12)
        {
            length = 12;
        }

        const string uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string lowercase = "abcdefghijklmnopqrstuvwxyz";
        const string digits = "0123456789";
        const string special = "!@#$%^&*";
        const string allChars = uppercase + lowercase + digits + special;

        var password = new char[length];

        // Ensure at least one character from each category
        password[0] = uppercase[RandomNumberGenerator.GetInt32(uppercase.Length)];
        password[1] = lowercase[RandomNumberGenerator.GetInt32(lowercase.Length)];
        password[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        password[3] = special[RandomNumberGenerator.GetInt32(special.Length)];

        // Fill the rest with random characters from all categories
        for (int i = 4; i < length; i++)
        {
            password[i] = allChars[RandomNumberGenerator.GetInt32(allChars.Length)];
        }

        // Shuffle the password to avoid predictable pattern
        for (int i = length - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (password[i], password[j]) = (password[j], password[i]);
        }

        return new string(password);
    }

    /// <summary>
    /// Returns a ProblemDetails response with automatic logging.
    /// </summary>
    /// <param name="statusCode">HTTP status code (e.g., StatusCodes.Status404NotFound)</param>
    /// <param name="title">Brief error title</param>
    /// <param name="detail">Detailed error message (optional)</param>
    /// <param name="location">The name of the calling method (automatically populated)</param>
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
    private partial void LogOkUsername(/*[TestData]*/ string name, [CallerMemberName] string? location = null);

    [LoggerMessage(4, LogLevel.Information, "{Location}: OK {Count}")]
    private partial void LogOkCount(int count, [CallerMemberName] string? location = null);

    [LoggerMessage(5, LogLevel.Debug, "{Location}: Starting {Key}")]
    private partial void LogStartingKey(Guid key, [CallerMemberName] string? location = null);

    [LoggerMessage(6, LogLevel.Information, "{Location}: OK {Key}")]
    private partial void LogOkKey(Guid key, [CallerMemberName] string? location = null);

    [LoggerMessage(12, LogLevel.Debug, "{Location}: Starting {Username}")]
    private partial void LogStartingUsername(/*[TestData]*/ string username, [CallerMemberName] string? location = null);

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

    [LoggerMessage(13, LogLevel.Information, "{Location}: Test identified - TestName={TestName}, TestId={TestId}, TestClass={TestClass}")]
    private partial void LogIdentifyTest(/*[TestData]*/ string testName, /*[TestData]*/ string testId, /*[TestData]*/ string testClass, [CallerMemberName] string? location = null);

    [LoggerMessage(14, LogLevel.Warning, "{Location}: No current Activity found")]
    private partial void LogIdentifyNoActivity([CallerMemberName] string? location = null);

    #endregion
}
