using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using YoFi.V3.Entities.Tenancy.Models;

namespace YoFi.V3.Entities.Models;

/// <summary>
/// Represents a transaction in import review state during the bank import workflow.
/// </summary>
/// <remarks>
/// <para>
/// Import review transactions are temporary staging records created when users upload OFX/QFX bank files.
/// Users review these transactions, check for duplicates, and selectively accept them into the main transaction table.
/// Once accepted or rejected, records are removed from this table.
/// </para>
/// <para>
/// This separate table (rather than a status flag on the main Transaction table) ensures:
/// <list type="bullet">
/// <item><description>Clean separation of temporary staging data from production transaction data</description></item>
/// <item><description>No impact on main transaction queries, reports, or analytics</description></item>
/// <item><description>Additional import-specific metadata (DuplicateStatus, DuplicateOfKey)</description></item>
/// <item><description>Simple bulk operations (delete all pending imports for a tenant)</description></item>
/// </list>
/// </para>
/// </remarks>
[Table("YoFi.V3.ImportReviewTransactions")]
public record ImportReviewTransaction : BaseTenantModel
{
    /// <summary>
    /// Date the transaction occurred.
    /// </summary>
    /// <remarks>
    /// Transaction date as reported by the bank.
    /// </remarks>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Recipient or payee of the transaction.
    /// </summary>
    /// <remarks>
    /// Payee or merchant name. Required field with maximum length of 200 characters.
    /// </remarks>
    [Required]
    [MaxLength(200)]
    public string Payee { get; set; } = string.Empty;

    /// <summary>
    /// Total amount of the transaction.
    /// </summary>
    /// <remarks>
    /// Transaction amount (positive for deposits, negative for withdrawals).
    /// </remarks>
    public decimal Amount { get; set; }

    /// <summary>
    /// Source of the transaction (e.g., "MegaBankCorp Checking 1234").
    /// </summary>
    /// <remarks>
    /// Import source derived from OFX account info. Nullable with maximum length of 200 characters.
    /// </remarks>
    [MaxLength(200)]
    public string? Source { get; set; }

    /// <summary>
    /// Bank's unique identifier for this transaction.
    /// </summary>
    /// <remarks>
    /// Bank transaction ID from OFX FITID field for duplicate detection. Nullable with maximum length of 100 characters.
    /// </remarks>
    [MaxLength(100)]
    public string? ExternalId { get; set; }

    /// <summary>
    /// Optional memo or notes for the transaction.
    /// </summary>
    /// <remarks>
    /// Notes field from bank statement. Nullable with maximum length of 1000 characters.
    /// </remarks>
    [MaxLength(1000)]
    public string? Memo { get; set; }

    /// <summary>
    /// Duplicate detection status determining default UI selection.
    /// </summary>
    /// <remarks>
    /// Indicates whether this transaction is new or a duplicate of an existing transaction.
    /// Defaults to <see cref="Models.DuplicateStatus.New"/>.
    /// </remarks>
    public DuplicateStatus DuplicateStatus { get; set; } = DuplicateStatus.New;

    /// <summary>
    /// Key of existing transaction if duplicate detected.
    /// </summary>
    /// <remarks>
    /// Populated when <see cref="DuplicateStatus"/> is <see cref="Models.DuplicateStatus.ExactDuplicate"/>
    /// or <see cref="Models.DuplicateStatus.PotentialDuplicate"/>. Nullable.
    /// </remarks>
    public Guid? DuplicateOfKey { get; set; }

    // Navigation properties

    /// <summary>
    /// Navigation property to the parent tenant.
    /// </summary>
    public virtual Tenant? Tenant { get; set; }
}
