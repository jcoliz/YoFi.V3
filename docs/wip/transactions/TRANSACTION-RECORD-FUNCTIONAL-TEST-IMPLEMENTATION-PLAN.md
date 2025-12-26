---
status: In Progress - 3 of 8 scenarios complete
feature: Transaction Record Fields (Memo, Source, ExternalId)
test_plan: TRANSACTION-RECORD-FUNCTIONAL-TEST-PLAN.md
implementation_mode: code
scenarios: 8 scenarios (Priority 1 and 2)
---

# Functional Test Implementation Plan: Transaction Record Fields

## Overview

This document provides a detailed implementation plan for the 8 approved functional test scenarios from [`TRANSACTION-RECORD-FUNCTIONAL-TEST-PLAN.md`](TRANSACTION-RECORD-FUNCTIONAL-TEST-PLAN.md).

**Test Infrastructure Status**: ✅ READY
- TransactionsPage POM: Updated with new field locators and page-ready pattern
- TransactionDetailsPage POM: Created with complete field support and page-ready pattern
- Frontend ready pattern: Implemented in both transaction pages
- Test data seeding: Supported via TestControlController (needs enhancement for new fields)

**Implementation Approach**: One scenario at a time, with test execution after each to verify correctness.

---

## Implementation Status

- ✅ **Scenario 1: Quick edit modal shows only Payee and Memo fields** - COMPLETE
- ✅ **Scenario 2: User updates Memo via quick edit modal** - COMPLETE
- ✅ **Scenario 3: User navigates from transaction list to details page** - COMPLETE
- ⏳ **Scenario 4: User edits all fields on transaction details page** - PENDING
- ⏳ **Scenario 5: User returns to list from transaction details page** - PENDING
- ⏳ **Scenario 6: User sees all fields in create transaction modal** - PENDING
- ⏳ **Scenario 7: User creates transaction with all fields populated** - PENDING
- ⏳ **Scenario 8: Created transaction displays all fields on details page** - PENDING

---

## Phase 1: Infrastructure Enhancement

### Task 1.1: Enhance TransactionSeedRequest DTO

**File**: [`src/Controllers/TestControlController.cs`](../../../src/Controllers/TestControlController.cs)

**Current Definition** (lines 78-82):
```csharp
/// <summary>
/// Request to seed transactions in a workspace.
/// </summary>
/// <param name="Count">Number of transactions to create.</param>
/// <param name="PayeePrefix">Prefix for payee names (default: "Test Transaction").</param>
public record TransactionSeedRequest(int Count, string PayeePrefix = "Test Transaction");
```

**Enhancement Required**:
```csharp
/// <summary>
/// Request to seed transactions in a workspace.
/// </summary>
/// <param name="Count">Number of transactions to create.</param>
/// <param name="PayeePrefix">Prefix for payee names (default: "Test Transaction").</param>
/// <param name="Memo">Optional memo text to apply to all seeded transactions.</param>
/// <param name="Source">Optional source text to apply to all seeded transactions.</param>
/// <param name="ExternalId">Optional external ID text to apply to all seeded transactions.</param>
public record TransactionSeedRequest(
    int Count,
    string PayeePrefix = "Test Transaction",
    string? Memo = null,
    string? Source = null,
    string? ExternalId = null
);
```

**Update SeedTransactions Method** (lines 528-537):
```csharp
// Current:
var transaction = new TransactionEditDto(
    Date: baseDate.AddDays(random.Next(0, 30)),
    Amount: Math.Round((decimal)(random.NextDouble() * 490 + 10), 2),
    Payee: $"{request.PayeePrefix} {i}",
    Memo: null,
    Source: null,
    ExternalId: null
);

// Enhanced:
var transaction = new TransactionEditDto(
    Date: baseDate.AddDays(random.Next(0, 30)),
    Amount: Math.Round((decimal)(random.NextDouble() * 490 + 10), 2),
    Payee: $"{request.PayeePrefix} {i}",
    Memo: request.Memo,
    Source: request.Source,
    ExternalId: request.ExternalId
);
```

**Rationale**: Allows functional tests to seed transactions with specific field values for verification scenarios.

---

### Task 1.2: Add TransactionRecordSteps Class

**File**: `tests/Functional/Steps/TransactionRecordSteps.cs` (NEW)

**Purpose**: Provide step definitions specific to transaction record field testing.

**Base Class**: Extend `WorkspaceTenancySteps` to inherit workspace/user management steps.

