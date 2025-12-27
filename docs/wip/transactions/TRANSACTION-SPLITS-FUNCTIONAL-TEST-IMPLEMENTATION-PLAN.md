---
status: Approved
feature: Transaction Splits - Alpha-1 Story 3 (Category Field)
scope: Detailed implementation plan for functional test changes
target_release: Alpha-1
related_plan: TRANSACTION-SPLITS-FUNCTIONAL-TEST-PLAN.md
---

# Functional Test Implementation Plan: Transaction Splits Category Tests

## Overview

This document provides detailed, actionable instructions for implementing the 4 functional test scenarios approved in Step 10.5:

**Test Updates (2):**
1. Update "User creates transaction with all fields populated" - Add Category to data table
2. Update "Quick edit modal shows only Payee and Memo fields" - Change to "Payee, Category, and Memo"

**New Scenarios (3):**
1. User edits category via quick edit and sees it in list
2. Transaction details page displays category
3. User edits category on details page

**Implementation Date**: 2025-12-27

---

## Section 1: Page Object Changes

### 1.1 TransactionsPage.cs - Add Category Locators and Methods

**File**: [`tests/Functional/Pages/TransactionsPage.cs`](../../../tests/Functional/Pages/TransactionsPage.cs)

#### Changes Required:

**A. Add Category locator for Create Modal** (Insert after line 160)

```csharp
/// <summary>
/// Category input in create modal
/// </summary>
public ILocator CreateCategoryInput => Page!.GetByTestId("create-transaction-category");
```

**Location**: Insert between `CreateExternalIdInput` (line 160) and `CreateButton` (line 165)

**B. Add Category locator for Edit Modal** (Insert after line 194)

```csharp
/// <summary>
/// Category input in edit modal
/// </summary>
public ILocator EditCategoryInput => Page!.GetByTestId("edit-transaction-category");
```

**Location**: Insert between `EditMemoInput` (line 194) and `EditAmountInput` (line 199)

**C. Add Category column getter method** (Insert after line 816)

```csharp
/// <summary>
/// Gets the category text from a transaction row by payee name
/// </summary>
/// <param name="payeeName">The payee name of the transaction</param>
/// <returns>The category text as displayed in the table</returns>
public async Task<string?> GetTransactionCategoryAsync(string payeeName)
{
    var row = GetTransactionRowByPayee(payeeName);
    var categoryCell = row.Locator("td.category-cell");
    return await categoryCell.TextContentAsync();
}
```

**Location**: Insert after `GetTransactionMemoAsync()` method (line 816)

**D. Add FillCreateCategory method** (Insert after line 417)

```csharp
/// <summary>
/// Fills the category field in the create transaction modal
/// </summary>
/// <param name="category">Transaction category</param>
/// <remarks>
/// Single action method. Use this when you need to test partial form submission or validation.
/// </remarks>
public async Task FillCreateCategoryAsync(string category)
{
    await CreateCategoryInput.FillAsync(category);
}
```

**Location**: Insert after `FillCreateExternalIdAsync()` method (line 417)

**E. Add FillEditCategory method** (Insert after line 583)

```csharp
/// <summary>
/// Fills the category field in the edit transaction modal
/// </summary>
/// <param name="newCategory">New category text</param>
/// <remarks>
/// Single action method. Use this when you need to test partial form updates or validation.
/// Must be called after OpenEditModalAsync.
/// </remarks>
public async Task FillEditCategoryAsync(string newCategory)
{
    await EditCategoryInput.FillAsync(newCategory);
}
```

**Location**: Insert after `FillEditMemoAsync()` method (line 583)

**Summary**: TransactionsPage.cs needs 5 additions:
- 2 new locators (CreateCategoryInput, EditCategoryInput)
- 3 new methods (GetTransactionCategoryAsync, FillCreateCategoryAsync, FillEditCategoryAsync)

---

### 1.2 TransactionDetailsPage.cs - Add Category Locators and Methods

**File**: [`tests/Functional/Pages/TransactionDetailsPage.cs`](../../../tests/Functional/Pages/TransactionDetailsPage.cs)

#### Changes Required:

