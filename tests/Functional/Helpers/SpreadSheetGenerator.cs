using ClosedXML.Excel;

namespace YoFi.V3.Tests.Functional.Helpers;

public static class SpreadSheetGenerator
{
    /// <summary>
    /// Creates an in-memory XLSX workbook with the table's headers and values as text.
    /// </summary>
    public static byte[] GenerateWorkbook(DataTable table, string worksheetName)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(worksheetName);

        for (var column = 0; column < table.ColumnCount; column++)
        {
            worksheet.Cell(1, column + 1).Value = table.Headers[column];
            for (var row = 0; row < table.RowCount; row++)
            {
                worksheet.Cell(row + 2, column + 1).Value = table[row][column];
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
