namespace YoFi.V3.Entities.Exceptions;

/// <summary>
/// Exception thrown when a requested transaction cannot be found.
/// </summary>
public class TransactionNotFoundException : Exception
{
    /// <summary>
    /// Gets the unique key of the transaction that was not found.
    /// </summary>
    public Guid TransactionKey { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionNotFoundException"/> class.
    /// </summary>
    /// <param name="key">The unique identifier of the transaction that was not found.</param>
    public TransactionNotFoundException(Guid key)
        : base($"Transaction with key '{key}' was not found.")
    {
        TransactionKey = key;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionNotFoundException"/> class with a custom message.
    /// </summary>
    /// <param name="key">The unique identifier of the transaction that was not found.</param>
    /// <param name="message">The custom error message.</param>
    public TransactionNotFoundException(Guid key, string message)
        : base(message)
    {
        TransactionKey = key;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionNotFoundException"/> class with a custom message and inner exception.
    /// </summary>
    /// <param name="key">The unique identifier of the transaction that was not found.</param>
    /// <param name="message">The custom error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public TransactionNotFoundException(Guid key, string message, Exception innerException)
        : base(message, innerException)
    {
        TransactionKey = key;
    }
}