**A. Add Category display locator** (Insert after line 119)

```csharp
/// <summary>
/// Transaction category field (display mode)
/// </summary>
public ILocator CategoryDisplay => Page!.GetByTestId("transaction-category");
```

**Location**: Insert between `ExternalIdDisplay` (line 119) and `#endregion` for Display Mode Elements

**B. Add Category edit input locator** (Insert after line 158)

```csharp
/// <summary>
/// Category input (edit mode)
/// </summary>
public ILocator EditCategoryInput => Page!.GetByTestId("edit-category");
```

**Location**: Insert after `EditExternalIdInput` (line 158) and before `#endregion` for Edit Mode Elements

**C. Add FillCategory method** (Insert after line 353)

```csharp
/// <summary>
/// Fills the category field in edit mode
/// </summary>
/// <param name="category">Category text</param>
public async Task FillCategoryAsync(string category)
{
    await EditCategoryInput.FillAsync(category);
}
```

**Location**: Insert after `FillExternalIdAsync()` method (line 353)

**D. Add GetCategory query method** (Insert after line 467)

```csharp
/// <summary>
/// Gets the category text from the details display
/// </summary>
/// <returns>The category text</returns>
public async Task<string?> GetCategoryAsync()
{
    return await CategoryDisplay.TextContentAsync();
}
```

**Location**: Insert after `GetExternalIdAsync()` method (line 467)

**Summary**: TransactionDetailsPage.cs needs 4 additions:
- 2 new locators (CategoryDisplay, EditCategoryInput)
- 2 new methods (FillCategoryAsync, GetCategoryAsync)

---

## Section 2: Step Definition Changes

### 2.1 TransactionRecordSteps.cs - Add Category Handling

**File**: [`tests/Functional/Steps/TransactionRecordSteps.cs`](../../../tests/Functional/Steps/TransactionRecordSteps.cs)

#### Changes Required:

**A. Add Category object store key** (Insert after line 29)

```csharp
protected const string KEY_TRANSACTION_CATEGORY = "TransactionCategory";
```

**Location**: Insert after `KEY_TRANSACTION_EXTERNAL_ID` constant (line 29)

**B. Update GivenIHaveAWorkspaceWithATransaction to handle Category** (Modify lines 91-143)

Add Category handling after ExternalId parsing (around line 103):

```csharp
transactionTable.TryGetKeyValue("Category", out var category);
```

Add Category to seed request (around line 118):

```csharp
var seedRequest = new Generated.TransactionSeedRequest
{
    Count = 1,
    PayeePrefix = payee,
    Memo = memo,
    Source = source,
    ExternalId = externalId,
    Category = category  // ADD THIS LINE
};
```

Add Category to object store (after line 140):

```csharp
if (!string.IsNullOrEmpty(category))
    _objectStore.Add(KEY_TRANSACTION_CATEGORY, category);
```

**C. Update WhenIFillInTheFollowingTransactionFields** (Modify lines 572-621)

Add Category case to the switch statement (after "Memo" case, before "Source" case around line 609):

```csharp
case "Category":
    await transactionsPage.FillCreateCategoryAsync(value);
    _objectStore.Add(KEY_TRANSACTION_CATEGORY, value);
    break;
```

**D. Add new When step for Category editing** (Insert after line 271)

```csharp
/// <summary>
/// Changes the category field in the edit modal or details page.
/// </summary>
/// <param name="newCategory">The new category value.</param>
/// <remarks>
/// Fills the category field and stores the new value in object store for verification.
/// Works in both quick edit modal and transaction details page edit mode.
/// </remarks>
[When("I change Category to {newCategory}")]
protected async Task WhenIChangeCategoryTo(string newCategory)
{
    // When: Check if we're in TransactionDetailsPage or TransactionsPage edit mode
    if (_objectStore.Contains<string>(KEY_EDIT_MODE))
    {
        var editMode = _objectStore.Get<string>(KEY_EDIT_MODE);
        if (editMode == "TransactionDetailsPage")
        {
            // Fill category in details page edit mode
            var detailsPage = GetOrCreatePage<TransactionDetailsPage>();
            await detailsPage.FillCategoryAsync(newCategory);
        }
        else
        {
            // Fill category in quick edit modal
            var transactionsPage = GetOrCreatePage<TransactionsPage>();
            await transactionsPage.FillEditCategoryAsync(newCategory);
        }
    }
    else
    {
        // Default to quick edit modal
        var transactionsPage = GetOrCreatePage<TransactionsPage>();
        await transactionsPage.FillEditCategoryAsync(newCategory);
    }

    // And: Store the new category for verification
    _objectStore.Add(KEY_TRANSACTION_CATEGORY, newCategory);
}
```

