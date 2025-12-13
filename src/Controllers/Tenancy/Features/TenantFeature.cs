using YoFi.V3.Controllers.Tenancy.Api.Dto;
using YoFi.V3.Entities.Tenancy.Exceptions;
using YoFi.V3.Entities.Tenancy.Models;
using YoFi.V3.Entities.Tenancy.Providers;

namespace YoFi.V3.Controllers.Tenancy.Features;

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
            throw new TenantAccessDeniedException(userId, tenantKey);
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
            throw new TenantAccessDeniedException(userId, tenantKey);
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
            throw new TenantAccessDeniedException(userId, tenantKey);
        }

        await tenantRepository.DeleteTenantAsync(tenant);
    }
}
