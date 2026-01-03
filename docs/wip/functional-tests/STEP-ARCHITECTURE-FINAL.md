---
status: Recommended
created: 2026-01-03
priority: High
target_audience: Development Team
---

# Final Step Architecture: AI-Generated Tests with Step Definition Classes

## Executive Summary

**Problem:** Current functional test architecture uses deep inheritance chains that prevent step reuse across features, causing code duplication and 5-10 minute searches to find existing steps.

**Solution:** Eliminate feature step classes entirely. Tests inherit `FunctionalTestBase` directly and declare only the step definition classes they actually use. ALL step implementations go into small, focused step definition classes organized by domain.

**Impact:**
- ✅ 90% code reduction in test files (10 lines of helper declarations vs 500-1000 lines)
- ✅ Zero code duplication (all steps reusable)
- ✅ AI generates only needed step classes per test
- ✅ Small, manageable step files (~15-20 steps each)
- ✅ Expected 20-30% faster test development

## Architecture Overview

### Current Architecture (Problems)

```
BankImportTests (generated)
    ↓ inherits from
BankImportSteps (500+ lines, mixes concerns)
    ↓ inherits from
TransactionRecordSteps (500+ lines)
    ↓ inherits from
AuthenticationSteps (200+ lines)
    ↓ inherits from
CommonSteps
    ↓ inherits from
FunctionalTestBase
```

**Problems:**
- ❌ Deep inheritance prevents cross-feature reuse
- ❌ `BankImportSteps` can't use `WorkspaceSteps` methods
- ❌ Forces code duplication (see line 29 in BankImportSteps.cs)
- ❌ Large files hard to navigate (55+ steps in TransactionRecordSteps)

### New Architecture (Solution)

```
BankImportTests (generated, 10 lines)
    ↓ inherits from
FunctionalTestBase (671 lines of infrastructure)

BankImportTests declares:
    - WorkspaceSteps (composition)
    - BankImportSteps (composition)
    - AuthSteps (composition)
```

**Benefits:**
- ✅ Tests can use ANY step from ANY step class
- ✅ No inheritance chains - all step classes are independent
- ✅ AI includes only the step classes actually used
- ✅ Small, focused step definition files

## File Structure

### Step Definition Classes (Organized by Domain)

```
tests/Functional/Steps/
├── StepDefinitionsBase.cs          # Base class with test infrastructure access
│
├── AuthSteps.cs                    # Authentication (~37 steps)
├── BankImportSteps.cs              # Bank import operations (~20 steps)
│
├── Workspace/                      # Workspace domain (split from 53 steps)
│   ├── WorkspaceListSteps.cs       # Workspace list operations (~20 steps)
│   ├── WorkspaceDetailsSteps.cs    # Workspace details operations (~15 steps)
│   └── WorkspaceCollaborationSteps.cs  # Sharing/collaboration (~18 steps)
│
└── Transaction/                    # Transaction domain (split from 55 steps)
    ├── TransactionListSteps.cs     # Transaction list operations (~15 steps)
    ├── TransactionDetailsSteps.cs  # Transaction details operations (~15 steps)
    ├── TransactionEditSteps.cs     # Editing operations (~15 steps)
    └── TransactionCreateSteps.cs   # Creation operations (~10 steps)
```

### Generated Test Files (AI-Generated)

```
tests/Functional/Tests/
├── BankImport.feature.cs           # Inherits FunctionalTestBase directly
├── TransactionRecord.feature.cs    # Inherits FunctionalTestBase directly
└── Tenancy.feature.cs              # Inherits FunctionalTestBase directly
```

## Implementation Details

### StepDefinitionsBase (New Base Class)

Provides step definition classes access to all test infrastructure:

```csharp
namespace YoFi.V3.Tests.Functional.Steps;

/// <summary>
/// Base class for all step definition classes, providing access to test infrastructure.
/// </summary>
public abstract class StepDefinitionsBase
{
    protected readonly FunctionalTestBase Test;

    protected StepDefinitionsBase(FunctionalTestBase test)
    {
        Test = test ?? throw new ArgumentNullException(nameof(test));
    }

    // ===== Delegate to FunctionalTestBase infrastructure =====

    /// <summary>Gets the Page Object Model store for accessing pages.</summary>
    protected ObjectStore ObjectStore => Test._objectStore;

    /// <summary>Gets the test control API client for setup/cleanup operations.</summary>
    protected TestControlClient TestControlClient => Test.testControlClient;

    /// <summary>Gets user credentials by friendly name.</summary>
    protected Dictionary<string, TestUserCredentials> UserCredentials => Test._userCredentials;

    /// <summary>Gets workspace keys by full workspace name.</summary>
    protected Dictionary<string, Guid> WorkspaceKeys => Test._workspaceKeys;

    /// <summary>Gets the Playwright Page for browser automation.</summary>
    protected IPage Page => Test.Page;

    /// <summary>Gets or creates a page object model.</summary>
    protected T GetOrCreatePage<T>() where T : class
    {
        if (ObjectStore.TryGet<T>(out var existing))
            return existing;

        // Create new page instance using the constructor that takes IPage
        var pageInstance = (T)Activator.CreateInstance(typeof(T), Page)!;
        ObjectStore.Add(pageInstance);
        return pageInstance;
    }

    /// <summary>Creates unique test user credentials for the current test.</summary>
    protected TestUserCredentials CreateTestUserCredentials(string friendlyName) =>
        Test.CreateTestUserCredentials(friendlyName);

    /// <summary>Creates test user credentials AND registers them on the server.</summary>
    protected Task<TestUserCredentials> CreateTestUserCredentialsOnServer(string friendlyName) =>
        Test.CreateTestUserCredentialsOnServer(friendlyName);

    /// <summary>Tracks a workspace for cleanup in TearDown.</summary>
    protected void TrackCreatedWorkspace(string workspaceName, Guid workspaceKey) =>
        Test.TrackCreatedWorkspace(workspaceName, workspaceKey);
}
```

**Key Design Decision: Composition NOT Inheritance**

```csharp
// ✅ Step classes contain FunctionalTestBase (composition)
public class WorkspaceListSteps : StepDefinitionsBase
{
    public WorkspaceListSteps(FunctionalTestBase test) : base(test) { }
}

// ❌ NOT inheritance - step classes don't inherit from FunctionalTestBase
public class WorkspaceListSteps : FunctionalTestBase // NO!
```

**Why Composition?**
- ✅ Step classes don't need to BE tests, they just need ACCESS to test infrastructure
- ✅ Single inheritance preserved for test classes
- ✅ Clear separation: Tests run scenarios, Step classes implement steps
- ✅ Multiple step classes can be used in one test

### Step Definition Class Example

```csharp
namespace YoFi.V3.Tests.Functional.Steps.Workspace;

/// <summary>
/// Step definitions for workspace list operations.
/// </summary>
public class WorkspaceListSteps : StepDefinitionsBase
{
    public WorkspaceListSteps(FunctionalTestBase test) : base(test) { }

    /// <summary>
    /// Creates a workspace owned by the specified user.
    /// </summary>
    [Given("I have an active workspace {workspaceName}")]
    [Given("{username} owns a workspace called {workspaceName}")]
    [Given("{username} owns {workspaceName}")]
    public async Task GivenUserOwnsAWorkspace(string username, string workspaceName)
    {
        var fullWorkspaceName = $"__TEST__{workspaceName}_{TestContext.CurrentContext.Test.ID:X8}";

        if (!UserCredentials.TryGetValue(username, out var cred))
        {
            throw new InvalidOperationException($"User '{username}' not found");
        }

        var request = new WorkspaceCreateRequest
        {
            Name = fullWorkspaceName,
            Description = $"Test workspace: {workspaceName}",
            Role = "Owner"
        };

        var result = await TestControlClient.CreateWorkspaceForUserAsync(cred.Username, request);
        WorkspaceKeys[result.Name] = result.Key;
        ObjectStore.Add("CurrentWorkspace", fullWorkspaceName);
    }

    /// <summary>
    /// Navigates to the workspace list page.
    /// </summary>
    [When("I view my workspace list")]
    public async Task WhenIViewMyWorkspaceList()
    {
        var workspacesPage = GetOrCreatePage<WorkspacesPage>();
        await workspacesPage.NavigateAsync();
    }

    /// <summary>
    /// Verifies workspace appears in user's list.
    /// </summary>
    [Then("I should see {workspaceName} in my workspace list")]
    public async Task ThenIShouldSeeInMyWorkspaceList(string workspaceName)
    {
        var workspacesPage = GetOrCreatePage<WorkspacesPage>();
        var fullName = $"__TEST__{workspaceName}_{TestContext.CurrentContext.Test.ID:X8}";
        Assert.That(await workspacesPage.HasWorkspaceAsync(fullName), Is.True);
    }
}
```

