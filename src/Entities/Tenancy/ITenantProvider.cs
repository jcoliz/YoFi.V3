namespace YoFi.V3.Entities.Tenancy;

/// <summary>
/// Provides access to the current tenant.
/// </summary>
/// <remarks>
/// Application features which work on a per-tenant basis should depend on this interface
/// to get the current tenant context.
/// </remarks>
public interface ITenantProvider
{
    /// <summary>
    /// Gets the current tenant.
    /// </summary>
    Tenant CurrentTenant { get; }
}
