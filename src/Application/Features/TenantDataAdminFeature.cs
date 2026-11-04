using Microsoft.Extensions.Caching.Memory;
using YoFi.V3.Application.Dto;
using YoFi.V3.Entities.Exceptions;
using YoFi.V3.Entities.Tenancy.Providers;

namespace YoFi.V3.Application.Features;

/// <summary>Coordinates owner-authorized administration of existing workspace data.</summary>
/// <param name="tenantProvider">Current tenant established by the tenant middleware.</param>
/// <param name="deletionRepository">Atomic tenant-scoped persistence operations.</param>
/// <param name="memoryCache">Cache containing payee matching rules.</param>
public sealed class TenantDataAdminFeature(
    ITenantProvider tenantProvider,
    ITenantDataDeletionRepository deletionRepository,
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
        var counts = await deletionRepository.DeleteAllAsync(
            tenantProvider.CurrentTenant.Id,
            request.ConfirmationWorkspaceName,
            cancellationToken);

        if (counts is null)
        {
            throw new ValidationException(
                nameof(request.ConfirmationWorkspaceName),
                "The confirmation does not match the current workspace name. No data was deleted.");
        }

        memoryCache.Remove(PayeeMatchingRuleFeature.CacheKey(tenantProvider.CurrentTenant.Id));
        return new TenantDataDeletionResultDto(new TenantDataDeletionCountsDto(
            counts.Transactions,
            counts.Splits,
            counts.PayeeMatchingRules,
            counts.ImportReviewTransactions));
    }
}
