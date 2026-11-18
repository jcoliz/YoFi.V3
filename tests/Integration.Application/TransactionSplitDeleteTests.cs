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
public class TransactionSplitDeleteTests : FeatureTestBase
{
    private TransactionsFeature _feature = null!;
    private ITenantProvider _tenantProvider = null!;
    private Tenant _tenant = null!;

    [SetUp]
    public new async Task SetUp()
    {
        await base.SetUp();
        _tenant = new Tenant { Name = "Split deletion tenant", Description = "Split deletion tests" };
        _context.Tenants.Add(_tenant);
        await _context.SaveChangesAsync();
        _tenantProvider = new DeleteTestTenantProvider { CurrentTenant = _tenant };
        _feature = new TransactionsFeature(_tenantProvider, _dataProvider);
    }

    [TestCase(0, -10000)]
    [TestCase(1, -10000)]
    [TestCase(2, -10000)]
    [TestCase(1, 10001)]
    [TestCase(0, 0)]
    [TestCase(1, -11001)]
    public async Task DeleteSplitAsync_RebalancesAndPreservesSurvivingFields(int deletedIndex, int amountCents)
    {
        var original = await CreateTransactionAsync(amountCents / 100m);
        var deleted = original.Splits[deletedIndex];
        var survivors = original.Splits.Where(s => s.Key != deleted.Key).ToArray();
        var expected = survivors.Select((split, index) => split with
        {
            Order = index,
            Amount = index == 0 ? original.Amount - survivors.Skip(1).Sum(s => s.Amount) : split.Amount
        }).ToArray();

        var result = await _feature.DeleteSplitAsync(original.Key, deleted.Key);

        Assert.That(result.Splits, Is.EqualTo(expected));
        Assert.That(result.Splits.Sum(s => s.Amount), Is.EqualTo(original.Amount));
        Assert.That(result with { Splits = original.Splits }, Is.EqualTo(original));
        _context.ChangeTracker.Clear();
        var persisted = await _feature.GetTransactionByKeyAsync(original.Key);
        Assert.That(persisted.Splits, Is.EqualTo(expected));
        Assert.That(persisted with { Splits = original.Splits }, Is.EqualTo(original));
        Assert.That(await _context.Splits.AnyAsync(s => s.Key == deleted.Key), Is.False);
    }

    [TestCase(0)]
    [TestCase(1)]
    public async Task DeleteSplitAsync_FromTwoToOne_RestoresSingleCategoryWorkflows(int deletedIndex)
    {
        var created = await _feature.AddTransactionAsync(new TransactionEditDto(
            new DateOnly(2026, 1, 1), -100m, "Two splits", "Transaction memo", null, null, "Groceries"));
        var original = await _feature.AddSplitAsync(created.Key, new SplitEditDto(-40m, "Household", "Split memo"));
        var survivor = original.Splits[1 - deletedIndex];
        _context.ChangeTracker.Clear();

        var result = await _feature.DeleteSplitAsync(original.Key, original.Splits[deletedIndex].Key);

        Assert.That(result.Category, Is.EqualTo(survivor.Category));
        Assert.That(result.Splits, Is.Empty);
        Assert.That(result.Amount, Is.EqualTo(-100m));
        _context.ChangeTracker.Clear();
        var persistedSplit = await _context.Splits.SingleAsync();
        Assert.That(persistedSplit.Key, Is.EqualTo(survivor.Key));
        Assert.That(persistedSplit.Category, Is.EqualTo(survivor.Category));
        Assert.That(persistedSplit.Memo, Is.EqualTo(survivor.Memo));
        Assert.That(persistedSplit.Order, Is.Zero);
        Assert.That(persistedSplit.Amount, Is.EqualTo(-100m));
        var detail = await _feature.GetTransactionByKeyAsync(original.Key);
        Assert.That(detail.Category, Is.EqualTo(survivor.Category));
        var list = await _feature.GetTransactionsAsync();
        Assert.That(list.Items.Single().Category, Is.EqualTo(survivor.Category));
        Assert.That(list.Items.Single().HasMultipleSplits, Is.False);

        await _feature.QuickEditTransactionAsync(original.Key, new TransactionQuickEditDto(
            "Quick edited", "Updated memo", "Updated category"));
        await _feature.UpdateTransactionAsync(original.Key, new TransactionEditDto(
            original.Date, -100.01m, "Quick edited", "Updated memo", null, null, "Updated category"));
        _context.ChangeTracker.Clear();
        persistedSplit = await _context.Splits.SingleAsync();
        Assert.That(persistedSplit.Amount, Is.EqualTo(-100.01m));
        Assert.That(persistedSplit.Category, Is.EqualTo("Updated Category"));
        Assert.That(persistedSplit.Key, Is.EqualTo(survivor.Key));
        Assert.That(persistedSplit.Memo, Is.EqualTo(survivor.Memo));
    }

