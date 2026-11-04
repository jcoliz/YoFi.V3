namespace YoFi.V3.Entities.Tenancy.Providers;

/// <summary>Committed counts from deleting a tenant's workspace data.</summary>
/// <param name="Transactions">Deleted transactions.</param>
/// <param name="Splits">Deleted transaction splits.</param>
/// <param name="PayeeMatchingRules">Deleted payee matching rules.</param>
/// <param name="ImportReviewTransactions">Deleted pending bank-import reviews.</param>
public sealed record TenantDataDeletionCounts(
    int Transactions,
    int Splits,
    int PayeeMatchingRules,
    int ImportReviewTransactions);

/// <summary>Performs an atomic deletion of tenant-scoped workspace data.</summary>
public interface ITenantDataDeletionRepository
{
    /// <summary>
    /// Deletes all tenant data if the supplied workspace name exactly matches its current name.
    /// </summary>
    /// <param name="tenantId">Internal ID of the tenant established by tenant middleware.</param>
    /// <param name="confirmationWorkspaceName">Name entered by the owner as confirmation.</param>
    /// <param name="cancellationToken">Token checked throughout the operation.</param>
    /// <returns>Committed deletion counts, or null if the current name does not match.</returns>
    Task<TenantDataDeletionCounts?> DeleteAllAsync(
        long tenantId,
        string confirmationWorkspaceName,
        CancellationToken cancellationToken);
}
