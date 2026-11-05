namespace YoFi.V3.Application.Dto;

/// <summary>A row-level validation or skip message from an imported workbook.</summary>
/// <param name="RowNumber">One-based spreadsheet row number.</param>
/// <param name="Field">Legacy worksheet field associated with the message.</param>
/// <param name="Code">Stable machine-readable issue code.</param>
/// <param name="Message">User-readable explanation.</param>
public record PayeeImportIssueDto(int RowNumber, string Field, string Code, string Message);

/// <summary>Detected original YoFi payee sheet and its validation summary.</summary>
/// <param name="WorksheetFound">Whether a Payee worksheet was found.</param>
/// <param name="ValidRecordCount">Number of valid payee rules ready to create.</param>
/// <param name="SkippedRecordCount">Number of rows intentionally skipped.</param>
/// <param name="IgnoredWorksheets">Other worksheets that will not be imported.</param>
/// <param name="IgnoredColumns">Unrecognized columns in the Payee worksheet.</param>
/// <param name="Errors">Blocking row or worksheet validation errors.</param>
/// <param name="SkippedRows">Rows skipped with a non-blocking reason.</param>
/// <param name="Sha256">Uppercase SHA-256 digest of the exact uploaded bytes.</param>
public record PayeeImportPreviewDto(
    bool WorksheetFound,
    int ValidRecordCount,
    int SkippedRecordCount,
    IReadOnlyCollection<string> IgnoredWorksheets,
    IReadOnlyCollection<string> IgnoredColumns,
    IReadOnlyCollection<PayeeImportIssueDto> Errors,
    IReadOnlyCollection<PayeeImportIssueDto> SkippedRows,
    string Sha256)
{
    /// <summary>Whether the preview can be confirmed without any blocking validation errors.</summary>
    public bool CanImport => WorksheetFound && Errors.Count == 0;
}

/// <summary>Committed result for an original YoFi Payee worksheet import.</summary>
/// <param name="Created">Number of new payee matching rules committed.</param>
/// <param name="Skipped">Number of blank-category rows skipped.</param>
/// <param name="Updated">Always zero; this legacy format has no V3 Keys.</param>
/// <param name="Failed">Always zero on success; a failed worksheet throws and commits no rows.</param>
public record PayeeImportResultDto(int Created, int Skipped, int Updated, int Failed);
