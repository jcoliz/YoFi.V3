---
status: In review
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
// Helper: Generate unique credentials AND track for cleanup
protected TestUserCredentials CreateTestUserCredentials(string friendlyName)
{
    var testId = TestContext.CurrentContext.Test.ID.GetHashCode();
    var username = $"__TEST__{friendlyName}_{testId:X8}";
    var password = $"Test_{testId:X8}!";

    var creds = new TestUserCredentials
    {
        ShortName = friendlyName,
        Username = username,
        Email = $"{username}@test.local",
        Password = password
    };

    // Store immediately for lookup and cleanup
    _userCredentials[friendlyName] = creds;

    return creds;
}
```

**Key insight:** Generate AND track in one step. No separate `TrackCreatedUser()` needed!

**Usage: Setup Existing Users**
```csharp
// Generate credentials (automatically tracked with empty ID)
var aliceCreds = CreateTestUserCredentials("alice");

// Ask server to create user in DB (returns credentials with ID populated)
var created = await testControlClient.CreateUsersAsync(new[] { aliceCreds });

// Update dictionary with server-populated credentials (including ID)
var createdUser = created.Single();
_userCredentials[createdUser.ShortName] = createdUser;
```

**Key flow:** Generate → Track (empty ID) → API create → Update with server credentials (including ID)

**Usage: Testing Registration Flow**
```csharp
// Generate credentials (automatically tracked)
var userCreds = CreateTestUserCredentials("testuser");

// Fill registration form (form will create user, not API)
await registerPage.EnterRegistrationDetailsAsync(userCreds.Email, userCreds.Username, ...);
```

**Key insight:** `CreateTestUserCredentials()` generates AND tracks automatically. No separate tracking call needed!

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
var userCreds = CreateTestUserCredentials("user");  // Auto-tracked
var created = await testControlClient.CreateUsersAsync(new[] { userCreds });

// Update with server-populated ID
var createdUser = created.Single();
_userCredentials[createdUser.ShortName] = createdUser;
```

**Key change:** Remove all `DeleteUsersAsync()` and `DeleteAllTestDataAsync()` calls. Each test creates unique users that don't collide with other tests.

#### 3. Test-Scoped Cleanup

