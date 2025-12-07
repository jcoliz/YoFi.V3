namespace YoFi.V3.Entities.Models;

/// <summary>
/// Base implementation of IModel
/// </summary>
/// <remarks>
/// Helpful so we don't always have to remember to set a GUID Key
/// when creating new records.
/// </remarks>
public record BaseModel : IModel
{
    /// <inheritdoc/>
    public long Id { get; set; }

    /// <inheritdoc/>
    public Guid Key { get; set; } = Guid.NewGuid();
}
