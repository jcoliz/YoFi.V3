namespace YoFi.V3.Entities.Exceptions;

/// <summary>
/// Exception thrown when a payee matching rule cannot be found.
/// Automatically maps to HTTP 404 in the API pipeline.
/// </summary>
public class PayeeMatchingRuleNotFoundException : ResourceNotFoundException
{
    /// <summary>
    /// Gets the type of resource that was not found.
    /// </summary>
    public override string ResourceType => "Payee matching rule";

    /// <summary>
    /// Initializes a new instance of the <see cref="PayeeMatchingRuleNotFoundException"/> class.
    /// </summary>
    /// <param name="key">The unique identifier of the payee matching rule that was not found.</param>
    public PayeeMatchingRuleNotFoundException(Guid key)
        : base(key)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PayeeMatchingRuleNotFoundException"/> class with a custom message.
    /// </summary>
    /// <param name="key">The unique identifier of the payee matching rule that was not found.</param>
    /// <param name="message">The custom error message.</param>
    public PayeeMatchingRuleNotFoundException(Guid key, string message)
        : base(key, message)
    {
    }
}
