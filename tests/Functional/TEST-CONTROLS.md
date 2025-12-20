# Test Control API

## Overview

The Test Control API provides endpoints for functional tests to manage test users, workspaces, and test data. All operations enforce the `__TEST__` prefix on usernames and workspace names for safety, ensuring test operations cannot affect production data.

## Architecture

**Test Control Endpoint:** [`TestControlController`](../../src/Controllers/TestControlController.cs) at `/testcontrol`

**Client Access:** [`FunctionalTestBase.testControlClient`](Infrastructure/FunctionalTestBase.cs) provides access to the API from functional tests

**Security:** All test operations require the `__TEST__` prefix on usernames and workspace names. The API returns 403 Forbidden if the prefix is missing.

## Test User Management

### User Lifecycle

Test users follow a simple lifecycle managed entirely through the Test Control API:

1. **Creation** - Users are created with auto-generated credentials including secure random passwords
2. **Auto-Approval** - Users are automatically approved (`EmailConfirmed = true`) for immediate login
3. **Usage** - Tests use the credentials to authenticate and interact with the application
4. **Cleanup** - All test users are deleted after tests complete

### Username Convention

Test usernames follow the format `__TEST__XXXX` where `XXXX` is a 4-digit hex identifier. Examples:
- `__TEST__A3F2`
- `__TEST__01BC`
- `__TEST__FF00`

### API Operations

**Create Single User:** `POST /testcontrol/users`
- Generates random username and credentials
- Returns `TestUserCredentials` with ID, username, email, and password

**Create Bulk Users:** `POST /testcontrol/users/bulk`
- Accepts array of usernames (must have `__TEST__` prefix)
- Returns array of credentials for all created users

**Delete All Test Users:** `DELETE /testcontrol/users`
- Deletes all users containing `__TEST__` in their username
- Used during test cleanup

## Workspace Management

### Workspace Lifecycle

Workspaces are created for test users with specific roles assigned:

1. **Creation** - Workspace created with `__TEST__` prefix
2. **Role Assignment** - User assigned a role (Owner, Editor, or Viewer)
3. **Data Operations** - Tests seed transactions and perform operations in the workspace
4. **Cleanup** - Workspaces deleted with cascade removal of transactions

### API Operations

**Create Workspace:** `POST /testcontrol/users/{username}/workspaces`
- Creates workspace with specified name, description, and role
- Validates both username and workspace name have `__TEST__` prefix
- Returns `TenantResultDto` with workspace details

**Assign User to Workspace:** `POST /testcontrol/users/{username}/workspaces/{workspaceKey}/assign`
- Assigns user to existing workspace with specified role
- Validates user and workspace are test entities

**Bulk Workspace Setup:** `POST /testcontrol/users/{username}/workspaces/bulk`
- Creates multiple workspaces for a user in one request
- Validates all workspace names before creating any

## Transaction Seeding

The Test Control API provides an endpoint to seed test transactions in workspaces.

**Seed Transactions:** `POST /testcontrol/users/{username}/workspaces/{tenantKey}/transactions/seed`

This endpoint uses a specialized authorization approach:

### Anonymous Tenant Access Authorization

Transaction seeding requires tenant context but runs without user authentication. The solution uses the `AllowAnonymousTenantAccess` authorization policy:

1. **Authorization Handler** - [`AnonymousTenantAccessHandler`](../../src/Controllers/Tenancy/Authorization/AnonymousTenantAccessHandler.cs) extracts the tenant key from the route and stores it in `HttpContext.Items["TenantKey"]`
2. **Tenant Context Middleware** - [`TenantContextMiddleware`](../../src/Controllers/Tenancy/Context/TenantContextMiddleware.cs) picks up the stored key and sets the tenant context
3. **Feature Injection** - `TransactionsFeature` is injected with tenant context already populated
4. **Transaction Creation** - Transactions are created normally through the feature

This approach allows unauthenticated API calls to seed test data while maintaining tenant isolation and leveraging existing transaction validation logic.

## Data Cleanup

**Delete All Test Data:** `DELETE /testcontrol/data`
- Deletes all workspaces with `__TEST__` prefix
- Deletes all users with `__TEST__` prefix
- Cascade deletes remove role assignments and transactions

## Error Testing

The Test Control API provides endpoints for testing error handling:

**List Error Codes:** `GET /testcontrol/errors`
- Returns available error codes and descriptions

**Generate Error:** `GET /testcontrol/errors/{code}`
- Generates specific HTTP errors or exceptions
- Supports various 4xx and 5xx status codes
- Can throw specific exception types to test exception handling

## Test Correlation

All functional tests include distributed tracing headers for correlation with backend logs:

- **W3C Trace Context:** `traceparent` header with trace ID and span ID
- **Custom Headers:** `X-Test-Name`, `X-Test-Id`, `X-Test-Class` for direct correlation

The [`FunctionalTestBase`](Infrastructure/FunctionalTestBase.cs) creates an `Activity` for each test and automatically adds correlation headers to all HTTP requests (both Playwright browser requests and Test Control API calls).

## Safety Features

### Prefix Enforcement

All operations validate the `__TEST__` prefix:
- User operations require username starts with `__TEST__`
- Workspace operations require workspace name starts with `__TEST__`
- API returns 403 Forbidden if prefix is missing

### Isolation

Test operations are isolated from production data:
- Test users have unique prefix-based usernames
- Test workspaces have unique prefix-based names
- Tenant context ensures data operations are scoped to specific workspaces

### Cleanup

Test data is ephemeral:
- Tests clean up after themselves via `DELETE /testcontrol/data`
- Cascade deletes ensure no orphaned data
- Test users and workspaces can be identified and removed easily

## Usage Example

Typical test flow in functional tests:

```csharp
[Test]
public async Task ExampleTest()
{
    // Given: A test user is created
    var user = await testControlClient.CreateUserAsync();

    // And: User has a workspace with Owner role
    var workspace = await testControlClient.CreateWorkspaceForUserAsync(
        user.Username,
        new("__TEST__MyWorkspace", "Test workspace", "Owner")
    );

    // And: Workspace has test transactions
    await testControlClient.SeedTransactionsAsync(
        user.Username,
        workspace.Key,
        new(Count: 10)
    );

    // When: Test interacts with the application
    // ... test steps ...

    // Then: Test assertions
    // ... assertions ...

    // Cleanup happens automatically in base class teardown
}
```

## Implementation Files

- **Controller:** [`src/Controllers/TestControlController.cs`](../../src/Controllers/TestControlController.cs)
- **Client:** Generated via NSwag from controller (auto-imported in functional tests)
- **Base Class:** [`tests/Functional/Infrastructure/FunctionalTestBase.cs`](Infrastructure/FunctionalTestBase.cs)
- **Authorization Policy:** [`src/Controllers/Tenancy/Authorization/AnonymousTenantAccessHandler.cs`](../../src/Controllers/Tenancy/Authorization/AnonymousTenantAccessHandler.cs)
