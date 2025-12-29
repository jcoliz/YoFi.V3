namespace YoFi.V3.Application.Import.Services;

using YoFi.V3.Application.Import.Dto;

/// <summary>
/// Service for parsing OFX (Open Financial Exchange) files into transactions.
/// </summary>
public interface IOfxParsingService
{
    /// <summary>
    /// Parses an OFX file stream and extracts transaction data.
    /// </summary>
    /// <param name="fileStream">The stream containing OFX file data.</param>
    /// <param name="fileName">The name of the file being parsed.</param>
    Task<OfxParsingResult> ParseAsync(Stream fileStream, string fileName);
}
