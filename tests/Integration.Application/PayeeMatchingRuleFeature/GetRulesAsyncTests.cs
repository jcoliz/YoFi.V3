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
        Assert.That(result.Metadata.TotalCount, Is.EqualTo(0));
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
    public async Task GetRulesAsync_SortByCategory_SortsByCategoryAscending()
    {
        // Given: Multiple rules with different categories
        var rule1 = new PayeeMatchingRule { PayeePattern = "P1", Category = "Zebra", TenantId = _testTenant.Id };
        var rule2 = new PayeeMatchingRule { PayeePattern = "P2", Category = "Apple", TenantId = _testTenant.Id };
        var rule3 = new PayeeMatchingRule { PayeePattern = "P3", Category = "Banana", TenantId = _testTenant.Id };
        _context.PayeeMatchingRules.AddRange(rule1, rule2, rule3);
        await _context.SaveChangesAsync();

        // When: Getting rules sorted by category
        var result = await _feature.GetRulesAsync(sortBy: PayeeRuleSortBy.Category);

        // Then: Should be sorted by Category A-Z
        var itemsList = result.Items.ToList();
        Assert.That(itemsList[0].Category, Is.EqualTo("Apple"));
        Assert.That(itemsList[1].Category, Is.EqualTo("Banana"));
        Assert.That(itemsList[2].Category, Is.EqualTo("Zebra"));
    }

    [Test]
    public async Task GetRulesAsync_SortByLastUsedAt_SortsByMostRecentFirst()
    {
        // Given: Multiple rules with different LastUsedAt values
        var old = DateTimeOffset.UtcNow.AddDays(-10);
        var recent = DateTimeOffset.UtcNow.AddDays(-1);
        var rule1 = new PayeeMatchingRule { PayeePattern = "P1", Category = "C1", TenantId = _testTenant.Id, LastUsedAt = old };
        var rule2 = new PayeeMatchingRule { PayeePattern = "P2", Category = "C2", TenantId = _testTenant.Id, LastUsedAt = recent };
        var rule3 = new PayeeMatchingRule { PayeePattern = "P3", Category = "C3", TenantId = _testTenant.Id, LastUsedAt = null };
        _context.PayeeMatchingRules.AddRange(rule1, rule2, rule3);
        await _context.SaveChangesAsync();

        // When: Getting rules sorted by LastUsedAt
        var result = await _feature.GetRulesAsync(sortBy: PayeeRuleSortBy.LastUsedAt);

        // Then: Should be sorted most recent first, null last
        var itemsList = result.Items.ToList();
        Assert.That(itemsList[0].PayeePattern, Is.EqualTo("P2")); // Most recent
        Assert.That(itemsList[1].PayeePattern, Is.EqualTo("P1")); // Older
        Assert.That(itemsList[2].PayeePattern, Is.EqualTo("P3")); // Never used (null)
    }

    [Test]
    public async Task GetRulesAsync_WithPagination_ReturnsCorrectPage()
    {
        // Given: 10 rules
        for (int i = 1; i <= 10; i++)
        {
            var rule = new PayeeMatchingRule
            {
                PayeePattern = $"Pattern{i:D2}",
                Category = "Cat",
                TenantId = _testTenant.Id
            };
            _context.PayeeMatchingRules.Add(rule);
        }
        await _context.SaveChangesAsync();

        // When: Getting page 2 with 3 items per page
        var result = await _feature.GetRulesAsync(pageNumber: 2, pageSize: 3);

        // Then: Should return correct page
        Assert.That(result.Items, Has.Count.EqualTo(3));
        Assert.That(result.Metadata.TotalCount, Is.EqualTo(10));
        Assert.That(result.Metadata.PageNumber, Is.EqualTo(2));
        Assert.That(result.Metadata.PageSize, Is.EqualTo(3));
        Assert.That(result.Metadata.TotalPages, Is.EqualTo(4));

        // And: Should contain items 4, 5, 6 (sorted by pattern)
        var itemsList = result.Items.ToList();
        Assert.That(itemsList[0].PayeePattern, Is.EqualTo("Pattern04"));
        Assert.That(itemsList[1].PayeePattern, Is.EqualTo("Pattern05"));
        Assert.That(itemsList[2].PayeePattern, Is.EqualTo("Pattern06"));
    }

    [Test]
    public async Task GetRulesAsync_WithSearchText_FiltersPayeePattern()
    {
        // Given: Multiple rules
        var rule1 = new PayeeMatchingRule { PayeePattern = "Amazon", Category = "Shopping", TenantId = _testTenant.Id };
        var rule2 = new PayeeMatchingRule { PayeePattern = "Safeway", Category = "Groceries", TenantId = _testTenant.Id };
        var rule3 = new PayeeMatchingRule { PayeePattern = "Target", Category = "Shopping", TenantId = _testTenant.Id };
        _context.PayeeMatchingRules.AddRange(rule1, rule2, rule3);
        await _context.SaveChangesAsync();

        // When: Searching for "Safe"
        var result = await _feature.GetRulesAsync(searchText: "Safe");

        // Then: Should return only matching rule
        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items.First().PayeePattern, Is.EqualTo("Safeway"));
    }

    [Test]
    public async Task GetRulesAsync_WithSearchText_FiltersCategory()
    {
        // Given: Multiple rules
        var rule1 = new PayeeMatchingRule { PayeePattern = "Amazon", Category = "Shopping", TenantId = _testTenant.Id };
        var rule2 = new PayeeMatchingRule { PayeePattern = "Safeway", Category = "Groceries", TenantId = _testTenant.Id };
        var rule3 = new PayeeMatchingRule { PayeePattern = "Target", Category = "Shopping", TenantId = _testTenant.Id };
        _context.PayeeMatchingRules.AddRange(rule1, rule2, rule3);
        await _context.SaveChangesAsync();

        // When: Searching for "Shop"
        var result = await _feature.GetRulesAsync(searchText: "Shop");

        // Then: Should return rules with matching category
        Assert.That(result.Items, Has.Count.EqualTo(2));
        Assert.That(result.Items.Select(r => r.PayeePattern), Contains.Item("Amazon"));
        Assert.That(result.Items.Select(r => r.PayeePattern), Contains.Item("Target"));
    }

    [Test]
    public async Task GetRulesAsync_WithSearchText_IsCaseInsensitive()
    {
        // Given: Rule with mixed case
        var rule = new PayeeMatchingRule { PayeePattern = "Amazon", Category = "Shopping", TenantId = _testTenant.Id };
        _context.PayeeMatchingRules.Add(rule);
        await _context.SaveChangesAsync();

        // When: Searching with different case
        var result = await _feature.GetRulesAsync(searchText: "AMAZON");

        // Then: Should find the rule
        Assert.That(result.Items, Has.Count.EqualTo(1));
        Assert.That(result.Items.First().PayeePattern, Is.EqualTo("Amazon"));
    }
}
