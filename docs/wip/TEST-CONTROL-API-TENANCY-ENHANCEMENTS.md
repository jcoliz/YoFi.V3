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

### 1. Bulk User Creation with Credentials

**Problem:** Background section in [`Tenancy.feature:13-17`](../../tests/Functional/Features/Tenancy.feature#L13-L17) needs to create multiple named users (alice, bob, charlie). Tests need credentials immediately for login steps.

**Endpoint:**
```csharp
// POST /TestControl/users/bulk
[HttpPost("users/bulk")]
[ProducesResponseType(typeof(IReadOnlyCollection<TestUserCredentials>), StatusCodes.Status201Created)]
public async Task<IActionResult> CreateBulkUsers([FromBody] string[] usernames)

public record TestUserCredentials(Guid Id, string Username, string Email, string Password);
```

**Request Body:**
```json
["alice", "bob", "charlie"]
```

**Response:**
```json
[
  {
    "Id": "abc-123-def-456",
    "Username": "__TEST__alice",
    "Email": "__TEST__alice@test.com",
    "Password": "SecurePass123!"
  },
  {
    "Id": "xyz-789-ghi-012",
    "Username": "__TEST__bob",
    "Email": "__TEST__bob@test.com",
    "Password": "SecurePass456!"
  },
  {
    "Id": "mno-345-pqr-678",
    "Username": "__TEST__charlie",
    "Email": "__TEST__charlie@test.com",
    "Password": "SecurePass789!"
  }
]
```

**Implementation Notes:**
- Prefix usernames with `__TEST__` for consistency with existing pattern
- Generate secure random passwords for each user
- **Return ALL credentials immediately** - Test remembers them for later login steps
- Users are automatically approved (no email confirmation required for tests)
- **No separate "get credentials" endpoint needed** - All data returned at creation time

---

### 2. Create Workspace for User

**Problem:** Many scenarios need pre-seeded workspaces with specific names and roles ([lines 49-53](../../tests/Functional/Features/Tenancy.feature#L49-L53), [78](../../tests/Functional/Features/Tenancy.feature#L78), [87](../../tests/Functional/Features/Tenancy.feature#L87), [93](../../tests/Functional/Features/Tenancy.feature#L93), etc.).

**Endpoint:**
```csharp
// POST /TestControl/users/{username}/workspaces
[HttpPost("users/{username}/workspaces")]
[ProducesResponseType(typeof(TenantResultDto), StatusCodes.Status201Created)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public async Task<IActionResult> CreateWorkspaceForUser(
    string username,
    [FromBody] WorkspaceCreateRequest request)

public record WorkspaceCreateRequest(string Name, string Description, string Role = "Owner");
```

**Request Body:**
```json
{
  "Name": "__TEST__Personal",
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
- **Validates user is a test user** - Username must start with `__TEST__` prefix (returns 403 if not)
- **Validates workspace name has test prefix** - Workspace name must start with `__TEST__` prefix (returns 403 if not)
- Looks up user by `__TEST__{username}` format
- Calls [`TenantFeature.CreateTenantForUserAsync()`](../../src/Controllers/Tenancy/Features/TenantFeature.cs#L22)
- Sets specified role via [`ITenantRepository.AddUserTenantRoleAsync()`](../../src/Entities/Tenancy/Providers/ITenantRepository.cs#L51)
- Returns 404 if user not found
- Returns 403 if username doesn't have `__TEST__` prefix OR workspace name doesn't have `__TEST__` prefix

---

### 3. Assign User to Existing Workspace

**Problem:** Scenarios need users with different roles in the same workspace ([lines 49-53](../../tests/Functional/Features/Tenancy.feature#L49-L53): alice has Owner/Editor/Viewer roles across different workspaces).

**Endpoint:**
```csharp
// POST /TestControl/users/{username}/workspaces/{workspaceKey}/assign
[HttpPost("users/{username}/workspaces/{workspaceKey:guid}/assign")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
public async Task<IActionResult> AssignUserToWorkspace(
    string username,
    Guid workspaceKey,
    [FromBody] UserRoleAssignment assignment)

public record UserRoleAssignment(string Role);
```

**Request Body:**
```json
{
  "Role": "Editor"
}
```

**Response:** 204 No Content on success

**Implementation Notes:**
- **Validates user is a test user** - Username must start with `__TEST__` prefix (returns 403 if not)
- **Validates workspace is a test workspace** - Workspace name must start with `__TEST__` prefix (returns 403 if not)
- Looks up workspace by Key via [`ITenantRepository.GetTenantByKeyAsync()`](../../src/Entities/Tenancy/Providers/ITenantRepository.cs#L74)
- Looks up user by `__TEST__{username}` (from path parameter)
- Calls [`ITenantRepository.AddUserTenantRoleAsync()`](../../src/Entities/Tenancy/Providers/ITenantRepository.cs#L51) with specified role
- Returns 404 if workspace or user not found
- Returns 403 if username doesn't have `__TEST__` prefix OR workspace name doesn't have `__TEST__` prefix
- Returns 409 if user already has a role in that workspace

---

### 4. Seed Transactions for Workspace

**Problem:** [Lines 106-107](../../tests/Functional/Features/Tenancy.feature#L106-L107) need specific transaction counts in specific workspaces.

**Endpoint:**
```csharp
// POST /TestControl/users/{username}/workspaces/{workspaceKey}/transactions/seed
[HttpPost("users/{username}/workspaces/{workspaceKey:guid}/transactions/seed")]
[ProducesResponseType(typeof(IReadOnlyCollection<TransactionResultDto>), StatusCodes.Status201Created)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public async Task<IActionResult> SeedTransactions(
    string username,
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
- **Validates user is a test user** - Username must start with `__TEST__` prefix (returns 403 if not)
- **Validates workspace is a test workspace** - Workspace name must start with `__TEST__` prefix (returns 403 if not)
- **Validates user has a role on the workspace** - User must have some role assignment on the workspace (returns 403 if not)
- Looks up workspace via [`ITenantRepository.GetTenantByKeyAsync()`](../../src/Entities/Tenancy/Providers/ITenantRepository.cs#L74)
- Looks up user by `__TEST__{username}` and verifies role on workspace
- Sets [`TenantContext`](../../src/Controllers/Tenancy/Context/TenantContext.cs) to specified workspace
- Creates `Count` transactions with auto-generated realistic data:
  - **Payee format:** `"{PayeePrefix} {i}"` (e.g., "Personal Expense 1", "Personal Expense 2")
  - **Amount:** Random between $10.00 and $500.00
  - **Date:** Distributed over last 30 days
- Uses [`TransactionsFeature.AddTransactionAsync()`](../../src/Application/Features/TransactionsFeature.cs) for each transaction
- Returns 404 if workspace or user not found
- Returns 403 if username doesn't have `__TEST__` prefix OR workspace name doesn't have `__TEST__` prefix OR user has no role on workspace

---

### 5. Delete All Test Data

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
- Deletes all workspaces where name has `__TEST__` prefix
- Deletes all test users (leverages existing [`DeleteUsersAsync()`](../../src/Controllers/TestControlController.cs#L109) functionality)
- Relies on cascade delete configuration to remove:
  - User-tenant role assignments
  - Transactions belonging to test workspaces
- More comprehensive than just deleting users
- Double-safe: Deletes by workspace name prefix AND by user prefix

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
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public async Task<IActionResult> BulkWorkspaceSetup(
    string username,
    [FromBody] WorkspaceSetupRequest[] workspaces)

public record WorkspaceSetupRequest(string Name, string Description, string Role);
public record WorkspaceSetupResult(Guid Key, string Name, string Role);
```

**Request Body:**
```json
[
  { "Name": "__TEST__Personal", "Description": "My workspace", "Role": "Owner" },
  { "Name": "__TEST__Family Budget", "Description": "Shared workspace", "Role": "Editor" },
  { "Name": "__TEST__Tax Records", "Description": "Read-only access", "Role": "Viewer" }
]
```

**Response:**
```json
[
  { "Key": "abc-123-def", "Name": "__TEST__Personal", "Role": "Owner" },
  { "Key": "xyz-789-ghi", "Name": "__TEST__Family Budget", "Role": "Editor" },
  { "Key": "mno-456-pqr", "Name": "__TEST__Tax Records", "Role": "Viewer" }
]
```

**Implementation Notes:**
- **Validates user is a test user** - Username must start with `__TEST__` prefix (returns 403 if not)
- **Validates all workspace names have test prefix** - All workspace names must start with `__TEST__` prefix (returns 403 if any don't)
- Convenience endpoint that combines multiple operations
- For "Owner" role: Creates workspace with user as owner
- For other roles: Creates workspace owned by a system user, then assigns specified role to target user
- Returns keys for all created workspaces
- Returns 403 if username doesn't have `__TEST__` prefix OR any workspace name doesn't have `__TEST__` prefix
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
- `ITestControlClient.CreateBulkUsersAsync(string[] usernames)` → Returns `IReadOnlyCollection<TestUserCredentials>`
- `ITestControlClient.CreateWorkspaceForUserAsync(string username, WorkspaceCreateRequest request)`
- `ITestControlClient.AssignUserToWorkspaceAsync(string username, Guid workspaceKey, UserRoleAssignment assignment)`
- `ITestControlClient.SeedTransactionsAsync(string username, Guid workspaceKey, TransactionSeedRequest request)`
- `ITestControlClient.DeleteAllTestDataAsync()`
- `ITestControlClient.BulkWorkspaceSetupAsync(string username, WorkspaceSetupRequest[] workspaces)`

---

## Implementation Priority

### Phase 1: Essential Infrastructure (Blocking)
1. **Bulk user creation with credentials** (#1) - Required by Background section, returns all credentials
2. **Create workspace for user** (#2) - Required for basic workspace scenarios
3. **Delete all test data** (#5) - Required for test isolation

### Phase 2: Multi-User Scenarios
4. **Assign user to workspace** (#3) - Required for shared workspace tests
5. **Bulk workspace setup** (#6) - Convenience for complex setups

### Phase 3: Data Seeding
6. **Seed transactions** (#4) - Required for data isolation tests

---

## Usage Example in Test Steps

### Example 1: Background Section - Bulk User Creation

**Gherkin:**
```gherkin
Background:
  Given these test users exist:
    | Username |
    | alice    |
    | bob      |
    | charlie  |
```

**Step Implementation:**
```csharp
[Given(@"these test users exist:")]
protected async Task GivenTheseTestUsersExist(DataTable usersTable)
{
    var usernames = usersTable.Rows.Select(row => row["Username"]).ToArray();

    // Single API call creates all users AND returns credentials
    var credentials = await _testControlClient.CreateBulkUsersAsync(usernames);

    // Store credentials in test context for later login steps
    foreach (var cred in credentials)
    {
        var shortUsername = cred.Username.Replace("__TEST__", "");
        _userCredentials[shortUsername] = cred;
    }
}

[Given(@"I am logged in as (.*)")]
protected async Task GivenIAmLoggedInAs(string username)
{
    // Retrieve stored credentials - no API call needed!
    var cred = _userCredentials[username];
    await _authPage.LoginAsync(cred.Email, cred.Password);
}
```

### Example 2: Workspace Setup

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

1. **Test-only restriction** - All workspace-related endpoints validate that:
   - Username contains `__TEST__` prefix (returns 403 Forbidden if not)
   - Workspace name contains `__TEST__` prefix (returns 403 Forbidden if not)
   - For workspace data operations (seed transactions): User must have an existing role on the workspace (returns 403 if not)
   - This dual-prefix validation prevents accidental modification of production workspaces or data via Test Control API

2. **Production tenant name validation** - The regular tenant management API (TenantController/TenantFeature) MUST reject tenant names starting with `__TEST__`:
   - Add validation rule to prevent users from creating production tenants with `__TEST__` prefix
   - This prevents users from accidentally creating tenants that could be deleted by Test Control cleanup
   - Validation should return 400 Bad Request with clear error message: "Tenant names cannot start with '__TEST__' as this prefix is reserved for automated testing"
   - Implementation location: Add to TenantEditDto validation or TenantFeature business logic
   - **Important:** This creates intentional coupling between test infrastructure and production code for user safety

3. **Development environment only** - Test Control API should be disabled in production via:
   ```csharp
   #if DEBUG
   builder.Services.AddControllers().AddApplicationPart(typeof(TestControlController).Assembly);
   #endif
   ```

4. **No authentication required** - Test Control API bypasses authentication for convenience in tests

5. **Separate port/route** - Consider hosting on different port or under `/internal/test/` route

6. **Role validation** - Workspace operations require user to already have a role assignment, preventing unauthorized access to arbitrary workspaces

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