**Location**: Insert after `WhenIChangeMemoTo()` method (line 271)

**E. Update ThenIShouldOnlySeeFieldsForPayeeAndMemo** (Modify lines 659-670)

Change method name and implementation to verify Category is also visible:

```csharp
/// <summary>
/// Verifies that only Payee, Category, and Memo fields are visible in the quick edit modal.
/// </summary>
/// <remarks>
/// Tests the quick edit modal constraint - only Payee, Category, and Memo fields should
/// be editable via the modal (PATCH endpoint).
/// </remarks>
[Then("I should only see fields for \"Payee\", \"Category\", and \"Memo\"")]
protected async Task ThenIShouldOnlySeeFieldsForPayeeCategoryAndMemo()
{
    // Then: Verify Payee field is visible
    var transactionsPage = GetOrCreatePage<TransactionsPage>();
    var payeeVisible = await transactionsPage.EditPayeeInput.IsVisibleAsync();
    Assert.That(payeeVisible, Is.True, "Payee field should be visible in quick edit modal");

    // And: Verify Category field is visible
    var categoryVisible = await transactionsPage.EditCategoryInput.IsVisibleAsync();
    Assert.That(categoryVisible, Is.True, "Category field should be visible in quick edit modal");

    // And: Verify Memo field is visible
    var memoVisible = await transactionsPage.EditMemoInput.IsVisibleAsync();
    Assert.That(memoVisible, Is.True, "Memo field should be visible in quick edit modal");
}
```

**F. Add Then step for Category verification in list** (Insert after line 762)

```csharp
/// <summary>
/// Verifies that the updated category appears in the transaction list.
/// </summary>
/// <remarks>
/// Retrieves the payee and new category from object store, waits for page to update,
/// and verifies the category in the transaction list matches the updated value.
/// </remarks>
[Then("I should see the updated category in the transaction list")]
protected async Task ThenIShouldSeeTheUpdatedCategoryInTheTransactionList()
{
    // Then: Get the payee and new category from object store
    var payee = GetRequiredFromStore(KEY_TRANSACTION_PAYEE);
    var expectedCategory = GetRequiredFromStore(KEY_TRANSACTION_CATEGORY);

    // And: Wait for page to update (loading spinner to hide)
    var transactionsPage = GetOrCreatePage<TransactionsPage>();
    await transactionsPage.WaitForLoadingCompleteAsync();

    // And: Verify the category in the transaction list
    var actualCategory = await transactionsPage.GetTransactionCategoryAsync(payee);

    Assert.That(actualCategory?.Trim(), Is.EqualTo(expectedCategory),
        $"Expected category to be '{expectedCategory}' but was '{actualCategory}'");
}
```

**Location**: Insert after `ThenIShouldSeeTheUpdatedMemoInTheTransactionList()` method (line 762)

**G. Update ThenIShouldSeeTheFollowingFieldsInTheCreateForm** (Modify lines 1104-1130)

Add Category case to the switch statement (after "Amount" case, before "Memo" case around line 1122):

```csharp
"Category" => await transactionsPage.CreateCategoryInput.IsVisibleAsync(),
```

**H. Update ThenIShouldSeeAllTheExpectedTransactionFieldsDisplayed** (Modify lines 917-961)

Add Category verification after Memo check (around line 944):

```csharp
if (_objectStore.Contains<string>(KEY_TRANSACTION_CATEGORY))
{
    var expectedCategory = _objectStore.Get<string>(KEY_TRANSACTION_CATEGORY);
    var categoryValue = await detailsPage.GetCategoryAsync();
    Assert.That(categoryValue?.Trim(), Is.EqualTo(expectedCategory),
        $"Category field should be '{expectedCategory}'");
}
```