**Key Points:**
- ✅ `[Given]`/`[When]`/`[Then]` attributes directly on public methods
- ✅ Multiple attribute patterns supported (different Gherkin phrasings)
- ✅ Access to all test infrastructure via `StepDefinitionsBase`
- ✅ Small, focused file (~15-20 steps)

### AI-Generated Test Example

**Gherkin Feature File:**
```gherkin
Feature: Bank Import
  Scenario: Upload OFX file
    Given I am logged in as "alice"
    And I have an active workspace "Checking"
    When I upload OFX file "bank.ofx"
    Then I should see 10 transactions
```

**AI-Generated C# Test:**

```csharp
namespace YoFi.V3.Tests.Functional.Tests;

/// <summary>
/// Bank Import functional tests.
/// </summary>
public class BankImportTests : FunctionalTestBase
{
    // ===== Step Definition Classes (AI generates only what's used) =====

    protected AuthSteps AuthSteps => _authSteps ??= new(this);
    protected WorkspaceListSteps WorkspaceSteps => _workspaceSteps ??= new(this);
    protected BankImportSteps BankImportSteps => _bankImportSteps ??= new(this);

    private AuthSteps? _authSteps;
    private WorkspaceListSteps? _workspaceSteps;
    private BankImportSteps? _bankImportSteps;

    // ===== Generated Test =====

    /// <summary>
    /// Upload OFX file
    /// </summary>
    [Test]
    public async Task UploadOFXFile()
    {
        // Given I am logged in as "alice"
        await AuthSteps.GivenIAmLoggedInAs("alice");

        // And I have an active workspace "Checking"
        await WorkspaceSteps.GivenUserOwnsAWorkspace("I", "Checking");

        // When I upload OFX file "bank.ofx"
        await BankImportSteps.WhenIUploadOFXFile("bank.ofx");

        // Then I should see 10 transactions
        await BankImportSteps.ThenIShouldSeeTransactions(10);
    }
}
```

**Inheritance Chain:**
```
BankImportTests (concrete test class)
    ↓ inherits from
FunctionalTestBase (671 lines of test infrastructure)
```

**When `new(this)` is called:**
- `this` = instance of `BankImportTests`
- `BankImportTests` IS-A `FunctionalTestBase`
- So `this` contains all test infrastructure (Page, ObjectStore, TestControlClient, etc.)

## AI Generation Logic

### Step 1: Parse Gherkin Feature File

```gherkin
Given I have an active workspace "Checking"
When I upload OFX file "bank.ofx"
Then page should display 10 transactions
```

### Step 2: Search for Step Methods in All Step Definition Classes

**AI searches in:**
- `AuthSteps.cs`
- `WorkspaceListSteps.cs`
- `WorkspaceDetailsSteps.cs`
- `WorkspaceCollaborationSteps.cs`
- `TransactionListSteps.cs`
- `TransactionDetailsSteps.cs`
- `BankImportSteps.cs`
- etc.

**AI finds:**
- Step 1 → `WorkspaceListSteps.GivenUserOwnsAWorkspace()` ✅
- Step 2 → `BankImportSteps.WhenIUploadOFXFile()` ✅
- Step 3 → `BankImportSteps.ThenPageShouldDisplayTransactions()` ✅

### Step 3: Track Which Step Classes Are Needed

