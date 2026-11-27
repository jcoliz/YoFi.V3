using ClosedXML.Excel;
using YoFi.V3.Application.Services;

namespace YoFi.V3.Tests.Unit.Application.TenantDataAdmin;

[TestFixture]
public class OriginalYoFiSplitWorksheetHandlerTests
{
    private OriginalYoFiSplitWorksheetHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _handler = new OriginalYoFiSplitWorksheetHandler();
    }

    [Test]
    public void Metadata_IdentifiesLegacySplitWorksheet()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_handler.FormatId, Is.EqualTo("original-yofi"));
            Assert.That(_handler.WorksheetName, Is.EqualTo("Split"));
            Assert.That(_handler.DataType, Is.EqualTo("Splits"));
            Assert.That(_handler.DependencyOrder, Is.EqualTo(300));
            Assert.That(_handler.RequiresWorksheetNames, Is.EqualTo(["Transaction"]));
        }
    }

    [Test]
    public void Preview_ValidRowsAcceptsMemoAndReportsDistinctIgnoredColumns()
    {
        using var workbook = Workbook(
            ["ID", "Amount", "Category", "Memo", "TransactionID", "Extra", "extra"],
            [
                [1, 12.50m, " food : dining ", "  lunch  ", 101, "a", "b"],
                [2, -4m, "Refund", "", 102, "c", "d"]
            ]);

        var preview = _handler.Preview(workbook.Worksheet("Split"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(preview.ValidRecordCount, Is.EqualTo(2));
            Assert.That(preview.Errors, Is.Empty);
            Assert.That(preview.IgnoredColumns, Is.EqualTo(["Extra"]));
            Assert.That(preview.CanImport, Is.True);
        }
    }

    [Test]
    public void Preview_MissingRequiredHeadersReportsAllErrors()
    {
        using var workbook = Workbook(["ID", "Memo", "Extra"], []);

        var preview = _handler.Preview(workbook.Worksheet("Split"));

        Assert.That(preview.Errors.Select(error => (error.Field, error.Code)), Is.EquivalentTo(new[]
        {
            ("Amount", "missing_header"),
            ("Category", "missing_header"),
            ("TransactionID", "missing_header")
        }));
    }

    [Test]
    public void Preview_InvalidAmountReportsRowError()
    {
        using var workbook = Workbook(["Amount", "Category", "TransactionID"],
            [["not-decimal", "Food", 101]]);

        var error = _handler.Preview(workbook.Worksheet("Split")).Errors.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(error.RowNumber, Is.EqualTo(2));
            Assert.That(error.Field, Is.EqualTo("Amount"));
            Assert.That(error.Code, Is.EqualTo("invalid_value"));
        }
    }

    [Test]
    public void Preview_InvalidTransactionIdReportsRowError()
    {
        using var workbook = Workbook(["Amount", "Category", "TransactionID"],
            [[12m, "Food", "not-a-legacy-id"]]);

        var error = _handler.Preview(workbook.Worksheet("Split")).Errors.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(error.RowNumber, Is.EqualTo(2));
            Assert.That(error.Field, Is.EqualTo("TransactionID"));
            Assert.That(error.Code, Is.EqualTo("invalid_value"));
        }
    }

    [Test]
    public void Preview_DtoValidationReportsAmountCategoryAndMemoErrors()
    {
        using var workbook = Workbook(["Amount", "Category", "Memo", "TransactionID"],
            [[1.234m, new string('c', 201), new string('m', 501), 101]]);

        var errors = _handler.Preview(workbook.Worksheet("Split")).Errors;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(errors.Any(error => error.Field == "Amount"), Is.True);
            Assert.That(errors.Any(error => error.Field == "Category"), Is.True);
            Assert.That(errors.Any(error => error.Field == "Memo"), Is.True);
        }
    }

    [Test]
    public void Preview_BlankRowsAreIgnored()
    {
        using var workbook = Workbook(["Amount", "Category", "TransactionID"], []);
        var worksheet = workbook.Worksheet("Split");
        worksheet.Row(2).Clear(XLClearOptions.All);

        var preview = _handler.Preview(worksheet);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(preview.ValidRecordCount, Is.Zero);
            Assert.That(preview.Errors, Is.Empty);
        }
    }

    [Test]
    public void Preview_NumericAndNegativeTransactionIdsAreAccepted()
    {
        using var workbook = Workbook(["Amount", "Category", "TransactionID"],
            [[1m, "Food", -101], [2m, "Dining", 101]]);

        var preview = _handler.Preview(workbook.Worksheet("Split"));

        Assert.That(preview.CanImport, Is.True);
        Assert.That(preview.ValidRecordCount, Is.EqualTo(2));
    }

    [Test]
    public async Task Import_InvalidWorksheetThrowsBeforePersistence()
    {
        using var workbook = Workbook(["Amount"], []);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.ImportAsync(workbook.Worksheet("Split"), CancellationToken.None));

        Assert.That(exception!.Message, Does.Contain("validated by the import coordinator"));
    }

    [Test]
    public async Task Import_ValidWorksheetReturnsZeroPersistenceCounts()
    {
        using var workbook = Workbook(["Amount", "Category", "Memo", "TransactionID"],
            [[12m, "Food", " lunch ", 101]]);

        var result = await _handler.ImportAsync(workbook.Worksheet("Split"), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.WorksheetName, Is.EqualTo("Split"));
            Assert.That(result.DataType, Is.EqualTo("Splits"));
            Assert.That(result.Created, Is.Zero);
            Assert.That(result.Updated, Is.Zero);
            Assert.That(result.Skipped, Is.Zero);
            Assert.That(result.Failed, Is.Zero);
        }
    }

    [Test]
    public async Task Import_CanceledValidWorksheetStillReturnsSuccess()
    {
        using var workbook = Workbook(["Amount", "Category", "TransactionID"],
            [[12m, "Food", 101]]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = await _handler.ImportAsync(workbook.Worksheet("Split"), cancellation.Token);

        Assert.That(result.Created, Is.Zero);
    }

    [Test]
    public void WorkbookContextCanBeSet()
    {
        using var workbook = Workbook(["Amount", "Category", "TransactionID"],
            [[12m, "Food", 101]]);

        Assert.DoesNotThrow(() =>
            ((IWorkbookImportContextHandler)_handler).SetWorkbookContext(workbook));
    }

    private static XLWorkbook Workbook(IEnumerable<object?> headers, IEnumerable<object?[]> rows)
    {
        var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Split");
        SetRow(worksheet, 1, headers.ToArray());
        var rowNumber = 2;
        foreach (var row in rows)
        {
            SetRow(worksheet, rowNumber++, row);
        }
        return workbook;
    }

    private static void SetRow(IXLWorksheet worksheet, int rowNumber, IReadOnlyList<object?> values)
    {
        for (var index = 0; index < values.Count; index++)
        {
            worksheet.Cell(rowNumber, index + 1).Value = XLCellValue.FromObject(values[index]);
        }
    }
}
