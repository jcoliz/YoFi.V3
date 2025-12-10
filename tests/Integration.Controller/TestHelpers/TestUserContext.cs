using YoFi.V3.Entities.Tenancy;

namespace YoFi.V3.Tests.Integration.Controller.TestHelpers;

/// <summary>
/// Scoped service to hold test user context for the current request
/// Populated by TestUserScopedHandler, consumed by middleware to populate HttpContext.Items
/// </summary>
public class TestUserContext
{
    public List<(Guid tenantKey, TenantRole role)> TenantRoles { get; set; } = new();
    public string UserId { get; set; } = "test-user-id";
    public string UserName { get; set; } = "test-user";
}