    [Test]
    public async Task DeleteSplitAsync_LastSplit_RejectsWithoutChanges()
    {
        var original = await _feature.AddTransactionAsync(new TransactionEditDto(
            new DateOnly(2026, 1, 1), -100m, "Single", null, null, null, "Groceries"));
        var split = await _context.Splits.SingleAsync();
        _context.ChangeTracker.Clear();

        var exception = await Assert.ThrowsAsync<ValidationException>(() =>
            _feature.DeleteSplitAsync(original.Key, split.Key));

        Assert.That(exception!.Message, Does.Contain("last split"));
        _context.ChangeTracker.Clear();
        Assert.That((await _context.Splits.SingleAsync()).Amount, Is.EqualTo(-100m));
        Assert.That(await _feature.GetTransactionByKeyAsync(original.Key), Is.EqualTo(original));
        await Assert.ThrowsAsync<SplitNotFoundException>(() =>
            _feature.DeleteSplitAsync(original.Key, Guid.NewGuid()));
    }

    [Test]
    public async Task DeleteSplitAsync_MissingOrUnrelatedKeys_RejectsWithoutChanges()
    {
        var original = await CreateTransactionAsync();
        var other = await CreateTransactionAsync();
        await Assert.ThrowsAsync<TransactionNotFoundException>(() =>
            _feature.DeleteSplitAsync(Guid.NewGuid(), original.Splits[0].Key));
        foreach (var key in new[] { Guid.NewGuid(), other.Splits[0].Key })
        {
            var exception = await Assert.ThrowsAsync<SplitNotFoundException>(() =>
                _feature.DeleteSplitAsync(original.Key, key));
            Assert.That(exception!.ResourceKey, Is.EqualTo(key));
        }
        _context.ChangeTracker.Clear();
        Assert.That((await _feature.GetTransactionByKeyAsync(original.Key)).Splits, Is.EqualTo(original.Splits));
        Assert.That((await _feature.GetTransactionByKeyAsync(other.Key)).Splits, Is.EqualTo(other.Splits));
    }

    [Test]
    public async Task DeleteSplitAsync_OtherTenant_RejectsWithoutChanges()
    {
        var original = await CreateTransactionAsync();
        var otherTenant = new Tenant { Name = "Other", Description = "Other tenant" };
        _context.Tenants.Add(otherTenant);
        await _context.SaveChangesAsync();
        var otherFeature = new TransactionsFeature(
            new DeleteTestTenantProvider { CurrentTenant = otherTenant }, _dataProvider);

        await Assert.ThrowsAsync<TransactionNotFoundException>(() =>
            otherFeature.DeleteSplitAsync(original.Key, original.Splits[0].Key));

        _context.ChangeTracker.Clear();
        Assert.That((await _feature.GetTransactionByKeyAsync(original.Key)).Splits, Is.EqualTo(original.Splits));
    }

    [Test]
    public async Task DeleteSplitAsync_FailureAfterSave_RollsBackRemovalBalancingAndOrdering()
    {
        var original = await CreateTransactionAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>(_options)
            .AddInterceptors(new FailAfterSaveInterceptor()).Options;
        await using var context = new ApplicationDbContext(options);
        var feature = new TransactionsFeature(_tenantProvider, context);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            feature.DeleteSplitAsync(original.Key, original.Splits[0].Key));

