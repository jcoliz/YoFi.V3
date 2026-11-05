using ClosedXML.Excel;

namespace YoFi.V3.Application.Services;

/// <summary>Detects the original YoFi workbook format from its legacy Payee worksheet.</summary>
public sealed class OriginalYoFiWorkbookFormatDetector : IWorkbookImportFormatDetector
{
    /// <inheritdoc />
    public string FormatId => "original-yofi";

    /// <inheritdoc />
    public bool CanDetect(XLWorkbook workbook) =>
        workbook.Worksheets.Any(worksheet =>
            string.Equals(worksheet.Name, "Payee", StringComparison.OrdinalIgnoreCase));
}
