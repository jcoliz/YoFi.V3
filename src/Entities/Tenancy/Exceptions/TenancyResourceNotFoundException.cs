namespace YoFi.V3.Entities.Tenancy.Exceptions;

/// <summary>
/// Base exception for when a tenancy-related resource cannot be found.
/// </summary>
public abstract class TenancyResourceNotFoundException : TenancyException
{
    /// <summary>
    /// Gets the type of resource that was not found (e.g., "Tenant", "UserTenantRole").
    /// </summary>
    public abstract string ResourceType { get; }

    /// <summary>
    /// Gets the unique key of the resource that was not found (if applicable).
    /// </summary>
    public Guid? ResourceKey { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenancyResourceNotFoundException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="resourceKey">The unique identifier of the resource that was not found.</param>
    protected TenancyResourceNotFoundException(string message, Guid? resourceKey = null)
        : base(message)
    {
        ResourceKey = resourceKey;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenancyResourceNotFoundException"/> class with an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="resourceKey">The unique identifier of the resource that was not found.</param>
    /// <param name="innerException">The inner exception.</param>
    protected TenancyResourceNotFoundException(string message, Guid? resourceKey, Exception innerException)
        : base(message, innerException)
    {
        ResourceKey = resourceKey;
    }
}