**Key Methods Needed**:

```csharp
/// <summary>
/// Given: I have a workspace with this transaction: [DataTable]
/// </summary>
protected async Task GivenIHaveAWorkspaceWithThisTransaction(DataTable transactionTable);

/// <summary>
/// Given: I am viewing the details page for a transaction with: [DataTable]
/// </summary>
protected async Task GivenIAmViewingTheDetailsPageForATransactionWith(DataTable transactionTable);

/// <summary>
/// Given: I have created a transaction with Memo, Source, and ExternalId
/// </summary>
protected async Task GivenIHaveCreatedATransactionWithMemoSourceAndExternalId();

/// <summary>
/// Given: I have an empty workspace
/// </summary>
protected async Task GivenIHaveAnEmptyWorkspace();

/// <summary>
/// When: I click the edit button on {payee} transaction
/// </summary>
protected async Task WhenIClickTheEditButtonOnTransaction(string payee);

/// <summary>
/// When: I quick edit the {payee} transaction
/// </summary>
protected async Task WhenIQuickEditTheTransaction(string payee);

/// <summary>
/// When: I change Memo to {newMemo}
/// </summary>
protected async Task WhenIChangeMemoTo(string newMemo);

/// <summary>
/// When: I click on the {payee} transaction row
/// </summary>
protected async Task WhenIClickOnTheTransactionRow(string payee);

/// <summary>
/// When: I click the "Edit" button (on details page)
/// </summary>
protected async Task WhenIClickTheEditButton();

/// <summary>
/// When: I change Source to {newSource}
/// </summary>
protected async Task WhenIChangeSourceTo(string newSource);

/// <summary>
/// When: I change ExternalId to {newExternalId}
/// </summary>
protected async Task WhenIChangeExternalIdTo(string newExternalId);

/// <summary>
/// When: I click "Save" (on details page)
/// </summary>
protected async Task WhenIClickSave();

/// <summary>
/// When: I click "Back to Transactions"
/// </summary>
protected async Task WhenIClickBackToTransactions();

/// <summary>
/// When: I create a new transaction with: [DataTable]
/// </summary>
protected async Task WhenICreateANewTransactionWith(DataTable fieldTable);

/// <summary>
/// Then: I should see a modal titled {expectedTitle}
/// </summary>
protected async Task ThenIShouldSeeAModalTitled(string expectedTitle);

/// <summary>
/// Then: I should only see fields for Payee and Memo
/// </summary>
protected async Task ThenIShouldOnlySeeFieldsForPayeeAndMemo();

/// <summary>
/// Then: I should not see fields for Date, Amount, Source, or ExternalId
/// </summary>
protected async Task ThenIShouldNotSeeFieldsForDateAmountSourceOrExternalId();

/// <summary>
/// Then: the modal should close
/// </summary>
protected async Task ThenTheModalShouldClose();

/// <summary>
/// Then: I should see the updated memo in the transaction list
/// </summary>
protected async Task ThenIShouldSeeTheUpdatedMemoInTheTransactionList();

/// <summary>
/// Then: I should navigate to the transaction details page
/// </summary>
protected async Task ThenIShouldNavigateToTheTransactionDetailsPage();

/// <summary>
/// Then: I should see all transaction fields displayed: [DataTable]
/// </summary>
protected async Task ThenIShouldSeeAllTransactionFieldsDisplayed(DataTable fieldTable);

/// <summary>
/// Then: I should see {expectedValue} as the {fieldName}
/// </summary>
protected async Task ThenIShouldSeeValueAsField(string expectedValue, string fieldName);

/// <summary>
/// Then: I should return to the transaction list
/// </summary>
protected async Task ThenIShouldReturnToTheTransactionList();

/// <summary>
/// Then: I should see all my transactions
/// </summary>
protected async Task ThenIShouldSeeAllMyTransactions();

/// <summary>
/// Then: I should see required fields: Date, Payee, Amount
/// </summary>
protected async Task ThenIShouldSeeRequiredFields();

/// <summary>
/// Then: I should see optional fields: Memo, Source, ExternalId
/// </summary>
protected async Task ThenIShouldSeeOptionalFields();

/// <summary>
/// Then: the Memo column should display {expectedMemo}
/// </summary>
protected async Task ThenTheMemoColumnShouldDisplay(string expectedMemo);

/// <summary>
/// Then: I should see all fields displayed correctly: [DataTable]
/// </summary>
protected async Task ThenIShouldSeeAllFieldsDisplayedCorrectly(DataTable fieldTable);
```