**I. Update ThenIShouldSeeValueAsField** (Modify lines 1026-1043)

Add Category case to the switch statement (after "Source" case around line 1035):

```csharp
"Category" => await detailsPage.GetCategoryAsync(),
```

**Summary**: TransactionRecordSteps.cs needs 9 changes:
- 1 new constant (KEY_TRANSACTION_CATEGORY)
- Update 4 existing methods to handle Category
- Add 2 new step methods (WhenIChangeCategoryTo, ThenIShouldSeeTheUpdatedCategoryInTheTransactionList)
- Rename 1 method (ThenIShouldOnlySeeFieldsForPayeeCategoryAndMemo)

---

## Section 3: Feature File Changes

### 3.1 TransactionRecord.feature - Updates and New Scenarios

**File**: [`tests/Functional/Features/TransactionRecord.feature`](../../../tests/Functional/Features/TransactionRecord.feature)

#### Changes Required:

**A. Update Scenario 1: Quick edit modal field description** (Line 17)

**BEFORE** (lines 17-27):
```gherkin
    Scenario: Quick edit modal shows only Payee and Memo fields
        Given I have a workspace with a transaction:
            | Field  | Value           |
            | Payee  | Coffee Shop     |
            | Amount | 5.50            |
            | Memo   | Morning coffee  |
        And I am on the transactions page
        When I click the "Edit" button on the transaction
        Then I should see a modal titled "Quick Edit Transaction"
        And I should only see fields for "Payee" and "Memo"
        And I should not see fields for "Date", "Amount", "Source", or "ExternalId"
```

**AFTER**:
```gherkin
    Scenario: Quick edit modal shows Payee, Category, and Memo fields
        Given I have a workspace with a transaction:
            | Field  | Value           |
            | Payee  | Coffee Shop     |
            | Amount | 5.50            |
            | Memo   | Morning coffee  |
        And I am on the transactions page
        When I click the "Edit" button on the transaction
        Then I should see a modal titled "Quick Edit Transaction"
        And I should only see fields for "Payee", "Category", and "Memo"
        And I should not see fields for "Date", "Amount", "Source", or "ExternalId"
```

**Changes**:
- Line 17: Change title from "shows only Payee and Memo fields" to "shows Payee, Category, and Memo fields"
- Line 26: Change from `"Payee" and "Memo"` to `"Payee", "Category", and "Memo"`

---

**B. Update Scenario 7: Add Category to create transaction data** (Lines 93-107)

**BEFORE** (lines 93-107):
```gherkin
    Scenario: User creates transaction with all fields populated
        Given I am on the transactions page
        When I click the "Add Transaction" button
        And I fill in the following transaction fields:
            | Field       | Value                   |
            | Date        | 2024-06-15              |
            | Payee       | Office Depot            |
            | Amount      | 250.75                  |
            | Memo        | Printer paper and toner |
            | Source      | Business Card           |
            | External ID | OD-2024-0615-001        |
        And I click "Save"
        Then the modal should close
        And I should see a transaction with Payee "Office Depot"
```

**AFTER**:
```gherkin
    Scenario: User creates transaction with all fields populated
        Given I am on the transactions page
        When I click the "Add Transaction" button
        And I fill in the following transaction fields:
            | Field       | Value                   |
            | Date        | 2024-06-15              |
            | Payee       | Office Depot            |
            | Amount      | 250.75                  |
            | Category    | Office Supplies         |
            | Memo        | Printer paper and toner |
            | Source      | Business Card           |
            | External ID | OD-2024-0615-001        |
        And I click "Save"
        Then the modal should close
        And I should see a transaction with Payee "Office Depot"
```

**Changes**:
- Insert new row in data table after "Amount" (line 100): `| Category    | Office Supplies         |`

---

**C. Update Scenario 6: Add Category to field list** (Lines 80-91)

