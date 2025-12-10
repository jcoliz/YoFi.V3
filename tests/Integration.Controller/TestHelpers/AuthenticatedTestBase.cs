using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using YoFi.V3.Data;
using YoFi.V3.Entities.Tenancy;

namespace YoFi.V3.Tests.Integration.Controller.TestHelpers;

/// <summary>
/// Base class for integration tests that require authenticated access with tenant-based authorization.
/// </summary>
/// <remarks>
/// <para>
/// This base class simplifies writing integration tests for endpoints protected by tenant role authorization.
/// It handles the setup of test infrastructure (factory, database, authenticated client) and provides
/// convenient methods for switching between tenant roles during test execution.
/// </para>
///
/// <para><strong>Default Role: Editor</strong></para>
/// <para>
/// By default, the authenticated client has Editor role, which is the most common permission level for
/// data manipulation tests. This follows the principle of least privilege while covering the majority
/// of test scenarios.
/// </para>
///
/// <para><strong>Usage Pattern:</strong></para>
/// <code>
/// [TestFixture]
/// public class MyControllerTests : AuthenticatedTestBase
/// {
///     [Test]
///     public async Task GetData_AsEditor_ReturnsData()
///     {
///         // _client is already authenticated as Editor
///         // _testTenantKey is already created in the database
///         var response = await _client.GetAsync($"/api/tenant/{_testTenantKey}/data");
///         Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
///     }
///
///     [Test]
///     public async Task DeleteData_AsViewer_ReturnsForbidden()
///     {
///         SwitchToViewer(); // Change to Viewer role
///         var response = await _client.DeleteAsync($"/api/tenant/{_testTenantKey}/data/123");
///         Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
///     }
///
///     [Test]
///     public async Task ManageTenant_AsOwner_Succeeds()
///     {
///         SwitchToOwner(); // Explicitly switch to Owner for privileged operations
///         var response = await _client.PutAsync($"/api/tenant/{_testTenantKey}/settings", content);
///         Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
///     }
/// }
/// </code>
///
/// <para><strong>Multi-Tenant Testing:</strong></para>
/// <para>
/// For tests that verify cross-tenant isolation, use CreateMultiTenantClient() to create
/// a client authenticated with access to multiple tenants simultaneously.
/// </para>
///
/// <para><strong>Test Lifecycle:</strong></para>
/// <list type="bullet">
/// <item>OneTimeSetUp: Creates factory, tenant, and authenticated client</item>
/// <item>Tests: Use _client and _testTenantKey</item>
/// <item>OneTimeTearDown: Cleans up client, factory, and temporary database</item>
/// </list>
/// </remarks>
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