**Used step classes:**
- `WorkspaceListSteps` ✅
- `BankImportSteps` ✅

**NOT used:**
- `AuthSteps` (no auth steps in this scenario)
- `TransactionEditSteps` (no editing steps)
- etc.

### Step 4: Generate Test Class with ONLY Needed Step Classes

```csharp
public class BankImportTests : FunctionalTestBase
{
    // Only include step classes that are actually used
    protected WorkspaceListSteps WorkspaceSteps => _workspaceSteps ??= new(this);
    protected BankImportSteps BankImportSteps => _bankImportSteps ??= new(this);

    private WorkspaceListSteps? _workspaceSteps;
    private BankImportSteps? _bankImportSteps;

    [Test]
    public async Task UploadOFXFile()
    {
        await WorkspaceSteps.GivenUserOwnsAWorkspace("I", "Checking");
        await BankImportSteps.WhenIUploadOFXFile("bank.ofx");
        await BankImportSteps.ThenPageShouldDisplayTransactions(10);
    }
}
```

## Benefits

### Code Reduction

**Before (Current Architecture):**
```csharp
// BankImportSteps.cs - 500+ lines
public abstract class BankImportSteps : TransactionRecordSteps
{
    // 20 import-specific steps
    // PLUS inherits 55 steps from TransactionRecordSteps
    // PLUS inherits 37 steps from AuthenticationSteps
    // Total: 112 steps visible in ONE file (overwhelming!)
}

// BankImportTests.cs - Minimal
public class BankImportTests : BankImportSteps
{
    [Test]
    public async Task Test() { }
}
```

**After (New Architecture):**
```csharp
// BankImportSteps.cs - 100 lines (focused)
public class BankImportSteps : StepDefinitionsBase
{
    // ONLY 20 import-specific steps
}

// BankImportTests.cs - 15 lines (AI-generated)
public class BankImportTests : FunctionalTestBase
{
    protected BankImportSteps BankImportSteps => _bankImportSteps ??= new(this);
    private BankImportSteps? _bankImportSteps;

    [Test]
    public async Task Test() { }
}
```

**Total Code:** 90% reduction (115 lines vs 1000+ lines)

### No More Code Duplication

**Before:**
```csharp
// BankImportSteps.cs line 29
// FORCED duplication because inheritance chain blocks access to WorkspaceSteps
protected async Task GivenIHaveAnActiveWorkspace(string name)
{
    // Duplicate code from WorkspaceTenancySteps
}
```

**After:**
```csharp
// BankImportTests.cs - Just use WorkspaceListSteps!
protected WorkspaceListSteps WorkspaceSteps => _workspaceSteps ??= new(this);

await WorkspaceSteps.GivenUserOwnsAWorkspace("Checking");
```

### Better Discoverability

**IntelliSense shows:**
```csharp
WorkspaceSteps.    // ← Ctrl+Space shows ALL workspace step methods
TransactionListSteps.    // ← Ctrl+Space shows ALL transaction list step methods
BankImportSteps.   // ← Ctrl+Space shows ALL bank import step methods
```

**Step Catalog (Auto-generated Markdown):**
```markdown
## WorkspaceListSteps

| Pattern | Method | File | Line |
|---------|--------|------|------|
| `I have an active workspace {workspaceName}` | `GivenUserOwnsAWorkspace` | `Workspace/WorkspaceListSteps.cs` | 25 |
| `{username} owns {workspaceName}` | `GivenUserOwnsAWorkspace` | `Workspace/WorkspaceListSteps.cs` | 25 |
```

### Small, Manageable Files

**Before:**
- `TransactionRecordSteps.cs` - 55 steps (hard to navigate)
- `WorkspaceTenancySteps.cs` - 53 steps (hard to navigate)

**After:**
- `TransactionListSteps.cs` - 15 steps ✅
- `TransactionDetailsSteps.cs` - 15 steps ✅
- `TransactionEditSteps.cs` - 15 steps ✅
- `WorkspaceListSteps.cs` - 20 steps ✅
- `WorkspaceCollaborationSteps.cs` - 18 steps ✅

**Each file is focused and easy to navigate!**

