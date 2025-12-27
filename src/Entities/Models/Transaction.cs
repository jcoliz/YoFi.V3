using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using YoFi.V3.Entities.Tenancy.Models;

namespace YoFi.V3.Entities.Models;

/// <summary>
/// A financial transaction record tied to a specific tenant.
/// </summary>
/// <remarks>
/// Transactions represent financial events imported from bank/credit card sources
/// or entered manually. Each transaction has one or more splits for categorization.
/// </remarks>
[Table("YoFi.V3.Transactions")]
public record Transaction : BaseTenantModel
{
    /// <summary>
    /// Date the transaction occurred.
    /// </summary>
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>
    /// Recipient or payee of the transaction.
    /// </summary>
    /// <remarks>
    /// Required field. Typically populated from bank data or user entry.
    /// </remarks>
    [Required]
    public string Payee { get; set; } = string.Empty;

    /// <summary>
    /// Total amount of the transaction (authoritative imported value).
    /// </summary>
    /// <remarks>
    /// This is the amount imported from the bank/source and represents the authoritative
    /// transaction value. Can be negative for credits/refunds. Splits may total to a
    /// different amount (validation warning for user).
    ///
    /// YoFi is single-currency for now, so no currency code is stored.
    /// </remarks>
    public decimal Amount { get; set; } = 0;

    /// <summary>
    /// Source of the transaction (e.g., "MegaBankCorp Checking 1234", "Manual Entry").
    /// </summary>
    /// <remarks>
    /// Free-text field typically populated by importer with bank name, account type,
    /// and last 4 digits of account number. Can be any text. Nullable for manual entries
    /// or when source is unknown.
    /// </remarks>
    [MaxLength(200)]
    public string? Source { get; set; }

    /// <summary>
    /// Bank's unique identifier for this transaction.
    /// </summary>
    /// <remarks>
    /// Used for duplicate detection during import. Format varies by bank/institution.
    /// Nullable for manual entries. Importer is responsible for populating this field
    /// and preventing duplicate imports.
    /// </remarks>
    [MaxLength(100)]
    public string? ExternalId { get; set; }

    /// <summary>
    /// Optional memo for the entire transaction.
    /// </summary>
    /// <remarks>
    /// Transaction-level notes for user context. Most transactions won't have memos.
    /// Examples: "Reimbursable", "Split with roommate", "Gift for John's birthday".
    /// For split-specific notes, use Split.Memo instead.
    /// </remarks>
    [MaxLength(1000)]
    public string? Memo { get; set; }

    // Navigation properties

    /// <summary>
    /// Navigation property to the parent tenant.
    /// </summary>
    public virtual Tenant? Tenant { get; set; }

    /// <summary>
    /// Splits categorizing this transaction.
    /// </summary>
    /// <remarks>
    /// Every transaction must have at least one split. In the most common case
    /// (single split), the UI hides split complexity and edits the split directly.
    /// </remarks>
    public virtual ICollection<Split> Splits { get; set; } = new List<Split>();
}
