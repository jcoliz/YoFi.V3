namespace YoFi.V3.Entities.Tenancy.Exceptions;

/// <summary>
/// Exception thrown when attempting to access the current tenant but the tenant context has not been set.
/// This typically indicates that the tenant middleware has not run or has failed to resolve a tenant.
/// This is a code error (500) rather than a client error.
/// </summary>
public class TenantContextNotSetException : TenancyException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TenantContextNotSetException"/> class.
    /// </summary>
    public TenantContextNotSetException()
        : base("Current tenant is not set. The tenant middleware may not have run or failed to resolve a tenant.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantContextNotSetException"/> class with a custom message.
    /// </summary>
    /// <param name="message">The custom error message.</param>
    public TenantContextNotSetException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantContextNotSetException"/> class with an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public TenantContextNotSetException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
