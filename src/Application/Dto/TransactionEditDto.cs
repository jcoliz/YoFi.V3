using System.ComponentModel.DataAnnotations;
using YoFi.V3.Application.Validation;

namespace YoFi.V3.Application.Dto;

/// <summary>
/// Transaction data for creating or updating transactions (input DTO).
/// </summary>
/// <param name="Date">Date the transaction occurred (max 50 years in past, 5 years in future)</param>
/// <param name="Amount">Transaction amount (cannot be zero; can be negative for credits/refunds)</param>
/// <param name="Payee">Recipient or payee of the transaction (required, cannot be whitespace, max 200 chars)</param>
/// <param name="Memo">Optional memo for additional context (max 1000 chars)</param>
/// <param name="Source">Source of the transaction (optional, max 200 chars, typically from importer)</param>
/// <param name="ExternalId">Bank's unique identifier (optional, max 100 chars, for duplicate detection)</param>
/// <remarks>
/// This is an input DTO with validation attributes. All properties are validated before
/// being persisted to the database. For query results, see <see cref="TransactionResultDto"/>
/// or <see cref="TransactionDetailDto"/>.
///
/// Validation rules:
/// - Date: Must be within 50 years in the past and 5 years in the future
/// - Amount: Must be non-zero (enforced in business logic)
/// - Payee: Required, cannot be empty or whitespace, max 200 characters
/// - Memo: Optional, max 1000 characters, plain text only
/// - Source: Optional, max 200 characters, typically set by importer
/// - ExternalId: Optional, max 100 characters, for duplicate detection
/// </remarks>
public record TransactionEditDto(
    [DateRange(50, 5, ErrorMessage = "Transaction date must be within 50 years in the past and 5 years in the future")]
    DateOnly Date,

    [Range(typeof(decimal), "-999999999", "999999999", ErrorMessage = "Amount must be a valid value")]
    decimal Amount,

    [Required(ErrorMessage = "Payee is required")]
    [NotWhiteSpace(ErrorMessage = "Payee cannot be empty")]
    [MaxLength(200, ErrorMessage = "Payee cannot exceed 200 characters")]
    string Payee,

    [MaxLength(1000, ErrorMessage = "Memo cannot exceed 1000 characters")]
    string? Memo,

    [MaxLength(200, ErrorMessage = "Source cannot exceed 200 characters")]
    string? Source,

    [MaxLength(100, ErrorMessage = "ExternalId cannot exceed 100 characters")]
    string? ExternalId
);
