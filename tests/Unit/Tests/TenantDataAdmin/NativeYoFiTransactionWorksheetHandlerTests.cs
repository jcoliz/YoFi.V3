using ClosedXML.Excel;
using DotNetNuxt.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using YoFi.V3.Application.Services;
using YoFi.V3.Entities.Models;
using YoFi.V3.Tests.Unit.TestHelpers;

namespace YoFi.V3.Tests.Unit.Application.TenantDataAdmin;

[TestFixture]
public class NativeYoFiTransactionWorksheetHandlerTests
{
    private static readonly Guid ExistingKey = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly TimeProvider _timeProvider = new FixedTimeProvider();

    private Mock<IDataProvider> _data = null!;
    private MemoryCache _cache = null!;
    private NativeYoFiTransactionWorksheetHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _data = new Mock<IDataProvider>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        var tenant = new TestTenantProvider();
        tenant.CurrentTenant.Id = 42;
        var service = new DataImportService(tenant, _data.Object, _cache, new RegexValidationService(), _timeProvider);
        _handler = new NativeYoFiTransactionWorksheetHandler(service, _timeProvider);
    }

    [TearDown]
    public void TearDown() => _cache.Dispose();

    [Test]
    public void Metadata_IdentifiesNativeTransactionWorksheet()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_handler.FormatId, Is.EqualTo("yofi.v3"));
            Assert.That(_handler.WorksheetName, Is.EqualTo("Transactions"));
            Assert.That(_handler.DataType, Is.EqualTo("Transactions"));
            Assert.That(_handler.DependencyOrder, Is.EqualTo(200));
            Assert.That(_handler.RequiresWorksheetNames, Is.Empty);
        }
    }

    [Test]
    public void Preview_ValidRowsMapOptionalFieldsAndIgnoredColumns()
    {
        using var workbook = Workbook(
            ["Key", "Date", "Payee", "Amount", "Source", "ExternalId", "Memo", "Category", "Extra", "extra"],
            [
                [ExistingKey.ToString(), new DateTime(2026, 6, 15), " Coffee Shop ", 12.50m, " Bank ", " ext-1 ", "  lunch  ", " food : dining ", "a", "b"],
                ["", "2026-06-16", "Refund", -4m, "", "", "", " SPLIT ", "c", "d"]
            ]);

        var preview = _handler.Preview(workbook.Worksheet("Transactions"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(preview.ValidRecordCount, Is.EqualTo(2));
            Assert.That(preview.Errors, Is.Empty);
            Assert.That(preview.IgnoredColumns, Is.EqualTo(["Extra"]));
            Assert.That(preview.CanImport, Is.True);
        }
    }

    [Test]
    public void Preview_MissingRequiredHeadersReportsThreeErrors()
    {
        using var workbook = Workbook(["Key", "Source"], []);

        var preview = _handler.Preview(workbook.Worksheet("Transactions"));

        Assert.That(preview.Errors.Select(error => (error.Field, error.Code)), Is.EquivalentTo(new[]
        {
            ("Date", "missing_header"),
            ("Payee", "missing_header"),
            ("Amount", "missing_header")
        }));
    }

    [Test]
    public void Preview_DuplicateKeyHeaderReportsError()
    {
        using var workbook = Workbook(["Key", "Key", "Date", "Payee", "Amount"], []);

        var error = _handler.Preview(workbook.Worksheet("Transactions")).Errors.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(error.Field, Is.EqualTo("Key"));
            Assert.That(error.Code, Is.EqualTo("duplicate_header"));
        }
    }

    [Test]
    public void Preview_InvalidAndDuplicateKeysReportRowErrors()
    {
        using var workbook = Workbook(["Key", "Date", "Payee", "Amount"],
        [
            ["not-a-guid", new DateTime(2026, 6, 15), "One", 1m],
            [Guid.Empty.ToString(), new DateTime(2026, 6, 15), "Two", 1m],
            [ExistingKey.ToString(), new DateTime(2026, 6, 15), "Three", 1m],
            [ExistingKey.ToString(), new DateTime(2026, 6, 15), "Four", 1m]
        ]);

        var errors = _handler.Preview(workbook.Worksheet("Transactions")).Errors;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(errors.Any(error => error.RowNumber == 2 && error.Code == "invalid_guid"), Is.True);
            Assert.That(errors.Any(error => error.RowNumber == 3 && error.Code == "invalid_guid"), Is.True);
            Assert.That(errors.Any(error => error.RowNumber == 5 && error.Code == "duplicate_key"), Is.True);
        }
    }

    [Test]
    public void Preview_InvalidDatePayeeAndAmountReportFieldErrors()
    {
        using var workbook = Workbook(["Date", "Payee", "Amount"],
        [
            ["not-a-date", "Shop", 1m],
            [new DateTime(2026, 6, 15), "Shop", 1m],
            [new DateTime(2026, 6, 15), "Shop", "not-decimal"]
        ]);
        workbook.Worksheet("Transactions").Cell(3, 2).FormulaA1 = "\"Formula payee\"";

        var errors = _handler.Preview(workbook.Worksheet("Transactions")).Errors;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(errors.Any(error => error.RowNumber == 2 && error.Field == "Date"), Is.True);
            Assert.That(errors.Any(error => error.RowNumber == 3 && error.Field == "Payee"), Is.True);
            Assert.That(errors.Any(error => error.RowNumber == 4 && error.Field == "Amount"), Is.True);
        }
    }

    [Test]
    public void Preview_FormulaFieldsAndBlankRowsAreHandled()
    {
        using var workbook = Workbook(["Date", "Payee", "Amount"], []);
        var worksheet = workbook.Worksheet("Transactions");
        worksheet.Cell(2, 1).FormulaA1 = "TODAY()";
        worksheet.Cell(2, 2).Value = "Shop";
        worksheet.Cell(2, 3).Value = 1m;
        worksheet.Row(3).Clear(XLClearOptions.All);

        var preview = _handler.Preview(worksheet);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(preview.ValidRecordCount, Is.Zero);
            Assert.That(preview.Errors.Single().Field, Is.EqualTo("Date"));
            Assert.That(preview.Errors.Single().Code, Is.EqualTo("invalid_value"));
        }
    }

    [Test]
    public void Preview_DtoValidationReportsAmountPayeeMemoAndCategoryErrors()
    {
        using var workbook = Workbook(["Date", "Payee", "Amount", "Memo", "Source", "ExternalId", "Category"],
        [[new DateTime(2026, 6, 15), new string('p', 201), 0m,
          new string('m', 1001), new string('s', 201), new string('e', 101), new string('c', 201)]]);

        var errors = _handler.Preview(workbook.Worksheet("Transactions")).Errors;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(errors.Any(error => error.Field == "Amount"), Is.True);
            Assert.That(errors.Any(error => error.Field == "Payee"), Is.True);
            Assert.That(errors.Any(error => error.Field == "Memo"), Is.True);
            Assert.That(errors.Any(error => error.Field == "Source"), Is.True);
            Assert.That(errors.Any(error => error.Field == "ExternalId"), Is.True);
            Assert.That(errors.Any(error => error.Field == "Category"), Is.True);
        }
    }

    [Test]
    public async Task Import_InvalidWorksheetThrowsBeforePersistence()
    {
        using var workbook = Workbook(["Date"], []);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.ImportAsync(workbook.Worksheet("Transactions"), CancellationToken.None));

        Assert.That(exception!.Message, Does.Contain("validated by the import coordinator"));
        _data.Verify(data => data.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Import_ValidWorksheetCreatesAndUpdatesTransactionsAndSplits()
    {
        var existingKey = ExistingKey;
        var existing = new Transaction
        {
            Id = 7,
            Key = existingKey,
            TenantId = 42,
            Payee = "Old",
            Amount = 9m,
            Date = new DateOnly(2026, 1, 1)
        };
        existing.Splits.Add(new Split { Key = Guid.NewGuid(), TransactionId = 7, Amount = 9m, Category = "Old", Order = 0 });
        existing.Splits.Add(new Split { Key = Guid.NewGuid(), TransactionId = 7, Amount = 1m, Category = "Extra", Order = 1 });
        var transactions = new List<Transaction> { existing };
        var splits = new List<Split>();
        _data.Setup(data => data.Get<Transaction>()).Returns(transactions.AsQueryable());
        _data.Setup(data => data.Get<Split>()).Returns(splits.AsQueryable());
        _data.Setup(data => data.ToListAsync(It.IsAny<IQueryable<Transaction>>()))
            .Returns((IQueryable<Transaction> query) => Task.FromResult(query.ToList()));
        _data.Setup(data => data.ToListAsync(It.IsAny<IQueryable<Split>>()))
            .Returns((IQueryable<Split> query) => Task.FromResult(query.ToList()));
        _data.Setup(data => data.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        using var workbook = Workbook(["Key", "Date", "Payee", "Amount", "Source", "ExternalId", "Memo", "Category"],
        [
            [existingKey.ToString(), new DateTime(2026, 6, 15), " Coffee ", 12m, " Bank ", " ext-1 ", " memo ", " food:dining "],
            ["", new DateTime(2026, 6, 16), "Refund", -4m, "", "", "", ""]
        ]);

        var result = await _handler.ImportAsync(workbook.Worksheet("Transactions"), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Created, Is.EqualTo(1));
            Assert.That(result.Updated, Is.EqualTo(1));
            Assert.That(result.Skipped, Is.Zero);
            Assert.That(existing.Payee, Is.EqualTo("Coffee"));
            Assert.That(existing.Amount, Is.EqualTo(12m));
            Assert.That(existing.Source, Is.EqualTo("Bank"));
            Assert.That(existing.ExternalId, Is.EqualTo("ext-1"));
            Assert.That(existing.Memo, Is.EqualTo("memo"));
            Assert.That(existing.Splits, Has.Count.EqualTo(1));
            Assert.That(existing.Splits.Single().Amount, Is.EqualTo(12m));
            Assert.That(existing.Splits.Single().Category, Is.EqualTo("Food:Dining"));
        }
        _data.Verify(data => data.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Import_CanceledWorksheetObservesCancellation()
    {
        using var workbook = Workbook(["Date", "Payee", "Amount"],
            [[new DateTime(2026, 6, 15), "Coffee", 3m]]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _handler.ImportAsync(workbook.Worksheet("Transactions"), cancellation.Token));
    }

    private static XLWorkbook Workbook(IEnumerable<object?> headers, IEnumerable<object?[]> rows)
    {
        var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Transactions");
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

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
    }
}
