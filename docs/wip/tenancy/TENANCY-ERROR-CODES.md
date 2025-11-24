# HTTP Error Codes for Tenant Access Control

## The Question: 403 vs 404?

When a user tries to access a tenant they don't have permission for, should we return:
- **403 Forbidden** - "You don't have permission"
- **404 Not Found** - "This doesn't exist"

## The Security Consideration

**Your instinct is correct**: 404 is generally better to **hide tenant existence** from unauthorized users.

### Information Disclosure Attack

```http
# Attacker enumerates tenant IDs
GET /api/tenant/00000000-0000-0000-0000-000000000001/transactions
GET /api/tenant/00000000-0000-0000-0000-000000000002/transactions
GET /api/tenant/00000000-0000-0000-0000-000000000003/transactions
...
```

**With 403 responses:**
```
403 Forbidden - Tenant exists, but you can't access it ✓ Confirmed tenant exists
404 Not Found - Tenant doesn't exist (or does it?)
```

**With 404 responses:**
```
404 Not Found - Could be non-existent or unauthorized (ambiguous) ✓ Better
404 Not Found - Could be non-existent or unauthorized (ambiguous)
```

## Recommendation: Context-Dependent

The answer depends on **who the user is and what they're trying to access**.

### Scenario 1: Anonymous/Unauthenticated User
**Return**: **404 Not Found**

```http
GET /api/tenant/abc123/transactions
Authorization: (none)

→ 404 Not Found
```

**Rationale**: Don't reveal tenant existence to anonymous users.

### Scenario 2: Authenticated User, No Access to Tenant
**Return**: **404 Not Found** (recommended) OR **403 Forbidden** (acceptable)

```http
GET /api/tenant/abc123/transactions
Authorization: Bearer <valid-token-for-different-tenant>

→ 404 Not Found (preferred)
→ 403 Forbidden (acceptable)
```

**Preference**: **404** to prevent tenant enumeration by authenticated users.

### Scenario 3: Authenticated User, Has Access but Insufficient Role
**Return**: **403 Forbidden**

```http
DELETE /api/tenant/abc123/transactions/xyz
Authorization: Bearer <token-with-viewer-role>

→ 403 Forbidden
{
  "error": "InsufficientPermissions",
  "message": "This operation requires Editor or Owner role. You have Viewer role.",
  "requiredRole": "Editor",
  "yourRole": "Viewer"
}
```

**Rationale**: User knows the tenant exists (they have access), so be explicit about the permission issue.

### Scenario 4: Tenant Exists but Is Inactive/Deleted
**Return**: **404 Not Found**

```http
GET /api/tenant/abc123/transactions
# Where tenant abc123 has IsActive = false

→ 404 Not Found
```

**Rationale**: Treat inactive tenants as non-existent for all purposes.

## Implementation Strategy

### Recommended Approach: Differentiate by Context

```csharp
public class TenantRoleHandler : AuthorizationHandler<TenantRoleRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantRoleRequirement requirement)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var tenantId = httpContext?.Request.RouteValues["tenantId"]?.ToString();

        if (string.IsNullOrEmpty(tenantId))
        {
            // No tenant ID in route - let it fail naturally (400/404)
            return Task.CompletedTask;
        }

        // Find user's claim for this specific tenant
        var tenantRoleClaim = context.User.FindFirst(c =>
            c.Type == "tenant_role" &&
            c.Value.StartsWith($"{tenantId}:"));

        if (tenantRoleClaim == null)
        {
            // User has NO access to this tenant at all
            // Fail authorization → will result in 403
            // But we'll convert to 404 in middleware (see below)
            context.Fail(new AuthorizationFailureReason(this, "NoTenantAccess"));
            return Task.CompletedTask;
        }

        // User HAS access - check role level
        var parts = tenantRoleClaim.Value.Split(':');
        if (parts.Length == 2 && Enum.TryParse<TenantRole>(parts[1], out var userRole))
        {
            if (userRole >= requirement.MinimumRole)
            {
                context.Succeed(requirement);
            }
            else
            {
                // User has access but insufficient role
                // Fail with clear reason → stays as 403
                context.Fail(new AuthorizationFailureReason(this,
                    $"InsufficientRole: Required={requirement.MinimumRole}, Actual={userRole}"));
            }
        }

        return Task.CompletedTask;
    }
}
```

### Custom Middleware to Convert 403 → 404

```csharp
public class TenantAuthorizationMiddleware
{
    private readonly RequestDelegate _next;

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        // After the request is processed
        if (context.Response.StatusCode == 403)
        {
            // Check if this was a "NoTenantAccess" failure
            var endpoint = context.GetEndpoint();
            var authData = endpoint?.Metadata.GetMetadata<IAuthorizeData>();

            if (authData != null && context.Request.Path.StartsWithSegments("/api/tenant"))
            {
                // Check authorization result
                var authService = context.RequestServices.GetRequiredService<IAuthorizationService>();
                var user = context.User;

                // If user has NO access to tenant (not just insufficient role)
                // convert 403 → 404 to hide tenant existence
                var tenantId = context.Request.RouteValues["tenantId"]?.ToString();
                if (!string.IsNullOrEmpty(tenantId))
                {
                    var hasTenantAccess = user.FindFirst(c =>
                        c.Type == "tenant_role" &&
                        c.Value.StartsWith($"{tenantId}:")) != null;

                    if (!hasTenantAccess)
                    {
                        context.Response.StatusCode = 404;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsJsonAsync(new
                        {
                            error = "NotFound",
                            message = "The requested resource was not found."
                        });
                    }
                }
            }
        }
    }
}
```

