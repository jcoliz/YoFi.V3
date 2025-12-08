using Microsoft.AspNetCore.Http;

namespace YoFi.V3.Controllers.Tenancy;

public class TenantContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext)
    {
        // Extract from route (already validated by authorization)
        // TODO: Authorization will place this value in HttpContext.Items, so we can use that instead
        if (context.Request.RouteValues.TryGetValue("tenantKey", out var tenantKeyValue) &&
            Guid.TryParse(tenantKeyValue?.ToString(), out var tenantKey))
        {
            await tenantContext.SetCurrentTenantAsync(tenantKey);
#if false
            // TODO: Also set role on tenant provider

            // Extract role from claims
            var roleClaim = context.User.FindFirst(c =>
                c.Type == "tenant_role" &&
                c.Value.StartsWith($"{tenantKey}:"));

            if (roleClaim != null)
            {
                var parts = roleClaim.Value.Split(':');
                if (parts.Length == 2 && Enum.TryParse<TenantRole>(parts[1], out var role))
                {
                    tenantContextImpl.Role = role;
                }
            }
#endif
        }

        await next(context);
    }
}
