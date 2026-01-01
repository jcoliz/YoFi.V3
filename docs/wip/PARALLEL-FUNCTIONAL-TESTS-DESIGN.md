---
status: Draft
target_release: TBD
created: 2025-12-31
---

# Parallel Functional Test Execution Design

## Problem Statement

Functional tests currently cannot run in parallel due to how test user accounts are managed. The current pattern:

1. **Delete all test users** via `DeleteUsersAsync()` before creating accounts
2. Create test user accounts with predictable names
3. Tests use these accounts for authentication
4. Cleanup happens after tests complete

This approach ensures:
- ✅ No duplicate account problems (fresh start every time)
- ✅ No lingering test accounts in the system (cleanup is guaranteed)
- ❌ **Cannot run tests in parallel** (tests would interfere with each other)

### Current Implementation

**Location:** [`tests/Functional/Steps/Common/CommonGivenSteps.cs:58`](tests/Functional/Steps/Common/CommonGivenSteps.cs:58)

```csharp
[Given("I have an existing account")]
protected async Task GivenIHaveAnExistingAccount()
{
    if (_objectStore.Contains<Generated.TestUserCredentials>())
        return;
    await testControlClient.DeleteUsersAsync();  // ❌ Blocks parallel execution
    var user = await testControlClient.CreateUsersAsync(new[] { "user" });
    _objectStore.Add(user.First());
}
```

**Also seen in:**
- [`AuthenticationSteps.cs:124`](tests/Functional/Steps/AuthenticationSteps.cs:124) - Registration tests
- [`AuthenticationSteps.cs:315`](tests/Functional/Steps/AuthenticationSteps.cs:315) - Weak password tests
- [`AuthenticationSteps.cs:355`](tests/Functional/Steps/AuthenticationSteps.cs:355) - Mismatched password tests

## Design Goals

1. **Enable parallel test execution** - Multiple tests run simultaneously without interference
2. **Maintain test isolation** - Each test has its own accounts and data
3. **Automatic cleanup** - Test accounts are reliably cleaned up after tests complete
4. **No duplicate accounts** - Tests don't fail due to account collisions
5. **Backwards compatible** - Existing tests continue to work with minimal changes
6. **Simple for test authors** - Easy to use in new tests

## Solution: Test-Isolated Account Strategy

### Core Concepts

#### 1. Unified User Creation Pattern - Client Owns Credentials

**Single Responsibility Principle:** Client (test) always generates credentials, server (API) just stores them in the database.

```csharp
// Helper: Generate unique credentials (same for ALL scenarios)
protected TestUserCredentials CreateTestUserCredentials(string friendlyName)
{
    var testId = TestContext.CurrentContext.Test.ID.GetHashCode();
    var username = $"__TEST__{friendlyName}_{testId:X8}";
    var password = $"Test_{testId:X8}!";

    return new TestUserCredentials
    {
        ShortName = friendlyName,
        Username = username,
        Email = $"{username}@test.local",
        Password = password
    };
}
```

**Usage: Setup Existing Users**
```csharp
// Generate credentials on client
var aliceCreds = CreateTestUserCredentials("alice");

// Ask server to create user in DB
var created = await testControlClient.CreateUsersAsync(new[] { aliceCreds });
var alice = created.First();
```

**Usage: Testing Registration Flow**
```csharp
// Generate credentials on client (SAME method!)
var userCreds = CreateTestUserCredentials("testuser");

// Fill registration form (form will create user, not API)
await registerPage.EnterRegistrationDetailsAsync(userCreds.Email, userCreds.Username, ...);
```

**Key insight:** Same credential generation for both patterns! The only difference is WHO creates the database record (Test Control API vs. registration form).

#### 2. Remove Bulk Delete Operations

**Current pattern (blocks parallel execution):**
```csharp
// ❌ Delete ALL test users before creating new ones
await testControlClient.DeleteUsersAsync();

// Then create user
var user = await testControlClient.CreateUsersAsync(new[] { "user" });
```

