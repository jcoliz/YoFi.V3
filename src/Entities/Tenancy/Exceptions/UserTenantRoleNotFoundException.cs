namespace YoFi.V3.Entities.Tenancy.Exceptions;

/// <summary>
/// Exception thrown when attempting to access a user-tenant role assignment that doesn't exist.
/// </summary>
public class UserTenantRoleNotFoundException : TenancyResourceNotFoundException
{
    /// <inheritdoc/>
    public override string ResourceType => "UserTenantRole";

    /// <summary>
    /// Gets the user identifier.
    /// </summary>
    public string UserId { get; }

    /// <summary>
    /// Gets the user name.
    /// </summary>
    public string UserName { get; }

    /// <summary>
    /// Gets the tenant key.
    /// </summary>
    public Guid TenantKey { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UserTenantRoleNotFoundException"/> class.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="userName">The user name.</param>
    /// <param name="tenantKey">The tenant key.</param>
    public UserTenantRoleNotFoundException(string userId, string userName, Guid tenantKey)
        : base($"User '{userName}' does not have a role assignment for tenant '{tenantKey}'.")
    {
        UserId = userId;
        UserName = userName;
        TenantKey = tenantKey;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UserTenantRoleNotFoundException"/> class with an inner exception.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="userName">The user name.</param>
    /// <param name="tenantKey">The tenant key.</param>
    /// <param name="innerException">The inner exception.</param>
    public UserTenantRoleNotFoundException(string userId, string userName, Guid tenantKey, Exception innerException)
        : base($"User '{userName}' does not have a role assignment for tenant '{tenantKey}'.", null, innerException)
    {
        UserId = userId;
        UserName = userName;
        TenantKey = tenantKey;
    }
}
