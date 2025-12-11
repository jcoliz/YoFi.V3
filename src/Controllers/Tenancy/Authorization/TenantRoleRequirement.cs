using Microsoft.AspNetCore.Authorization;
using YoFi.V3.Entities.Tenancy.Models;

namespace YoFi.V3.Controllers.Tenancy.Authorization;

/// <summary>
/// Authorization requirement that specifies a minimum tenant role needed for access.
/// </summary>
/// <remarks>
/// This requirement is used in conjunction with <see cref="TenantRoleHandler"/> to enforce
/// role-based access control within a tenant context.
/// </remarks>
public class TenantRoleRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// Gets the minimum role required to satisfy this authorization requirement.
    /// </summary>
    public TenantRole MinimumRole { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantRoleRequirement"/> class.
    /// </summary>
    /// <param name="minimumRole">The minimum tenant role required.</param>
    public TenantRoleRequirement(TenantRole minimumRole) => MinimumRole = minimumRole;
}
