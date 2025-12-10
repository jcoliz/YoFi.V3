namespace YoFi.V3.Entities.Tenancy.Exceptions;

/// <summary>
/// Exception thrown when attempting to add a user-tenant role assignment that already exists.
/// A user can only have one role per tenant due to unique constraint.
/// </summary>
public class DuplicateUserTenantRoleException : TenancyException
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
    /// Initializes a new instance of the <see cref="DuplicateUserTenantRoleException"/> class.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    public DuplicateUserTenantRoleException(string userId, long tenantId)
        : base($"User '{userId}' already has a role assignment for tenant '{tenantId}'.")
    {
        UserId = userId;
        TenantId = tenantId;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DuplicateUserTenantRoleException"/> class with an inner exception.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="innerException">The inner exception.</param>
    public DuplicateUserTenantRoleException(string userId, long tenantId, Exception innerException)
        : base($"User '{userId}' already has a role assignment for tenant '{tenantId}'.", innerException)
    {
        UserId = userId;
        TenantId = tenantId;
    }
}
