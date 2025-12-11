# Tenancy Infrastructure

This directory contains the multi-tenancy infrastructure for YoFi.V3, providing tenant-scoped authorization, context management, and API operations.

## Overview

The tenancy system enables multiple users to have isolated data spaces (tenants) within a single application instance. Users can belong to multiple tenants with different roles (Owner, Editor, Viewer) in each.

## Directory Structure

```
Tenancy/
├── Api/                              # API Layer - Controllers and DTOs
│   ├── TenantController.cs          # RESTful endpoints for tenant management
│   └── Dto/                          # Data Transfer Objects
│       ├── TenantEditDto.cs         # Input DTO for create/update operations
│       ├── TenantResultDto.cs       # Output DTO for tenant data
│       └── TenantRoleResultDto.cs   # Output DTO with role information
│
├── Authorization/                    # Authorization Components
│   ├── RequireTenantRoleAttribute.cs # Declarative authorization attribute
│   ├── TenantRoleHandler.cs         # Authorization handler logic
│   ├── TenantRoleRequirement.cs     # Authorization requirement definition
│   └── TenantUserClaimsService.cs   # Claims provider for JWT tokens
│
├── Context/                          # Request Context Management
│   ├── TenantContext.cs             # Tenant context state holder
│   └── TenantContextMiddleware.cs   # Middleware to initialize context
│
├── Features/                         # Business Logic Layer
│   └── TenantFeature.cs             # Tenant operations orchestration
│
├── Exceptions/                       # Exception Handling
│   └── TenancyExceptionHandler.cs   # HTTP exception mapping
│
├── ServiceCollectionExtensions.cs   # Dependency injection configuration
└── README.md                         # This file
```

## How It Works

### 1. Authentication & Claims
When a user authenticates, [`TenantUserClaimsService`](Authorization/TenantUserClaimsService.cs) adds custom claims to their JWT token representing their tenant roles:
```
tenant_role: "{tenantKey}:{role}"
```

### 2. Authorization
For tenant-scoped endpoints (e.g., `/api/tenant/{tenantKey}/transactions`):
- [`RequireTenantRoleAttribute`](Authorization/RequireTenantRoleAttribute.cs) is applied to controllers/actions
- [`TenantRoleHandler`](Authorization/TenantRoleHandler.cs) validates the user has the required role claim
- If authorized, the tenant key is stored in `HttpContext.Items["TenantKey"]`

### 3. Context Initialization
[`TenantContextMiddleware`](Context/TenantContextMiddleware.cs) runs after authorization and:
- Retrieves the tenant key from `HttpContext.Items`
- Loads the full tenant entity from the database
- Stores it in [`TenantContext`](Context/TenantContext.cs) for use by features/controllers

### 4. Business Logic
[`TenantFeature`](Features/TenantFeature.cs) provides tenant management operations:
- Create new tenants (user becomes owner)
- Retrieve user's tenants with role information
- Update/delete tenants (owner only)

### 5. API Layer
[`TenantController`](Api/TenantController.cs) provides RESTful endpoints:
- `GET /api/tenant` - List user's tenants
- `GET /api/tenant/{key}` - Get specific tenant
- `POST /api/tenant` - Create new tenant
- `PUT /api/tenant/{tenantKey}` - Update tenant (owner only)
- `DELETE /api/tenant/{tenantKey}` - Delete tenant (owner only)

## Integration

To add tenancy support to your application:

```csharp
// In Program.cs or Startup.cs

// 1. Add services
services.AddTenancy();

// 2. Add middleware (AFTER authentication/authorization)
app.UseAuthentication();
app.UseAuthorization();
app.UseTenancy();  // <-- Add this
app.MapControllers();
```

## Usage in Controllers

For tenant-scoped resources:

```csharp
[Route("api/tenant/{tenantKey}/[controller]")]
[ApiController]
public class MyResourceController : ControllerBase
{
    private readonly ITenantProvider _tenantProvider;

    public MyResourceController(ITenantProvider tenantProvider)
    {
        _tenantProvider = tenantProvider;
    }

    [HttpGet]
    [RequireTenantRole(TenantRole.Viewer)]  // Minimum role required
    public async Task<IActionResult> GetResources()
    {
        // Access current tenant
        var tenant = _tenantProvider.CurrentTenant;

        // Your logic here - automatically scoped to tenant
    }
}
```

## Role Hierarchy

Roles have a natural hierarchy where higher roles inherit lower role permissions:

- **Owner** ≥ Editor ≥ Viewer
- An Owner can do everything an Editor can
- An Editor can do everything a Viewer can

When using `[RequireTenantRole(TenantRole.Editor)]`, users with Editor OR Owner roles can access the endpoint.

## Future Plans

**⚠️ IMPORTANT: These tenancy files are intended to be extracted into a separate, reusable project.**

The current structure facilitates this future separation:
- Clear separation of concerns (API, Authorization, Context, Features)
- Minimal coupling to YoFi.V3-specific code
- Well-defined interfaces and extension points

Once extracted, this will become a standalone package that can be:
- Reused across multiple projects
- Versioned independently
- Tested in isolation
- Maintained as a separate concern

## Related Documentation

- [TENANCY.md](../../../docs/TENANCY.md) - High-level tenancy architecture and design decisions
- [ADR-0009: Accounts and Tenancy](../../../docs/adr/0009-accounts-and-tenancy.md) - Architecture decision record
- [Tenancy Restructure Plan](../../../docs/wip/TENANCY-DIRECTORY-RESTRUCTURE.md) - Details on the current directory structure

## Testing

See [`tests/Integration.Controller/`](../../../tests/Integration.Controller/):
- [`TenantControllerTests.cs`](../../../tests/Integration.Controller/TenantControllerTests.cs) - API endpoint tests
- [`TenantContextMiddlewareTests.cs`](../../../tests/Integration.Controller/TenantContextMiddlewareTests.cs) - Middleware behavior tests
- [`EndToEndAuthenticationTests.cs`](../../../tests/Integration.Controller/EndToEndAuthenticationTests.cs) - Full authentication flow tests
