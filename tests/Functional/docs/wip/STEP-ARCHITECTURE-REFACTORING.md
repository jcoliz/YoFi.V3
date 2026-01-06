---
status: Draft
created: 2026-01-03
priority: High
target_audience: Development Team
---

# Step Inheritance Architecture Refactoring

## The Real Problem

**Current Architecture:** Deep inheritance hierarchy prevents step reuse across features

```
FunctionalTestBase (Infrastructure)
    ↓
CommonGivenSteps → CommonWhenSteps → CommonThenSteps
    ↓
AuthenticationSteps (37 steps)
    ↓
TransactionRecordSteps (55 steps)
    ↓
BankImportSteps (13 steps) ← **Can't access WorkspaceTenancySteps!**
```

**Problem:** BankImportSteps inherits from TransactionRecordSteps, but needs workspace management steps from WorkspaceTenancySteps. **Linear inheritance prevents cross-feature step reuse.**

**Evidence from BankImportSteps.cs:**
```csharp
// Line 29: "Follows same pattern as GivenUserOwnsAWorkspaceCalled
// from WorkspaceTenancySteps"
// But can't call it because we inherit from TransactionRecordSteps!

[Given("I have an active workspace {workspaceName}")]
protected async Task GivenIHaveAnActiveWorkspace(string workspaceName)
{
    // DUPLICATE implementation instead of calling WorkspaceTenancySteps
    // because inheritance hierarchy prevents it!
}
```

## Root Cause Analysis

### Why Linear Inheritance Fails for BDD Steps

**BDD Features are NOT hierarchical** - they're cross-cutting concerns:
- Bank Import needs: Authentication + Workspace + Transaction steps
- Transaction Record needs: Authentication + Workspace steps
- Workspace Tenancy needs: Authentication steps only

**Current forced hierarchy:**
```
You must pick ONE inheritance chain:
- Inherit from TransactionRecordSteps (gets transactions, loses workspace management)
- Inherit from WorkspaceTenancySteps (gets workspace management, loses transactions)
```

**Result:** Code duplication and brittle architecture

## Solution: Composition Over Inheritance

Replace deep inheritance with **step method delegation** using helper classes.

### New Architecture Pattern

```
FunctionalTestBase (Infrastructure only)
    ↓
Feature Test Class
    ├── Uses AuthStepHelper (authentication steps)
    ├── Uses WorkspaceStepHelper (workspace steps)
    ├── Uses TransactionStepHelper (transaction steps)
    └── Feature-specific steps (local methods)
```

### Implementation Pattern

#### Before (Inheritance - BAD)

```csharp
// BankImportSteps.cs
public abstract class BankImportSteps : TransactionRecordSteps // ❌ Locked in!
{
    [Given("I have an active workspace {workspaceName}")]
    protected async Task GivenIHaveAnActiveWorkspace(string workspaceName)
    {
        // Duplicate implementation because can't access WorkspaceTenancySteps
    }
}
```

#### After (Composition - GOOD)

```csharp
// BankImportSteps.cs
public abstract class BankImportSteps : FunctionalTestBase
{
    // Compose instead of inherit
    protected AuthStepHelper AuthSteps => _authSteps ??= new AuthStepHelper(this);
    protected WorkspaceStepHelper WorkspaceSteps => _workspaceSteps ??= new WorkspaceStepHelper(this);
    protected TransactionStepHelper TransactionSteps => _transactionSteps ??= new TransactionStepHelper(this);

    private AuthStepHelper? _authSteps;
    private WorkspaceStepHelper? _workspaceSteps;
    private TransactionStepHelper? _transactionSteps;

    [Given("I have an active workspace {workspaceName}")]
    protected async Task GivenIHaveAnActiveWorkspace(string workspaceName)
    {
        // Delegate to WorkspaceStepHelper - no duplication!
        await WorkspaceSteps.GivenUserOwnsAWorkspaceCalled("I", workspaceName);
    }
}
```

