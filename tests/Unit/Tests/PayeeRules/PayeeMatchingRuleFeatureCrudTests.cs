using System.Linq.Expressions;
using DotNetNuxt.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Features;
using YoFi.V3.Application.Services;
using YoFi.V3.Entities.Exceptions;
using YoFi.V3.Entities.Models;
using YoFi.V3.Tests.Unit.TestHelpers;

namespace YoFi.V3.Tests.Unit.Application.PayeeRules;

[TestFixture]
public class PayeeMatchingRuleFeatureCrudTests
{
    private Mock<IDataProvider> _data = null!;
    private TestTenantProvider _tenant = null!;
    private MemoryCache _cache = null!;
    private Mock<TimeProvider> _time = null!;
    private PayeeMatchingRuleFeature _feature = null!;
    private List<PayeeMatchingRule> _rules = null!;

    [SetUp]
    public void SetUp()
    {
        _data = new Mock<IDataProvider>();
        _tenant = new TestTenantProvider();
        _tenant.CurrentTenant.Id = 42;
        _cache = new MemoryCache(new MemoryCacheOptions());
        _time = new Mock<TimeProvider>();
        _time.Setup(time => time.GetUtcNow())
            .Returns(new DateTimeOffset(2040, 7, 1, 12, 0, 0, TimeSpan.Zero));
        _rules = [];
        _data.Setup(data => data.Get<PayeeMatchingRule>()).Returns(() => _rules.AsQueryable());
        _data.Setup(data => data.ToListNoTrackingAsync(It.IsAny<IQueryable<PayeeMatchingRule>>()))
            .Returns((IQueryable<PayeeMatchingRule> query) => Task.FromResult(query.ToList()));
        _data.Setup(data => data.ToListAsync(It.IsAny<IQueryable<PayeeMatchingRule>>()))
            .Returns((IQueryable<PayeeMatchingRule> query) => Task.FromResult(query.ToList()));
        _data.Setup(data => data.SingleOrDefaultAsync(It.IsAny<IQueryable<PayeeMatchingRule>>()))
            .Returns((IQueryable<PayeeMatchingRule> query) => Task.FromResult(query.SingleOrDefault()));
        _data.Setup(data => data.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _feature = new PayeeMatchingRuleFeature(_tenant, _data.Object, _cache, _time.Object);
    }

    [TearDown]
    public void TearDown() => _cache.Dispose();

    [Test]
    public async Task GetRulesAsync_FiltersSortsPaginatesAndReturnsMetadata()
    {
        _rules.AddRange([
            Rule("Zeta", "Home", 1),
            Rule("Amazon", "Shopping", 9),
            Rule("Coffee", "Food", 5)
        ]);

        var result = await _feature.GetRulesAsync(1, PayeeRuleSortBy.PayeePattern, "a");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Items.Select(item => item.PayeePattern), Is.EqualTo(new[] { "Amazon", "Zeta" }));
            Assert.That(result.Metadata.TotalCount, Is.EqualTo(2));
            Assert.That(result.Metadata.PageNumber, Is.EqualTo(1));
        }
    }

    [TestCase(PayeeRuleSortBy.Category)]
    [TestCase(PayeeRuleSortBy.LastUsedAt)]
    [TestCase(PayeeRuleSortBy.NumberOfMatches)]
    [TestCase((PayeeRuleSortBy)999)]
    public async Task GetRulesAsync_SupportedAndFallbackSortModes(PayeeRuleSortBy sortBy)
    {
        var coffee = Rule("Coffee", "Food", 1);
        coffee.LastUsedAt = new DateTimeOffset(2040, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var amazon = Rule("Amazon", "Shopping", 9);
        amazon.LastUsedAt = new DateTimeOffset(2040, 6, 1, 0, 0, 0, TimeSpan.Zero);
        _rules.AddRange([coffee, amazon]);

        var result = await _feature.GetRulesAsync(1, sortBy);

        Assert.That(result.Items, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task GetRuleByKeyAsync_ReturnsMatchingTenantRule()
    {
        var rule = Rule("Coffee", "Food", 3);
        _rules.Add(rule);

        var result = await _feature.GetRuleByKeyAsync(rule.Key);

        Assert.That(result.Category, Is.EqualTo("Food"));
        Assert.That(result.MatchCount, Is.EqualTo(3));
    }

    [Test]
    public async Task GetRuleByKeyAsync_OtherTenantOrMissingRuleThrows()
    {
        var rule = Rule("Coffee", "Food", 0);
        rule.TenantId = 99;
        _rules.Add(rule);

        await Assert.ThrowsAsync<PayeeMatchingRuleNotFoundException>(() => _feature.GetRuleByKeyAsync(rule.Key));
    }

    [Test]
    public async Task CreateRuleAsync_AddsSanitizedRuleInvalidatesCacheAndCancelsWhenRequested()
    {
        var dto = new PayeeMatchingRuleEditDto("  Amazon  ", false, " shopping : household ");
        _cache.Set(PayeeMatchingRuleFeature.CacheKey(42), _rules);

        var result = await _feature.CreateRuleAsync(dto);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.PayeePattern, Is.EqualTo("  Amazon  "));
            Assert.That(result.Category, Is.EqualTo("Shopping:Household"));
            Assert.That(result.TransactionsCategorized, Is.Zero);
            Assert.That(_cache.TryGetValue(PayeeMatchingRuleFeature.CacheKey(42), out _), Is.False);
        }
        _data.Verify(data => data.Add(It.Is<IModel>(item => item is PayeeMatchingRule)), Times.Once);
        _data.Verify(data => data.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task CreateRuleAsync_CancellationThrowsBeforeCreation()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => _feature.CreateRuleAsync(
            new PayeeMatchingRuleEditDto("Coffee", false, "Food"), cancellationToken: source.Token));
    }

    [Test]
    public async Task CreateRuleAsync_AppliesNewRuleToUncategorizedTenantTransactions()
    {
        var transaction = new Transaction
        {
            TenantId = 42,
            Payee = "Coffee Shop",
            Splits = [new Split { Category = string.Empty, Amount = -10m }]
        };
        _data.Setup(data => data.GetIncluding<Transaction, ICollection<Split>>(
                It.IsAny<Expression<Func<Transaction, ICollection<Split>>>>()))
            .Returns(new[] { transaction }.AsQueryable());
        _data.Setup(data => data.ToListAsync(It.IsAny<IQueryable<Transaction>>()))
            .Returns((IQueryable<Transaction> query) => Task.FromResult(query.ToList()));

        var result = await _feature.CreateRuleAsync(
            new PayeeMatchingRuleEditDto("Coffee", false, " food : dining "), true);

        Assert.That(result.TransactionsCategorized, Is.EqualTo(1));
        Assert.That(transaction.Splits.Single().Category, Is.EqualTo("Food:Dining"));
        _data.Verify(data => data.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task UpdateRuleAsync_UpdatesSanitizedFieldsAndInvalidatesCache()
    {
        var rule = Rule("Old", "Old", 2);
        _rules.Add(rule);
        _cache.Set(PayeeMatchingRuleFeature.CacheKey(42), _rules);

        var result = await _feature.UpdateRuleAsync(rule.Key,
            new PayeeMatchingRuleEditDto("New Pattern", true, " new : category "));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.PayeePattern, Is.EqualTo("New Pattern"));
            Assert.That(result.PayeeIsRegex, Is.True);
            Assert.That(result.Category, Is.EqualTo("New:Category"));
            Assert.That(rule.ModifiedAt, Is.EqualTo(_time.Object.GetUtcNow()));
            Assert.That(_cache.TryGetValue(PayeeMatchingRuleFeature.CacheKey(42), out _), Is.False);
        }
        _data.Verify(data => data.UpdateRange(It.Is<IEnumerable<IModel>>(items => items.Contains(rule))), Times.Once);
    }

    [Test]
    public async Task DeleteRuleAsync_RemovesRuleAndInvalidatesCache()
    {
        var rule = Rule("Old", "Old", 0);
        _rules.Add(rule);
        _cache.Set(PayeeMatchingRuleFeature.CacheKey(42), _rules);

        await _feature.DeleteRuleAsync(rule.Key);

        _data.Verify(data => data.Remove(rule), Times.Once);
        _data.Verify(data => data.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.That(_cache.TryGetValue(PayeeMatchingRuleFeature.CacheKey(42), out _), Is.False);
    }

    [Test]
    public async Task MatchPayeesAsync_EmptyAndMatchedPayeesUpdateUsage()
    {
        var rule = Rule("Coffee", "Food", 2);
        _rules.Add(rule);
        _cache.Set(PayeeMatchingRuleFeature.CacheKey(42), _rules);

        var empty = await _feature.MatchPayeesAsync([]);
        var matches = await _feature.MatchPayeesAsync(["Coffee Shop", "Coffee Shop", "Unknown"]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(empty, Is.Empty);
            Assert.That(matches, Is.EqualTo(new Dictionary<string, string> { ["Coffee Shop"] = "Food" }));
            Assert.That(rule.MatchCount, Is.EqualTo(3));
            Assert.That(rule.LastUsedAt, Is.EqualTo(_time.Object.GetUtcNow()));
        }
    }

    [Test]
    public async Task FindBestMatchAsyncAndApplyMatchingRulesAsyncPreserveInputOrder()
    {
        var rule = Rule("Coffee", "Food", 0);
        _rules.Add(rule);

        var best = await _feature.FindBestMatchAsync("Coffee Shop");
        var categories = await _feature.ApplyMatchingRulesAsync([
            new TestMatchable("Coffee Shop"),
            new TestMatchable("Unknown")
        ]);

        Assert.That(best, Is.EqualTo("Food"));
        Assert.That(categories, Is.EqualTo(new string?[] { "Food", null }));
    }

    [Test]
    public async Task ApplyMatchingRulesAsyncEmptyCollectionDoesNotLoadRules()
    {
        var result = await _feature.ApplyMatchingRulesAsync([]);

        Assert.That(result, Is.Empty);
        _data.Verify(data => data.Get<PayeeMatchingRule>(), Times.Never);
    }

    private PayeeMatchingRule Rule(string pattern, string category, int matchCount)
    {
        var now = _time.Object.GetUtcNow();
        return new PayeeMatchingRule
        {
            Id = _rules.Count + 1,
            Key = Guid.NewGuid(),
            TenantId = 42,
            PayeePattern = pattern,
            PayeeIsRegex = false,
            Category = category,
            CreatedAt = now,
            ModifiedAt = now,
            MatchCount = matchCount
        };
    }

    private sealed record TestMatchable(string Payee) : IMatchableTransaction;
}
