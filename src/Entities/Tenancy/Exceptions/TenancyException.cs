namespace YoFi.V3.Entities.Tenancy.Exceptions;

/// <summary>
/// Base exception for all tenancy-related errors.
/// Provides a common base for exception handling.
/// </summary>
public abstract class TenancyException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TenancyException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    protected TenancyException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenancyException"/> class with an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    protected TenancyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