## Refactoring Plan

### Step 1: Create Step Helper Base Class

**File:** `tests/Functional/Steps/Helpers/StepHelperBase.cs`

```csharp
namespace YoFi.V3.Tests.Functional.Steps.Helpers;

/// <summary>
/// Base class for step helper classes that provide reusable step implementations.
/// </summary>
/// <remarks>
/// Step helpers use composition instead of inheritance to provide cross-feature step reuse.
/// Each helper encapsulates a cohesive set of step implementations (auth, workspace, transactions, etc.)
/// that can be used by any test class through delegation.
/// </remarks>
public abstract class StepHelperBase
{
    protected readonly FunctionalTestBase _test;

    protected StepHelperBase(FunctionalTestBase test)
    {
        _test = test;
    }

    // Convenience accessors to test infrastructure
    protected IPage Page => _test.Page;
    protected ObjectStore ObjectStore => _test._objectStore;
    protected Dictionary<string, TestUserCredentials> UserCredentials => _test._userCredentials;
    protected Dictionary<string, Guid> WorkspaceKeys => _test._workspaceKeys;
    protected TestControlClient TestControlClient => _test.testControlClient;

    // Helper methods
    protected T GetOrCreatePage<T>() where T : BasePage
    {
        if (!ObjectStore.Contains<T>())
        {
            var page = (T)Activator.CreateInstance(typeof(T), Page)!;
            ObjectStore.Add(page);
        }
        return ObjectStore.Get<T>();
    }
}
```

### Step 2: Extract Step Helpers

#### Authentication Step Helper

**File:** `tests/Functional/Steps/Helpers/AuthStepHelper.cs`

```csharp
namespace YoFi.V3.Tests.Functional.Steps.Helpers;

/// <summary>
/// Provides reusable authentication-related step implementations.
/// </summary>
public class AuthStepHelper : StepHelperBase
{
    public AuthStepHelper(FunctionalTestBase test) : base(test) { }

    // Move authentication steps here from AuthenticationSteps
    public async Task GivenIAmOnTheLoginPage()
    {
        var loginPage = GetOrCreatePage<LoginPage>();
        await loginPage.NavigateAsync();
        Assert.That(await loginPage.IsOnLoginPageAsync(), Is.True, "Should be on login page");
    }

    public async Task GivenIHaveAnExistingAccount()
    {
        var userCreds = await _test.CreateTestUserCredentialsOnServer("I");
    }

    public async Task GivenIAmLoggedIn()
    {
        await GivenIHaveAnExistingAccount();
        await GivenIAmOnTheLoginPage();
        await WhenILoginWithMyCredentials();
        await ThenIShouldSeeTheHomePage();
    }

    public async Task WhenILoginWithMyCredentials()
    {
        var loginPage = GetOrCreatePage<LoginPage>();
        var testuser = UserCredentials["I"];
        await loginPage.LoginAsync(testuser.Email, testuser.Password);
    }

    public async Task ThenIShouldSeeTheHomePage()
    {
        var homePage = new HomePage(Page);
        await homePage.WaitForPageReadyAsync();
        Assert.That(await homePage.BrochureSection.IsVisibleAsync(), Is.True);
    }

    // ... other auth steps
}
```

#### Workspace Step Helper

**File:** `tests/Functional/Steps/Helpers/WorkspaceStepHelper.cs`

