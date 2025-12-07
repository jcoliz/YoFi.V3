using System.ComponentModel.DataAnnotations.Schema;

namespace YoFi.V3.Entities.Tenancy;

[Table("YoFi.V3.UserTenantRoleAssignments")]
public class UserTenantRoleAssignment
{
    public long Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public long TenantId { get; set; }
    public TenantRole Role { get; set; }

    // Navigation properties
    public virtual Tenant? Tenant { get; set; }
}

public enum TenantRole
{
    Viewer = 1,
    Editor = 2,
    Owner = 3
}
