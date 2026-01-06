# Functional Tests - TODO Items

This document tracks incomplete step implementations and improvements needed for functional tests.

## Page Object Improvements

### TransactionsPage - Replace NetworkIdle with Explicit API Waiting

**Context**: Currently using `NetworkIdle` after transaction create/edit/delete operations in [`TransactionsPage.cs`](../../../tests/Functional/Pages/TransactionsPage.cs:460), but this is an indirect heuristic. The frontend actually makes a GET transactions API call after each mutation to refresh the list.

**Better Approach**: Explicitly wait for both API calls using `Task.WhenAll()`.

**Changes Needed in `tests/Functional/Pages/TransactionsPage.cs`:**

1. **Add GET transactions regex** (around line 18-20 with other regexes):
   ```csharp
   private static readonly Regex GetTransactionsApiRegex = new(@"/api/tenant/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}/Transactions\?", RegexOptions.Compiled);
   ```

2. **Update `SubmitCreateFormAsync()`** (line ~286):
   ```csharp
   public async Task SubmitCreateFormAsync()
   {
       var createResponseTask = Page!.WaitForResponseAsync(CreateTransactionApiRegex);
       var getTransactionsResponseTask = Page!.WaitForResponseAsync(GetTransactionsApiRegex);

       await CreateButton.ClickAsync();

       await Task.WhenAll(createResponseTask, getTransactionsResponseTask);
   }
   ```

3. **Update `SubmitEditFormAsync()`** (line ~450):
   ```csharp
   public async Task SubmitEditFormAsync()
   {
       var updateResponseTask = Page!.WaitForResponseAsync(UpdateTransactionApiRegex);
       var getTransactionsResponseTask = Page!.WaitForResponseAsync(GetTransactionsApiRegex);

       await UpdateButton.ClickAsync();

       await Task.WhenAll(updateResponseTask, getTransactionsResponseTask);
   }
   ```

   **Remove** the `NetworkIdle` wait on line 460.

4. **Update `ConfirmDeleteAsync()`** (line ~527):
   ```csharp
   public async Task ConfirmDeleteAsync()
   {
       var deleteResponseTask = Page!.WaitForResponseAsync(UpdateTransactionApiRegex);
       var getTransactionsResponseTask = Page!.WaitForResponseAsync(GetTransactionsApiRegex);

       await DeleteButton.ClickAsync();

       await Task.WhenAll(deleteResponseTask, getTransactionsResponseTask);
   }
   ```

**Benefits:**
- ✅ More explicit and reliable than NetworkIdle
- ✅ Faster (no 500ms timeout needed)
- ✅ Documents the actual frontend behavior
- ✅ Better error messages if specific API calls fail

**Testing:**
After changes, run functional tests to verify stability:
```powershell
.\scripts\Run-FunctionalTestsVsContainer.ps1
```

---

## Authentication Steps - TODO Items

## High Priority - Called by Active Tests

### 1. `ThenIShouldSeeAMessageIndicatingINeedToLogIn()`
**Location**: [`AuthenticationSteps.cs:645`](AuthenticationSteps.cs:645)
**Used in Test**: Anonymous user cannot access protected pages (line 311 in [`Tests/Authentication.feature.cs`](../Tests/Authentication.feature.cs:311))
**Current Status**: Stub implementation with TODO comment
**Required Implementation**:
- Check for login required message on the page
- Verify message indicates user needs to authenticate
- Likely check for redirect banner or modal indicating authentication requirement

**Current Code**:
```csharp
/// <summary>
/// Then: I should see a message indicating I need to log in
/// </summary>
protected async Task ThenIShouldSeeAMessageIndicatingINeedToLogIn()
{
    // TODO: Check for login required message
    await Task.CompletedTask;
}
```

---

### 2. `ThenAfterLoggingInIShouldBeRedirectedToTheOriginallyRequestedPage()`
**Location**: [`AuthenticationSteps.cs:654`](AuthenticationSteps.cs:654)
**Used in Test**: Anonymous user cannot access protected pages (line 314 in [`Tests/Authentication.feature.cs`](../Tests/Authentication.feature.cs:314))
**Current Status**: Stub implementation with TODO comment
**Required Implementation**:
- Simulate login after being redirected to login page
- Verify user is redirected back to the originally requested protected page
- This tests the "return URL" functionality after authentication

**Current Code**:
```csharp
/// <summary>
/// Then: after logging in, I should be redirected to the originally requested page
/// </summary>
protected async Task ThenAfterLoggingInIShouldBeRedirectedToTheOriginallyRequestedPage()
{
    // TODO: Verify redirect after login works correctly
    await Task.CompletedTask;
}
```

---

## Lower Priority - Not Currently Called

These step methods have TODOs but are not currently invoked by any active test scenarios:

### 8. `WhenITryToNavigateToTheLoginPage()`
**Location**: [`AuthenticationSteps.cs:314`](AuthenticationSteps.cs:314)
**Status**: Has TODO in remarks about simulating navigation via in-app links/buttons instead of direct URL navigation

---

## Summary

- **2 High Priority TODOs**: Currently blocking full test coverage for "Anonymous user cannot access protected pages" scenario
- **1 Design TODO**: Navigation simulation improvement opportunity

## Next Steps

1. Implement `ThenIShouldSeeAMessageIndicatingINeedToLogIn()` to check for authentication-required messaging
2. Implement `ThenAfterLoggingInIShouldBeRedirectedToTheOriginallyRequestedPage()` to verify return URL functionality
3. Consider whether lower priority TODOs represent missing test scenarios or deprecated step methods