**Object Store Keys**:
```csharp
private const string KEY_LAST_CREATED_TRANSACTION_KEY = "LastCreatedTransactionKey";
private const string KEY_LAST_MEMO = "LastMemo";
```

**Helper Methods**:
```csharp
private TransactionsPage GetOrCreateTransactionsPage() => GetOrCreatePage<TransactionsPage>();
private TransactionDetailsPage GetOrCreateTransactionDetailsPage() => GetOrCreatePage<TransactionDetailsPage>();
```

---

## Phase 2: Feature File and Test Generation

### Task 2.1: Create TransactionRecord.feature

**File**: `tests/Functional/Features/TransactionRecord.feature` (NEW)

**Structure**:
```gherkin
@using:YoFi.V3.Tests.Functional.Steps
@namespace:YoFi.V3.Tests.Functional.Features
@baseclass:TransactionRecordSteps
@template:Features/FunctionalTest.mustache
Feature: Transaction Record Fields (Memo, Source, ExternalId)
    As a YoFi user
    I want to add memo, source, and external ID information to my transactions
    So that I can track transaction details and import information

Background:
    Given the application is running
    And these users exist:
        | Username |
        | alice    |

Rule: User can edit transactions via two different paths - quick edit from list or full edit from details

    Scenario: User quick edits Payee and Memo from transaction list
        Given I am logged in as a user with Editor role
        And I have a workspace with this transaction:
            | Date       | Payee      | Amount | Memo          | Source         | ExternalId |
            | 2024-12-25 | Coffee Co  | -5.50  | Morning latte | Chase Checking | CHK-001    |
        When I click the edit button on "Coffee Co" transaction
        Then I should see a modal titled "Quick Edit Transaction"
        And I should only see fields for Payee and Memo
        And I should not see fields for Date, Amount, Source, or ExternalId

    Scenario: User updates Memo via quick edit modal
        Given I am logged in as a user with Editor role
        And I have a workspace with this transaction:
            | Date       | Payee      | Amount | Memo          | Source         | ExternalId |
            | 2024-12-25 | Coffee Co  | -5.50  | Morning latte | Chase Checking | CHK-001    |
        When I quick edit the "Coffee Co" transaction
        And I change Memo to "Large latte with extra shot"
        And I click "Update"
        Then the modal should close
        And I should see the updated memo in the transaction list

    Scenario: User navigates from transaction list to details page
        Given I am logged in as a user with Editor role
        And I have a workspace with this transaction:
            | Date       | Payee     | Amount | Memo    | Source         | ExternalId |
            | 2024-12-24 | Gas Mart  | -40.00 | Fuel up | Chase Checking | CHK-002    |
        When I click on the "Gas Mart" transaction row
        Then I should navigate to the transaction details page
        And I should see all transaction fields displayed:
            | Field      | Value          |
            | Date       | 12/24/2024     |
            | Payee      | Gas Mart       |
            | Amount     | -40.00         |
            | Memo       | Fuel up        |
            | Source     | Chase Checking |
            | ExternalId | CHK-002        |

    Scenario: User edits all fields on transaction details page
        Given I am logged in as a user with Editor role
        And I am viewing the details page for a transaction with:
            | Date       | Payee     | Amount | Memo    | Source         | ExternalId |
            | 2024-12-24 | Gas Mart  | -40.00 | Fuel up | Chase Checking | CHK-002    |
        When I click the "Edit" button
        And I change Source to "Chase Visa"
        And I change ExternalId to "VISA-123"
        And I click "Save"
        Then I should see "Chase Visa" as the Source
        And I should see "VISA-123" as the ExternalId

    Scenario: User returns to list from transaction details page
        Given I am logged in as a user with Editor role
        And I am viewing the details page for a transaction
        When I click "Back to Transactions"
        Then I should return to the transaction list
        And I should see all my transactions

Rule: User can create new transactions with all optional fields and see them displayed correctly

    Scenario: User sees all fields in create transaction modal
        Given I am logged in as a user with Editor role
        And I have a workspace
        When I click "New Transaction"
        Then I should see a modal titled "Create Transaction"
        And I should see required fields: Date, Payee, Amount
        And I should see optional fields: Memo, Source, ExternalId

    Scenario: User creates transaction with all fields populated
        Given I am logged in as a user with Editor role
        And I have an empty workspace
        When I create a new transaction with:
            | Field      | Value                        |
            | Date       | 2024-12-25                   |
            | Payee      | Holiday Market               |
            | Amount     | -123.45                      |
            | Memo       | Christmas dinner ingredients |
            | Source     | Wells Fargo Checking 4567    |
            | ExternalId | WF-DEC-20241225-001          |
        Then the modal should close
        And I should see "Holiday Market" in the transaction list
        And the Memo column should display "Christmas dinner ingredients"

    Scenario: Created transaction displays all fields on details page
        Given I am logged in as a user with Editor role
        And I have created a transaction with Memo, Source, and ExternalId
        When I click on that transaction row
        Then I should navigate to the transaction details page
        And I should see all fields displayed correctly:
            | Field      | Value                        |
            | Memo       | Christmas dinner ingredients |
            | Source     | Wells Fargo Checking 4567    |
            | ExternalId | WF-DEC-20241225-001          |
```

