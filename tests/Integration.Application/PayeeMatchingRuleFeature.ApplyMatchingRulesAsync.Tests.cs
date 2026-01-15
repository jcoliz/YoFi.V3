using YoFi.V3.Application.Services;
using YoFi.V3.Entities.Models;

namespace YoFi.V3.Tests.Integration.Application;

/// <summary>
/// Application Integration tests for PayeeMatchingRuleFeature.ApplyMatchingRulesAsync.
/// </summary>
[TestFixture]
public class PayeeMatchingRuleFeatureApplyMatchingRulesAsyncTests : PayeeMatchingRuleFeatureTestBase
{
    [Test]
    public async Task ApplyMatchingRulesAsync_NoRules_ReturnsAllNulls()
    {
        // Given: No rules in database
        var transactions = new[]
        {
            new TestMatchableTransaction("Amazon.com"),
            new TestMatchableTransaction("Safeway"),
            new TestMatchableTransaction("Target")
        };

        // When: Applying matching rules
        var result = await _feature.ApplyMatchingRulesAsync(transactions);

        // Then: Should return array of nulls
        Assert.That(result, Has.Count.EqualTo(3));
        Assert.That(result[0], Is.Null);
        Assert.That(result[1], Is.Null);
        Assert.That(result[2], Is.Null);
    }

    [Test]
    public async Task ApplyMatchingRulesAsync_WithMatchingRules_ReturnsCategories()
    {
        // Given: Rules for some payees
        var rule1 = new PayeeMatchingRule
        {
            PayeePattern = "Amazon",
            PayeeIsRegex = false,
            Category = "Shopping",
            TenantId = _testTenant.Id
        };
        var rule2 = new PayeeMatchingRule
        {
            PayeePattern = "Safeway",
            PayeeIsRegex = false,
            Category = "Groceries",
            TenantId = _testTenant.Id
        };
        _context.PayeeMatchingRules.AddRange(rule1, rule2);
        await _context.SaveChangesAsync();

        // And: Transactions with matching and non-matching payees
        var transactions = new[]
        {
            new TestMatchableTransaction("Amazon.com"),
            new TestMatchableTransaction("Target"),
            new TestMatchableTransaction("Safeway Store")
        };

        // When: Applying matching rules
        var result = await _feature.ApplyMatchingRulesAsync(transactions);

        // Then: Should return categories in parallel order
        Assert.That(result, Has.Count.EqualTo(3));
        Assert.That(result[0], Is.EqualTo("Shopping"));
        Assert.That(result[1], Is.Null);
        Assert.That(result[2], Is.EqualTo("Groceries"));
    }

    [Test]
    public async Task ApplyMatchingRulesAsync_PreservesTransactionOrder()
    {
        // Given: Rule
        var rule = new PayeeMatchingRule
        {
            PayeePattern = "Store",
            PayeeIsRegex = false,
            Category = "Shopping",
            TenantId = _testTenant.Id
        };
        _context.PayeeMatchingRules.Add(rule);
        await _context.SaveChangesAsync();

        // And: Transactions in specific order
        var transactions = new[]
        {
            new TestMatchableTransaction("Store A"),
            new TestMatchableTransaction("Bank"),
            new TestMatchableTransaction("Store B"),
            new TestMatchableTransaction("Restaurant"),
            new TestMatchableTransaction("Store C")
        };

        // When: Applying matching rules
        var result = await _feature.ApplyMatchingRulesAsync(transactions);

        // Then: Result order should match input order
        Assert.That(result, Has.Count.EqualTo(5));
        Assert.That(result[0], Is.EqualTo("Shopping"));
        Assert.That(result[1], Is.Null);
        Assert.That(result[2], Is.EqualTo("Shopping"));
        Assert.That(result[3], Is.Null);
        Assert.That(result[4], Is.EqualTo("Shopping"));
    }

    [Test]
    public async Task ApplyMatchingRulesAsync_DuplicatePayees_ReturnsConsistentCategories()
    {
        // Given: Rule
        var rule = new PayeeMatchingRule
        {
            PayeePattern = "Amazon",
            PayeeIsRegex = false,
            Category = "Shopping",
            TenantId = _testTenant.Id,
            MatchCount = 0
        };
        _context.PayeeMatchingRules.Add(rule);
        await _context.SaveChangesAsync();
        var ruleId = rule.Id;

        // And: Transactions with duplicate payees
        var transactions = new[]
        {
            new TestMatchableTransaction("Amazon.com"),
            new TestMatchableTransaction("Amazon.com"),
            new TestMatchableTransaction("Amazon.com")
        };

        // When: Applying matching rules
        var result = await _feature.ApplyMatchingRulesAsync(transactions);

        // Then: All should get same category
        Assert.That(result, Has.Count.EqualTo(3));
        Assert.That(result[0], Is.EqualTo("Shopping"));
        Assert.That(result[1], Is.EqualTo("Shopping"));
        Assert.That(result[2], Is.EqualTo("Shopping"));

        // And: MatchCount should be incremented only once (de-duplication)
        _context.ChangeTracker.Clear();
        var updatedRule = _context.PayeeMatchingRules.Single(r => r.Id == ruleId);
        Assert.That(updatedRule.MatchCount, Is.EqualTo(1));
    }

