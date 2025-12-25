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
/// <remarks>
/// Complete transaction DTO including all fields. Used for detail views and editing forms.
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
    string? ExternalId
);
