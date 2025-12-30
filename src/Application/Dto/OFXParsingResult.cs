namespace YoFi.V3.Application.Dto;

/// <summary>
/// Result of parsing an OFX file.
/// </summary>
public class OfxParsingResult
{
    /// <summary>
    /// Collection of transactions extracted from the OFX file.
    /// </summary>
    public required IReadOnlyCollection<TransactionImportDto> Transactions { get; init; }

    /// <summary>
    /// Collection of errors encountered during parsing.
    /// </summary>
    public required IReadOnlyCollection<OfxParsingError> Errors { get; init; }
}
