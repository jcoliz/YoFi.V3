using System.ComponentModel.DataAnnotations;
using YoFi.V3.Application.Validation;

namespace YoFi.V3.Application.Dto;

/// <summary>
/// Transaction data for creating or updating transactions (input DTO).
/// </summary>
/// <param name="Date">Date the transaction occurred (max 50 years in past, 5 years in future)</param>
/// <param name="Amount">Transaction amount (cannot be zero; can be negative for credits/refunds)</param>
/// <param name="Payee">Recipient or payee of the transaction (required, cannot be whitespace, max 200 chars)</param>
/// <remarks>
/// This is an input DTO with validation attributes. All properties are validated before
/// being persisted to the database. For query results, see <see cref="TransactionResultDto"/>.
///
/// Validation rules:
/// - Date: Must be within 50 years in the past and 5 years in the future
/// - Amount: Must be non-zero (enforced in business logic)
/// - Payee: Required, cannot be empty or whitespace, max 200 characters
/// </remarks>
public record TransactionEditDto(
    [property: DateRange(50, 5, ErrorMessage = "Transaction date must be within 50 years in the past and 5 years in the future")]
    DateOnly Date,

    [property: Range(typeof(decimal), "-999999999", "999999999", ErrorMessage = "Amount must be a valid value")]
    decimal Amount,

    [property: Required(ErrorMessage = "Payee is required")]
    [property: NotWhiteSpace(ErrorMessage = "Payee cannot be empty")]
    [property: MaxLength(200, ErrorMessage = "Payee cannot exceed 200 characters")]
    string Payee
);
