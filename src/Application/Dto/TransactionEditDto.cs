using System.ComponentModel.DataAnnotations;

namespace YoFi.V3.Application.Dto;

/// <summary>
/// Transaction data for creating or updating transactions (input DTO).
/// </summary>
/// <param name="Date">Date the transaction occurred</param>
/// <param name="Amount">Transaction amount (cannot be zero; can be negative for credits/refunds)</param>
/// <param name="Payee">Recipient or payee of the transaction (required, max 200 chars)</param>
/// <remarks>
/// This is an input DTO with validation attributes. All properties are validated before
/// being persisted to the database. For query results, see <see cref="TransactionResultDto"/>.
///
/// Validation rules:
/// - Amount: Must be non-zero (enforced in business logic)
/// - Payee: Required, max 200 characters (enforced by data annotations)
/// </remarks>
public record TransactionEditDto(
    DateOnly Date,

    [Range(typeof(decimal), "-999999999", "999999999", ErrorMessage = "Amount must be a valid value")]
    decimal Amount,

    [Required(ErrorMessage = "Payee is required")]
    [MaxLength(200, ErrorMessage = "Payee cannot exceed 200 characters")]
    string Payee
);
