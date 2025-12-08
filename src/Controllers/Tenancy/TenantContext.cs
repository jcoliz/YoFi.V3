using YoFi.V3.Entities.Providers;
using YoFi.V3.Entities.Tenancy;

namespace YoFi.V3.Controllers.Tenancy;

public class TenantContext(IDataProvider dataProvider): ITenantProvider
{
    public Tenant CurrentTenant
    {
        get
        {
            if (_currentTenant == null)
            {
                throw new InvalidOperationException("Current tenant is not set.");
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
        var tenantQuery = dataProvider
            .Get<Tenant>()
            .Where(t => t.Key == tenantKey);

        var tenant = await dataProvider.SingleOrDefaultAsync(tenantQuery);

        if (tenant == null)
        {
            throw new InvalidOperationException($"Tenant with key {tenantKey} not found.");
        }

        CurrentTenant = tenant;
    }
}