Clean up only the accounts created by the current test using the `_createdUsers` tracking list:

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

    // Clean up test-specific users in bulk
    if (_userCredentials.Count > 0)
    {
        var usernames = _userCredentials.Values.Select(u => u.Username).ToList();
        await testControlClient.DeleteUsersAsync(usernames);
    }

    _testActivity?.Stop();
    _testActivity?.Dispose();
}
```

**Future Enhancement: Background Cleanup Job**
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

#### Add Bulk Delete Endpoint with Required Body

**New endpoint:** `DELETE /testcontrol/users` with required body containing specific usernames

```csharp
/// <summary>
/// Delete specific test users by username list.
/// </summary>
/// <param name="usernames">Collection of usernames to delete. Must not be empty.</param>
/// <returns>204 No Content on success.</returns>
[HttpDelete("users")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
public async Task<IActionResult> DeleteUsers([FromBody] IReadOnlyCollection<string> usernames)
{
    // Require explicit username list
    if (usernames == null || usernames.Count == 0)
    {
        return ProblemWithLog(
            StatusCodes.Status400BadRequest,
            "Username list required",
            "Must provide explicit list of usernames to delete. Empty list not allowed."
        );
    }

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

    // Delete specified users
    var usersToDelete = userManager.Users
        .Where(u => u.UserName != null && usernames.Contains(u.UserName))
        .ToList();

    foreach (var user in usersToDelete)
    {
        await userManager.DeleteAsync(user);
    }

    LogOkCount(usersToDelete.Count);
    return NoContent();
}
```

**Key changes:**
- ✅ **Required body** - empty/null list returns 400 Bad Request
- ✅ **No "delete all" behavior** - must explicitly provide usernames
- ✅ **Safer** - impossible to accidentally delete all test users
- ✅ All usernames validated for `__TEST__` prefix

**For bulk cleanup:** Use existing `DELETE /testcontrol/data` endpoint (if still needed for CI/CD cleanup).

### 2. FunctionalTestBase Changes

#### Add Cleanup Tracking

```csharp
public abstract partial class FunctionalTestBase : PageTest
{
    // Existing fields...

    // Track users by friendly name for lookups AND cleanup
    protected readonly Dictionary<string, TestUserCredentials> _userCredentials = new();

    // Track workspaces for cleanup
    private readonly List<Guid> _createdWorkspaces = new();

    // ... existing code ...
}
```

**Design:** Single dictionary provides both:
- **O(1) lookups** by friendly name during test steps
- **Cleanup** via `_userCredentials.Values` in TearDown

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

        // Clean up users in bulk
        if (_userCredentials.Count > 0)
        {
            try
            {
                var usernames = _userCredentials.Values.Select(u => u.Username).ToList();
                await testControlClient.DeleteUsersAsync(usernames);
            }
            catch (Exception ex)
            {
                // Log but don't fail test if cleanup fails
                TestContext.Out.WriteLine($"[Cleanup] Failed to delete users: {ex.Message}");
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

#### Add Helper for Tracking Workspaces

```csharp
/// <summary>
/// Tracks a created workspace for cleanup in TearDown.
/// </summary>
protected void TrackCreatedWorkspace(Guid workspaceKey)
{
    _createdWorkspaces.Add(workspaceKey);
}
```

**Note:** No `TrackCreatedUser()` needed! `CreateTestUserCredentials()` tracks automatically when generating credentials.

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
    // Generate credentials (automatically tracked with empty ID)
    var userCreds = CreateTestUserCredentials("user");

    // Ask API to create user in DB (returns with ID populated)
    var created = await testControlClient.CreateUsersAsync(new[] { userCreds });

    // Update dictionary with server-populated credentials (including ID)
    var createdUser = created.Single();
    _userCredentials[createdUser.ShortName] = createdUser;
}
```

**Key changes:**
- ❌ Remove `DeleteUsersAsync()` call
- ❌ Remove `_objectStore` pattern
- ✅ Client generates credentials (auto-tracked)
- ✅ API creates and returns with ID
- ✅ Update dictionary with server-populated ID

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
    var friendlyNames = usersTable.ToSingleColumnList().ToList();

    // Generate credentials for all users (auto-tracked)
    var credentialsList = friendlyNames.Select(name => CreateTestUserCredentials(name)).ToList();

    // Create all users in bulk
    var created = await testControlClient.CreateUsersAsync(credentialsList);

    // Update dictionary with server-populated IDs
    foreach (var createdUser in created)
    {
        _userCredentials[createdUser.ShortName] = createdUser;
    }
}
```

**Key changes:**
- ❌ Remove `DeleteAllTestDataAsync()` call
- ✅ Generate credentials client-side (auto-tracked)
- ✅ API creates and returns with IDs
- ✅ Update dictionary with server-populated IDs

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

    // Generate credentials (automatically tracked)
    var user = CreateTestUserCredentials("testuser");

    await registerPage.EnterRegistrationDetailsAsync(user.Email, user.Username, user.Password, user.Password);

    // Note: User will be created by FORM SUBMISSION (not Test Control API)
}
```

**Key changes:**
- ❌ Remove `DeleteUsersAsync()` call
- ❌ Remove `_objectStore` pattern
- ✅ Generate credentials (auto-tracked)
- ✅ No manual tracking needed

**Pattern:** Remove `DeleteUsersAsync()` calls and `_objectStore` usage from all 3 occurrences in `AuthenticationSteps.cs`.

#### Using Stored Credentials in Test Steps

After creating users, retrieve their credentials from the dictionary using the friendly name:

```csharp
[When("I login as {string}")]
protected async Task WhenILoginAs(string friendlyName)
{
    // Retrieve credentials by friendly name
    var userCreds = _userCredentials[friendlyName];

    // Use credentials for login
    var loginPage = GetOrCreateLoginPage();
    await loginPage.LoginAsync(userCreds.Username, userCreds.Password);
}

[Given("{string} creates a workspace called {string}")]
protected async Task GivenUserCreatesWorkspace(string friendlyName, string workspaceName)
{
    // Retrieve user credentials
    var userCreds = _userCredentials[friendlyName];

    // Create workspace for this user
    var workspace = await testControlClient.CreateWorkspaceForUserAsync(
        userCreds.Id,
        new TenantEditDto(workspaceName, "Test workspace")
    );

    // Track for cleanup
    TrackCreatedWorkspace(workspace.Key);
}

[Then("I should see {string}'s email address")]
protected async Task ThenIShouldSeeUsersEmail(string friendlyName)
{
    var userCreds = _userCredentials[friendlyName];
    var profilePage = It<Pages.ProfilePage>();

    var displayedEmail = await profilePage.GetEmailAsync();
    Assert.That(displayedEmail, Is.EqualTo(userCreds.Email));
}
```

**Key insight:** The dictionary provides O(1) lookup by friendly name. Tests reference users by their Gherkin-friendly names ("alice", "bob"), not the unique generated usernames.

### 4. NUnit Configuration for Parallel Execution

#### Assembly-Level Attributes

Create or update `tests/Functional/AssemblyInfo.cs`:

```csharp
using NUnit.Framework;

// Enable parallel execution at the fixture level
[assembly: Parallelizable(ParallelScope.Fixtures)]

// IMPORTANT: Start conservative for SQLite (single writer limitation)
[assembly: LevelOfParallelism(2)]  // Only 2 parallel tests initially

// Can increase cautiously after testing, but SQLite single-writer may limit benefit
// [assembly: LevelOfParallelism(4)]  // Test thoroughly, monitor for "database is locked"
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

### Incremental Migration Approach

This strategy allows old and new patterns to coexist during migration, minimizing risk and allowing gradual rollout.

### Phase 1: Add V2 Endpoints (Non-Breaking)

**Add new endpoints alongside existing ones - no breaking changes:**

1. ✅ **NEW ENDPOINT:** Add `POST /testcontrol/v2/users` that accepts `TestUserCredentials[]`
2. ✅ **KEEP EXISTING:** `POST /testcontrol/users` (accepts `string[]`) continues to work
3. ✅ **NEW ENDPOINT:** Add `DELETE /testcontrol/v2/users` with required body
4. ✅ **KEEP EXISTING:** `DELETE /testcontrol/users` (no body, deletes all) continues to work
5. ✅ Regenerate API client: `pwsh -File ./scripts/Generate-ApiClient.ps1`

**Result:** Both old and new patterns work. Existing tests continue working unchanged.

### Phase 2: Update Infrastructure (Non-Breaking)

**Add new helpers to FunctionalTestBase - existing tests unaffected:**

1. ✅ **MOVE** `_userCredentials` from `WorkspaceTenancySteps` to `FunctionalTestBase`
2. ✅ Keep existing `SetupWorkspaceTenancySteps()` - it still works (clears base class field)
3. ✅ Add `_createdWorkspaces` tracking list to `FunctionalTestBase`
4. ✅ Add `CreateTestUserCredentials()` helper to `FunctionalTestBase`
5. ✅ Add `TrackCreatedWorkspace()` helper to `FunctionalTestBase`
6. ✅ Update `TearDown` with `CleanupTestResourcesAsync()` - only cleans if tracking lists have data

**Key insight:** Moving `_userCredentials` to base class doesn't break `WorkspaceTenancySteps` because:
- It inherits the field from base
- Its existing `SetupWorkspaceTenancySteps()` clears the inherited field
- Other tests that don't use it are unaffected

**Result:** New helpers available. Tests using `_objectStore` still work. Tests using `_userCredentials` in `WorkspaceTenancySteps` still work.

### Phase 3: Migrate Tests One at a Time (Incremental)

**Port tests gradually - unmigrated tests continue working:**

**Pattern for each test:**
1. ✅ Replace `DeleteUsersAsync()` with nothing (or keep temporarily if needed)
2. ✅ Replace `CreateUsersAsync(string[])` with V2 endpoint: `CreateUsers2Async(TestUserCredentials[])`
3. ✅ Replace `_objectStore.Get<TestUserCredentials>()` with `_userCredentials[name]`
4. ✅ Add `TrackCreatedWorkspace()` calls where workspaces are created
5. ✅ Test that individual scenario still works

**Migration order (suggested):**
1. Start with `CommonGivenSteps.GivenIHaveAnExistingAccount` (most common)
2. Then `WorkspaceTenancySteps.GivenTheseUsersExist` (multi-user scenarios)
3. Then `AuthenticationSteps` registration scenarios (3 methods)
4. Then other steps as needed

**Result:** Migrated tests use new pattern. Unmigrated tests still use old pattern. Both work!

### Phase 4: Remove Old Endpoints and Add Validation (Breaking - Only After All Tests Migrated)

**Only after ALL tests are migrated:**

1. ✅ Remove `POST /testcontrol/users` (old endpoint)
2. ✅ Remove `DELETE /testcontrol/users` (old endpoint, no body)
3. ✅ Remove `DELETE /testcontrol/data` (bulk delete) - OR keep for manual cleanup
4. ✅ Rename V2 endpoints to remove `/v2` suffix (optional cleanup)
5. ✅ Add validation to `_objectStore` to prevent credential storage (see below)
6. ✅ Regenerate API client

**Note:** `_objectStore` is kept for other test data types. We only prevent storing credentials in it.

**Add ObjectStore validation to prevent credential misuse:**

```csharp
// In FunctionalTestBase or ObjectStore class
public void Add<T>(T item) where T : class
{
    // Prevent storing credentials in ObjectStore - use _userCredentials dictionary instead
    if (typeof(T) == typeof(TestUserCredentials))
    {
        throw new InvalidOperationException(
            $"TestUserCredentials should not be stored in ObjectStore. " +
            $"Use _userCredentials dictionary instead for proper cleanup and parallel execution support.");
    }

    // ... existing Add logic ...
}