### Task 2.2: Generate Test Class Manually

**File**: `tests/Functional/Tests/TransactionRecord.feature.cs` (NEW)

**Process**: Manually generate C# test class from the feature file following the patterns in [`tests/Functional/.roo/rules/functional-tests.md`](../../../tests/Functional/.roo/rules/functional-tests.md):

1. **Map Feature Tags to Template**:
   - `@using:YoFi.V3.Tests.Functional.Steps` → using statement
   - `@namespace:YoFi.V3.Tests.Functional.Features` → namespace
   - `@baseclass:TransactionRecordSteps` → base class
   - `@template:Features/FunctionalTest.mustache` → template structure

2. **Create Class Structure**:
   ```csharp
   using YoFi.V3.Tests.Functional.Steps;
   using YoFi.V3.Tests.Functional.Helpers;

   namespace YoFi.V3.Tests.Functional.Features;

   /// <summary>
   /// Transaction Record Fields (Memo, Source, ExternalId)
   /// </summary>
   /// <remarks>
   /// As a YoFi user
   /// I want to add memo, source, and external ID information to my transactions
   /// So that I can track transaction details and import information
   /// </remarks>
   public partial class TransactionRecord : TransactionRecordSteps
   {
       [SetUp]
       public async Task Background()
       {
           // Background steps
       }

       #region Rule: User can edit transactions via two different paths

       [Test]
       public async Task UserQuickEditsPayeeAndMemoFromTransactionList() { }

       // ... 7 more test methods

       #endregion
   }
   ```

3. **Map Gherkin Steps to C# Method Calls**:
   - Find step methods in `TransactionRecordSteps` by XML `<summary>` comments
   - Generate method calls with extracted parameters (quoted strings)
   - Handle DataTable creation for table steps
   - Preserve `And` keyword in comments but map to previous keyword when finding methods

4. **Create Regions for Rules**:
   - Each `Rule:` becomes a `#region Rule: [Name]` with description comment
   - Group scenarios under their respective rules

**Verification**: Test class should:
- Have correct namespace: `YoFi.V3.Tests.Functional.Features`
- Extend `TransactionRecordSteps`
- Contain 8 test methods (one per scenario)
- Have correct `[Test]` attributes
- Have Background as `[SetUp]` method
- Have 2 regions (one per Rule)

---

## Phase 3: Implementation Order (One Scenario at a Time)

### Scenario 1: User quick edits Payee and Memo from transaction list

**Status: ✅ COMPLETE - Implemented and passing (4s duration)**

**Steps to Implement**:

1. **`GivenIHaveAWorkspaceWithThisTransaction(DataTable)`**:
   - Create workspace for logged-in user
   - Parse DataTable row to extract: Date, Payee, Amount, Memo, Source, ExternalId
   - Seed transaction using enhanced `TransactionSeedRequest` (Task 1.1)
   - Store payee in object store for later reference

2. **`WhenIClickTheEditButtonOnTransaction(string payee)`**:
   - Get TransactionsPage
   - Navigate to transactions page (with page-ready wait)
   - Click edit button for specified payee: `await transactionsPage.ClickEditButtonAsync(payee)`
   - Wait for modal to appear

3. **`ThenIShouldSeeAModalTitled(string expectedTitle)`**:
   - Get modal title element
   - Assert title matches expected value

