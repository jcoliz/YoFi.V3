using ClosedXML.Excel;
using YoFi.V3.Application.Exceptions;
using YoFi.V3.Application.Services;

namespace YoFi.V3.Tests.Unit.Application;

/// <summary>Unit tests for split workbook parsing and template generation.</summary>
[TestFixture]
public class TransactionSplitWorkbookServiceTests
{
    private TransactionSplitWorkbookService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new TransactionSplitWorkbookService();
    }

    [Test]
    public void Parse_ValidWorkbook_ReturnsNormalizedRowsInSourceOrder()
    {
        var workbook = CreateWorkbook();
        var worksheet = workbook.Worksheet("Splits");
        worksheet.Cell(1, 1).Value = " category ";
        worksheet.Cell(1, 2).Value = " AMOUNT ";
        worksheet.Cell(1, 3).Value = " memo ";
        worksheet.Cell(2, 1).Value = "  groceries  ";
        worksheet.Cell(2, 2).Value = "12.34";
        worksheet.Cell(2, 3).Value = "  weekly buy  ";
        worksheet.Cell(3, 1).Value = "Travel";
        worksheet.Cell(3, 2).Value = "-2";
        worksheet.Cell(3, 3).Value = "   ";

        var rows = _service.Parse(ToBytes(workbook));

        Assert.That(rows.Count, Is.EqualTo(2));
        Assert.That(rows.First().Amount, Is.EqualTo(12.34m));
        Assert.That(rows.First().Category, Is.EqualTo("Groceries"));
        Assert.That(rows.First().Memo, Is.EqualTo("weekly buy"));
        Assert.That(rows.Last().Amount, Is.EqualTo(-2m));
        Assert.That(rows.Last().Category, Is.EqualTo("Travel"));
        Assert.That(rows.Last().Memo, Is.Null);
    }

    [Test]
    public void Parse_MissingRequiredHeader_ThrowsGroupedValidation()
    {
        var workbook = CreateWorkbook();
        var worksheet = workbook.Worksheet("Splits");
        worksheet.Cell(1, 1).Value = "Memo";

        var exception = Assert.Throws<ValidationProblemsException>(
            () => _service.Parse(ToBytes(workbook)));

        Assert.That(exception!.Title, Is.EqualTo("The split workbook failed validation."));
        Assert.That(exception.Errors.Keys, Contains.Item("Row 1: Header"));
        Assert.That(exception.Errors.Values.SelectMany(messages => messages),
            Has.Some.Contains("required"));
    }

    [Test]
    public void Parse_ZeroOnlyWorkbook_ThrowsEmptyImport()
    {
        var workbook = CreateWorkbook();
        var worksheet = workbook.Worksheet("Splits");
        worksheet.Cell(1, 1).Value = "Category";
        worksheet.Cell(1, 2).Value = "Amount";
        worksheet.Cell(2, 1).Value = "Food";
        worksheet.Cell(2, 2).Value = "0";

        var exception = Assert.Throws<ValidationProblemsException>(
            () => _service.Parse(ToBytes(workbook)));

        Assert.That(exception!.Errors.Values.SelectMany(messages => messages),
            Has.Some.Contains("does not contain any non-zero"));
    }

    [Test]
    public void Parse_201NonEmptyRows_ReportsRowLimit()
    {
        var workbook = CreateWorkbook();
        var worksheet = workbook.Worksheet("Splits");
        worksheet.Cell(1, 1).Value = "Category";
        worksheet.Cell(1, 2).Value = "Amount";

        for (var row = 2; row <= 202; row++)
        {
            worksheet.Cell(row, 1).Value = "Category";
            worksheet.Cell(row, 2).Value = "1";
        }

        var exception = Assert.Throws<ValidationProblemsException>(
            () => _service.Parse(ToBytes(workbook)));

        Assert.That(exception!.Errors.Values.SelectMany(messages => messages),
            Has.Some.Contains("more than 200"));
    }

    [Test]
    public void Parse_InvalidWorkbook_ThrowsGroupedValidation()
    {
        var exception = Assert.Throws<ValidationProblemsException>(
            () => _service.Parse([1, 2, 3, 4]));

        Assert.That(exception!.Errors.Values.SelectMany(messages => messages),
            Has.Some.Contains("not a readable XLSX workbook"));
    }

    [Test]
    public void CreateTemplate_ReturnsNamedWorkbookWithHeadersAndNoDataRows()
    {
        var result = _service.CreateTemplate();

        Assert.That(result.FileName, Is.EqualTo("yofi-transaction-splits-template.xlsx"));
        using var workbook = new XLWorkbook(new MemoryStream(result.Content));
        var worksheet = workbook.Worksheet("Splits");

        Assert.That(worksheet.Cell(1, 1).GetString(), Is.EqualTo("Category"));
        Assert.That(worksheet.Cell(1, 2).GetString(), Is.EqualTo("Amount"));
        Assert.That(worksheet.Cell(1, 3).GetString(), Is.EqualTo("Memo"));
        Assert.That(worksheet.LastRowUsed()!.RowNumber(), Is.EqualTo(1));

        var exception = Assert.Throws<ValidationProblemsException>(
            () => _service.Parse(result.Content));
        Assert.That(exception!.Errors.Values.SelectMany(messages => messages),
            Has.Some.Contains("does not contain any non-zero"));
    }

    private static XLWorkbook CreateWorkbook()
    {
        var workbook = new XLWorkbook();
        workbook.AddWorksheet("Splits");
        return workbook;
    }

    private static byte[] ToBytes(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        workbook.Dispose();
        return stream.ToArray();
    }
}
