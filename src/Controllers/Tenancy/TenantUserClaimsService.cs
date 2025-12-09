using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using NuxtIdentity.Core.Abstractions;
using YoFi.V3.Entities.Providers;
using YoFi.V3.Entities.Tenancy;

namespace YoFi.V3.Controllers.Tenancy;

public class TenantUserClaimsService<TUser>(IDataProvider dataProvider)
    : IUserClaimsProvider<TUser> where TUser : IdentityUser
{
    /// <summary>
    /// Get all user tenant roles for a specific user, as a list of Claim objects.
    /// </summary>
    /// <param name="user">Which user</param>
    /// <returns>Claims which represent the user's tenant role assignments</returns>
    /// <exception cref="NotImplementedException"></exception>
    public Task<IEnumerable<Claim>> GetClaimsAsync(TUser user)
    {
        // Get the user's tenant roles from the data provider
        // FIX: This is broken architecturally! UserTenantRoleAssignment does not implement IModel,
        // so IDataProvider can't get it.
        //
        // Moreover, IDataProvider is application specific. I want to write tenancy in a way that
        // is not tied to a specific application, so I can resuse it in future applications.
        // So how will I give it access to the database?

        //var tenantClaims = dataProvider.Get<UserTenantRoleAssignment>();

        throw new NotImplementedException();
    }
}
