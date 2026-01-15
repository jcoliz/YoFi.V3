using Microsoft.Extensions.Caching.Memory;
using YoFi.V3.Application.Features;
using YoFi.V3.Application.Services;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Tenancy.Models;
using YoFi.V3.Entities.Tenancy.Providers;
using YoFi.V3.Tests.Integration.Application.TestHelpers;

namespace YoFi.V3.Tests.Integration.Application;

/// <summary>
/// Base class for PayeeMatchingRuleFeature integration tests.
/// </summary>
/// <remarks>
/// Provides shared setup and teardown for PayeeMatchingRuleFeature tests,
/// including test tenant creation and dependency initialization.
/// </remarks>
public abstract class PayeeMatchingRuleFeatureTestBase : FeatureTestBase
{
    protected PayeeMatchingRuleFeature _feature = null!;
    protected ITenantProvider _tenantProvider = null!;
    protected IMemoryCache _memoryCache = null!;
    protected Tenant _testTenant = null!;

    [SetUp]
    public new async Task SetUp()
    {
        await base.SetUp();

        // Create test tenant in database
        _testTenant = new Tenant
        {
            Key = Guid.NewGuid(),
            Name = "Test Tenant"
        };
        _context.Tenants.Add(_testTenant);
        await _context.SaveChangesAsync();

        // Create tenant provider
        _tenantProvider = new TestTenantProvider { CurrentTenant = _testTenant };

        // Create memory cache
        _memoryCache = new MemoryCache(new MemoryCacheOptions());

        // Create feature with dependencies
        _feature = new PayeeMatchingRuleFeature(_tenantProvider, _dataProvider, _memoryCache);
    }

    [TearDown]
    public void TearDownPayeeTests()
    {
        // Dispose memory cache before base class disposes context
        _memoryCache?.Dispose();
    }
}

/// <summary>
/// Test implementation of ITenantProvider for testing.
/// </summary>
file class TestTenantProvider : ITenantProvider
{
    public required Tenant CurrentTenant { get; set; }
}

/// <summary>
/// Test implementation of IMatchableTransaction for testing ApplyMatchingRulesAsync.
/// </summary>
file class TestMatchableTransaction(string payee) : IMatchableTransaction
{
    public string Payee { get; } = payee;
}
