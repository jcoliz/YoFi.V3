using Microsoft.EntityFrameworkCore;
using YoFi.V3.Application.Dto;
using YoFi.V3.Entities.Exceptions;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Tenancy.Models;

namespace YoFi.V3.Tests.Integration.Application;

/// <summary>
/// Application integration tests for manually applying payee matching rules to transactions.
/// </summary>
[TestFixture]
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

    [Test]
    public async Task ApplyRulesToTransactionAsync_MultipleSplitsReturnsExpectedOutcomeWithoutWriting()
    {
        // Given: A persisted multi-split transaction and a matching tenant rule
        var rule = new PayeeMatchingRule
        {
            TenantId = _testTenant.Id,
            PayeePattern = "Weekly",
            PayeeIsRegex = false,
            Category = "Groceries",
            MatchCount = 2
        };
        var transaction = new Transaction
        {
            TenantId = _testTenant.Id,
            Date = new DateOnly(2026, 1, 1),
            Payee = "Weekly Shop",
            Amount = -50m,
            Splits =
            [
                new Split { Amount = -30m, Category = "Groceries", Order = 0 },
                new Split { Amount = -20m, Category = "Household", Order = 1 }
            ]
        };
        _context.PayeeMatchingRules.Add(rule);
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();
        var ruleId = rule.Id;
        var transactionKey = transaction.Key;

        _context.ChangeTracker.Clear();

        // When: Applying rules to the stale multi-split state
        var result = await _feature.ApplyRulesToTransactionAsync(transactionKey);

        // Then: The typed stale outcome is returned without transaction or rule writes
        _context.ChangeTracker.Clear();
        var persistedTransaction = await _context.Transactions
            .Include(t => t.Splits)
            .SingleAsync(t => t.Key == transactionKey);
        var persistedRule = await _context.PayeeMatchingRules.SingleAsync(r => r.Id == ruleId);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Outcome,
                Is.EqualTo(PayeeRuleApplicationOutcome.TransactionHasMultipleSplits));
            Assert.That(result.Category, Is.Null);
            Assert.That(result.PreviousCategory, Is.Null);
            Assert.That(persistedTransaction.Splits, Has.Count.EqualTo(2));
            Assert.That(persistedTransaction.Splits.Select(s => s.Category),
                Is.EqualTo(new[] { "Groceries", "Household" }));
            Assert.That(persistedRule.MatchCount, Is.EqualTo(2));
            Assert.That(persistedRule.LastUsedAt, Is.Null);
        }
    }

    [Test]
    public async Task ApplyRulesToTransactionAsync_OtherTenantTransactionThrowsNotFound()
    {
        // Given: A transaction in another tenant and no transaction in the current tenant
        var otherTenant = new Tenant
        {
            Key = Guid.NewGuid(),
            Name = "Other Tenant"
        };
        _context.Tenants.Add(otherTenant);
        await _context.SaveChangesAsync();

        var otherTransaction = Transaction("Coffee Shop", "Dining");
        otherTransaction.TenantId = otherTenant.Id;
        _context.Transactions.Add(otherTransaction);
        await _context.SaveChangesAsync();
        var otherTransactionKey = otherTransaction.Key;

        _context.ChangeTracker.Clear();

        // When: Applying rules for the current tenant
        // Then: The other tenant's transaction is not found or modified
        await Assert.ThrowsAsync<TransactionNotFoundException>(
            () => _feature.ApplyRulesToTransactionAsync(otherTransactionKey));

        _context.ChangeTracker.Clear();
        var persisted = await _context.Transactions
            .Include(t => t.Splits)
            .SingleAsync(t => t.Key == otherTransactionKey);
        Assert.That(persisted.Splits.Single().Category, Is.EqualTo("Dining"));
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