public void Add<T>(string key, T item) where T : class
{
    // Prevent storing credentials in ObjectStore - use _userCredentials dictionary instead
    if (typeof(T) == typeof(TestUserCredentials))
    {
        throw new InvalidOperationException(
            $"TestUserCredentials should not be stored in ObjectStore. " +
            $"Use _userCredentials dictionary instead for proper cleanup and parallel execution support.");
    }

    // ... existing Add logic ...
}
```

**Result:** Clean final API with only new patterns. ObjectStore remains for other test data, but credentials cannot be misused.

### Phase 5: Enable Parallel Execution (Optional)

**Only after migration is complete and working:**

1. ✅ Add NUnit parallel configuration to `AssemblyInfo.cs`
2. ✅ Run tests in parallel and verify no interference
3. ✅ Adjust `LevelOfParallelism` based on system capacity

**Result:** Tests run in parallel! 🎉

## Benefits

### Performance Improvements

**Current (Sequential):**
- 100 tests × 10 seconds each = **1000 seconds (16.7 minutes)**

**With Parallel Execution (2 workers - SQLite safe):**
- Theoretical: 100 tests ÷ 2 workers × 10s = 500s (8.3 min)
- **Actual with SQLite: ~600-700s (10-12 min)** due to write contention
- **1.5-2x faster!** ⚡

**With Parallel Execution (4 workers - risky with SQLite):**
- May not improve beyond 2 workers due to SQLite single-writer limitation
- High risk of "database is locked" errors
- **Not recommended initially**

**Note:** SQLite's single-writer limitation significantly impacts parallel test performance. For better parallelism (4-8 workers), consider migrating functional tests to SQL Server.

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

### Risk: SQLite Write Concurrency Limitations ⚠️ CRITICAL

**Symptom:** Tests fail with "database is locked" errors when running in parallel.

**Root cause:** SQLite only supports **one writer at a time**. Multiple parallel tests writing simultaneously will queue and may timeout.

**SQLite concurrency characteristics:**
- ❌ **Single writer** - Only ONE write transaction active at a time
- ✅ **Multiple readers** - Many concurrent reads are fine
- ⚠️ **Write contention** - Parallel tests writing = queuing/retries/timeouts

**Mitigation strategies:**

**Option 1: Limit Parallelism (Recommended Start)**
```csharp
// Start very conservative for SQLite
[assembly: LevelOfParallelism(2)]  // Only 2 parallel tests
```
- **2 workers**: Safest, minimal write contention, **2x speedup**
- **4 workers**: May work if tests are read-heavy, monitor for locks

**Option 2: Enable Write-Ahead Logging (WAL) Mode**
WAL mode allows concurrent reads during writes:
```
Connection string: "Data Source=test.db;Mode=ReadWriteCreate;Cache=Shared;Journal Mode=WAL"
```
- Reduces lock contention
- Standard recommendation for concurrent SQLite access
- Should be enabled regardless of parallelism level

**Option 3: Increase Busy Timeout**
Give locked transactions more time to complete:
```csharp
// In DbContext OnConfiguring
optionsBuilder.UseSqlite(connectionString, options =>
{
    options.CommandTimeout(30);  // 30 second timeout
});

