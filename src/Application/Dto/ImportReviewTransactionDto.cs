using YoFi.V3.Entities.Models;

namespace YoFi.V3.Application.Dto;

/// <summary>
/// Presents information about an imported transaction for user review.
/// </summary>
/// <param name="Key">The unique identifier for the import review transaction.</param>
/// <param name="Date">Transaction date as reported by the bank.</param>
/// <param name="Payee">Payee or merchant name for the transaction.</param>
/// <param name="Category">Matched category (placeholder for future Payee Matching rules feature, empty for now).</param>
/// <param name="Amount">Transaction amount (positive for deposits, negative for withdrawals).</param>
/// <param name="DuplicateStatus">Status indicating whether this transaction is new or a duplicate.</param>
/// <param name="DuplicateOfKey">Key of the existing transaction if this is detected as a duplicate.</param>
/// <param name="IsSelected">Indicates whether this transaction is selected for import.</param>
public record ImportReviewTransactionDto(
    Guid Key,
    DateOnly Date,
    string Payee,
    string Category,
    decimal Amount,
    DuplicateStatus DuplicateStatus,
    Guid? DuplicateOfKey,
    bool IsSelected
);
