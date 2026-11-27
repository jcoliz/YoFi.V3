using ClosedXML.Excel;
using YoFi.V3.Application.Services;

namespace YoFi.V3.Tests.Unit.Application.TenantDataAdmin;

[TestFixture]
public class NativeYoFiV3WorkbookFormatDetectorTests
{
    private NativeYoFiV3WorkbookFormatDetector _detector = null!;

    [SetUp]
    public void SetUp()
    {
        _detector = new NativeYoFiV3WorkbookFormatDetector();
    }

    [Test]
    public void FormatId_IsNativeV3()
    {
        Assert.That(_detector.FormatId, Is.EqualTo("yofi.v3"));
    }

    [Test]
    public void CanDetect_NativeMetadataMarkerIsRecognized()
    {
        using var workbook = Workbook("Metadata", ["Name", "Value"],
            [["Format", "YoFi.V3"]]);

        Assert.That(_detector.CanDetect(workbook), Is.True);
    }

    [Test]
    public void CanDetect_HeaderNamesAndMetadataWorksheetAreCaseInsensitive()
    {
        using var workbook = Workbook("METADATA", [" name ", " value "],
            [[" format ", "YoFi.V3"]]);

        Assert.That(_detector.CanDetect(workbook), Is.True);
    }

    [Test]
    public void CanDetect_TrimmedMarkerIsRecognized()
    {
        using var workbook = Workbook("Metadata", ["Name", "Value"],
            [["Format", "  YoFi.V3  "]]);

        Assert.That(_detector.CanDetect(workbook), Is.True);
    }

    [Test]
    public void CanDetect_MissingMetadataWorksheetReturnsFalse()
    {
        using var workbook = new XLWorkbook();
        workbook.AddWorksheet("Transactions");

        Assert.That(_detector.CanDetect(workbook), Is.False);
    }

    [TestCase("Name")]
    [TestCase("Value")]
    public void CanDetect_MissingOrDuplicateHeaderColumnsReturnFalse(string missingHeader)
    {
        var headers = missingHeader == "Name"
            ? new[] { "Value", "Value" }
            : new[] { "Name", "Name" };
        using var workbook = Workbook("Metadata", headers,
            [["Format", "YoFi.V3"]]);

        Assert.That(_detector.CanDetect(workbook), Is.False);
    }

    [Test]
    public void CanDetect_DuplicateNameHeaderAcrossColumnsReturnsFalse()
    {
        using var workbook = Workbook("Metadata", ["Name", "Name", "Value"],
            [["Format", "other", "YoFi.V3"]]);

        Assert.That(_detector.CanDetect(workbook), Is.False);
    }

    [Test]
    public void CanDetect_DuplicateValueHeaderAcrossColumnsReturnsFalse()
    {
        using var workbook = Workbook("Metadata", ["Name", "Value", "Value"],
            [["Format", "YoFi.V3", "YoFi.V3"]]);

        Assert.That(_detector.CanDetect(workbook), Is.False);
    }

    [Test]
    public void CanDetect_NoFormatRowReturnsFalse()
    {
        using var workbook = Workbook("Metadata", ["Name", "Value"],
            [["Version", "YoFi.V3"]]);

        Assert.That(_detector.CanDetect(workbook), Is.False);
    }

    [Test]
    public void CanDetect_DuplicateFormatRowsReturnFalse()
    {
        using var workbook = Workbook("Metadata", ["Name", "Value"],
        [
            ["Format", "YoFi.V3"],
            [" format ", "YoFi.V3"]
        ]);

        Assert.That(_detector.CanDetect(workbook), Is.False);
    }

    [Test]
    public void CanDetect_WrongMarkerTextIsRejectedCaseSensitively()
    {
        using var workbook = Workbook("Metadata", ["Name", "Value"],
            [["Format", "yofi.v3"]]);

        Assert.That(_detector.CanDetect(workbook), Is.False);
    }

    [Test]
    public void CanDetect_BlankFormatRowAndOtherMetadataAreRejected()
    {
        using var workbook = Workbook("Metadata", ["Name", "Value"],
        [
            ["Format", ""],
            ["Version", "1"]
        ]);

        Assert.That(_detector.CanDetect(workbook), Is.False);
    }

    private static XLWorkbook Workbook(string worksheetName, IReadOnlyList<string> headers,
        IReadOnlyList<object?[]> rows)
    {
        var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet(worksheetName);
        for (var column = 0; column < headers.Count; column++)
            worksheet.Cell(1, column + 1).Value = headers[column];

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            for (var column = 0; column < row.Length; column++)
                worksheet.Cell(rowIndex + 2, column + 1).Value = XLCellValue.FromObject(row[column]);
        }
        return workbook;
    }
}