**Problem:** `DeleteUsersAsync()` removes ALL test users, including those being used by other parallel tests.

**New pattern (parallel-safe):**
```csharp
// ✅ NO bulk delete! Just create unique user directly
var userCreds = CreateTestUserCredentials("user");
var created = await testControlClient.CreateUsersAsync(new[] { userCreds });
TrackCreatedUser(created.First());
```

**Key change:** Remove all `DeleteUsersAsync()` and `DeleteAllTestDataAsync()` calls. Each test creates unique users that don't collide with other tests.

#### 3. Test-Scoped Cleanup

Instead of deleting all test users upfront, clean up only the accounts created by the current test:

**Option A: Cleanup in TearDown (Preferred)**
```csharp
[TearDown]
public async Task TearDown()
{
    // Existing screenshot logic...

    // Clean up test users created by this specific test
    if (_objectStore.Contains<TestUserCredentials>())
    {
        var credentials = _objectStore.GetAll<TestUserCredentials>();
        foreach (var cred in credentials)
        {
            await testControlClient.DeleteUserAsync(cred.Username);
        }
    }

    _testActivity?.Stop();
    _testActivity?.Dispose();
}
```

**Option B: Background Cleanup Job (Future Enhancement)**
- Periodically delete test users older than X hours
- Handles cleanup if tests crash or are interrupted
- Keeps the database clean without test-time overhead

## Detailed Design

### 1. Test Control API Changes

#### Change CreateUsers to Accept Full Credentials

**Current endpoint:** `POST /testcontrol/users` accepts `string[]` usernames

**New endpoint:** `POST /testcontrol/users` accepts `TestUserCredentials[]`

```csharp
/// <summary>
/// Create multiple test users from provided credentials
/// </summary>
/// <param name="credentialsList">Collection of credentials including username, email, password</param>
/// <returns>Collection of created user credentials with IDs populated</returns>
[HttpPost("users")]
[ProducesResponseType(typeof(IReadOnlyCollection<TestUserCredentials>), StatusCodes.Status201Created)]
public async Task<IActionResult> CreateUsers([FromBody] IReadOnlyCollection<TestUserCredentials> credentialsList)
{
    LogStartingCount(credentialsList.Count);

    var createdCredentials = new List<TestUserCredentials>();

    foreach (var creds in credentialsList)
    {
        // Validate username has test prefix
        if (!creds.Username.StartsWith(TestPrefix, StringComparison.Ordinal))
        {
            return ProblemWithLog(
                StatusCodes.Status403Forbidden,
                "Username must have test prefix",
                $"Username '{creds.Username}' must start with {TestPrefix}"
            );
        }

        // Create user with provided credentials
        var identityUser = new IdentityUser
        {
            UserName = creds.Username,
            Email = creds.Email,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(identityUser, creds.Password);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Unable to create user {creds.Username}: {errors}");
        }

        // Get created user to populate ID
        var createdUser = await userManager.FindByNameAsync(creds.Username);
        createdCredentials.Add(new TestUserCredentials(
            Id: Guid.Parse(createdUser!.Id),
            ShortName: creds.ShortName,
            Username: creds.Username,
            Email: creds.Email,
            Password: creds.Password
        ));
    }

    LogOkCount(createdCredentials.Count);
    return CreatedAtAction(nameof(CreateUsers), createdCredentials);
}
```

**Key changes:**
- ❌ Remove suffix generation logic (client provides complete usernames)
- ✅ Accept `TestUserCredentials[]` instead of `string[]`
- ✅ Server just validates `__TEST__` prefix and stores in DB
- ✅ Returns credentials with `Id` populated

#### Update Bulk Delete Endpoint

**Existing endpoint:** `DELETE /testcontrol/users` (deletes ALL test users)

**New overload:** `DELETE /testcontrol/users` with body containing specific usernames

