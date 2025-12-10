
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
/// - GET /api/tenant/{key} - Retrieve a specific tenant (verifies user access)
/// - POST /api/tenant - Create a new tenant with the user as owner
///
/// Note: This controller operates at the user level (managing tenant memberships),
/// whereas tenant-scoped operations (like transactions) use routes like /api/tenant/{tenantKey}/resource.
/// </remarks>
[Authorize]
[Route("api/[controller]")]
[ApiController]
[Produces("application/json")]
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
        var userId = Guid.Parse(User.Identity!.Name!);
        var tenants = await tenantFeature.GetTenantsForUserAsync(userId);
        return Ok(tenants);
    }

    /// <summary>
    /// Get a specific tenant for current user by tenant key
    /// </summary>
    /// <param name="key">The unique key of the tenant</param>
    /// <returns>The tenant with the user's role if they have access</returns>
    [HttpGet("{key:guid}")]
    [ProducesResponseType(typeof(TenantRoleResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTenant(Guid key)
    {
        var userId = Guid.Parse(User.Identity!.Name!);
        var tenant = await tenantFeature.GetTenantForUserAsync(userId, key);
        return Ok(tenant);
    }

    /// <summary>
    /// Create a new tenant, with current user as owner
    /// </summary>
    /// <param name="tenantDto">The tenant data including name and description</param>
    /// <returns>The created tenant's information</returns>
    [HttpPost()]
    [ProducesResponseType(typeof(TenantResultDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateTenant([FromBody] TenantEditDto tenantDto)
    {
        var userId = Guid.Parse(User.Identity!.Name!);
        var tenantResult = await tenantFeature.CreateTenantForUserAsync(userId, tenantDto);
        return CreatedAtAction(nameof(GetTenant), new { key = tenantResult.Key }, tenantResult);
    }
}