**BEFORE** (lines 80-91):
```gherkin
    Scenario: User sees all fields in create transaction modal
        Given I am on the transactions page
        When I click the "Add Transaction" button
        Then I should see a create transaction modal
        And I should see the following fields in the create form:
            | Field       |
            | Date        |
            | Payee       |
            | Amount      |
            | Memo        |
            | Source      |
            | External ID |
```

**AFTER**:
```gherkin
    Scenario: User sees all fields in create transaction modal
        Given I am on the transactions page
        When I click the "Add Transaction" button
        Then I should see a create transaction modal
        And I should see the following fields in the create form:
            | Field       |
            | Date        |
            | Payee       |
            | Amount      |
            | Category    |
            | Memo        |
            | Source      |
            | External ID |
```

**Changes**:
- Insert new row in data table after "Amount" (line 88): `| Category    |`

---

**D. Add NEW Scenario: User edits category via quick edit** (Insert after line 42)

```gherkin
    Scenario: User edits category via quick edit and sees it in list
        Given I have a workspace with a transaction:
            | Field    | Value       |
            | Payee    | Grocery Co  |
            | Amount   | -45.67      |
            | Category | Food        |
        And I am on the transactions page
        When I quick edit the "Grocery Co" transaction
        And I change Category to "Groceries"
        And I click "Update"
        Then the modal should close
        And I should see the updated category in the transaction list
```

**Location**: Insert after Scenario 2 "User updates Memo via quick edit modal" (ends at line 42)

---

**E. Add NEW Scenario: Transaction details page displays category** (Insert after line 55)

```gherkin
    Scenario: Transaction details page displays category
        Given I have a workspace with a transaction:
            | Field    | Value          |
            | Payee    | Restaurant XYZ |
            | Amount   | -32.50         |
            | Category | Dining         |
        And I am on the transactions page
        When I click on the transaction row
        Then I should navigate to the transaction details page
        And I should see "Dining" as the Category
```

**Location**: Insert after Scenario 3 "User navigates from transaction list to details page" (ends at line 55)

---

**F. Add NEW Scenario: User edits category on details page** (Insert after line 70)

```gherkin
    Scenario: User edits category on transaction details page
        Given I am viewing the details page for a transaction with:
            | Field    | Value      |
            | Payee    | Hardware   |
            | Amount   | -89.99     |
            | Category | Tools      |
        When I click the "Edit" button
        And I change Category to "Home Improvement"
        And I click "Save"
        Then I should see "Home Improvement" as the Category
```

**Location**: Insert after Scenario 4 "User edits all fields on transaction details page" (ends at line 70)

---

**G. Update Scenario 8: Add Category to created transaction** (Lines 108-121)

**BEFORE** (lines 108-121):
```gherkin
    Scenario: Created transaction displays all fields on details page
        Given I am on the transactions page
        When I click the "Add Transaction" button
        And I fill in the following transaction fields:
            | Field       | Value                   |
            | Date        | 2024-06-15              |
            | Payee       | Office Depot            |
            | Amount      | 250.75                  |
            | Memo        | Printer paper and toner |
            | Source      | Business Card           |
            | External ID | OD-2024-0615-001        |
        And I click "Save"
        And I click on the transaction row
        Then I should see all the expected transaction fields displayed
```

**AFTER**:
```gherkin
    Scenario: Created transaction displays all fields on details page
        Given I am on the transactions page
        When I click the "Add Transaction" button
        And I fill in the following transaction fields:
            | Field       | Value                   |
            | Date        | 2024-06-15              |
            | Payee       | Office Depot            |
            | Amount      | 250.75                  |
            | Category    | Office Supplies         |
            | Memo        | Printer paper and toner |
            | Source      | Business Card           |
            | External ID | OD-2024-0615-001        |
        And I click "Save"
        And I click on the transaction row
        Then I should see all the expected transaction fields displayed
```

**Changes**:
- Insert new row in data table after "Amount" (line 115): `| Category    | Office Supplies         |`

---

**Summary**: TransactionRecord.feature needs 7 changes:
- 4 updates to existing scenarios (lines 17, 26, 88, 100, 115)
- 3 new scenarios added (quick edit category, details display category, details edit category)

