using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using YoFi.V3.Entities.Tenancy;

namespace YoFi.V3.Controllers.Tenancy;

/// <summary>
/// Manages tenant operations for the current authenticated user.
/// </summary>
/// <remarks>
/// This controller provides endpoints for users to view and manage their tenant memberships.
/// All operations are scoped to the currently authenticated user and require the [Authorize] attribute.
///
/// Routes:
/// - GET /api/tenant - Retrieve all tenants the user has access to
/// - GET /api/tenant/{tenantKey} - Retrieve a specific tenant (verifies user access)
/// - POST /api/tenant - Create a new tenant with the user as owner
/// - PUT /api/tenant/{tenantKey} - Update an existing tenant (requires Owner role)
/// - DELETE /api/tenant/{tenantKey} - Delete a tenant (requires Owner role)
///
/// Note: This controller operates at the user level (managing tenant memberships),
/// whereas tenant-scoped operations (like transactions) use routes like /api/tenant/{tenantKey}/resource.
/// </remarks>
[Authorize]
[Route("api/[controller]")]
[ApiController]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public partial class TenantController(TenantFeature tenantFeature, ILogger<TenantController> logger) : ControllerBase
{
    /// <summary>
    /// Get all tenants for current user
    /// </summary>
    [HttpGet()]
    [ProducesResponseType(typeof(ICollection<TenantRoleResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTenants()
    {
        LogStarting();

        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var tenants = await tenantFeature.GetTenantsForUserAsync(userId);

        LogOkCount(tenants.Count);
        return Ok(tenants);
    }

    /// <summary>
    /// Get a specific tenant for current user by tenant key
    /// </summary>
    /// <param name="key">The unique key of the tenant</param>
    /// <returns>The tenant with the user's role if they have access</returns>
    /// <remarks>
    /// Uses 'key' parameter instead of 'tenantKey' to avoid triggering tenant-scoped authorization middleware.
    /// Authorization is handled in the feature layer via GetTenantForUserAsync.
    /// </remarks>
    [HttpGet("{key:guid}")]
    [ProducesResponseType(typeof(TenantRoleResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTenant(Guid key)
    {
        LogStartingKey(key);

        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var tenant = await tenantFeature.GetTenantForUserAsync(userId, key);

        LogOkKey(key);
        return Ok(tenant);
    }

    /// <summary>
    /// Create a new tenant, with current user as owner
    /// </summary>
    /// <param name="tenantDto">The tenant data including name and description</param>
    /// <returns>The created tenant's information</returns>
    [HttpPost()]
    [ProducesResponseType(typeof(TenantResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateTenant([FromBody] TenantEditDto tenantDto)
    {
        LogStarting();

        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var tenantResult = await tenantFeature.CreateTenantForUserAsync(userId, tenantDto);

        LogOkKey(tenantResult.Key);
        return CreatedAtAction(nameof(GetTenant), new { key = tenantResult.Key }, tenantResult);
    }

    /// <summary>
    /// Update an existing tenant (requires Owner role)
    /// </summary>
    /// <param name="tenantKey">The unique key of the tenant to update</param>
    /// <param name="tenantDto">The updated tenant data including name and description</param>
    /// <returns>The updated tenant's information</returns>
    [HttpPut("{tenantKey:guid}")]
    [RequireTenantRole(TenantRole.Owner)]
    [ProducesResponseType(typeof(TenantResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTenant(Guid tenantKey, [FromBody] TenantEditDto tenantDto)
    {
        LogStartingKey(tenantKey);

        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var tenantResult = await tenantFeature.UpdateTenantForUserAsync(userId, tenantKey, tenantDto);

        LogOkKey(tenantKey);
        return Ok(tenantResult);
    }

    /// <summary>
    /// Delete a tenant (requires Owner role)
    /// </summary>
    /// <param name="tenantKey">The unique key of the tenant to delete</param>
    /// <returns>No content on successful deletion</returns>
    [HttpDelete("{tenantKey:guid}")]
    [RequireTenantRole(TenantRole.Owner)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTenant(Guid tenantKey)
    {
        LogStartingKey(tenantKey);

        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        await tenantFeature.DeleteTenantForUserAsync(userId, tenantKey);

        LogOkKey(tenantKey);
        return NoContent();
    }

    [LoggerMessage(0, LogLevel.Debug, "{Location}: Starting")]
    private partial void LogStarting([CallerMemberName] string? location = null);

    [LoggerMessage(1, LogLevel.Debug, "{Location}: Starting {Key}")]
    private partial void LogStartingKey(Guid key, [CallerMemberName] string? location = null);

    [LoggerMessage(2, LogLevel.Information, "{Location}: OK {Count}")]
    private partial void LogOkCount(int count, [CallerMemberName] string? location = null);

    [LoggerMessage(3, LogLevel.Information, "{Location}: OK {Key}")]
    private partial void LogOkKey(Guid key, [CallerMemberName] string? location = null);
}
