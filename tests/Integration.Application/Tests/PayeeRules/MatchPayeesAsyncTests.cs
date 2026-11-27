using YoFi.V3.Entities.Models;

namespace YoFi.V3.Tests.Integration.Application;

/// <summary>
/// Application Integration tests for PayeeMatchingRuleFeature.MatchPayeesAsync.
/// </summary>
public class PayeeMatchingRuleFeatureMatchPayeesAsyncTests : PayeeMatchingRuleFeatureTestBase
{



    [Test]
    public async Task MatchPayeesAsync_UpdatesUsageStatistics()
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

        // When: Matching payees (causes rule to match)
        var payees = new[] { "Amazon.com" };
        await _feature.MatchPayeesAsync(payees);

        // Then: MatchCount should be incremented
        _context.ChangeTracker.Clear(); // Force reload from database
        var updatedRule = _context.PayeeMatchingRules.Single(r => r.Id == ruleId);
        Assert.That(updatedRule.MatchCount, Is.EqualTo(6));

        // And: LastUsedAt should be updated
        Assert.That(updatedRule.LastUsedAt, Is.EqualTo(_timeProvider.GetUtcNow()));
    }





}
