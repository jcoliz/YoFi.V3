using YoFi.V3.Entities.Exceptions;
using YoFi.V3.Entities.Models;

namespace YoFi.V3.Tests.Integration.Application;

/// <summary>
/// Application Integration tests for PayeeMatchingRuleFeature.DeleteRuleAsync.
/// </summary>
[TestFixture]
public class PayeeMatchingRuleFeatureDeleteRuleAsyncTests : PayeeMatchingRuleFeatureTestBase
{
    [Test]
    public async Task DeleteRuleAsync_ExistingRule_RemovesFromDatabase()
    {
        // Given: Existing rule
        var rule = new PayeeMatchingRule
        {
            PayeePattern = "Amazon",
            Category = "Shopping",
            TenantId = _testTenant.Id
        };
        _context.PayeeMatchingRules.Add(rule);
        await _context.SaveChangesAsync();
        var ruleKey = rule.Key;

        // When: Deleting the rule
        await _feature.DeleteRuleAsync(ruleKey);

        // Then: Rule should be removed from database
        var dbRule = _context.PayeeMatchingRules.SingleOrDefault(r => r.Key == ruleKey);
        Assert.That(dbRule, Is.Null);
    }

    [Test]
    public void DeleteRuleAsync_NonExistentKey_ThrowsNotFoundException()
    {
        // Given: Non-existent key
        var nonExistentKey = Guid.NewGuid();

        // When/Then: Deleting should throw
        Assert.ThrowsAsync<PayeeMatchingRuleNotFoundException>(
            async () => await _feature.DeleteRuleAsync(nonExistentKey));
    }
}
