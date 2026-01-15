using YoFi.V3.Application.Dto;
using YoFi.V3.Entities.Models;

namespace YoFi.V3.Tests.Integration.Application;

/// <summary>
/// Application Integration tests for PayeeMatchingRuleFeature cache invalidation.
/// </summary>
[TestFixture]
public class PayeeMatchingRuleFeatureCacheInvalidationTests : PayeeMatchingRuleFeatureTestBase
{
    [Test]
    public async Task CreateRuleAsync_InvalidatesCache()
    {
        // Given: Cache is populated by reading rules
        var initialResult = await _feature.GetRulesAsync();
        Assert.That(initialResult.Items, Is.Empty);

        // When: Creating a new rule
        var ruleDto = new PayeeMatchingRuleEditDto("Amazon", false, "Shopping");
        await _feature.CreateRuleAsync(ruleDto);

        // Then: Next read should see the new rule
        var afterCreateResult = await _feature.GetRulesAsync();
        Assert.That(afterCreateResult.Items, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task UpdateRuleAsync_InvalidatesCache()
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

        // And: Cache is populated
        var initialResult = await _feature.GetRulesAsync();
        Assert.That(initialResult.Items.First().Category, Is.EqualTo("Shopping"));

        // When: Updating the rule
        var updateDto = new PayeeMatchingRuleEditDto("Amazon", false, "Online");
        await _feature.UpdateRuleAsync(rule.Key, updateDto);

        // Then: Next read should see updated category
        var afterUpdateResult = await _feature.GetRulesAsync();
        Assert.That(afterUpdateResult.Items.First().Category, Is.EqualTo("Online"));
    }

    [Test]
    public async Task DeleteRuleAsync_InvalidatesCache()
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

        // And: Cache is populated
        var initialResult = await _feature.GetRulesAsync();
        Assert.That(initialResult.Items, Has.Count.EqualTo(1));

        // When: Deleting the rule
        await _feature.DeleteRuleAsync(rule.Key);

        // Then: Next read should not see the rule
        var afterDeleteResult = await _feature.GetRulesAsync();
        Assert.That(afterDeleteResult.Items, Is.Empty);
    }
}
