# Step Definitions

This directory contains step definition classes that implement Gherkin steps from feature files.

## Structure

```
Steps/
├── Common/                      # Shared step definitions
│   ├── CommonGivenSteps.cs     # Common Given steps
│   ├── CommonWhenSteps.cs      # Common When steps
│   ├── CommonThenSteps.cs      # Common Then steps
│   └── README.md
├── AuthenticationSteps.cs       # Authentication feature steps
├── WeatherSteps.cs             # Weather feature steps
├── WorkspaceTenancySteps.cs    # Workspace tenancy feature steps
└── README.md                    # This file
```

## Step Definition Classes

### Common Steps ([`Common/`](Common/))

Shared step implementations used across multiple features. See [`Common/README.md`](Common/README.md) for details.

**Inheritance Chain:**
```
FunctionalTestBase → CommonGivenSteps → CommonWhenSteps → CommonThenSteps
```

### Feature-Specific Steps

#### [`AuthenticationSteps.cs`](AuthenticationSteps.cs)

Step definitions for user authentication scenarios (registration, login, logout, profile).

**Extends:** [`CommonThenSteps`](Common/CommonThenSteps.cs)

**Key Steps:**
- Registration flow (valid/invalid scenarios)
- Login flow (success/failure scenarios)
- Logout flow
- Profile viewing
- Access control (authenticated/unauthenticated)

#### [`WeatherSteps.cs`](WeatherSteps.cs)

Step definitions for weather forecast display scenarios.

**Extends:** [`CommonThenSteps`](Common/CommonThenSteps.cs)

**Key Steps:**
- Navigate to weather page
- Verify forecast display
- Verify temperature conversions
- Verify chronological ordering

#### [`WorkspaceTenancySteps.cs`](WorkspaceTenancySteps.cs)

Step definitions for multi-tenant workspace scenarios.

**Extends:** [`CommonThenSteps`](Common/CommonThenSteps.cs)

**Key Steps:**
- Workspace creation, viewing, updating, deletion
- User access management (Owner, Editor, Viewer roles)
- Transaction management within workspaces
- Workspace isolation verification

**Special Features:**
- Test prefix handling (`__TEST__` prefix added automatically)
- Bulk user/workspace setup for complex scenarios
- Role-based permission verification

## Inheritance Hierarchy

All step definition classes inherit from the common step chain, providing access to all shared step implementations:

```
FunctionalTestBase (Infrastructure/)
    ↓
CommonGivenSteps (Common/)
    ↓
CommonWhenSteps (Common/)
    ↓
CommonThenSteps (Common/)
    ↓
┌───────────────────┬────────────────┬──────────────────────┐
│                   │                │                      │
AuthenticationSteps WeatherSteps     WorkspaceTenancySteps
```

## Generated Test Classes

The step definition classes are used as base classes for generated test classes in [`../Tests/`](../Tests/):

```csharp
// Generated from Features/Authentication.feature
public class UserAuthenticationTests : AuthenticationSteps
{
    [Test]
    public async Task UserLogsIntoAnExistingAccount()
    {
        await GivenIHaveAnExistingAccount();
        await GivenIAmOnTheLoginPage();
        await WhenIEnterMyCredentials();
        await WhenIClickTheLoginButton();
        await ThenIShouldSeeTheHomePage();
    }
}
```

## Design Principles

1. **Gherkin Mapping**: Step methods map directly to Gherkin steps in feature files
2. **XML Documentation**: All step methods include XML comments with the Gherkin pattern they implement
3. **Reusability**: Common steps are shared; feature-specific steps remain isolated
4. **Page Object Pattern**: Steps use page objects to interact with the UI
5. **Object Store**: Shared data between steps uses the ObjectStore from Infrastructure

## Writing New Steps

When adding new step definitions:

1. **Add to appropriate class**:
   - Common steps → [`Common/Common{Given|When|Then}Steps.cs`](Common/)
   - Feature-specific → Existing or new feature step class

2. **Follow XML documentation pattern**:
   ```csharp
   /// <summary>
   /// When: I do something with {parameter}
   /// </summary>
   protected async Task WhenIDoSomethingWith(string parameter)
   {
       // Implementation
   }
   ```

3. **Use page objects** from [`../Pages/`](../Pages/)

4. **Share data** via `_objectStore` (inherited from [`FunctionalTestBase`](../Infrastructure/FunctionalTestBase.cs))

5. **Follow Gherkin style comments** in test logic (Given/When/Then/And)

## Related Documentation

- [`../Infrastructure/README.md`](../Infrastructure/README.md) - Test infrastructure
- [`../Pages/README.md`](../Pages/README.md) - Page Object Models
- [`../Features/`](../Features/) - Gherkin feature files (source of truth)
- [`../Tests/README.md`](../Tests/README.md) - Generated test classes
- [`../README.md`](../README.md) - Functional tests overview
