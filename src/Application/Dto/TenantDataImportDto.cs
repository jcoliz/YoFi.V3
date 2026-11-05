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
