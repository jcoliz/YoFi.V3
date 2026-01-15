using YoFi.V3.Entities.Models;

namespace YoFi.V3.Tests.Integration.Application;

/// <summary>
/// Application Integration tests for PayeeMatchingRuleFeature.MatchPayeesAsync.
/// </summary>
[TestFixture]
public class PayeeMatchingRuleFeatureMatchPayeesAsyncTests : PayeeMatchingRuleFeatureTestBase
{
    [Test]
    public async Task MatchPayeesAsync_NoRules_ReturnsEmptyDictionary()
    {
        // Given: No rules in database
        var payees = new[] { "Amazon", "Safeway", "Target" };

        // When: Matching payees
        var result = await _feature.MatchPayeesAsync(payees);

        // Then: Should return empty dictionary
        Assert.That(result, Is.Empty);
    }

    [Test]
    public async Task MatchPayeesAsync_SubstringMatch_ReturnsCategory()
    {
        // Given: Substring rule
        var rule = new PayeeMatchingRule
        {
            PayeePattern = "Amazon",
            PayeeIsRegex = false,
            Category = "Shopping",
            TenantId = _testTenant.Id
        };
        _context.PayeeMatchingRules.Add(rule);
        await _context.SaveChangesAsync();

        // When: Matching payee that contains pattern
        var payees = new[] { "Amazon.com", "Safeway" };
        var result = await _feature.MatchPayeesAsync(payees);

        // Then: Should match Amazon.com
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result["Amazon.com"], Is.EqualTo("Shopping"));
    }

    [Test]
    public async Task MatchPayeesAsync_RegexMatch_ReturnsCategory()
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

        // When: Matching payee that matches regex
        var payees = new[] { "AMZN Marketplace", "Target" };
        var result = await _feature.MatchPayeesAsync(payees);

        // Then: Should match AMZN Marketplace
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result["AMZN Marketplace"], Is.EqualTo("Shopping"));
    }

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
            LastUsedAt = DateTimeOffset.UtcNow.AddDays(-7)
        };
        _context.PayeeMatchingRules.Add(rule);
        await _context.SaveChangesAsync();
        var ruleId = rule.Id;

        // When: Matching payees (causes rule to match)
        var beforeMatch = DateTimeOffset.UtcNow;
        var payees = new[] { "Amazon.com" };
        await _feature.MatchPayeesAsync(payees);
        var afterMatch = DateTimeOffset.UtcNow;

        // Then: MatchCount should be incremented
        _context.ChangeTracker.Clear(); // Force reload from database
        var updatedRule = _context.PayeeMatchingRules.Single(r => r.Id == ruleId);
        Assert.That(updatedRule.MatchCount, Is.EqualTo(6));

        // And: LastUsedAt should be updated
        Assert.That(updatedRule.LastUsedAt, Is.Not.Null);
        Assert.That(updatedRule.LastUsedAt!.Value, Is.GreaterThanOrEqualTo(beforeMatch));
        Assert.That(updatedRule.LastUsedAt!.Value, Is.LessThanOrEqualTo(afterMatch));
    }

    [Test]
    public async Task MatchPayeesAsync_ConflictResolution_RegexBeatsSubstring()
    {
        // Given: Both regex and substring rules that match same payee
        var regexRule = new PayeeMatchingRule
        {
            PayeePattern = "^AMZN.*",
            PayeeIsRegex = true,
            Category = "Online",
            TenantId = _testTenant.Id,
            ModifiedAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
        var substringRule = new PayeeMatchingRule
        {
            PayeePattern = "AMZN",
            PayeeIsRegex = false,
            Category = "Shopping",
            TenantId = _testTenant.Id,
            ModifiedAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
        _context.PayeeMatchingRules.AddRange(regexRule, substringRule);
        await _context.SaveChangesAsync();

        // When: Matching payee that matches both
        var payees = new[] { "AMZN Marketplace" };
        var result = await _feature.MatchPayeesAsync(payees);

        // Then: Should prefer regex rule
        Assert.That(result["AMZN Marketplace"], Is.EqualTo("Online"));
    }

    [Test]
    public async Task MatchPayeesAsync_ConflictResolution_LongerSubstringBeatsshorter()
    {
        // Given: Two substring rules, one longer than other
        var shortRule = new PayeeMatchingRule
        {
            PayeePattern = "Amazon",
            PayeeIsRegex = false,
            Category = "Shopping",
            TenantId = _testTenant.Id,
            ModifiedAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
        var longRule = new PayeeMatchingRule
        {
            PayeePattern = "Amazon Prime",
            PayeeIsRegex = false,
            Category = "Subscription",
            TenantId = _testTenant.Id,
            ModifiedAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
        _context.PayeeMatchingRules.AddRange(shortRule, longRule);
        await _context.SaveChangesAsync();

        // When: Matching payee that matches both
        var payees = new[] { "Amazon Prime Video" };
        var result = await _feature.MatchPayeesAsync(payees);

        // Then: Should prefer longer substring
        Assert.That(result["Amazon Prime Video"], Is.EqualTo("Subscription"));
    }

    [Test]
    public async Task MatchPayeesAsync_ConflictResolution_NewerBeatsOlder()
    {
        // Given: Two rules with same precedence, different ModifiedAt
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

        // When: Matching payee that matches both
        var payees = new[] { "Amazon.com" };
        var result = await _feature.MatchPayeesAsync(payees);

        // Then: Should prefer newer rule
        Assert.That(result["Amazon.com"], Is.EqualTo("E-Commerce"));
    }

    [Test]
    public async Task MatchPayeesAsync_DuplicatePayees_OnlyProcessedOnce()
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

        // When: Matching with duplicate payees
        var payees = new[] { "Amazon.com", "Amazon.com", "Amazon.com" };
        var result = await _feature.MatchPayeesAsync(payees);

        // Then: Should return single entry
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result["Amazon.com"], Is.EqualTo("Shopping"));

        // And: MatchCount should be incremented only once
        _context.ChangeTracker.Clear();
        var updatedRule = _context.PayeeMatchingRules.Single(r => r.Id == ruleId);
        Assert.That(updatedRule.MatchCount, Is.EqualTo(1));
    }

    [Test]
    public async Task MatchPayeesAsync_EmptyPayeeList_ReturnsEmptyDictionary()
    {
        // Given: Some rules exist in database
        var rule = new PayeeMatchingRule
        {
            PayeePattern = "Amazon",
            PayeeIsRegex = false,
            Category = "Shopping",
            TenantId = _testTenant.Id
        };
        _context.PayeeMatchingRules.Add(rule);
        await _context.SaveChangesAsync();

        // When: Matching with empty payee list (covers line 182 early return)
        var result = await _feature.MatchPayeesAsync(new List<string>());

        // Then: Should return empty dictionary without querying rules
        Assert.That(result, Is.Empty);
    }
}
