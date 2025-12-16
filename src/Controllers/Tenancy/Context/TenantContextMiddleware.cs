using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace YoFi.V3.Controllers.Tenancy.Context;

/// <summary>
/// Middleware that sets the current tenant context for tenant-scoped requests.
/// </summary>
/// <param name="next">The next middleware in the pipeline.</param>
/// <param name="logger">Logger for diagnostic output.</param>
/// <remarks>
/// This middleware must be added AFTER authentication and authorization middlewares in the pipeline.
/// It extracts the tenant key from <see cref="HttpContext.Items"/> (where it's placed by
/// the authorization handler during authorization) and uses it to set the current tenant
/// in <see cref="TenantContext"/>.
///
/// <para><strong>Behavior:</strong></para>
/// <list type="bullet">
/// <item>
/// <description>If the route contains a "tenantKey" parameter AND <c>HttpContext.Items["TenantKey"]</c>
/// is set, the tenant context is initialized.</description>
/// </item>
/// <item>
/// <description>If the route contains a "tenantKey" parameter BUT <c>HttpContext.Items["TenantKey"]</c>
/// is NOT set, returns 401 Unauthorized (indicates missing/failed authorization).</description>
/// </item>
/// <item>
/// <description>If the route does NOT contain a "tenantKey" parameter, the request continues
/// without tenant context (for non-tenant endpoints like /api/version).</description>
/// </item>
/// </list>
///
/// <para><strong>Pipeline Order:</strong></para>
/// <code>
/// app.UseAuthentication();     // 1. Authenticate user
/// app.UseAuthorization();      // 2. Run authorization handlers (sets Items["TenantKey"])
/// app.UseTenancy();            // 3. THIS MIDDLEWARE - uses Items["TenantKey"]
/// app.MapControllers();        // 4. Route to controllers/features
/// </code>
/// </remarks>
public partial class TenantContextMiddleware(RequestDelegate next, ILogger<TenantContextMiddleware> logger)
{
    /// <summary>
    /// Processes the HTTP request and sets the tenant context if applicable.
    /// </summary>
    /// <param name="context">The HTTP context for the current request.</param>
    /// <param name="tenantContext">The tenant context service to be populated.</param>
    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext)
    {
        // Check if the route expects a tenant key parameter
        var hasTenantRoute = context.Request.RouteValues.ContainsKey("tenantKey");

        // Extract tenant key from HttpContext.Items (placed there by authorization handler)
        if (context.Items.TryGetValue("TenantKey", out var tenantKeyObj) &&
            tenantKeyObj is Guid tenantKey)
        {
            // Authorization succeeded and set the tenant key - use it
            await tenantContext.SetCurrentTenantAsync(tenantKey);
        }
        else if (hasTenantRoute)
        {
            // Route expects a tenant but authorization didn't set the tenant key
            // This indicates missing or failed authorization - fail fast with clear error
            LogUnauthorizedTenantAccess();

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;

            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "Tenant authorization required for this endpoint",
                Type = "https://tools.ietf.org/html/rfc7235#section-3.1",
                Instance = context.Request.Path
            };

            // Add trace ID for correlation (matches CustomExceptionHandler pattern)
            var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
            problemDetails.Extensions["traceId"] = traceId;

            await context.Response.WriteAsJsonAsync(problemDetails);
            return; // Stop pipeline - don't call next()
        }
        // else: Route doesn't need tenant context - allow request to continue

        await next(context);
    }

    [LoggerMessage(1, LogLevel.Warning, "{Location}: Tenant authorization required but not provided")]
    private partial void LogUnauthorizedTenantAccess([CallerMemberName] string? location = null);
}
