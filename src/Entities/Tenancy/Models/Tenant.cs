using System.ComponentModel.DataAnnotations.Schema;
using YoFi.V3.Entities.Models;

namespace YoFi.V3.Entities.Tenancy.Models;

/// <summary>
/// A tenant (i.e., customer, organization, etc.) in a multi-tenant application
/// </summary>
/// <remarks>
/// Entire tenancy feature is designed to be application-independent, so
/// Tenant does not inherit from BaseModel or implement IModel directly.
/// </remarks>
[Table("YoFi.V3.Tenants")]
public record Tenant
{
    /// <summary>
    /// Database identity for this record
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Public-facing unique identifier for this record
    /// </summary>
    public Guid Key { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // For future use
#if false
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? DeactivatedAt { get; set; }
    public string? DeactivatedByUserId { get; set; }
#endif

    // Navigation properties
    public virtual ICollection<UserTenantRoleAssignment> RoleAssignments { get; set; } = new List<UserTenantRoleAssignment>();
}