```csharp
/// <summary>
/// Delete specific test users by username list.
/// </summary>
/// <param name="usernames">Optional collection of usernames to delete. If null/empty, deletes ALL test users.</param>
/// <returns>204 No Content on success.</returns>
[HttpDelete("users")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public async Task<IActionResult> DeleteUsers([FromBody] IReadOnlyCollection<string>? usernames = null)
{
    IEnumerable<IdentityUser> usersToDelete;

    if (usernames == null || usernames.Count == 0)
    {
        // No list provided - delete ALL test users (existing behavior)
        usersToDelete = userManager.Users
            .Where(u => u.UserName != null && u.UserName.Contains(TestPrefix))
            .ToList();
    }
    else
    {
        // Specific list provided - delete only those users
        // Validate all usernames have test prefix
        var invalidUsernames = usernames.Where(u => !u.StartsWith(TestPrefix, StringComparison.Ordinal)).ToList();
        if (invalidUsernames.Count > 0)
        {
            return ProblemWithLog(
                StatusCodes.Status403Forbidden,
                "All usernames must have test prefix",
                $"Invalid usernames: {string.Join(", ", invalidUsernames)}"
            );
        }

        usersToDelete = userManager.Users
            .Where(u => u.UserName != null && usernames.Contains(u.UserName))
            .ToList();
    }

    foreach (var user in usersToDelete)
    {
        await userManager.DeleteAsync(user);
    }

    LogOkCount(usersToDelete.Count());
    return NoContent();
}
```

**Key changes:**
- ✅ Bulk delete now accepts optional list of specific usernames
- ✅ Empty/null request = delete ALL (existing behavior for CI/CD)
- ✅ Specific list = delete only those users (for test cleanup)
- ✅ All usernames validated for `__TEST__` prefix

#### Make CreateUsers More Flexible

The current `CreateUsers` endpoint already adds a unique suffix per call:

```csharp
// From TestControlController.CreateUsersInternalAsync()
var runSuffix = Guid.NewGuid().ToString("N")[..8];
var finalUsername = username.StartsWith(TestPrefix, StringComparison.Ordinal) ? username : TestPrefix + username;
finalUsername += "_" + runSuffix;
```

**Good:** This prevents collisions between test runs.

**Issue:** Test steps currently use hardcoded usernames like `"user"`, which gets transformed to `__TEST__user_{runSuffix}`. If we pass the full unique username from `CreateTestUserCredentials()`, we get double suffixes:

```
__TEST__user_ABCD1234_{runSuffix}
```

**Solution:** Modify `CreateUsersInternalAsync` to detect if username already has a unique suffix (contains underscore after `__TEST__` prefix) and skip adding `runSuffix` in that case:

```csharp
private async Task<IReadOnlyCollection<TestUserCredentials>> CreateUsersInternalAsync(...)
{
    var runSuffix = Guid.NewGuid().ToString("N")[..8];

    foreach (var username in usernames)
    {
        var finalUsername = username.StartsWith(TestPrefix, StringComparison.Ordinal)
            ? username
            : TestPrefix + username;

        // Only add run suffix if username doesn't already have a unique identifier
        // Pattern: __TEST__name_XXXXXXXX where X is hex digit
        if (!UsernameHasUniqueSuffix(finalUsername))
        {
            finalUsername += "_" + runSuffix;
        }

        // ... rest of implementation
    }
}

private static bool UsernameHasUniqueSuffix(string username)
{
    // Check if username matches pattern: __TEST__XXX_XXXXXXXX (8 hex chars after underscore)
    var parts = username.Split('_');
    if (parts.Length < 3) return false;

    var lastPart = parts[^1];
    return lastPart.Length == 8 && IsHexString(lastPart);
}

private static bool IsHexString(string str) =>
    str.All(c => (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f'));
```

### 2. FunctionalTestBase Changes

#### Add Cleanup Tracking

