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
        Assert.That(result.MatchCount, Is.Zero);
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


}
