using Microsoft.AspNetCore.Identity;
using NuxtIdentity.Core.Abstractions;
using YoFi.V3.Entities.Tenancy.Models;
using YoFi.V3.Entities.Tenancy.Providers;

namespace YoFi.V3.Controllers.Tenancy.Seeding;

/// <summary>
/// Seed stage for creating tenants and assigning user roles to those new tenants
/// </summary>
/// <param name="tenantRepository"></param>
public class TenantSeedStage(ITenantRepository tenantRepository) : ISeedStage
{
    string ISeedStage.Name => "Tenants";
    int ISeedStage.Order => 0;

    async Task ISeedStage.SeedAsync(ISeedGroupContext group, CancellationToken cancellationToken)
    {
        // Note that we do these one at a time so we can report errors on individual items
        foreach (var item in group.Items<TenantSeedOptions>("Tenants"))
        {
            await group.RunAsync(item, "tenant", item.Value.Name ?? "(none)", async () =>
            {
                if (item.Value.Key == Guid.Empty)
                    return SeedResult.Failure("Tenant key must be non-empty. Please check this placeholder value.");

                if (string.IsNullOrWhiteSpace(item.Value.Name))
                    return SeedResult.Failure("Tenant name must be non-empty. Please check this placeholder value.");

                var tenant = await tenantRepository.GetTenantByKeyAsync(item.Value.Key);

                if (tenant != null)
                    return SeedResult.AlreadyExists("Tenant already exists.");

                var newTenant = new Tenant
                {
                    Key = item.Value.Key,
                    Name = item.Value.Name,
                    Description = item.Value.Description,
                };

                await tenantRepository.AddTenantAsync(newTenant);

                return SeedResult.Success();
            });
        }
    }
}

/// <summary>
/// Seed stage for assigning user roles to tenants.
/// </summary>
/// <param name="tenantRepository"></param>
/// <param name="userManager"></param>
public class TenantRoleSeedStage(ITenantRepository tenantRepository, UserManager<IdentityUser> userManager) : ISeedStage
{
    string ISeedStage.Name => "TenantRoles";
    int ISeedStage.Order => 1;

    async Task ISeedStage.SeedAsync(ISeedGroupContext group, CancellationToken cancellationToken)
    {
        foreach (var item in group.Items<TenantRoleSeedOptions>("TenantRoles"))
        {
            var username = item.Value.UserName ?? "(none)";
            await group.RunAsync(item, "tenantRole", $"{username} -> {item.Value.Role}", async () =>
            {
                if (item.Value.Key == Guid.Empty)
                    return SeedResult.Failure("Tenant key must be non-empty. Please check this placeholder value.");

                if (string.IsNullOrWhiteSpace(item.Value.UserName))
                    return SeedResult.Failure("User name must be non-empty. Please check this placeholder value.");

                var tenant = await tenantRepository.GetTenantByKeyAsync(item.Value.Key);

                if (tenant == null)
                    return SeedResult.Failure("Tenant not found.");

                var user = await userManager.FindByNameAsync(item.Value.UserName);

                if (user == null)
                    return SeedResult.Failure($"User '{item.Value.UserName}' not found.");

                var existingAssignment = await tenantRepository.GetUserTenantRoleAsync(user.Id, tenant.Id);
                if (existingAssignment != null)
                    return SeedResult.AlreadyExists($"User already has '{existingAssignment.Role}' role for this tenant. *NOTE* User role on a tenant cannot be changed with seeding, can only be newly created");

                // If we were even more precise, we could throw an error if user already has a different role for this tenant, since changing roles is not allowed via seeding.

                await tenantRepository.AddUserTenantRoleAsync(new UserTenantRoleAssignment
                {
                    UserId = user.Id,
                    TenantId = tenant.Id,
                    Role = item.Value.Role
                });

                return SeedResult.Success();
            });
        }
    }
}
