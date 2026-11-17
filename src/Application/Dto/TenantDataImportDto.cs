namespace YoFi.V3.Application.Dto;

/// <summary>A row-level validation or skip message from an imported worksheet.</summary>
/// <param name="RowNumber">One-based spreadsheet row number.</param>
/// <param name="Field">Source worksheet field associated with the message.</param>
/// <param name="Code">Stable machine-readable issue code.</param>
/// <param name="Message">User-readable explanation.</param>
public record DataImportIssueDto(int RowNumber, string Field, string Code, string Message);

/// <summary>Validation and detection summary for one recognized worksheet.</summary>
/// <param name="WorksheetName">Source worksheet name.</param>
/// <param name="DataType">V3 data type that the worksheet maps to.</param>
/// <param name="ValidRecordCount">Number of valid records ready to import.</param>
/// <param name="SkippedRecordCount">Number of rows intentionally skipped.</param>
/// <param name="IgnoredColumns">Unrecognized columns in the worksheet.</param>
/// <param name="Errors">Blocking row or worksheet validation errors.</param>
/// <param name="SkippedRows">Rows skipped with a non-blocking reason.</param>
public record ImportWorksheetPreviewDto(
    string WorksheetName,
    string DataType,
    int ValidRecordCount,
    int SkippedRecordCount,
    IReadOnlyCollection<string> IgnoredColumns,
    IReadOnlyCollection<DataImportIssueDto> Errors,
    IReadOnlyCollection<DataImportIssueDto> SkippedRows)
{
    /// <summary>Whether this worksheet can be imported without blocking validation errors.</summary>
    public bool CanImport => Errors.Count == 0;
}

/// <summary>Format-neutral preview of the supported content in an uploaded workbook.</summary>
/// <param name="FormatId">Detected source format identifier, or null when no format was detected.</param>
/// <param name="Worksheets">Recognized worksheet previews.</param>
/// <param name="IgnoredWorksheets">Worksheets that have no registered import handler.</param>
/// <param name="Sha256">Uppercase SHA-256 digest of the exact uploaded bytes.</param>
public record TenantDataImportPreviewDto(
    string? FormatId,
    IReadOnlyCollection<ImportWorksheetPreviewDto> Worksheets,
    IReadOnlyCollection<string> IgnoredWorksheets,
    string Sha256)
{
    /// <summary>Whether the detected workbook has supported worksheets and all validate.</summary>
    public bool CanImport =>
        FormatId is not null
        && Worksheets.Count > 0
        && Worksheets.All(worksheet => worksheet.CanImport);
}

/// <summary>Committed result for one imported worksheet/data type.</summary>
/// <param name="WorksheetName">Source worksheet name.</param>
/// <param name="DataType">V3 data type that received the records.</param>
/// <param name="Created">Number of new records committed.</param>
/// <param name="Skipped">Number of source rows intentionally skipped.</param>
/// <param name="Updated">Number of existing records updated.</param>
/// <param name="Failed">Always zero on success; a failed worksheet throws.</param>
public record ImportWorksheetResultDto(
    string WorksheetName,
    string DataType,
    int Created,
    int Skipped,
    int Updated,
    int Failed);

/// <summary>Committed results for all worksheets in a confirmed import.</summary>
/// <param name="Worksheets">Per-worksheet/data-type import results.</param>
public record TenantDataImportResultDto(IReadOnlyCollection<ImportWorksheetResultDto> Worksheets);

/// <summary>An original-worksheet transaction graph to create in the authorized tenant.</summary>
/// <param name="Date">Transaction date.</param>
/// <param name="Amount">Authoritative transaction amount.</param>
/// <param name="Payee">Transaction payee.</param>
/// <param name="Memo">Optional transaction memo.</param>
/// <param name="BankReference">Supplied or generated legacy ExternalId.</param>
/// <param name="Splits">Validated split allocations for the transaction.</param>
public record OriginalTransactionImportDto(
    DateOnly Date,
    decimal Amount,
    string Payee,
    string? Memo,
    string? BankReference,
    IReadOnlyCollection<OriginalSplitImportDto> Splits);

/// <summary>A split allocation imported from an original YoFi worksheet.</summary>
/// <param name="Amount">Signed split amount.</param>
/// <param name="Category">Sanitized split category.</param>
/// <param name="Memo">Optional split memo.</param>
public record OriginalSplitImportDto(decimal Amount, string Category, string? Memo);

/// <summary>A transaction row imported from a native YoFi.V3 worksheet.</summary>
/// <param name="Key">Optional native GUID key.</param>
/// <param name="Date">Transaction date.</param>
/// <param name="Amount">Authoritative transaction amount.</param>
/// <param name="Payee">Transaction payee.</param>
/// <param name="Memo">Optional transaction memo.</param>
/// <param name="Source">Optional native source value.</param>
/// <param name="ExternalId">Optional native external identifier.</param>
/// <param name="Category">Sanitized category or the SPLIT marker.</param>
public record NativeTransactionImportDto(
    Guid? Key,
    DateOnly Date,
    decimal Amount,
    string Payee,
    string? Memo,
    string? Source,
    string? ExternalId,
    string? Category);

/// <summary>A split row imported from a native YoFi.V3 worksheet.</summary>
/// <param name="Key">Optional native split GUID key.</param>
/// <param name="TransactionKey">GUID key of the parent native transaction.</param>
/// <param name="Amount">Signed split amount.</param>
/// <param name="Category">Sanitized split category.</param>
/// <param name="Memo">Optional split memo.</param>
/// <param name="Order">Zero-based display order.</param>
public record NativeSplitImportDto(
    Guid? Key,
    Guid TransactionKey,
    decimal Amount,
    string Category,
    string? Memo,
    int Order);

/// <summary>Counts for a native transaction worksheet persistence operation.</summary>
public record NativeTransactionImportResultDto(
    int CreatedTransactions,
    int UpdatedTransactions,
    int CreatedSplits,
    int UpdatedSplits,
    int SkippedTransactions);

/// <summary>A native payee matching-rule row for key-based upsert.</summary>
/// <param name="Key">Optional GUID key.</param>
/// <param name="PayeePattern">Required matching pattern.</param>
/// <param name="PayeeIsRegex">Whether the pattern is a regular expression.</param>
/// <param name="Category">Sanitized rule category.</param>
public record NativePayeeMatchingRuleImportDto(
    Guid? Key,
    string PayeePattern,
    bool PayeeIsRegex,
    string Category);