```csharp
public abstract partial class FunctionalTestBase : PageTest
{
    // Existing fields...

    // Track ALL users created during this test (from any pattern)
    private readonly List<TestUserCredentials> _createdUsers = new();

    // Track ALL workspaces created during this test
    private readonly List<Guid> _createdWorkspaces = new();

    // ... existing code ...
}
```

**Critical:** The `_createdUsers` list is the single source of truth for cleanup. ALL user creation paths (Pattern A and Pattern B) must call `TrackCreatedUser()`.

#### Update TearDown with Cleanup

```csharp
[TearDown]
public async Task TearDown()
{
    // Capture screenshot only on test failure
    if (TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Failed)
    {
        var pageModel = It<Pages.BasePage>();
        await pageModel.SaveScreenshotAsync($"FAILED");
    }

    // Clean up test-specific users and workspaces
    await CleanupTestResourcesAsync();

    _testActivity?.Stop();
    _testActivity?.Dispose();
}

/// <summary>
/// Cleans up test resources (users and workspaces) created during this test execution.
/// </summary>
private async Task CleanupTestResourcesAsync()
{
    try
    {
        // Clean up workspaces first (cascade will delete transactions)
        foreach (var workspaceKey in _createdWorkspaces)
        {
            try
            {
                await testControlClient.DeleteWorkspaceAsync(workspaceKey);
            }
            catch (Exception ex)
            {
                // Log but don't fail test if cleanup fails
                TestContext.Out.WriteLine($"[Cleanup] Failed to delete workspace {workspaceKey}: {ex.Message}");
            }
        }

        // Clean up users
        foreach (var user in _createdUsers)
        {
            try
            {
                await testControlClient.DeleteUserAsync(user.Username);
            }
            catch (Exception ex)
            {
                // Log but don't fail test if cleanup fails
                TestContext.Out.WriteLine($"[Cleanup] Failed to delete user {user.Username}: {ex.Message}");
            }
        }
    }
    catch (Exception ex)
    {
        // Swallow exceptions during cleanup to avoid masking test failures
        TestContext.Error.WriteLine($"[Cleanup] Unexpected error during cleanup: {ex}");
    }
}
```

#### Add Helper for Tracking Created Resources

```csharp
/// <summary>
/// Tracks a created user for cleanup in TearDown.
/// </summary>
/// <remarks>
/// MUST be called for EVERY user created during the test, regardless of creation pattern:
/// - Pattern A: After API creates user
/// - Pattern B: After form submits successfully (or even before, to ensure cleanup)
/// This is the ONLY way to ensure users are cleaned up in TearDown.
/// </remarks>
protected void TrackCreatedUser(TestUserCredentials user)
{
    _createdUsers.Add(user);

    // Also add to object store for backward compatibility with existing tests
    // (some tests retrieve users from object store in later steps)
    if (!_objectStore.Contains<TestUserCredentials>())
    {
        _objectStore.Add(user);
    }
}

/// <summary>
/// Tracks a created workspace for cleanup in TearDown.
/// </summary>
protected void TrackCreatedWorkspace(Guid workspaceKey)
{
    _createdWorkspaces.Add(workspaceKey);
}
```

**Important:** Tests that create multiple users should track each one individually. The `_userCredentials` dictionary in `WorkspaceTenancySteps` is for test step lookups, but `_createdUsers` list is for cleanup.

### 3. Step Implementation Updates

#### CommonGivenSteps - Unified Pattern

**Before:**
```csharp
[Given("I have an existing account")]
protected async Task GivenIHaveAnExistingAccount()
{
    if (_objectStore.Contains<Generated.TestUserCredentials>())
        return;
    await testControlClient.DeleteUsersAsync();  // ❌ Blocks parallel
    var user = await testControlClient.CreateUsersAsync(new[] { "user" });
    _objectStore.Add(user.First());
}
```

