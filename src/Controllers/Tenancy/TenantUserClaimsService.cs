using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using NuxtIdentity.Core.Abstractions;
using YoFi.V3.Entities.Tenancy;

namespace YoFi.V3.Controllers.Tenancy;

public class TenantUserClaimsService<TUser>(ITenantRepository tenantRepository)
    : IUserClaimsProvider<TUser> where TUser : IdentityUser
{
    /// <summary>
    /// Get all user tenant roles for a specific user, as a list of Claim objects.
    /// </summary>
    /// <param name="user">Which user</param>
    /// <returns>Claims which represent the user's tenant role assignments</returns>
    /// <exception cref="NotImplementedException"></exception>
    public async Task<IEnumerable<Claim>> GetClaimsAsync(TUser user)
    {
        var userRoles = await tenantRepository.GetUserTenantRolesAsync(user.Id);

        // Convert user tenant roles to claims
        var claims = userRoles.Select(ur => new Claim(
            type: "tenant_role",
            value: $"{ur.TenantId}:{ur.Role}"
        ));

        return claims;
    }
}