        _context.ChangeTracker.Clear();
        Assert.That((await _feature.GetTransactionByKeyAsync(original.Key)).Splits, Is.EqualTo(original.Splits));
    }

    [TestCase("Delete")]
    [TestCase("Add")]
    [TestCase("Edit")]
    [TestCase("Transaction")]
    public async Task DeleteSplitAsync_CompetingOperation_RejectsStaleCalculation(string operation)
    {
        var original = await CreateTransactionAsync();
        if (operation == "Delete")
        {
            original = await _feature.DeleteSplitAsync(original.Key, original.Splits[2].Key);
            _context.ChangeTracker.Clear();
        }
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var readCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var continueSave = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var options = new DbContextOptionsBuilder<ApplicationDbContext>(_options)
            .AddInterceptors(new BeforeSaveInterceptor(async token =>
            {
                readCompleted.SetResult();
                await continueSave.Task.WaitAsync(token);
            })).Options;
        await using var context = new ApplicationDbContext(options);
        var feature = new TransactionsFeature(_tenantProvider, context);
        var stale = operation switch
        {
            "Delete" => feature.DeleteSplitAsync(original.Key, original.Splits[^1].Key, timeout.Token),
            "Add" => feature.AddSplitAsync(original.Key, new SplitEditDto(-5m, "Stale", null), timeout.Token),
            "Edit" => feature.UpdateSplitAsync(original.Key, original.Splits[1].Key,
                new SplitEditDto(-5m, "Stale", null), timeout.Token),
            "Transaction" => feature.UpdateTransactionAsync(original.Key, new TransactionEditDto(
                original.Date, -100.01m, original.Payee, original.Memo, original.Source, original.ExternalId, null)),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
        TransactionDetailDto committed;
        try
        {
            await readCompleted.Task.WaitAsync(timeout.Token);
            committed = await _feature.DeleteSplitAsync(original.Key, original.Splits[0].Key, timeout.Token);
        }
        finally
        {
            continueSave.TrySetResult();
        }
        await Assert.ThrowsAsync<DotNetNuxt.Abstractions.DataConflictException>(async () => await stale);
        _context.ChangeTracker.Clear();
        var persisted = await _feature.GetTransactionByKeyAsync(original.Key);
        Assert.That(persisted.Splits, Is.EqualTo(committed.Splits));
        Assert.That(persisted.Amount, Is.EqualTo(original.Amount));
        Assert.That(persisted.Category, Is.EqualTo(committed.Category));
        var splits = await _context.Splits.ToListAsync();
        Assert.That(splits, Is.Not.Empty);
        Assert.That(splits.Sum(s => s.Amount), Is.EqualTo(persisted.Amount));
    }

    [Test]
    public async Task DeleteSplitAsync_Cancelled_DoesNotSave()
    {
        var original = await CreateTransactionAsync();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        Assert.That(async () => await _feature.DeleteSplitAsync(
            original.Key, original.Splits[0].Key, cancellation.Token),
            Throws.InstanceOf<OperationCanceledException>());

        _context.ChangeTracker.Clear();
        Assert.That((await _feature.GetTransactionByKeyAsync(original.Key)).Splits, Is.EqualTo(original.Splits));
    }

    private async Task<TransactionDetailDto> CreateTransactionAsync(decimal amount = -100m)
    {
        var transaction = new Transaction
        {
            TenantId = _tenant.Id,
            Date = new DateOnly(2026, 1, 1),
            Amount = amount,
            Payee = "Split purchase",
            Memo = "Transaction memo",
            Source = "Imported",
            ExternalId = Guid.NewGuid().ToString(),
            Splits =
            [
                new Split { Amount = 0m, Category = "Last", Memo = "Last memo", Order = 12 },
                new Split { Amount = -60.01m, Category = "First", Memo = "First memo", Order = 4 },
                new Split { Amount = -39.99m, Category = "Middle", Memo = "Middle memo", Order = 8 }
            ]
        };
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        return await _feature.GetTransactionByKeyAsync(transaction.Key);
    }

    private sealed class DeleteTestTenantProvider : ITenantProvider
    {
        public required Tenant CurrentTenant { get; init; }
    }

    private sealed class FailAfterSaveInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated failure after saving");
    }

    private sealed class BeforeSaveInterceptor(Func<CancellationToken, Task> beforeSave) : SaveChangesInterceptor
    {
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            await beforeSave(cancellationToken);
            return result;
        }
    }
}
