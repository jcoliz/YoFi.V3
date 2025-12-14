# TenantFeature Methods for Test Control API

**Status:** Implemented
**Related:** [`TEST-CONTROL-API-TENANCY-ENHANCEMENTS.md`](TEST-CONTROL-API-TENANCY-ENHANCEMENTS.md)
**Source:** [`src/Controllers/Tenancy/Features/TenantFeature.cs`](../../src/Controllers/Tenancy/Features/TenantFeature.cs)

## Overview

This document describes the additional methods added to [`TenantFeature`](../../src/Controllers/Tenancy/Features/TenantFeature.cs) to support Test Control API implementation without requiring direct access to [`ITenantRepository`](../../src/Entities/Tenancy/Providers/ITenantRepository.cs).

## New Methods

### 1. GetTenantByKeyAsync

```csharp
public async Task<Tenant?> GetTenantByKeyAsync(Guid tenantKey)
```

**Purpose:** Retrieves a tenant by its unique key without user access checks.

**Use Case:** Test Control API needs to look up workspaces by key for validation and operations.

**Returns:** The tenant if found; otherwise, null.

**Remarks:** This method bypasses user access validation and is intended for administrative or test control scenarios. For user-facing operations, use [`GetTenantForUserAsync`](../../src/Controllers/Tenancy/Features/TenantFeature.cs#L74) instead.

---

### 2. AddUserTenantRoleAsync

```csharp
public async Task AddUserTenantRoleAsync(Guid userId, long tenantId, TenantRole role)
```

**Purpose:** Assigns a role to a user for a specific tenant.

**Parameters:**
- `userId` - The unique identifier of the user to assign the role to
- `tenantId` - The tenant identifier (not the Key, the internal ID)
- `role` - The role to assign (Owner, Editor, Viewer)

**Use Case:** Test Control API endpoint "Assign User to Existing Workspace" needs to add users to workspaces with specific roles.

**Throws:** `DuplicateUserTenantRoleException` when the user already has a role in the tenant.

---

### 3. GetTenantsByNamePrefixAsync

```csharp
public async Task<IReadOnlyCollection<Tenant>> GetTenantsByNamePrefixAsync(string namePrefix)
```

**Purpose:** Retrieves all tenants whose names start with the specified prefix.

**Parameters:**
- `namePrefix` - The prefix to filter tenant names by (e.g., `"__TEST__"`)

**Use Case:** Test Control API endpoint "Delete All Test Data" needs to find all test workspaces for cleanup.

**Returns:** A collection of tenants matching the prefix.

**Remarks:** This method is useful for bulk operations on test data or administrative tasks where tenants are identified by naming conventions.

---

### 4. DeleteTenantsByKeysAsync

```csharp
public async Task DeleteTenantsByKeysAsync(IEnumerable<Guid> tenantKeys)
```

**Purpose:** Deletes multiple tenants by their unique keys.

**Parameters:**
- `tenantKeys` - The collection of tenant keys to delete

**Use Case:** Test Control API endpoint "Delete All Test Data" needs to delete all test workspaces in bulk.

**Remarks:** This method is intended for bulk cleanup operations such as removing test data. Each tenant is deleted individually without user access validation.

---

### 5. HasUserTenantRoleAsync

```csharp
public async Task<bool> HasUserTenantRoleAsync(Guid userId, long tenantId)
```

**Purpose:** Verifies if a user has any role assignment for a specific tenant.

**Parameters:**
- `userId` - The user identifier
- `tenantId` - The tenant identifier (not the Key, the internal ID)

**Use Case:** Test Control API endpoint "Seed Transactions" needs to verify the user has access to the workspace before seeding data.

**Returns:** True if the user has a role in the tenant; otherwise, false.

---

## Usage Pattern in TestController

TestController should take both `TenantFeature` and `UserManager<IdentityUser>` as dependencies:

```csharp
public class TestControlController(
    TenantFeature tenantFeature,
    UserManager<IdentityUser> userManager,
    // ... other dependencies
) : ControllerBase
{
    // ...
}
```

### Example: Create Workspace for User

```csharp
[HttpPost("users/{username}/workspaces")]
public async Task<IActionResult> CreateWorkspaceForUser(
    string username,
    [FromBody] WorkspaceCreateRequest request)
{
    // Validate test user prefix
    if (!username.StartsWith("__TEST__"))
        return Forbid("Only test users allowed");

    // Validate test workspace prefix
    if (!request.Name.StartsWith("__TEST__"))
        return Forbid("Only test workspaces allowed");

    // Look up user by username via UserManager (user management domain)
    var user = await _userManager.FindByNameAsync($"__TEST__{username}");
    if (user == null)
        return NotFound($"User {username} not found");

    var userId = Guid.Parse(user.Id);

    // Create workspace via TenantFeature
    var tenantDto = new TenantEditDto(request.Name, request.Description);
    var result = await _tenantFeature.CreateTenantForUserAsync(userId, tenantDto);

    // If role is not Owner, we need to add the specified role
    if (request.Role != "Owner")
    {
        var tenant = await _tenantFeature.GetTenantByKeyAsync(result.Key);
        if (tenant != null)
        {
            var role = Enum.Parse<TenantRole>(request.Role);
            await _tenantFeature.AddUserTenantRoleAsync(userId, tenant.Id, role);
        }
    }

    return CreatedAtAction(nameof(CreateWorkspaceForUser), result);
}
```

### Example: Assign User to Workspace

```csharp
[HttpPost("users/{username}/workspaces/{workspaceKey:guid}/assign")]
public async Task<IActionResult> AssignUserToWorkspace(
    string username,
    Guid workspaceKey,
    [FromBody] UserRoleAssignment assignment)
{
    // Validate test user prefix
    if (!username.StartsWith("__TEST__"))
        return Forbid("Only test users allowed");

    // Look up workspace via TenantFeature
    var tenant = await _tenantFeature.GetTenantByKeyAsync(workspaceKey);
    if (tenant == null)
        return NotFound($"Workspace {workspaceKey} not found");

    // Validate test workspace prefix
    if (!tenant.Name.StartsWith("__TEST__"))
        return Forbid("Only test workspaces allowed");

    // Look up user via UserManager (user management domain)
    var user = await _userManager.FindByNameAsync($"__TEST__{username}");
    if (user == null)
        return NotFound($"User {username} not found");

    var userId = Guid.Parse(user.Id);
    var role = Enum.Parse<TenantRole>(assignment.Role);

    // Assign role via TenantFeature
    try
    {
        await _tenantFeature.AddUserTenantRoleAsync(userId, tenant.Id, role);
        return NoContent();
    }
    catch (DuplicateUserTenantRoleException)
    {
        return Conflict($"User {username} already has a role in workspace {workspaceKey}");
    }
}
```

### Example: Delete All Test Data

```csharp
[HttpDelete("data")]
public async Task<IActionResult> DeleteAllTestData()
{
    // Get all test workspaces via TenantFeature
    var testTenants = await _tenantFeature.GetTenantsByNamePrefixAsync("__TEST__");
    var tenantKeys = testTenants.Select(t => t.Key);

    // Delete all test workspaces via TenantFeature
    await _tenantFeature.DeleteTenantsByKeysAsync(tenantKeys);

    // Delete all test users (existing functionality)
    await DeleteUsersAsync();

    return NoContent();
}
```

### Example: Verify User Access Before Seeding Transactions

```csharp
[HttpPost("users/{username}/workspaces/{workspaceKey:guid}/transactions/seed")]
public async Task<IActionResult> SeedTransactions(
    string username,
    Guid workspaceKey,
    [FromBody] TransactionSeedRequest request)
{
    // Validate test user prefix
    if (!username.StartsWith("__TEST__"))
        return Forbid("Only test users allowed");

    // Look up workspace via TenantFeature
    var tenant = await _tenantFeature.GetTenantByKeyAsync(workspaceKey);
    if (tenant == null)
        return NotFound($"Workspace {workspaceKey} not found");

    // Validate test workspace prefix
    if (!tenant.Name.StartsWith("__TEST__"))
        return Forbid("Only test workspaces allowed");

    // Look up user via UserManager (user management domain)
    var user = await _userManager.FindByNameAsync($"__TEST__{username}");
    if (user == null)
        return NotFound($"User {username} not found");

    var userId = Guid.Parse(user.Id);

    // Verify user has access to workspace via TenantFeature
    var hasAccess = await _tenantFeature.HasUserTenantRoleAsync(userId, tenant.Id);
    if (!hasAccess)
        return Forbid($"User {username} has no role in workspace {workspaceKey}");

    // Set tenant context and seed transactions
    // ... (implementation continues)

    return Created("", transactions);
}
```

## Benefits

✅ **Proper layering** - TestController depends on TenantFeature for tenant operations, not ITenantRepository
✅ **Consistent abstraction** - Tenant operations go through the feature layer, user operations through UserManager
✅ **Clear domain boundaries** - User management stays in its domain, tenant management in its domain
✅ **Maintainability** - Changes to repository don't directly impact TestController
✅ **Testability** - Both TenantFeature and UserManager can be mocked for unit testing TestController
✅ **Clear separation** - User-facing methods vs. administrative methods are clearly distinguished

## Related Documentation

- [Test Control API Enhancement Plan](TEST-CONTROL-API-TENANCY-ENHANCEMENTS.md)
- [Tenancy Feature Implementation Notes](TENANCY-FEATURE-IMPLEMENTATION-NOTES.md)
- [TenantFeature Source](../../src/Controllers/Tenancy/Features/TenantFeature.cs)
- [TestController Source](../../src/Controllers/TestControlController.cs)
