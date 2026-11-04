namespace YoFi.V3.Application.Dto;

/// <summary>Workspace name required to authorize deletion of all workspace data.</summary>
/// <param name="ConfirmationWorkspaceName">The exact current workspace name.</param>
public record TenantDataDeletionRequest(string ConfirmationWorkspaceName);

/// <summary>Committed deletion counts for each tenant-scoped data type.</summary>
/// <param name="Transactions">Deleted transactions.</param>
/// <param name="Splits">Deleted transaction splits.</param>
/// <param name="PayeeMatchingRules">Deleted payee matching rules.</param>
/// <param name="ImportReviewTransactions">Deleted pending bank-import reviews.</param>
public record TenantDataDeletionCountsDto(
    int Transactions,
    int Splits,
    int PayeeMatchingRules,
    int ImportReviewTransactions);

/// <summary>Result returned after all workspace data deletion commits successfully.</summary>
/// <param name="Deleted">Committed per-type deletion counts.</param>
public record TenantDataDeletionResultDto(TenantDataDeletionCountsDto Deleted);
