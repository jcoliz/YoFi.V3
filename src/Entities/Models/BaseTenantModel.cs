using YoFi.V3.Entities.Tenancy.Models;

namespace YoFi.V3.Entities.Models;

/// <summary>
/// Base implementation of tenant-scoped model
/// </summary>
/// <remarks>
/// Note that this inherits from BaseModel to get Id and Key implementations,
/// which makes it application-specific, which is why it's here and not in the
/// Tenancy namespace.
/// </remarks>
public record BaseTenantModel : BaseModel, ITenantModel
{
    /// <inheritdoc/>
    public long TenantId { get; set; }
}
