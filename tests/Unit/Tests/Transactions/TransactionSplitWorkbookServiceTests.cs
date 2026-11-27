using ClosedXML.Excel;
using YoFi.V3.Application.Services;
using YoFi.V3.Entities.Exceptions;

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

        var exception = Assert.Throws<ValidationException>(
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

        var exception = Assert.Throws<ValidationException>(
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

        var exception = Assert.Throws<ValidationException>(
            () => _service.Parse(ToBytes(workbook)));

        Assert.That(exception!.Errors.Values.SelectMany(messages => messages),
            Has.Some.Contains("more than 200"));
    }

    [Test]
    public void Parse_InvalidWorkbook_ThrowsGroupedValidation()
    {
        var exception = Assert.Throws<ValidationException>(
            () => _service.Parse([1, 2, 3, 4]));

        Assert.That(exception!.Errors.Values.SelectMany(messages => messages),
            Has.Some.Contains("not a readable XLSX workbook"));
    }

    [Test]
    public void Parse_MemoMatchesLaterWorksheet_ReturnsRowsFromMatchedWorksheet()
    {
        var workbook = CreateWorkbook();
        var first = workbook.Worksheet("Splits");
        first.Cell(1, 1).Value = "Memo";

        var paystub = workbook.AddWorksheet(" PayStub ");
        paystub.Cell(1, 1).Value = "Category";
        paystub.Cell(1, 2).Value = "Amount";
        paystub.Cell(2, 1).Value = "Salary";
        paystub.Cell(2, 2).Value = "800";

        var rows = _service.Parse(ToBytes(workbook), "  paystub  ");

        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows.Single().Category, Is.EqualTo("Salary"));
        Assert.That(rows.Single().Amount, Is.EqualTo(800m));
    }

    [Test]
    public void Parse_DuplicateNormalizedMemoMatches_UsesFirstWorksheetInWorkbookOrder()
    {
        var workbook = CreateWorkbook();
        var first = workbook.Worksheet("Splits");
        var firstMatch = workbook.AddWorksheet(" Paystub ");
        firstMatch.Cell(1, 1).Value = "Category";
        firstMatch.Cell(1, 2).Value = "Amount";
        firstMatch.Cell(2, 1).Value = "First match";
        firstMatch.Cell(2, 2).Value = "10";

        var secondMatch = workbook.AddWorksheet("paystub");
        secondMatch.Cell(1, 1).Value = "Category";
        secondMatch.Cell(1, 2).Value = "Amount";
        secondMatch.Cell(2, 1).Value = "Second match";
        secondMatch.Cell(2, 2).Value = "20";

        var rows = _service.Parse(ToBytes(workbook), "PAYSTUB");

        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows.Single().Category, Is.EqualTo("First Match"));
        Assert.That(rows.Single().Amount, Is.EqualTo(10m));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("Missing memo")]
    public void Parse_BlankOrMissingMemoMatch_FallsBackToFirstWorksheet(string? transactionMemo)
    {
        var workbook = CreateWorkbook();
        var first = workbook.Worksheet("Splits");
        first.Cell(1, 1).Value = "Category";
        first.Cell(1, 2).Value = "Amount";
        first.Cell(2, 1).Value = "Fallback";
        first.Cell(2, 2).Value = "30";

        workbook.AddWorksheet("Other sheet").Cell(1, 1).Value = "Memo";

        var rows = _service.Parse(ToBytes(workbook), transactionMemo);

        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows.Single().Category, Is.EqualTo("Fallback"));
        Assert.That(rows.Single().Amount, Is.EqualTo(30m));
    }

    [Test]
    public void Parse_UnselectedWorksheetWithInvalidData_IsIgnored()
    {
        var workbook = CreateWorkbook();
        var first = workbook.Worksheet("Splits");
        first.Cell(1, 1).Value = "Memo";
        first.Cell(2, 1).Value = "Invalid unselected row";

        var matched = workbook.AddWorksheet("Paystub");
        matched.Cell(1, 1).Value = "Category";
        matched.Cell(1, 2).Value = "Amount";
        matched.Cell(2, 1).Value = "Selected";
        matched.Cell(2, 2).Value = "40";

        var rows = _service.Parse(ToBytes(workbook), "paystub");

        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows.Single().Category, Is.EqualTo("Selected"));
        Assert.That(rows.Single().Amount, Is.EqualTo(40m));
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

        var exception = Assert.Throws<ValidationException>(
            () => _service.Parse(result.Content));
        Assert.That(exception!.Errors.Values.SelectMany(messages => messages),
            Has.Some.Contains("does not contain any non-zero"));
    }

    [Test]
    public void Parse_NullContent_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _service.Parse(null!));
    }

    [Test]
    public void Parse_DuplicateHeaders_ThrowsGroupedValidation()
    {
        var workbook = CreateWorkbook();
        var worksheet = workbook.Worksheet("Splits");
        worksheet.Cell(1, 1).Value = "Category";
        worksheet.Cell(1, 2).Value = "Category";
        worksheet.Cell(1, 3).Value = "Amount";
        worksheet.Cell(1, 4).Value = "Amount";

        var exception = Assert.Throws<ValidationException>(
            () => _service.Parse(ToBytes(workbook)));

        Assert.That(exception!.Errors.Values.SelectMany(messages => messages),
            Has.Some.Contains("appears more than once"));
    }

    [Test]
    public void Parse_InvalidFormulaAndTextAmountRows_ReportDistinctErrors()
    {
        var workbook = CreateWorkbook();
        var worksheet = workbook.Worksheet("Splits");
        worksheet.Cell(1, 1).Value = "Category";
        worksheet.Cell(1, 2).Value = "Amount";
        worksheet.Cell(2, 1).Value = "Formula";
        worksheet.Cell(2, 2).FormulaA1 = "1/0";
        worksheet.Cell(3, 1).Value = "Text";
        worksheet.Cell(3, 2).Value = "not-a-decimal";

        var exception = Assert.Throws<ValidationException>(
            () => _service.Parse(ToBytes(workbook)));

        Assert.That(exception!.Errors.Keys, Has.Some.EqualTo("Row 2: Amount"));
        Assert.That(exception.Errors.Keys, Has.Some.EqualTo("Row 3: Amount"));
        Assert.That(exception.Errors.Values.SelectMany(messages => messages),
            Has.Some.Contains("formula"));
        Assert.That(exception.Errors.Values.SelectMany(messages => messages),
            Has.Some.Contains("decimal"));
    }

    [Test]
    public void Parse_CategoryAndMemoFormulaWithoutCachedText_ReportFormulaErrors()
    {
        var workbook = CreateWorkbook();
        var worksheet = workbook.Worksheet("Splits");
        worksheet.Cell(1, 1).Value = "Category";
        worksheet.Cell(1, 2).Value = "Amount";
        worksheet.Cell(1, 3).Value = "Memo";
        worksheet.Cell(2, 1).FormulaA1 = "1+1";
        worksheet.Cell(2, 2).Value = 1m;
        worksheet.Cell(2, 3).FormulaA1 = "2+2";

        var exception = Assert.Throws<ValidationException>(
            () => _service.Parse(ToBytes(workbook)));

        Assert.That(exception!.Errors.Keys, Has.Some.EqualTo("Row 2: Category"));
        Assert.That(exception.Errors.Keys, Has.Some.EqualTo("Row 2: Memo"));
        Assert.That(exception.Errors.Values.SelectMany(messages => messages).Count(message => message.Contains("cached text")),
            Is.EqualTo(2));
    }

    [Test]
    public void Parse_CategoryAndMemoOutsideLimits_ReportValidationErrors()
    {
        var workbook = CreateWorkbook();
        var worksheet = workbook.Worksheet("Splits");
        worksheet.Cell(1, 1).Value = "Category";
        worksheet.Cell(1, 2).Value = "Amount";
        worksheet.Cell(1, 3).Value = "Memo";
        worksheet.Cell(2, 1).Value = new string('c', 201);
        worksheet.Cell(2, 2).Value = 1m;
        worksheet.Cell(2, 3).Value = new string('m', 501);

        var exception = Assert.Throws<ValidationException>(
            () => _service.Parse(ToBytes(workbook)));

        Assert.That(exception!.Errors.Values.SelectMany(messages => messages),
            Has.Some.Contains("Category cannot exceed"));
        Assert.That(exception.Errors.Values.SelectMany(messages => messages),
            Has.Some.Contains("Memo cannot exceed"));
    }

    [Test]
    public void Parse_Exactly200NonEmptyRows_IsAccepted()
    {
        var workbook = CreateWorkbook();
        var worksheet = workbook.Worksheet("Splits");
        worksheet.Cell(1, 1).Value = "Category";
        worksheet.Cell(1, 2).Value = "Amount";

        for (var row = 2; row <= 201; row++)
        {
            worksheet.Cell(row, 1).Value = "Category";
            worksheet.Cell(row, 2).Value = "1";
        }

        var rows = _service.Parse(ToBytes(workbook));

        Assert.That(rows, Has.Count.EqualTo(200));
    }

    [Test]
    public void Parse_UnrecognizedExtraHeader_IsIgnored()
    {
        var workbook = CreateWorkbook();
        var worksheet = workbook.Worksheet("Splits");
        worksheet.Cell(1, 1).Value = "Category";
        worksheet.Cell(1, 2).Value = "Amount";
        worksheet.Cell(1, 3).Value = "Unknown";
        worksheet.Cell(2, 1).Value = "Food";
        worksheet.Cell(2, 2).Value = 1m;
        worksheet.Cell(2, 3).Value = "ignored";

        var rows = _service.Parse(ToBytes(workbook));

        Assert.That(rows, Has.Count.EqualTo(1));
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
