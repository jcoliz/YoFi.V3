using YoFi.V3.Application.Dto;
using YoFi.V3.Entities.Models;

namespace YoFi.V3.Tests.Integration.Application;

/// <summary>
/// Application Integration tests for PayeeMatchingRuleFeature.CreateRuleAsync.
/// </summary>
[TestFixture]
public class PayeeMatchingRuleFeatureCreateRuleAsyncTests : PayeeMatchingRuleFeatureTestBase
{
    [Test]
    public async Task CreateRuleAsync_ValidSubstringRule_StoresInDatabase()
    {
        // Given: Valid substring rule data
        var ruleDto = new PayeeMatchingRuleEditDto(
            PayeePattern: "Amazon",
            PayeeIsRegex: false,
            Category: "Shopping"
        );

        // When: Creating the rule
        var result = await _feature.CreateRuleAsync(ruleDto);

        // Then: Rule should be created with correct data
        Assert.That(result.Key, Is.Not.EqualTo(Guid.Empty));
        Assert.That(result.PayeePattern, Is.EqualTo("Amazon"));
        Assert.That(result.PayeeIsRegex, Is.False);
        Assert.That(result.Category, Is.EqualTo("Shopping"));
        Assert.That(result.MatchCount, Is.EqualTo(0));
        Assert.That(result.LastUsedAt, Is.Null);

        // And: Rule should be in database
        var dbRule = _context.PayeeMatchingRules.Single(r => r.Key == result.Key);
        Assert.That(dbRule.TenantId, Is.EqualTo(_testTenant.Id));
        Assert.That(dbRule.PayeePattern, Is.EqualTo("Amazon"));
    }

    [Test]
    public async Task CreateRuleAsync_ValidRegexRule_StoresInDatabase()
    {
        // Given: Valid regex rule data
        var ruleDto = new PayeeMatchingRuleEditDto(
            PayeePattern: "^AMZN.*",
            PayeeIsRegex: true,
            Category: "Shopping"
        );

        // When: Creating the rule
        var result = await _feature.CreateRuleAsync(ruleDto);

        // Then: Rule should be created with regex flag set
        Assert.That(result.Key, Is.Not.EqualTo(Guid.Empty));
        Assert.That(result.PayeePattern, Is.EqualTo("^AMZN.*"));
        Assert.That(result.PayeeIsRegex, Is.True);
        Assert.That(result.Category, Is.EqualTo("Shopping"));
    }

    [Test]
    public async Task CreateRuleAsync_CategoryWithWhitespace_SanitizesCategory()
    {
        // Given: Rule with category containing extra whitespace
        var ruleDto = new PayeeMatchingRuleEditDto(
            PayeePattern: "Amazon",
            PayeeIsRegex: false,
            Category: "  Shopping : Online  "
        );

        // When: Creating the rule
        var result = await _feature.CreateRuleAsync(ruleDto);

        // Then: Category should be sanitized (whitespace collapsed, colons normalized)
        Assert.That(result.Category, Is.EqualTo("Shopping:Online"));

        // And: Sanitized category stored in database
        var dbRule = _context.PayeeMatchingRules.Single(r => r.Key == result.Key);
        Assert.That(dbRule.Category, Is.EqualTo("Shopping:Online"));
    }

    [Test]
    public async Task CreateRuleAsync_SetsTimestamps()
    {
        // Given: Valid rule data
        var beforeCreate = DateTimeOffset.UtcNow;
        var ruleDto = new PayeeMatchingRuleEditDto("Amazon", false, "Shopping");

        // When: Creating the rule
        var result = await _feature.CreateRuleAsync(ruleDto);
        var afterCreate = DateTimeOffset.UtcNow;

        // Then: CreatedAt and ModifiedAt should be set to same value
        Assert.That(result.CreatedAt, Is.GreaterThanOrEqualTo(beforeCreate));
        Assert.That(result.CreatedAt, Is.LessThanOrEqualTo(afterCreate));
        Assert.That(result.ModifiedAt, Is.EqualTo(result.CreatedAt));
    }
}
