namespace YoFi.V3.Entities.Tenancy.Exceptions;

/// <summary>
/// Exception thrown when a requested tenant cannot be found.
/// Returns HTTP 403 (not 404) to prevent tenant enumeration attacks.
/// </summary>
/// <remarks>
/// For security reasons, this exception returns 403 Forbidden instead of 404 Not Found.
/// This makes it indistinguishable from explicit access denial, preventing
/// attackers from enumerating valid tenant IDs by observing different status codes.
/// </remarks>
public class TenantNotFoundException : TenancyAccessDeniedException
{
    /// <summary>
    /// Gets the unique key of the tenant that was not found.
    /// </summary>
    public Guid TenantKey { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantNotFoundException"/> class.
    /// </summary>
    /// <param name="key">The unique identifier of the tenant that was not found.</param>
    public TenantNotFoundException(Guid key)
        : base($"Access to tenant '{key}' is denied.")
    {
        TenantKey = key;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantNotFoundException"/> class with a custom message.
    /// </summary>
    /// <param name="key">The unique identifier of the tenant that was not found.</param>
    /// <param name="message">The custom error message.</param>
    public TenantNotFoundException(Guid key, string message)
        : base(message)
    {
        TenantKey = key;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantNotFoundException"/> class with an inner exception.
    /// </summary>
    /// <param name="key">The unique identifier of the tenant that was not found.</param>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public TenantNotFoundException(Guid key, string message, Exception innerException)
        : base(message, innerException)
    {
        TenantKey = key;
    }
}
