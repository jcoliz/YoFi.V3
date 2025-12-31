namespace YoFi.V3.Application.Dto;

/// <summary>
/// Request to set the selection state for specific import review transactions.
/// </summary>
/// <param name="Keys">Collection of transaction keys to update.</param>
/// <param name="IsSelected">The desired selection state (true = selected, false = deselected).</param>
/// <remarks>
/// Used to toggle individual transaction selections or bulk select/deselect multiple transactions.
/// </remarks>
public record SetSelectionRequest(
    IReadOnlyCollection<Guid> Keys,
    bool IsSelected
);
