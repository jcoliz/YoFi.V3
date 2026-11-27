using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Features;
using YoFi.V3.Entities.Exceptions;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Tenancy.Models;
using YoFi.V3.Entities.Tenancy.Providers;
using YoFi.V3.Tests.Integration.Application.TestHelpers;

namespace YoFi.V3.Tests.Integration.Application;

[TestFixture]
public class TransactionFilterFeatureTests : FeatureTestBase
{
    private TransactionsFeature _feature = null!;
    private Tenant _tenant = null!;

    [SetUp]
    public new async Task SetUp()
    {
        await base.SetUp();

        _tenant = new Tenant { Name = "Filter Tenant", Description = "Advanced transaction filter tests" };
        _context.Tenants.Add(_tenant);
        await _context.SaveChangesAsync();
        _feature = new TransactionsFeature(new FilterTestTenantProvider { CurrentTenant = _tenant }, _dataProvider);
    }

    [Test]
    public async Task GetTransactionsAsync_AdvancedTextFiltersMatchFieldsIndependentlyAndComposeWithSearch()
    {
        AddTransaction("Star Market", 12m, memo: "Trip expense", splits:
        [
            new Split { Amount = 4m, Category = "Food", Order = 0 },
            new Split { Amount = 4m, Category = "Other", Memo = "Refund details", Order = 1 }
        ]);
        AddTransaction("Star Market", 12m, memo: null,
        [
            new Split { Amount = 12m, Category = "Food", Memo = "Refund on later split", Order = 0 }
        ]);
        AddTransaction("Other Market", 12m, memo: "Trip expense",
        [
            new Split { Amount = 12m, Category = "Food", Order = 0 }
        ]);
        AddTransaction("Star Market", 12m, memo: "No match",
        [
            new Split { Amount = 12m, Category = "Food", Order = 0 }
        ]);
        await _context.SaveChangesAsync();

        var payeeResult = await GetFiltered(payee: " STAR ");
        var categoryResult = await GetFiltered(category: " food ");
        var memoResult = await GetFiltered(memo: " refund ");
        var transactionMemoResult = await GetFiltered(memo: "trip");
        var combinedResult = await GetFiltered(
            payee: "Star", category: " food ", memo: "refund",
            search: "Trip");

        Assert.That(payeeResult.Metadata.TotalCount, Is.EqualTo(3));
        Assert.That(categoryResult.Metadata.TotalCount, Is.EqualTo(4));
        Assert.That(memoResult.Metadata.TotalCount, Is.EqualTo(2));
        Assert.That(transactionMemoResult.Metadata.TotalCount, Is.EqualTo(2));
        Assert.That(combinedResult.Metadata.TotalCount, Is.EqualTo(1));
        Assert.That(combinedResult.Items.Single().Payee, Is.EqualTo("Star Market"));
    }



    [Test]
    public async Task GetTransactionsAsync_AdvancedFilterCountsBeforePagingAndRemainsTenantScoped()
    {
        for (var index = 0; index < 52; index++)
        {
            AddTransaction($"Matching {index:D2}", 5m);
        }

        var otherTenant = new Tenant { Name = "Other Filter Tenant", Description = "Other tenant" };
        _context.Tenants.Add(otherTenant);
        await _context.SaveChangesAsync();
        _context.Transactions.Add(new Transaction
        {
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Payee = "Matching outside tenant",
            Amount = 5m,
            TenantId = otherTenant.Id
        });
        await _context.SaveChangesAsync();

        var firstPage = await GetFiltered(payee: "Matching", pageNumber: 1);
        var secondPage = await GetFiltered(payee: "Matching", pageNumber: 2);

        Assert.That(firstPage.Metadata.TotalCount, Is.EqualTo(52));
        Assert.That(firstPage.Items, Has.Count.EqualTo(50));
        Assert.That(secondPage.Metadata.TotalCount, Is.EqualTo(52));
        Assert.That(secondPage.Items, Has.Count.EqualTo(2));
        Assert.That(
            firstPage.Items.Concat(secondPage.Items).Select(item => item.Key).Distinct().ToArray(),
            Has.Length.EqualTo(52));
    }

    private Task<YoFi.V3.Application.Dto.PaginatedResultDto<YoFi.V3.Application.Dto.TransactionResultDto>> GetFiltered(
        int? pageNumber = null,
        string? search = null,
        string? payee = null,
        string? category = null,
        string? memo = null,
        string? amount = null,
        bool uncategorizedOnly = false)
        => _feature.GetTransactionsAsync(pageNumber, search: search, payee: payee, category: category,
            memo: memo, amount: amount, uncategorizedOnly: uncategorizedOnly);

    private Transaction AddTransaction(
        string payee,
        decimal amount,
        string? memo = null,
        IReadOnlyCollection<Split>? splits = null)
    {
        var transaction = new Transaction
        {
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Payee = payee,
            Amount = amount,
            Memo = memo,
            TenantId = _tenant.Id
        };

        if (splits is not null)
        {
            foreach (var split in splits)
            {
                transaction.Splits.Add(split);
            }
        }

        _context.Transactions.Add(transaction);
        return transaction;
    }

    private sealed class FilterTestTenantProvider : ITenantProvider
    {
        public required Tenant CurrentTenant { get; init; }
    }
}
