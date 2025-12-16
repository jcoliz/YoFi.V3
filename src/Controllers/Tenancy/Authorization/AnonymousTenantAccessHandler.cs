using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace YoFi.V3.Controllers.Tenancy.Authorization;

/// <summary>
/// Authorization handler that allows anonymous access to tenant-scoped endpoints.
/// </summary>
/// <param name="httpContextAccessor">Accessor for the current HTTP context.</param>
/// <param name="logger">Logger for diagnostic output.</param>
/// <remarks>
/// This handler extracts the tenant key from the route and stores it in
/// <see cref="HttpContext.Items"/> without requiring user authentication.
/// The downstream <see cref="Context.TenantContextMiddleware"/> will pick up
/// the tenant key and set the tenant context normally.
///
/// <para><strong>SECURITY:</strong></para>
/// <para>
/// This handler ONLY validates that a tenant key exists in the route.
/// Endpoints using this policy MUST implement their own security validation
/// (e.g., checking __TEST__ prefix on tenant names and usernames).
/// </para>
/// </remarks>
public partial class AnonymousTenantAccessHandler(
    IHttpContextAccessor httpContextAccessor,
    ILogger<AnonymousTenantAccessHandler> logger)
    : AuthorizationHandler<AnonymousTenantAccessRequirement>
{
    /// <summary>
    /// Handles the authorization requirement by extracting and storing the tenant key.
    /// </summary>
    /// <param name="context">The authorization context.</param>
    /// <param name="requirement">The anonymous tenant access requirement.</param>
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AnonymousTenantAccessRequirement requirement)
    {
        LogHandlerCalled(context.User.Identity?.IsAuthenticated ?? false);

        var httpContext = httpContextAccessor.HttpContext;

        if (httpContext == null)
        {
            LogNoHttpContext();
            return Task.CompletedTask;
        }

        // Extract tenant key from route (same as TenantRoleHandler)
        var tenantKeyString = httpContext.Request.RouteValues["tenantKey"]?.ToString();

        if (string.IsNullOrEmpty(tenantKeyString))
        {
            LogNoTenantKey();
            return Task.CompletedTask; // Fail - no tenant key in route
        }

        if (!Guid.TryParse(tenantKeyString, out var tenantKey))
        {
            LogInvalidTenantKey(tenantKeyString);
            return Task.CompletedTask; // Fail - invalid GUID format
        }

        LogSettingTenantKey(tenantKey);

        // Store tenant key in HttpContext.Items for TenantContextMiddleware
        // (same location as TenantRoleHandler uses)
        httpContext.Items["TenantKey"] = tenantKey;

        // Succeed - allow anonymous access with tenant context
        context.Succeed(requirement);

        LogAuthorizationSuccess(tenantKey);
        return Task.CompletedTask;
    }

    [LoggerMessage(1, LogLevel.Debug, "{Location}: Handler called. IsAuthenticated={IsAuthenticated}")]
    private partial void LogHandlerCalled(bool isAuthenticated, [CallerMemberName] string? location = null);

    [LoggerMessage(2, LogLevel.Warning, "{Location}: No HttpContext available")]
    private partial void LogNoHttpContext([CallerMemberName] string? location = null);

    [LoggerMessage(3, LogLevel.Warning, "{Location}: No tenant key in route")]
    private partial void LogNoTenantKey([CallerMemberName] string? location = null);

    [LoggerMessage(4, LogLevel.Warning, "{Location}: Invalid tenant key format: {TenantKeyString}")]
    private partial void LogInvalidTenantKey(string tenantKeyString, [CallerMemberName] string? location = null);

    [LoggerMessage(5, LogLevel.Debug, "{Location}: Setting tenant key {TenantKey}")]
    private partial void LogSettingTenantKey(Guid tenantKey, [CallerMemberName] string? location = null);

    [LoggerMessage(6, LogLevel.Information, "{Location}: Anonymous tenant access granted for tenant {TenantKey}")]
    private partial void LogAuthorizationSuccess(Guid tenantKey, [CallerMemberName] string? location = null);
}
