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
            LastUsedAt = _timeProvider.GetUtcNow().AddDays(-7)
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
        await _feature.ApplyMatchingRulesAsync(transactions);

        // Then: MatchCount should be incremented
        _context.ChangeTracker.Clear();
        var updatedRule = _context.PayeeMatchingRules.Single(r => r.Id == ruleId);
        Assert.That(updatedRule.MatchCount, Is.EqualTo(6));

        // And: LastUsedAt should be updated
        Assert.That(updatedRule.LastUsedAt, Is.EqualTo(_timeProvider.GetUtcNow()));
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


}

/// <summary>
/// Test implementation of IMatchableTransaction for testing ApplyMatchingRulesAsync.
/// </summary>
file class TestMatchableTransaction(string payee) : IMatchableTransaction
{
    public string Payee { get; } = payee;
}