**After:**
```csharp
[Given("I have an existing account")]
protected async Task GivenIHaveAnExistingAccount()
{
    if (_objectStore.Contains<Generated.TestUserCredentials>())
        return;

    // Generate unique credentials on client
    var userCreds = CreateTestUserCredentials("user");

    // Ask API to create user in DB
    var created = await testControlClient.CreateUsersAsync(new[] { userCreds });
    var user = created.First();

    // Track for cleanup
    TrackCreatedUser(user);
}
```

**Key change:** Client generates credentials, API just stores in DB.

#### WorkspaceTenancySteps - Multiple Users

**Before (line 127):**
```csharp
[Given("these users exist")]
protected async Task GivenTheseUsersExist(DataTable usersTable)
{
    // Clear existing users and workspaces to avoid conflicts
    await testControlClient.DeleteAllTestDataAsync();  // ❌ Blocks parallel

    var usernames = usersTable.ToSingleColumnList().ToList();
    var credentials = await testControlClient.CreateUsersAsync(usernames);

    // Store with given username (what's in the datatable)
    foreach (var cred in credentials)
    {
        _userCredentials[cred.ShortName] = cred;
    }
}
```

**After:**
```csharp
[Given("these users exist")]
protected async Task GivenTheseUsersExist(DataTable usersTable)
{
    // Get friendly names from DataTable (e.g., "alice", "bob")
    var usernames = usersTable.ToSingleColumnList().ToList();

    // Create all users in bulk
    // API will automatically add __TEST__ prefix and unique suffix per test run
    var credentials = await testControlClient.CreateUsersAsync(usernames);

    // Store with short name as key (API returns ShortName = friendly name), track for cleanup
    foreach (var cred in credentials)
    {
        _userCredentials[cred.ShortName] = cred;  // ShortName = "alice", Username = "__TEST__alice_12345678"
        TrackCreatedUser(cred);
    }
}
```

**Key changes:**
- ❌ Remove `DeleteAllTestDataAsync()` call
- ✅ Pass friendly names directly to API (Pattern A)
- ✅ API handles uniqueness automatically
- ✅ **Track EACH user** in `_createdUsers` list for cleanup
- ✅ Store in `_userCredentials` dictionary for test step lookups

#### AuthenticationSteps - Same Unified Pattern

**Before (lines 119-130):**
```csharp
[When("I enter valid registration details")]
protected async Task WhenIEnterValidRegistrationDetails()
{
    var registerPage = GetOrCreateRegisterPage();

    await testControlClient.DeleteUsersAsync();  // ❌ Blocks parallel

    var user = CreateTestUserCredentials("testuser");
    _objectStore.Add("Registration Details", user);

    await registerPage.EnterRegistrationDetailsAsync(user.Email, user.Username, user.Password, user.Password);
}
```

**After:**
```csharp
[When("I enter valid registration details")]
protected async Task WhenIEnterValidRegistrationDetails()
{
    var registerPage = GetOrCreateRegisterPage();

    // SAME PATTERN: Generate credentials on client
    var user = CreateTestUserCredentials("testuser");
    _objectStore.Add("Registration Details", user);

    await registerPage.EnterRegistrationDetailsAsync(user.Email, user.Username, user.Password, user.Password);

    // Note: User will be created by FORM SUBMISSION (not Test Control API)
    // Track for cleanup - successful registration creates a real user
    TrackCreatedUser(user);
}
```

**Key insight:** Exact same pattern! Client generates credentials, someone else (form or API) creates the DB record.

**Pattern:** Remove `DeleteUsersAsync()` calls from all 3 occurrences in `AuthenticationSteps.cs`, add `TrackCreatedUser()` calls.

### 4. NUnit Configuration for Parallel Execution

#### Assembly-Level Attributes

Create or update `tests/Functional/AssemblyInfo.cs`:

```csharp
using NUnit.Framework;

// Enable parallel execution at the fixture level
[assembly: Parallelizable(ParallelScope.Fixtures)]

// Limit concurrent fixtures to avoid overwhelming the system
[assembly: LevelOfParallelism(4)]
```

