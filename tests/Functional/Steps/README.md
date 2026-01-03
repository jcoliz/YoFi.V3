# Step Definitions

This directory contains step definition classes that implement Gherkin steps from feature files.

## Architecture

This project uses a **composition-based step architecture** where test classes inherit from [`FunctionalTestBase`](../Infrastructure/FunctionalTestBase.cs) and compose only the step definition classes they need.

## Structure

```
Steps/
├── Transaction/                    # Transaction-related step definitions
│   ├── TransactionStepsBase.cs   # Base class for transaction steps
│   ├── TransactionSharedSteps.cs  # Shared transaction steps (modals, etc.)
│   ├── TransactionListSteps.cs    # Transaction list operations
│   ├── TransactionDetailsSteps.cs # Transaction details page operations
│   ├── TransactionEditSteps.cs    # Transaction editing operations
│   ├── TransactionQuickEditSteps.cs # Quick edit modal operations
│   ├── TransactionCreateSteps.cs  # Transaction creation operations
│   ├── TransactionDataSteps.cs    # Transaction test data setup
│   └── README.md
│
├── Workspace/                      # Workspace-related step definitions
│   ├── WorkspaceDataSteps.cs      # Workspace test data setup
│   └── README.md
│
├── AuthSteps.cs                    # Authentication steps (login, registration)
├── NavigationSteps.cs              # Site navigation steps
├── WeatherSteps.cs                 # Weather page steps
└── README.md                       # This file
```

## Step Definition Classes

### Transaction Steps ([`Transaction/`](Transaction/))

Transaction-related step definitions organized by responsibility. See [`Transaction/README.md`](Transaction/README.md) for details.

**Key Classes:**
- [`TransactionListSteps`](Transaction/TransactionListSteps.cs) - List viewing, navigation, filtering
- [`TransactionDetailsSteps`](Transaction/TransactionDetailsSteps.cs) - Details page navigation and verification
- [`TransactionEditSteps`](Transaction/TransactionEditSteps.cs) - Field editing and save operations
- [`TransactionQuickEditSteps`](Transaction/TransactionQuickEditSteps.cs) - Quick edit modal operations
- [`TransactionCreateSteps`](Transaction/TransactionCreateSteps.cs) - Transaction creation modal
- [`TransactionDataSteps`](Transaction/TransactionDataSteps.cs) - Test data seeding via Test Control API

### Workspace Steps ([`Workspace/`](Workspace/))

Workspace-related step definitions. See [`Workspace/README.md`](Workspace/README.md) for details.

**Key Classes:**
- [`WorkspaceDataSteps`](Workspace/WorkspaceDataSteps.cs) - Workspace creation and test data setup

### Core Steps

#### [`AuthSteps.cs`](AuthSteps.cs)

Step definitions for authentication (login, registration, logout).

**Key Steps:**
- User registration and login
- Session management
- Profile viewing

#### [`NavigationSteps.cs`](NavigationSteps.cs)

Step definitions for site navigation.

**Key Steps:**
- Site launching
- Page navigation
- URL verification

#### [`WeatherSteps.cs`](WeatherSteps.cs)

Step definitions for weather forecast display scenarios.

**Key Steps:**
- Navigate to weather page
- Verify forecast display
- Verify temperature conversions

## Composition Pattern

Tests compose step definition classes as needed using lazy initialization:

```csharp
public class TransactionTests : FunctionalTestBase
{
    // Compose only the step classes needed for this test
    protected TransactionListSteps ListSteps => _listSteps ??= new(this);
    private TransactionListSteps? _listSteps;

    protected TransactionEditSteps EditSteps => _editSteps ??= new(this);
    private TransactionEditSteps? _editSteps;

    protected WorkspaceDataSteps WorkspaceDataSteps => _workspaceDataSteps ??= new(this);
    private WorkspaceDataSteps? _workspaceDataSteps;

    [Test]
    public async Task UserEditsTransaction()
    {
        // Setup
        await WorkspaceDataSteps.GivenUserOwnsAWorkspaceCalled("alice", "Personal");
        await AuthSteps.GivenIAmLoggedInAs("alice");

        // Action
        await ListSteps.WhenIViewTransactionsIn("Personal");
        await EditSteps.WhenIUpdateThatTransaction();

        // Verification
        await EditSteps.ThenMyChangesShouldBeSaved();
    }
}
```

**Benefits:**
- ✅ Zero code duplication - all steps reusable across tests
- ✅ Small, focused step files (~15-20 steps each)
- ✅ Clear dependencies - tests declare only what they use
- ✅ Easy discoverability - IntelliSense shows available steps

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
