using ClosedXML.Excel;
using DotNetNuxt.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Services;
using YoFi.V3.Entities.Models;
using YoFi.V3.Tests.Unit.TestHelpers;

namespace YoFi.V3.Tests.Unit.Application.DataImport;

[TestFixture]
public class NativeYoFiSplitWorksheetHandlerTests
{
    private static readonly Guid TransactionKey = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private Mock<IDataProvider> _data = null!;
    private MemoryCache _cache = null!;
    private NativeYoFiSplitWorksheetHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _data = new Mock<IDataProvider>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        var tenant = new TestTenantProvider();
        tenant.CurrentTenant.Id = 42;
        var service = new DataImportService(tenant, _data.Object, _cache, new RegexValidationService(),
            TimeProvider.System);
        _handler = new NativeYoFiSplitWorksheetHandler(service);
    }

    [TearDown]
    public void TearDown() => _cache.Dispose();

    [Test]
    public void Metadata_IdentifiesNativeSplitWorksheet()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_handler.FormatId, Is.EqualTo("yofi.v3"));
            Assert.That(_handler.WorksheetName, Is.EqualTo("Splits"));
            Assert.That(_handler.DataType, Is.EqualTo("Splits"));
            Assert.That(_handler.DependencyOrder, Is.EqualTo(300));
            Assert.That(_handler.RequiresWorksheetNames, Is.EqualTo(["Transactions"]));
        }
    }

    [Test]
    public void Preview_ValidRowsMapsFieldsAndReportsDistinctIgnoredColumns()
    {
        using var workbook = Workbook(
            ["Key", "TransactionKey", "Amount", "Category", "Memo", "Order", "Extra", "extra"],
            [
                [Guid.NewGuid().ToString(), TransactionKey.ToString(), 12.34m, " food : dining ", "  lunch  ", "2", "a", "b"],
                ["", TransactionKey.ToString(), -5m, "", "   ", "0", "c", "d"],
                [null, TransactionKey.ToString(), 1m, "Transfer", "note", "1", "e", "f"]
            ]);

        var preview = _handler.Preview(workbook.Worksheet("Splits"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(preview.WorksheetName, Is.EqualTo("Splits"));
            Assert.That(preview.DataType, Is.EqualTo("Splits"));
            Assert.That(preview.ValidRecordCount, Is.EqualTo(3));
            Assert.That(preview.Errors, Is.Empty);
            Assert.That(preview.IgnoredColumns, Is.EqualTo(["Extra"]));
            Assert.That(preview.CanImport, Is.True);
        }
    }

    [Test]
    public void Preview_MissingRequiredHeadersReportsEachMissingColumn()
    {
        using var workbook = Workbook(["Key"], []);

        var preview = _handler.Preview(workbook.Worksheet("Splits"));

        Assert.That(preview.Errors.Select(error => (error.Field, error.Code)), Is.EquivalentTo(new[]
        {
            ("TransactionKey", "missing_header"),
            ("Amount", "missing_header"),
            ("Category", "missing_header"),
            ("Memo", "missing_header"),
            ("Order", "missing_header")
        }));
    }

    [TestCase("Key")]
    [TestCase("TransactionKey")]
    public void Preview_DuplicateKeyHeaderReportsError(string duplicatedHeader)
    {
        var headers = new[] { "TransactionKey", "Amount", "Category", "Memo", "Order", "Key" }.ToList();
        headers.Add(duplicatedHeader);
        using var workbook = Workbook(headers, []);

        var error = _handler.Preview(workbook.Worksheet("Splits")).Errors.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(error.RowNumber, Is.EqualTo(1));
            Assert.That(error.Field, Is.EqualTo("Key"));
            Assert.That(error.Code, Is.EqualTo("duplicate_header"));
        }
    }

    [Test]
    public void Preview_InvalidRowValuesReportsCodesAndContinues()
    {
        var duplicateKey = Guid.NewGuid();
        using var workbook = Workbook(RequiredHeaders,
        [
            ["not-a-guid", TransactionKey.ToString(), 1m, "Food", "memo", 0],
            [Guid.Empty.ToString(), TransactionKey.ToString(), 1m, "Food", "memo", 0],
            [duplicateKey.ToString(), TransactionKey.ToString(), 1m, "Food", "memo", 0],
            [duplicateKey.ToString(), TransactionKey.ToString(), 1m, "Food", "memo", 1],
            [Guid.NewGuid().ToString(), "not-a-guid", 1m, "Food", "memo", 1],
            [Guid.NewGuid().ToString(), TransactionKey.ToString(), "not-decimal", "Food", "memo", 1],
            [Guid.NewGuid().ToString(), TransactionKey.ToString(), 1m, "Food", "memo", "invalid"],
            [Guid.NewGuid().ToString(), TransactionKey.ToString(), 1m, "Food", "memo", -1],
            [null, TransactionKey.ToString(), 1m, "Food", "memo", 0]
        ]);

        var preview = _handler.Preview(workbook.Worksheet("Splits"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(preview.ValidRecordCount, Is.EqualTo(1));
            Assert.That(preview.Errors.Select(error => (error.RowNumber, error.Field, error.Code)), Is.EquivalentTo(new[]
            {
                (2, "Key", "invalid_or_duplicate_key"),
                (3, "Key", "invalid_or_duplicate_key"),
                (5, "Key", "invalid_or_duplicate_key"),
                (6, "TransactionKey", "invalid_guid"),
                (7, "Amount", "invalid_value"),
                (8, "Order", "invalid_value"),
                (9, "Order", "invalid_value"),
                (10, "Order", "duplicate_order")
            }));
        }
    }

    [Test]
    public void Preview_ValidationFailuresReportAllFieldErrors()
    {
        using var workbook = Workbook(RequiredHeaders,
        [[Guid.NewGuid().ToString(), TransactionKey.ToString(), 1.234m, new string('x', 201),
            new string('m', 501), 0]]);

        var errors = _handler.Preview(workbook.Worksheet("Splits")).Errors;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(errors.Select(error => error.Field), Does.Contain("Amount"));
            Assert.That(errors.Select(error => error.Field), Does.Contain("Category"));
            Assert.That(errors.Select(error => error.Field), Does.Contain("Memo"));
            Assert.That(errors.All(error => error.Code == "invalid_value"), Is.True);
        }
    }

    [Test]
    public void Preview_BlankRowsAreIgnored()
    {
        using var workbook = Workbook(RequiredHeaders, []);
        workbook.Worksheet("Splits").Cell(3, 1).Clear(XLClearOptions.All);

        var preview = _handler.Preview(workbook.Worksheet("Splits"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(preview.ValidRecordCount, Is.Zero);
            Assert.That(preview.Errors, Is.Empty);
        }
    }

    [Test]
    public void Preview_WorkbookContextRejectsSplitTotalAndTransactionReferenceMismatches()
    {
        var ordinaryTransactionKey = Guid.NewGuid();
        using var workbook = Workbook(RequiredHeaders,
        [
            [null, TransactionKey.ToString(), 4m, "One", null, 0],
            [null, ordinaryTransactionKey.ToString(), 6m, "Two", null, 0],
            [null, Guid.NewGuid().ToString(), 1m, "Three", null, 0],
            [null, TransactionKey.ToString(), 3m, "Four", null, 1]
        ]);
        AddTransactionWorksheet(workbook,
        [
            [TransactionKey.ToString(), 10m, " SPLIT "],
            [ordinaryTransactionKey.ToString(), 6m, "Food"],
            ["invalid-guid", "invalid-amount", "SPLIT"],
            ["still-invalid", 2m, "SPLIT"]
        ]);
        ((IWorkbookImportContextHandler)_handler).SetWorkbookContext(workbook);

        var errors = _handler.Preview(workbook.Worksheet("Splits")).Errors;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(errors.Any(error => error.Code == "split_sum_mismatch"), Is.True);
            Assert.That(errors.Any(error => error.Code == "non_split_transaction"), Is.True);
            Assert.That(errors.Any(error => error.Code == "transaction_key_mismatch"), Is.True);
        }
    }

    [Test]
    public void Preview_WorkbookWithoutUsableTransactionHeadersAddsReferenceErrorsOnly()
    {
        using var workbook = Workbook(RequiredHeaders,
            [[null, TransactionKey.ToString(), 1m, "Food", null, 0]]);
        workbook.AddWorksheet("Transactions").Cell(1, 1).Value = "Not Key";
        ((IWorkbookImportContextHandler)_handler).SetWorkbookContext(workbook);

        var error = _handler.Preview(workbook.Worksheet("Splits")).Errors.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(error.Code, Is.EqualTo("transaction_key_mismatch"));
            Assert.That(error.RowNumber, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task Import_InvalidWorksheetThrowsBeforePersistence()
    {
        using var workbook = Workbook(["Amount"], []);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.ImportAsync(workbook.Worksheet("Splits"), CancellationToken.None));

        Assert.That(exception!.Message, Does.Contain("validated by the import coordinator"));
        _data.Verify(data => data.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Import_ValidWorksheetCreatesAndUpdatesSplitsAndReturnsCounts()
    {
        var existingKey = Guid.NewGuid();
        var transaction = new Transaction { Id = 7, Key = TransactionKey, TenantId = 42 };
        var existingSplit = new Split
        {
            Key = existingKey,
            TransactionId = transaction.Id,
            Amount = 9m,
            Category = "Old",
            Order = 9
        };
        transaction.Splits.Add(existingSplit);
        var transactions = new List<Transaction> { transaction };
        var splits = new List<Split> { existingSplit };
        _data.Setup(data => data.Get<Transaction>()).Returns(transactions.AsQueryable());
        _data.Setup(data => data.Get<Split>()).Returns(splits.AsQueryable());
        _data.Setup(data => data.ToListAsync(It.IsAny<IQueryable<Transaction>>()))
            .Returns((IQueryable<Transaction> query) => Task.FromResult(query.ToList()));
        _data.Setup(data => data.ToListAsync(It.IsAny<IQueryable<Split>>()))
            .Returns((IQueryable<Split> query) => Task.FromResult(query.ToList()));
        _data.Setup(data => data.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        using var workbook = Workbook(RequiredHeaders,
        [
            [existingKey.ToString(), TransactionKey.ToString(), 7m, "food: takeout", " updated ", 0],
            [null, TransactionKey.ToString(), 3m, "Dining", null, 1]
        ]);

        var result = await _handler.ImportAsync(workbook.Worksheet("Splits"), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Created, Is.EqualTo(1));
            Assert.That(result.Updated, Is.EqualTo(1));
            Assert.That(result.Skipped, Is.Zero);
            Assert.That(result.Failed, Is.Zero);
            Assert.That(existingSplit.Amount, Is.EqualTo(7m));
            Assert.That(existingSplit.Category, Is.EqualTo("Food:Takeout"));
            Assert.That(existingSplit.Memo, Is.EqualTo("updated"));
            Assert.That(existingSplit.Order, Is.Zero);
        }
        _data.Verify(data => data.Add(It.IsAny<Split>()), Times.Once);
        _data.Verify(data => data.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Import_CanceledValidWorksheetObservesCancellation()
    {
        using var workbook = Workbook(RequiredHeaders,
            [[null, TransactionKey.ToString(), 1m, "Food", null, 0]]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _handler.ImportAsync(workbook.Worksheet("Splits"), cancellation.Token));
    }

    private static readonly string[] RequiredHeaders =
        ["Key", "TransactionKey", "Amount", "Category", "Memo", "Order"];

    private static XLWorkbook Workbook(IEnumerable<object?> headers, IEnumerable<object?[]> rows)
    {
        var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Splits");
        SetRow(worksheet, 1, headers.ToArray());
        var rowNumber = 2;
        foreach (var row in rows)
        {
            SetRow(worksheet, rowNumber++, row);
        }
        return workbook;
    }

    private static void AddTransactionWorksheet(XLWorkbook workbook, IEnumerable<object?[]> rows)
    {
        var worksheet = workbook.AddWorksheet("Transactions");
        SetRow(worksheet, 1, ["Key", "Amount", "Category"]);
        var rowNumber = 2;
        foreach (var row in rows)
        {
            SetRow(worksheet, rowNumber++, row);
        }
    }

    private static void SetRow(IXLWorksheet worksheet, int rowNumber, IReadOnlyList<object?> values)
    {
        for (var index = 0; index < values.Count; index++)
        {
            worksheet.Cell(rowNumber, index + 1).Value = XLCellValue.FromObject(values[index]);
        }
    }
}
