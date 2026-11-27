using Microsoft.Extensions.Caching.Memory;
using Moq;
using DotNetNuxt.Abstractions;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Features;
using YoFi.V3.Application.Services;
using YoFi.V3.Entities.Models;
using YoFi.V3.Tests.Unit.TestHelpers;

namespace YoFi.V3.Tests.Unit.Application.DataImport;

[TestFixture]
public class DataImportServiceNativeTransactionTests
{
    private Mock<IDataProvider> _data = null!;
    private TestTenantProvider _tenant = null!;
    private MemoryCache _cache = null!;
    private DataImportService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _data = new Mock<IDataProvider>();
        _tenant = new TestTenantProvider();
        _tenant.CurrentTenant.Id = 42;
        _cache = new MemoryCache(new MemoryCacheOptions());
        var time = new Mock<TimeProvider>();
        time.Setup(item => item.GetUtcNow()).Returns(new DateTimeOffset(2040, 7, 1, 12, 0, 0, TimeSpan.Zero));
        _service = new DataImportService(_tenant, _data.Object, _cache, new RegexValidationService(), time.Object);
        _data.Setup(data => data.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
    }

    [TearDown]
    public void TearDown() => _cache.Dispose();

    [Test]
    public async Task UpsertNativeTransactionsAsync_NullArgumentsThrow()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.UpsertNativeTransactionsAsync(null!, []));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.UpsertNativeTransactionsAsync([], null!));
    }

    [Test]
    public async Task UpsertNativeTransactionsAsync_CancelledTokenThrows()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => _service.UpsertNativeTransactionsAsync([], [], source.Token));
    }

    [Test]
    public async Task UpsertNativeTransactionsAsync_CreatesSimpleTransactionAndSplit()
    {
        var stagedTransactions = new List<Transaction>();
        var stagedSplits = new List<Split>();
        _data.Setup(data => data.Add(It.IsAny<IModel>()))
            .Callback((IModel model) =>
            {
                if (model is Transaction transaction) stagedTransactions.Add(transaction);
                if (model is Split split) stagedSplits.Add(split);
            });

        var result = await _service.UpsertNativeTransactionsAsync(
            [new NativeTransactionImportDto(null, new(2025, 1, 2), -12m, "  Corner Store  ", "memo", "bank", "external", "  food  ")],
            []);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.CreatedTransactions, Is.EqualTo(1));
            Assert.That(result.UpdatedTransactions, Is.Zero);
            Assert.That(result.CreatedSplits, Is.EqualTo(1));
            Assert.That(result.UpdatedSplits, Is.Zero);
            Assert.That(stagedTransactions, Has.Count.EqualTo(1));
            Assert.That(stagedTransactions[0].TenantId, Is.EqualTo(42));
            Assert.That(stagedTransactions[0].Payee, Is.EqualTo("  Corner Store  ")); 
            Assert.That(stagedTransactions[0].Memo, Is.EqualTo("memo"));
            Assert.That(stagedTransactions[0].Splits, Has.Count.EqualTo(1));
            Assert.That(stagedSplits.Single().Category, Is.EqualTo("Food"));
        }
    }

    [Test]
    public async Task UpsertNativeTransactionsAsync_UpdatesExistingSimpleAndRemovesExtraSplits()
    {
        var key = Guid.NewGuid();
        var transaction = new Transaction
        {
            Id = 7,
            TenantId = 42,
            Key = key,
            Splits =
            [
                new Split { Id = 1, Key = Guid.NewGuid(), TransactionId = 7, Amount = -10m, Category = "Old", Order = 0 },
                new Split { Id = 2, Key = Guid.NewGuid(), TransactionId = 7, Amount = -2m, Category = "Extra", Order = 1 }
            ]
        };
        _data.Setup(data => data.Get<Transaction>()).Returns(new[] { transaction }.AsQueryable());
        _data.Setup(data => data.ToListAsync(It.IsAny<IQueryable<Transaction>>()))
            .Returns((IQueryable<Transaction> query) => Task.FromResult(query.ToList()));
        _data.Setup(data => data.Get<Split>()).Returns(transaction.Splits.AsQueryable());
        _data.Setup(data => data.ToListAsync(It.IsAny<IQueryable<Split>>()))
            .Returns((IQueryable<Split> query) => Task.FromResult(query.ToList()));

        var removed = new List<Split>();
        _data.Setup(data => data.Remove(It.IsAny<IModel>()))
            .Callback((IModel model) => removed.Add((Split)model));

        var result = await _service.UpsertNativeTransactionsAsync(
            [new NativeTransactionImportDto(key, new(2025, 2, 1), -12m, "Updated", null, null, "new-external", "home")],
            []);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.CreatedTransactions, Is.Zero);
            Assert.That(result.UpdatedTransactions, Is.EqualTo(1));
            Assert.That(result.CreatedSplits, Is.Zero);
            Assert.That(result.UpdatedSplits, Is.EqualTo(1));
            Assert.That(transaction.Payee, Is.EqualTo("Updated"));
            Assert.That(transaction.ExternalId, Is.EqualTo("new-external"));
            Assert.That(transaction.Splits, Has.Count.EqualTo(1));
            Assert.That(transaction.Splits.Single().Amount, Is.EqualTo(-12m));
            Assert.That(transaction.Splits.Single().Category, Is.EqualTo("Home"));
            Assert.That(removed, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public async Task UpsertNativeTransactionsAsync_ReconcilesSplitGraph()
    {
        var transactionKey = Guid.NewGuid();
        var existingSplitKey = Guid.NewGuid();
        var missingSplitKey = Guid.NewGuid();
        var transaction = new Transaction
        {
            Id = 7,
            TenantId = 42,
            Key = transactionKey,
            Splits =
            [
                new Split { Id = 1, Key = existingSplitKey, TransactionId = 7, Amount = -5m, Category = "Old", Order = 0 },
                new Split { Id = 2, Key = Guid.NewGuid(), TransactionId = 7, Amount = -1m, Category = "Remove", Order = 1 }
            ]
        };
        _data.Setup(data => data.Get<Transaction>()).Returns(new[] { transaction }.AsQueryable());
        _data.Setup(data => data.ToListAsync(It.IsAny<IQueryable<Transaction>>()))
            .Returns((IQueryable<Transaction> query) => Task.FromResult(query.ToList()));
        _data.Setup(data => data.Get<Split>()).Returns(transaction.Splits.AsQueryable());
        _data.Setup(data => data.ToListAsync(It.IsAny<IQueryable<Split>>()))
            .Returns((IQueryable<Split> query) => Task.FromResult(query.ToList()));

        var removed = new List<Split>();
        _data.Setup(data => data.Remove(It.IsAny<IModel>())).Callback((IModel model) => removed.Add((Split)model));
        var added = new List<IModel>();
        _data.Setup(data => data.Add(It.IsAny<IModel>())).Callback((IModel model) => added.Add(model));

        var result = await _service.UpsertNativeTransactionsAsync(
            [new NativeTransactionImportDto(transactionKey, new(2025, 3, 1), -15m, "Split Store", null, null, null, "SPLIT")],
            [
                new NativeSplitImportDto(existingSplitKey, transactionKey, -10m, "food", null, 0),
                new NativeSplitImportDto(missingSplitKey, transactionKey, -5m, "home", null, 1)
            ]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.UpdatedTransactions, Is.EqualTo(1));
            Assert.That(result.CreatedSplits, Is.EqualTo(1));
            Assert.That(result.UpdatedSplits, Is.EqualTo(1));
            Assert.That(transaction.Splits, Has.Count.EqualTo(2));
            Assert.That(transaction.Splits.Single(split => split.Key == existingSplitKey).Category, Is.EqualTo("Food"));
            Assert.That(transaction.Splits.Single(split => split.Key == missingSplitKey).Key, Is.EqualTo(missingSplitKey));
            Assert.That(removed, Has.Count.EqualTo(1));
            Assert.That(added.Count(model => model is Split), Is.EqualTo(1));
        }
    }
}
