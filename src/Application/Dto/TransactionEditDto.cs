namespace YoFi.V3.Application.Dto;

/// <summary>
/// Transaction data for creating or updating transactions (input DTO).
/// </summary>
/// <param name="Date">Date the transaction occurred</param>
/// <param name="Amount">Transaction amount (can be negative for credits/refunds)</param>
/// <param name="Payee">Recipient or payee of the transaction</param>
/// <param name="Memo">Optional memo for additional context</param>
/// <param name="Source">Source of the transaction (optional, typically from importer)</param>
/// <param name="ExternalId">Bank's unique identifier (optional, for duplicate detection)</param>
/// <param name="Category">Category for the single split (optional, auto-sanitized)</param>
/// <remarks>
/// This DTO is validated by <see cref="YoFi.V3.Application.Validation.TransactionEditDtoValidator"/>
/// at the controller boundary. For query results, see <see cref="TransactionResultDto"/>
/// or <see cref="TransactionDetailDto"/>.
///
/// All validation rules are enforced by FluentValidation - see TransactionEditDtoValidator for:
/// - Date range validation (50 years past to 5 years future)
/// - Amount non-zero requirement
/// - Payee required and max length 200
/// - Memo max length 1000
/// - Source max length 200
/// - ExternalId max length 100
/// - Category max length 200
///
/// Updates balance the transaction by adjusting the first split by Order.
/// For single-split transactions, category also updates the single split.
/// For multi-split transactions, other splits and all split categories and memos are preserved;
/// Category must be null and a supplied category is rejected.
/// </remarks>
public record TransactionEditDto(
    DateOnly Date,
    decimal Amount,
    string Payee,
    string? Memo,
    string? Source,
    string? ExternalId,
    string? Category
);
