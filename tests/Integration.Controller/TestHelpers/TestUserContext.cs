using YoFi.V3.Entities.Tenancy;

namespace YoFi.V3.Tests.Integration.Controller.TestHelpers;

/// <summary>
/// Holds test user context data for the current HTTP request in integration tests.
/// </summary>
/// <remarks>
/// <para><strong>NOTE: This class is currently UNUSED in the active implementation.</strong></para>
/// <para>
/// The current test authentication flow uses HTTP headers to pass test user data
/// (see TestUserInjectingHandler in BaseTestWebApplicationFactory). This class was part of
/// an earlier middleware-based approach that was replaced with the simpler header-based solution.
/// </para>
///
/// <para>
/// This class is retained for potential future use if the authentication mechanism needs to change,
/// but it is not currently registered as a service or used in the test pipeline.
/// </para>
///
/// <para><strong>Active Flow (Header-Based):</strong></para>
/// <list type="number">
/// <item>TestUserInjectingHandler adds HTTP headers (X-Test-User-Id, X-Test-User-Name, X-Test-Tenant-Roles)</item>
/// <item>TestAuthenticationHandler reads headers and creates claims</item>
/// </list>
///
/// <para><strong>Previous Flow (Middleware-Based, Not Active):</strong></para>
/// <list type="number">
/// <item>TestUserScopedHandler would populate this scoped service</item>
/// <item>Middleware would read from this service and populate HttpContext.Items</item>
/// <item>TestAuthenticationHandler would read from HttpContext.Items</item>
/// </list>
/// </remarks>
public class TestUserContext
{
    public List<(Guid tenantKey, TenantRole role)> TenantRoles { get; set; } = new();
    public string UserId { get; set; } = "test-user-id";
    public string UserName { get; set; } = "test-user";
}
