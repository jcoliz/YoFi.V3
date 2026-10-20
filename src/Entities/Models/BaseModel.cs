using DotNetNuxt.Abstractions;

namespace YoFi.V3.Entities.Models;

/// <summary>
/// Base implementation of IModel
/// </summary>
/// <remarks>
/// Helpful so we don't always have to remember to set a GUID Key
/// when creating new records.
/// </remarks>
public record BaseModel : IKeyedModel
{
    /// <inheritdoc/>
    public int Id { get; set; }

    /// <inheritdoc/>
    public Guid Key { get; set; } = Guid.NewGuid();
}
