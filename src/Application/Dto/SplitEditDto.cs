namespace YoFi.V3.Application.Dto;

/// <summary>
/// Data for adding or updating a transaction split.
/// </summary>
/// <param name="Amount">Signed amount allocated to this split.</param>
/// <param name="Category">Optional category; an empty or missing value means uncategorized.</param>
/// <param name="Memo">Optional split-specific memo.</param>
/// <remarks>
/// Validate this DTO with <see cref="YoFi.V3.Application.Validation.SplitEditDtoValidator"/>
/// at the application boundary.
/// </remarks>
public record SplitEditDto(decimal Amount, string? Category, string? Memo);
