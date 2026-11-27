using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Features;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Tenancy.Models;

namespace YoFi.V3.Tests.Integration.Application;

/// <summary>
/// Application Integration tests for PayeeMatchingRuleFeature.GetRulesAsync.
/// </summary>
public class PayeeMatchingRuleFeatureGetRulesAsyncTests : PayeeMatchingRuleFeatureTestBase
{


    [Test]
    public async Task GetRulesAsync_DefaultSort_SortsByPayeePatternAscending()
    {
        // Given: Multiple rules with different patterns
        var ruleB = new PayeeMatchingRule { PayeePattern = "Beta", Category = "Cat1", TenantId = _testTenant.Id };
        var ruleA = new PayeeMatchingRule { PayeePattern = "Alpha", Category = "Cat2", TenantId = _testTenant.Id };
        var ruleC = new PayeeMatchingRule { PayeePattern = "Charlie", Category = "Cat3", TenantId = _testTenant.Id };
        _context.PayeeMatchingRules.AddRange(ruleB, ruleA, ruleC);
        await _context.SaveChangesAsync();

        // When: Getting rules with default sort
        var result = await _feature.GetRulesAsync();

        // Then: Should be sorted by PayeePattern A-Z
        Assert.That(result.Items, Has.Count.EqualTo(3));
        var itemsList = result.Items.ToList();
        Assert.That(itemsList[0].PayeePattern, Is.EqualTo("Alpha"));
        Assert.That(itemsList[1].PayeePattern, Is.EqualTo("Beta"));
        Assert.That(itemsList[2].PayeePattern, Is.EqualTo("Charlie"));
    }






}
