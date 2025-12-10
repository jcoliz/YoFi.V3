using Microsoft.Extensions.DependencyInjection;
using YoFi.V3.Entities.Tenancy;

namespace YoFi.V3.Tests.Integration.Controller.TestHelpers;

/// <summary>
/// Delegating handler that populates the scoped TestUserContext before each request
/// The middleware in BaseTestWebApplicationFactory will then transfer this to HttpContext.Items
/// </summary>
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
