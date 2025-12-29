namespace YoFi.V3.Application.Import.Services;

using OfxSharp;
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
        var transactions = new List<TransactionImportDto>();

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

        // Check if stream is empty
        if (fileStream.Length == 0 || (fileStream.CanSeek && fileStream.Position == fileStream.Length))
        {
            var emptyResult = new OfxParsingResult
            {
                Transactions = Array.Empty<TransactionImportDto>(),
                Errors = Array.Empty<OfxParsingError>()
            };
            return Task.FromResult(emptyResult);
        }

        // Try to parse with OFXSharp
        try
        {
            // Reset stream position if seekable
            if (fileStream.CanSeek)
            {
                fileStream.Position = 0;
            }

            var document = OfxDocumentReader.ReadFile(fileStream);

            // Extract transactions from all statements
            if (document?.Statements != null)
            {
                foreach (var statement in document.Statements)
                {
                    if (statement?.Transactions != null)
                    {
                        foreach (var transaction in statement.Transactions)
                        {
                            // For now, only extract the date (Test 5)
                            // More fields will be added in subsequent tests
                            var dateTime = transaction.Date?.DateTime ?? DateTime.MinValue;
                            transactions.Add(new TransactionImportDto
                            {
                                Date = DateOnly.FromDateTime(dateTime),
                                Amount = 0m,  // Will be implemented in Test 6
                                Payee = string.Empty,  // Will be implemented in Test 7
                                Source = string.Empty  // Will be implemented in Test 9
                            });
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add(new OfxParsingError
            {
                Message = $"Failed to parse OFX document: {ex.Message}"
            });
        }

        var parseResult = new OfxParsingResult
        {
            Transactions = transactions,
            Errors = errors
        };

        return Task.FromResult(parseResult);
    }
}
