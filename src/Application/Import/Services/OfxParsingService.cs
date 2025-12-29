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
                // Get bank name from signon (can be null)
                var bankName = document.SignOn?.Institution?.Name;

                foreach (var statement in document.Statements)
                {
                    if (statement?.Transactions != null)
                    {
                        // Build source string from available account info
                        var sourceParts = new List<string>();

                        if (!string.IsNullOrWhiteSpace(bankName))
                        {
                            sourceParts.Add(bankName);
                        }

                        if (statement.AccountFrom is Account.BankAccount bankAccount)
                        {
                            // Convert "CHECKING" to "Checking"
                            var accountTypeStr = bankAccount.BankAccountType.ToString();
                            var accountType = accountTypeStr.Length > 0
                                ? char.ToUpper(accountTypeStr[0]) + accountTypeStr.Substring(1).ToLower()
                                : accountTypeStr;
                            var accountId = bankAccount.AccountId;

                            if (!string.IsNullOrWhiteSpace(accountId))
                            {
                                sourceParts.Add($"{accountType} ({accountId})");
                            }
                            else if (!string.IsNullOrWhiteSpace(accountType))
                            {
                                sourceParts.Add(accountType);
                            }
                        }

                        var source = sourceParts.Count > 0 ? string.Join(" - ", sourceParts) : string.Empty;

                        foreach (var transaction in statement.Transactions)
                        {
                            // Extract date, amount, payee, and memo (Tests 5-8)
                            // Payee is required with smart handling for truncated NAME fields
                            var dateTime = transaction.Date?.DateTime ?? DateTime.MinValue;
                            var name = transaction.Name;
                            var transactionMemo = transaction.Memo;
                            string? payee;
                            string? memo;

                            // Handle various NAME/MEMO scenarios
                            if (string.IsNullOrWhiteSpace(name) ||
                                (!string.IsNullOrWhiteSpace(transactionMemo) &&
                                 transactionMemo.StartsWith(name, StringComparison.OrdinalIgnoreCase)))
                            {
                                // Case 1: No NAME - use MEMO as payee
                                // Case 2: NAME appears to be truncated (MEMO starts with NAME)
                                // In both cases: use MEMO as payee, leave memo blank
                                payee = transactionMemo;
                                memo = null;
                            }
                            else
                            {
                                // Case 3: Normal case - NAME is payee, MEMO is memo
                                payee = name;
                                memo = transactionMemo;
                            }

                            // If still no payee, skip this transaction with error
                            if (string.IsNullOrWhiteSpace(payee))
                            {
                                var date = DateOnly.FromDateTime(dateTime);
                                errors.Add(new OfxParsingError
                                {
                                    Message = $"Transaction on {date:yyyy-MM-dd} has no payee name (NAME and MEMO fields both missing or empty)"
                                });
                                continue; // Skip this transaction
                            }

                            // All fields extracted (Tests 5-9)
                            transactions.Add(new TransactionImportDto
                            {
                                Date = DateOnly.FromDateTime(dateTime),
                                Amount = transaction.Amount,
                                Payee = payee,
                                Memo = memo,
                                Source = source
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
