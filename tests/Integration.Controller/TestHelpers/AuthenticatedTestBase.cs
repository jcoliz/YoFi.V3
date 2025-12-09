using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using YoFi.V3.Data;
using YoFi.V3.Entities.Tenancy;

namespace YoFi.V3.Tests.Integration.Controller.TestHelpers;

/// <summary>
/// Base class for integration tests that require authenticated access.
/// Default role: Editor (use SwitchToOwner() for Owner-specific tests)
/// </summary>
public abstract class AuthenticatedTestBase
{
    protected BaseTestWebApplicationFactory _factory = null!;

    [SuppressMessage("NUnit1032", "NUnit1032:The field should be Disposed in a method annotated with [TearDownAttribute]",
        Justification = "Field is disposed in OneTimeTearDown method")]
    protected HttpClient _client = null!;
    protected Guid _testTenantKey;
    protected TenantRole _currentRole = TenantRole.Editor;

    /// <summary>
    /// Override to customize default role (default: Editor)
    /// </summary>
    protected virtual TenantRole DefaultRole => TenantRole.Editor;

    [OneTimeSetUp]
    public virtual async Task OneTimeSetUp()
    {
        _factory = new BaseTestWebApplicationFactory();
        _testTenantKey = await CreateTestTenantAsync();
        _client = _factory.CreateAuthenticatedClient(_testTenantKey, DefaultRole);
        _currentRole = DefaultRole;
    }

    [OneTimeTearDown]
    public virtual void OneTimeTearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    /// <summary>
    /// Creates a test tenant in the database and returns its key
    /// </summary>
    protected async Task<Guid> CreateTestTenantAsync(string? name = null, string? description = null)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var tenant = new Tenant
        {
            Key = Guid.NewGuid(),
            Name = name ?? "Test Tenant",
            Description = description ?? "Test tenant for integration testing",
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Set<Tenant>().Add(tenant);
        await dbContext.SaveChangesAsync();

        return tenant.Key;
    }

    /// <summary>
    /// Switch to Viewer role (read-only access)
    /// </summary>
    protected void SwitchToViewer()
    {
        SwitchToRole(TenantRole.Viewer);
    }

    /// <summary>
    /// Switch to Editor role (default - read/write access)
    /// </summary>
    protected void SwitchToEditor()
    {
        SwitchToRole(TenantRole.Editor);
    }

    /// <summary>
    /// Switch to Owner role (full control - use explicitly for Owner tests)
    /// </summary>
    protected void SwitchToOwner()
    {
        SwitchToRole(TenantRole.Owner);
    }

    /// <summary>
    /// Switch to specified role for current tenant
    /// </summary>
    protected void SwitchToRole(TenantRole role)
    {
        _client?.Dispose();
        _client = _factory.CreateAuthenticatedClient(_testTenantKey, role);
        _currentRole = role;
    }

    /// <summary>
    /// Create a client authenticated for multiple tenants (cross-tenant testing)
    /// </summary>
    protected HttpClient CreateMultiTenantClient(params (Guid tenantKey, TenantRole role)[] tenantRoles)
    {
        return _factory.CreateAuthenticatedClient(tenantRoles);
    }
}
