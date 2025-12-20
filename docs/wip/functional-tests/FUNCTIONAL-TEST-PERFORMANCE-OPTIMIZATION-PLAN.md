# Functional Test Performance Optimization Plan

## Overview

This plan provides actionable steps to reduce functional test execution time from **2.5s/test to ~0.3-1.0s/test** (60-88% improvement).

Target: Match or beat reference performance of 1.6s/test, with aggressive optimizations achieving 0.3s/test.

## Optimization Phases

### Phase 1: Quick Wins (HIGH Priority, LOW Effort)

**Target:** Reduce test time to ~1.2s/test
**Effort:** 2-4 hours
**Risk:** Low

#### 1.1 Eliminate Arbitrary Task.Delay Calls

**Locations:**
- [`CommonThenSteps.cs:64`](../../tests/Functional/Steps/Common/CommonThenSteps.cs#L64)
- [`LoginPage.cs:122`](../../tests/Functional/Pages/LoginPage.cs#L122)
- [`RegisterPage.cs:86`](../../tests/Functional/Pages/RegisterPage.cs#L86)

**Changes:**

**File:** `tests/Functional/Steps/Common/CommonThenSteps.cs`
```csharp
// BEFORE
protected override async Task ThenIShouldSeeTheHomePage()
{
    await Task.Delay(1000);  // ❌ Remove this
    Assert.That(Page.Url.EndsWith('/'), Is.True, "Should be on home page");
}

// AFTER
protected override async Task ThenIShouldSeeTheHomePage()
{
    // Wait for navigation to complete
    await Page.WaitForURLAsync(url => url.EndsWith('/'), new() { Timeout = 5000 });
    Assert.That(Page.Url.EndsWith('/'), Is.True, "Should be on home page");
}
```

**File:** `tests/Functional/Pages/LoginPage.cs`
```csharp
// BEFORE
public async Task ClickLoginButtonWithoutApiWaitAsync()
{
    await SaveScreenshotAsync("Before-login-attempt");
    await LoginButton.ClickAsync();
    await Task.Delay(500);  // ❌ Remove this
}

// AFTER
public async Task ClickLoginButtonWithoutApiWaitAsync()
{
    await SaveScreenshotAsync("Before-login-attempt");
    await LoginButton.ClickAsync();
    // Wait for error display or validation message to appear
    await ErrorDisplay.Or(Page.Locator("[data-invalid]")).WaitForAsync(new() {
        State = WaitForSelectorState.Visible,
        Timeout = 2000
    });
}
```

**File:** `tests/Functional/Pages/RegisterPage.cs`
```csharp
// BEFORE
public async Task ClickRegisterButtonWithoutApiWaitAsync()
{
    await SaveScreenshotAsync("Before-registration-attempt");
    await RegisterButton.ClickAsync();
    await Task.Delay(500);  // ❌ Remove this
}

// AFTER
public async Task ClickRegisterButtonWithoutApiWaitAsync()
{
    await SaveScreenshotAsync("Before-registration-attempt");
    await RegisterButton.ClickAsync();
    // Wait for error display or validation message to appear
    await ErrorDisplay.Or(Page.Locator("[data-invalid]")).WaitForAsync(new() {
        State = WaitForSelectorState.Visible,
        Timeout = 2000
    });
}
```

**Expected Savings:** 800-1000ms per test in login flows, 300-500ms in validation tests

#### 1.2 Enable Parallel Test Execution

**Locations:**
- [`local.runsettings`](../../tests/Functional/local.runsettings)
- [`docker.runsettings`](../../tests/Functional/docker.runsettings)

**Changes:**

**Both files:**
```xml
<!-- BEFORE -->
<RunConfiguration>
    <MaxCpuCount>1</MaxCpuCount>
</RunConfiguration>
<NUnit>
    <NumberOfTestWorkers>1</NumberOfTestWorkers>
</NUnit>

<!-- AFTER -->
<RunConfiguration>
    <MaxCpuCount>0</MaxCpuCount>  <!-- 0 = use all available cores -->
</RunConfiguration>
<NUnit>
    <NumberOfTestWorkers>4</NumberOfTestWorkers>  <!-- Or match CPU cores -->
</NUnit>
```

**Additional Requirements:**
- Ensure test isolation (no shared state between tests) ✅ Already achieved via `GivenIHaveAnExistingAccount()`
- Each test creates its own user via Test Control API ✅ Already implemented
- Browser contexts are isolated per test ✅ Playwright PageTest handles this

**Expected Improvement:** 2-4x throughput (wall-clock time reduction)

**Testing:** Run tests locally with `--workers=4` flag to verify no conflicts

#### 1.3 Reduce Default Timeouts

**Locations:**
- [`local.runsettings:6`](../../tests/Functional/local.runsettings#L6)
- [`docker.runsettings:6`](../../tests/Functional/docker.runsettings#L6)

**Changes:**

**local.runsettings:**
```xml
<!-- BEFORE -->
<Parameter name="defaultTimeout" value="12000" />

<!-- AFTER -->
<Parameter name="defaultTimeout" value="5000" />  <!-- 5 seconds is sufficient for local dev -->
```

**docker.runsettings:**
```xml
<!-- BEFORE -->
<Parameter name="defaultTimeout" value="6000" />

<!-- AFTER -->
<Parameter name="defaultTimeout" value="5000" />  <!-- Consistent with local -->
```

**Expected Impact:** Minimal for passing tests, but failures will be detected faster

---

### Phase 2: NetworkIdle Elimination (HIGH Priority, MEDIUM Effort)

**Target:** Reduce test time to ~0.3-0.5s/test
**Effort:** 6-10 hours
**Risk:** Medium (requires careful testing to ensure stability)

#### Strategy: Replace NetworkIdle with Specific Element Waits

**Pattern to Apply:**

```csharp
// ❌ BAD: Wait for entire network to be idle
await Page.GotoAsync("/profile");
await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

// ✅ GOOD: Wait for specific element that indicates page is ready
await Page.GotoAsync("/profile");
await Page.Locator("[data-test-id='profile-content']").WaitForAsync();
```

#### 2.1 Update Page Navigation Methods

**File:** `tests/Functional/Pages/ProfilePage.cs`
```csharp
// BEFORE
public async Task NavigateToProfileAsync()
{
    await Page!.GotoAsync("/profile");
    await Page!.WaitForLoadStateAsync(LoadState.NetworkIdle);
}

// AFTER
public async Task NavigateToProfileAsync()
{
    await Page!.GotoAsync("/profile");
    // Wait for key page element instead of network idle
    await ProfileContent.WaitForAsync(new() { State = WaitForSelectorState.Visible });
}
```

**Apply same pattern to:**
- [`TransactionsPage.cs:224-226`](../../tests/Functional/Pages/TransactionsPage.cs#L224-L226)
- [`WorkspacesPage.cs:142-144`](../../tests/Functional/Pages/WorkspacesPage.cs#L142-L144)
- All other page navigation methods

#### 2.2 Update Component Navigation Methods

**File:** `tests/Functional/Components/LoginState.cs`
```csharp
// BEFORE
public async Task NavigateToSignInAsync()
{
    await SignInMenuItem.ClickAsync();
    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
}

// AFTER
public async Task NavigateToSignInAsync()
{
    await SignInMenuItem.ClickAsync();
    // Wait for login form to appear
    await page.Locator("[data-test-id='LoginForm']").WaitForAsync();
}
```

**Apply same pattern to:**
- All methods in [`LoginState.cs`](../../tests/Functional/Components/LoginState.cs)
- [`Nav.cs:13`](../../tests/Functional/Components/Nav.cs#L13)
- [`WorkspaceSelector.cs`](../../tests/Functional/Components/WorkspaceSelector.cs) - multiple locations

#### 2.3 Update Step Definitions

**File:** `tests/Functional/Steps/Common/CommonGivenSteps.cs`
```csharp
// BEFORE
protected virtual async Task GivenIAmOnTheLoginPage()
{
    await Page.GotoAsync("/login");
    var loginPage = GetOrCreateLoginPage();
    Assert.That(await loginPage.IsOnLoginPageAsync(), Is.True, "Should be on login page");
    await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
}

// AFTER
protected virtual async Task GivenIAmOnTheLoginPage()
{
    await Page.GotoAsync("/login");
    var loginPage = GetOrCreateLoginPage();
    // IsOnLoginPageAsync() already waits for the form to be visible, so NetworkIdle is redundant
    Assert.That(await loginPage.IsOnLoginPageAsync(), Is.True, "Should be on login page");
}
```

**Apply same pattern to:**
- [`CommonWhenSteps.cs:54`](../../tests/Functional/Steps/Common/CommonWhenSteps.cs#L54)
- All navigation methods in [`AuthenticationSteps.cs`](../../tests/Functional/Steps/AuthenticationSteps.cs)
- All navigation methods in [`WorkspaceTenancySteps.cs`](../../tests/Functional/Steps/WorkspaceTenancySteps.cs)

#### 2.4 Testing Strategy for NetworkIdle Removal

1. **Start with one test file** - Pick `Weather.feature` as it's simplest
2. **Remove NetworkIdle waits** - Replace with element waits
3. **Run tests 10 times** - Ensure consistency
4. **Check for flakiness** - If tests pass 10/10 times, proceed
5. **Repeat for each feature file** - Authentication, Tenancy, etc.

**Rollback Plan:** If any test becomes flaky:
- Add back a minimal wait (e.g., `WaitForLoadStateAsync(LoadState.DOMContentLoaded)`)
- Or wait for specific API responses to complete
- Or increase timeout on element wait

**Expected Savings:** 1200-1800ms per test (most significant optimization)

---

### Phase 3: Screenshot Optimization (MEDIUM Priority, LOW Effort)

**Target:** Reduce overhead by 100-300ms per test
**Effort:** 2-3 hours
**Risk:** Low

#### 3.1 Conditional Screenshots (Only on Failure)

**Strategy:** Replace the blanket `@hook:before-first-then:SaveScreenshot` with failure-only screenshots.

**File:** `tests/Functional/Infrastructure/FunctionalTestBase.cs`

Add this to `TearDown()`:
```csharp
[TearDown]
public async Task TearDown()
{
    // Capture screenshot only on test failure
    if (TestContext.CurrentContext.Result.Outcome.Status == NUnit.Framework.Interfaces.TestStatus.Failed)
    {
        var pageModel = It<Pages.BasePage>();
        await pageModel.SaveScreenshotAsync($"FAILED-{TestContext.CurrentContext.Test.Name}");
    }

    _testActivity?.Stop();
    _testActivity?.Dispose();
}
```

**Remove from all feature files:**
```
@hook:before-first-then:SaveScreenshot  // ❌ Remove this line
```

**Files to update:**
- [`Authentication.feature:5`](../../tests/Functional/Features/Authentication.feature#L5)
- [`Weather.feature:5`](../../tests/Functional/Features/Weather.feature#L5)
- [`Tenancy.feature:5`](../../tests/Functional/Features/Tenancy.feature#L5)
- [`Tenancy-Collaboration.feature`](../../tests/Functional/Features/Tenancy-Collaboration.feature)
- All other `.feature` files

#### 3.2 Remove Redundant Screenshots Before Actions

**Rationale:** If test fails, the failure screenshot will capture the state anyway.

**Files to update:**
- [`LoginPage.cs:46`](../../tests/Functional/Pages/LoginPage.cs#L46) - Remove from `ClickLoginButtonAsync()`
- [`LoginPage.cs:119`](../../tests/Functional/Pages/LoginPage.cs#L119) - Remove from `ClickLoginButtonWithoutApiWaitAsync()`
- [`RegisterPage.cs:34`](../../tests/Functional/Pages/RegisterPage.cs#L34) - Remove from `RegisterAsync()`
- [`RegisterPage.cs:70`](../../tests/Functional/Pages/RegisterPage.cs#L70) - Remove from `ClickRegisterButtonAsync()`
- [`RegisterPage.cs:83`](../../tests/Functional/Pages/RegisterPage.cs#L83) - Remove from `ClickRegisterButtonWithoutApiWaitAsync()`
- [`ProfilePage.cs:72`](../../tests/Functional/Pages/ProfilePage.cs#L72) - Remove from `ClickLogoutAsync()`

**Exception:** Keep debug screenshots for known flaky tests (e.g., AB#1980, AB#1976 references in code)

**Expected Savings:** 100-300ms per test (varies by screenshot count)

---

### Phase 4: Advanced Optimizations (OPTIONAL)

**Target:** Further refinement
**Effort:** Variable
**Risk:** Low-Medium

#### 4.1 Browser Context Reuse Across Tests

**Current:** Each test gets a fresh browser context
**Optimization:** Reuse context for tests within same fixture, clear cookies/storage between tests

**Benefits:** Faster context creation
**Drawbacks:** More complex cleanup, potential for state leakage
**Recommendation:** Only if Phase 1-3 don't achieve target performance

#### 4.2 Lazy Screenshot Rendering

**Current:** Screenshots render full page synchronously
**Optimization:** Use viewport-only screenshots or defer rendering

**Changes:**
```csharp
// In BasePage.cs:69
await Page!.ScreenshotAsync(new PageScreenshotOptions() {
    Path = filename,
    OmitBackground = true,
    FullPage = false  // ✅ Viewport only (faster)
});
```

**Expected Savings:** 50-100ms per screenshot

#### 4.3 Reduce API Client Correlation Headers

**Current:** Every request includes test correlation headers
**Observation:** This adds minimal overhead but is useful for debugging
**Recommendation:** Keep these - the debugging value outweighs minimal performance cost

---

## Implementation Order

### Week 1: Quick Wins
1. ✅ Remove `Task.Delay()` calls (1-2 hours)
2. ✅ Enable parallel execution (1 hour)
3. ✅ Reduce default timeouts (30 minutes)
4. ✅ Run full test suite - verify no regressions
5. ✅ Measure new baseline performance

**Expected Result:** 1.0-1.5s/test, 4x throughput with parallel execution

### Week 2: NetworkIdle Elimination
1. ✅ Update Weather tests (2 hours)
2. ✅ Update Authentication tests (3 hours)
3. ✅ Update Tenancy tests (3 hours)
4. ✅ Update all page objects and components (2 hours)
5. ✅ Run test suite 10 times - ensure stability
6. ✅ Measure new baseline performance

**Expected Result:** 0.3-0.5s/test

### Week 3: Screenshot Optimization
1. ✅ Add failure-only screenshot hook (1 hour)
2. ✅ Remove redundant screenshots (1 hour)
3. ✅ Update feature files (30 minutes)
4. ✅ Run full test suite - verify screenshots still captured on failures

**Expected Result:** 0.2-0.4s/test

---

## Success Metrics

| Metric | Baseline | Phase 1 Target | Phase 2 Target | Phase 3 Target |
|--------|----------|----------------|----------------|----------------|
| Time per test (serial) | 2.5s | 1.2s | 0.5s | 0.3s |
| Time per test (parallel) | 2.5s | 0.3s | 0.125s | 0.075s |
| Total suite time (40 tests) | 100s | 12s | 5s | 3s |
| Flakiness rate | <1% | <1% | <2% | <1% |

## Testing Protocol

### After Each Phase:
1. Run full test suite locally 3 times
2. Run full test suite in CI 3 times
3. Verify pass rate remains ≥99%
4. Measure average execution time
5. Document any new flaky tests
6. Rollback specific changes if flakiness increases

### Flakiness Mitigation:
- If a test becomes flaky after NetworkIdle removal, add back minimal wait
- Use `Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded)` as middle ground
- Consider waiting for specific API responses instead of NetworkIdle

---

## Rollback Plan

Each phase is independent and can be rolled back separately:

**Phase 1:** Revert commits, restore Task.Delay and serial execution
**Phase 2:** Restore NetworkIdle waits for problematic tests only
**Phase 3:** Re-enable screenshot hooks in feature files

---

## Monitoring

### Continuous Monitoring (post-optimization):
- Track average test execution time in CI
- Monitor test flakiness rate (should remain <1%)
- Alert if test time increases >20% from baseline
- Review failed test screenshots to ensure they capture useful information

### Key Indicators:
- ✅ **Success:** Tests run faster AND remain stable
- ⚠️ **Warning:** Tests run faster BUT flakiness increases
- ❌ **Failure:** Tests break or become unreliable

---

## References

- [Playwright Best Practices](https://playwright.dev/docs/best-practices)
- [Playwright Performance](https://playwright.dev/docs/test-timeouts)
- [NUnit Parallel Execution](https://docs.nunit.org/articles/nunit/running-tests/parallelexecution.html)
- [`FUNCTIONAL-TEST-PERFORMANCE-ANALYSIS.md`](./FUNCTIONAL-TEST-PERFORMANCE-ANALYSIS.md) - Detailed analysis

---

## Implementation

Starting with a timing test run on Beach
1. Test summary: total: 32, failed: 0, succeeded: 32, skipped: 0, duration: 94.2s (2.94s/ea)

- Removed *all* screen shots
- Added back in screen shot on failure
- Removed Task.Delay
- Removed just a couple wait for network idle

2. Test summary: total: 32, failed: 0, succeeded: 32, skipped: 0, duration: 58.4s (1.825s/ea!)

@@ -139,8 +139,10 @@ public partial class WorkspacesPage(IPage page) : BasePage(page)
     /// </summary>
     public async Task NavigateAsync()
     {
-        await Page!.GotoAsync("/workspaces");
-        await Page!.WaitForLoadStateAsync(LoadState.NetworkIdle);
+        await WaitForApi(async () =>
+        {
+            await Page!.GotoAsync("/workspaces");
+        }, TenantsApiRegex());
     }

3. Test summary: total: 32, failed: 0, succeeded: 32, skipped: 0, duration: 47.8s

@@ -221,9 +221,10 @@ public partial class TransactionsPage(IPage page) : BasePage(page)
     /// </summary>
     public async Task NavigateAsync()
     {
-        await Page!.GotoAsync("/transactions");
-        // TODO: Wait for the transactions list to load
-        await Page!.WaitForLoadStateAsync(LoadState.NetworkIdle);
+        await WaitForApi(async () =>
+        {
+            await Page!.GotoAsync("/transactions");
+        }, TransactionsApiRegex());
     }

4. Test summary: total: 32, failed: 0, succeeded: 32, skipped: 0, duration: 44.9s