---

## Section 4: Implementation Order

Execute changes in this specific order to minimize errors and enable incremental testing:

### Phase 1: Page Object Foundation (Prerequisites)
**Goal**: Add all locators and methods before writing step definitions

1. **TransactionsPage.cs** - Add Category support
   - Add `CreateCategoryInput` locator (line ~161)
   - Add `EditCategoryInput` locator (line ~195)
   - Add `FillCreateCategoryAsync()` method (line ~418)
   - Add `FillEditCategoryAsync()` method (line ~584)
   - Add `GetTransactionCategoryAsync()` method (line ~817)

2. **TransactionDetailsPage.cs** - Add Category support
   - Add `CategoryDisplay` locator (line ~120)
   - Add `EditCategoryInput` locator (line ~159)
   - Add `FillCategoryAsync()` method (line ~354)
   - Add `GetCategoryAsync()` method (line ~468)

**Verification**: Build the Functional test project to verify no compilation errors:
```bash
dotnet build tests/Functional
```

---

> [!IMPORTANT] **IMPLEMENT AND TEST ONE SCENARIO AT A TIME (CRITICAL - never implement multiple scenarios simultaneously):**

Following phases should be iterated for each scenario.

---

### Phase 2: Step Definitions (Business Logic)
**Goal**: Add step methods that use the Page Object methods

3. **TransactionRecordSteps.cs** - Add Category handling
   - Add `KEY_TRANSACTION_CATEGORY` constant (line ~30)
   - Add `WhenIChangeCategoryTo()` step method (line ~272)
   - Add `ThenIShouldSeeTheUpdatedCategoryInTheTransactionList()` step method (line ~763)
   - Update `ThenIShouldOnlySeeFieldsForPayeeAndMemo()` → rename to `ThenIShouldOnlySeeFieldsForPayeeCategoryAndMemo()` (line ~659)
   - Update `GivenIHaveAWorkspaceWithATransaction()` to handle Category (lines ~103, ~118, ~141)
   - Update `WhenIFillInTheFollowingTransactionFields()` to handle Category (line ~610)
   - Update `ThenIShouldSeeTheFollowingFieldsInTheCreateForm()` to check Category (line ~1122)
   - Update `ThenIShouldSeeAllTheExpectedTransactionFieldsDisplayed()` to verify Category (line ~945)
   - Update `ThenIShouldSeeValueAsField()` to support Category (line ~1036)

**Verification**: Build the Functional test project again:
```bash
dotnet build tests/Functional
```

---

### Phase 3: Feature File Updates (Test Scenarios)
**Goal**: Update Gherkin scenarios to use new Category functionality

4. **TransactionRecord.feature** - Update existing scenarios
   - Update Scenario 1 title and assertion (lines 17, 26)
   - Update Scenario 6 field list (line 88)
   - Update Scenario 7 data table (line 100)
   - Update Scenario 8 data table (line 115)

5. **TransactionRecord.feature** - Add new scenarios
   - Add "User edits category via quick edit" scenario (after line 42)
   - Add "Transaction details page displays category" scenario (after line 55)
   - Add "User edits category on details page" scenario (after line 70)

6. **TransactionRecord.feature.cs** - **MANUALLY regenerate test file**

   **CRITICAL**: Per [`tests/Functional/INSTRUCTIONS.md`](../../../tests/Functional/INSTRUCTIONS.md), test file generation is **MANUAL**, not automatic.

   You must manually update [`tests/Functional/Tests/TransactionRecord.feature.cs`](../../../tests/Functional/Tests/TransactionRecord.feature.cs):
   - Follow the mustache template at [`Features/FunctionalTest.mustache`](../../../tests/Functional/Features/FunctionalTest.mustache)
   - Map each Gherkin step to methods in [`TransactionRecordSteps.cs`](../../../tests/Functional/Steps/TransactionRecordSteps.cs) using their `[Given]`/`[When]`/`[Then]` attribute patterns
   - Add `[Test]` attribute to each new scenario method
   - Generate DataTable code for steps with table data (see INSTRUCTIONS.md lines 62-108)
   - Ensure all step method calls use exact method names from the base class

