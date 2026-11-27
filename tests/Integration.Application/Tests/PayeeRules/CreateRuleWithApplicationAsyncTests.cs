using Microsoft.EntityFrameworkCore;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Helpers;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Tenancy.Models;

namespace YoFi.V3.Tests.Integration.Application;

/// <summary>
/// Tests creation and optional application of only the new rule against persisted transactions.
/// </summary>
public class PayeeMatchingRuleFeatureCreateRuleWithApplicationAsyncTests : PayeeMatchingRuleFeatureTestBase
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task CreateRuleAsync_ApplicationOption_ControlsTransactionUpdates(bool apply)
    {
        var transaction = Transaction("Amazon.com", "");
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        var result = await _feature.CreateRuleAsync(
            new PayeeMatchingRuleEditDto("Amazon", false, "Shopping"), apply);

        _context.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.TransactionsCategorized, Is.EqualTo(apply ? 1 : 0));
            Assert.That(await _context.Splits.Select(s => s.Category).SingleAsync(),
                Is.EqualTo(apply ? "Shopping" : ""));
            Assert.That(result.MatchCount, Is.EqualTo(apply ? 1 : 0));
            Assert.That(result.LastUsedAt, Is.EqualTo(apply ? _timeProvider.GetUtcNow() : (DateTimeOffset?)null));
        }
    }

    [Test]
    public async Task CreateRuleAsync_OmittedOption_LeavesMatchingTransactionsUnchanged()
    {
        _context.Transactions.Add(Transaction("Amazon.com", ""));
        await _context.SaveChangesAsync();

        var result = await _feature.CreateRuleAsync(new PayeeMatchingRuleEditDto("Amazon", false, "Shopping"));

        _context.ChangeTracker.Clear();
        Assert.That(result.TransactionsCategorized, Is.Zero);
        Assert.That(await _context.Splits.Select(s => s.Category).SingleAsync(), Is.Empty);
    }


    [Test]
    public async Task CreateRuleAsync_OnlyUpdatesEligibleCurrentTenantTransactions()
    {
        var otherTenant = new Tenant { Name = "Other workspace" };
        _context.Tenants.Add(otherTenant);
        await _context.SaveChangesAsync();
        var matching = Transaction("Amazon.com", "");
        var categorized = Transaction("Amazon.com", "Existing");
        var nonmatching = Transaction("Coffee", "");
        var otherWorkspace = Transaction("Amazon.com", "") with { TenantId = otherTenant.Id };
        var noSplits = Transaction("Amazon.com");
        var multiEmpty = Transaction("Amazon.com", "", "");
        var multiPartial = Transaction("Amazon.com", "", "Existing");
        var multiFull = Transaction("Amazon.com", "First", "Second");
        _context.Transactions.AddRange(
            matching, categorized, nonmatching, otherWorkspace, noSplits, multiEmpty, multiPartial, multiFull);
        var pending = new ImportReviewTransaction { Payee = "Amazon.com", TenantId = _testTenant.Id };
        _context.ImportReviewTransactions.Add(pending);
        await _context.SaveChangesAsync();

        var result = await _feature.CreateRuleAsync(new PayeeMatchingRuleEditDto("Amazon", false, "Shopping"), true);

        _context.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.TransactionsCategorized, Is.EqualTo(1));
            Assert.That(await Categories(matching.Key), Is.EqualTo(new[] { "Shopping" }));
            Assert.That(await Categories(categorized.Key), Is.EqualTo(new[] { "Existing" }));
            Assert.That(await Categories(nonmatching.Key), Is.EqualTo(new[] { "" }));
            Assert.That(await Categories(otherWorkspace.Key), Is.EqualTo(new[] { "" }));
            Assert.That(await Categories(noSplits.Key), Is.Empty);
            Assert.That(await Categories(multiEmpty.Key), Is.EqualTo(new[] { "", "" }));
            Assert.That(await Categories(multiPartial.Key), Is.EqualTo(new[] { "", "Existing" }));
            Assert.That(await Categories(multiFull.Key), Is.EqualTo(new[] { "First", "Second" }));
            Assert.That((await _context.ImportReviewTransactions.SingleAsync()).Category, Is.Null);
        }
    }

    public async Task CreateRuleAsync_NoTransactions_ReturnsZeroWithUnusedRule()
    {
        var result = await _feature.CreateRuleAsync(new PayeeMatchingRuleEditDto("Amazon", false, "Shopping"), true);

        Assert.That(result.TransactionsCategorized, Is.Zero);
        Assert.That(result.MatchCount, Is.Zero);
        Assert.That(result.LastUsedAt, Is.Null);
    }

    [Test]
    public async Task CreateRuleAsync_PersistenceFailure_RollsBackRuleAndCategories()
    {
        _context.Transactions.AddRange(Transaction("Amazon.com", ""), Transaction("Amazon Marketplace", ""));
        await _context.SaveChangesAsync();
        await _feature.GetRulesAsync();
        await _context.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION reject_split_categories() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'Injected category persistence failure';
            END;
            $$;
            CREATE TRIGGER reject_split_categories BEFORE UPDATE ON "Splits"
            FOR EACH ROW EXECUTE FUNCTION reject_split_categories();
            """);

        await Assert.ThrowsAsync<DbUpdateException>(() => _feature.CreateRuleAsync(
            new PayeeMatchingRuleEditDto("Amazon", false, "Shopping"), true));

        _context.ChangeTracker.Clear();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(await _context.PayeeMatchingRules.CountAsync(), Is.Zero);
            Assert.That(await _context.Splits.Select(s => s.Category).ToListAsync(), Is.All.Empty);
            Assert.That((await _feature.GetRulesAsync()).Items, Is.Empty);
        }
    }



    private Transaction Transaction(string payee, params string[] categories)
        => new()
        {
            TenantId = _testTenant.Id,
            Payee = payee,
            Date = new DateOnly(2026, 1, 1),
            Amount = -20m,
            Memo = "Transaction memo",
            Source = "Test bank",
            ExternalId = "External reference",
            Splits = categories.Select((category, order) => new Split
            {
                Category = category,
                Amount = -20m / categories.Length,
                Memo = "Split memo",
                Order = order
            }).ToList()
        };

    private Task<List<string>> Categories(Guid transactionKey)
        => _context.Splits.Where(s => s.Transaction!.Key == transactionKey)
            .OrderBy(s => s.Order).Select(s => s.Category).ToListAsync();
}
