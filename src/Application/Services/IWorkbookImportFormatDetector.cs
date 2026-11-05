using ClosedXML.Excel;

namespace YoFi.V3.Application.Services;

/// <summary>Detects one source format from workbook-level structure.</summary>
public interface IWorkbookImportFormatDetector
{
    /// <summary>Stable format identifier returned in import previews.</summary>
    string FormatId { get; }

    /// <summary>Returns whether this detector recognizes the workbook's source format.</summary>
    bool CanDetect(XLWorkbook workbook);
}
