namespace YoFi.V3.Entities.Exceptions;

/// <summary>
/// Base exception for when a requested resource cannot be found.
/// Automatically maps to HTTP 404 in the API pipeline.
/// </summary>
public abstract class ResourceNotFoundException : Exception
{
    /// <summary>
    /// Gets the type of resource that was not found (e.g., "Tenant", "Transaction").
    /// </summary>
    public abstract string ResourceType { get; }

    /// <summary>
    /// Gets the unique key of the resource that was not found.
    /// </summary>
    public Guid ResourceKey { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceNotFoundException"/> class.
    /// Generates a default message using the ResourceType.
    /// </summary>
    /// <param name="key">The unique identifier of the resource that was not found.</param>
    protected ResourceNotFoundException(Guid key)
        : base(string.Empty) // Temporary, will be set after ResourceType is available
    {
        ResourceKey = key;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceNotFoundException"/> class with a custom message.
    /// </summary>
    /// <param name="key">The unique identifier of the resource that was not found.</param>
    /// <param name="message">The custom error message.</param>
    protected ResourceNotFoundException(Guid key, string message)
        : base(message)
    {
        ResourceKey = key;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ResourceNotFoundException"/> class with a custom message and inner exception.
    /// </summary>
    /// <param name="key">The unique identifier of the resource that was not found.</param>
    /// <param name="message">The custom error message.</param>
    /// <param name="innerException">The inner exception.</param>
    protected ResourceNotFoundException(Guid key, string message, Exception innerException)
        : base(message, innerException)
    {
        ResourceKey = key;
    }

    /// <summary>
    /// Gets the exception message. Generates a default message if one wasn't provided.
    /// </summary>
    public override string Message =>
        string.IsNullOrEmpty(base.Message)
            ? $"{ResourceType} with key '{ResourceKey}' was not found."
            : base.Message;
}
