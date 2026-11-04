using DotNetNuxt.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using YoFi.V3.Application.Dto;
using YoFi.V3.Entities.Exceptions;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Tenancy.Providers;

namespace YoFi.V3.Application.Features;

/// <summary>Coordinates owner-authorized administration of existing workspace data.</summary>
/// <param name="tenantProvider">Current tenant established by the tenant middleware.</param>
/// <param name="tenantRepository">Tenant persistence and transaction operations.</param>
/// <param name="dataProvider">Persistence operations for tenant data models.</param>
/// <param name="memoryCache">Cache containing payee matching rules.</param>
public sealed class TenantDataAdminFeature(
    ITenantProvider tenantProvider,
    ITenantRepository tenantRepository,
    IDataProvider dataProvider,
    IMemoryCache memoryCache)
{
    /// <summary>Deletes all supported data for the current tenant after exact name confirmation.</summary>
    /// <param name="request">Workspace name confirmation.</param>
    /// <param name="cancellationToken">Token checked throughout the deletion.</param>
    /// <returns>Actual per-type counts committed by the database operation.</returns>
    /// <exception cref="ValidationException">The confirmation does not match the current workspace name.</exception>
    public async Task<TenantDataDeletionResultDto> DeleteAllAsync(
        TenantDataDeletionRequest request,
        CancellationToken cancellationToken)
    {
        var tenant = tenantProvider.CurrentTenant;
        if (!string.Equals(tenant.Name, request.ConfirmationWorkspaceName, StringComparison.Ordinal))
        {
            throw new ValidationException(
                nameof(request.ConfirmationWorkspaceName),
                "The confirmation does not match the current workspace name. No data was deleted.");
        }

        var counts = await tenantRepository.ExecuteTenantTransactionAsync(async token =>
        {
            token.ThrowIfCancellationRequested();
            var importReviewTransactions = await dataProvider.BulkDeleteAsync(
                dataProvider.Get<ImportReviewTransaction>().Where(item => item.TenantId == tenant.Id));

            token.ThrowIfCancellationRequested();
            var splits = await dataProvider.BulkDeleteAsync(
                dataProvider.Get<Split>().Where(split => dataProvider.Get<Transaction>()
                    .Where(item => item.TenantId == tenant.Id)
                    .Select(item => item.Id)
                    .Contains(split.TransactionId)));

            token.ThrowIfCancellationRequested();
            var transactions = await dataProvider.BulkDeleteAsync(
                dataProvider.Get<Transaction>().Where(item => item.TenantId == tenant.Id));

            token.ThrowIfCancellationRequested();
            var payeeMatchingRules = await dataProvider.BulkDeleteAsync(
                dataProvider.Get<PayeeMatchingRule>().Where(item => item.TenantId == tenant.Id));

            return new TenantDataDeletionCountsDto(
                transactions,
                splits,
                payeeMatchingRules,
                importReviewTransactions);
        }, cancellationToken);

        memoryCache.Remove(PayeeMatchingRuleFeature.CacheKey(tenant.Id));
        return new TenantDataDeletionResultDto(counts);
    }
}
