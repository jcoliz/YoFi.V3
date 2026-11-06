namespace YoFi.V3.Application.Dto;

/// <summary>
/// Transaction data for quick editing from list view (payee, memo, and category).
/// </summary>
/// <param name="Payee">Recipient or payee of the transaction</param>
/// <param name="Memo">Optional memo for additional context</param>
/// <param name="Category">Category for the single split (optional, auto-sanitized). Must be null for multi-split transactions.</param>
/// <remarks>
/// This DTO is validated by <see cref="YoFi.V3.Application.Validation.TransactionQuickEditDtoValidator"/>
/// at the controller boundary.
///
/// This is a specialized input DTO for "light" edits from the transaction list view.
/// It allows updating Payee, Memo, and Category fields, preserving all other transaction
/// properties (Date, Amount, Source, ExternalId) unchanged.
///
/// Category edits the single split (Order = 0). Multi-split transactions allow only payee
/// and memo edits; all split allocations are preserved.
///
/// For full transaction updates, use <see cref="TransactionEditDto"/>.
/// </remarks>
public record TransactionQuickEditDto(
    string Payee,
    string? Memo,
    string? Category
);
