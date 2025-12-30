namespace YoFi.V3.Application.Import.Dto;

/// <summary>
/// Result of an OFX file import operation, to be shown to the user.
/// </summary>
/// <param name="ImportedCount">Number of transactions successfully imported and stored for review.</param>
/// <param name="NewCount">Number of new transactions (no duplicates detected).</param>
/// <param name="ExactDuplicateCount">Number of exact duplicate transactions detected.</param>
/// <param name="PotentialDuplicateCount">Number of potential duplicate transactions detected.</param>
/// <param name="Errors">Collection of errors encountered during OFX parsing.</param>
/// <remarks>
/// The sum of NewCount, ExactDuplicateCount, and PotentialDuplicateCount equals ImportedCount.
/// Errors indicate problems parsing individual transactions or the OFX file structure, but do not prevent
/// successfully parsed transactions from being imported.
/// </remarks>
public record ImportResultDto(
    int ImportedCount,
    int NewCount,
    int ExactDuplicateCount,
    int PotentialDuplicateCount,
    IReadOnlyCollection<OfxParsingError> Errors
);
