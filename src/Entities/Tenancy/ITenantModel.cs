namespace YoFi.V3.Entities.Tenancy;

/// <summary>
/// Identifies an object as a model which is tied to a specific tenant
/// </summary>
public interface ITenantModel
{
    /// <summary>
    /// Database identity for the tenant which owns this record
    /// </summary>
    /// <remarks>
    /// Remember to create appropriate foreign key relationships, and an index on TenantId.
    /// Tenancy is designed to be application-independent, so we don't use IModel
    /// as a base interface here.
    /// </remarks>
    long TenantId { get; }
}
