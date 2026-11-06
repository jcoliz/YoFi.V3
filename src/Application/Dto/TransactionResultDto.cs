namespace YoFi.V3.Application.Dto;

/// <summary>
/// Transaction data returned from queries (output-only).
/// </summary>
/// <param name="Key">Unique identifier for the transaction</param>
/// <param name="Date">Date the transaction occurred</param>
/// <param name="Amount">Transaction amount (can be negative for credits/refunds)</param>
/// <param name="Payee">Recipient or payee of the transaction (max 200 chars)</param>
/// <param name="Memo">Optional memo for additional context</param>
/// <param name="Category">Category from the single split, or empty for multiple splits or no category</param>
/// <param name="HasMultipleSplits">Whether the transaction has more than one split</param>
/// <remarks>
/// This is an output DTO for list views - data is already validated when read from the database.
/// Includes essential fields plus user memo and category. Source and ExternalId are omitted to minimize
/// data transfer (internal tracking details not needed in list views).
///
/// Single-split categories come from Order = 0. Multi-split transactions have an empty category
/// and are identified by HasMultipleSplits.
///
/// For complete transaction details, see <see cref="TransactionDetailDto"/>.
/// For input/editing, see <see cref="TransactionEditDto"/>.
/// </remarks>
public record TransactionResultDto(Guid Key, DateOnly Date, decimal Amount, string Payee, string? Memo, string Category,
    bool HasMultipleSplits = false);
