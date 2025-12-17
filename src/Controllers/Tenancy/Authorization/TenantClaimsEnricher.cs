using System.Security.Claims;
using YoFi.V3.Entities.Tenancy.Providers;

namespace YoFi.V3.Controllers.Tenancy.Authorization;

/// <summary>
/// Default implementation that adds tenant role claims to user authentication tokens.
/// </summary>
/// <param name="tenantRepository">Repository for tenant data operations.</param>
/// <remarks>
/// This enricher retrieves all tenant role assignments for a user and converts them
/// into claims with the format "tenant_role: tenantKey:role". These claims are then
/// used by authorization policies to enforce tenant-scoped access control.
/// </remarks>
public class TenantClaimsEnricher(ITenantRepository tenantRepository) : IClaimsEnricher
{
    /// <summary>
    /// Gets tenant role claims for the specified user ID.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <returns>Claims representing the user's tenant role assignments in the format "tenantKey:role".</returns>
    public async Task<IEnumerable<Claim>> GetClaimsAsync(string userId)
    {
        var userRoles = await tenantRepository.GetUserTenantRolesAsync(userId);

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
