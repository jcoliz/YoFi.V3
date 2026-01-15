using YoFi.V3.Entities.Models;

namespace YoFi.V3.Tests.Integration.Application;

/// <summary>
/// Application Integration tests for PayeeMatchingRuleFeature.FindBestMatchAsync.
/// </summary>
[TestFixture]
public class PayeeMatchingRuleFeatureFindBestMatchAsyncTests : PayeeMatchingRuleFeatureTestBase
{
    [Test]
    public async Task FindBestMatchAsync_MatchingRule_ReturnsCategory()
    {
        // Given: Rule
        var rule = new PayeeMatchingRule
        {
            PayeePattern = "Amazon",
            PayeeIsRegex = false,
            Category = "Shopping",
            TenantId = _testTenant.Id
        };
        _context.PayeeMatchingRules.Add(rule);
        await _context.SaveChangesAsync();

        // When: Finding best match for payee
        var result = await _feature.FindBestMatchAsync("Amazon.com");

        // Then: Should return matching category
        Assert.That(result, Is.EqualTo("Shopping"));
    }

    [Test]
    public async Task FindBestMatchAsync_NoMatch_ReturnsNull()
    {
        // Given: Rule that doesn't match
        var rule = new PayeeMatchingRule
        {
            PayeePattern = "Amazon",
            PayeeIsRegex = false,
            Category = "Shopping",
            TenantId = _testTenant.Id
        };
        _context.PayeeMatchingRules.Add(rule);
        await _context.SaveChangesAsync();

        // When: Finding best match for non-matching payee
        var result = await _feature.FindBestMatchAsync("Safeway");

        // Then: Should return null
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task FindBestMatchAsync_DoesNotUpdateUsageStatistics()
    {
        // Given: Rule with usage statistics
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
        var originalMatchCount = rule.MatchCount;
        var originalLastUsedAt = rule.LastUsedAt;

        // When: Finding best match (which matches the rule)
        await _feature.FindBestMatchAsync("Amazon.com");

        // Then: Usage statistics should NOT be updated
        _context.ChangeTracker.Clear();
        var updatedRule = _context.PayeeMatchingRules.Single(r => r.Id == ruleId);
        Assert.That(updatedRule.MatchCount, Is.EqualTo(originalMatchCount));
        Assert.That(updatedRule.LastUsedAt, Is.EqualTo(originalLastUsedAt));
    }
}
