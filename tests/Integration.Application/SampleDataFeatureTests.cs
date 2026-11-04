using System.Collections.Immutable;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Features;
using YoFi.V3.Application.Services;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Tenancy.Models;
using YoFi.V3.Entities.Tenancy.Providers;
using YoFi.V3.Tests.Integration.Application.TestHelpers;

namespace YoFi.V3.Tests.Integration.Application;

[TestFixture]
public class SampleDataFeatureTests : FeatureTestBase
{
    private MemoryCache _cache = null!;
    private Tenant _tenant = null!;
    private Tenant _other = null!;
    private SampleDataFeature _feature = null!;
    private DataImportService _importer = null!;
    private TestTenantProvider _tenantProvider = null!;
    private SampleDataRequest Request => new(
        "personal-finance",
        _feature.GetScenarios().Single(s => s.Id == "personal-finance").Revision,
        new(2024, 1, 1),
        new(2024, 1, 31));

    [SetUp]
    public new async Task SetUp()
    {
        await base.SetUp();
        _tenant = new Tenant { Name = "Samples", Description = "Target workspace" };
        _other = new Tenant { Name = "Other", Description = "Unchanged workspace" };
        _context.Tenants.AddRange(_tenant, _other);
        await _context.SaveChangesAsync();
        _tenantProvider = new TestTenantProvider { CurrentTenant = _tenant };
        _cache = new MemoryCache(new MemoryCacheOptions());
        var regex = new RegexValidationService();
        _importer = new DataImportService(_tenantProvider, _dataProvider, _cache, regex);
        _feature = new SampleDataFeature(new SampleScenarioLoader(), new SampleDataGenerator(), _importer, _tenantProvider, _dataProvider, regex);
    }

    private sealed class TestTenantProvider : ITenantProvider
    {
        public required Tenant CurrentTenant { get; init; }
    }

    [TearDown]
    public void DisposeCache() => _cache.Dispose();

