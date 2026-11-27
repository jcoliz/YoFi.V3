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

public class SampleDataFeatureTests : FeatureTestBase
{
    private MemoryCache _cache = null!;
    private Tenant _tenant = null!;
    private Tenant _other = null!;
    private SampleDataFeature _feature = null!;
    private DataImportService _importer = null!;
    private TestTenantProvider _tenantProvider = null!;
    private TestTimeProvider _timeProvider = null!;
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
        _timeProvider = new TestTimeProvider();
        _importer = new DataImportService(_tenantProvider, _dataProvider, _cache, regex, _timeProvider);
        _feature = new SampleDataFeature(new SampleScenarioLoader(), new SampleDataGenerator(), _importer, _tenantProvider, _dataProvider, regex, _timeProvider);
    }

    private sealed class TestTenantProvider : ITenantProvider
    {
        public required Tenant CurrentTenant { get; init; }
    }

    [TearDown]
    public void DisposeCache() => _cache.Dispose();

    [Test]
    public async Task Load_PersistsEveryPaycheckAllocationWithExactAmountsAndOrder()
    {
        var scenario = new SampleScenarioLoader().Scenarios.Single(s => s.Id == Request.ScenarioId);
        var generated = new SampleDataGenerator().Generate(scenario, Request.StartDate!.Value, Request.EndDate!.Value);
        await _feature.LoadAsync(Request);
        _context.ChangeTracker.Clear();
        var paychecks = await _context.Transactions.Include(t => t.Splits)
            .Where(t => t.TenantId == _tenant.Id && t.Payee == "Megacorp Inc").ToListAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(paychecks, Has.Count.EqualTo(2));
            foreach (var paycheck in paychecks)
            {
                var expected = generated.TransactionSplits[paycheck.ExternalId!];
                var actual = paycheck.Splits.OrderBy(s => s.Order).ToArray();
                Assert.That(actual.Select(s => s.Category), Is.EqualTo(expected.Select(s => s.Category)));
                Assert.That(actual.Select(s => s.Amount), Is.EqualTo(expected.Select(s => s.Amount)));
                Assert.That(actual.Select(s => s.Order), Is.EqualTo(Enumerable.Range(0, 12)));
                Assert.That(actual.Select(s => s.Key), Is.Unique.And.All.Not.EqualTo(Guid.Empty));
                Assert.That(actual.Sum(s => s.Amount), Is.EqualTo(paycheck.Amount));
                Assert.That(actual[0].Amount, Is.EqualTo(5000m));
                Assert.That(actual[2].Amount, Is.EqualTo(-687m));
            }
        }
    }

    [Test]
    public async Task SplitPersistenceFailure_RollsBackTransactionsRulesAndSplits()
    {
        await _context.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION reject_sample_splits() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'Injected sample split failure';
            END;
            $$;
            CREATE TRIGGER reject_sample_splits BEFORE INSERT ON "Splits"
            FOR EACH ROW WHEN (NEW."Order" = 1)
            EXECUTE FUNCTION reject_sample_splits();
            """);
        await Assert.ThrowsAsync<DbUpdateException>(() => _feature.LoadAsync(Request));
        _context.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await _context.Transactions.CountAsync(), Is.Zero);
            Assert.That(await _context.Splits.CountAsync(), Is.Zero);
            Assert.That(await _context.PayeeMatchingRules.CountAsync(), Is.Zero);
        }
    }


    [Test]
    public async Task PersistenceFailure_RollsBackBothTypesAndSplits()
    {
        await _context.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION reject_sample_transactions() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'Injected sample persistence failure';
            END;
            $$;
            CREATE TRIGGER reject_sample_transactions BEFORE INSERT ON "Transactions"
            FOR EACH ROW EXECUTE FUNCTION reject_sample_transactions();
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
        var ruleFeature = new PayeeMatchingRuleFeature(_tenantProvider, _dataProvider, _cache, _timeProvider);
        Assert.That((await ruleFeature.GetRulesAsync()).Items, Is.Empty);
        await _feature.LoadAsync(Request);
        _timeProvider.UtcNow = _timeProvider.GetUtcNow().AddDays(1);
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

        _context.ChangeTracker.Clear();
        var rules = await _context.PayeeMatchingRules.ToListAsync();
        Assert.That(rules.Select(rule => rule.CreatedAt), Is.All.EqualTo(_timeProvider.GetUtcNow().AddDays(-1)));
        var usedRules = rules.Where(rule => rule.MatchCount > 0).ToArray();
        Assert.That(usedRules, Is.Not.Empty);
        Assert.That(usedRules.Select(rule => rule.LastUsedAt), Is.All.EqualTo(_timeProvider.GetUtcNow()));
    }


}
