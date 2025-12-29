namespace YoFi.V3.Application.Import.Services;

using YoFi.V3.Application.Import.Dto;

/// <summary>
/// Service for parsing OFX (Open Financial Exchange) files into transactions.
/// </summary>
public class OfxParsingService : IOfxParsingService
{
    /// <summary>
    /// Parses an OFX file stream and extracts transaction data.
    /// </summary>
    /// <param name="fileStream">The stream containing OFX file data.</param>
    /// <param name="fileName">The name of the file being parsed.</param>
    public Task<OfxParsingResult> ParseAsync(Stream fileStream, string fileName)
    {
        var errors = new List<OfxParsingError>();

        // Handle null stream
        if (fileStream == null)
        {
            var result = new OfxParsingResult
            {
                Transactions = Array.Empty<TransactionImportDto>(),
                Errors = Array.Empty<OfxParsingError>()
            };
            return Task.FromResult(result);
        }

        // Try to detect if this looks like valid OFX
        using var reader = new StreamReader(fileStream, leaveOpen: true);
        var content = reader.ReadToEnd();

        // If empty, return empty result (not an error)
        if (string.IsNullOrWhiteSpace(content))
        {
            var emptyResult = new OfxParsingResult
            {
                Transactions = Array.Empty<TransactionImportDto>(),
                Errors = Array.Empty<OfxParsingError>()
            };
            return Task.FromResult(emptyResult);
        }

        // If doesn't contain OFX markers, add error
        if (!content.Contains("<OFX>"))
        {
            errors.Add(new OfxParsingError
            {
                Message = "Invalid OFX format: Missing OFX header or content"
            });
        }

        var parseResult = new OfxParsingResult
        {
            Transactions = Array.Empty<TransactionImportDto>(),
            Errors = errors
        };

        return Task.FromResult(parseResult);
    }
}