    [Test]
    public async Task Load_PreservesExistingAndOtherTenantDataAndPendingReviews_AndRepeatedLoadAddsFreshCopies()
    {
        var existing = new Transaction { TenantId = _tenant.Id, Payee = "Existing", Amount = -123, Date = new(2024, 1, 5) };
        var other = new Transaction { TenantId = _other.Id, Payee = "Other", Amount = 456, Date = new(2024, 1, 5) };
        var rule = new PayeeMatchingRule { TenantId = _tenant.Id, PayeePattern = "Existing", Category = "Existing" };
        var pending = new ImportReviewTransaction { TenantId = _tenant.Id, Payee = "Pending", Amount = -5, Date = new(2024, 1, 5) };
        _context.Transactions.AddRange(existing, other);
        _context.PayeeMatchingRules.Add(rule);
        _context.ImportReviewTransactions.Add(pending);
        await _context.SaveChangesAsync();

        var preview = await _feature.PreviewAsync(Request);
        var loaded = await _feature.LoadAsync(Request);
        _context.ChangeTracker.Clear();
        var first = await _context.Transactions.Where(t => t.TenantId == _tenant.Id && t.Key != existing.Key).Include(t => t.Splits).ToListAsync();
        var repeated = await _feature.LoadAsync(Request);
        _context.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(preview.ExistingTransactionCount, Is.EqualTo(1));
            Assert.That(loaded.Created, Is.EqualTo(preview.Counts));
            Assert.That(repeated.Created, Is.EqualTo(loaded.Created));
            Assert.That(loaded.Updated.Total, Is.Zero);
            Assert.That(await _context.Transactions.CountAsync(t => t.TenantId == _tenant.Id), Is.EqualTo(1 + 2 * loaded.Created.Transactions));
            Assert.That(await _context.PayeeMatchingRules.CountAsync(r => r.TenantId == _tenant.Id), Is.EqualTo(1 + 2 * loaded.Created.PayeeMatchingRules));
            Assert.That(await _context.Transactions.Where(t => t.Key == existing.Key).Select(t => t.Amount).SingleAsync(), Is.EqualTo(-123));
            Assert.That(await _context.Transactions.CountAsync(t => t.TenantId == _other.Id), Is.EqualTo(1));
            Assert.That(await _context.Transactions.Where(t => t.Key == other.Key).Select(t => t.Amount).SingleAsync(), Is.EqualTo(456));
            Assert.That(await _context.PayeeMatchingRules.Where(r => r.Key == rule.Key).Select(r => r.Category).SingleAsync(), Is.EqualTo("Existing"));
            Assert.That(await _context.ImportReviewTransactions.CountAsync(r => r.Key == pending.Key), Is.EqualTo(1));
            foreach (var transaction in first)
            {
                Assert.That(transaction.Splits, Has.Count.EqualTo(1));
                Assert.That(transaction.Splits.Single().Amount, Is.EqualTo(transaction.Amount));
                Assert.That(transaction.Splits.Single().Order, Is.Zero);
            }
        }
        var all = await _context.Transactions.Where(t => t.TenantId == _tenant.Id && t.Key != existing.Key).ToListAsync();
        Assert.That(all.Select(t => t.Key), Is.Unique);
        Assert.That(all.GroupBy(t => t.ExternalId).Select(g => g.Count()), Is.All.EqualTo(2));
    }

    [Test]
    public async Task PersistenceFailure_RollsBackBothTypesAndSplits()
    {
        await _context.Database.ExecuteSqlRawAsync("""
            CREATE TRIGGER reject_sample_transactions BEFORE INSERT ON "YoFi.V3.Transactions"
            BEGIN
                SELECT RAISE(ABORT, 'Injected sample persistence failure');
            END;
            """);

        await Assert.ThrowsAsync<DbUpdateException>(() => _feature.LoadAsync(Request));
        _context.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await _context.Transactions.CountAsync(), Is.Zero);
            Assert.That(await _context.Splits.CountAsync(), Is.Zero);
            Assert.That(await _context.PayeeMatchingRules.CountAsync(), Is.Zero);
            Assert.That(await _context.Tenants.CountAsync(), Is.EqualTo(2));
        }
    }

    [Test]
    public async Task ExistingRuleCache_IsInvalidatedAfterCommitAndSamplesMatchThroughRealRuleFeature()
    {
        var ruleFeature = new PayeeMatchingRuleFeature(_tenantProvider, _dataProvider, _cache);
        Assert.That((await ruleFeature.GetRulesAsync()).Items, Is.Empty);
        await _feature.LoadAsync(Request);
        var scenario = new SampleScenarioLoader().Scenarios[0];
        var payees = scenario.Patterns.SelectMany(p => p.Payees).Distinct().ToArray();
        var matches = await ruleFeature.MatchPayeesAsync(payees);
        Assert.That(matches, Has.Count.EqualTo(payees.Length));
        foreach (var pattern in scenario.Patterns)
        {
            foreach (var payee in pattern.Payees)
            {
                Assert.That(matches[payee], Is.EqualTo(pattern.Category));
            }
        }
    }

    [Test]
    public async Task SharedImport_SanitizesCategoriesAndInitializesAuditAndUsage()
    {
        var before = DateTimeOffset.UtcNow;
        await _importer.CreateAsync(new SampleDataSet(
            [new TransactionEditDto(new(2024, 1, 1), -5, "Store", null, null, null, " food :: groceries ")],
            [new PayeeMatchingRuleEditDto("Store", false, " food :: groceries ")]));
        _context.ChangeTracker.Clear();
        var transaction = await _context.Transactions.Include(t => t.Splits).SingleAsync();
        var rule = await _context.PayeeMatchingRules.SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(transaction.Splits.Single().Category, Is.EqualTo("Food:Groceries"));
            Assert.That(rule.Category, Is.EqualTo("Food:Groceries"));
            Assert.That(rule.CreatedAt, Is.InRange(before, DateTimeOffset.UtcNow));
            Assert.That(rule.ModifiedAt, Is.EqualTo(rule.CreatedAt));
            Assert.That(rule.MatchCount, Is.Zero);
            Assert.That(rule.LastUsedAt, Is.Null);
        }
    }
}
