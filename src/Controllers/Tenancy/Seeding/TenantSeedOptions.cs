using YoFi.V3.Entities.Tenancy.Models;

namespace YoFi.V3.Controllers.Tenancy.Seeding;

/// <summary>
/// Options for seeding a tenant, including its name, description, key, and access roles.
/// </summary>
public record TenantSeedOptions
{
    /// <summary>
    /// Name of the seeded tenant.
    /// </summary>
    public string Name { get; init; } = string.Empty;
    /// <summary>
    /// Description of the seeded tenant.
    /// </summary>
    public string Description { get; init; } = string.Empty;
    /// <summary>
    /// Key of the seeded tenant.
    /// </summary>
    public Guid Key { get; init; } = Guid.Empty;
}

/// <summary>
/// Options for seeding a tenant role, including the user name and role.
/// </summary>
public record TenantRoleSeedOptions
{
    /// <summary>
    /// Key of the seeded tenant.
    /// </summary>
    public Guid Key { get; init; } = Guid.Empty;

    /// <summary>
    /// User name associated with the role.
    /// </summary>
    public string UserName { get; init; } = string.Empty;

    /// <summary>
    /// Role assigned to the user.
    /// </summary>
    public TenantRole Role { get; init; }
}
