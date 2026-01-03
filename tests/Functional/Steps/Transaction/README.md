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
**Purpose:** Creating new transactions via the create modal

**Responsibilities:**
- Opening the create transaction modal
- Filling in transaction form fields
- Verifying transaction creation modal fields
- Submitting new transaction
- Verifying transaction was created successfully

**Example Steps:**
- `[When("I click the \"Add Transaction\" button")]`
- `[Then("I should see a create transaction modal")]`
- `[Then("I should see the following fields in the create form:")]`
- `[When("I fill in the following transaction fields:")]`

### TransactionEditSteps
**Purpose:** Editing transaction fields (both quick edit and details page)

**Responsibilities:**
- Editing transaction fields (memo, category, source, external ID)
- Clicking save/update buttons
- Verifying modal close behavior
- Opening edit mode on details page

**Example Steps:**
- `[When("I click the \"Edit\" button")]` - Opens edit mode on details page
- `[When("I change Memo to {value}")]`
- `[When("I change Category to {value}")]`
- `[When("I change Source to {value}")]`
- `[When("I change ExternalId to {value}")]`
- `[When("I click \"Update\"")]` - Saves quick edit changes
- `[When("I click \"Save\"")]` - Saves full edit changes
- `[Then("the modal should close")]`

### TransactionQuickEditSteps
**Purpose:** Quick edit modal operations (limited field editing)

**Responsibilities:**
- Opening quick edit modal from transaction list
- Verifying quick edit modal shows only payee/category/memo
- Verifying quick edit modal hides date/amount/source/externalId
- Field value verification in modal

**Example Steps:**
- `[When("I click the \"Edit\" button on the transaction")]` - Opens quick edit from list
- `[When("I quick edit the transaction")]` - Alias for above
- `[Then("I should see a modal titled {title}")]`
- `[Then("I should only see fields for \"Payee\", \"Category\", and \"Memo\"")]`
- `[Then("the fields match the expected values")]`
- `[Then("I should not see fields for \"Date\", \"Amount\", \"Source\", or \"ExternalId\"")]`

### TransactionDetailsSteps
**Purpose:** Transaction details page navigation and verification

**Responsibilities:**
- Verifying navigation to transaction details page
- Verifying all transaction fields displayed on details page
- Verifying specific field values on details page
- Navigating back to transaction list

**Example Steps:**
- `[Then("I should navigate to the transaction details page")]`
- `[Then("I should see all the expected transaction fields displayed")]`
- `[Then("I should see {value} as the {field}")]`
- `[When("I click \"Back to Transactions\"")]`
- `[Then("I should return to the transaction list")]`
- `[Then("I should see all my transactions")]`

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

## Migration Status

### Completed Step Classes
- ✅ **TransactionListSteps** - List viewing, navigation, count assertions
- ✅ **TransactionDataSteps** - Test data seeding via Test Control API

### Migration Plan for TransactionRecord.feature.cs

The [`TransactionRecord.feature.cs`](../../Tests/TransactionRecord.feature.cs) test file currently inherits from `TransactionRecordSteps` and has 24 unmigrated step methods. These need to be extracted into the following step definition classes:

#### Create These New Step Classes:

**1. TransactionQuickEditSteps** (6 steps) - Quick edit modal operations
- `WhenIClickTheEditButtonOnTheTransaction()` - Opens quick edit modal from list
- `WhenIQuickEditTheTransaction()` - Alias for above
- `ThenIShouldSeeAModalTitled()` - Verifies modal title
- `ThenIShouldOnlySeeFieldsForPayeeCategoryAndMemo()` - Verifies limited fields shown
- `ThenTheFieldsMatchTheExpectedValues()` - Verifies pre-populated values
- `ThenIShouldNotSeeFieldsForDateAmountSourceOrExternalId()` - Verifies excluded fields

**2. TransactionEditSteps** (7 steps) - Field editing and save operations
- `WhenIClickTheEditButton()` - Opens edit mode on details page
- `WhenIChangeMemoTo()` - Updates memo field
- `WhenIChangeCategoryTo()` - Updates category field
- `WhenIChangeSourceTo()` - Updates source field
- `WhenIChangeExternalIdTo()` - Updates external ID field
- `WhenIClickUpdate()` - Saves quick edit changes
- `WhenIClickSave()` - Saves full edit changes
- `ThenTheModalShouldClose()` - Verifies modal dismissed

**3. TransactionDetailsSteps** (6 steps) - Details page navigation and verification
- `ThenIShouldNavigateToTheTransactionDetailsPage()` - Verifies navigation
- `ThenIShouldSeeAllTheExpectedTransactionFieldsDisplayed()` - Verifies all fields shown
- `ThenIShouldSeeValueAsField()` - Verifies specific field value
- `WhenIClickBackToTransactions()` - Navigates back to list
- `ThenIShouldReturnToTheTransactionList()` - Verifies return navigation
- `ThenIShouldSeeAllMyTransactions()` - Verifies list display

**4. TransactionCreateSteps** (5 steps) - Transaction creation modal
- `WhenIClickTheAddTransactionButton()` - Opens create modal
- `ThenIShouldSeeACreateTransactionModal()` - Verifies modal appeared
- `ThenIShouldSeeTheFollowingFieldsInTheCreateForm()` - Verifies form fields
- `WhenIFillInTheFollowingTransactionFields()` - Populates form data

#### Extend Existing Step Classes:

**TransactionListSteps** (add 4 new steps) - List verification after edits
- `ThenIShouldSeeTheUpdatedMemoInTheTransactionList()` - Verifies memo update visible
- `ThenIShouldSeeTheUpdatedCategoryInTheTransactionList()` - Verifies category update visible
- `ThenIShouldSeeATransactionWithPayee()` - Finds transaction by payee
- `ThenItContainsTheExpectedListFields()` - Verifies list row data

#### Update Test Class:
After creating step classes, update [`TransactionRecord.feature.cs`](../../Tests/TransactionRecord.feature.cs):
1. Change inheritance from `TransactionRecordSteps` to `FunctionalTestBase`
2. Add 4 new composed step class properties
3. Use `AuthSteps.GivenIAmLoggedInAsAUserWithEditorRole()` in SetUp
4. Route all method calls through composed step classes
5. Delete `TransactionRecordSteps.cs` once migration is complete

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