    [Test]
    public async Task ApplyMatchingRulesAsync_UpdatesUsageStatistics()
    {
        // Given: Rule with initial statistics
        var rule = new PayeeMatchingRule
        {
            PayeePattern = "Amazon",
            PayeeIsRegex = false,
            Category = "Shopping",
            TenantId = _testTenant.Id,
            MatchCount = 5,
            LastUsedAt = DateTimeOffset.UtcNow.AddDays(-7)
        };
        _context.PayeeMatchingRules.Add(rule);
        await _context.SaveChangesAsync();
        var ruleId = rule.Id;

        // And: Transactions with matching payee
        var transactions = new[]
        {
            new TestMatchableTransaction("Amazon.com"),
            new TestMatchableTransaction("Target")
        };

        // When: Applying matching rules
        var beforeMatch = DateTimeOffset.UtcNow;
        await _feature.ApplyMatchingRulesAsync(transactions);
        var afterMatch = DateTimeOffset.UtcNow;

        // Then: MatchCount should be incremented
        _context.ChangeTracker.Clear();
        var updatedRule = _context.PayeeMatchingRules.Single(r => r.Id == ruleId);
        Assert.That(updatedRule.MatchCount, Is.EqualTo(6));

        // And: LastUsedAt should be updated
        Assert.That(updatedRule.LastUsedAt, Is.Not.Null);
        Assert.That(updatedRule.LastUsedAt!.Value, Is.GreaterThanOrEqualTo(beforeMatch));
        Assert.That(updatedRule.LastUsedAt!.Value, Is.LessThanOrEqualTo(afterMatch));
    }

    [Test]
    public async Task ApplyMatchingRulesAsync_RegexRule_MatchesCorrectly()
    {
        // Given: Regex rule
        var rule = new PayeeMatchingRule
        {
            PayeePattern = "^AMZN.*",
            PayeeIsRegex = true,
            Category = "Shopping",
            TenantId = _testTenant.Id
        };
        _context.PayeeMatchingRules.Add(rule);
        await _context.SaveChangesAsync();

        // And: Transactions with regex-matching and non-matching payees
        var transactions = new[]
        {
            new TestMatchableTransaction("AMZN Marketplace"),
            new TestMatchableTransaction("Amazon.com"),
            new TestMatchableTransaction("AMZN Prime")
        };

        // When: Applying matching rules
        var result = await _feature.ApplyMatchingRulesAsync(transactions);

        // Then: Only regex matches should get category
        Assert.That(result, Has.Count.EqualTo(3));
        Assert.That(result[0], Is.EqualTo("Shopping"));
        Assert.That(result[1], Is.Null);
        Assert.That(result[2], Is.EqualTo("Shopping"));
    }

    [Test]
    public async Task ApplyMatchingRulesAsync_EmptyCollection_ReturnsEmptyArray()
    {
        // Given: Some rules exist
        var rule = new PayeeMatchingRule
        {
            PayeePattern = "Amazon",
            PayeeIsRegex = false,
            Category = "Shopping",
            TenantId = _testTenant.Id
        };
        _context.PayeeMatchingRules.Add(rule);
        await _context.SaveChangesAsync();

        // When: Applying rules to empty collection
        var result = await _feature.ApplyMatchingRulesAsync(Array.Empty<IMatchableTransaction>());

        // Then: Should return empty array
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task ApplyMatchingRulesAsync_ConflictResolution_PrefersNewerRule()
    {
        // Given: Two overlapping rules with different ModifiedAt
        var olderRule = new PayeeMatchingRule
        {
            PayeePattern = "Amazon",
            PayeeIsRegex = false,
            Category = "Shopping",
            TenantId = _testTenant.Id,
            ModifiedAt = DateTimeOffset.UtcNow.AddDays(-10)
        };
        var newerRule = new PayeeMatchingRule
        {
            PayeePattern = "Amazon",
            PayeeIsRegex = false,
            Category = "E-Commerce",
            TenantId = _testTenant.Id,
            ModifiedAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
        _context.PayeeMatchingRules.AddRange(olderRule, newerRule);
        await _context.SaveChangesAsync();

        // And: Transaction with matching payee
        var transactions = new[]
        {
            new TestMatchableTransaction("Amazon.com")
        };

        // When: Applying matching rules
        var result = await _feature.ApplyMatchingRulesAsync(transactions);

        // Then: Should use newer rule's category
        Assert.That(result[0], Is.EqualTo("E-Commerce"));
    }
}

/// <summary>
/// Test implementation of IMatchableTransaction for testing ApplyMatchingRulesAsync.
/// </summary>
file class TestMatchableTransaction(string payee) : IMatchableTransaction
{
    public string Payee { get; } = payee;
}