```csharp
namespace YoFi.V3.Tests.Functional.Steps.Helpers;

/// <summary>
/// Provides reusable workspace management step implementations.
/// </summary>
public class WorkspaceStepHelper : StepHelperBase
{
    public WorkspaceStepHelper(FunctionalTestBase test) : base(test) { }

    public async Task GivenUserOwnsAWorkspaceCalled(string shortName, string workspaceName)
    {
        var fullWorkspaceName = $"__TEST__{workspaceName}_{TestContext.CurrentContext.Test.ID:X8}";

        if (!UserCredentials.TryGetValue(shortName, out var cred))
        {
            throw new InvalidOperationException($"User '{shortName}' not found");
        }

        var request = new WorkspaceCreateRequest
        {
            Name = fullWorkspaceName,
            Description = $"Test workspace: {workspaceName}",
            Role = "Owner"
        };

        var result = await TestControlClient.CreateWorkspaceForUserAsync(cred.Username, request);
        WorkspaceKeys[result.Name] = result.Key;
    }

    public async Task WhenIViewMyWorkspaceList()
    {
        var workspacesPage = GetOrCreatePage<WorkspacesPage>();
        await workspacesPage.NavigateAsync();
    }

    // ... other workspace steps
}
```

#### Transaction Step Helper

**File:** `tests/Functional/Steps/Helpers/TransactionStepHelper.cs`

```csharp
namespace YoFi.V3.Tests.Functional.Steps.Helpers;

/// <summary>
/// Provides reusable transaction-related step implementations.
/// </summary>
public class TransactionStepHelper : StepHelperBase
{
    public TransactionStepHelper(FunctionalTestBase test) : base(test) { }

    public async Task GivenIAmOnTheTransactionsPage()
    {
        var transactionsPage = GetOrCreatePage<TransactionsPage>();
        await transactionsPage.NavigateAsync();
    }

    public async Task WhenIClickTheNewTransactionButton()
    {
        var transactionsPage = GetOrCreatePage<TransactionsPage>();
        await transactionsPage.ClickNewTransactionButtonAsync();
    }

    // ... other transaction steps
}
```

### Step 3: Update Feature Step Classes

#### Example: BankImportSteps (Refactored)

```csharp
/// <summary>
/// Step definitions for Bank Import functional tests.
/// </summary>
/// <remarks>
/// Uses composition (step helpers) instead of inheritance to access cross-feature steps.
/// Can now use workspace management steps without duplicating code!
/// </remarks>
public abstract class BankImportSteps : FunctionalTestBase
{
    #region Step Helpers (Composition)

    protected AuthStepHelper AuthSteps => _authSteps ??= new AuthStepHelper(this);
    protected WorkspaceStepHelper WorkspaceSteps => _workspaceSteps ??= new WorkspaceStepHelper(this);
    protected TransactionStepHelper TransactionSteps => _transactionSteps ??= new TransactionStepHelper(this);

    private AuthStepHelper? _authSteps;
    private WorkspaceStepHelper? _workspaceSteps;
    private TransactionStepHelper? _transactionSteps;

    #endregion

    #region Given Steps

    [Given("I have an active workspace {workspaceName}")]
    protected async Task GivenIHaveAnActiveWorkspace(string workspaceName)
    {
        // Delegate to WorkspaceStepHelper - no duplication!
        await WorkspaceSteps.GivenUserOwnsAWorkspaceCalled("I", workspaceName);

        // Set current workspace context for import page
        ObjectStore.Add("CurrentWorkspace", workspaceName);
    }

    [Given("I am on the import review page")]
    protected async Task GivenIAmOnTheImportReviewPage()
    {
        var workspaceName = ObjectStore.Get<string>("CurrentWorkspace");
        var importPage = GetOrCreatePage<ImportPage>();
        await importPage.NavigateAsync();
        await importPage.WorkspaceSelector.SelectWorkspaceAsync(workspaceName);
    }

    // Bank import-specific steps stay here
    [When("I upload OFX file {filename}")]
    protected async Task WhenIUploadOFXFile(string filename)
    {
        var importPage = GetOrCreatePage<ImportPage>();
        await importPage.UploadFileAsync(filename);
        await importPage.WaitForUploadCompleteAsync();
    }

    #endregion
}
```

## Migration Strategy

### Phase 1: Create Infrastructure (2-3 hours)