**Explanation:**
- `ParallelScope.Fixtures` - Different test fixture classes run in parallel
- Tests within the same fixture run sequentially (preserves test order within features)
- `LevelOfParallelism(4)` - Max 4 fixtures run simultaneously (adjust based on system resources)

#### Alternative: Test-Level Parallelism

For more aggressive parallelism:

```csharp
[assembly: Parallelizable(ParallelScope.All)]
[assembly: LevelOfParallelism(8)]
```

**Trade-offs:**
- **Fixtures**: More conservative, easier to debug, good for CI/CD
- **All (Children)**: Maximum parallelism, requires more system resources

### 5. Workspace and Transaction Cleanup

Workspaces created during tests also need cleanup. Two approaches:

#### Option A: Track and Clean Explicitly

```csharp
// In test steps that create workspaces
var workspace = await testControlClient.CreateWorkspaceForUserAsync(...);
TrackCreatedWorkspace(workspace.Key);
```

#### Option B: Delete by User Association

Add Test Control API endpoint to delete all workspaces for a user:

```csharp
[HttpDelete("users/{username}/workspaces")]
public async Task<IActionResult> DeleteUserWorkspaces(string username)
{
    // Get user
    var user = await userManager.FindByNameAsync(username);
    if (user == null) return NotFound();

    // Get all workspaces where user has a role
    var userId = Guid.Parse(user.Id);
    var workspaces = await tenantFeature.GetTenantsForUserAsync(userId);

    // Delete only test workspaces
    var testWorkspaces = workspaces
        .Where(w => w.Name.StartsWith(TestPrefix))
        .Select(w => w.Key)
        .ToList();

    if (testWorkspaces.Count > 0)
    {
        await tenantFeature.DeleteTenantsByKeysAsync(testWorkspaces);
    }

    return NoContent();
}
```

Then in cleanup:

```csharp
// Delete user's workspaces (cascade deletes transactions)
await testControlClient.DeleteUserWorkspacesAsync(user.Username);

// Delete user
await testControlClient.DeleteUserAsync(user.Username);
```

**Recommendation:** Start with Option A (explicit tracking) for clarity and control. Add Option B if cleanup becomes cumbersome.

## Migration Strategy

### Phase 1: Add Infrastructure (Non-Breaking)

1. ✅ Add `DELETE /testcontrol/users/{username}` endpoint
2. ✅ Update `CreateUsersInternalAsync` to handle pre-suffixed usernames
3. ✅ Add cleanup tracking to `FunctionalTestBase`
4. ✅ Add `TrackCreatedUser` / `TrackCreatedWorkspace` helpers
5. ✅ Update `TearDown` with cleanup logic

**Result:** Infrastructure in place, but tests still work as before (no parallel execution yet).

### Phase 2: Update Test Steps (Breaking Changes)

1. ✅ Remove `DeleteUsersAsync()` from `CommonGivenSteps.GivenIHaveAnExistingAccount()`
2. ✅ Remove `DeleteUsersAsync()` from `AuthenticationSteps` (3 locations)
3. ✅ Update steps to use `TrackCreatedUser()` for cleanup
4. ✅ Update workspace creation steps to use `TrackCreatedWorkspace()`

**Result:** Tests now use isolated accounts, but still run sequentially.

### Phase 3: Enable Parallel Execution

1. ✅ Add NUnit parallel configuration to `AssemblyInfo.cs`
2. ✅ Run tests in parallel and verify no interference
3. ✅ Adjust `LevelOfParallelism` based on system capacity

**Result:** Tests run in parallel! 🎉

### Phase 4: Documentation and Optimization

1. ✅ Update [`tests/Functional/TEST-CONTROLS.md`](tests/Functional/TEST-CONTROLS.md) with new patterns
2. ✅ Add migration guide for existing tests
3. ✅ Consider background cleanup job for orphaned accounts
4. ✅ Measure and optimize parallel execution performance

