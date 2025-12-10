using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YoFi.V3.Entities.Tenancy;

namespace YoFi.V3.Tests.Integration.Controller.TestHelpers;

public class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
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
