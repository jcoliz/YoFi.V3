---
status: Draft
created: 2026-01-03
priority: High - Advanced Pattern
target_audience: Development Team
---

# Advanced Step Architecture: Direct Helper Invocation

## The Idea: Eliminate the Wrapper Layer

**Your Question:** Could we put `[Given]` attributes directly on helper methods and call them from generated tests?

**Answer:** YES! This would be even cleaner than the intermediate wrapper approach.

## Pattern Comparison

### Option A: Wrapper Methods (Original Proposal)

```csharp
// Helper class (no attributes)
public class WorkspaceStepHelper : StepHelperBase
{
    public async Task GivenUserOwnsAWorkspaceCalled(string shortName, string workspaceName)
    {
        // Implementation
    }
}

// Feature step class (wrapper with attribute)
public abstract class BankImportSteps : FunctionalTestBase
{
    protected WorkspaceStepHelper WorkspaceSteps => _workspaceSteps ??= new(this);

    [Given("I have an active workspace {workspaceName}")]
    protected async Task GivenIHaveAnActiveWorkspace(string workspaceName)
    {
        await WorkspaceSteps.GivenUserOwnsAWorkspaceCalled("I", workspaceName);
    }
}

// Generated test
public class BankImportTests : BankImportSteps
{
    [Test]
    public async Task ImportOFXFile()
    {
        await GivenIHaveAnActiveWorkspace("MyWorkspace"); // ← Calls wrapper
    }
}
```

**Problem:** Still have wrapper methods in each feature class.

### Option B: Direct Helper Invocation (Your Idea!) ✅

```csharp
// Helper class (with attributes!)
public class WorkspaceStepHelper : StepHelperBase
{
    [Given("I have an active workspace {workspaceName}")]
    [Given("{username} owns a workspace called {workspaceName}")]
    [Given("{username} owns {workspaceName}")]
    public async Task GivenUserOwnsAWorkspaceCalled(string username, string workspaceName)
    {
        // Implementation
    }
}

// Feature step class (exposes helpers only)
public abstract class BankImportSteps : FunctionalTestBase
{
    protected WorkspaceStepHelper WorkspaceSteps => _workspaceSteps ??= new(this);
    private WorkspaceStepHelper? _workspaceSteps;

    // NO wrapper methods needed! Helper methods are the steps!
}

// Generated test (calls helper directly!)
public class BankImportTests : BankImportSteps
{
    [Test]
    public async Task ImportOFXFile()
    {
        // ✅ Calls helper method directly - no wrapper!
        await WorkspaceSteps.GivenUserOwnsAWorkspaceCalled("I", "MyWorkspace");
    }
}
```

**Benefits:**
- ✅ No wrapper layer
- ✅ Single source of truth (attribute + implementation in same place)
- ✅ Generated tests call helpers directly
- ✅ IntelliSense works perfectly

## Step Helper Base Class

### What Goes in StepHelperBase?

`StepHelperBase` provides helpers access to all the test infrastructure from [`FunctionalTestBase`](../../../tests/Functional/Infrastructure/FunctionalTestBase.cs):

```csharp
namespace YoFi.V3.Tests.Functional.Steps.Helpers;

/// <summary>
/// Base class for all step helper classes, providing access to test infrastructure.
/// </summary>
public abstract class StepHelperBase
{
    protected readonly FunctionalTestBase Test;

    protected StepHelperBase(FunctionalTestBase test)
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

    /// <summary>Gets or creates a page object model.</summary>
    protected T GetOrCreatePage<T>() where T : class
    {
        if (ObjectStore.TryGet<T>(out var existing))
            return existing;

        // Create new page instance using the constructor that takes IPage
        var page = Test.Page;
        var pageInstance = (T)Activator.CreateInstance(typeof(T), page)!;
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

### Key Design Decisions

**1. Pass `FunctionalTestBase` to Constructor**

Helpers need access to:
- ✅ `ObjectStore` - For page object models
- ✅ `TestControlClient` - For test data setup via API
- ✅ `Page` - For creating page objects
- ✅ `UserCredentials` - For tracking created users
- ✅ `WorkspaceKeys` - For tracking created workspaces

**2. Protected Properties (Not Inheritance)**

```csharp
// ✅ Composition - Helper contains FunctionalTestBase
protected readonly FunctionalTestBase Test;

// ❌ NOT inheritance - Helper is NOT a FunctionalTestBase
public class WorkspaceStepHelper : FunctionalTestBase // NO!
```

**3. Delegate Common Operations**

Provide shortcuts to common operations so helpers don't need `Test.` prefix:

```csharp
// ✅ Clean syntax in helper methods
var loginPage = GetOrCreatePage<LoginPage>();