## Benefits

### Performance Improvements

**Current (Sequential):**
- 100 tests × 10 seconds each = **1000 seconds (16.7 minutes)**

**With Parallel Execution (4 workers):**
- 100 tests ÷ 4 workers × 10 seconds each = **250 seconds (4.2 minutes)**
- **4x faster!** ⚡

**With Parallel Execution (8 workers):**
- 100 tests ÷ 8 workers × 10 seconds each = **125 seconds (2.1 minutes)**
- **8x faster!** 🚀

### Developer Experience

- ✅ Faster feedback loop during development
- ✅ Faster CI/CD pipeline execution
- ✅ Same test authoring patterns (minimal changes needed)
- ✅ Better resource utilization on CI/CD servers

### Reliability

- ✅ Each test is truly isolated (no shared state)
- ✅ Test failures don't affect other running tests
- ✅ Deterministic test execution (no race conditions)
- ✅ Cleanup happens per-test (no orphaned data)

## Risks and Mitigations

### Risk: Database Connection Pool Exhaustion

**Symptom:** Tests fail with "connection pool exhausted" errors when running many tests in parallel.

**Mitigation:**
- Configure larger connection pool: `MaxPoolSize=100` in connection string
- Limit `LevelOfParallelism` to match available connections
- Monitor database connection usage during test runs

### Risk: Test Cleanup Failures Leave Orphaned Accounts

**Symptom:** Test database accumulates orphaned `__TEST__` accounts over time.

**Mitigation:**
- Add background cleanup job (run daily or on-demand)
- Keep the bulk `DELETE /testcontrol/users` endpoint for manual cleanup
- Add test fixture `OneTimeSetUp` to clean orphaned accounts before test suite runs:

```csharp
[OneTimeSetUp]
public async Task OneTimeSetup()
{
    // ... existing prerequisites ...

    // Clean up any orphaned test accounts from previous runs
    await testControlClient.DeleteUsersAsync();
}
```

### Risk: Browser Resource Exhaustion

**Symptom:** Playwright fails to launch browsers when too many tests run in parallel.

**Mitigation:**
- Limit `LevelOfParallelism` based on available system memory
- Each Chromium instance uses ~100-200 MB RAM
- For 8GB system: max 4-8 parallel tests
- For 16GB system: max 8-16 parallel tests

### Risk: Flaky Tests Due to Timing Issues

**Symptom:** Tests that passed sequentially fail intermittently when running in parallel.

**Mitigation:**
- Not related to account management (tests are isolated)
- Could be infrastructure issues (database, network)
- Add retries for transient failures
- Increase timeouts if needed

## Alternative Approaches Considered

### Alternative 1: One Shared Test User Pool

**Concept:** Create a pool of test users upfront, assign them to tests dynamically.

**Pros:**
- No user creation overhead during tests
- Predictable number of test accounts

**Cons:**
- ❌ Complex synchronization needed (lock/unlock users)
- ❌ Test failures could leave users in inconsistent state
- ❌ Harder to trace which test used which account
- ❌ Not truly isolated

**Verdict:** Rejected - Too complex, not worth the trade-offs.

### Alternative 2: Database Snapshots/Transactions

**Concept:** Each test runs in a database transaction, rolled back after test completes.

**Pros:**
- Perfect isolation
- Fast cleanup (just rollback)

