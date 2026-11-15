using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Features;
using YoFi.V3.Data;
using YoFi.V3.Entities.Exceptions;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Tenancy.Models;
using YoFi.V3.Entities.Tenancy.Providers;
using YoFi.V3.Tests.Integration.Application.TestHelpers;

namespace YoFi.V3.Tests.Integration.Application;

[TestFixture]
public class TransactionSplitUpdateTests : FeatureTestBase
{
    private TransactionsFeature _feature = null!;
    private ITenantProvider _tenantProvider = null!;
    private Tenant _tenant = null!;

    [SetUp]
    public new async Task SetUp()
    {
        await base.SetUp();
        _tenant = new Tenant { Name = "Split update tenant", Description = "Split update tests" };
        _context.Tenants.Add(_tenant);
        await _context.SaveChangesAsync();
        _tenantProvider = new SplitTestTenantProvider { CurrentTenant = _tenant };
        _feature = new TransactionsFeature(_tenantProvider, _dataProvider, _context);
    }

    [TestCase(0, -70, -70, -10, -20)]
    [TestCase(1, -30, -50, -30, -20)]
    [TestCase(2, -10, -70, -20, -10)]
    [TestCase(2, 0, -80, -20, 0)]
    [TestCase(2, 10, -90, -20, 10)]
    public async Task UpdateSplitAsync_RebalancesFirstOtherSplitAndPreservesFields(
        int editedIndex, int amount, int expectedFirst, int expectedSecond, int expectedThird)
    {
        var transaction = await CreateTransactionAsync();
        var original = await _feature.GetTransactionByKeyAsync(transaction.Key);

        var result = await _feature.UpdateSplitAsync(transaction.Key, original.Splits[editedIndex].Key,
            new SplitEditDto(amount, "  home : supplies  ", "Updated memo"));

        var expectedSplits = original.Splits.Select((split, index) => split with
        {
            Amount = new[] { expectedFirst, expectedSecond, expectedThird }[index],
            Category = index == editedIndex ? "Home:Supplies" : split.Category,
            Memo = index == editedIndex ? "Updated memo" : split.Memo
        }).ToArray();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Splits, Is.EqualTo(expectedSplits));
            Assert.That(result.Splits.Sum(split => split.Amount), Is.EqualTo(result.Amount));
            Assert.That(result with { Splits = original.Splits }, Is.EqualTo(original));
        }

        _context.ChangeTracker.Clear();
        var persisted = await _feature.GetTransactionByKeyAsync(transaction.Key);
        Assert.That(persisted.Splits, Is.EqualTo(expectedSplits));
        Assert.That(persisted with { Splits = original.Splits }, Is.EqualTo(original));
    }

    [Test]
    public async Task UpdateSplitAsync_WithIncomeAndCents_PreservesExactBalance()
    {
        var transaction = await CreateTransactionAsync(100.01m);
        var target = transaction.Splits.Single(split => split.Order == 12);

        var result = await _feature.UpdateSplitAsync(transaction.Key, target.Key,
            new SplitEditDto(40.02m, null, null));

        Assert.That(result.Splits.Select(split => split.Amount), Is.EqualTo(new[] { 79.99m, -20m, 40.02m }));
        Assert.That(result.Splits.Sum(split => split.Amount), Is.EqualTo(100.01m));
        Assert.That(result.Splits[2].Category, Is.Empty);
        Assert.That(result.Splits[2].Memo, Is.Null);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task UpdateSplitAsync_WithBlankCategoryAndMemo_ClearsEditedFields(string? category)
    {
        var transaction = await CreateTransactionAsync();
        var target = transaction.Splits.Single(split => split.Order == 12);

        var result = await _feature.UpdateSplitAsync(transaction.Key, target.Key,
            new SplitEditDto(-20m, category, null));

        Assert.That(result.Splits[2].Category, Is.Empty);
        Assert.That(result.Splits[2].Memo, Is.Null);
        Assert.That(result.Splits.Select(split => split.Amount), Is.EqualTo(new[] { -60m, -20m, -20m }));
    }

    [Test]
    public async Task UpdateSplitAsync_WithExistingImbalance_RebalancesEvenForCategoryOnlyEdit()
    {
        var transaction = await CreateTransactionAsync(-110m);
        var target = transaction.Splits.Single(split => split.Order == 12);

        var result = await _feature.UpdateSplitAsync(transaction.Key, target.Key,
            new SplitEditDto(-20m, "Updated", "Changed category only"));

        Assert.That(result.Splits.Select(split => split.Amount), Is.EqualTo(new[] { -70m, -20m, -20m }));
        Assert.That(result.Splits.Sum(split => split.Amount), Is.EqualTo(-110m));
    }

    [Test]
    public async Task UpdateSplitAsync_WithMissingTransaction_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<TransactionNotFoundException>(() =>
            _feature.UpdateSplitAsync(Guid.NewGuid(), Guid.NewGuid(), new SplitEditDto(0m, null, null)));
    }

    [Test]
    public async Task UpdateSplitAsync_WithMissingSplit_LeavesTransactionUnchanged()
    {
        var transaction = await CreateTransactionAsync();
        var original = await _feature.GetTransactionByKeyAsync(transaction.Key);
        var missingKey = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<SplitNotFoundException>(() =>
            _feature.UpdateSplitAsync(transaction.Key, missingKey, new SplitEditDto(0m, null, null)));

        Assert.That(exception!.ResourceKey, Is.EqualTo(missingKey));
        _context.ChangeTracker.Clear();
        Assert.That((await _feature.GetTransactionByKeyAsync(transaction.Key)).Splits, Is.EqualTo(original.Splits));
    }

    [Test]
    public async Task UpdateSplitAsync_WithSplitFromAnotherTransaction_ThrowsNotFound()
    {
        var transaction = await CreateTransactionAsync();
        var other = await CreateTransactionAsync();

        await Assert.ThrowsAsync<SplitNotFoundException>(() =>
            _feature.UpdateSplitAsync(transaction.Key, other.Splits.First().Key,
                new SplitEditDto(0m, null, null)));
    }

    [Test]
    public async Task UpdateSplitAsync_WithOtherTenantTransaction_ThrowsNotFound()
    {
        var transaction = await CreateTransactionAsync();
        var otherTenant = new Tenant { Name = "Other", Description = "Other tenant" };
        _context.Tenants.Add(otherTenant);
        await _context.SaveChangesAsync();
        var otherProvider = new SplitTestTenantProvider { CurrentTenant = otherTenant };
        var otherFeature = new TransactionsFeature(otherProvider, _dataProvider, _context);

        await Assert.ThrowsAsync<TransactionNotFoundException>(() =>
            otherFeature.UpdateSplitAsync(transaction.Key, transaction.Splits.First().Key,
                new SplitEditDto(0m, null, null)));
    }

    [Test]
    public async Task UpdateSplitAsync_WithSingleSplit_RejectsEditWithoutChangingData()
    {
        var created = await _feature.AddTransactionAsync(
            new TransactionEditDto(new DateOnly(2026, 1, 1), -100m, "Single", null, null, null, "Original"));
        var split = await _context.Splits.SingleAsync();
        _context.ChangeTracker.Clear();

        await Assert.ThrowsAsync<ValidationException>(() =>
            _feature.UpdateSplitAsync(created.Key, split.Key, new SplitEditDto(-70m, "Changed", "Changed")));

        _context.ChangeTracker.Clear();
        var persisted = await _context.Splits.SingleAsync();
        Assert.That(persisted.Amount, Is.EqualTo(-100m));
        Assert.That(persisted.Category, Is.EqualTo("Original"));
        Assert.That(persisted.Memo, Is.Null);
    }

    [Test]
    public async Task UpdateSplitAsync_WhenSavingFails_RollsBackBothSplits()
    {
        var transaction = await CreateTransactionAsync();
        var original = await _feature.GetTransactionByKeyAsync(transaction.Key);

        // Bypass boundary validation to force a database constraint failure during the save.
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            _feature.UpdateSplitAsync(transaction.Key, original.Splits[2].Key,
                new SplitEditDto(-40m, new string('x', 201), null)));

        _context.ChangeTracker.Clear();
        var persisted = await _feature.GetTransactionByKeyAsync(transaction.Key);
        Assert.That(persisted.Splits, Is.EqualTo(original.Splits));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task UpdateSplitAsync_WithCompetingSplitOperation_RejectsStaleCalculation(bool addSplit)
    {
        var transaction = await CreateTransactionAsync();
        var firstKey = transaction.Splits.Single(split => split.Order == 4).Key;
        var lastKey = transaction.Splits.Single(split => split.Order == 12).Key;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var readCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var continueSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var interceptor = new BeforeSaveInterceptor(async token =>
        {
            readCompleted.SetResult();
            await continueSave.Task.WaitAsync(token);
        });
        var options = new DbContextOptionsBuilder<ApplicationDbContext>(_options)
            .AddInterceptors(interceptor)
            .Options;
        await using var concurrentContext = new ApplicationDbContext(options);
        var concurrentFeature = new TransactionsFeature(_tenantProvider, concurrentContext, concurrentContext);

        var input = new SplitEditDto(-40m, "Stale", null);
        var staleUpdate = addSplit
            ? concurrentFeature.AddSplitAsync(transaction.Key, input, timeout.Token)
            : concurrentFeature.UpdateSplitAsync(transaction.Key, lastKey, input, timeout.Token);
        TransactionDetailDto committed;
        try
        {
            await readCompleted.Task.WaitAsync(timeout.Token);
            committed = await _feature.UpdateSplitAsync(transaction.Key, firstKey,
                new SplitEditDto(-50m, "Committed", null), timeout.Token);
        }
        finally
        {
            continueSave.TrySetResult();
        }

        await Assert.ThrowsAsync<DataConflictException>(async () => await staleUpdate);
        _context.ChangeTracker.Clear();
        var persisted = await _feature.GetTransactionByKeyAsync(transaction.Key);
        Assert.That(persisted.Splits, Is.EqualTo(committed.Splits));
        Assert.That(persisted.Splits.Sum(split => split.Amount), Is.EqualTo(persisted.Amount));
    }

    [Test]
    public async Task UpdateSplitAsync_WithCancelledRequest_DoesNotSave()
    {
        var transaction = await CreateTransactionAsync();
        var original = await _feature.GetTransactionByKeyAsync(transaction.Key);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        Assert.That(async () => await _feature.UpdateSplitAsync(transaction.Key, original.Splits[2].Key,
            new SplitEditDto(-40m, null, null), cancellation.Token), Throws.InstanceOf<OperationCanceledException>());

        _context.ChangeTracker.Clear();
        Assert.That((await _feature.GetTransactionByKeyAsync(transaction.Key)).Splits, Is.EqualTo(original.Splits));
    }

    private async Task<Transaction> CreateTransactionAsync(decimal amount = -100m)
    {
        var transaction = new Transaction
        {
            TenantId = _tenant.Id,
            Date = new DateOnly(2026, 1, 1),
            Amount = amount,
            Payee = "Split purchase",
            Memo = "Transaction memo",
            Source = "Imported account",
            ExternalId = "Original external ID",
            Splits =
            [
                new Split { Amount = -20m, Category = "Last", Memo = "Last memo", Order = 12 },
                new Split { Amount = -60m, Category = "First", Memo = "First memo", Order = 4 },
                new Split { Amount = -20m, Category = "Middle", Memo = "Middle memo", Order = 8 }
            ]
        };
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        return transaction;
    }

    private sealed class SplitTestTenantProvider : ITenantProvider
    {
        public required Tenant CurrentTenant { get; init; }
    }

    private sealed class BeforeSaveInterceptor(Func<CancellationToken, Task> beforeSave) : SaveChangesInterceptor
    {
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            await beforeSave(cancellationToken);
            return result;
        }
    }
}