1. ✅ Create `StepHelperBase` class
2. ✅ Extract `AuthStepHelper` (most used, foundational)
3. ✅ Update one test class to use it (proof of concept)
4. ✅ Run tests to verify no breakage

### Phase 2: Extract Remaining Helpers (4-6 hours)

5. ✅ Extract `WorkspaceStepHelper`
6. ✅ Extract `TransactionStepHelper`
7. ✅ Extract any other cohesive step groups

### Phase 3: Refactor Feature Step Classes (6-8 hours)

8. ✅ Refactor `BankImportSteps` to use helpers
9. ✅ Refactor `TransactionRecordSteps` to use helpers
10. ✅ Refactor `WorkspaceTenancySteps` to use helpers
11. ✅ Refactor `AuthenticationSteps` to use helpers
12. ✅ Remove duplicate step implementations

### Phase 4: Cleanup (2 hours)

13. ✅ Remove now-empty inheritance chain
14. ✅ Update documentation
15. ✅ Run full test suite

**Total Effort:** 14-19 hours (1.5-2.5 days)

## Benefits

### ✅ Cross-Feature Step Reuse

```csharp
// BankImportSteps can now use workspace steps!
await WorkspaceSteps.GivenUserOwnsAWorkspaceCalled("I", workspaceName);

// TransactionRecordSteps can use auth and workspace steps
await AuthSteps.GivenIAmLoggedIn();
await WorkspaceSteps.GivenUserOwnsAWorkspaceCalled("I", "MyWorkspace");
await TransactionSteps.WhenIAddATransaction();
```

### ✅ No Code Duplication

**Before:** Each step class duplicates common patterns
**After:** Single source of truth in step helpers

### ✅ Clear Dependencies

```csharp
// Explicitly declare what helpers you need
protected AuthStepHelper AuthSteps { get; }
protected WorkspaceStepHelper WorkspaceSteps { get; }

// Instead of unclear inheritance chain
```

### ✅ Easier Testing

```csharp
// Can mock/stub individual helpers in unit tests if needed
var mockAuthSteps = new Mock<AuthStepHelper>();
```

### ✅ Better Discoverability

```csharp
// IntelliSense shows available helpers
AuthSteps.      // Ctrl+Space shows all auth steps
WorkspaceSteps. // Ctrl+Space shows all workspace steps
TransactionSteps. // Ctrl+Space shows all transaction steps
```

## Alternative Considered: Mixins/Traits

**C# doesn't support mixins**, but we could use interfaces + extension methods:

```csharp
public interface IAuthSteps { }
public static class AuthStepsExtensions
{
    public static async Task GivenIAmLoggedIn(this IAuthSteps steps) { }
}

public class BankImportSteps : FunctionalTestBase, IAuthSteps, IWorkspaceSteps { }
```

**Why Not:**
- Extension methods can't access protected members
- Less clear than explicit helper composition
- Harder to maintain state

**Verdict:** Composition via helpers is cleaner for C#.

## Migration Example: Before and After

### Before (Current - Broken)

```csharp
// BankImportSteps.cs - Can't access WorkspaceTenancySteps!
public abstract class BankImportSteps : TransactionRecordSteps
{
    [Given("I have an active workspace {workspaceName}")]
    protected async Task GivenIHaveAnActiveWorkspace(string workspaceName)
    {
        // ❌ DUPLICATE CODE - copy-pasted from WorkspaceTenancySteps
        // because we inherit from TransactionRecordSteps instead!
        var fullWorkspaceName = AddTestPrefix(workspaceName);
        var cred = _userCredentials["I"];
        var request = new WorkspaceCreateRequest { ... };
        var result = await testControlClient.CreateWorkspaceForUserAsync(...);
        _workspaceKeys[result.Name] = result.Key;
    }
}
```

### After (Refactored - Clean)

