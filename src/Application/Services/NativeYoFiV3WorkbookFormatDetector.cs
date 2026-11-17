using ClosedXML.Excel;

namespace YoFi.V3.Application.Services;

/// <summary>Detects a native YoFi.V3 workbook from an authoritative Metadata Format marker.</summary>
public sealed class NativeYoFiV3WorkbookFormatDetector : IWorkbookImportFormatDetector
{
    /// <inheritdoc />
    public string FormatId => "yofi.v3";

    /// <inheritdoc />
    public bool CanDetect(XLWorkbook workbook)
    {
        var worksheet = workbook.Worksheets.FirstOrDefault(item =>
            string.Equals(item.Name, "Metadata", StringComparison.OrdinalIgnoreCase));
        if (worksheet is null) return false;

        var headers = worksheet.Row(1).CellsUsed()
            .Select(cell => (Name: cell.GetString().Trim(), Column: cell.Address.ColumnNumber))
            .ToArray();
        var nameColumn = headers.Where(header => string.Equals(header.Name, "Name", StringComparison.OrdinalIgnoreCase))
            .Select(header => header.Column).ToArray();
        var valueColumn = headers.Where(header => string.Equals(header.Name, "Value", StringComparison.OrdinalIgnoreCase))
            .Select(header => header.Column).ToArray();
        if (nameColumn.Length != 1 || valueColumn.Length != 1) return false;

        var markers = new List<string>();
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var row = 2; row <= lastRow; row++)
        {
            if (!worksheet.Row(row).IsEmpty() &&
                string.Equals(worksheet.Cell(row, nameColumn[0]).GetString().Trim(), "Format", StringComparison.OrdinalIgnoreCase))
            {
                markers.Add(worksheet.Cell(row, valueColumn[0]).GetString().Trim());
            }
        }

        return markers.Count == 1 && string.Equals(markers[0], "YoFi.V3", StringComparison.Ordinal);
    }
}
