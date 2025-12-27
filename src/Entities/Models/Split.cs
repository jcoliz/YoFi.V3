using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace YoFi.V3.Entities.Models;

/// <summary>
/// Represents a portion of a transaction allocated to a specific category.
/// </summary>
/// <remarks>
/// Splits allow transactions to be categorized across multiple categories.
/// Every transaction must have at least one split. The sum of split amounts
/// should match the transaction amount, but this is a validation warning
/// rather than a hard constraint (the imported transaction amount is authoritative).
///
/// Splits inherit from BaseModel (not BaseTenantModel) because tenant isolation
/// comes from the parent Transaction entity.
/// </remarks>
[Table("YoFi.V3.Splits")]
public record Split : BaseModel
{
    /// <summary>
    /// Foreign key to the parent transaction.
    /// </summary>
    public long TransactionId { get; set; }

    /// <summary>
    /// Amount allocated to this category.
    /// </summary>
    /// <remarks>
    /// Can be negative for credits/refunds. The sum of all splits for a transaction
    /// should match Transaction.Amount, but this is not enforced at the database level.
    /// </remarks>
    public decimal Amount { get; set; }

    /// <summary>
    /// Category for this split.
    /// </summary>
    /// <remarks>
    /// Empty string indicates uncategorized. Categories are free-form text
    /// in YoFi (no separate Category table) to support flexible user workflows.
    /// This field is required and cannot be null.
    /// </remarks>
    [Required]
    [MaxLength(200)]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Optional memo specific to this split.
    /// </summary>
    /// <remarks>
    /// Split-level memo for notes about this specific categorization.
    /// Most splits won't have a memo (transaction memo is more common).
    /// </remarks>
    [MaxLength(500)]
    public string? Memo { get; set; }

    /// <summary>
    /// Display order for splits within a transaction.
    /// </summary>
    /// <remarks>
    /// Zero-based index for stable ordering in UI. Users can reorder splits,
    /// and this preserves their preference across queries.
    /// </remarks>
    public int Order { get; set; }

    // Navigation properties

    /// <summary>
    /// Navigation property to the parent transaction.
    /// </summary>
    public virtual Transaction? Transaction { get; set; }
}
