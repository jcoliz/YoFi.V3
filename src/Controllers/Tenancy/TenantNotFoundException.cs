using YoFi.V3.Entities.Exceptions;

namespace YoFi.V3.Controllers.Tenancy;

/// <summary>
/// Exception thrown when a requested tenant cannot be found.
/// </summary>
public class TenantNotFoundException : ResourceNotFoundException
{
    /// <inheritdoc/>
    public override string ResourceType => "Tenant";

    /// <summary>
    /// Gets the unique key of the tenant that was not found.
    /// </summary>
    public Guid TenantKey => ResourceKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantNotFoundException"/> class.
    /// </summary>
    /// <param name="key">The unique identifier of the tenant that was not found.</param>
    public TenantNotFoundException(Guid key)
        : base(key)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantNotFoundException"/> class with a custom message.
    /// </summary>
    /// <param name="key">The unique identifier of the tenant that was not found.</param>
    /// <param name="message">The custom error message.</param>
    public TenantNotFoundException(Guid key, string message)
        : base(key, message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantNotFoundException"/> class with a custom message and inner exception.
    /// </summary>
    /// <param name="key">The unique identifier of the tenant that was not found.</param>
    /// <param name="message">The custom error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public TenantNotFoundException(Guid key, string message, Exception innerException)
        : base(key, message, innerException)
    {
    }
}
