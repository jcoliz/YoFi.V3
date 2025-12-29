namespace YoFi.V3.Application.Import.Dto;

/// <summary>
/// Transaction data imported from an OFX file.
/// </summary>
public class TransactionImportDto
{
    /// <summary>
    /// Transaction date (date only, time component is discarded from OFX).
    /// </summary>
    public required DateOnly Date { get; init; }

    /// <summary>
    /// Transaction amount (negative for debits, positive for credits).
    /// </summary>
    public required decimal Amount { get; init; }

    /// <summary>
    /// Payee or merchant name.
    /// </summary>
    public required string Payee { get; init; }

    /// <summary>
    /// Transaction memo or description.
    /// </summary>
    public string? Memo { get; init; }

    /// <summary>
    /// Source identifier (bank name, account type, account ID).
    /// </summary>
    public required string Source { get; init; }
}