// Or in connection string
"Data Source=test.db;Busy Timeout=30000"  // milliseconds
```

**Option 4: Switch to PostgreSQL for Functional Tests (Future)**
If SQLite write contention becomes unmanageable:
- ✅ PostgreSQL handles concurrent writes efficiently with MVCC (Multi-Version Concurrency Control)
- ✅ Use Docker container for local development and CI/CD
- ✅ More realistic for production environment (Azure Database for PostgreSQL)
- ✅ Better parallelism support than SQLite (true concurrent writes)
- ❌ Additional infrastructure complexity

**Recommendation:**
1. **Start with `LevelOfParallelism(2)` + WAL mode**
2. **Monitor for "database is locked" errors**
3. **Measure actual speedup** (may be less than 2x due to write queueing)
4. **Consider PostgreSQL** if write contention limits parallel benefit

**Expected performance with SQLite:**
- **Sequential**: 100 tests × 10s = 1000s (16.7 min)
- **2 workers**: ~600-700s (10-12 min) - **1.5-2x faster** (write contention reduces benefit)
- **4 workers**: May not improve further due to SQLite single-writer limitation

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

### PART 1: Add V2 Endpoints (Non-Breaking - Existing Tests Continue Working)

**Add new endpoints alongside existing ones - no breaking changes.**

#### Test Control API - V2 Endpoints
- [ ] Add `POST /testcontrol/v2/users` endpoint that accepts `TestUserCredentials[]`
- [ ] Keep existing `POST /testcontrol/users` (accepts `string[]`) unchanged
- [ ] Add `DELETE /testcontrol/v2/users` endpoint with required body
- [ ] Keep existing `DELETE /testcontrol/users` (no body, deletes all) unchanged
- [ ] Regenerate API client: `pwsh -File ./scripts/Generate-ApiClient.ps1`
- [ ] Verify API client has both old and new methods (e.g., `CreateUsersAsync` and `CreateUsers2Async`)
- [ ] Add integration tests for V2 endpoints

**Result:** Both old and new API patterns work. No tests break.

---

### PART 2: Update Functional Test Infrastructure (Non-Breaking)

**Add new helpers to FunctionalTestBase - existing tests unaffected.**

#### Functional Test Base - Add Helpers
- [ ] **Move** `_userCredentials` dictionary from [`WorkspaceTenancySteps`](tests/Functional/Steps/WorkspaceTenancySteps.cs:28) to [`FunctionalTestBase`](tests/Functional/Infrastructure/FunctionalTestBase.cs)
- [ ] Keep `_userCredentials` declaration in `WorkspaceTenancySteps` for now (will remove later after migration)
- [ ] Keep `SetupWorkspaceTenancySteps()` in `WorkspaceTenancySteps` (still needed for unmigrated tests)
- [ ] Add `_createdWorkspaces` tracking list to `FunctionalTestBase`
- [ ] Add `CreateTestUserCredentials()` helper to `FunctionalTestBase` (generates AND tracks)
- [ ] Add `TrackCreatedWorkspace()` helper method to `FunctionalTestBase`
- [ ] Update `TearDown` with `CleanupTestResourcesAsync()` call
- [ ] Add `CleanupTestResourcesAsync()` implementation (only cleans if tracking lists have data)

**Result:** New helpers available. Existing tests using old pattern continue working.

---

### PART 3: Migrate Tests Incrementally (One at a Time)

**Port tests gradually - unmigrated tests continue working.**

#### Migrate CommonGivenSteps.GivenIHaveAnExistingAccount
- [ ] Update [`CommonGivenSteps.GivenIHaveAnExistingAccount`](tests/Functional/Steps/Common/CommonGivenSteps.cs:53)
  - Remove `DeleteUsersAsync()` call
  - Replace `CreateUsersAsync(string[])` with `CreateUsers2Async(TestUserCredentials[])`
  - Replace `_objectStore.Add(user)` with `_userCredentials[name] = user`
  - Remove `_objectStore.Contains<TestUserCredentials>()` check
- [ ] Run tests that use this step - verify they still pass
- [ ] Search for steps that retrieve credentials with `_objectStore.Get<TestUserCredentials>()` and update to use `_userCredentials["user"]`

#### Migrate WorkspaceTenancySteps.GivenTheseUsersExist
- [ ] Update [`WorkspaceTenancySteps.GivenTheseUsersExist`](tests/Functional/Steps/WorkspaceTenancySteps.cs:127)
  - Remove `DeleteAllTestDataAsync()` call
  - Replace `CreateUsersAsync(string[])` with `CreateUsers2Async(TestUserCredentials[])`
  - Update dictionary population to use server-returned credentials
- [ ] Add `TrackCreatedWorkspace()` calls to workspace creation steps
- [ ] Run workspace tenancy tests - verify they still pass
- [ ] Remove local `_userCredentials` field from `WorkspaceTenancySteps` (now using base class)
- [ ] Remove `SetupWorkspaceTenancySteps()` method (cleanup now in base TearDown)

#### Migrate AuthenticationSteps Registration Scenarios
- [ ] Update [`AuthenticationSteps.WhenIEnterValidRegistrationDetails`](tests/Functional/Steps/AuthenticationSteps.cs:119)
  - Remove `DeleteUsersAsync()` call
  - Remove `_objectStore.Add("Registration Details", user)`
  - Credentials already tracked by `CreateTestUserCredentials()`
- [ ] Update [`AuthenticationSteps.WhenIEnterRegistrationDetailsWithAWeakPassword`](tests/Functional/Steps/AuthenticationSteps.cs:310)
  - Remove `DeleteUsersAsync()` call
  - Remove `_objectStore` pattern
- [ ] Update [`AuthenticationSteps.WhenIEnterRegistrationDetailsWithMismatchedPasswords`](tests/Functional/Steps/AuthenticationSteps.cs:350)
  - Remove `DeleteUsersAsync()` call
  - Remove `_objectStore` pattern
- [ ] Run authentication tests - verify they still pass

#### Migrate Remaining Steps
- [ ] Search for remaining `DeleteUsersAsync()` calls in `tests/Functional/Steps/` and remove
- [ ] Search for remaining `DeleteAllTestDataAsync()` calls and remove
- [ ] Search for `_objectStore.Get<TestUserCredentials>()` and replace with `_userCredentials[name]`
- [ ] Search for `_objectStore.Add` with `TestUserCredentials` and remove (auto-tracked now)
- [ ] Search for `_objectStore.Contains<TestUserCredentials>()` and replace with `_userCredentials.ContainsKey(name)`

#### Testing & Validation (Sequential Only)
- [ ] Run ALL tests sequentially to verify no regression
- [ ] Verify cleanup works (no orphaned accounts in database after test runs)
- [ ] Verify test isolation (each test creates unique users)
- [ ] Confirm no test uses `_objectStore` for credentials anymore

---

### PART 4: Remove Old Endpoints and Add Validation (Breaking - Only After All Tests Migrated)

**Only after ALL tests are migrated and working.**

#### Remove Old API Endpoints
- [ ] Verify ALL tests are migrated (no calls to old `CreateUsersAsync(string[])` or old `DeleteUsersAsync()`)
- [ ] Remove `POST /testcontrol/users` (old endpoint accepting `string[]`)
- [ ] Remove `DELETE /testcontrol/users` (old endpoint with no body)
- [ ] Rename `POST /testcontrol/v2/users` to `POST /testcontrol/users` (remove v2)
- [ ] Rename `DELETE /testcontrol/v2/users` to `DELETE /testcontrol/users` (remove v2)
- [ ] Keep `DELETE /testcontrol/data` for manual cleanup (optional)
- [ ] Regenerate API client: `pwsh -File ./scripts/Generate-ApiClient.ps1`

#### Add ObjectStore Validation
- [ ] Add validation to `ObjectStore.Add<T>(T item)` to throw exception if `T` is `TestUserCredentials`
- [ ] Add validation to `ObjectStore.Add<T>(string key, T item)` to throw exception if `T` is `TestUserCredentials`
- [ ] Run all tests - verify no exceptions thrown (all credentials in `_userCredentials` now)

#### Final Cleanup
- [ ] Remove `_userCredentials` field from `WorkspaceTenancySteps` if not already done
- [ ] Remove `SetupWorkspaceTenancySteps()` method if not already done
- [ ] Verify no test code references `_objectStore` with `TestUserCredentials`

#### Documentation
- [ ] Update [`tests/Functional/TEST-CONTROLS.md`](tests/Functional/TEST-CONTROLS.md)
- [ ] Document new credential management patterns for test authors
- [ ] Document cleanup patterns
- [ ] Document ObjectStore validation rules

**STOP HERE - Consolidation refactoring complete. Tests use best practices for isolation and cleanup.**

---

### PART 2 (OPTIONAL): Page Object Model Enhancement - Credential Encapsulation

**This is a nice-to-have refactoring that reduces coupling and simplifies test step code.**

Currently, test steps retrieve credentials and manually extract properties before calling page object methods:

```csharp
// Current pattern (Tell, Don't Ask violation)
var userCreds = _userCredentials["alice"];
await loginPage.LoginAsync(userCreds.Username, userCreds.Password);  // Step extracts properties
```

**Proposed improvement:** Pass the entire credential object to page methods, let the page extract what it needs:

```csharp
// Improved pattern (Tell, Don't Ask principle)
var userCreds = _userCredentials["alice"];
await loginPage.LoginAsync(userCreds);  // Page extracts properties internally
```

#### Benefits

1. **Reduced coupling** - Steps don't need to know which credential properties each page needs
2. **Easier changes** - If page needs additional properties, no step changes required
3. **Cleaner code** - Less property extraction noise in test steps
4. **Encapsulation** - Page object knows what it needs from credentials

#### Example: LoginPage

**Before:**
```csharp
public async Task LoginAsync(string username, string password)
{
    await UsernameInput.FillAsync(username);
    await PasswordInput.FillAsync(password);
    await SubmitButton.ClickAsync();
}
```

**After:**
```csharp
public async Task LoginAsync(TestUserCredentials credentials)
{
    await UsernameInput.FillAsync(credentials.Username);
    await PasswordInput.FillAsync(credentials.Password);
    await SubmitButton.ClickAsync();
}
```

**Step usage simplifies:**
```csharp
// Before
var userCreds = _userCredentials["alice"];
await loginPage.LoginAsync(userCreds.Username, userCreds.Password);

