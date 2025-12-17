using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using NuxtIdentity.Core.Abstractions;
using YoFi.V3.Controllers.Tenancy.Authorization;

namespace YoFi.V3.BackEnd.Setup;

/// <summary>
/// Adapter that integrates the tenant claims enricher with NuxtIdentity's claims provider system.
/// </summary>
/// <typeparam name="TUser">The type of user identity.</typeparam>
/// <param name="claimsEnricher">The claims enricher that provides tenant-specific claims.</param>
/// <remarks>
/// This adapter bridges the gap between the framework-agnostic <see cref="IClaimsEnricher"/>
/// abstraction and NuxtIdentity's <see cref="IUserClaimsProvider{TUser}"/> interface.
/// This is application-specific integration code that connects the generic tenancy library
/// with the NuxtIdentity authentication framework used in this application.
/// </remarks>
public class NuxtIdentityTenantClaimsAdapter<TUser>(IClaimsEnricher claimsEnricher)
    : IUserClaimsProvider<TUser> where TUser : IdentityUser
{
    /// <summary>
    /// Get all user tenant roles for a specific user, as a list of Claim objects.
    /// </summary>
    /// <param name="user">The user to get claims for.</param>
    /// <returns>Claims representing the user's tenant role assignments in the format "tenantKey:role".</returns>
    public async Task<IEnumerable<Claim>> GetClaimsAsync(TUser user)
        => await claimsEnricher.GetClaimsAsync(user.Id);
}
