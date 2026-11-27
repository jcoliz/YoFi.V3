using ClosedXML.Excel;
using DotNetNuxt.Abstractions;
using Moq;
using YoFi.V3.Application.Services;
using YoFi.V3.Entities.Models;

namespace YoFi.V3.Tests.Unit.Application.TenantDataAdmin;

[TestFixture]
public class TransactionExportWorksheetWriterTests
{
    private Mock<IDataProvider> _data = null!;
    private TransactionExportWorksheetWriter _writer = null!;

    [SetUp]
    public void SetUp()
    {
        _data = new Mock<IDataProvider>();
        _writer = new TransactionExportWorksheetWriter(_data.Object);
    }

    [Test]
    public void Metadata_IdentifiesTransactionExporter()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_writer.DataType, Is.EqualTo("Transactions"));
            Assert.That(_writer.WorksheetName, Is.EqualTo("Transactions"));
            Assert.That(_writer.FileNamePart, Is.EqualTo("transactions"));
            Assert.That(_writer.SupportsDateFilter, Is.True);
        }
    }

    [Test]
    public async Task WriteAsync_EmptyDataWritesHeadersAndZeroCounts()
    {
        var transactions = new List<Transaction>();
        _data.Setup(data => data.Get<Transaction>()).Returns(transactions.AsQueryable());
        _data.Setup(data => data.ToListNoTrackingAsync(It.IsAny<IQueryable<TransactionExportWorksheetWriter.TransactionExportRow>>()))
            .Returns((IQueryable<TransactionExportWorksheetWriter.TransactionExportRow> query) =>
                Task.FromResult(query.ToList()));
        using var workbook = new XLWorkbook();

        var counts = await _writer.WriteAsync(workbook, 42, null, null, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(workbook.Worksheet("Transactions").Row(1).CellsUsed().Select(cell => cell.GetString()),
                Is.EqualTo(new[] { "Key", "Date", "Payee", "Amount", "Source", "ExternalId", "Memo", "Category" }));
            Assert.That(workbook.Worksheet("Splits").Row(1).CellsUsed().Select(cell => cell.GetString()),
                Is.EqualTo(new[] { "Key", "TransactionKey", "Amount", "Category", "Memo", "Order" }));
            Assert.That(counts.Select(count => (count.DataType, count.RecordCount)), Is.EqualTo(new[]
            {
                ("Transactions", 0),
                ("Splits", 0)
            }));
        }
    }

    [Test]
    public async Task WriteAsync_FiltersTenantAndDatesAndExportsSplitAndInlineCategories()
    {
        var inRangeSplit = new Transaction
        {
            Id = 1,
            Key = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            TenantId = 42,
            Date = new DateOnly(2025, 1, 15),
            Payee = "In range",
            Amount = 30m,
            Source = "Bank",
            ExternalId = "ext",
            Memo = "Memo",
            Splits =
            [
                new Split { Key = Guid.Parse("21111111-1111-1111-1111-111111111111"), TransactionId = 1, Amount = 10m, Category = "Food", Memo = "Lunch", Order = 0 },
                new Split { Key = Guid.Parse("31111111-1111-1111-1111-111111111111"), TransactionId = 1, Amount = 20m, Category = "Home", Order = 1 }
            ]
        };
        var inRangeSingle = new Transaction
        {
            Id = 2,
            Key = Guid.Parse("41111111-1111-1111-1111-111111111111"),
            TenantId = 42,
            Date = new DateOnly(2025, 1, 20),
            Payee = "Single",
            Amount = 12m,
            Splits =
            [
                new Split { Key = Guid.Parse("51111111-1111-1111-1111-111111111111"), TransactionId = 2, Amount = 12m, Category = "Inline", Order = 0 }
            ]
        };
        var tooOld = new Transaction { Id = 3, TenantId = 42, Date = new DateOnly(2024, 12, 31), Payee = "Old", Amount = 1m };
        var otherTenant = new Transaction { Id = 4, TenantId = 99, Date = new DateOnly(2025, 1, 16), Payee = "Other", Amount = 1m };
        var transactions = new List<Transaction> { inRangeSplit, inRangeSingle, tooOld, otherTenant };
        var splits = inRangeSplit.Splits.Concat(inRangeSingle.Splits).ToList();

        _data.Setup(data => data.Get<Transaction>()).Returns(transactions.AsQueryable());
        _data.Setup(data => data.Get<Split>()).Returns(splits.AsQueryable());
        _data.Setup(data => data.ToListNoTrackingAsync(It.IsAny<IQueryable<TransactionExportWorksheetWriter.TransactionExportRow>>()))
            .Returns((IQueryable<TransactionExportWorksheetWriter.TransactionExportRow> query) =>
                Task.FromResult(query.ToList()));
        _data.Setup(data => data.ToListNoTrackingAsync(It.IsAny<IQueryable<TransactionExportWorksheetWriter.SplitExportRow>>()))
            .Returns((IQueryable<TransactionExportWorksheetWriter.SplitExportRow> query) =>
                Task.FromResult(query.ToList()));
        using var workbook = new XLWorkbook();

        var counts = await _writer.WriteAsync(workbook, 42,
            new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 31), CancellationToken.None);

        var transactionWorksheet = workbook.Worksheet("Transactions");
        var splitsWorksheet = workbook.Worksheet("Splits");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(transactionWorksheet.LastRowUsed()!.RowNumber(), Is.EqualTo(3));
            Assert.That(transactionWorksheet.Row(2).Cell(1).GetString(), Is.EqualTo(inRangeSplit.Key.ToString("D")));
            Assert.That(transactionWorksheet.Row(2).Cell(8).GetString(), Is.EqualTo("SPLIT"));
            Assert.That(transactionWorksheet.Row(3).Cell(1).GetString(), Is.EqualTo(inRangeSingle.Key.ToString("D")));
            Assert.That(transactionWorksheet.Row(3).Cell(8).GetString(), Is.EqualTo("Inline"));
            Assert.That(splitsWorksheet.LastRowUsed()!.RowNumber(), Is.EqualTo(3));
            Assert.That(splitsWorksheet.Row(2).Cell(2).GetString(), Is.EqualTo(inRangeSplit.Key.ToString("D")));
            Assert.That(splitsWorksheet.Row(2).Cell(5).GetString(), Is.EqualTo("Lunch"));
            Assert.That(counts.Select(count => (count.DataType, count.RecordCount)), Is.EqualTo(new[]
            {
                ("Transactions", 2),
                ("Splits", 2)
            }));
        }
    }

    [Test]
    public async Task WriteAsync_ZeroSplitsWritesEmptyCategoryAndNoSplitRows()
    {
        var transaction = new Transaction
        {
            Id = 1,
            Key = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            TenantId = 42,
            Date = new DateOnly(2025, 1, 15),
            Payee = "No split",
            Amount = 5m
        };
        _data.Setup(data => data.Get<Transaction>()).Returns(new[] { transaction }.AsQueryable());
        _data.Setup(data => data.Get<Split>()).Returns(Array.Empty<Split>().AsQueryable());
        _data.Setup(data => data.ToListNoTrackingAsync(It.IsAny<IQueryable<TransactionExportWorksheetWriter.TransactionExportRow>>()))
            .Returns((IQueryable<TransactionExportWorksheetWriter.TransactionExportRow> query) =>
                Task.FromResult(query.ToList()));
        _data.Setup(data => data.ToListNoTrackingAsync(It.IsAny<IQueryable<TransactionExportWorksheetWriter.SplitExportRow>>()))
            .Returns((IQueryable<TransactionExportWorksheetWriter.SplitExportRow> query) =>
                Task.FromResult(query.ToList()));
        using var workbook = new XLWorkbook();

        var counts = await _writer.WriteAsync(workbook, 42, null, null, CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(workbook.Worksheet("Transactions").Row(2).Cell(8).GetString(), Is.Empty);
            Assert.That(workbook.Worksheet("Splits").LastRowUsed()!.RowNumber(), Is.EqualTo(1));
            Assert.That(counts.Select(count => count.RecordCount), Is.EqualTo(new[] { 1, 0 }));
        }
    }

    [Test]
    public async Task WriteAsync_PreCanceledTokenThrowsBeforeQuerying()
    {
        using var workbook = new XLWorkbook();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _writer.WriteAsync(workbook, 42, null, null, cancellation.Token));

        _data.Verify(data => data.Get<Transaction>(), Times.Never);
    }
}
