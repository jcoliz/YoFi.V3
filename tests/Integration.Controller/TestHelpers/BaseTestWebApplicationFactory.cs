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
            // Override the production authentication setup with test authentication
            // We need to set the test scheme as the default for all authentication types
            services.AddAuthentication(options =>
            {
                // Set test scheme as default for all authentication needs
                options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultScheme = TestAuthenticationHandler.SchemeName;
            })
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
        var testUserId = userId ?? "test-user-id";
        var testUserName = userName ?? "test-user";
        var testTenantRoles = tenantRoles.ToList();

        // Create a client with a custom handler that injects test user data into HttpContext.Items
        var handler = new TestUserInjectingHandler(testTenantRoles, testUserId, testUserName);

        return CreateDefaultClient(handler);
    }

    /// <summary>
    /// Custom delegating handler that ensures test user data is available in HttpContext.Items
    /// </summary>
    private class TestUserInjectingHandler : DelegatingHandler
    {
        private readonly List<(Guid tenantKey, TenantRole role)> _tenantRoles;
        private readonly string _userId;
        private readonly string _userName;

        public TestUserInjectingHandler(
            List<(Guid tenantKey, TenantRole role)> tenantRoles,
            string userId,
            string userName)
        {
            _tenantRoles = tenantRoles;
            _userId = userId;
            _userName = userName;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            // Add custom header with serialized test user data
            // The test auth handler will extract this
            request.Headers.Add("X-Test-User-Id", _userId);
            request.Headers.Add("X-Test-User-Name", _userName);

            // Serialize tenant roles as header (format: "guid1:role1,guid2:role2")
            if (_tenantRoles.Any())
            {
                var rolesHeader = string.Join(",", _tenantRoles.Select(tr => $"{tr.tenantKey}:{tr.role}"));
                request.Headers.Add("X-Test-Tenant-Roles", rolesHeader);
            }

            return base.SendAsync(request, cancellationToken);
        }
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
