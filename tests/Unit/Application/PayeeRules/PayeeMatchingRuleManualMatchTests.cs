using System.Data;
using System.Linq.Expressions;
using DotNetNuxt.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Features;
using YoFi.V3.Entities.Exceptions;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Tenancy.Models;
using YoFi.V3.Tests.Unit.TestHelpers;

namespace YoFi.V3.Tests.Unit.Application.PayeeRules;

/// <summary>
/// Unit tests for manually applying payee matching rules to one transaction.
/// </summary>
[TestFixture]
public class PayeeMatchingRuleManualMatchTests
{
    private Mock<IDataProvider> _data = null!;
    private TestTenantProvider _tenant = null!;
    private MemoryCache _cache = null!;
    private Mock<TimeProvider> _timeProvider = null!;
    private PayeeMatchingRuleFeature _feature = null!;
    private List<Transaction> _transactions = null!;
    private List<PayeeMatchingRule> _trackedRules = null!;

    [SetUp]
    public void SetUp()
    {
        _data = new Mock<IDataProvider>();
        _tenant = new TestTenantProvider();
        _tenant.CurrentTenant.Id = 42;
        _cache = new MemoryCache(new MemoryCacheOptions());
        _timeProvider = new Mock<TimeProvider>();
        _timeProvider.Setup(time => time.GetUtcNow())
            .Returns(new DateTimeOffset(2040, 7, 1, 12, 0, 0, TimeSpan.Zero));

        _transactions = [];
        _trackedRules = [];

        _data.Setup(data => data.GetIncluding<Transaction, ICollection<Split>>(
                It.IsAny<Expression<Func<Transaction, ICollection<Split>>>>()))
            .Returns(() => _transactions.AsQueryable());
        _data.Setup(data => data.Get<PayeeMatchingRule>())
            .Returns(() => _trackedRules.AsQueryable());
        _data.Setup(data => data.SingleOrDefaultAsync(
                It.IsAny<IQueryable<Transaction>>()))
            .Returns((IQueryable<Transaction> query) =>
                Task.FromResult(query.SingleOrDefault()));
        _data.Setup(data => data.ToListNoTrackingAsync(
                It.IsAny<IQueryable<PayeeMatchingRule>>()))
            .Returns((IQueryable<PayeeMatchingRule> query) =>
                Task.FromResult(query.ToList()));
        _data.Setup(data => data.ToListAsync(
                It.IsAny<IQueryable<PayeeMatchingRule>>()))
            .Returns((IQueryable<PayeeMatchingRule> query) =>
                Task.FromResult(query.ToList()));
        _data.Setup(data => data.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _data.Setup(data => data.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task<PayeeRuleApplicationResultDto>>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<IsolationLevel?>()))
            .Returns((Func<CancellationToken, Task<PayeeRuleApplicationResultDto>> operation,
                CancellationToken cancellationToken,
                IsolationLevel? _) => operation(cancellationToken));

        _feature = new PayeeMatchingRuleFeature(
            _tenant,
            _data.Object,
            _cache,
            _timeProvider.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _cache.Dispose();
    }

    [TestCase("")]
    [TestCase("Dining")]
    public async Task ApplyRulesToTransactionAsync_MatchPersistsCategoryAndUsageStatistics(
        string previousCategory)
    {
        // Given: A tenant transaction with one split and a matching payee rule
        var transaction = CreateTransaction("Coffee Shop", previousCategory);
        _transactions.Add(transaction);
        var rule = CreateRule("Coffee", "  food :  dining  ");
        _trackedRules.Add(rule);
        _cache.Set(PayeeMatchingRuleFeature.CacheKey(42), new List<PayeeMatchingRule> { rule });

        // When: The complete rule set is applied
        var result = await _feature.ApplyRulesToTransactionAsync(transaction.Key);

        // Then: The category is sanitized and persisted with usage statistics
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.TransactionKey, Is.EqualTo(transaction.Key));
            Assert.That(result.Outcome, Is.EqualTo(PayeeRuleApplicationOutcome.Applied));
            Assert.That(result.PreviousCategory, Is.EqualTo(previousCategory));
            Assert.That(result.Category, Is.EqualTo("Food:Dining"));
            Assert.That(transaction.Splits.Single().Category, Is.EqualTo("Food:Dining"));
            Assert.That(rule.MatchCount, Is.EqualTo(1));
            Assert.That(rule.LastUsedAt, Is.EqualTo(_timeProvider.Object.GetUtcNow()));
        }
        _data.Verify(data => data.Update(It.Is<IModel>(item => ReferenceEquals(item, transaction.Splits.Single()))),
            Times.Once);
        _data.Verify(data => data.UpdateRange(It.Is<IEnumerable<IModel>>(items =>
                items.Cast<PayeeMatchingRule>().Contains(rule))),
            Times.Once);
        _data.Verify(data => data.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Test]
    public async Task ApplyRulesToTransactionAsync_NoMatchPreservesCategoryAndDoesNotWrite()
    {
        // Given: A tenant transaction and a rule that does not match
        var transaction = CreateTransaction("Unknown Vendor", "Miscellaneous");
        _transactions.Add(transaction);
        var rule = CreateRule("Coffee", "Food");
        _cache.Set(PayeeMatchingRuleFeature.CacheKey(42), new List<PayeeMatchingRule> { rule });

        // When: Rules are applied
        var result = await _feature.ApplyRulesToTransactionAsync(transaction.Key);

        // Then: The existing category is preserved and no persistence occurs
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Outcome, Is.EqualTo(PayeeRuleApplicationOutcome.NoMatch));
            Assert.That(result.Category, Is.EqualTo("Miscellaneous"));
            Assert.That(result.PreviousCategory, Is.EqualTo("Miscellaneous"));
            Assert.That(transaction.Splits.Single().Category, Is.EqualTo("Miscellaneous"));
        }
        _data.Verify(data => data.Update(It.IsAny<IModel>()), Times.Never);
        _data.Verify(data => data.UpdateRange(It.IsAny<IEnumerable<IModel>>()), Times.Never);
        _data.Verify(data => data.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ApplyRulesToTransactionAsync_MultipleSplitsReturnsExpectedOutcomeWithoutWriting()
    {
        // Given: A transaction that became split while stale UI was present
        var transaction = CreateTransaction("Weekly Shop", "Groceries");
        transaction.Splits.Add(new Split
        {
            TransactionId = transaction.Id,
            Amount = -20,
            Category = "Household",
            Order = 1
        });
        _transactions.Add(transaction);
        var rule = CreateRule("Weekly", "Food");
        _trackedRules.Add(rule);
        _cache.Set(PayeeMatchingRuleFeature.CacheKey(42), new List<PayeeMatchingRule> { rule });

        // When: Rules are applied
        var result = await _feature.ApplyRulesToTransactionAsync(transaction.Key);

        // Then: It returns a stale-row outcome before matching or statistics
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Outcome, Is.EqualTo(PayeeRuleApplicationOutcome.TransactionHasMultipleSplits));
            Assert.That(result.Category, Is.Null);
            Assert.That(result.PreviousCategory, Is.Null);
            Assert.That(rule.MatchCount, Is.Zero);
            Assert.That(rule.LastUsedAt, Is.Null);
        }
        _data.Verify(data => data.Update(It.IsAny<IModel>()), Times.Never);
        _data.Verify(data => data.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public void ApplyRulesToTransactionAsync_TransactionWithoutSplitThrowsValidationException()
    {
        // Given: A persisted transaction with no category allocation
        var transaction = CreateTransaction("Empty", "");
        transaction.Splits.Clear();
        _transactions.Add(transaction);

        // When: Rules are applied
        // Then: The invariant is reported as a validation failure
        Assert.That(
            async () => await _feature.ApplyRulesToTransactionAsync(transaction.Key),
            Throws.InstanceOf<ValidationException>());
    }

    [Test]
    public void ApplyRulesToTransactionAsync_OtherTenantTransactionThrowsNotFound()
    {
        // Given: A transaction owned by a tenant other than the current tenant
        var transaction = CreateTransaction("Other", "Dining");
        transaction.TenantId = 99;
        _transactions.Add(transaction);

        // When: Rules are applied
        // Then: Tenant isolation produces not-found
        Assert.That(
            async () => await _feature.ApplyRulesToTransactionAsync(transaction.Key),
            Throws.InstanceOf<TransactionNotFoundException>());
    }

    [Test]
    public void ApplyRulesToTransactionAsync_CancellationRequestedThrowsOperationCanceledException()
    {
        // Given: A canceled request
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // When: Rules are applied
        // Then: Cancellation is observed before loading the transaction
        Assert.That(
            async () => await _feature.ApplyRulesToTransactionAsync(Guid.NewGuid(), cancellation.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }

    private Transaction CreateTransaction(string payee, string category)
    {
        var transaction = new Transaction
        {
            Id = 1,
            Key = Guid.NewGuid(),
            TenantId = 42,
            Payee = payee,
            Amount = -10
        };
        transaction.Splits.Add(new Split
        {
            TransactionId = transaction.Id,
            Amount = -10,
            Category = category,
            Order = 0
        });
        return transaction;
    }

    private static PayeeMatchingRule CreateRule(string pattern, string category)
    {
        return new PayeeMatchingRule
        {
            Id = 1,
            Key = Guid.NewGuid(),
            TenantId = 42,
            PayeePattern = pattern,
            PayeeIsRegex = false,
            Category = category,
            CreatedAt = DateTimeOffset.UtcNow,
            ModifiedAt = DateTimeOffset.UtcNow,
            MatchCount = 0,
            LastUsedAt = null
        };
    }
}
