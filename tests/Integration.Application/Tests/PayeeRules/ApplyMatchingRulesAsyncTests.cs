using YoFi.V3.Application.Services;
using YoFi.V3.Entities.Models;

namespace YoFi.V3.Tests.Integration.Application;

/// <summary>
/// Application Integration tests for PayeeMatchingRuleFeature.ApplyMatchingRulesAsync.
/// </summary>
public class PayeeMatchingRuleFeatureApplyMatchingRulesAsyncTests : PayeeMatchingRuleFeatureTestBase
{

    [Test]
    public async Task ApplyMatchingRulesAsync_WithMatchingRules_ReturnsCategories()
    {
        // Given: Rules for some payees
        var rule1 = new PayeeMatchingRule
        {
            PayeePattern = "Amazon",
            PayeeIsRegex = false,
            Category = "Shopping",
            TenantId = _testTenant.Id
        };
        var rule2 = new PayeeMatchingRule
        {
            PayeePattern = "Safeway",
            PayeeIsRegex = false,
            Category = "Groceries",
            TenantId = _testTenant.Id
        };
        _context.PayeeMatchingRules.AddRange(rule1, rule2);
        await _context.SaveChangesAsync();

        // And: Transactions with matching and non-matching payees
        var transactions = new[]
        {
            new TestMatchableTransaction("Amazon.com"),
            new TestMatchableTransaction("Target"),
            new TestMatchableTransaction("Safeway Store")
        };

        // When: Applying matching rules
        var result = await _feature.ApplyMatchingRulesAsync(transactions);

        // Then: Should return categories in parallel order
        Assert.That(result, Has.Count.EqualTo(3));
        Assert.That(result[0], Is.EqualTo("Shopping"));
        Assert.That(result[1], Is.Null);
        Assert.That(result[2], Is.EqualTo("Groceries"));
    }






}

/// <summary>
/// Test implementation of IMatchableTransaction for testing ApplyMatchingRulesAsync.
/// </summary>
file class TestMatchableTransaction(string payee) : IMatchableTransaction
{
    public string Payee { get; } = payee;
}
