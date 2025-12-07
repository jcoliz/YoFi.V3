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
    /// Tenancy is designed to be application-independent, so we don't use IModel
    /// as a base interface here.
    /// </remarks>
    long TenantId { get; }
}
