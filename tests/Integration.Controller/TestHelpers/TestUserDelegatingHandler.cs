using Microsoft.Extensions.DependencyInjection;
using YoFi.V3.Entities.Tenancy;

namespace YoFi.V3.Tests.Integration.Controller.TestHelpers;

/// <summary>
/// DEPRECATED: Delegating handler from an earlier middleware-based authentication approach.
/// </summary>
/// <remarks>
/// <para><strong>This class is currently UNUSED and should be considered deprecated.</strong></para>
///
/// <para>
/// This handler was part of an earlier implementation that attempted to use request.Properties
/// to pass test user data through the HTTP pipeline. However, request.Properties don't automatically
/// flow to HttpContext.Items, making this approach ineffective.
/// </para>
///
/// <para><strong>Current Implementation:</strong></para>
/// <para>
/// The active authentication flow uses TestUserInjectingHandler (inside BaseTestWebApplicationFactory)
/// which passes test user data via HTTP headers. This is simpler, more reliable, and requires no middleware.
/// </para>
///
/// <para><strong>Why This Approach Didn't Work:</strong></para>
/// <list type="bullet">
/// <item>request.Properties are specific to HttpRequestMessage (client-side)</item>
/// <item>HttpContext.Items are specific to the server-side request processing</item>
/// <item>These two storage mechanisms don't automatically synchronize</item>
/// <item>Would have required custom middleware to bridge the gap</item>
/// </list>
///
/// <para>
/// This class is retained for historical reference but should not be used in new tests.
/// Use BaseTestWebApplicationFactory.CreateAuthenticatedClient() instead.
/// </para>
/// </remarks>
public class TestUserScopedHandler : DelegatingHandler
{
    private readonly BaseTestWebApplicationFactory _factory;
    private readonly List<(Guid tenantKey, TenantRole role)> _tenantRoles;
    private readonly string _userId;
    private readonly string _userName;

    public TestUserScopedHandler(
        BaseTestWebApplicationFactory factory,
        List<(Guid tenantKey, TenantRole role)> tenantRoles,
        string userId,
        string userName)
    {
        _factory = factory;
        _tenantRoles = tenantRoles;
        _userId = userId;
        _userName = userName;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // The request will create a new scope, and the middleware will populate HttpContext.Items
        // from the TestUserContext service. We need to set properties on the request that the
        // middleware can access to populate the context.

        // Store test user data in request properties so middleware can access it
        request.Properties["TestUser:TenantRoles"] = _tenantRoles;
        request.Properties["TestUser:UserId"] = _userId;
        request.Properties["TestUser:UserName"] = _userName;

        return await base.SendAsync(request, cancellationToken);
    }
}