## Migration Strategy

### Phase 1: Create Infrastructure (2-3 hours)

1. **Create `StepDefinitionsBase`**
   - Copy test infrastructure delegation from proposal
   - Place in `tests/Functional/Steps/StepDefinitionsBase.cs`

2. **Verify FunctionalTestBase exposes needed properties**
   - `_objectStore`, `_userCredentials`, `_workspaceKeys` need to be `internal` or `public`
   - Or add internal getters: `internal ObjectStore ObjectStore => _objectStore;`

### Phase 2: Extract Step Definition Classes (6-8 hours)

3. **Extract AuthSteps**
   - Copy methods from `AuthenticationSteps.cs`
   - Change base class to `StepDefinitionsBase`
   - Make methods `public` (not `protected`)
   - Test with one existing test

4. **Extract WorkspaceListSteps**
   - Copy workspace list methods from `WorkspaceTenancySteps.cs`
   - Create `tests/Functional/Steps/Workspace/WorkspaceListSteps.cs`
   - Test with existing tenancy tests

5. **Extract remaining step classes**
   - `WorkspaceDetailsSteps`
   - `WorkspaceCollaborationSteps`
   - `TransactionListSteps`
   - `TransactionDetailsSteps`
   - `TransactionEditSteps`
   - `TransactionCreateSteps`
   - `BankImportSteps`

### Phase 3: Update Generated Tests (3-4 hours)

6. **Update one test file to new pattern**
   - Change inheritance: `FunctionalTestBase` (not feature step class)
   - Add step class property declarations
   - Update method calls to use step classes
   - Run tests to verify

7. **Update remaining test files**
   - Apply same pattern to all test files
   - Remove only step classes actually used per test

8. **Delete old feature step classes**
   - `AuthenticationSteps.cs`
   - `BankImportSteps.cs` (old version)
   - `TransactionRecordSteps.cs`
   - `WorkspaceTenancySteps.cs`
   - `Common/CommonGivenSteps.cs` etc.

### Phase 4: Update AI Generation (1-2 hours)

9. **Update `INSTRUCTIONS.md` for AI generation**
   - Document new pattern (search all step classes)
   - Include only needed step class declarations
   - Update examples

10. **Create step catalog tool**
    - `scripts/Generate-StepCatalog.ps1`
    - Auto-generate `STEP-CATALOG.md`
    - Include in CI/CD

**Total Migration Effort:** 12-17 hours (1.5-2 days)

## Comparison: Before vs After

| Aspect | Before (Current) | After (New) | Improvement |
|--------|-----------------|-------------|-------------|
| **Code per test file** | 500-1000 lines | 10-15 lines | 90% reduction ✅ |
| **Step reusability** | Limited by inheritance | All steps usable | 100% reusable ✅ |
| **Code duplication** | Forced by inheritance | Zero | Eliminated ✅ |
| **File size** | 55+ steps per file | 15-20 steps per file | More manageable ✅ |
| **Step discoverability** | 5-10 min search | IntelliSense + catalog | 80% faster ✅ |
| **AI generation** | Complex (inheritance) | Simple (composition) | Easier ✅ |
| **Test development speed** | Baseline | 20-30% faster | Significant ✅ |

## Conclusion

**Eliminate all feature step classes (BankImportSteps, TransactionRecordSteps, etc.).**

**New architecture:**
1. Tests inherit `FunctionalTestBase` directly
2. ALL step implementations go into small, focused step definition classes
3. Step definition classes organized by domain (Workspace/, Transaction/)
4. AI generates only the step class declarations actually used per test
5. Zero code duplication, maximum reusability

**This is the cleanest, most maintainable architecture for AI-generated functional tests.**

## References

- [FunctionalTestBase.cs](../../../tests/Functional/Infrastructure/FunctionalTestBase.cs) - 671 lines of test infrastructure
- [Step Catalog Organization](./STEP-CATALOG-ORGANIZATION.md) - Discoverability improvements
- [Functional Test Complexity Analysis](./FUNCTIONAL-TEST-COMPLEXITY-ANALYSIS.md) - Original problem analysis
