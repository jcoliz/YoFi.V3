using YoFi.V3.Entities.Tenancy;

namespace YoFi.V3.Tests.Integration.Controller.TestHelpers;

/// <summary>
/// Delegating handler that injects test user context into HttpContext.Items
/// for TestAuthenticationHandler to consume
/// </summary>
public class TestUserDelegatingHandler : DelegatingHandler
{
    private readonly List<(Guid tenantKey, TenantRole role)> _tenantRoles;
    private readonly string _userId;
    private readonly string _userName;

    public TestUserDelegatingHandler(
        List<(Guid tenantKey, TenantRole role)> tenantRoles,
        string userId,
        string userName)
    {
        _tenantRoles = tenantRoles;
        _userId = userId;
        _userName = userName;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Store in request options (will be available in HttpContext)
        request.Options.TryAdd("TestUser:TenantRoles", _tenantRoles);
        request.Options.TryAdd("TestUser:UserId", _userId);
        request.Options.TryAdd("TestUser:UserName", _userName);

        return await base.SendAsync(request, cancellationToken);
    }
}
