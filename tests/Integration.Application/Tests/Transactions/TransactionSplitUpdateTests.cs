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
        _feature = new TransactionsFeature(_tenantProvider, _dataProvider);
    }

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

    public async Task UpdateTransactionAsync_WhenSavingFails_RollsBackAmountAndFirstSplit()
    {
        var transaction = await CreateTransactionAsync();
        var original = await _feature.GetTransactionByKeyAsync(transaction.Key);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            _feature.UpdateTransactionAsync(transaction.Key, new TransactionEditDto(
                original.Date, -100.01m, new string('x', 201), original.Memo,
                original.Source, original.ExternalId, null)));

        _context.ChangeTracker.Clear();
        var persisted = await _feature.GetTransactionByKeyAsync(transaction.Key);
        Assert.That(persisted.Splits, Is.EqualTo(original.Splits));
        Assert.That(persisted with { Splits = original.Splits }, Is.EqualTo(original));
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
