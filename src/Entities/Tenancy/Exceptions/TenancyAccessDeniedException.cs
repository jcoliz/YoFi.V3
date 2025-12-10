namespace YoFi.V3.Entities.Tenancy.Exceptions;

/// <summary>
/// Base exception for when access to a tenancy-related resource is denied.
/// </summary>
public abstract class TenancyAccessDeniedException : TenancyException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TenancyAccessDeniedException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    protected TenancyAccessDeniedException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenancyAccessDeniedException"/> class with an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    protected TenancyAccessDeniedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
