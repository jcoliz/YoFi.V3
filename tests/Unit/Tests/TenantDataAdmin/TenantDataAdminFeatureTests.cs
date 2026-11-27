using System.Data;
using DotNetNuxt.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Features;
using YoFi.V3.Entities.Exceptions;
using YoFi.V3.Entities.Models;
using YoFi.V3.Tests.Unit.TestHelpers;

namespace YoFi.V3.Tests.Unit.Application.TenantDataAdmin;

[TestFixture]
public class TenantDataAdminFeatureTests
{
    private Mock<IDataProvider> _data = null!;
    private TestTenantProvider _tenant = null!;
    private MemoryCache _cache = null!;
    private TenantDataAdminFeature _feature = null!;

    [SetUp]
    public void SetUp()
    {
        _data = new Mock<IDataProvider>();
        _tenant = new TestTenantProvider();
        _tenant.CurrentTenant.Id = 42;
        _tenant.CurrentTenant.Name = "Workspace";
        _cache = new MemoryCache(new MemoryCacheOptions());
        _feature = new TenantDataAdminFeature(_tenant, _data.Object, _cache);
    }

    [TearDown]
    public void TearDown() => _cache.Dispose();

    [Test]
    public async Task DeleteAllAsync_MismatchedConfirmationThrowsValidationWithoutDeletion()
    {
        await Assert.ThrowsAsync<ValidationException>(() => _feature.DeleteAllAsync(
            new TenantDataDeletionRequest("Different"), CancellationToken.None));

        _data.Verify(data => data.ExecuteInTransactionAsync(
            It.IsAny<Func<CancellationToken, Task<TenantDataDeletionCountsDto>>>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<IsolationLevel?>()), Times.Never);
    }

    [Test]
    public async Task DeleteAllAsync_ConfirmedRequestDeletesSupportedTypesAndInvalidatesCache()
    {
        _data.Setup(data => data.Get<ImportReviewTransaction>()).Returns(Array.Empty<ImportReviewTransaction>().AsQueryable());
        _data.Setup(data => data.Get<Split>()).Returns(Array.Empty<Split>().AsQueryable());
        _data.Setup(data => data.Get<Transaction>()).Returns(Array.Empty<Transaction>().AsQueryable());
        _data.Setup(data => data.Get<PayeeMatchingRule>()).Returns(Array.Empty<PayeeMatchingRule>().AsQueryable());
        _data.Setup(data => data.BulkDeleteAsync(It.IsAny<IQueryable<ImportReviewTransaction>>())).ReturnsAsync(4);
        _data.Setup(data => data.BulkDeleteAsync(It.IsAny<IQueryable<Split>>())).ReturnsAsync(7);
        _data.Setup(data => data.BulkDeleteAsync(It.IsAny<IQueryable<Transaction>>())).ReturnsAsync(3);
        _data.Setup(data => data.BulkDeleteAsync(It.IsAny<IQueryable<PayeeMatchingRule>>())).ReturnsAsync(2);
        _data.Setup(data => data.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task<TenantDataDeletionCountsDto>>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<IsolationLevel?>()))
            .Returns((Func<CancellationToken, Task<TenantDataDeletionCountsDto>> operation,
                CancellationToken token,
                IsolationLevel? _) => operation(token));
        _cache.Set(PayeeMatchingRuleFeature.CacheKey(42), new List<PayeeMatchingRule>());

        var result = await _feature.DeleteAllAsync(
            new TenantDataDeletionRequest("Workspace"), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Deleted.Transactions, Is.EqualTo(3));
            Assert.That(result.Deleted.Splits, Is.EqualTo(7));
            Assert.That(result.Deleted.PayeeMatchingRules, Is.EqualTo(2));
            Assert.That(result.Deleted.ImportReviewTransactions, Is.EqualTo(4));
            Assert.That(_cache.TryGetValue(PayeeMatchingRuleFeature.CacheKey(42), out _), Is.False);
        }
    }

    [Test]
    public async Task DeleteAllAsync_CancelledBeforeRequestThrows()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        _data.Setup(data => data.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task<TenantDataDeletionCountsDto>>>(),
                source.Token,
                It.IsAny<IsolationLevel?>()))
            .Returns((Func<CancellationToken, Task<TenantDataDeletionCountsDto>> operation,
                CancellationToken token,
                IsolationLevel? _) => operation(token));

        await Assert.ThrowsAsync<OperationCanceledException>(() => _feature.DeleteAllAsync(
            new TenantDataDeletionRequest("Workspace"), source.Token));
    }

    [Test]
    public async Task DeleteAllAsync_CancellationInsideTransactionPreventsLaterDeletion()
    {
        using var source = new CancellationTokenSource();
        _data.Setup(data => data.Get<ImportReviewTransaction>()).Returns(Array.Empty<ImportReviewTransaction>().AsQueryable());
        _data.Setup(data => data.BulkDeleteAsync(It.IsAny<IQueryable<ImportReviewTransaction>>()))
            .Returns(async () =>
            {
                await Task.Yield();
                source.Cancel();
                return 1;
            });
        _data.Setup(data => data.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task<TenantDataDeletionCountsDto>>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<IsolationLevel?>()))
            .Returns((Func<CancellationToken, Task<TenantDataDeletionCountsDto>> operation,
                CancellationToken token,
                IsolationLevel? _) => operation(source.Token));

        await Assert.ThrowsAsync<OperationCanceledException>(() => _feature.DeleteAllAsync(
            new TenantDataDeletionRequest("Workspace"), source.Token));

        _data.Verify(data => data.BulkDeleteAsync(It.IsAny<IQueryable<Split>>()), Times.Never);
    }
}
