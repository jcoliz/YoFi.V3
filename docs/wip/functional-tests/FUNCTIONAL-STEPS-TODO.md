# Authentication Steps - TODO Items

This document tracks incomplete step implementations that are actively used in the Authentication feature tests.

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
