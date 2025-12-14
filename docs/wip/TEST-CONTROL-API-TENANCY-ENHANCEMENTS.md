# Test Control API Enhancement Plan for Tenancy Feature Testing

**Status:** Planning
**Related:** [`tests/Functional/Features/Tenancy.feature`](../../tests/Functional/Features/Tenancy.feature)
**Current API:** [`src/Controllers/TestControlController.cs`](../../src/Controllers/TestControlController.cs)

## Overview

This document outlines required enhancements to the Test Control API to support complex multi-user, multi-workspace test scenarios in functional tests. The current Test Control API only supports single-user operations, but the Tenancy feature tests require creating multiple users with different workspace access levels and pre-seeding test data.

## Current Test Control API Capabilities

✅ **Already Implemented:**
- `POST /TestControl/users` - Create single test user
- `DELETE /TestControl/users` - Delete all test users
- `PUT /TestControl/users/{username}/approve` - Approve user (stub implementation)

## Required New Endpoints

### 1. Bulk User Creation

**Problem:** Background section in [`Tenancy.feature:13-17`](../../tests/Functional/Features/Tenancy.feature#L13-L17) needs to create multiple named users (alice, bob, charlie).

**Endpoint:**
```csharp
// POST /TestControl/users/bulk
[HttpPost("users/bulk")]
[ProducesResponseType(typeof(IReadOnlyCollection<TestUser>), StatusCodes.Status201Created)]
public async Task<IActionResult> CreateBulkUsers([FromBody] string[] usernames)
```

**Request Body:**
```json
["alice", "bob", "charlie"]
```

**Response:**
```json
[
  { "Id": 1, "Username": "__TEST__alice", "Email": "__TEST__alice@test.com", "Password": "..." },
  { "Id": 2, "Username": "__TEST__bob", "Email": "__TEST__bob@test.com", "Password": "..." },
  { "Id": 3, "Username": "__TEST__charlie", "Email": "__TEST__charlie@test.com", "Password": "..." }
]
```

**Implementation Notes:**
- Prefix usernames with `__TEST__` for consistency with existing pattern
- Generate secure random passwords for each user
- Return created users with credentials for later login
- Users are automatically approved (no email confirmation required for tests)

---

### 2. Create Workspace for User

**Problem:** Many scenarios need pre-seeded workspaces with specific names and roles ([lines 49-53](../../tests/Functional/Features/Tenancy.feature#L49-L53), [78](../../tests/Functional/Features/Tenancy.feature#L78), [87](../../tests/Functional/Features/Tenancy.feature#L87), [93](../../tests/Functional/Features/Tenancy.feature#L93), etc.).

**Endpoint:**
```csharp
// POST /TestControl/users/{username}/workspaces
[HttpPost("users/{username}/workspaces")]
[ProducesResponseType(typeof(TenantResultDto), StatusCodes.Status201Created)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
public async Task<IActionResult> CreateWorkspaceForUser(
    string username,
    [FromBody] WorkspaceCreateRequest request)

public record WorkspaceCreateRequest(string Name, string Description, string Role = "Owner");
```

**Request Body:**
```json
{
  "Name": "Personal",
  "Description": "My personal workspace",
  "Role": "Owner"
}
```

**Response:**
```json
{
  "Key": "abc-123-def-456-789",
  "Name": "Personal",
  "Description": "My personal workspace",
  "CreatedAt": "2024-12-14T06:00:00Z"
}
```

**Implementation Notes:**
- Looks up user by `__TEST__{username}` format
- Calls [`TenantFeature.CreateTenantForUserAsync()`](../../src/Controllers/Tenancy/Features/TenantFeature.cs#L22)
- Sets specified role via [`ITenantRepository.AddUserTenantRoleAsync()`](../../src/Entities/Tenancy/Providers/ITenantRepository.cs#L51)
- Returns 404 if user not found

---

### 3. Assign User to Existing Workspace

**Problem:** Scenarios need users with different roles in the same workspace ([lines 49-53](../../tests/Functional/Features/Tenancy.feature#L49-L53): alice has Owner/Editor/Viewer roles across different workspaces).

**Endpoint:**
```csharp
// POST /TestControl/workspaces/{workspaceKey}/users
[HttpPost("workspaces/{workspaceKey:guid}/users")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
public async Task<IActionResult> AssignUserToWorkspace(
    Guid workspaceKey,
    [FromBody] UserRoleAssignment assignment)

public record UserRoleAssignment(string Username, string Role);
```

**Request Body:**
```json
{
  "Username": "alice",
  "Role": "Editor"
}
```

**Response:** 204 No Content on success

**Implementation Notes:**
- Looks up workspace by Key via [`ITenantRepository.GetTenantByKeyAsync()`](../../src/Entities/Tenancy/Providers/ITenantRepository.cs#L74)
- Looks up user by `__TEST__{username}`
- Calls [`ITenantRepository.AddUserTenantRoleAsync()`](../../src/Entities/Tenancy/Providers/ITenantRepository.cs#L51) with specified role
- Returns 404 if workspace or user not found
- Returns 409 if user already has a role in that workspace

---

### 4. Seed Transactions for Workspace

**Problem:** [Lines 106-107](../../tests/Functional/Features/Tenancy.feature#L106-L107) need specific transaction counts in specific workspaces.

**Endpoint:**
```csharp
// POST /TestControl/workspaces/{workspaceKey}/transactions/seed
[HttpPost("workspaces/{workspaceKey:guid}/transactions/seed")]
[ProducesResponseType(typeof(IReadOnlyCollection<TransactionResultDto>), StatusCodes.Status201Created)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
public async Task<IActionResult> SeedTransactions(
    Guid workspaceKey,
    [FromBody] TransactionSeedRequest request)

public record TransactionSeedRequest(int Count, string PayeePrefix = "Test Transaction");
```

**Request Body:**
```json
{
  "Count": 5,
  "PayeePrefix": "Personal Expense"
}
```

**Response:**
```json
[
  {
    "Key": "trans-1-key",
    "Date": "2024-11-14T00:00:00Z",
    "Payee": "Personal Expense 1",
    "Amount": 125.50
  },
  {
    "Key": "trans-2-key",
    "Date": "2024-11-20T00:00:00Z",
    "Payee": "Personal Expense 2",
    "Amount": 87.25
  }
  // ... 3 more
]
```

**Implementation Notes:**
- Looks up workspace via [`ITenantRepository.GetTenantByKeyAsync()`](../../src/Entities/Tenancy/Providers/ITenantRepository.cs#L74)
- Sets [`TenantContext`](../../src/Controllers/Tenancy/Context/TenantContext.cs) to specified workspace
- Creates `Count` transactions with auto-generated realistic data:
  - **Payee format:** `"{PayeePrefix} {i}"` (e.g., "Personal Expense 1", "Personal Expense 2")
  - **Amount:** Random between $10.00 and $500.00
  - **Date:** Distributed over last 30 days
- Uses [`TransactionsFeature.AddTransactionAsync()`](../../src/Application/Features/TransactionsFeature.cs) for each transaction
- Returns 404 if workspace not found

---

### 5. Get User Credentials

**Problem:** Steps need to log in as specific users created in Background.

**Endpoint:**
```csharp
// GET /TestControl/users/{username}/credentials
[HttpGet("users/{username}/credentials")]
[ProducesResponseType(typeof(TestUser), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
public IActionResult GetUserCredentials(string username)
```

**Response:**
```json
{
  "Id": 1,
  "Username": "__TEST__alice",
  "Email": "__TEST__alice@test.com",
  "Password": "SecurePass123!"
}
```

**Implementation Notes:**
- Looks up test user from in-memory cache or database
- Returns credentials for test user created via bulk endpoint
- Required for "Given I am logged in as {username}" step implementations
- Returns 404 if user not found
- **Security Note:** Only works for users with `__TEST__` prefix

---

### 6. Delete All Test Data

**Problem:** Need clean slate between test runs, including workspaces and transactions.

**Endpoint:**
```csharp
// DELETE /TestControl/data
[HttpDelete("data")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
public async Task<IActionResult> DeleteAllTestData()
```

**Response:** 204 No Content

**Implementation Notes:**
- Deletes all workspaces where owner has `__TEST__` prefix in username
- Deletes all test users (leverages existing [`DeleteUsersAsync()`](../../src/Controllers/TestControlController.cs#L109) functionality)
- Relies on cascade delete configuration to remove:
  - User-tenant role assignments
  - Transactions belonging to test workspaces
- More comprehensive than just deleting users

---

## Helper Endpoint for Complex Scenarios

### 7. Bulk Workspace Setup

**Problem:** [Lines 49-56](../../tests/Functional/Features/Tenancy.feature#L49-L56) need one user with access to 3 workspaces with different roles.

**Endpoint:**
```csharp
// POST /TestControl/users/{username}/workspaces/bulk
[HttpPost("users/{username}/workspaces/bulk")]
[ProducesResponseType(typeof(IReadOnlyCollection<WorkspaceSetupResult>), StatusCodes.Status201Created)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
public async Task<IActionResult> BulkWorkspaceSetup(
    string username,
    [FromBody] WorkspaceSetupRequest[] workspaces)

public record WorkspaceSetupRequest(string Name, string Description, string Role);
public record WorkspaceSetupResult(Guid Key, string Name, string Role);
```

**Request Body:**
```json
[
  { "Name": "Personal", "Description": "My workspace", "Role": "Owner" },
  { "Name": "Family Budget", "Description": "Shared workspace", "Role": "Editor" },
  { "Name": "Tax Records", "Description": "Read-only access", "Role": "Viewer" }
]
```

**Response:**
```json
[
  { "Key": "abc-123-def", "Name": "Personal", "Role": "Owner" },
  { "Key": "xyz-789-ghi", "Name": "Family Budget", "Role": "Editor" },
  { "Key": "mno-456-pqr", "Name": "Tax Records", "Role": "Viewer" }
]
```

**Implementation Notes:**
- Convenience endpoint that combines multiple operations
- For "Owner" role: Creates workspace with user as owner
- For other roles: Creates workspace owned by a system user, then assigns specified role to target user
- Returns keys for all created workspaces
- Transactional: Either all succeed or all fail (rollback on error)

---

## Updated API Client Generation

After implementing these endpoints, regenerate the functional test API client:

```powershell
# 1. Start the backend with Test Control API
cd src/WireApiHost
dotnet run

# 2. Regenerate the API client
# (NSwag automatically detects new endpoints)
cd ../../tests/Functional
# API client regenerates automatically via nswag.json configuration
```

The generated [`ApiClient.cs`](../../tests/Functional/Api/ApiClient.cs) will include:
- `ITestControlClient.CreateBulkUsersAsync(string[] usernames)`
- `ITestControlClient.CreateWorkspaceForUserAsync(string username, WorkspaceCreateRequest request)`
- `ITestControlClient.AssignUserToWorkspaceAsync(Guid workspaceKey, UserRoleAssignment assignment)`
- `ITestControlClient.SeedTransactionsAsync(Guid workspaceKey, TransactionSeedRequest request)`
- `ITestControlClient.GetUserCredentialsAsync(string username)`
- `ITestControlClient.DeleteAllTestDataAsync()`
- `ITestControlClient.BulkWorkspaceSetupAsync(string username, WorkspaceSetupRequest[] workspaces)`

---

## Implementation Priority

### Phase 1: Essential Infrastructure (Blocking)
1. **Bulk user creation** (#1) - Required by Background section
2. **Get user credentials** (#5) - Required for login steps
3. **Create workspace for user** (#2) - Required for basic workspace scenarios
4. **Delete all test data** (#6) - Required for test isolation

### Phase 2: Multi-User Scenarios
5. **Assign user to workspace** (#3) - Required for shared workspace tests
6. **Bulk workspace setup** (#7) - Convenience for complex setups

### Phase 3: Data Seeding
7. **Seed transactions** (#4) - Required for data isolation tests

---

## Usage Example in Test Steps

**Before (Complex UI automation):**
```csharp
// Given I have access to these workspaces:
protected async Task GivenIHaveAccessToTheseWorkspaces(DataTable workspaces)
{
    foreach (var row in workspaces.Rows)
    {
        // Navigate to workspace creation page
        // Fill in forms
        // Submit
        // Wait for success
        // Navigate to role management
        // Assign role
        // etc...
    }
}
```

**After (Simple API calls):**
```csharp
// Given I have access to these workspaces:
protected async Task GivenIHaveAccessToTheseWorkspaces(DataTable workspaces)
{
    var username = _currentUser.Username.Replace("__TEST__", "");
    var requests = workspaces.Rows.Select(row => new WorkspaceSetupRequest(
        Name: row["Workspace Name"],
        Description: $"Test workspace for {username}",
        Role: row["My Role"]
    )).ToArray();

    var createdWorkspaces = await _testControlClient.BulkWorkspaceSetupAsync(username, requests);
    _objectStore.Add("CreatedWorkspaces", createdWorkspaces);
}
```

---

## Benefits

✅ **Simplified test setup** - Single API calls instead of complex UI automation
✅ **Faster test execution** - Direct data creation vs. UI interactions (10x+ faster)
✅ **More reliable tests** - Consistent test data state, no UI flakiness
✅ **Reusable infrastructure** - Same endpoints work for all tenancy-related functional tests
✅ **Easier maintenance** - Changes to data structure only affect Test Control API
✅ **Better debugging** - Can recreate exact test state via API calls
✅ **Test isolation** - Clean slate for every test run

---

## Security Considerations

1. **Test-only restriction** - All endpoints only work with users/data containing `__TEST__` prefix
2. **Development environment only** - Test Control API should be disabled in production via:
   ```csharp
   #if DEBUG
   builder.Services.AddControllers().AddApplicationPart(typeof(TestControlController).Assembly);
   #endif
   ```
3. **No authentication required** - Test Control API bypasses authentication for convenience
4. **Separate port/route** - Consider hosting on different port or under `/internal/test/` route

---

## Related Documentation

- [Tenancy System Documentation](../TENANCY.md)
- [Functional Testing Guide](../../tests/Functional/README.md)
- [Tenancy Feature File](../../tests/Functional/Features/Tenancy.feature)
- [Test Control Controller](../../src/Controllers/TestControlController.cs)

---

## Next Steps

1. Implement Phase 1 endpoints in [`TestControlController`](../../src/Controllers/TestControlController.cs)
2. Add corresponding DTOs and request/response models
3. Regenerate API client via NSwag
4. Create `WorkspaceTenancySteps.cs` base class using new API
5. Implement step definitions leveraging Test Control API
6. Add integration tests for Test Control API endpoints
7. Document usage patterns in test authoring guide
