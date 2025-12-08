
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using YoFi.V3.Entities.Providers;
using YoFi.V3.Entities.Tenancy;

namespace YoFi.V3.Controllers.Tenancy;

[Route("api/[controller]")]
[ApiController]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
// TODO: Using datacontext directly here for now. Need to refactor to use a Tenant Feature.
public partial class TenantController(IDataProvider dataContext, ILogger<TenantController> logger) : ControllerBase
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
        throw new NotImplementedException();
    }
}
