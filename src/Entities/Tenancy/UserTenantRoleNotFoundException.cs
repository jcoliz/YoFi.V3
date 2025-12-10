namespace YoFi.V3.Entities.Tenancy;

/// <summary>
/// Exception thrown when attempting to remove a user-tenant role assignment that doesn't exist.
/// </summary>
public class UserTenantRoleNotFoundException : Exception
{
    /// <summary>
    /// Gets the user identifier.
    /// </summary>
    public string UserId { get; }

    /// <summary>
    /// Gets the tenant identifier.
    /// </summary>
    public long TenantId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UserTenantRoleNotFoundException"/> class.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    public UserTenantRoleNotFoundException(string userId, long tenantId)
        : base($"User '{userId}' does not have a role assignment for tenant '{tenantId}'.")
    {
        UserId = userId;
        TenantId = tenantId;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UserTenantRoleNotFoundException"/> class with an inner exception.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="innerException">The inner exception.</param>
    public UserTenantRoleNotFoundException(string userId, long tenantId, Exception innerException)
        : base($"User '{userId}' does not have a role assignment for tenant '{tenantId}'.", innerException)
    {
        UserId = userId;
        TenantId = tenantId;
    }
}
