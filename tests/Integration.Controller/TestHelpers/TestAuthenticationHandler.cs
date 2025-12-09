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
        // Retrieve test user configuration from HttpContext.Items
        // (Set by BaseTestWebApplicationFactory before request)
        var tenantRoles = Context.Items["TestUser:TenantRoles"]
            as List<(Guid tenantKey, TenantRole role)>;
        var userId = Context.Items["TestUser:UserId"] as string ?? "test-user-id";
        var userName = Context.Items["TestUser:UserName"] as string ?? "test-user";

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
