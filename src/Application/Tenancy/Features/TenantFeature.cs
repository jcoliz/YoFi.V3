using YoFi.V3.Application.Tenancy.Dto;
using YoFi.V3.Entities.Tenancy.Exceptions;
using YoFi.V3.Entities.Tenancy.Models;
using YoFi.V3.Entities.Tenancy.Providers;

namespace YoFi.V3.Application.Tenancy.Features;

/// <summary>
/// Provides tenant management operations including tenant creation and user-tenant role retrieval.
/// </summary>
/// <param name="tenantRepository">The repository for tenant data operations.</param>
public class TenantFeature(ITenantRepository tenantRepository)
{
    /// <summary>
    /// Creates a new tenant and assigns the specified user as the owner.
    /// </summary>
    /// <param name="userId">The unique identifier of the user who will be the tenant owner.</param>
    /// <param name="tenantDto">The tenant data including name and description.</param>
    /// <returns>A <see cref="TenantResultDto"/> containing the created tenant's information.</returns>
    public async Task<TenantResultDto> CreateTenantForUserAsync(Guid userId, TenantEditDto tenantDto)
    {
        var tenant = new Tenant
        {
            Name = tenantDto.Name,
            Description = tenantDto.Description,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await tenantRepository.AddTenantAsync(tenant);

        await tenantRepository.AddUserTenantRoleAsync(new UserTenantRoleAssignment
        {
            UserId = userId.ToString(),
            TenantId = tenant.Id,
            Role = TenantRole.Owner
        });

        return new TenantResultDto(
            Key: tenant.Key,
            Name: tenant.Name,
            Description: tenant.Description,
            CreatedAt: tenant.CreatedAt
        );
    }

    /// <summary>
    /// Retrieves all tenants associated with the specified user, including their role in each tenant.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <returns>A collection of <see cref="TenantRoleResultDto"/> containing tenant information and the user's role.</returns>
    public async Task<IReadOnlyCollection<TenantRoleResultDto>> GetTenantsForUserAsync(Guid userId)
    {
        var roles = await tenantRepository.GetUserTenantRolesAsync(userId.ToString());

        return roles.Select(utr => new TenantRoleResultDto(
            Key: utr.Tenant!.Key,
            Name: utr.Tenant.Name,
            Description: utr.Tenant.Description,
            Role: utr.Role,
            CreatedAt: utr.Tenant.CreatedAt
        )).ToList();
    }

    /// <summary>
    /// Retrieves a specific tenant for a user by tenant key, including the user's role.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <param name="tenantKey">The unique key of the tenant to retrieve.</param>
    /// <returns>A <see cref="TenantRoleResultDto"/> containing tenant information and the user's role.</returns>
    /// <exception cref="TenantNotFoundException">Thrown when the tenant is not found.</exception>
    /// <exception cref="TenantAccessDeniedException">Thrown when the user doesn't have access to the tenant.</exception>
    public async Task<TenantRoleResultDto> GetTenantForUserAsync(Guid userId, Guid tenantKey)
    {
        // First, get the tenant by key to get its ID
        var tenant = await tenantRepository.GetTenantByKeyAsync(tenantKey);
        if (tenant == null)
        {
            throw new TenantNotFoundException(tenantKey);
        }

        // Then check if the user has access to this tenant
        var role = await tenantRepository.GetUserTenantRoleAsync(userId.ToString(), tenant.Id);
        if (role == null)
        {
            // Username will be resolved downstream by exception handler if needed
            throw new TenantAccessDeniedException(userId, string.Empty, tenantKey);
        }

        return new TenantRoleResultDto(
            Key: tenant.Key,
            Name: tenant.Name,
            Description: tenant.Description,
            Role: role.Role,
            CreatedAt: tenant.CreatedAt
        );
    }

    /// <summary>
    /// Updates an existing tenant for a user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user requesting the update.</param>
    /// <param name="tenantKey">The unique key of the tenant to update.</param>
    /// <param name="tenantDto">The updated tenant data including name and description.</param>
    /// <returns>A <see cref="TenantResultDto"/> containing the updated tenant's information.</returns>
    /// <exception cref="TenantNotFoundException">Thrown when the tenant is not found.</exception>
    /// <exception cref="TenantAccessDeniedException">Thrown when the user doesn't have access to the tenant.</exception>
    public async Task<TenantResultDto> UpdateTenantForUserAsync(Guid userId, Guid tenantKey, TenantEditDto tenantDto)
    {
        // First, get the tenant by key to get its ID
        var tenant = await tenantRepository.GetTenantByKeyAsync(tenantKey);
        if (tenant == null)
        {
            throw new TenantNotFoundException(tenantKey);
        }

        // Then check if the user has access to this tenant
        var role = await tenantRepository.GetUserTenantRoleAsync(userId.ToString(), tenant.Id);
        if (role == null)
        {
            // Username will be resolved downstream by exception handler if needed
            throw new TenantAccessDeniedException(userId, string.Empty, tenantKey);
        }

        // Update the tenant properties
        tenant.Name = tenantDto.Name;
        tenant.Description = tenantDto.Description;

        await tenantRepository.UpdateTenantAsync(tenant);

        return new TenantResultDto(
            Key: tenant.Key,
            Name: tenant.Name,
            Description: tenant.Description,
            CreatedAt: tenant.CreatedAt
        );
    }

    /// <summary>
    /// Deletes a tenant for a user.
    /// </summary>
    /// <param name="userId">The unique identifier of the user requesting the deletion.</param>
    /// <param name="tenantKey">The unique key of the tenant to delete.</param>
    /// <exception cref="TenantNotFoundException">Thrown when the tenant is not found.</exception>
    /// <exception cref="TenantAccessDeniedException">Thrown when the user doesn't have access to the tenant.</exception>
    public async Task DeleteTenantForUserAsync(Guid userId, Guid tenantKey)
    {
        // First, get the tenant by key to get its ID
        var tenant = await tenantRepository.GetTenantByKeyAsync(tenantKey);
        if (tenant == null)
        {
            throw new TenantNotFoundException(tenantKey);
        }

        // Then check if the user has access to this tenant
        var role = await tenantRepository.GetUserTenantRoleAsync(userId.ToString(), tenant.Id);
        if (role == null)
        {
            // Username will be resolved downstream by exception handler if needed
            throw new TenantAccessDeniedException(userId, string.Empty, tenantKey);
        }

        await tenantRepository.DeleteTenantAsync(tenant);
    }

    /// <summary>
    /// Retrieves a tenant by its unique key without user access checks.
    /// </summary>
    /// <param name="tenantKey">The unique key of the tenant to retrieve.</param>
    /// <returns>The tenant if found; otherwise, null.</returns>
    /// <remarks>
    /// This method bypasses user access validation and is intended for administrative
    /// or test control scenarios. For user-facing operations, use GetTenantForUserAsync instead.
    /// </remarks>
    public async Task<Tenant?> GetTenantByKeyAsync(Guid tenantKey)
    {
        return await tenantRepository.GetTenantByKeyAsync(tenantKey);
    }

    /// <summary>
    /// Assigns a role to a user for a specific tenant.
    /// </summary>
    /// <param name="userId">The unique identifier of the user to assign the role to.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="role">The role to assign to the user.</param>
    /// <exception cref="DuplicateUserTenantRoleException">Thrown when the user already has a role in the tenant.</exception>
    public async Task AddUserTenantRoleAsync(Guid userId, long tenantId, TenantRole role)
    {
        await tenantRepository.AddUserTenantRoleAsync(new UserTenantRoleAssignment
        {
            UserId = userId.ToString(),
            TenantId = tenantId,
            Role = role
        });
    }

    /// <summary>
    /// Retrieves all tenants whose names start with the specified prefix.
    /// </summary>
    /// <param name="namePrefix">The prefix to filter tenant names by.</param>
    /// <returns>A collection of tenants matching the prefix.</returns>
    /// <remarks>
    /// This method is useful for bulk operations on test data or administrative tasks
    /// where tenants are identified by naming conventions.
    /// </remarks>
    public async Task<IReadOnlyCollection<Tenant>> GetTenantsByNamePrefixAsync(string namePrefix)
    {
        var allRoles = await tenantRepository.GetUserTenantRolesAsync(string.Empty);
        var matchingTenants = allRoles
            .Where(r => r.Tenant != null && r.Tenant.Name.StartsWith(namePrefix, StringComparison.Ordinal))
            .Select(r => r.Tenant!)
            .DistinctBy(t => t.Id)
            .ToList();
        return matchingTenants;
    }

    /// <summary>
    /// Deletes multiple tenants by their unique keys.
    /// </summary>
    /// <param name="tenantKeys">The collection of tenant keys to delete.</param>
    /// <remarks>
    /// This method is intended for bulk cleanup operations such as removing test data.
    /// Each tenant is deleted individually without user access validation.
    /// </remarks>
    public async Task DeleteTenantsByKeysAsync(IReadOnlyCollection<Guid> tenantKeys)
    {
        foreach (var key in tenantKeys)
        {
            var tenant = await tenantRepository.GetTenantByKeyAsync(key);
            if (tenant != null)
            {
                await tenantRepository.DeleteTenantAsync(tenant);
            }
        }
    }

    /// <summary>
    /// Verifies if a user has any role assignment for a specific tenant.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <returns>True if the user has a role in the tenant; otherwise, false.</returns>
    public async Task<bool> HasUserTenantRoleAsync(Guid userId, long tenantId)
    {
        var role = await tenantRepository.GetUserTenantRoleAsync(userId.ToString(), tenantId);
        return role != null;
    }
}
