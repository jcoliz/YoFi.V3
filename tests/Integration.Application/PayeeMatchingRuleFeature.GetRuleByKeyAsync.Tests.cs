using YoFi.V3.Entities.Exceptions;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Tenancy.Models;

namespace YoFi.V3.Tests.Integration.Application;

/// <summary>
/// Application Integration tests for PayeeMatchingRuleFeature.GetRuleByKeyAsync.
/// </summary>
[TestFixture]
public class PayeeMatchingRuleFeatureGetRuleByKeyAsyncTests : PayeeMatchingRuleFeatureTestBase
{
    [Test]
    public async Task GetRuleByKeyAsync_ExistingRule_ReturnsRule()
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

        // When: Getting rule by key
        var result = await _feature.GetRuleByKeyAsync(rule.Key);

        // Then: Should return correct rule
        Assert.That(result.Key, Is.EqualTo(rule.Key));
        Assert.That(result.PayeePattern, Is.EqualTo("Amazon"));
        Assert.That(result.Category, Is.EqualTo("Shopping"));
    }

    [Test]
    public void GetRuleByKeyAsync_NonExistentKey_ThrowsNotFoundException()
    {
        // Given: Non-existent key
        var nonExistentKey = Guid.NewGuid();

        // When/Then: Getting rule should throw
        Assert.ThrowsAsync<PayeeMatchingRuleNotFoundException>(
            async () => await _feature.GetRuleByKeyAsync(nonExistentKey));
    }

    [Test]
    public async Task GetRuleByKeyAsync_DifferentTenant_ThrowsNotFoundException()
    {
        // Given: Rule for different tenant
        var otherTenant = new Tenant { Key = Guid.NewGuid(), Name = "Other Tenant" };
        _context.Tenants.Add(otherTenant);
        await _context.SaveChangesAsync(); // Save tenant first

        var rule = new PayeeMatchingRule
        {
            PayeePattern = "Amazon",
            Category = "Shopping",
            TenantId = otherTenant.Id
        };
        _context.PayeeMatchingRules.Add(rule);
        await _context.SaveChangesAsync(); // Then save rule

        // When/Then: Getting rule from current tenant should throw
        Assert.ThrowsAsync<PayeeMatchingRuleNotFoundException>(
            async () => await _feature.GetRuleByKeyAsync(rule.Key));
    }
}
