using YoFi.V3.Application.Features;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Tenancy.Models;
using YoFi.V3.Entities.Tenancy.Providers;
using YoFi.V3.Tests.Integration.Application.TestHelpers;

namespace YoFi.V3.Tests.Integration.Application;

[TestFixture]
public class TransactionSearchFeatureTests : FeatureTestBase
{
    private TransactionsFeature _feature = null!;
    private Tenant _tenant = null!;

    [SetUp]
    public new async Task SetUp()
    {
        await base.SetUp();

        _tenant = new Tenant { Name = "Search Tenant", Description = "Transactions search tests" };
        _context.Tenants.Add(_tenant);
        await _context.SaveChangesAsync();
        _feature = new TransactionsFeature(new SearchTestTenantProvider { CurrentTenant = _tenant }, _dataProvider);
    }

    private sealed class SearchTestTenantProvider : ITenantProvider
    {
        public required Tenant CurrentTenant { get; init; }
    }

    [Test]
    public async Task GetTransactionsAsync_SearchesAllTextFieldsAndAnySplitWithoutDuplicates()
    {
        AddTransaction("Morning COFFEE", 10m);
        AddTransaction("Transaction memo", 11m, memo: "A Coffee memo");
        AddTransaction("Split memo", 12m, splits:
        [
            new Split { Amount = 12m, Category = "Other", Order = 0 },
            new Split { Amount = 12m, Category = "Unrelated", Memo = "Coffee split memo", Order = 1 }
        ]);
        AddTransaction("Split category", 13m, splits:
        [
            new Split { Amount = 6m, Category = "Other", Order = 0 },
            new Split { Amount = 7m, Category = "Coffee category", Memo = "coffee split memo", Order = 1 },
            new Split { Amount = 0m, Category = "Another coffee category", Order = 2 }
        ]);
        AddTransaction("Does not match", 14m, memo: null,
            splits: [new Split { Amount = 14m, Category = "Other", Order = 0 }]);
        await _context.SaveChangesAsync();

        var result = await _feature.GetTransactionsAsync(search: "  cOfFeE  ");

        Assert.That(result.Metadata.TotalCount, Is.EqualTo(4));
        Assert.That(result.Items.Select(item => item.Payee),
            Is.EquivalentTo(new[] { "Morning COFFEE", "Transaction memo", "Split memo", "Split category" }));
    }

    [Test]
    public async Task GetTransactionsAsync_SearchUsesLiteralTextAndExactSignedOrUnsignedAmounts()
    {
        AddTransaction("100%_real", 1m);
        AddTransaction("100xxreal", 2m);
        AddTransaction("Positive amount", 42m);
        AddTransaction("Negative amount", -42m);
        AddTransaction("Bank 42 reference", 1m);
        AddTransaction("Larger amount", 142m);
        AddTransaction("Fractional amount", 42.5m);
        AddTransaction("Split amount only", 1m,
            splits: [new Split { Amount = 99m, Category = "Other", Order = 0 }]);
        await _context.SaveChangesAsync();

        var literalResult = await _feature.GetTransactionsAsync(search: "%_");
        var unsignedResult = await _feature.GetTransactionsAsync(search: "42");
        var signedResult = await _feature.GetTransactionsAsync(search: "+$42");
        var negativeResult = await _feature.GetTransactionsAsync(search: "$-42");
        var splitAmountResult = await _feature.GetTransactionsAsync(search: "99");

        Assert.That(literalResult.Items.Select(item => item.Payee), Is.EqualTo(new[] { "100%_real" }));
        Assert.That(unsignedResult.Items.Select(item => item.Payee),
            Is.EquivalentTo(new[] { "Positive amount", "Negative amount", "Bank 42 reference" }));
        Assert.That(signedResult.Items.Select(item => item.Payee), Is.EqualTo(new[] { "Positive amount" }));
        Assert.That(negativeResult.Items.Select(item => item.Payee), Is.EqualTo(new[] { "Negative amount" }));
        Assert.That(splitAmountResult.Metadata.TotalCount, Is.Zero);
    }

    [Test]
    public async Task GetTransactionsAsync_TrimsSearchAndAppliesItBeforePaginationAndDateBounds()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var yesterday = today.AddDays(-1);
        for (var index = 0; index < 51; index++)
        {
            AddTransaction($"Unrelated {index}", 1m, date: today);
        }

        AddTransaction("Match on prior page", 1m, date: yesterday);
        await _context.SaveChangesAsync();

        var result = await _feature.GetTransactionsAsync(
            pageNumber: 1,
            fromDate: yesterday,
            toDate: yesterday,
            search: " Match ");
        var whitespaceSearch = await _feature.GetTransactionsAsync(search: "   ");

        Assert.That(result.Metadata.TotalCount, Is.EqualTo(1));
        Assert.That(result.Items.Select(item => item.Payee), Is.EqualTo(new[] { "Match on prior page" }));
        Assert.That(whitespaceSearch.Metadata.TotalCount, Is.EqualTo(52));
    }

    [Test]
    public async Task GetTransactionsAsync_SearchRemainsTenantScoped()
    {
        AddTransaction("Shared search term", 1m);
        var otherTenant = new Tenant { Name = "Other Search Tenant", Description = "Other tenant" };
        _context.Tenants.Add(otherTenant);
        await _context.SaveChangesAsync();
        _context.Transactions.Add(new Transaction
        {
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Payee = "Shared search term",
            Amount = 2m,
            TenantId = otherTenant.Id
        });
        await _context.SaveChangesAsync();

        var result = await _feature.GetTransactionsAsync(search: "Shared search term");

        Assert.That(result.Metadata.TotalCount, Is.EqualTo(1));
        Assert.That(result.Items.Single().Amount, Is.EqualTo(1m));
    }

    private Transaction AddTransaction(
        string payee,
        decimal amount,
        string? memo = null,
        DateOnly? date = null,
        IReadOnlyCollection<Split>? splits = null)
    {
        var transaction = new Transaction
        {
            Date = date ?? DateOnly.FromDateTime(DateTime.UtcNow),
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
}
