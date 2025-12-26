namespace YoFi.V3.Application.Dto;

/// <summary>
/// Transaction data returned from queries (output-only).
/// </summary>
/// <param name="Key">Unique identifier for the transaction</param>
/// <param name="Date">Date the transaction occurred</param>
/// <param name="Amount">Transaction amount (can be negative for credits/refunds)</param>
/// <param name="Payee">Recipient or payee of the transaction (max 200 chars)</param>
/// <param name="Memo">Optional memo for additional context</param>
/// <remarks>
/// This is an output DTO for list views - data is already validated when read from the database.
/// Includes essential fields plus user memo. Source and ExternalId are omitted to minimize
/// data transfer (internal tracking details not needed in list views).
/// For complete transaction details, see <see cref="TransactionDetailDto"/>.
/// For input/editing, see <see cref="TransactionEditDto"/>.
/// </remarks>
public record TransactionResultDto(Guid Key, DateOnly Date, decimal Amount, string Payee, string? Memo);
