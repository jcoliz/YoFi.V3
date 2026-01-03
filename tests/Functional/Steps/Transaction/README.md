# Transaction Step Classes

This directory contains transaction-related step definition classes organized by domain responsibility.

## Architecture

All transaction step classes inherit from a base class that provides:
- Common helper methods (object store access, page object creation)
- Object store key constants
- Page object factory methods
- Composition of shared step classes (NavigationSteps, AuthSteps, WorkspaceDataSteps)

## Step Classes

### TransactionListSteps
**Purpose:** Transaction list viewing and navigation operations

**Responsibilities:**
- Viewing transaction lists in a workspace
- Navigating to transaction pages
- Filtering and searching transactions
- Verifying transaction visibility
- Transaction count assertions

**Example Steps:**
- `[When("I view transactions in {workspaceName}")]`
- `[When("I try to view transactions in {workspaceName}")]`
- `[Then("I should see exactly {expectedCount} transactions")]`
- `[Then("I should see the transactions")]`
- `[Then("they should all be from {workspaceName} workspace")]`
- `[Then("I should not see any transactions from {workspaceName}")]`

### TransactionCreateSteps
**Purpose:** Creating new transactions

**Responsibilities:**
- Adding new transactions via UI
- Transaction creation workflows
- Verifying transaction was created successfully
- Storing created transaction details for later reference

**Example Steps:**
- `[When("I add a transaction to {workspaceName}")]`
- `[Then("the transaction should be saved successfully")]`

### TransactionEditSteps
**Purpose:** Editing existing transactions

**Responsibilities:**
- Opening edit dialogs/pages
- Modifying transaction fields (payee, amount, date, memo, etc.)
- Submitting edits
- Verifying changes were saved
- Transaction update workflows

**Example Steps:**
- `[When("I update that transaction")]`
- `[When("I try to add or edit transactions")]`
- `[Then("my changes should be saved")]`

### TransactionDeleteSteps
**Purpose:** Deleting transactions

**Responsibilities:**
- Deleting transactions
- Verifying transaction was removed
- Permission checks for delete operations

**Example Steps:**
- `[When("I delete that transaction")]`
- `[Then("it should be removed")]`

### TransactionPermissionsSteps
**Purpose:** Transaction permission checks (if needed separate from list/edit)

**Responsibilities:**
- Checking if user can create transactions
- Checking if user can edit transactions
- Checking if user can delete transactions
- Role-based access control for transactions

**Example Steps:**
- `[When("I try to add or edit transactions")]`
- `[Then("I should not be able to make those changes")]`

**Note:** May be merged with TransactionEditSteps if permission checks are simple.

### TransactionAssertionSteps
**Purpose:** Transaction state verification and data isolation checks

**Responsibilities:**
- Verifying transaction data accuracy
- Data isolation between workspaces
- Access control assertions (cannot access other workspace transactions)

**Example Steps:**
- `[Then("they should all be from {workspaceName} workspace")]`
- `[Then("I should not see any transactions from {workspaceName}")]`
- `[Then("I should not be able to access that data")]`

## Composition Pattern

Test classes compose the transaction step classes as needed:

```csharp
public class TransactionTests : FunctionalTestBase
{
    protected TransactionListSteps ListSteps => _listSteps ??= new(this);
    private TransactionListSteps? _listSteps;

    protected TransactionCreateSteps CreateSteps => _createSteps ??= new(this);
    private TransactionCreateSteps? _createSteps;

    protected TransactionEditSteps EditSteps => _editSteps ??= new(this);
    private TransactionEditSteps? _editSteps;

    protected TransactionDeleteSteps DeleteSteps => _deleteSteps ??= new(this);
    private TransactionDeleteSteps? _deleteSteps;

    protected WorkspaceDataSteps WorkspaceDataSteps => _workspaceDataSteps ??= new(this);
    private WorkspaceDataSteps? _workspaceDataSteps;

    [Test]
    public async Task UserCreatesTransaction()
    {
        // Setup
        await WorkspaceDataSteps.GivenUserOwnsAWorkspaceCalled("alice", "Personal");
        await AuthSteps.GivenIAmLoggedInAs("alice");

        // Action
        await CreateSteps.WhenIAddATransactionTo("Personal");

        // Verification
        await ListSteps.ThenTheTransactionShouldBeSavedSuccessfully();
    }
}
```

## Design Principles

1. **Single Responsibility** - Each class handles one aspect of transaction functionality
2. **Composition over Inheritance** - Step classes compose AuthSteps and WorkspaceDataSteps rather than inheriting complex behavior
3. **DRY** - Common functionality lives in a base class (if needed)
4. **Testability** - Clear separation makes it easy to test individual concerns
5. **Maintainability** - Smaller, focused classes are easier to understand and modify

## Migration from WorkspaceTenancySteps

The transaction-related methods from [`WorkspaceTenancySteps.cs`](../WorkspaceTenancySteps.cs) are being split into these domain-focused classes. During migration:
- Existing tests continue to work with the old class
- New tests use the new composition architecture
- Gradual migration of tests from inheritance to composition

## Object Store Keys

Transaction-related object store keys (to be defined in base class):
- `KEY_LAST_TRANSACTION_PAYEE` - Payee name of the last transaction created/modified
- `KEY_TRANSACTION_KEY` - GUID key of the last transaction created/modified
- `KEY_CAN_MAKE_DESIRED_CHANGES` - Permission check result for transaction operations
- `KEY_HAS_WORKSPACE_ACCESS` - Workspace access check result

## Test Prefix Handling

All transaction operations use workspace names with the **__TEST__** prefix. The prefix is added automatically via helper methods when calling APIs or storing in object store. This ensures:
- Feature files remain readable
- API calls include the test prefix
- Object store keys are consistent
- Test isolation is maintained

## Cross-Step Dependencies

Transaction steps often depend on workspace setup:
- Use `WorkspaceDataSteps` for workspace creation before transaction operations
- Transaction operations require a workspace context
- Permission checks depend on user's role in the workspace
