namespace YoFi.V3.Entities.Exceptions;

/// <summary>
/// Exception thrown when a split cannot be found within the requested transaction.
/// </summary>
public class SplitNotFoundException(Guid key) : ResourceNotFoundException(key)
{
    /// <inheritdoc />
    public override string ResourceType => "Split";
}
