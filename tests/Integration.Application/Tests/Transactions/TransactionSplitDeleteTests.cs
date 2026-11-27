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
