using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YoFi.V3.Entities.Tenancy;

namespace YoFi.V3.Tests.Integration.Controller.TestHelpers;

/// <summary>
/// Custom authentication handler for integration tests that creates authenticated users from HTTP headers.
/// </summary>
/// <remarks>
/// <para>
/// This handler replaces production authentication (ASP.NET Identity + NuxtIdentity JWT) during integration testing.
/// It reads test user data from custom HTTP headers and creates an authenticated ClaimsPrincipal with appropriate claims.
/// </para>
///
/// <para><strong>Architecture:</strong></para>
/// <list type="number">
/// <item>TestUserInjectingHandler (in BaseTestWebApplicationFactory) adds headers to HTTP requests</item>
/// <item>ASP.NET Core authentication middleware invokes this handler</item>
/// <item>Handler reads headers and creates claims (including tenant_role claims for multi-tenancy)</item>
/// <item>Authorization handlers (e.g., TenantRoleHandler) validate claims normally</item>
/// </list>
///
/// <para><strong>Headers Read:</strong></para>
/// <list type="bullet">
/// <item><c>X-Test-User-Id</c>: Maps to ClaimTypes.NameIdentifier claim</item>
/// <item><c>X-Test-User-Name</c>: Maps to ClaimTypes.Name claim</item>
/// <item><c>X-Test-Tenant-Roles</c>: Parsed into tenant_role claims (format: "tenantGuid:RoleName,...")</item>
/// </list>
///
/// <para><strong>Tenant Role Claims:</strong></para>
/// <para>
/// Each tenant role is stored as a claim with type "tenant_role" and value "{tenantKey}:{role}".
/// This matches the production claim structure expected by TenantRoleHandler, ensuring authorization
/// logic works identically in tests and production.
/// </para>
///
/// <para><strong>Example:</strong></para>
/// <code>
/// // Header: X-Test-Tenant-Roles: "123e4567-e89b-12d3-a456-426614174000:Editor"
/// // Results in claim: Type="tenant_role", Value="123e4567-e89b-12d3-a456-426614174000:Editor"
/// </code>
/// </remarks>
public class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    /// <summary>
    /// The authentication scheme name used to register and identify this handler.
    /// </summary>
    public const string SchemeName = "TestScheme";

    public TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Read test user data from custom headers (set by TestUserInjectingHandler)
        var userId = Context.Request.Headers["X-Test-User-Id"].FirstOrDefault() ?? "test-user-id";
        var userName = Context.Request.Headers["X-Test-User-Name"].FirstOrDefault() ?? "test-user";

        // Parse tenant roles from header (format: "guid1:role1,guid2:role2")
        var tenantRoles = new List<(Guid tenantKey, TenantRole role)>();
        var rolesHeader = Context.Request.Headers["X-Test-Tenant-Roles"].FirstOrDefault();

        if (!string.IsNullOrEmpty(rolesHeader))
        {
            foreach (var roleEntry in rolesHeader.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = roleEntry.Split(':', 2);
                if (parts.Length == 2 &&
                    Guid.TryParse(parts[0], out var tenantKey) &&
                    Enum.TryParse<TenantRole>(parts[1], out var role))
                {
                    tenantRoles.Add((tenantKey, role));
                }
            }
        }

        Logger.LogDebug("TestAuthenticationHandler: tenantRoles={RoleCount}, userId={UserId}, userName={UserName}",
            tenantRoles.Count, userId, userName);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, userName)
        };

        // Add tenant role claims
        if (tenantRoles != null)
        {
            foreach (var (tenantKey, role) in tenantRoles)
            {
                claims.Add(new Claim("tenant_role", $"{tenantKey}:{role}"));
            }
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
