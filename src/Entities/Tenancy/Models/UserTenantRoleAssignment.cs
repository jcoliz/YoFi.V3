using System.ComponentModel.DataAnnotations.Schema;

namespace YoFi.V3.Entities.Tenancy.Models;

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

/// <summary>
/// Defines the roles a user can have within a tenant.
/// </summary>
/// <remarks>
/// Roles are hierarchical: Owner ≥ Editor ≥ Viewer.
/// Higher roles inherit all permissions of lower roles.
/// </remarks>
public enum TenantRole
{
    /// <summary>
    /// Can view tenant data but cannot make changes.
    /// </summary>
    Viewer = 1,

    /// <summary>
    /// Can view and edit tenant data but cannot manage tenant settings or users.
    /// </summary>
    Editor = 2,

    /// <summary>
    /// Full access to tenant data and settings, including user management.
    /// </summary>
    Owner = 3
}
