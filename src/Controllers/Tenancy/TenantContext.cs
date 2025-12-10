using YoFi.V3.Entities.Tenancy;

namespace YoFi.V3.Controllers.Tenancy;

public class TenantContext(ITenantRepository tenantRepository): ITenantProvider
{
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
