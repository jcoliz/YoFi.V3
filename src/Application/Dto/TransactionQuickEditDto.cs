using System.ComponentModel.DataAnnotations;
using YoFi.V3.Application.Validation;

namespace YoFi.V3.Application.Dto;

/// <summary>
/// Transaction data for quick editing from list view (payee and memo only).
/// </summary>
/// <param name="Payee">Recipient or payee of the transaction (required, cannot be whitespace, max 200 chars)</param>
/// <param name="Memo">Optional memo for additional context (max 1000 chars)</param>
/// <remarks>
/// This is a specialized input DTO for "light" edits from the transaction list view.
/// It only allows updating Payee and Memo fields, preserving all other transaction
/// properties (Date, Amount, Source, ExternalId) unchanged.
///
/// For full transaction updates, use <see cref="TransactionEditDto"/>.
/// </remarks>
public record TransactionQuickEditDto(
    [Required(ErrorMessage = "Payee is required")]
    [NotWhiteSpace(ErrorMessage = "Payee cannot be empty")]
    [MaxLength(200, ErrorMessage = "Payee cannot exceed 200 characters")]
    string Payee,

    [MaxLength(1000, ErrorMessage = "Memo cannot exceed 1000 characters")]
    string? Memo
);
