using YoFi.V3.Entities.Tenancy;
using YoFi.V3.Entities.Tenancy.Exceptions;

namespace YoFi.V3.Controllers.Tenancy.Context;

/// <summary>
/// Manages the current tenant context for the HTTP request.
/// </summary>
/// <param name="tenantRepository">Repository for tenant data operations.</param>
/// <remarks>
/// This service maintains the current tenant for the request scope and implements
/// <see cref="ITenantProvider"/> to provide tenant information to other services.
/// The tenant context is set by <see cref="TenantContextMiddleware"/> early in the
/// request pipeline.
/// </remarks>
public class TenantContext(ITenantRepository tenantRepository): ITenantProvider
{
    /// <summary>
    /// Gets the current tenant for this request.
    /// </summary>
    /// <exception cref="TenantContextNotSetException">Thrown when accessed before context is set.</exception>
    public Tenant CurrentTenant
    {
        get
        {
            if (_currentTenant == null)
            {
                // Truly a 500 error if we try to access current tenant when not set
                throw new TenantContextNotSetException();
            }

            return _currentTenant;
        }
        private set
        {
            _currentTenant = value;
        }
    }
    private Tenant? _currentTenant = null;

    /// <summary>
    /// Sets the current tenant for this request by loading it from the repository.
    /// </summary>
    /// <param name="tenantKey">The unique key of the tenant to set as current.</param>
    /// <exception cref="TenantNotFoundException">Thrown when the tenant is not found.</exception>
    public async Task SetCurrentTenantAsync(Guid tenantKey)
    {
        var tenant = await tenantRepository.GetTenantByKeyAsync(tenantKey);

        if (tenant == null)
        {
            // Needs to return 404 (somehow)
            throw new TenantNotFoundException(tenantKey);
        }

        CurrentTenant = tenant;
    }
}
