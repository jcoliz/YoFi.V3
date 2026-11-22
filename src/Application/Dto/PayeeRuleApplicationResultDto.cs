namespace YoFi.V3.Application.Dto;

/// <summary>
/// The outcome of manually applying payee rules to a transaction.
/// </summary>
public enum PayeeRuleApplicationOutcome
{
    /// <summary>The winning rule was applied to the transaction.</summary>
    Applied,

    /// <summary>No payee rule matched the transaction.</summary>
    NoMatch,

    /// <summary>The persisted transaction now has multiple splits.</summary>
    TransactionHasMultipleSplits
}

/// <summary>
/// Represents the result of manually applying payee rules to one transaction.
/// </summary>
/// <param name="TransactionKey">Tenant-scoped transaction key.</param>
/// <param name="Outcome">The typed application outcome.</param>
/// <param name="Category">The resulting category, or null for a stale multi-split transaction.</param>
/// <param name="PreviousCategory">The category before the operation, or null for a stale multi-split transaction.</param>
public record PayeeRuleApplicationResultDto(
    Guid TransactionKey,
    PayeeRuleApplicationOutcome Outcome,
    string? Category,
    string? PreviousCategory);