**After manual regeneration, verify build**:
```bash
dotnet build tests/Functional
```

---

### Phase 4: Frontend Verification (Before Running Tests)
**Goal**: Verify frontend has required data-test-id attributes

7. **Verify frontend test IDs exist** (Read-only verification)
   - Check `src/FrontEnd.Nuxt/app/pages/transactions/index.vue`:
     - Line 688-711: Create modal should have `data-test-id="create-transaction-category"`
     - Line 764-820: Edit modal should have `data-test-id="edit-transaction-category"`
     - Line 524: Table should have Category column with `class="category-cell"`
   - Check `src/FrontEnd.Nuxt/app/pages/transactions/[key].vue`:
     - Details page should have `data-test-id="transaction-category"` (display mode)
     - Details page should have `data-test-id="edit-category"` (edit mode)

**If any test IDs are missing**, stop and update the frontend first. The functional tests will fail without these locators.

---

### Phase 5: Run Tests (Validation)

- Run ONLY this scenario: `dotnet test tests/Functional --filter "DisplayName~ScenarioName"`
- Example: `dotnet test tests/Functional --filter "DisplayName~CreateNewTransaction"`
- This runs against the local development environment (fast feedback loop)

**Expected Results**:
- All 10 scenarios should pass (7 original + 3 new)
- Test execution time should be similar to before (~2-3 minutes)
- No new failures introduced

---

## Section 5: Verification Checklist

Use this checklist to verify each change before moving to the next phase:

### Phase 1 Verification: Page Objects
- [ ] TransactionsPage.cs compiles without errors
- [ ] TransactionDetailsPage.cs compiles without errors
- [ ] All 7 new methods have XML documentation comments
- [ ] All locators follow existing naming patterns (e.g., `CreateCategoryInput`, `EditCategoryInput`)
- [ ] Build succeeds: `dotnet build tests/Functional`

### Phase 2 Verification: Step Definitions
- [ ] TransactionRecordSteps.cs compiles without errors
- [ ] All 9 changes applied correctly
- [ ] Renamed method has updated `[Then]` attribute with new text
- [ ] New step methods follow Gherkin-style comments (Given/When/Then/And)
- [ ] Build succeeds: `dotnet build tests/Functional`

### Phase 3 Verification: Feature File and Test Generation
- [ ] All 4 existing scenarios updated with Category in `.feature` file
- [ ] All 3 new scenarios added in correct locations in `.feature` file
- [ ] Gherkin syntax is valid (proper indentation, pipe-delimited tables)
- [ ] Manually regenerate `TransactionRecord.feature.cs` per INSTRUCTIONS.md
- [ ] Each new scenario mapped to step methods using step attributes
- [ ] Generated test file includes new scenarios with proper `[Test]` attributes
- [ ] Build succeeds: `dotnet build tests/Functional`

### Phase 4 Verification: Frontend Test IDs
- [ ] Create modal has `data-test-id="create-transaction-category"`
- [ ] Edit modal has `data-test-id="edit-transaction-category"`
- [ ] Table category column has `class="category-cell"`
- [ ] Details display has `data-test-id="transaction-category"`
- [ ] Details edit has `data-test-id="edit-category"`

### Phase 5 Verification: Test Execution
- [ ] All 10 TransactionRecord scenarios pass
- [ ] No new test failures introduced
- [ ] Test execution completes in reasonable time
- [ ] Console output shows all scenarios executed

---

## Section 6: Troubleshooting Guide

### Issue: Build errors in Page Objects

**Symptom**: Compilation errors when building `tests/Functional`

**Solutions**:
1. Verify locator syntax matches existing patterns
2. Check that `Page!` is used (not `Page`)
3. Ensure methods are placed inside the class but outside other method bodies
4. Verify XML documentation comments are properly formatted

---

### Issue: Step definition not recognized

**Symptom**: Feature file shows "No matching step definition found"

**Solutions**:
1. Verify `[When]`, `[Then]`, or `[Given]` attribute text exactly matches feature file text
2. Check for typos in attribute text vs. Gherkin step text
3. Rebuild solution to regenerate step binding cache
4. Verify method is `protected` and not `private`

