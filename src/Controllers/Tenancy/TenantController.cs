
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using YoFi.V3.Entities.Tenancy;

namespace YoFi.V3.Controllers.Tenancy;

[Route("api/[controller]")]
[ApiController]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
// TODO: Need to refactor to use a Tenant Feature.
public partial class TenantController(ITenantRepository tenantRepository, ILogger<TenantController> logger) : ControllerBase
{
    /// <summary>
    /// Get all tenants for current user
    /// </summary>
    /// <remarks>
    /// TODO: Use a DTO (we don't return ID)
    /// </remarks>
    /// <returns></returns>
    [HttpGet()]
    [ProducesResponseType(typeof(ICollection<Tenant>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTenants()
    {
        // TODO: Need to get current user ID from claims/context
        // Then call tenantRepository.GetUserTenantRolesAsync(userId)
        // And return the tenants from those roles
        throw new NotImplementedException();
    }

    /// <summary>
    /// Create a new tenant, with current user as owner
    /// </summary>
    /// <remarks>
    /// TODO: Use an Edit DTO for input, and a Result DTO for output
    /// </remarks>
    /// <returns></returns>
    [HttpPost()]
    [ProducesResponseType(typeof(Tenant), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateTenant([FromBody] Tenant tenant)
    {
        // TODO: Need to:
        // 1. Create the tenant (ITenantRepository needs AddTenantAsync method)
        // 2. Assign current user as owner (using tenantRepository.AddUserTenantRoleAsync)
        throw new NotImplementedException();
    }
}
