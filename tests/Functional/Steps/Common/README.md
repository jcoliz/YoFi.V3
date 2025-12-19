# Common Step Definitions

This directory contains common step definitions shared across all feature tests.

## Contents

### [`CommonGivenSteps.cs`](CommonGivenSteps.cs)

Common "Given" step implementations used across multiple features.

**Provided Steps:**
- `GivenLaunchedSite()` - Launch the site and verify it loads
- `GivenTheApplicationIsRunning()` - Verify application is running
- `GivenIAmNotLoggedIn()` - Ensure user is not logged in
- `GivenIHaveAnExistingAccount()` - Create a test user account
- `GivenIAmOnTheLoginPage()` - Navigate to login page
- `GivenIAmLoggedIn()` - Complete login flow (account + login + verify)

**Helpers:**
- `GetOrCreateLoginPage()` - Page object factory for LoginPage
- `GetOrCreateWeatherPage()` - Page object factory for WeatherPage

### [`CommonWhenSteps.cs`](CommonWhenSteps.cs)

Common "When" step implementations for actions.

**Provided Steps:**
- `WhenUserLaunchesSite()` - Launch site and store response
- `VisitPage(option)` - Navigate via navbar to a specific page
- `WhenIEnterMyCredentials()` - Fill in login form with test credentials
- `WhenIClickTheLoginButton()` - Submit login form

### [`CommonThenSteps.cs`](CommonThenSteps.cs)

Common "Then" step implementations for assertions.

**Provided Steps:**
- `ThenPageLoadedOk()` - Verify page response is successful
- `PageTitleContains(text)` - Assert page title contains text
- `PageHeadingIs(text)` - Assert H1 heading matches text
- `WeatherPageDisplaysForecasts(count)` - Verify weather forecast count
- `ThenIShouldSeeTheHomePage()` - Verify user is on home page

## Inheritance Hierarchy

The common steps form an inheritance chain to provide cumulative functionality:

```
FunctionalTestBase (../../Infrastructure/)
    ↓ inherits
CommonGivenSteps
    ↓ inherits
CommonWhenSteps
    ↓ inherits
CommonThenSteps
    ↓ inherited by
Feature-specific steps (AuthenticationSteps, WeatherSteps, etc.)
```

### Why This Chain?

1. **Given steps** often call **When** and **Then** steps (e.g., `GivenIAmLoggedIn()` calls login actions and verifications)
2. **When steps** may reference **Given** step helpers (e.g., page object factories)
3. **Then steps** need access to all step types for complex assertions
4. **Feature-specific steps** inherit all common steps to use or override them

## Abstract Methods

[`CommonGivenSteps`](CommonGivenSteps.cs) declares abstract methods that must be implemented by derived classes:

- `WhenUserLaunchesSite()` - Implemented in [`CommonWhenSteps`](CommonWhenSteps.cs)
- `WhenIEnterMyCredentials()` - Implemented in [`CommonWhenSteps`](CommonWhenSteps.cs)
- `WhenIClickTheLoginButton()` - Implemented in [`CommonWhenSteps`](CommonWhenSteps.cs)
- `ThenPageLoadedOk()` - Implemented in [`CommonThenSteps`](CommonThenSteps.cs)
- `ThenIShouldSeeTheHomePage()` - Implemented in [`CommonThenSteps`](CommonThenSteps.cs)

This pattern allows Given steps to call When/Then steps without circular dependencies.

## Design Principles

1. **DRY (Don't Repeat Yourself)**: Common steps are defined once and reused
2. **Single Responsibility**: Each class focuses on one step type (Given/When/Then)
3. **Template Method Pattern**: Abstract methods allow base classes to call derived implementations
4. **Composability**: Feature-specific steps can use, extend, or override common steps

## Usage Example

```csharp
// Feature-specific step class
public abstract class AuthenticationSteps : CommonThenSteps
{
    // Can use all common Given/When/Then steps
    protected async Task GivenIAmOnTheRegistrationPage()
    {
        // Can call common helpers
        await Page.GotoAsync("/register");
    }

    // Can override common steps if needed
    protected override async Task GivenIAmOnTheLoginPage()
    {
        // Custom implementation
        await base.GivenIAmOnTheLoginPage();
        // Additional logic
    }
}
```

## Related Documentation

- [`../../Infrastructure/README.md`](../../Infrastructure/README.md) - Test infrastructure
- [`../README.md`](../README.md) - All step definitions
- [`../../README.md`](../../README.md) - Functional tests overview
