namespace YoFi.V3.Application.Helpers;

using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using OfxSharp;
using YoFi.V3.Application.Dto;

/// <summary>
/// Helper for parsing OFX (Open Financial Exchange) files into transactions.
/// </summary>
public static partial class OfxParsingHelper
{
    /// <summary>
    /// Parses an OFX file stream and extracts transaction data.
    /// </summary>
    /// <param name="fileStream">The stream containing OFX file data.</param>
    /// <param name="fileName">The name of the file being parsed.</param>
    public static Task<OfxParsingResult> ParseAsync(Stream fileStream, string fileName)
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
                                (!string.IsNullOrWhiteSpace(transactionMemo) && IsNameTruncatedMemo(name, transactionMemo)))
                            {
                                // Case 1: No NAME - use MEMO as payee
                                // Case 2: NAME appears to be truncated (MEMO starts with NAME, accounting for whitespace)
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
                                    Message = $"Transaction on {date:yyyy-MM-dd} has no payee name (NAME and MEMO fields both missing or empty)",
                                    FileName = fileName
                                });
                                continue; // Skip this transaction
                            }

                            // Generate ExternalId: use FITID (required by OFXSharp library)
                            // NOTE: The fallback to GenerateTransactionHash is unreachable because OFXSharp
                            // requires FITID to be present and will throw an exception during parsing if missing.
                            // See test: OfxParsingHelperTests.ParseAsync_TransactionWithoutFitid_FailsToParse
#if false // Unreachable code: OFXSharp requires FITID to be present
                            var externalId = !string.IsNullOrWhiteSpace(transaction.TransactionId)
                                ? transaction.TransactionId
                                : GenerateTransactionHash(dateTime, transaction.Amount, payee, memo ?? string.Empty, source);
#else
                            var externalId = transaction.TransactionId!; // OFXSharp guarantees non-null FITID
#endif

                            // All fields extracted (Tests 5-9)
                            transactions.Add(new TransactionImportDto
                            {
                                ExternalId = externalId,
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
                Message = $"Failed to parse OFX document: {ex.Message}",
                FileName = fileName
            });
        }

        var parseResult = new OfxParsingResult
        {
            Transactions = transactions,
            Errors = errors
        };

        return Task.FromResult(parseResult);
    }

    /// <summary>
    /// Determines if the NAME field appears to be a truncated version of the MEMO field.
    /// </summary>
    /// <param name="name">The NAME field from the OFX transaction.</param>
    /// <param name="memo">The MEMO field from the OFX transaction.</param>
    /// <returns>True if NAME appears to be a truncated MEMO; otherwise false.</returns>
    /// <remarks>
    /// Banks often truncate payee names in the NAME field but include the full name in MEMO.
    /// This method handles cases where the NAME may have extra whitespace by normalizing
    /// both strings before comparison.
    /// </remarks>
    private static bool IsNameTruncatedMemo(string name, string memo)
    {
        // Normalize whitespace: collapse multiple spaces to single space and trim
        var normalizedName = CollapseWhitespace(name).Trim();
        var normalizedMemo = CollapseWhitespace(memo).Trim();

        // Check if normalized MEMO starts with normalized NAME (case-insensitive)
        return normalizedMemo.StartsWith(normalizedName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Collapses multiple whitespace characters into single spaces.
    /// </summary>
    /// <param name="input">The string to normalize.</param>
    /// <returns>String with whitespace collapsed.</returns>
    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();

    private static string CollapseWhitespace(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        return WhitespacePattern().Replace(input, " ");
    }

#if false // Unreachable code: OFXSharp requires FITID to be present in all transactions
    /// <summary>
    /// Generates a unique hash for a transaction based on its key fields.
    /// </summary>
    /// <remarks>
    /// UNREACHABLE: This method was intended to be used when FITID is not available from the OFX file.
    /// However, the OFXSharp library requires FITID to be present and will throw an exception during
    /// parsing if it's missing. Therefore, this fallback hash generation is never executed.
    ///
    /// The hash would be computed from date, amount, payee, memo, and source to ensure
    /// identical transactions produce the same hash for duplicate detection.
    ///
    /// See test: OfxParsingHelperTests.ParseAsync_TransactionWithoutFitid_FailsToParse
    /// </remarks>
    private static string GenerateTransactionHash(DateTime date, decimal amount, string payee, string memo, string source)
    {
        // Create a stable string representation of the transaction
        var dataString = $"{date:yyyy-MM-ddTHH:mm:ss}|{amount:F2}|{payee}|{memo}|{source}";
        var bytes = Encoding.UTF8.GetBytes(dataString);
        var hashBytes = SHA256.HashData(bytes);

        // Convert to hex string
        return Convert.ToHexString(hashBytes);
    }
#endif
}
