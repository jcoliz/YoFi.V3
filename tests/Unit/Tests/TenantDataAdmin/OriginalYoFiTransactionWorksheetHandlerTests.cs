using ClosedXML.Excel;
using DotNetNuxt.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using YoFi.V3.Application.Services;
using YoFi.V3.Entities.Models;
using YoFi.V3.Tests.Unit.TestHelpers;

namespace YoFi.V3.Tests.Unit.Application.TenantDataAdmin;

[TestFixture]
public class OriginalYoFiTransactionWorksheetHandlerTests
{
    private static readonly DateTime TransactionTimestamp = new(2026, 6, 15, 12, 30, 0);
    private readonly TimeProvider _timeProvider = new FixedTimeProvider();

    private Mock<IDataProvider> _data = null!;
    private MemoryCache _cache = null!;
    private OriginalYoFiTransactionWorksheetHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _data = new Mock<IDataProvider>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        var tenant = new TestTenantProvider();
        tenant.CurrentTenant.Id = 42;
        var service = new DataImportService(tenant, _data.Object, _cache, new RegexValidationService(), _timeProvider);
        _handler = new OriginalYoFiTransactionWorksheetHandler(service, _timeProvider);
    }

    [TearDown]
    public void TearDown() => _cache.Dispose();

    [Test]
    public void Metadata_IdentifiesLegacyTransactionWorksheet()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_handler.FormatId, Is.EqualTo("original-yofi"));
            Assert.That(_handler.WorksheetName, Is.EqualTo("Transaction"));
            Assert.That(_handler.DataType, Is.EqualTo("Transactions"));
            Assert.That(_handler.DependencyOrder, Is.EqualTo(200));
            Assert.That(_handler.RequiresWorksheetNames, Is.Empty);
        }
    }

    [Test]
    public void Preview_ValidRowsAcceptDatesAndReportsDistinctIgnoredColumns()
    {
        using var workbook = Workbook(
            [" ID ", "Timestamp", "Payee", "Amount", "Category", "Memo", "BankReference", "Extra", "extra"],
            [
                [12, TransactionTimestamp, " Coffee Shop ", 12.50m, " food : dining ", "  breakfast ", " bank-12 ", "a", "b"],
                [13, "2026-06-16", "Refund", -4m, "Refunds", " ", "", "c", "d"]
            ]);

        var preview = _handler.Preview(workbook.Worksheet("Transaction"));

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
        using var workbook = Workbook(["ID", "Other"], []);

        var preview = _handler.Preview(workbook.Worksheet("Transaction"));

        Assert.That(preview.Errors.Select(error => (error.Field, error.Code)), Is.EquivalentTo(new[]
        {
            ("Timestamp", "missing_header"),
            ("Payee", "missing_header"),
            ("Amount", "missing_header")
        }));
    }

    [Test]
    public void Preview_InvalidRequiredValuesAndValidationReportErrors()
    {
        using var workbook = Workbook(RequiredHeaders,
        [
            ["invalid-date", "Shop", 1m, "Food", "memo", "ref"],
            [TransactionTimestamp, "Shop", "not-decimal", "Food", "memo", "ref"],
            [TransactionTimestamp, "Shop", 0m, "Food", "memo", new string('r', 101)],
            [TransactionTimestamp, new string('p', 201), 2m, new string('c', 201), new string('m', 1001), "ref"]
        ]);

        var errors = _handler.Preview(workbook.Worksheet("Transaction")).Errors;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(errors.Count, Is.GreaterThanOrEqualTo(7));
            Assert.That(errors.Any(error => error.RowNumber == 2 && error.Field == "Timestamp/Payee/Amount"), Is.True);
            Assert.That(errors.Any(error => error.RowNumber == 3 && error.Field == "Timestamp/Payee/Amount"), Is.True);
            Assert.That(errors.Any(error => error.RowNumber == 4 && error.Field == "Amount"), Is.True);
            Assert.That(errors.Any(error => error.RowNumber == 5 && error.Field == "Payee"), Is.True);
            Assert.That(errors.Any(error => error.RowNumber == 5 && error.Field == "Memo"), Is.True);
        }
    }

    [Test]
    public void Preview_FormulaPayeeIsRejectedAndBlankRowsAreIgnored()
    {
        using var workbook = Workbook(RequiredHeaders, []);
        var worksheet = workbook.Worksheet("Transaction");
        worksheet.Cell(2, 1).Value = TransactionTimestamp;
        worksheet.Cell(2, 2).FormulaA1 = "\"Formula payee\"";
        worksheet.Cell(2, 3).Value = 1m;
        worksheet.Row(3).Clear(XLClearOptions.All);

        var error = _handler.Preview(worksheet).Errors.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(error.RowNumber, Is.EqualTo(2));
            Assert.That(error.Field, Is.EqualTo("Timestamp/Payee/Amount"));
        }
    }

    [Test]
    public void Preview_SplitWorksheetMapsMatchingRowsAndRejectsMismatchedTotal()
    {
        using var workbook = Workbook(
            ["ID", "Timestamp", "Payee", "Amount", "Category", "Memo", "BankReference"],
            [
                [12, TransactionTimestamp, "Split purchase", 10m, "Top", null, "split-ref"],
                [13, TransactionTimestamp, "Bad split", 5m, "Top", null, "bad-ref"]
            ]);
        AddSplitWorksheet(workbook,
        [
            [12, 6m, "food: one", " memo one "],
            [12, 4m, "food: two", null],
            [13, 4m, "food", null],
            ["invalid-id", 1m, "ignored", null],
            [12, "not-decimal", "ignored", null]
        ]);
        ((IWorkbookImportContextHandler)_handler).SetWorkbookContext(workbook);

        var preview = _handler.Preview(workbook.Worksheet("Transaction"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(preview.ValidRecordCount, Is.EqualTo(1));
            Assert.That(preview.Errors.Single().RowNumber, Is.EqualTo(3));
            Assert.That(preview.Errors.Single().Code, Is.EqualTo("split_sum_mismatch"));
        }
    }

    [Test]
    public void Preview_UnusableSplitHeadersFallsBackToTransactionSplit()
    {
        using var workbook = Workbook(
            ["ID", "Timestamp", "Payee", "Amount", "Category"],
            [[12, TransactionTimestamp, "Coffee", 3m, "food"]]);
        var split = workbook.AddWorksheet("Split");
        SetRow(split, 1, ["TransactionID", "Amount"]);
        ((IWorkbookImportContextHandler)_handler).SetWorkbookContext(workbook);

        var preview = _handler.Preview(workbook.Worksheet("Transaction"));

        Assert.That(preview.ValidRecordCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Import_InvalidWorksheetThrowsBeforePersistence()
    {
        using var workbook = Workbook(["Timestamp"], []);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.ImportAsync(workbook.Worksheet("Transaction"), CancellationToken.None));

        Assert.That(exception!.Message, Does.Contain("validated by the import coordinator"));
        _data.Verify(data => data.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Import_ValidWorksheetCreatesTransactionAndSplitGraphs()
    {
        _data.Setup(data => data.Get<Transaction>()).Returns(Array.Empty<Transaction>().AsQueryable());
        _data.Setup(data => data.ToListAsync(It.IsAny<IQueryable<string?>>()))
            .ReturnsAsync([]);
        _data.Setup(data => data.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        using var workbook = Workbook(
            ["ID", "Timestamp", "Payee", "Amount", "Category", "Memo", "BankReference"],
            [[12, TransactionTimestamp, " Coffee ", 10m, "food", " note ", ""]]);
        AddSplitWorksheet(workbook, [[12, 6m, "food: a", " one "], [12, 4m, "food: b", null]]);
        ((IWorkbookImportContextHandler)_handler).SetWorkbookContext(workbook);
        List<IModel>? staged = null;
        _data.Setup(data => data.AddRange(It.IsAny<IEnumerable<IModel>>()))
            .Callback((IEnumerable<IModel> models) => staged = models.ToList());

        var result = await _handler.ImportAsync(workbook.Worksheet("Transaction"), CancellationToken.None);

        var transaction = staged!.OfType<Transaction>().Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Created, Is.EqualTo(1));
            Assert.That(result.Skipped, Is.Zero);
            Assert.That(result.Updated, Is.Zero);
            Assert.That(transaction.Payee, Is.EqualTo("Coffee"));
            Assert.That(transaction.ExternalId,
                Is.EqualTo(DataImportService.GenerateLegacyExternalId("Coffee", 10m, new DateOnly(2026, 6, 15))));
            Assert.That(transaction.Splits.Select(split => split.Amount), Is.EqualTo([6m, 4m]));
            Assert.That(transaction.Splits.Select(split => split.Category), Is.EqualTo(["Food:A", "Food:B"]));
            Assert.That(transaction.Splits.First().Memo, Is.EqualTo("one"));
            Assert.That(transaction.Splits.Select(split => split.Order), Is.EqualTo([0, 1]));
        }
        _data.Verify(data => data.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Import_ExistingExternalIdIsNotCreated()
    {
        var externalId = "existing-ref";
        _data.Setup(data => data.Get<Transaction>()).Returns(Array.Empty<Transaction>().AsQueryable());
        _data.Setup(data => data.ToListAsync(It.IsAny<IQueryable<string?>>())).ReturnsAsync([externalId]);
        using var workbook = Workbook(RequiredHeaders,
            [[TransactionTimestamp, "Coffee", 3m, "Food", null, externalId]]);

        var result = await _handler.ImportAsync(workbook.Worksheet("Transaction"), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Created, Is.Zero);
            Assert.That(result.Updated, Is.Zero);
        }
        _data.Verify(data => data.AddRange(It.IsAny<IEnumerable<IModel>>()), Times.Never);
        _data.Verify(data => data.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Import_CanceledWorksheetObservesCancellation()
    {
        using var workbook = Workbook(RequiredHeaders,
            [[TransactionTimestamp, "Coffee", 3m, "Food", null, "ref"]]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _handler.ImportAsync(workbook.Worksheet("Transaction"), cancellation.Token));
    }

    private static readonly string[] RequiredHeaders =
        ["Timestamp", "Payee", "Amount", "Category", "Memo", "BankReference"];

    private static XLWorkbook Workbook(IEnumerable<object?> headers, IEnumerable<object?[]> rows)
    {
        var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Transaction");
        SetRow(worksheet, 1, headers.ToArray());
        var rowNumber = 2;
        foreach (var row in rows)
        {
            SetRow(worksheet, rowNumber++, row);
        }
        return workbook;
    }

    private static void AddSplitWorksheet(XLWorkbook workbook, IEnumerable<object?[]> rows)
    {
        var worksheet = workbook.AddWorksheet("Split");
        SetRow(worksheet, 1, ["TransactionID", "Amount", "Category", "Memo"]);
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

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
    }
}