**Cons:**
- ❌ ASP.NET Identity doesn't work well with ambient transactions
- ❌ Complex to set up with Entity Framework
- ❌ Doesn't work with distributed transactions (API + Database)
- ❌ Playwright tests make real HTTP requests (can't participate in transaction)

**Verdict:** Rejected - Not feasible for functional tests with real HTTP calls.

### Alternative 3: One Database Per Test

**Concept:** Each test gets its own isolated database instance.

**Pros:**
- Perfect isolation
- No cleanup needed (just drop database)

**Cons:**
- ❌ Very slow (database creation is expensive)
- ❌ Requires database server configuration changes
- ❌ Complex connection string management
- ❌ Resource intensive

**Verdict:** Rejected - Too slow and complex for functional tests.

## Implementation Checklist

### Test Control API
- [ ] Add `DELETE /testcontrol/users/{username}` endpoint
- [ ] Add `DELETE /testcontrol/users/{username}/workspaces` endpoint (optional)
- [ ] Regenerate API client: `pwsh -File ./scripts/Generate-ApiClient.ps1`
- [ ] Add integration tests for new endpoints

### Functional Test Base
- [ ] Add `_createdUsers` tracking list to [`FunctionalTestBase`](tests/Functional/Infrastructure/FunctionalTestBase.cs)
- [ ] Add `_createdWorkspaces` tracking list
- [ ] Add `TrackCreatedUser` helper method
- [ ] Add `TrackCreatedWorkspace` helper method
- [ ] Update `TearDown` with `CleanupTestResourcesAsync` call
- [ ] Add `CleanupTestResourcesAsync` implementation
- [ ] Add `OneTimeSetUp` cleanup for orphaned accounts (optional)

### Test Steps
- [ ] Update [`CommonGivenSteps.GivenIHaveAnExistingAccount`](tests/Functional/Steps/Common/CommonGivenSteps.cs:53) - single user
- [ ] Update [`WorkspaceTenancySteps.GivenTheseUsersExist`](tests/Functional/Steps/WorkspaceTenancySteps.cs:127) - **multiple users in bulk**
- [ ] Update [`AuthenticationSteps.WhenIEnterValidRegistrationDetails`](tests/Functional/Steps/AuthenticationSteps.cs:119)
- [ ] Update [`AuthenticationSteps.WhenIEnterRegistrationDetailsWithAWeakPassword`](tests/Functional/Steps/AuthenticationSteps.cs:310)
- [ ] Update [`AuthenticationSteps.WhenIEnterRegistrationDetailsWithMismatchedPasswords`](tests/Functional/Steps/AuthenticationSteps.cs:350)
- [ ] Add `TrackCreatedUser` calls to `WorkspaceTenancySteps` for users/workspaces created via Test Control API
- [ ] Search for other `DeleteUsersAsync()` or `DeleteAllTestDataAsync()` calls and update

### NUnit Configuration
- [ ] Create or update [`tests/Functional/AssemblyInfo.cs`](tests/Functional/AssemblyInfo.cs)
- [ ] Add `[assembly: Parallelizable(ParallelScope.Fixtures)]`
- [ ] Add `[assembly: LevelOfParallelism(4)]`

### Testing & Validation
- [ ] Run existing tests sequentially to verify no regression
- [ ] Run tests in parallel with 2 workers
- [ ] Run tests in parallel with 4 workers
- [ ] Run tests in parallel with 8 workers
- [ ] Verify cleanup works (no orphaned accounts in database)
- [ ] Measure performance improvements
- [ ] Check for flaky tests

### Documentation
- [ ] Update [`tests/Functional/TEST-CONTROLS.md`](tests/Functional/TEST-CONTROLS.md)
- [ ] Add parallel execution section to [`tests/Functional/README.md`](tests/Functional/README.md)
- [ ] Document cleanup patterns for test authors
- [ ] Add troubleshooting guide for common parallel execution issues

## Conclusion

This design enables parallel functional test execution by:

1. **Eliminating shared state** - Each test creates unique accounts
2. **Test-scoped cleanup** - Each test cleans up its own resources
3. **Minimal API changes** - Add individual delete endpoint, keep bulk delete for compatibility
4. **Simple migration** - Remove `DeleteUsersAsync()` calls, add tracking
5. **Performance gains** - 4-8x faster test execution with parallel workers

The approach balances simplicity, reliability, and performance, making it suitable for immediate implementation and long-term maintenance.

