using YoFi.V3.Entities.Models;

namespace YoFi.V3.Tests.Integration.Application;

/// <summary>
/// Application Integration tests for PayeeMatchingRuleFeature.FindBestMatchAsync.
/// </summary>
[TestFixture]
public class PayeeMatchingRuleFeatureFindBestMatchAsyncTests : PayeeMatchingRuleFeatureTestBase
{
    [Test]
    public async Task FindBestMatchAsync_MatchingRule_ReturnsCategory()
    {
        // Given: Rule
        var rule = new PayeeMatchingRule
        {
            PayeePattern = "Amazon",
            PayeeIsRegex = false,
            Category = "Shopping",
            TenantId = _testTenant.Id
        };
        _context.PayeeMatchingRules.Add(rule);
        await _context.SaveChangesAsync();

        // When: Finding best match for payee
        var result = await _feature.FindBestMatchAsync("Amazon.com");

        // Then: Should return matching category
        Assert.That(result, Is.EqualTo("Shopping"));
    }


}
