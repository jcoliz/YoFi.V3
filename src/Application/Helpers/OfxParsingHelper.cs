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

        if (!ValidateStream(fileStream))
        {
            return Task.FromResult(CreateEmptyResult());
        }

        ParseOfxDocument(fileStream, fileName, transactions, errors);

        var parseResult = new OfxParsingResult
        {
            Transactions = transactions,
            Errors = errors
        };

        return Task.FromResult(parseResult);
    }

    /// <summary>
    /// Validates that the stream is not null or empty.
    /// </summary>
    /// <param name="stream">The stream to validate.</param>
    /// <returns>True if stream is valid; otherwise false.</returns>
    private static bool ValidateStream(Stream? stream)
    {
        if (stream == null)
        {
            return false;
        }

        if (stream.Length == 0 || (stream.CanSeek && stream.Position == stream.Length))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Creates an empty parsing result.
    /// </summary>
    /// <returns>An empty <see cref="OfxParsingResult"/>.</returns>
    private static OfxParsingResult CreateEmptyResult()
    {
        return new OfxParsingResult
        {
            Transactions = Array.Empty<TransactionImportDto>(),
            Errors = Array.Empty<OfxParsingError>()
        };
    }

    /// <summary>
    /// Parses an OFX document and extracts transactions.
    /// </summary>
    /// <param name="fileStream">The stream containing OFX file data.</param>
    /// <param name="fileName">The name of the file being parsed.</param>
    /// <param name="transactions">List to populate with parsed transactions.</param>
    /// <param name="errors">List to populate with parsing errors.</param>
    private static void ParseOfxDocument(
        Stream fileStream,
        string fileName,
        List<TransactionImportDto> transactions,
        List<OfxParsingError> errors)
    {
        try
        {
            if (fileStream.CanSeek)
            {
                fileStream.Position = 0;
            }

            var document = OfxDocumentReader.ReadFile(fileStream);

            if (document?.Statements == null)
            {
                return;
            }

            var bankName = document.SignOn?.Institution?.Name;

            foreach (var statement in document.Statements)
            {
                ProcessStatement(statement, bankName, fileName, transactions, errors);
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
    }

    /// <summary>
    /// Processes a single OFX statement and extracts its transactions.
    /// </summary>
    /// <param name="statement">The OFX statement to process.</param>
    /// <param name="bankName">The bank name from the document.</param>
    /// <param name="fileName">The name of the file being parsed.</param>
    /// <param name="transactions">List to populate with parsed transactions.</param>
    /// <param name="errors">List to populate with parsing errors.</param>
    private static void ProcessStatement(
        OfxStatementResponse? statement,
        string? bankName,
        string fileName,
        List<TransactionImportDto> transactions,
        List<OfxParsingError> errors)
    {
        if (statement?.Transactions == null)
        {
            return;
        }

        var source = BuildSourceString(bankName, statement.AccountFrom);

        foreach (var transaction in statement.Transactions)
        {
            ProcessTransaction(transaction, source, fileName, transactions, errors);
        }
    }

    /// <summary>
    /// Builds a source string from bank name and account information.
    /// </summary>
    /// <param name="bankName">The bank name.</param>
    /// <param name="accountFrom">The account information.</param>
    /// <returns>A formatted source string.</returns>
    private static string BuildSourceString(string? bankName, Account? accountFrom)
    {
        var sourceParts = new List<string>();

        if (!string.IsNullOrWhiteSpace(bankName))
        {
            sourceParts.Add(bankName);
        }

        if (accountFrom is Account.BankAccount bankAccount)
        {
            AddAccountInfoToSource(sourceParts, bankAccount);
        }

        return sourceParts.Count > 0 ? string.Join(" - ", sourceParts) : string.Empty;
    }

    /// <summary>
    /// Adds account information to the source parts list.
    /// </summary>
    /// <param name="sourceParts">List of source parts to append to.</param>
    /// <param name="bankAccount">The bank account information.</param>
    private static void AddAccountInfoToSource(List<string> sourceParts, Account.BankAccount bankAccount)
    {
        var accountTypeStr = bankAccount.BankAccountType.ToString();
        var accountType = FormatAccountType(accountTypeStr);
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

    /// <summary>
    /// Formats an account type string by converting to title case.
    /// </summary>
    /// <param name="accountTypeStr">The account type string (e.g., "CHECKING").</param>
    /// <returns>Title case account type (e.g., "Checking").</returns>
    private static string FormatAccountType(string accountTypeStr)
    {
        return accountTypeStr.Length > 0
            ? char.ToUpper(accountTypeStr[0]) + accountTypeStr.Substring(1).ToLower()
            : accountTypeStr;
    }

    /// <summary>
    /// Processes a single OFX transaction and adds it to the transactions list.
    /// </summary>
    /// <param name="transaction">The OFX transaction to process.</param>
    /// <param name="source">The source string for this transaction.</param>
    /// <param name="fileName">The name of the file being parsed.</param>
    /// <param name="transactions">List to populate with parsed transactions.</param>
    /// <param name="errors">List to populate with parsing errors.</param>
    private static void ProcessTransaction(
        OfxSharp.Transaction transaction,
        string source,
        string fileName,
        List<TransactionImportDto> transactions,
        List<OfxParsingError> errors)
    {
        var dateTime = transaction.Date?.DateTime ?? DateTime.MinValue;
        var (payee, memo) = ExtractPayeeAndMemo(transaction.Name, transaction.Memo);

        if (string.IsNullOrWhiteSpace(payee))
        {
            var date = DateOnly.FromDateTime(dateTime);
            errors.Add(new OfxParsingError
            {
                Message = $"Transaction on {date:yyyy-MM-dd} has no payee name (NAME and MEMO fields both missing or empty)",
                FileName = fileName
            });
            return;
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

    /// <summary>
    /// Extracts payee and memo from OFX NAME and MEMO fields with smart handling for truncation.
    /// </summary>
    /// <param name="name">The NAME field from the OFX transaction.</param>
    /// <param name="transactionMemo">The MEMO field from the OFX transaction.</param>
    /// <returns>A tuple containing the payee and memo values.</returns>
    /// <remarks>
    /// Handles three cases:
    /// 1. No NAME - use MEMO as payee
    /// 2. NAME appears truncated (MEMO starts with NAME) - use MEMO as payee
    /// 3. Normal case - NAME is payee, MEMO is memo
    /// </remarks>
    private static (string? payee, string? memo) ExtractPayeeAndMemo(string? name, string? transactionMemo)
    {
        if (string.IsNullOrWhiteSpace(name) ||
            (!string.IsNullOrWhiteSpace(transactionMemo) && IsNameTruncatedMemo(name, transactionMemo)))
        {
            // Case 1: No NAME - use MEMO as payee
            // Case 2: NAME appears to be truncated (MEMO starts with NAME, accounting for whitespace)
            // In both cases: use MEMO as payee, leave memo blank
            return (transactionMemo, null);
        }

        // Case 3: Normal case - NAME is payee, MEMO is memo
        return (name, transactionMemo);
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
