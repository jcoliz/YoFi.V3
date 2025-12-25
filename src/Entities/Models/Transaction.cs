using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using YoFi.V3.Entities.Tenancy.Models;

namespace YoFi.V3.Entities.Models;

/// <summary>
/// A financial transaction record tied to a specific tenant.
/// </summary>
/// <remarks>
/// Transactions represent financial events imported from bank/credit card sources
/// or entered manually. Each transaction can be annotated with additional user context.
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
    /// Amount of the transaction.
    /// </summary>
    /// <remarks>
    /// Can be negative for credits/refunds. YoFi is single-currency for now,
    /// so no currency code is stored.
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
    /// Optional memo for additional transaction context.
    /// </summary>
    /// <remarks>
    /// Plain text field for user notes. Most transactions won't have memos.
    /// Examples: "Reimbursable", "Split with roommate", "Gift for John's birthday".
    /// </remarks>
    [MaxLength(1000)]
    public string? Memo { get; set; }

    // Navigation properties
    public virtual Tenant? Tenant { get; set; }
}
