# Test Helpers

This directory contains reusable test infrastructure including test authentication, base test classes, and custom factories for integration testing.

## Architecture Overview

The test authentication system replaces production authentication (ASP.NET Identity + NuxtIdentity JWT) with a programmatically-controlled test scheme while maintaining identical authorization behavior.

### Component Flow

```
Test Code
    ↓ CreateAuthenticatedClient(tenantKey, role)
BaseTestWebApplicationFactory
    ↓ Creates TestUserInjectingHandler with tenant roles
HTTP Request
    ↓ Headers: X-Test-User-Id, X-Test-User-Name, X-Test-Tenant-Roles
TestAuthenticationHandler
    ↓ Reads headers, creates ClaimsPrincipal with tenant_role claims
ASP.NET Core Authorization
    ↓ Invokes TenantRoleHandler
TenantRoleHandler
    ↓ Validates user has required tenant role
Authorized Request → Controller
```

## Core Components

### [`BaseTestWebApplicationFactory`](BaseTestWebApplicationFactory.cs)

**Purpose**: Factory that configures the test application with test authentication.

**Key Features**:
- Replaces production auth with `TestAuthenticationHandler` as default scheme
- Provides `CreateAuthenticatedClient()` methods for programmatic authentication
- Manages temporary SQLite database (auto-created on construction, auto-deleted on disposal)
- Supports configuration overrides via constructor

**Usage**:
```csharp
var factory = new BaseTestWebApplicationFactory();
var client = factory.CreateAuthenticatedClient(tenantKey, TenantRole.Editor);
```

### [`TestAuthenticationHandler`](TestAuthenticationHandler.cs)

**Purpose**: Custom ASP.NET Core authentication handler that creates authenticated users from HTTP headers.

**How It Works**:
1. Registered as the default authentication scheme (`TestScheme`)
2. Reads test user data from custom headers:
   - `X-Test-User-Id` → `ClaimTypes.NameIdentifier` claim
   - `X-Test-User-Name` → `ClaimTypes.Name` claim
   - `X-Test-Tenant-Roles` → `tenant_role` claims (format: "tenantGuid:RoleName,...")
3. Creates `ClaimsPrincipal` with appropriate claims
4. Authorization handlers (like `TenantRoleHandler`) validate claims normally

**Header Format Example**:
```
X-Test-User-Id: test-user-123
X-Test-User-Name: Test User
X-Test-Tenant-Roles: 123e4567-e89b-12d3-a456-426614174000:Editor,789abcde-f012-34g5-h678-901234567890:Viewer
```

### [`AuthenticatedTestBase`](AuthenticatedTestBase.cs)

**Purpose**: Base class for integration tests requiring authenticated access.

**Default Behavior**:
- Creates factory and authenticated client in `OneTimeSetUp`
- Default role: **Editor** (most common permission level)
- Creates test tenant automatically
- Provides helper methods for role switching and multi-tenant testing

**Role Switching**:
```csharp
SwitchToViewer();  // Read-only access
SwitchToEditor();  // Read/write access (default)
SwitchToOwner();   // Full control (use explicitly)
```

**Multi-Tenant Support**:
```csharp
var client = CreateMultiTenantClient(
    (tenant1Key, TenantRole.Editor),
    (tenant2Key, TenantRole.Viewer)
);
```

### `TestUserInjectingHandler` (private nested class)

**Purpose**: Delegating handler that adds test user data as HTTP headers.

**Why Headers?**
- `HttpClient` delegating handlers can't directly access `HttpContext.Items`
- Headers provide reliable data transport through the HTTP pipeline
- Simple, standard HTTP mechanism

**Location**: Private nested class in [`BaseTestWebApplicationFactory`](BaseTestWebApplicationFactory.cs#L142)

## Design Decisions

### Why HTTP Headers?

**Simple and Reliable**:
- Works naturally with `HttpClient` delegating handlers
- Standard HTTP mechanism for passing data through the pipeline
- No complex middleware registration required

### Why Editor as Default Role?

- **Principle of least privilege** - Tests need write access but not full ownership
- **Most common scenario** - Majority of tests manipulate data (read/write)
- **Explicit Owner tests** - `SwitchToOwner()` makes privileged operations obvious

### Why TestAuthenticationHandler Instead of Mocking?

- **Real authorization flow** - Tests actual `TenantRoleHandler` logic
- **No mocking complexity** - Uses ASP.NET Core's built-in auth system
- **Production parity** - Authorization works identically in tests and production

## Related Documentation

- [Main Testing README](../README.md) - Overview and quick start
- [Testing Guide](../TESTING-GUIDE.md) - Implementation patterns and examples
- [Project Rules](../../../.roorules) - Gherkin-style test documentation
