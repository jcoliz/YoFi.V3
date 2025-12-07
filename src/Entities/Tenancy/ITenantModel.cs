namespace YoFi.V3.Entities.Tenancy;

/// <summary>
/// Identifies an object as a model which is tied to a specific tenant
/// </summary>
public interface ITenantModel
{
    /// <summary>
    /// Database identity for the tenant which owns this record
    /// </summary>
    long TenantId { get; }
}
