using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using YoFi.V3.Entities.Tenancy;

namespace YoFi.V3.Controllers.Tenancy.Authorization;

/// <summary>
/// Authorization handler that validates tenant role requirements for protected resources.
/// </summary>
/// <param name="httpContextAccessor">Accessor for the current HTTP context.</param>
/// <param name="logger">Logger for diagnostic output.</param>
/// <remarks>
/// This handler extracts the tenant key from the route, validates the user has appropriate
/// tenant role claims, and stores the validated tenant key and role in <see cref="HttpContext.Items"/>
/// for downstream middleware to use.
/// </remarks>
public partial class TenantRoleHandler(
    IHttpContextAccessor httpContextAccessor,
    ILogger<TenantRoleHandler> logger) : AuthorizationHandler<TenantRoleRequirement>
{
    /// <summary>
    /// Handles the authorization requirement by validating the user's tenant role.
    /// </summary>
    /// <param name="context">The authorization context.</param>
    /// <param name="requirement">The tenant role requirement to validate.</param>
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantRoleRequirement requirement)
    {
        LogHandlerCalled(requirement.MinimumRole, context.User.Identity?.IsAuthenticated ?? false);
        var httpContext = httpContextAccessor.HttpContext;

        if (httpContext == null)
        {
            LogNoHttpContext();
            return Task.CompletedTask;
        }

        var tenantKey = httpContext.Request.RouteValues["tenantKey"]?.ToString();

        if (string.IsNullOrEmpty(tenantKey))
        {
            LogNoTenantKey();
            return Task.CompletedTask;
        }

        LogCheckingTenantKey(tenantKey);

        var allClaims = context.User.Claims.Select(c => $"{c.Type}={c.Value}").ToList();
        LogAllClaims(string.Join(", ", allClaims));

        var claim = context.User.FindFirst(c =>
            c.Type == "tenant_role" &&
            c.Value.StartsWith($"{tenantKey}:"));

        if (claim != null)
        {
            LogFoundClaim(claim.Value);
            var parts = claim.Value.Split(':');
            if (parts.Length == 2 &&
                Enum.TryParse<TenantRole>(parts[1], out var userRole) &&
                userRole >= requirement.MinimumRole)
            {
                // STORE IT in HttpContext.Items for later use
                httpContext.Items["TenantKey"] = Guid.Parse(tenantKey);
                httpContext.Items["TenantRole"] = userRole;

                LogAuthorizationSuccess(tenantKey, userRole);
                context.Succeed(requirement);
            }
            else
            {
                LogInsufficientRole(parts.Length > 1 ? parts[1] : "unknown", requirement.MinimumRole);
            }
        }
        else
        {
            LogNoMatchingClaim(tenantKey);
        }
#if false

        // For now, just allow all if tenantKey is present
        httpContext?.Items["TenantKey"] = Guid.Parse(tenantKey);
        httpContext?.Items["TenantRole"] = TenantRole.Editor; // Default to Editor for now

        context.Succeed(requirement);
#endif

        return Task.CompletedTask;
    }

    [LoggerMessage(0, LogLevel.Debug, "{Location}: Handler called. MinimumRole={MinimumRole}, IsAuthenticated={IsAuthenticated}")]
    private partial void LogHandlerCalled(TenantRole minimumRole, bool isAuthenticated, [CallerMemberName] string? location = null);

    [LoggerMessage(1, LogLevel.Warning, "{Location}: No HttpContext available")]
    private partial void LogNoHttpContext([CallerMemberName] string? location = null);

    [LoggerMessage(2, LogLevel.Warning, "{Location}: No tenant key in route")]
    private partial void LogNoTenantKey([CallerMemberName] string? location = null);

    [LoggerMessage(3, LogLevel.Debug, "{Location}: Checking tenant key {TenantKey}")]
    private partial void LogCheckingTenantKey(string tenantKey, [CallerMemberName] string? location = null);

    [LoggerMessage(4, LogLevel.Debug, "{Location}: All claims: {Claims}")]
    private partial void LogAllClaims(string claims, [CallerMemberName] string? location = null);

    [LoggerMessage(5, LogLevel.Debug, "{Location}: Found matching claim: {ClaimValue}")]
    private partial void LogFoundClaim(string claimValue, [CallerMemberName] string? location = null);

    [LoggerMessage(6, LogLevel.Information, "{Location}: Authorization success for tenant {TenantKey} with role {UserRole}")]
    private partial void LogAuthorizationSuccess(string tenantKey, TenantRole userRole, [CallerMemberName] string? location = null);

    [LoggerMessage(7, LogLevel.Warning, "{Location}: Insufficient role. User has {UserRole}, requires {MinimumRole}")]
    private partial void LogInsufficientRole(string userRole, TenantRole minimumRole, [CallerMemberName] string? location = null);

    [LoggerMessage(8, LogLevel.Warning, "{Location}: No matching tenant_role claim for tenant {TenantKey}")]
    private partial void LogNoMatchingClaim(string tenantKey, [CallerMemberName] string? location = null);
}
