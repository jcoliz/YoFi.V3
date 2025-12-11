using Microsoft.AspNetCore.Authorization;
using YoFi.V3.Entities.Tenancy.Models;

namespace YoFi.V3.Controllers.Tenancy.Authorization;

/// <summary>
/// Authorization attribute that requires the user to have a minimum tenant role.
/// Applies tenant-based authorization to controllers and actions.
/// </summary>
/// <remarks>
/// This attribute maps to authorization policies in the format "TenantRole_{role}".
/// Users must have at least the specified role within the tenant context to access the protected resource.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public class RequireTenantRoleAttribute : AuthorizeAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RequireTenantRoleAttribute"/> class.
    /// </summary>
    /// <param name="minimumRole">The minimum tenant role required to access the protected resource.</param>
    public RequireTenantRoleAttribute(TenantRole minimumRole)
    {
        Policy = $"TenantRole_{minimumRole}"; // Maps to registered policy
    }
}
