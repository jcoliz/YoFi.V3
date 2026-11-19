namespace YoFi.V3.Application.Dto;

/// <summary>Result of generating an XLSX split-upload template.</summary>
/// <param name="Content">The generated XLSX workbook bytes.</param>
/// <param name="FileName">The download filename.</param>
public record SplitUploadTemplateResultDto(byte[] Content, string FileName);

/// <summary>One validated, non-zero allocation read from an uploaded workbook.</summary>
/// <param name="RowNumber">One-based source worksheet row number.</param>
/// <param name="Amount">Signed allocation amount.</param>
/// <param name="Category">Sanitized category.</param>
/// <param name="Memo">Optional trimmed memo.</param>
public record SplitUploadRowDto(
    int RowNumber,
    decimal Amount,
    string Category,
    string? Memo);

/// <summary>One workbook, header, or row validation issue.</summary>
/// <param name="RowNumber">One-based row number; workbook issues use row 1.</param>
/// <param name="Field">Field or area that failed validation.</param>
/// <param name="Code">Stable validation issue code.</param>
/// <param name="Message">User-facing issue message.</param>
public record SplitUploadIssueDto(
    int RowNumber,
    string Field,
    string Code,
    string Message);
