using Microsoft.AspNetCore.Authorization;

namespace YoFi.V3.Controllers.Tenancy.Authorization;

/// <summary>
/// Authorization requirement that allows anonymous access to tenant-scoped endpoints.
/// </summary>
/// <remarks>
/// This requirement is used for test utility endpoints that need tenant context
/// without user authentication. The handler validates that a tenant key exists
/// in the route and sets it in HttpContext.Items for downstream middleware.
///
/// <para><strong>SECURITY WARNING:</strong></para>
/// <para>
/// Only use this for test utility endpoints that have their own validation logic
/// (e.g., checking for __TEST__ prefix on usernames and tenant names).
/// </para>
/// </remarks>
public class AnonymousTenantAccessRequirement : IAuthorizationRequirement
{
    // Marker class - no properties needed
}
