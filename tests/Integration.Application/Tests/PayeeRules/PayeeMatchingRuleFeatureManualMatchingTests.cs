using Microsoft.EntityFrameworkCore;
using YoFi.V3.Application.Dto;
using YoFi.V3.Entities.Exceptions;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Tenancy.Models;

namespace YoFi.V3.Tests.Integration.Application;

/// <summary>
/// Application integration tests for manually applying payee matching rules to transactions.
/// </summary>
public class PayeeMatchingRuleFeatureManualMatchingTests : PayeeMatchingRuleFeatureTestBase
{
    [TestCase("")]
    [TestCase("Dining")]
    public async Task ApplyRulesToTransactionAsync_MatchPersistsCategoryAndUsageStatistics(
        string previousCategory)
    {
        // Given: A real tenant rule and transaction with one persisted split
        var rule = new PayeeMatchingRule
        {
            TenantId = _testTenant.Id,
            PayeePattern = "Coffee",
            PayeeIsRegex = false,
            Category = "  cafes  ",
            MatchCount = 3,
            ModifiedAt = _timeProvider.GetUtcNow().AddDays(-1)
        };
        var transaction = Transaction("Coffee Shop", previousCategory);
        transaction.TenantId = _testTenant.Id;
        _context.PayeeMatchingRules.Add(rule);
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();
        var ruleId = rule.Id;
        var transactionKey = transaction.Key;

        _context.ChangeTracker.Clear();

        // When: Applying the complete tenant rule set to the persisted transaction
        var result = await _feature.ApplyRulesToTransactionAsync(transactionKey);

        // Then: The previous and resulting categories are reported and persisted
        _context.ChangeTracker.Clear();
        var persistedTransaction = await _context.Transactions
            .Include(t => t.Splits)
            .SingleAsync(t => t.Key == transactionKey);
        var persistedRule = await _context.PayeeMatchingRules.SingleAsync(r => r.Id == ruleId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.TransactionKey, Is.EqualTo(transactionKey));
            Assert.That(result.Outcome, Is.EqualTo(PayeeRuleApplicationOutcome.Applied));
            Assert.That(result.PreviousCategory, Is.EqualTo(previousCategory));
            Assert.That(result.Category, Is.EqualTo("Cafes"));
            Assert.That(persistedTransaction.Splits, Has.Count.EqualTo(1));
            Assert.That(persistedTransaction.Splits.Single().Category, Is.EqualTo("Cafes"));
            Assert.That(persistedRule.MatchCount, Is.EqualTo(4));
            Assert.That(persistedRule.LastUsedAt, Is.EqualTo(_timeProvider.GetUtcNow()));
        }
    }



    private static Transaction Transaction(string payee, string category)
        => new()
        {
            Date = new DateOnly(2026, 1, 1),
            Payee = payee,
            Amount = -10m,
            Splits =
            [
                new Split
                {
                    Amount = -10m,
                    Category = category,
                    Order = 0
                }
            ]
        };
}
