using System.ComponentModel.DataAnnotations.Schema;
using YoFi.V3.Entities.Models;

namespace YoFi.V3.Entities.Tenancy;

[Table("YoFi.V3.Tenants")]
public class Tenant: IModel
{
    public long Id { get; set; }
    public Guid Key { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    // For future use
#if false
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? DeactivatedAt { get; set; }
    public string? DeactivatedByUserId { get; set; }
#endif

    // Navigation properties
    public virtual ICollection<UserTenantRoleAssignment> RoleAssignments { get; set; } = new List<UserTenantRoleAssignment>();
}
