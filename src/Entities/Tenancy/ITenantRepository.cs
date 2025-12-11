namespace YoFi.V3.Entities.Tenancy;

/// <summary>
/// Repository interface for managing tenant-related data operations.
/// Provides methods for tenant retrieval and user-tenant role assignment management.
/// </summary>
public interface ITenantRepository
{
    /// <summary>
    /// Adds a new tenant to the repository.
    /// </summary>
    /// <param name="tenant">The tenant entity to add.</param>
    Task<Tenant> AddTenantAsync(Tenant tenant);

    /// <summary>
    /// Updates an existing tenant in the repository.
    /// </summary>
    /// <param name="tenant">The tenant entity to update.</param>
    Task UpdateTenantAsync(Tenant tenant);

    /// <summary>
    /// Deletes a tenant from the repository.
    /// </summary>
    /// <param name="tenant">The tenant entity to delete.</param>
    Task DeleteTenantAsync(Tenant tenant);

    /// <summary>
    /// Retrieves all tenant role assignments for a specific user.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <returns>A collection of user-tenant role assignments.</returns>
    Task<ICollection<UserTenantRoleAssignment>> GetUserTenantRolesAsync(string userId);

    /// <summary>
    /// Retrieves a specific user-tenant role assignment.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <returns>The user-tenant role assignment if found; otherwise, null.</returns>
    Task<UserTenantRoleAssignment?> GetUserTenantRoleAsync(string userId, long tenantId);

    /// <summary>
    /// Adds a new user-tenant role assignment.
    /// </summary>
    /// <param name="assignment">The user-tenant role assignment to add.</param>
    /// <exception cref="DuplicateUserTenantRoleException">
    /// Thrown when a role assignment already exists for the specified user and tenant.
    /// </exception>
    Task AddUserTenantRoleAsync(UserTenantRoleAssignment assignment);

    /// <summary>
    /// Removes an existing user-tenant role assignment.
    /// </summary>
    /// <param name="assignment">The user-tenant role assignment to remove.</param>
    /// <exception cref="UserTenantRoleNotFoundException">
    /// Thrown when the specified user-tenant role assignment does not exist.
    /// </exception>
    Task RemoveUserTenantRoleAsync(UserTenantRoleAssignment assignment);

    /// <summary>
    /// Retrieves a tenant by its identifier.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <returns>The tenant if found; otherwise, null.</returns>
    Task<Tenant?> GetTenantAsync(long tenantId);

    /// <summary>
    /// Retrieves a tenant by its unique key.
    /// </summary>
    /// <param name="tenantKey">The tenant unique key (GUID).</param>
    /// <returns>The tenant if found; otherwise, null.</returns>
    Task<Tenant?> GetTenantByKeyAsync(Guid tenantKey);
}
