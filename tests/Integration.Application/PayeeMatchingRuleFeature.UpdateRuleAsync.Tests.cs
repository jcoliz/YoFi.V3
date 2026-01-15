using YoFi.V3.Application.Dto;
using YoFi.V3.Entities.Exceptions;
using YoFi.V3.Entities.Models;

namespace YoFi.V3.Tests.Integration.Application;

/// <summary>
/// Application Integration tests for PayeeMatchingRuleFeature.UpdateRuleAsync.
/// </summary>
[TestFixture]
public class PayeeMatchingRuleFeatureUpdateRuleAsyncTests : PayeeMatchingRuleFeatureTestBase
{
    [Test]
    public async Task UpdateRuleAsync_ExistingRule_UpdatesFields()
    {
        // Given: Existing rule
        var rule = new PayeeMatchingRule
        {
            PayeePattern = "Amazon",
            PayeeIsRegex = false,
            Category = "Shopping",
            TenantId = _testTenant.Id,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-5),
            ModifiedAt = DateTimeOffset.UtcNow.AddDays(-5)
        };
        _context.PayeeMatchingRules.Add(rule);
        await _context.SaveChangesAsync();
        var originalModifiedAt = rule.ModifiedAt;

        // When: Updating the rule
        var updateDto = new PayeeMatchingRuleEditDto(
            PayeePattern: "^AMZN.*",
            PayeeIsRegex: true,
            Category: "Online"
        );
        var result = await _feature.UpdateRuleAsync(rule.Key, updateDto);

        // Then: Fields should be updated
        Assert.That(result.PayeePattern, Is.EqualTo("^AMZN.*"));
        Assert.That(result.PayeeIsRegex, Is.True);
        Assert.That(result.Category, Is.EqualTo("Online"));

        // And: ModifiedAt should be updated
        Assert.That(result.ModifiedAt, Is.GreaterThan(originalModifiedAt));

        // And: CreatedAt should remain unchanged
        Assert.That(result.CreatedAt, Is.EqualTo(rule.CreatedAt));
    }

    [Test]
    public async Task UpdateRuleAsync_CategoryWithWhitespace_SanitizesCategory()
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

        // When: Updating with category containing whitespace
        var updateDto = new PayeeMatchingRuleEditDto(
            PayeePattern: "Amazon",
            PayeeIsRegex: false,
            Category: "  Online : Shopping  "
        );
        var result = await _feature.UpdateRuleAsync(rule.Key, updateDto);

        // Then: Category should be sanitized
        Assert.That(result.Category, Is.EqualTo("Online:Shopping"));
    }

    [Test]
    public void UpdateRuleAsync_NonExistentKey_ThrowsNotFoundException()
    {
        // Given: Non-existent key
        var nonExistentKey = Guid.NewGuid();
        var updateDto = new PayeeMatchingRuleEditDto("Pattern", false, "Category");

        // When/Then: Updating should throw
        Assert.ThrowsAsync<PayeeMatchingRuleNotFoundException>(
            async () => await _feature.UpdateRuleAsync(nonExistentKey, updateDto));
    }
}
