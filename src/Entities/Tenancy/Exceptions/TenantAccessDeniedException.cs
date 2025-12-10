namespace YoFi.V3.Entities.Tenancy.Exceptions;

/// <summary>
/// Exception thrown when a user attempts to access a tenant they don't have permissions for.
/// This is distinct from TenantNotFoundException - the tenant exists, but the user lacks access.
/// However, both return the same HTTP 403 status code for security.
/// </summary>
public class TenantAccessDeniedException : TenancyAccessDeniedException
{
    /// <summary>
    /// Gets the user identifier.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// Gets the tenant key that access was denied for.
    /// </summary>
    public Guid TenantKey { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantAccessDeniedException"/> class.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="tenantKey">The tenant key that access was denied for.</param>
    public TenantAccessDeniedException(Guid userId, Guid tenantKey)
        : base($"User '{userId}' does not have access to tenant '{tenantKey}'.")
    {
        UserId = userId;
        TenantKey = tenantKey;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantAccessDeniedException"/> class with a custom message.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="tenantKey">The tenant key that access was denied for.</param>
    /// <param name="message">The custom error message.</param>
    public TenantAccessDeniedException(Guid userId, Guid tenantKey, string message)
        : base(message)
    {
        UserId = userId;
        TenantKey = tenantKey;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantAccessDeniedException"/> class with an inner exception.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="tenantKey">The tenant key that access was denied for.</param>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public TenantAccessDeniedException(Guid userId, Guid tenantKey, string message, Exception innerException)
        : base(message, innerException)
    {
        UserId = userId;
        TenantKey = tenantKey;
    }
}