---

### Issue: Test fails with "Element not found"

**Symptom**: Playwright timeout waiting for locator

**Solutions**:
1. Verify frontend has correct `data-test-id` attribute
2. Check CSS class name matches (e.g., `category-cell`)
3. Use browser developer tools to inspect actual HTML
4. Verify element is visible (not hidden by CSS or conditional rendering)
5. Check if page hydration is complete (wait for page ready)

---

### Issue: Category value not displayed correctly

**Symptom**: Assertion fails comparing expected vs. actual category

**Solutions**:
1. Verify backend sanitization is applied (CategoryHelper)
2. Check if transaction was seeded with correct category value
3. Verify object store contains correct KEY_TRANSACTION_CATEGORY value
4. Add `.Trim()` to text content extraction if whitespace is issue
5. Check if category cell uses correct CSS class for querying

---

## Section 7: Testing Strategy Notes

### Why These Tests?

**Coverage Philosophy**: These 4 tests (2 updates + 3 new) provide:
1. **UI Contract Verification**: Category field appears in all expected locations (create modal, edit modal, list, details)
2. **Workflow Validation**: Category data flows correctly through create and edit workflows
3. **Display Verification**: Category values display correctly in list and details views

**What We're NOT Testing** (covered by lower layers):
- Category sanitization rules (18 unit tests)
- Authorization checks (controller integration tests)
- Validation edge cases (controller integration tests)
- API contract details (controller integration tests)

### Maintenance Expectations

**Low Maintenance Burden**:
- New tests follow existing patterns (quick edit, details page)
- Reuse existing step definitions where possible
- Page Object pattern isolates UI changes
- No complex test data setup required

**Update Triggers**:
- Frontend UI changes to Category field placement
- Changes to data-test-id attribute names
- Changes to transaction creation/editing workflows

---

## Section 8: Implementation Summary

### Files Modified: 5

1. **tests/Functional/Pages/TransactionsPage.cs**
   - 2 new locators
   - 3 new methods
   - ~40 lines added

2. **tests/Functional/Pages/TransactionDetailsPage.cs**
   - 2 new locators
   - 2 new methods
   - ~30 lines added

3. **tests/Functional/Steps/TransactionRecordSteps.cs**
   - 1 new constant
   - 2 new step methods
   - 7 existing methods updated
   - ~80 lines added/modified

4. **tests/Functional/Features/TransactionRecord.feature** (Gherkin)
   - 4 existing scenarios updated
   - 3 new scenarios added
   - ~50 lines added/modified

5. **tests/Functional/Tests/TransactionRecord.feature.cs** (Generated C# test file)
   - **Manual regeneration required** per INSTRUCTIONS.md
   - 3 new `[Test]` methods generated from new scenarios
   - DataTable creation code for table-based steps
   - Step method calls mapped via attributes

### Total Changes
- **Locators Added**: 4 (2 in TransactionsPage, 2 in TransactionDetailsPage)
- **Methods Added**: 7 (5 in Page Objects, 2 in Step Definitions)
- **Methods Updated**: 7 (all in TransactionRecordSteps)
- **Scenarios Updated**: 4
- **Scenarios Added**: 3
- **Total New Test Scenarios**: 10 (7 existing + 3 new)

### Estimated Implementation Time
- Phase 1 (Page Objects): 30 minutes
- Phase 2 (Step Definitions): 45 minutes
- Phase 3 (Feature File): 30 minutes
- Phase 4 (Verification): 15 minutes
- Phase 5 (Test Execution): 10 minutes
- **Total**: ~2.5 hours

---

## Section 9: Success Criteria

Implementation is complete when:

✅ All 3 files compile without errors
✅ Feature file generates test code correctly
✅ All 10 TransactionRecord scenarios pass
✅ No existing tests broken by changes
✅ Console output shows all expected scenarios executed
✅ Test execution time is comparable to baseline (~2-3 minutes)

---

## Document History

| Date | Author | Change |
|------|--------|--------|
| 2025-12-27 | Architect Mode | Initial implementation plan with exact line numbers and changes |
