using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using YoFi.V3.Entities.Tenancy.Models;

namespace YoFi.V3.Tests.Integration.Controller.TestHelpers;

/// <summary>
/// Base WebApplicationFactory for integration tests with test authentication support.
/// </summary>
/// <remarks>
/// <para>
/// This factory configures a test instance of the web application with:
/// - In-memory SQLite database (auto-created, auto-cleaned)
/// - Test authentication scheme that bypasses production JWT authentication
/// - Configurable application settings via constructor
/// </para>
///
/// <para><strong>Authentication Architecture:</strong></para>
/// <list type="bullet">
/// <item>Overrides production authentication (ASP.NET Identity + NuxtIdentity) with TestAuthenticationHandler</item>
/// <item>Test user data flows via HTTP headers: X-Test-User-Id, X-Test-User-Name, X-Test-Tenant-Roles</item>
/// <item>TestAuthenticationHandler reads headers and creates claims (including tenant_role claims)</item>
/// <item>Authorization handlers (like TenantRoleHandler) validate claims normally</item>
/// </list>
///
/// <para><strong>Usage:</strong></para>
/// <code>
/// // Create unauthenticated client (for public endpoints)
/// var client = factory.CreateClient();
///
/// // Create authenticated client with Editor role (default)
/// var client = factory.CreateAuthenticatedClient(tenantKey);
///
/// // Create authenticated client with specific role
/// var client = factory.CreateAuthenticatedClient(tenantKey, TenantRole.Owner);
///
/// // Create client with access to multiple tenants
/// var client = factory.CreateAuthenticatedClient(new[]
/// {
///     (tenant1Key, TenantRole.Editor),
///     (tenant2Key, TenantRole.Viewer)
/// });
/// </code>
///
/// <para>
/// The factory automatically creates a temporary SQLite database and cleans it up on disposal.
/// Default configuration values are provided for common settings (version, environment, CORS, connection string).
/// </para>
/// </remarks>
public class BaseTestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath;
    private readonly Dictionary<string, string?> _configurationOverrides;
    private readonly string? _environment;

    public BaseTestWebApplicationFactory(
        Dictionary<string, string?>? configurationOverrides = null,
        string? dbPath = null,
        string? environment = null)
    {
        _configurationOverrides = configurationOverrides ?? new Dictionary<string, string?>();
        _dbPath = dbPath ?? Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");
        _environment = environment;

        // Set default configuration if not provided
        if (!_configurationOverrides.ContainsKey("Application:Version"))
            _configurationOverrides["Application:Version"] = "test-version";

        if (!_configurationOverrides.ContainsKey("Application:AllowedCorsOrigins:0"))
            _configurationOverrides["Application:AllowedCorsOrigins:0"] = "http://localhost:3000";

        if (!_configurationOverrides.ContainsKey("ConnectionStrings:DefaultConnection"))
            _configurationOverrides["ConnectionStrings:DefaultConnection"] = $"Data Source={_dbPath}";

        // JWT configuration required for non-Development environments
        if (!_configurationOverrides.ContainsKey("Jwt:Key"))
            _configurationOverrides["Jwt:Key"] = "YeO9WbjMjlalrCq5CpJEdBPFMevqfN4UtczfTtEmL14=";

        if (!_configurationOverrides.ContainsKey("Jwt:Issuer"))
            _configurationOverrides["Jwt:Issuer"] = "http://localhost:5000";

        if (!_configurationOverrides.ContainsKey("Jwt:Audience"))
            _configurationOverrides["Jwt:Audience"] = "http://localhost:5000";
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set environment if specified
        if (_environment is not null)
        {
            builder.UseEnvironment(_environment);
        }

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
    /// Delegating handler that injects test user authentication data via HTTP headers.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This handler is part of the test authentication flow. It intercepts outgoing HTTP requests
    /// and adds custom headers containing test user information. These headers are then read by
    /// TestAuthenticationHandler to create an authenticated user with appropriate claims.
    /// </para>
    ///
    /// <para><strong>Headers Added:</strong></para>
    /// <list type="bullet">
    /// <item><c>X-Test-User-Id</c>: The user's unique identifier</item>
    /// <item><c>X-Test-User-Name</c>: The user's display name</item>
    /// <item><c>X-Test-Tenant-Roles</c>: Comma-separated list of tenant:role pairs (e.g., "guid1:Editor,guid2:Viewer")</item>
    /// </list>
    ///
    /// <para>
    /// This approach was chosen because HttpClient delegating handlers can't directly access HttpContext.Items.
    /// HTTP headers provide a reliable way to pass test data through the HTTP pipeline to the authentication handler.
    /// </para>
    /// </remarks>
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
