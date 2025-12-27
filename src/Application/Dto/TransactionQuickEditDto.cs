using System.ComponentModel.DataAnnotations;
using YoFi.V3.Application.Validation;

namespace YoFi.V3.Application.Dto;

/// <summary>
/// Transaction data for quick editing from list view (payee, memo, and category).
/// </summary>
/// <param name="Payee">Recipient or payee of the transaction (required, cannot be whitespace, max 200 chars)</param>
/// <param name="Memo">Optional memo for additional context (max 1000 chars)</param>
/// <param name="Category">Category for the single split (optional, max 200 chars, auto-sanitized)</param>
/// <remarks>
/// This is a specialized input DTO for "light" edits from the transaction list view.
/// It allows updating Payee, Memo, and Category fields, preserving all other transaction
/// properties (Date, Amount, Source, ExternalId) unchanged.
///
/// Alpha-1 (Stories 3 &amp; 5): Category edits the single split (Order = 0). User never sees split complexity.
///
/// For full transaction updates, use <see cref="TransactionEditDto"/>.
/// </remarks>
public record TransactionQuickEditDto(
    [Required(ErrorMessage = "Payee is required")]
    [NotWhiteSpace(ErrorMessage = "Payee cannot be empty")]
    [MaxLength(200, ErrorMessage = "Payee cannot exceed 200 characters")]
    string Payee,

    [MaxLength(1000, ErrorMessage = "Memo cannot exceed 1000 characters")]
    string? Memo,

    [MaxLength(200, ErrorMessage = "Category cannot exceed 200 characters")]
    string? Category
);