### Simpler Approach: Always Return 404 for No Access

If the above is too complex, use a simpler rule:

```csharp
public class TenantAccessMiddleware
{
    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        // Convert all 403s on tenant endpoints to 404s
        if (context.Response.StatusCode == 403 &&
            context.Request.Path.StartsWithSegments("/api/tenant"))
        {
            context.Response.StatusCode = 404;
        }
    }
}
```

**Downside**: Less helpful error messages for legitimate users with insufficient roles.

## My Recommendation: Hybrid Approach

### Default Strategy: Security-First

1. **No tenant access** → **404 Not Found**
   - Hides tenant existence
   - Prevents enumeration

2. **Has access, insufficient role** → **403 Forbidden** with detailed message
   - User already knows tenant exists
   - Clear feedback helps legitimate users

3. **Invalid tenant ID format** → **400 Bad Request**
   - Not a security issue
   - Invalid GUID format

### Implementation

```csharp
public enum TenantAuthorizationFailureReason
{
    NoAccess,           // User not in tenant → 404
    InsufficientRole,   // User in tenant, wrong role → 403
    InvalidTenantId     // Bad GUID → 400
}

// Store failure reason in HttpContext.Items
public class TenantRoleHandler : AuthorizationHandler<TenantRoleRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantRoleRequirement requirement)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var tenantId = httpContext?.Request.RouteValues["tenantId"]?.ToString();

        // Check if tenant ID is valid GUID
        if (!Guid.TryParse(tenantId, out var tenantGuid))
        {
            httpContext.Items["TenantAuthFailure"] = TenantAuthorizationFailureReason.InvalidTenantId;
            return Task.CompletedTask;
        }

        var tenantRoleClaim = context.User.FindFirst(c =>
            c.Type == "tenant_role" &&
            c.Value.StartsWith($"{tenantId}:"));

        if (tenantRoleClaim == null)
        {
            httpContext.Items["TenantAuthFailure"] = TenantAuthorizationFailureReason.NoAccess;
            return Task.CompletedTask;
        }

        var parts = tenantRoleClaim.Value.Split(':');
        if (parts.Length == 2 && Enum.TryParse<TenantRole>(parts[1], out var userRole))
        {
            if (userRole >= requirement.MinimumRole)
            {
                context.Succeed(requirement);
            }
            else
            {
                httpContext.Items["TenantAuthFailure"] = TenantAuthorizationFailureReason.InsufficientRole;
                httpContext.Items["TenantRequiredRole"] = requirement.MinimumRole;
                httpContext.Items["TenantActualRole"] = userRole;
            }
        }

        return Task.CompletedTask;
    }
}

// Middleware to convert status codes based on failure reason
public class TenantAuthorizationResponseMiddleware
{
    private readonly RequestDelegate _next;

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        if (context.Response.StatusCode == 403 &&
            context.Request.Path.StartsWithSegments("/api/tenant"))
        {
            var failureReason = context.Items["TenantAuthFailure"] as TenantAuthorizationFailureReason?;

            switch (failureReason)
            {
                case TenantAuthorizationFailureReason.NoAccess:
                    context.Response.StatusCode = 404;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "NotFound",
                        message = "The requested resource was not found."
                    });
                    break;

                case TenantAuthorizationFailureReason.InsufficientRole:
                    // Keep 403, provide helpful message
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "InsufficientPermissions",
                        message = $"This operation requires {context.Items["TenantRequiredRole"]} role. You have {context.Items["TenantActualRole"]} role.",
                        requiredRole = context.Items["TenantRequiredRole"]?.ToString(),
                        yourRole = context.Items["TenantActualRole"]?.ToString()
                    });
                    break;

                case TenantAuthorizationFailureReason.InvalidTenantId:
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = "BadRequest",
                        message = "Invalid tenant ID format."
                    });
                    break;
            }
        }
    }
}
```

## Summary

### Quick Answer: **Return 404 for unauthorized access**

**Why?**
- ✅ Prevents tenant enumeration
- ✅ Hides tenant existence from unauthorized users
- ✅ Better security posture
- ✅ No information leakage

**When to use 403 instead?**
- User HAS access to tenant but insufficient role
- User should know they're in the tenant but need higher permissions

### Response Code Matrix

| Situation | HTTP Status | Response Body |
|-----------|-------------|---------------|
| Anonymous user tries to access tenant | **404** | Generic "not found" |
| Authenticated user, no access to tenant | **404** | Generic "not found" |
| User has Viewer, tries Owner action | **403** | "Requires Owner role. You have Viewer." |
| Invalid tenant GUID format | **400** | "Invalid tenant ID format" |
| Tenant exists but IsActive=false | **404** | Generic "not found" |

**This provides security by default while still helping legitimate users understand permission issues.**
