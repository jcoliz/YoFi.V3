using ClosedXML.Excel;

namespace YoFi.V3.Application.Services;

/// <summary>Detects an original YoFi workbook from recognized legacy worksheet signatures.</summary>
public sealed class OriginalYoFiWorkbookFormatDetector : IWorkbookImportFormatDetector
{
    /// <inheritdoc />
    public string FormatId => "original-yofi";

    /// <inheritdoc />
    public bool CanDetect(XLWorkbook workbook) =>
        workbook.Worksheets.Any(HasPayeeSignature)
        || workbook.Worksheets.Any(HasTransactionSignature)
        || workbook.Worksheets.Any(HasSplitSignature);

    private static bool HasPayeeSignature(IXLWorksheet worksheet)
    {
        if (!string.Equals(worksheet.Name, "Payee", StringComparison.OrdinalIgnoreCase)) return false;
        var headers = HeaderNames(worksheet);
        return headers.Contains("Name") && headers.Contains("Category");
    }

    private static bool HasTransactionSignature(IXLWorksheet worksheet)
    {
        if (!string.Equals(worksheet.Name, "Transaction", StringComparison.OrdinalIgnoreCase)) return false;
        var headers = HeaderNames(worksheet);
        return headers.Contains("Timestamp") && headers.Contains("Payee") && headers.Contains("Amount");
    }

    private static bool HasSplitSignature(IXLWorksheet worksheet)
    {
        if (!string.Equals(worksheet.Name, "Split", StringComparison.OrdinalIgnoreCase)) return false;
        var headers = HeaderNames(worksheet);
        return headers.Contains("Amount") && headers.Contains("Category") && headers.Contains("TransactionID");
    }

    private static HashSet<string> HeaderNames(IXLWorksheet worksheet) => worksheet.Row(1).CellsUsed()
        .Select(cell => cell.GetString().Trim())
        .Where(value => value.Length > 0)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
