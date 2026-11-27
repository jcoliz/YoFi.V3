using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Features;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Tenancy.Models;

namespace YoFi.V3.Tests.Integration.Application;

/// <summary>
/// Application Integration tests for PayeeMatchingRuleFeature.GetRulesAsync.
/// </summary>
[TestFixture]
public class PayeeMatchingRuleFeatureGetRulesAsyncTests : PayeeMatchingRuleFeatureTestBase
{
    [Test]
    public async Task GetRulesAsync_NoRules_ReturnsEmptyList()
    {
        // Given: No rules in database

        // When: Getting rules
        var result = await _feature.GetRulesAsync();

        // Then: Should return empty list with pagination metadata
        Assert.That(result.Items, Is.Empty);
        Assert.That(result.Metadata.TotalCount, Is.Zero);
        Assert.That(result.Metadata.PageNumber, Is.EqualTo(1));
    }

    [Test]
    public async Task GetRulesAsync_MultipleRules_ReturnsOnlyCurrentTenantRules()
    {
        // Given: Rules for current tenant
        var rule1 = new PayeeMatchingRule
        {
            PayeePattern = "Amazon",
            Category = "Shopping",
            TenantId = _testTenant.Id
        };
        var rule2 = new PayeeMatchingRule
        {
            PayeePattern = "Safeway",
            Category = "Groceries",
            TenantId = _testTenant.Id
        };
        _context.PayeeMatchingRules.AddRange(rule1, rule2);
        await _context.SaveChangesAsync();

        // And: Rule for different tenant
        var otherTenant = new Tenant { Key = Guid.NewGuid(), Name = "Other Tenant" };
        _context.Tenants.Add(otherTenant);
        await _context.SaveChangesAsync(); // Save tenant first

        var otherRule = new PayeeMatchingRule
        {
            PayeePattern = "Target",
            Category = "Shopping",
            TenantId = otherTenant.Id
        };
        _context.PayeeMatchingRules.Add(otherRule);
        await _context.SaveChangesAsync(); // Then save rule

        // When: Getting rules
        var result = await _feature.GetRulesAsync();

        // Then: Should return only current tenant's rules
        Assert.That(result.Items, Has.Count.EqualTo(2));
        Assert.That(result.Items.Select(r => r.PayeePattern), Contains.Item("Amazon"));
        Assert.That(result.Items.Select(r => r.PayeePattern), Contains.Item("Safeway"));
        Assert.That(result.Items.Select(r => r.PayeePattern), Does.Not.Contain("Target"));
    }

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



    [Test]
    public async Task GetRulesAsync_WithPagination_ReturnsCorrectPage()
    {
        // Given: 100 rules (more than one page at 50 items per page)
        for (int i = 1; i <= 100; i++)
        {
            var rule = new PayeeMatchingRule
            {
                PayeePattern = $"Pattern{i:D3}",
                Category = "Cat",
                TenantId = _testTenant.Id
            };
            _context.PayeeMatchingRules.Add(rule);
        }
        await _context.SaveChangesAsync();

        // When: Getting page 2
        var result = await _feature.GetRulesAsync(pageNumber: 2);

        // Then: Should return correct page
        Assert.That(result.Items, Has.Count.EqualTo(50));
        Assert.That(result.Metadata.TotalCount, Is.EqualTo(100));
        Assert.That(result.Metadata.PageNumber, Is.EqualTo(2));
        Assert.That(result.Metadata.PageSize, Is.EqualTo(50));
        Assert.That(result.Metadata.TotalPages, Is.EqualTo(2));

        // And: Should contain items 51-100 (sorted by pattern)
        var itemsList = result.Items.ToList();
        Assert.That(itemsList[0].PayeePattern, Is.EqualTo("Pattern051"));
        Assert.That(itemsList[49].PayeePattern, Is.EqualTo("Pattern100"));
    }



}
