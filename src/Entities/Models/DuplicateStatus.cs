namespace YoFi.V3.Entities.Models;

/// <summary>
/// Status of a transaction in import review relative to existing transactions.
/// </summary>
/// <remarks>
/// Used during bank import to indicate whether an imported transaction is new or a duplicate
/// of an existing transaction. This status determines the default selection state in the review UI:
/// <list type="bullet">
/// <item><description><see cref="New"/> transactions are selected by default (user should import)</description></item>
/// <item><description><see cref="ExactDuplicate"/> and <see cref="PotentialDuplicate"/> are deselected by default (user should review)</description></item>
/// </list>
/// </remarks>
public enum DuplicateStatus
{
    /// <summary>
    /// New transaction - no duplicates found in existing transactions or pending imports.
    /// </summary>
    /// <remarks>
    /// Selected by default for import.
    /// </remarks>
    New = 0,

    /// <summary>
    /// Exact duplicate - same ExternalId (FITID) and matching data (Date, Amount, Payee).
    /// </summary>
    /// <remarks>
    /// Deselected by default. User should NOT import to avoid duplicate records.
    /// </remarks>
    ExactDuplicate = 1,

    /// <summary>
    /// Potential duplicate - same ExternalId (FITID) but different data (Date, Amount, or Payee).
    /// </summary>
    /// <remarks>
    /// Highlighted and deselected by default. User should review carefully before importing.
    /// May indicate a bank correction, amended transaction, or data quality issue.
    /// </remarks>
    PotentialDuplicate = 2
}
