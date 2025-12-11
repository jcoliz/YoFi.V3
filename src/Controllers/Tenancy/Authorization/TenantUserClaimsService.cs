using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using NuxtIdentity.Core.Abstractions;
using YoFi.V3.Entities.Tenancy.Providers;

namespace YoFi.V3.Controllers.Tenancy.Authorization;

/// <summary>
/// Service that provides tenant role claims for user authentication tokens.
/// </summary>
/// <typeparam name="TUser">The type of user identity.</typeparam>
/// <param name="tenantRepository">Repository for tenant data operations.</param>
/// <remarks>
/// This service is registered with the NuxtIdentity framework to add custom claims
/// to JWT tokens when users authenticate. Each tenant role assignment becomes a claim
/// that can be used for authorization decisions.
/// </remarks>
public class TenantUserClaimsService<TUser>(ITenantRepository tenantRepository)
    : IUserClaimsProvider<TUser> where TUser : IdentityUser
{
    /// <summary>
    /// Get all user tenant roles for a specific user, as a list of Claim objects.
    /// </summary>
    /// <param name="user">The user to get claims for.</param>
    /// <returns>Claims representing the user's tenant role assignments in the format "tenantKey:role".</returns>
    public async Task<IEnumerable<Claim>> GetClaimsAsync(TUser user)
    {
        var userRoles = await tenantRepository.GetUserTenantRolesAsync(user.Id);

        // Convert user tenant roles to claims
        var claims = userRoles
            .Where(ur => ur.Tenant != null)
            .Select(ur => new Claim(
                type: "tenant_role",
                value: $"{ur.Tenant?.Key ?? Guid.Empty}:{ur.Role}"
            ));

        return claims;
    }
}