// ❌ Without delegation
var loginPage = Test.GetOrCreatePage<LoginPage>(); // More verbose
```

### FunctionalTestBase (No Changes Needed!)

[`FunctionalTestBase`](../../../tests/Functional/Infrastructure/FunctionalTestBase.cs) already provides everything helpers need:

**Key Infrastructure (671 lines):**
- ✅ Playwright `Page` and `Context`
- ✅ `ObjectStore` for page models
- ✅ `TestControlClient` for test control API
- ✅ Test correlation headers (distributed tracing)
- ✅ User credential tracking and cleanup
- ✅ Workspace tracking and cleanup
- ✅ Screenshot capture on failure
- ✅ Browser console log capture

**No changes needed** - just expose via `StepHelperBase`.

## Implementation Details

### Step Helper with Attributes

```csharp
namespace YoFi.V3.Tests.Functional.Steps.Helpers;

/// <summary>
/// Provides reusable workspace management step implementations.
/// </summary>
/// <remarks>
/// Step attributes are directly on public methods, allowing generated tests
/// to call helper methods without wrapper layers.
/// </remarks>
public class WorkspaceStepHelper : StepHelperBase
{
    public WorkspaceStepHelper(FunctionalTestBase test) : base(test) { }

    /// <summary>
    /// Creates a workspace owned by the specified user.
    /// </summary>
    [Given("I have an active workspace {workspaceName}")]
    [Given("{username} owns a workspace called {workspaceName}")]
    [Given("{username} owns {workspaceName}")]
    public async Task GivenUserOwnsAWorkspaceCalled(string username, string workspaceName)
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

### Feature Step Class (Just a Container for Helpers!)

```csharp
namespace YoFi.V3.Tests.Functional.Steps;

/// <summary>
/// Step definitions for Bank Import functional tests.
/// </summary>
/// <remarks>
/// Provides access to step helpers. ALL step implementations are in helpers
/// to enable maximum reusability across features.
/// </remarks>
public abstract class BankImportSteps : FunctionalTestBase
{
    #region Step Helpers

    protected AuthStepHelper AuthSteps => _authSteps ??= new(this);
    protected WorkspaceStepHelper WorkspaceSteps => _workspaceSteps ??= new(this);
    protected TransactionStepHelper TransactionSteps => _transactionSteps ??= new(this);
    protected BankImportStepHelper BankImportSteps => _bankImportSteps ??= new(this);

    private AuthStepHelper? _authSteps;
    private WorkspaceStepHelper? _workspaceSteps;
    private TransactionStepHelper? _transactionSteps;
    private BankImportStepHelper? _bankImportSteps;

    #endregion

    // NO step methods here! Everything is in helpers!
}
```

### Bank Import Step Helper (All Steps in Helper)

```csharp
namespace YoFi.V3.Tests.Functional.Steps.Helpers;

/// <summary>
/// Provides bank import step implementations.
/// </summary>
public class BankImportStepHelper : StepHelperBase
{
    public BankImportStepHelper(FunctionalTestBase test) : base(test) { }

    /// <summary>
    /// Uploads an OFX file from sample data.
    /// </summary>
    [When("I upload OFX file {filename}")]
    public async Task WhenIUploadOFXFile(string filename)
    {
        var importPage = GetOrCreatePage<ImportPage>();
        await importPage.UploadFileAsync(filename);
        await importPage.WaitForUploadCompleteAsync();
    }

    /// <summary>
    /// Navigates to the import review page.
    /// </summary>
    [Given("I am on the import review page")]
    public async Task GivenIAmOnTheImportReviewPage()
    {
        var workspaceName = ObjectStore.Get<string>("CurrentWorkspace");
        var importPage = GetOrCreatePage<ImportPage>();
        await importPage.NavigateAsync();
        await importPage.WorkspaceSelector.SelectWorkspaceAsync(workspaceName);
    }

    /// <summary>
    /// Verifies expected transaction count on import review page.
    /// </summary>
    [Then("page should display {count} transactions")]
    public async Task ThenPageShouldDisplayTransactions(int count)
    {
        var importPage = GetOrCreatePage<ImportPage>();
        var actualCount = await importPage.GetTransactionCountAsync();
        Assert.That(actualCount, Is.EqualTo(count));
    }

    /// <summary>
    /// Verifies duplicate detection worked correctly.
    /// </summary>
    [Then("{count} transactions should be deselected by default")]
    public async Task ThenTransactionsShouldBeDeselectedByDefault(int count)
    {
        var importPage = GetOrCreatePage<ImportPage>();
        var actualCount = await importPage.GetDeselectedCountAsync();
        Assert.That(actualCount, Is.EqualTo(count));
    }
}
```

### Generated Test (All Helpers!)

```csharp
/// <summary>
/// Import OFX bank statement file
/// </summary>
[Test]
public async Task ImportOFXBankStatementFile()
{
    // Given I have an active workspace "Checking"
    await WorkspaceSteps.GivenUserOwnsAWorkspaceCalled("I", "Checking");

    // And I am on the import review page
    await BankImportSteps.GivenIAmOnTheImportReviewPage();

    // When I upload OFX file "checking-jan-2024.ofx"
    await BankImportSteps.WhenIUploadOFXFile("checking-jan-2024.ofx");

    // Then page should display 10 transactions
    await BankImportSteps.ThenPageShouldDisplayTransactions(10);
}
```

**Key Insight:** Today's "bank import specific" step might be tomorrow's "transaction import" step used by multiple features!

## Gherkin-to-C# Mapping

### AI Instructions Would Change

**Current Instructions:**
```
Find method in `@baseclass` file whose [Given] attribute matches the step text
```

**New Instructions:**
```
1. Search in the feature step class for methods with matching [Given] attribute
2. If not found, search in step helper properties (AuthSteps, WorkspaceSteps, etc.)
3. If found in helper, call: await {HelperProperty}.{MethodName}(params)
4. If not found anywhere, report missing step method
```

### Example Mapping

**Gherkin:**
```gherkin
Given I have an active workspace "Checking"
```

**AI Mapping Process:**
1. Search in `BankImportSteps` for `[Given("I have an active workspace {workspaceName}")]` - NOT FOUND
2. Check helpers: `AuthSteps`, `WorkspaceSteps`, `TransactionSteps`
3. Search in `WorkspaceStepHelper` for pattern - FOUND: `GivenUserOwnsAWorkspaceCalled`
4. Generate: `await WorkspaceSteps.GivenUserOwnsAWorkspaceCalled("I", "Checking");`

## Advantages Over Wrapper Pattern

### ✅ Less Code

**Wrapper Pattern:**
- Helper: 50 lines (no attributes)
- Feature class: 50 lines (wrappers with attributes)
- **Total: 100 lines**

**Direct Pattern:**
- Helper: 50 lines (with attributes)
- Feature class: 10 lines (just expose helpers)
- **Total: 60 lines (40% reduction!)**

### ✅ Single Source of Truth

**Attribute and implementation in same place:**
```csharp
[Given("{username} owns {workspaceName}")]
public async Task GivenUserOwnsAWorkspaceCalled(string username, string workspaceName)
{
    // Implementation right here
}
```

No need to search multiple files to understand a step.

### ✅ Better for Refactoring

Change attribute pattern in ONE place (the helper), not in every feature class that wraps it.

### ✅ IntelliSense Works Better

```csharp
WorkspaceSteps. // ← Ctrl+Space shows ALL workspace step methods
                // with their [Given] attributes as XML doc comments
```

## Potential Challenges

### Challenge 1: Feature-Specific Parameter Defaults

**Problem:** Sometimes you want different default parameters per feature.

**Example:**
```gherkin
# In WorkspaceTenancy feature
Given "alice" owns "Personal"

# In BankImport feature (current user implied)
Given I have an active workspace "Checking"
```

**Solution:** Multiple attribute patterns on same method:
```csharp
[Given("I have an active workspace {workspaceName}")]
[Given("{username} owns {workspaceName}")]
public async Task GivenUserOwnsAWorkspaceCalled(string username = "I", string workspaceName)
{
    // "I" is default for username
}
```

### Challenge 2: Method Visibility

**Problem:** Should helper methods be `public` or `protected`?

**Solution:** Make them `public` since they're called from generated test classes:
```csharp
public class WorkspaceStepHelper : StepHelperBase
{
    public async Task GivenUserOwnsAWorkspaceCalled(...) // ← public!
}
```

### Challenge 3: Step Attribute Discoverability

**Problem:** How do developers know which helper has which steps?

**Solution:** Enhanced step catalog includes helper location:
```markdown
| Pattern | Method | Location | Line |
|---------|--------|----------|------|
| `{username} owns {workspaceName}` | `GivenUserOwnsAWorkspaceCalled` | `WorkspaceSteps.WorkspaceStepHelper` | 25 |
```

## Migration Strategy (Updated)

### Phase 1: Proof of Concept (2-3 hours)

1. Create `StepHelperBase`
2. Create `AuthStepHelper` with `[Given]` attributes on methods
3. Update `AuthenticationSteps` to expose `AuthSteps` property
4. Update ONE generated test to call `AuthSteps.GivenIAmLoggedIn()` directly
5. Verify it works

### Phase 2: Extract All Helpers (4-6 hours)

6. Extract `WorkspaceStepHelper` with attributes
7. Extract `TransactionStepHelper` with attributes
8. Extract other cohesive groups

### Phase 3: Update Feature Classes (3-4 hours)

9. Remove wrapper methods from `BankImportSteps`
10. Remove wrapper methods from other feature classes
11. Keep only feature-specific steps in feature classes

### Phase 4: Update Generation Instructions (1-2 hours)

12. Update `INSTRUCTIONS.md` to support helper method lookup
13. Test AI generation with new pattern
14. Document helper search priority

**Total Effort:** 10-15 hours (1-2 days) - **Faster than wrapper pattern!**

## Comparison: Wrapper vs Direct

| Aspect | Wrapper Pattern | Direct Pattern | Winner |
|--------|----------------|----------------|--------|
| **Lines of code** | 100 per feature | 60 per feature | Direct ✅ |
| **Discoverability** | Good (IntelliSense) | Great (IntelliSense) | Direct ✅ |
| **Single source of truth** | No (attribute in wrapper) | Yes (attribute in helper) | Direct ✅ |
| **Migration effort** | 14-19 hours | 10-15 hours | Direct ✅ |
| **Refactoring ease** | Medium | Easy | Direct ✅ |
| **AI generation complexity** | Simple | Medium | Wrapper ⚠️ |

**Verdict:** Direct pattern is better IF AI can handle helper method lookup.

## AI Generation Example

### Feature File

```gherkin
Given I have an active workspace "Checking"
When I upload OFX file "checking-jan-2024.ofx"
Then page should display 10 transactions
```

### AI Prompt (Updated)

```
For each Gherkin step:
1. Search BankImportSteps for matching [Given/When/Then] attribute
2. If found, generate: await {MethodName}(params);
3. If not found, search step helpers:
   - Check WorkspaceSteps (WorkspaceStepHelper)
   - Check AuthSteps (AuthStepHelper)
   - Check TransactionSteps (TransactionStepHelper)
4. If found in helper, generate: await {HelperName}.{MethodName}(params);
5. If not found, add comment: // TODO: Missing step method
```

### Generated Test

```csharp
[Test]
public async Task ImportOFXBankStatementFile()
{
    // Given I have an active workspace "Checking"
    await WorkspaceSteps.GivenUserOwnsAWorkspaceCalled("I", "Checking");

    // When I upload OFX file "checking-jan-2024.ofx"
    await WhenIUploadOFXFile("checking-jan-2024.ofx");

    // Then page should display 10 transactions
    await ThenPageShouldDisplayTransactions(10);
}
```

## Recommendation

### 🎯 Use Direct Helper Invocation Pattern ✅

**Reasons:**
1. ✅ Less code (40% reduction vs wrapper pattern)
2. ✅ Single source of truth (attribute + implementation together)
3. ✅ Easier refactoring (change in one place)
4. ✅ Better discoverability (IntelliSense shows all helpers)
5. ✅ Faster migration (10-15 hours vs 14-19 hours)

**Only downside:** AI must search helpers as well as feature classes (medium complexity, easily solvable)

### Implementation Order

1. **Immediate:** Create proof of concept with `AuthStepHelper`
2. **Week 1:** Extract all step helpers with attributes
3. **Week 2:** Update generated tests to call helpers directly
4. **Week 3:** Remove wrapper methods, cleanup

## Conclusion

**Your idea is BETTER than the original proposal!**

By putting step attributes directly on helper methods, you:
- Eliminate an entire layer of wrapper code
- Create a true single source of truth
- Make the architecture simpler and more maintainable

The only requirement is updating the AI generation instructions to search helpers, which is straightforward.

**Recommendation:** Go with the **Direct Helper Invocation** pattern. It's cleaner, simpler, and more maintainable long-term.

## References

- [Original refactoring proposal](./STEP-ARCHITECTURE-REFACTORING.md) - Wrapper pattern
- **This document** - Direct helper invocation (BETTER!)
- Gang of Four: Prefer composition over inheritance
- SOLID Principles: Single Responsibility (helpers are cohesive units)
