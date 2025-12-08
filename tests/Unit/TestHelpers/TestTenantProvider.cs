using YoFi.V3.Entities.Tenancy;

namespace YoFi.V3.Tests.Unit.TestHelpers;

public class TestTenantProvider : ITenantProvider
{
    public Tenant CurrentTenant { get; set; } = new Tenant
    {
        Key = Guid.NewGuid(),
        Name = "Test Tenant"
    };
}
