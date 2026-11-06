namespace YoFi.V3.Application.Dto;

/// <summary>
/// Complete transaction data including all fields (output-only).
/// </summary>
/// <param name="Key">Unique identifier for the transaction</param>
/// <param name="Date">Date the transaction occurred</param>
/// <param name="Amount">Transaction amount (can be negative for credits/refunds)</param>
/// <param name="Payee">Recipient or payee of the transaction</param>
/// <param name="Memo">Optional memo for additional context</param>
/// <param name="Source">Source of the transaction (e.g., "Chase Checking 1234")</param>
/// <param name="ExternalId">Bank's unique identifier for duplicate detection</param>
/// <param name="Category">Category from the single split, or empty when there are multiple splits or no category</param>
/// <remarks>
/// Complete transaction DTO including all fields. Used for detail views and editing forms.
///
/// Category comes from Order = 0 for single-split transactions. Splits contains ordered
/// allocations only for multi-split transactions, where they are displayed instead of Category.
///
/// For list views with basic fields only, see <see cref="TransactionResultDto"/>.
/// For input/editing, see <see cref="TransactionEditDto"/>.
/// </remarks>
public record TransactionDetailDto(
    Guid Key,
    DateOnly Date,
    decimal Amount,
    string Payee,
    string? Memo,
    string? Source,
    string? ExternalId,
    string Category
)
{
    /// <summary>Gets allocations in display order for multi-split transactions; otherwise empty.</summary>
    public IReadOnlyList<TransactionSplitDto> Splits { get; init; } = [];
}

/// <summary>A transaction's category allocation returned for display.</summary>
/// <param name="Key">Unique identifier for the split.</param>
/// <param name="Amount">Signed amount allocated to this split.</param>
/// <param name="Category">Category assigned to this split.</param>
/// <param name="Memo">Optional split-specific memo.</param>
/// <param name="Order">Zero-based display order.</param>
public record TransactionSplitDto(Guid Key, decimal Amount, string Category, string? Memo, int Order);
