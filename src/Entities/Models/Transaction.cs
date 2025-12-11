using System.ComponentModel.DataAnnotations.Schema;
using YoFi.V3.Entities.Tenancy.Models;

namespace YoFi.V3.Entities.Models;

/// <summary>
/// A financial transaction record tied to a specific tenant
/// </summary>
/// <remarks>
/// This is a simple example model for bringing up tenancy.
/// </remarks>
[Table("YoFi.V3.Transactions")]
public record Transaction : BaseTenantModel
{
    /// <summary>
    /// Date the transaction occurred
    /// </summary>
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>
    /// Recipient or payee of the transaction
    /// </summary>
    public string Payee { get; set; } = string.Empty;

    /// <summary>
    /// Amount of the transaction
    /// </summary>
    /// <remarks>
    /// Note that YoFi is single-currency for now, so no currency code is stored.
    /// </remarks>
    public decimal Amount { get; set; } = 0;

    // Navigation properties
    public virtual Tenant? Tenant { get; set; }
}
