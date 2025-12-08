using Microsoft.AspNetCore.Authorization;
using YoFi.V3.Entities.Tenancy;

namespace YoFi.V3.Controllers.Tenancy;

public class TenantRoleRequirement : IAuthorizationRequirement
{
    public TenantRole MinimumRole { get; }
    public TenantRoleRequirement(TenantRole minimumRole) => MinimumRole = minimumRole;
}
