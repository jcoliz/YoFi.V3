using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using YoFi.V3.Entities.Tenancy;

namespace YoFi.V3.Controllers.Tenancy;

public class TenantRoleHandler(IHttpContextAccessor httpContextAccessor) : AuthorizationHandler<TenantRoleRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantRoleRequirement requirement)
    {
        var httpContext = httpContextAccessor.HttpContext;

        if (httpContext == null)
            return Task.CompletedTask;

        var tenantKey = httpContext?.Request.RouteValues["tenantKey"]?.ToString();

        if (string.IsNullOrEmpty(tenantKey))
            return Task.CompletedTask;

#if false
        // TODO: Implement multi-tenant roles
        var claim = context.User.FindFirst(c =>
            c.Type == "tenant_role" &&
            c.Value.StartsWith($"{tenantKey}:"));

        if (claim != null)
        {
            var parts = claim.Value.Split(':');
            if (parts.Length == 2 &&
                Enum.TryParse<TenantRole>(parts[1], out var userRole) &&
                userRole >= requirement.MinimumRole)
            {
                // STORE IT in HttpContext.Items for later use
                httpContext.Items["TenantKey"] = Guid.Parse(tenantKey);
                httpContext.Items["TenantRole"] = userRole;

                context.Succeed(requirement);
            }
        }
#else


        // For now, just allow all if tenantKey is present
        httpContext?.Items["TenantKey"] = Guid.Parse(tenantKey);
        httpContext?.Items["TenantRole"] = TenantRole.Editor; // Default to Editor for now

        context.Succeed(requirement);
#endif

        return Task.CompletedTask;
    }
}