// After
var userCreds = _userCredentials["alice"];
await loginPage.LoginAsync(userCreds);
```

#### Example: RegisterPage

**Before:**
```csharp
public async Task RegisterAsync(string email, string username, string password)
{
    await EmailInput.FillAsync(email);
    await UsernameInput.FillAsync(username);
    await PasswordInput.FillAsync(password);
    await ConfirmPasswordInput.FillAsync(password);
    await SubmitButton.ClickAsync();
}
```

**After:**
```csharp
public async Task RegisterAsync(TestUserCredentials credentials)
{
    await EmailInput.FillAsync(credentials.Email);
    await UsernameInput.FillAsync(credentials.Username);
    await PasswordInput.FillAsync(credentials.Password);
    await ConfirmPasswordInput.FillAsync(credentials.Password);
    await SubmitButton.ClickAsync();
}
```

#### Implementation Checklist

- [ ] Update `LoginPage.LoginAsync()` to accept `TestUserCredentials`
- [ ] Update `RegisterPage.RegisterAsync()` to accept `TestUserCredentials`
- [ ] Update all step methods that call these page methods
- [ ] Search for other page methods that accept credential properties and refactor similarly
- [ ] Consider adding overloads to maintain backward compatibility during migration

#### Migration Strategy

**Option A: Breaking change (simpler)**
- Update all page methods at once
- Update all calling steps at once
- Single consistent pattern throughout

**Option B: Gradual migration (safer)**
- Add overloaded methods that accept `TestUserCredentials`
- Gradually migrate steps to use new overloads
- Remove old methods once migration complete

**Recommendation:** Option A if Part 1 is already a breaking change. No point in incremental migration if we're already doing a big refactoring.

---

### PART 3: Parallel Execution Experiment (Optional - SQLite May Not Support This Well)

**Only attempt after Part 1 is complete and working. SQLite's single-writer limitation may prevent meaningful parallelization.**

#### NUnit Configuration
- [ ] Create or update [`tests/Functional/AssemblyInfo.cs`](tests/Functional/AssemblyInfo.cs)
- [ ] Add `[assembly: Parallelizable(ParallelScope.Fixtures)]`
- [ ] Start conservative: `[assembly: LevelOfParallelism(2)]`

#### SQLite Configuration
- [ ] Enable WAL mode in connection string: `Journal Mode=WAL`
- [ ] Increase busy timeout: `Busy Timeout=30000`

#### Testing & Validation (Parallel Execution)
- [ ] Run tests in parallel with 2 workers
- [ ] Monitor for "database is locked" errors
- [ ] Measure actual speedup (expect 1.5-2x at best due to SQLite single-writer)
- [ ] Try 4 workers cautiously (may not improve or may cause lock errors)
- [ ] Check for flaky tests due to write contention

#### If SQLite Parallelization Fails
- [ ] Document SQLite limitations in test documentation
- [ ] Consider PostgreSQL migration for better concurrency
- [ ] Keep consolidation refactoring (still valuable for test quality)

#### Documentation (If Parallel Works)
- [ ] Add parallel execution section to [`tests/Functional/README.md`](tests/Functional/README.md)
- [ ] Document SQLite limitations and realistic performance expectations
- [ ] Add troubleshooting guide for "database is locked" errors

## Conclusion

This design enables parallel functional test execution by:

1. **Eliminating shared state** - Each test creates unique accounts
2. **Test-scoped cleanup** - Each test cleans up its own resources
3. **Minimal API changes** - Add individual delete endpoint, keep bulk delete for compatibility
4. **Simple migration** - Remove `DeleteUsersAsync()` calls, add tracking
5. **Performance gains** - 4-8x faster test execution with parallel workers

The approach balances simplicity, reliability, and performance, making it suitable for immediate implementation and long-term maintenance.

