using System.Data;
using Microsoft.EntityFrameworkCore;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Tenancy.Providers;

namespace YoFi.V3.Data;

/// <summary>Performs dependency-ordered tenant data deletion in one database transaction.</summary>
/// <param name="dbContext">Application database context.</param>
public sealed class TenantDataDeletionRepository(ApplicationDbContext dbContext) : ITenantDataDeletionRepository
{
    /// <inheritdoc />
    public async Task<TenantDataDeletionCounts?> DeleteAllAsync(
        long tenantId,
        string confirmationWorkspaceName,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        var currentName = await dbContext.Tenants
            .AsNoTracking()
            .Where(tenant => tenant.Id == tenantId)
            .Select(tenant => tenant.Name)
            .SingleOrDefaultAsync(cancellationToken);

        if (!string.Equals(currentName, confirmationWorkspaceName, StringComparison.Ordinal))
        {
            return null;
        }

        var importReviewTransactions = await dbContext.ImportReviewTransactions
            .Where(item => item.TenantId == tenantId)
            .ExecuteDeleteAsync(cancellationToken);

        var splits = await dbContext.Splits
            .Where(split => dbContext.Transactions
                .Where(item => item.TenantId == tenantId)
                .Select(item => item.Id)
                .Contains(split.TransactionId))
            .ExecuteDeleteAsync(cancellationToken);

        var transactions = await dbContext.Transactions
            .Where(item => item.TenantId == tenantId)
            .ExecuteDeleteAsync(cancellationToken);

        var payeeMatchingRules = await dbContext.PayeeMatchingRules
            .Where(item => item.TenantId == tenantId)
            .ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new TenantDataDeletionCounts(
            transactions,
            splits,
            payeeMatchingRules,
            importReviewTransactions);
    }
}
