using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using YoFi.V3.Entities.Tenancy;

namespace YoFi.V3.Tests.Integration.Controller.TestHelpers;

/// <summary>
/// Base WebApplicationFactory with common test configuration
/// </summary>
public class BaseTestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath;
    private readonly Dictionary<string, string?> _configurationOverrides;

    public BaseTestWebApplicationFactory(
        Dictionary<string, string?>? configurationOverrides = null,
        string? dbPath = null)
    {
        _configurationOverrides = configurationOverrides ?? new Dictionary<string, string?>();
        _dbPath = dbPath ?? Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");

        // Set default configuration if not provided
        if (!_configurationOverrides.ContainsKey("Application:Version"))
            _configurationOverrides["Application:Version"] = "test-version";

        if (!_configurationOverrides.ContainsKey("Application:Environment"))
            _configurationOverrides["Application:Environment"] = "Local";

        if (!_configurationOverrides.ContainsKey("Application:AllowedCorsOrigins:0"))
            _configurationOverrides["Application:AllowedCorsOrigins:0"] = "http://localhost:3000";

        if (!_configurationOverrides.ContainsKey("ConnectionStrings:DefaultConnection"))
            _configurationOverrides["ConnectionStrings:DefaultConnection"] = $"Data Source={_dbPath}";
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(_configurationOverrides);
        });

        builder.ConfigureTestServices(services =>
        {
            // Register test authentication scheme
            services.AddAuthentication(TestAuthenticationHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName,
                    options => { });
        });
    }

    /// <summary>
    /// Creates an authenticated HTTP client with Editor role (default for most tests)
    /// </summary>
    public HttpClient CreateAuthenticatedClient(Guid tenantKey)
        => CreateAuthenticatedClient(tenantKey, TenantRole.Editor);

    /// <summary>
    /// Creates an authenticated HTTP client with specified role
    /// </summary>
    public HttpClient CreateAuthenticatedClient(Guid tenantKey, TenantRole role,
        string? userId = null, string? userName = null)
    {
        return CreateAuthenticatedClient(
            new[] { (tenantKey, role) },
            userId,
            userName);
    }

    /// <summary>
    /// Creates an authenticated HTTP client with multiple tenant roles (for cross-tenant tests)
    /// </summary>
    public HttpClient CreateAuthenticatedClient(
        (Guid tenantKey, TenantRole role)[] tenantRoles,
        string? userId = null,
        string? userName = null)
    {
        var client = CreateClient();

        // Store test user info in a way that will be accessible per-request
        // We'll use a delegating handler to inject into HttpContext.Items
        var handler = new TestUserDelegatingHandler(
            tenantRoles.ToList(),
            userId ?? "test-user-id",
            userName ?? "test-user");

        return CreateDefaultClient(handler);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            // Clean up the temporary database file
            try
            {
                if (File.Exists(_dbPath))
                {
                    File.Delete(_dbPath);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
