using System.ComponentModel.DataAnnotations.Schema;

namespace YoFi.V3.Entities.Tenancy;

/// <summary>
/// Describes a role assigned to a specific user for a specific tenant
/// </summary>
/// <remarks>
/// One user may only have one role. Roles are linearly ordered by power level.
/// So a user with the Editor role can do everything a Viewer can do, but not
/// everything an Owner can do.
/// </remarks>
[Table("YoFi.V3.UserTenantRoleAssignments")]
public record UserTenantRoleAssignment
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