4. **`ThenIShouldOnlySeeFieldsForPayeeAndMemo()`**:
   - Assert EditPayeeInput is visible
   - Assert EditMemoInput is visible

5. **`ThenIShouldNotSeeFieldsForDateAmountSourceOrExternalId()`**:
   - Assert EditDateInput is NOT visible (or doesn't exist)
   - Assert EditAmountInput is NOT visible
   - Assert EditSourceInput is NOT visible
   - Assert EditExternalIdInput is NOT visible

**Run Test**: `dotnet test tests/Functional --filter "Name~quick_edits_Payee_and_Memo"`

---

### Scenario 2: User updates Memo via quick edit modal

**Status: ✅ COMPLETE - Implemented and passing (4s duration)**

**Steps to Implement**:

1. Reuse: `GivenIHaveAWorkspaceWithThisTransaction(DataTable)` (already implemented)

2. **`WhenIQuickEditTheTransaction(string payee)`**:
   - Get TransactionsPage
   - Navigate to transactions page (with page-ready wait)
   - Click edit button for specified payee
   - Wait for modal to appear

3. **`WhenIChangeMemoTo(string newMemo)`**:
   - Get TransactionsPage
   - Fill memo field: `await transactionsPage.FillEditMemoAsync(newMemo)`
   - Store new memo value in object store

4. **`WhenIClickUpdate()` (generic step - already exists in CommonSteps?)**:
   - Click Update button on modal
   - Wait for modal to close

5. **`ThenTheModalShouldClose()`**:
   - Assert modal is not visible
   - Wait for loading spinner to hide

6. **`ThenIShouldSeeTheUpdatedMemoInTheTransactionList()`**:
   - Get updated memo from object store
   - Get memo value from transaction row in list
   - Assert memo matches expected value

**Run Test**: `dotnet test tests/Functional --filter "Name~updates_Memo_via_quick_edit"`

---

### Scenario 3: User navigates from transaction list to details page

**Status: ✅ COMPLETE - Implemented and passing (5s duration)**

**Steps to Implement**:

1. Reuse: `GivenIHaveAWorkspaceWithThisTransaction(DataTable)` (already implemented)

2. **`WhenIClickOnTheTransactionRow(string payee)`**:
   - Get TransactionsPage
   - Click on transaction row: `await transactionsPage.ClickTransactionRowAsync(payee)`

3. **`ThenIShouldNavigateToTheTransactionDetailsPage()`**:
   - Assert URL contains `/transactions/` (details page pattern)
   - Get TransactionDetailsPage
   - Wait for page ready: `await detailsPage.WaitForPageReadyAsync()`

4. **`ThenIShouldSeeAllTransactionFieldsDisplayed(DataTable)`**:
   - Get TransactionDetailsPage
   - Parse DataTable to get expected field values
   - For each field, assert displayed value matches expected:
     - `await detailsPage.GetDisplayedDateAsync()` → Assert "12/24/2024"
     - `await detailsPage.GetDisplayedPayeeAsync()` → Assert "Gas Mart"
     - `await detailsPage.GetDisplayedAmountAsync()` → Assert "-40.00"
     - `await detailsPage.GetDisplayedMemoAsync()` → Assert "Fuel up"
     - `await detailsPage.GetDisplayedSourceAsync()` → Assert "Chase Checking"
     - `await detailsPage.GetDisplayedExternalIdAsync()` → Assert "CHK-002"

**Run Test**: `dotnet test tests/Functional --filter "Name~navigates_from_transaction_list_to_details"`

---

### Scenario 4: User edits all fields on transaction details page

**Steps to Implement**:

1. **`GivenIAmViewingTheDetailsPageForATransactionWith(DataTable)`**:
   - Create workspace and transaction (reuse logic from Scenario 1)
   - Navigate directly to details page: `await detailsPage.NavigateAsync(transactionKey)`
   - Wait for page ready

2. **`WhenIClickTheEditButton()`**:
   - Get TransactionDetailsPage
   - Click Edit button: `await detailsPage.StartEditingAsync()`
   - Wait for edit mode to activate

3. **`WhenIChangeSourceTo(string newSource)`**:
   - Get TransactionDetailsPage
   - Fill source field: `await detailsPage.FillSourceAsync(newSource)`
   - Store new source in object store

4. **`WhenIChangeExternalIdTo(string newExternalId)`**:
   - Get TransactionDetailsPage
   - Fill external ID field: `await detailsPage.FillExternalIdAsync(newExternalId)`
   - Store new external ID in object store

5. **`WhenIClickSave()`**:
   - Get TransactionDetailsPage
   - Click Save button: `await detailsPage.SaveAsync()`
   - Wait for save to complete (display mode returns)

6. **`ThenIShouldSeeValueAsField(string expectedValue, string fieldName)`**:
   - Get TransactionDetailsPage
   - Based on fieldName, call appropriate getter:
     - "Source" → `await detailsPage.GetDisplayedSourceAsync()`
     - "ExternalId" → `await detailsPage.GetDisplayedExternalIdAsync()`
   - Assert value matches expected

**Run Test**: `dotnet test tests/Functional --filter "Name~edits_all_fields_on_transaction_details"`

---

### Scenario 5: User returns to list from transaction details page

**Steps to Implement**:

1. Reuse: `GivenIAmViewingTheDetailsPageForATransactionWith(DataTable)` (already implemented)

2. **`WhenIClickBackToTransactions()`**:
   - Get TransactionDetailsPage
   - Click back button/link (need to add to POM if missing)
   - OR: Navigate directly to list page

3. **`ThenIShouldReturnToTheTransactionList()`**:
   - Assert URL is `/transactions` (list page)
   - Get TransactionsPage
   - Wait for page ready: `await transactionsPage.WaitForPageReadyAsync()`

4. **`ThenIShouldSeeAllMyTransactions()`**:
   - Get TransactionsPage
   - Get transaction count: `await transactionsPage.GetTransactionCountAsync()`
   - Assert count is greater than 0

**Run Test**: `dotnet test tests/Functional --filter "Name~returns_to_list_from_transaction_details"`

---

### Scenario 6: User sees all fields in create transaction modal

**Steps to Implement**:

1. **`GivenIHaveAWorkspace()`** (might already exist in WorkspaceTenancySteps):
   - Create workspace for logged-in user
   - Store workspace key/name

2. **`WhenIClickNewTransaction()`**:
   - Get TransactionsPage
   - Navigate to transactions page
   - Click New Transaction button: `await transactionsPage.ClickNewTransactionAsync()`
   - Wait for modal to appear

3. **`ThenIShouldSeeRequiredFields()`**:
   - Assert CreateDateInput is visible
   - Assert CreatePayeeInput is visible
   - Assert CreateAmountInput is visible

4. **`ThenIShouldSeeOptionalFields()`**:
   - Assert CreateMemoInput is visible
   - Assert CreateSourceInput is visible
   - Assert CreateExternalIdInput is visible

**Run Test**: `dotnet test tests/Functional --filter "Name~sees_all_fields_in_create"`

---

### Scenario 7: User creates transaction with all fields populated

**Steps to Implement**:

1. **`GivenIHaveAnEmptyWorkspace()`**:
   - Create workspace for logged-in user
   - Do NOT seed any transactions
   - Store workspace key/name

2. **`WhenICreateANewTransactionWith(DataTable)`**:
   - Get TransactionsPage
   - Navigate to transactions page
   - Click New Transaction button
   - Parse DataTable to get field values
   - Fill all fields:
     - `await transactionsPage.FillCreateDateAsync(date)`
     - `await transactionsPage.FillCreatePayeeAsync(payee)`
     - `await transactionsPage.FillCreateAmountAsync(amount)`
     - `await transactionsPage.FillCreateMemoAsync(memo)`
     - `await transactionsPage.FillCreateSourceAsync(source)`
     - `await transactionsPage.FillCreateExternalIdAsync(externalId)`
   - Click Create/Save button
   - Store payee and memo in object store

3. Reuse: `ThenTheModalShouldClose()` (already implemented)

4. **`ThenIShouldSeeInTheTransactionList(string payee)`** (might already exist):
   - Get TransactionsPage
   - Assert transaction with payee is visible: `await transactionsPage.HasTransactionAsync(payee)`

5. **`ThenTheMemoColumnShouldDisplay(string expectedMemo)`**:
   - Get TransactionsPage
   - Get memo from object store (or use parameter)
   - Get memo value from transaction row
   - Assert memo matches expected value

**Run Test**: `dotnet test tests/Functional --filter "Name~creates_transaction_with_all_fields"`

---

### Scenario 8: Created transaction displays all fields on details page

**Steps to Implement**:

1. **`GivenIHaveCreatedATransactionWithMemoSourceAndExternalId()`**:
   - Create workspace
   - Create transaction with all fields populated (reuse create logic)
   - Store transaction key for navigation

2. **`WhenIClickOnThatTransactionRow()`**:
   - Get TransactionsPage
   - Get payee from object store
   - Click transaction row: `await transactionsPage.ClickTransactionRowAsync(payee)`

3. Reuse: `ThenIShouldNavigateToTheTransactionDetailsPage()` (already implemented)

4. **`ThenIShouldSeeAllFieldsDisplayedCorrectly(DataTable)`**:
   - Get TransactionDetailsPage
   - Parse DataTable to get expected field values
   - For each field, assert displayed value matches expected
   - Similar to Scenario 3 but only checking Memo, Source, ExternalId

**Run Test**: `dotnet test tests/Functional --filter "Name~Created_transaction_displays_all_fields"`

---

## Phase 4: Final Verification

### Task 4.1: Run All Transaction Record Tests

**Command**:
```bash
pwsh -File ./scripts/Run-FunctionalTestsVsContainer.ps1 -Filter "TransactionRecord"
```

**Expected Result**: All 8 scenarios pass.

---

### Task 4.2: Run Full Functional Test Suite

**Command**:
```bash
pwsh -File ./scripts/Run-FunctionalTestsVsContainer.ps1
```

**Expected Result**: All existing tests + 8 new tests pass.

---

## Implementation Notes

### DataTable Parsing Pattern

For steps that accept DataTables, use the existing `DataTableExtensions` helper:

```csharp
// Single row with named columns
var row = table.Rows[0];
var date = DateOnly.Parse(row["Date"]);
var payee = row["Payee"];
var amount = decimal.Parse(row["Amount"]);
var memo = row["Memo"];
var source = row["Source"];
var externalId = row["ExternalId"];
```

### Object Store Usage

Store intermediate values for later assertions:

```csharp
// Store transaction payee for later reference
_objectStore.Add(KEY_LAST_TRANSACTION_PAYEE, payee);

// Store created transaction key for navigation
_objectStore.Add(KEY_LAST_CREATED_TRANSACTION_KEY, transactionKey);

// Retrieve stored value
var payee = _objectStore.Get<string>(KEY_LAST_TRANSACTION_PAYEE);
```

### Page Object Model Polling Pattern

Always wait for page-ready state before interacting:

```csharp
// List page
await transactionsPage.NavigateAsync(waitForReady: true);
await transactionsPage.WaitForNewTransactionButtonEnabledAsync();

// Details page
await detailsPage.NavigateAsync(transactionKey);
await detailsPage.WaitForPageReadyAsync();
await detailsPage.WaitForEditButtonEnabledAsync();
```

### Test Data Prefix

All test usernames and workspace names MUST use `__TEST__` prefix:

```csharp
var fullUsername = AddTestPrefix(username);
var fullWorkspaceName = AddTestPrefix(workspaceName);
```

This is inherited from `WorkspaceTenancySteps` base class.

---

## Success Criteria

✅ All 8 functional test scenarios implemented
✅ All tests pass when run individually
✅ All tests pass when run as a suite
✅ No regressions in existing functional tests
✅ Test execution time under 60 seconds total
✅ Page Object Models cover all necessary interactions
✅ Test data cleanup works correctly (no orphaned test data)

---

## Risk Mitigation

**Risk**: Frontend SSR/hydration timing issues
- **Mitigation**: Page-ready pattern already implemented in both POMs and frontend pages

**Risk**: Modal state detection (open/closed)
- **Mitigation**: Use explicit waits for modal visibility/hidden state

**Risk**: Transaction row click ambiguity
- **Mitigation**: Use unique payee names in test data, click by `data-test-id` where possible

**Risk**: Details page navigation
- **Mitigation**: Verify URL pattern and wait for page-ready state

**Risk**: Test data cleanup failures
- **Mitigation**: TestControlController's `DeleteAllTestData` endpoint handles cascading deletes

---

## Next Steps

1. **Implement Task 1.1**: Enhance `TransactionSeedRequest` DTO
2. **Implement Task 1.2**: Create `TransactionRecordSteps` class
3. **Implement Task 2.1**: Create `TransactionRecord.feature` file
4. **Implement Task 2.2**: Generate test class (automatic)
5. **Implement Scenarios 1-8**: One at a time, with test execution after each
6. **Implement Task 4.1-4.2**: Final verification

**Switch to Code Mode** after user approval of this plan.