```csharp
// BankImportSteps.cs - Uses composition!
public abstract class BankImportSteps : FunctionalTestBase
{
    protected WorkspaceStepHelper WorkspaceSteps =>
        _workspaceSteps ??= new WorkspaceStepHelper(this);
    private WorkspaceStepHelper? _workspaceSteps;

    [Given("I have an active workspace {workspaceName}")]
    protected async Task GivenIHaveAnActiveWorkspace(string workspaceName)
    {
        // ✅ REUSE - delegates to single source of truth
        await WorkspaceSteps.GivenUserOwnsAWorkspaceCalled("I", workspaceName);
        ObjectStore.Add("CurrentWorkspace", workspaceName);
    }
}
```

## Impact on Step Attribute Matching

**No change!** Step attributes remain on the public methods in feature step classes:

```csharp
// BankImportSteps.cs
[Given("I have an active workspace {workspaceName}")]  // ← Step attribute stays here
protected async Task GivenIHaveAnActiveWorkspace(string workspaceName)
{
    // Implementation delegates to helper
    await WorkspaceSteps.GivenUserOwnsAWorkspaceCalled("I", workspaceName);
}
```

**Generated tests still call:** `await GivenIHaveAnActiveWorkspace(workspaceName);`

**Internal delegation is hidden** from test generation and step matching.

## Comparison to Industry Patterns

| Framework | Pattern | YoFi.V3 Current | YoFi.V3 Proposed |
|-----------|---------|-----------------|------------------|
| **SpecFlow** | Step definition classes can be independent | Deep inheritance | Composition ✅ |
| **Cucumber (Ruby)** | Steps in separate files, no inheritance | Deep inheritance | Composition ✅ |
| **Behave (Python)** | Context object passed around | Deep inheritance | Composition ✅ |
| **Playwright (TypeScript)** | Fixtures and Page Objects | Deep inheritance | Composition ✅ |

**Industry Standard:** Composition over inheritance for test steps.

**YoFi.V3:** Currently using inheritance (antipattern) → Refactor to composition.

## Risks and Mitigation

### Risk: Breaking Existing Tests

**Mitigation:**
- Incremental migration (one helper at a time)
- Keep both patterns working during transition
- Run full test suite after each step

### Risk: Increased Complexity

**Mitigation:**
- Clear naming: `AuthSteps`, `WorkspaceSteps`, `TransactionSteps`
- Good documentation
- VS Code IntelliSense guides developers

### Risk: Performance (Creating Helpers)

**Mitigation:**
- Lazy initialization (`??=` pattern)
- Helpers are lightweight (just delegation)
- No measurable performance impact

## Conclusion

The deep inheritance hierarchy is **the root cause** of:
- ❌ Code duplication (can't reuse steps across features)
- ❌ Poor discoverability (deep chains hard to navigate)
- ❌ Brittle architecture (changing one class breaks descendants)

**Solution:** Refactor to composition-based architecture using step helpers.

**Expected Benefits:**
- ✅ Cross-feature step reuse (no more duplication)
- ✅ Better discoverability (clear helper groupings)
- ✅ Flexible architecture (mix and match helpers as needed)
- ✅ Easier maintenance (change in one place affects all users)

**Effort:** 14-19 hours (1.5-2.5 days)

**ROI:** Saves 10-20 minutes per feature test + eliminates code duplication

This refactoring addresses your **actual pain point** and follows industry best practices for BDD test organization.

## References

- [`tests/Functional/Steps/BankImportSteps.cs`](../../tests/Functional/Steps/BankImportSteps.cs) - Current inheritance problem (line 17)
- [`tests/Functional/Steps/Common/CommonGivenSteps.cs`](../../tests/Functional/Steps/Common/CommonGivenSteps.cs) - Current common steps
- Martin Fowler: [Composition vs Inheritance](https://martinfowler.com/bliki/CompositionVersusInheritance.html)
- Gang of Four: Prefer composition over inheritance (Design Patterns book)
