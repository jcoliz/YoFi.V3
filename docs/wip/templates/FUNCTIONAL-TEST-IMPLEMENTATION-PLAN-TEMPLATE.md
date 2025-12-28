# Functional Test Implementation Plan Template

**Purpose:** Guide Architect mode in creating detailed implementation plans that bridge from Gherkin scenarios to C# test code.

**When to Use:** Step 10.6 of Implementation Workflow

---

## Instructions for Architect Mode

### 1. Review Project Documentation FIRST

**CRITICAL:** Read these documents completely before starting:

1. **[`tests/Functional/INSTRUCTIONS.md`](../../../tests/Functional/INSTRUCTIONS.md)**
   - Test generation patterns (custom, NOT SpecFlow)
   - Step definition matching via XML comments
   - Feature file → C# test generation process
   - Manual template-based generation

2. **[`docs/TESTING-STRATEGY.md`](../../TESTING-STRATEGY.md)**
   - When to use functional tests
   - Scenario design principles
   - Test distribution guidance

3. **[`tests/Functional/.roorules`](../../../tests/Functional/.roorules)** (if exists)
   - Project-specific functional test patterns
   - Conventions and best practices

**Key Understanding:** This project does NOT use SpecFlow. Tests are generated manually using custom templates.

### 2. Review Functional Test Plan

Locate the functional test plan created in Step 10.5.

**Verify:**
- [ ] Plan exists in `docs/wip/{feature-area}/`
- [ ] YAML frontmatter shows `status: Approved`
- [ ] All scenarios have Gherkin blocks

**If not approved:** Work with user to get approval before proceeding.

### 3. Analyze Implementation Requirements

For EACH scenario in the test plan, analyze these four areas:

#### A. Page Object Models (POMs)

**Questions to Answer:**
- What UI elements need interaction?
- What selectors are needed? (`data-test-id`, CSS, etc.)
- Do existing POMs cover these? (Check `tests/Functional/Pages/`)
- What new locators need to be added?
- What new methods need to be created?

**Example Analysis:**
```markdown
**Scenario:** Quick edit modal shows Payee and Memo fields

**POM Analysis:**
- Page: TransactionsPage
- Needed locators:
  - `QuickEditButton` - existing
  - `QuickEditModal` - existing
  - `QuickEditPayeeInput` - NEW (add to TransactionsPage)
  - `QuickEditMemoInput` - NEW (add to TransactionsPage)
- Needed methods:
  - `ClickQuickEditAsync()` - existing
  - `IsQuickEditFieldVisibleAsync(fieldName)` - NEW helper method
```

#### B. Step Definitions

**Questions to Answer:**
- What Gherkin steps are in this scenario?
- Do existing step definitions match? (Check `tests/Functional/Steps/`)
- What new step methods need to be created?
- What parameters do they need?
- Can steps be reused across scenarios?

**Example Analysis:**
```markdown
**Scenario:** User creates transaction with amount $50.00

**Step Analysis:**
- `Given user is logged in` - EXISTING (CommonGivenSteps)
- `When user creates transaction with amount $50.00` - NEW
  - Method: `WhenICreateTransactionWithAmount(decimal amount)`
  - Location: TransactionRecordSteps.cs
  - Logic: Click Create button, fill form, save
- `Then transaction should appear in list` - NEW
  - Method: `ThenTransactionShouldAppearInList()`
  - Location: TransactionRecordSteps.cs
  - Logic: Verify transaction visible in table
```

#### C. Test Control Endpoints

**Questions to Answer:**
- What test data needs to be seeded?
- What state needs to be reset between tests?
- Do existing test control endpoints support this?
- What new endpoints need to be added to TestControlController?

**Example Analysis:**
```markdown
**Scenario:** User edits existing transaction

**Test Control Analysis:**
- Need: Seed transaction with specific data
- Existing: `POST /api/test/seed-transaction` - CAN USE
- Parameters needed: Payee, Amount, Date, Memo
- No new endpoints needed
```

#### D. Test Data

**Questions to Answer:**
- What specific data values are needed?
- Can data be generated (random) or must be specific?
- Are there data dependencies between scenarios?
- Do we need helper methods to generate test data?

**Example Analysis:**
```markdown
**Scenario:** Quick edit changes Memo from empty to "Morning coffee"

**Test Data:**
- Initial: Payee="Starbucks", Amount=-5.00, Memo=(empty)
- Change: Memo="Morning coffee"
- Verification: Memo displays "Morning coffee" in list
```

### 4. Determine Implementation Order

**Principles:**
1. **Simplest first:** Start with scenarios requiring fewest new components
2. **Dependency order:** Scenarios that seed data for others go first
3. **Infrastructure first:** If new POMs/test control needed, those scenarios first
4. **Build incrementally:** Each scenario should work independently

**Example:**
```markdown
## Implementation Order

1. **Scenario 1:** User creates transaction
   - Reason: Simplest, establishes basic POM patterns
   - New: TransactionCreateModal POM, basic step definitions

2. **Scenario 2:** User edits transaction via quick edit
   - Reason: Reuses creation logic, adds quick edit patterns
   - New: Quick edit locators, edit step definitions
   - Depends on: Test control endpoint for seeding transaction

3. **Scenario 3:** User views transaction details
   - Reason: Reuses test data from previous scenarios
   - New: TransactionDetailsPage POM
   - Depends on: Seeded transaction data
```

### 5. Create Implementation Plan Document

**Location:** Same directory as functional test plan (`docs/wip/{feature-area}/`)

**Filename:** `{FEATURE}-FUNCTIONAL-TEST-IMPLEMENTATION-PLAN.md`

**Structure:**

```markdown
---
status: Draft
references:
  - PRD-{FEATURE}.md
  - {FEATURE}-DESIGN.md
  - {FEATURE}-FUNCTIONAL-TEST-PLAN.md
---

# {Feature} Functional Test Implementation Plan

## Overview

[2-3 sentences summarizing feature and test scope]

**Reference Test Plan:** [{FEATURE}-FUNCTIONAL-TEST-PLAN.md](./{FEATURE}-FUNCTIONAL-TEST-PLAN.md)

**Total Scenarios:** [N] scenarios

**Estimated Complexity:** [Low/Medium/High]

## Implementation Requirements Summary

### Page Object Models

**Existing POMs to Modify:**
- `TransactionsPage.cs` - Add [list locators/methods]
- `TransactionDetailsPage.cs` - Add [list locators/methods]

**New POMs to Create:**
- `{ModalName}Page.cs` - [Purpose, key locators]

### Step Definitions

**Existing Steps to Use:**
- `tests/Functional/Steps/Common/CommonGivenSteps.cs` - [list methods]
- `tests/Functional/Steps/{Feature}Steps.cs` - [list methods]

**New Steps to Create:**
- `WhenIDoSomething()` - [Purpose, parameters, logic summary]
- `ThenIShouldSeeSomething()` - [Purpose, verification logic]

### Test Control Endpoints

**Existing Endpoints to Use:**
- `POST /api/test/seed-transaction` - [Parameters: ...]

**New Endpoints to Add:**
- `POST /api/test/{new-endpoint}` - [Purpose, parameters, response]

### Feature File

**Location:** `tests/Functional/Features/{Feature}.feature`

**Scenarios:** [N] scenarios from test plan

## Detailed Scenario Analysis

### Scenario 1: [Title from Test Plan]

**Gherkin (from test plan):**
```gherkin
Scenario: [Title]
  Given [step]
  When [step]
  Then [step]
```

**Page Object Models:**
- **File:** `tests/Functional/Pages/TransactionsPage.cs`
- **New Locators:**
  - `CreateButton` - `data-test-id="create-transaction-button"`
  - `PayeeInput` - `data-test-id="payee-input"`
- **New Methods:**
  - `ClickCreateButtonAsync()` - Clicks create button, waits for modal
  - `FillPayeeAsync(string payee)` - Fills payee input field

**Step Definitions:**
- **File:** `tests/Functional/Steps/TransactionRecordSteps.cs`
- **New Methods:**
  - `WhenICreateTransactionWithPayee(string payee)` - Uses TransactionsPage.ClickCreateButtonAsync(), fills form, clicks save
  - `ThenTransactionShouldAppearWithPayee(string payee)` - Verifies transaction row visible with payee text

**Test Control Endpoints:**
- None needed (creates data via UI)

**Test Data:**
- Payee: "Test Merchant"
- Amount: -50.00
- Date: Today

**Dependencies:**
- None (first scenario)

**Status:** ⏳ Not Started

---

### Scenario 2: [Title from Test Plan]

[Repeat structure above for each scenario]

---

## Implementation Order

**Recommended sequence:**

1. **Scenario 1:** [Title]
   - Reason: [Why first]
   - Complexity: [Low/Medium/High]
   - Estimated time: [Quick/Moderate/Lengthy]

2. **Scenario 2:** [Title]
   - Reason: [Why second]
   - Depends on: [Scenario 1 components]

[Continue for all scenarios]

## Risk Assessment

**Technical Risks:**
- [Risk 1]: [Description] - Mitigation: [Approach]
- [Risk 2]: [Description] - Mitigation: [Approach]

**Testing Risks:**
- [Risk 1]: [Description] - Mitigation: [Approach]

**Dependency Risks:**
- [Risk 1]: [Description] - Mitigation: [Approach]

## Pre-Implementation Checklist

Before starting Step 11 (Functional Tests implementation):

- [ ] All scenarios analyzed above
- [ ] POMs identified (existing + new)
- [ ] Step definitions identified (existing + new)
- [ ] Test control endpoints identified (existing + new)
- [ ] Implementation order decided
- [ ] Risks assessed with mitigations
- [ ] User reviewed and approved this plan
- [ ] YAML status changed to `Approved`
```

### 6. Update PRD YAML Frontmatter

Add link to implementation plan:

```yaml
functional_test_implementation_plan: {FEATURE}-FUNCTIONAL-TEST-IMPLEMENTATION-PLAN.md
```

### 7. Review with User

**Present Plan:**
1. Summarize total scenarios and estimated complexity
2. Highlight any technical risks or challenges
3. Confirm implementation order makes sense
4. Request approval to proceed

**Discussion Points:**
- Are new test control endpoints acceptable?
- Is complexity reasonable for the value?
- Should any scenarios be descoped?

### 8. Mark Approved

After user approval, update YAML:

```yaml
status: Approved
```

---

## Complete Example: Transaction Record Implementation Plan

```markdown
---
status: Approved
references:
  - PRD-TRANSACTION-RECORD.md
  - TRANSACTION-RECORD-DESIGN.md
  - TRANSACTION-RECORD-FUNCTIONAL-TEST-PLAN.md
---

# Transaction Record Functional Test Implementation Plan

## Overview

Implements 3 functional test scenarios for Transaction Record feature covering quick edit modal field visibility and details page edit workflow.

**Reference Test Plan:** [TRANSACTION-RECORD-FUNCTIONAL-TEST-PLAN.md](./TRANSACTION-RECORD-FUNCTIONAL-TEST-PLAN.md)

**Total Scenarios:** 3 scenarios

**Estimated Complexity:** Medium (new POMs needed, moderate test control work)

## Implementation Requirements Summary

### Page Object Models

**Existing POMs to Modify:**
- `TransactionsPage.cs` - Add quick edit locators (QuickEditButton, QuickEditPayeeInput, QuickEditMemoInput)
- `TransactionDetailsPage.cs` - Add edit mode locators (EditButton, SaveButton, field inputs)

**New POMs:** None (existing pages cover all scenarios)

### Step Definitions

**Existing Steps to Use:**
- `CommonGivenSteps.GivenIAmLoggedIn()` - User login
- `CommonWhenSteps.WhenINavigateTo()` - Page navigation

**New Steps to Create:**
- `WhenIClickQuickEdit()` - Opens quick edit modal
- `WhenIChangeMemoTo(string memo)` - Fills memo field
- `ThenIShouldSeeMemoInList(string memo)` - Verifies memo visible
- `WhenIChangeAllFields()` - Fills all fields on details page
- `ThenAllFieldsShouldShowUpdatedValues()` - Verifies all fields updated

### Test Control Endpoints

**Existing Endpoints to Use:**
- `POST /api/test/seed-transaction` - Seeds transaction with specific data

**New Endpoints:** None needed

### Feature File

**Location:** `tests/Functional/Features/TransactionRecord.feature`

**Scenarios:** 3 scenarios from test plan

## Detailed Scenario Analysis

### Scenario 1: Quick edit modal shows Payee, Category, and Memo fields

**Gherkin:**
```gherkin
Scenario: Quick edit modal shows Payee, Category, and Memo fields
  Given user is viewing transactions page with existing transaction
  When user clicks "Quick Edit" button
  Then modal should display Payee input field
  And modal should display Category input field
  And modal should display Memo input field
  And modal should NOT display Amount field
  And modal should NOT display Date field
```

**Page Object Models:**
- **File:** `tests/Functional/Pages/TransactionsPage.cs`
- **New Locators:**
  - `QuickEditButton` - `data-test-id="quick-edit-button"`
  - `QuickEditModal` - `data-test-id="quick-edit-modal"`
  - `QuickEditPayeeInput` - `[data-test-id="quick-edit-modal"] [data-test-id="payee-input"]`
  - `QuickEditCategoryInput` - `[data-test-id="quick-edit-modal"] [data-test-id="category-input"]`
  - `QuickEditMemoInput` - `[data-test-id="quick-edit-modal"] [data-test-id="memo-input"]`
  - `QuickEditAmountInput` - `[data-test-id="quick-edit-modal"] [data-test-id="amount-input"]`
  - `QuickEditDateInput` - `[data-test-id="quick-edit-modal"] [data-test-id="date-input"]`
- **New Methods:**
  - `ClickQuickEditButtonAsync()` - Clicks first quick edit button in list
  - `IsQuickEditFieldVisibleAsync(string fieldName)` - Returns bool for field visibility

**Step Definitions:**
- **File:** `tests/Functional/Steps/TransactionRecordSteps.cs`
- **New Methods:**
  - `GivenIAmViewingTransactionsPageWithTransaction()` - Uses test control to seed transaction, navigates to page
  - `WhenIClickQuickEditButton()` - Calls TransactionsPage.ClickQuickEditButtonAsync()
  - `ThenModalShouldDisplayField(string fieldName)` - Asserts field visible
  - `ThenModalShouldNotDisplayField(string fieldName)` - Asserts field NOT visible

**Test Control Endpoints:**
- `POST /api/test/seed-transaction` with params: Payee, Amount, Date

**Test Data:**
- Payee: "Starbucks"
- Amount: -5.00
- Date: 2024-01-15
- Memo: empty

**Dependencies:** None

**Status:** ⏳ Not Started

---

### Scenario 2: User edits Memo via quick edit and sees it in transaction list

**Gherkin:**
```gherkin
Scenario: User edits Memo via quick edit and sees it in transaction list
  Given user is viewing transactions page with transaction:
    | Field | Value     |
    | Payee | Starbucks |
    | Memo  | (none)    |
  When user clicks "Quick Edit" button
  And user changes Memo field to "Morning coffee"
  And user clicks "Save"
  Then transaction should show "Morning coffee" in Memo column
```

**Page Object Models:**
- **File:** `tests/Functional/Pages/TransactionsPage.cs`
- **Reuse:** Locators from Scenario 1
- **New Methods:**
  - `FillQuickEditMemoAsync(string memo)` - Fills memo input in quick edit modal
  - `GetTransactionMemoAsync()` - Returns memo text from first transaction row

**Step Definitions:**
- **File:** `tests/Functional/Steps/TransactionRecordSteps.cs`
- **Reuse:** `GivenIAmViewingTransactionsPageWithTransaction()`, `WhenIClickQuickEditButton()`
- **New Methods:**
  - `WhenIChangeMemoFieldTo(string memo)` - Fills memo field
  - `WhenIClickSave()` - Clicks save button in modal
  - `ThenTransactionShouldShowMemoInColumn(string memo)` - Verifies memo in list

**Test Control Endpoints:**
- `POST /api/test/seed-transaction` - Same as Scenario 1

**Test Data:**
- Initial: Payee="Starbucks", Memo=empty
- Change to: Memo="Morning coffee"

**Dependencies:** Scenario 1 (POMs, basic steps)

**Status:** ⏳ Not Started

---

### Scenario 3: User edits all fields on transaction details page

**Gherkin:**
```gherkin
Scenario: User edits all fields on transaction details page
  Given user is viewing details page for transaction
  When user clicks "Edit" button
  And user changes all fields
  And user clicks "Save"
  Then all fields should show updated values
```

**Page Object Models:**
- **File:** `tests/Functional/Pages/TransactionDetailsPage.cs`
- **New Locators:**
  - `EditButton` - `data-test-id="edit-button"`
  - `SaveButton` - `data-test-id="save-button"`
  - `PayeeInput` - `data-test-id="payee-input"` (edit mode)
  - `AmountInput` - `data-test-id="amount-input"` (edit mode)
  - `DateInput` - `data-test-id="date-input"` (edit mode)
  - `CategoryInput` - `data-test-id="category-input"` (edit mode)
  - `MemoInput` - `data-test-id="memo-input"` (edit mode)
- **New Methods:**
  - `ClickEditButtonAsync()` - Enters edit mode
  - `FillAllFieldsAsync(TransactionData data)` - Fills all input fields
  - `ClickSaveButtonAsync()` - Saves changes
  - `GetAllFieldValuesAsync()` - Returns all displayed field values

**Step Definitions:**
- **File:** `tests/Functional/Steps/TransactionRecordSteps.cs`
- **New Methods:**
  - `GivenIAmViewingDetailsPageForTransaction()` - Seeds transaction, navigates to details page
  - `WhenIClickEditButton()` - Enters edit mode
  - `WhenIChangeAllFields()` - Fills all fields with new values
  - `WhenIClickSaveButton()` - Saves changes
  - `ThenAllFieldsShouldShowUpdatedValues()` - Verifies all fields updated

**Test Control Endpoints:**
- `POST /api/test/seed-transaction` - Same as previous scenarios

**Test Data:**
- Initial: Payee="Starbucks", Amount=-5.00, Date=2024-01-15, Memo=empty
- Updated: Payee="Coffee Shop", Amount=-6.50, Date=2024-01-16, Memo="Latte"

**Dependencies:** Scenarios 1 & 2 (test control patterns)

**Status:** ⏳ Not Started

---

## Implementation Order

1. **Scenario 1:** Quick edit modal field visibility
   - Reason: Establishes quick edit POM patterns, simplest verification
   - Complexity: Low
   - Estimated: Quick (1-2 iterations)

2. **Scenario 2:** Edit Memo via quick edit
   - Reason: Reuses Scenario 1 infrastructure, adds save workflow
   - Complexity: Medium
   - Estimated: Moderate (2-3 iterations)
   - Depends on: Scenario 1 POMs and step definitions

3. **Scenario 3:** Edit all fields on details page
   - Reason: Most complex, new details page POM
   - Complexity: Medium-High
   - Estimated: Moderate (3-4 iterations)
   - Depends on: Test control endpoint patterns from 1 & 2

## Risk Assessment

**Technical Risks:**
- **Risk:** Quick edit modal selector complexity (nested within modal)
  - Mitigation: Use compound selectors: `[data-test-id="modal"] [data-test-id="field"]`
- **Risk:** Timing issues with modal open/close animations
  - Mitigation: Wait for modal visible before interacting with fields

**Testing Risks:**
- **Risk:** Scenario 3 has many fields to verify (potential brittleness)
  - Mitigation: Use helper method to verify all fields at once, clear failure messages

**Dependency Risks:**
- **Risk:** Test control endpoint must seed transaction and return ID for details page navigation
  - Mitigation: Verify test control returns transaction key in response

## Pre-Implementation Checklist

- [x] All scenarios analyzed above
- [x] POMs identified (modify TransactionsPage + TransactionDetailsPage)
- [x] Step definitions identified (5 new methods in TransactionRecordSteps)
- [x] Test control endpoints identified (reuse existing seed-transaction)
- [x] Implementation order decided (1 → 2 → 3)
- [x] Risks assessed with mitigations
- [ ] User reviewed and approved this plan
- [ ] YAML status changed to `Approved`
```

---

## Success Criteria

✅ All scenarios from test plan analyzed in detail
✅ POM requirements clear (files, locators, methods)
✅ Step definition requirements clear (methods, parameters, logic)
✅ Test control endpoint needs identified
✅ Test data specified for each scenario
✅ Implementation order determined with rationale
✅ Risks identified with mitigations
✅ Document location correct (same directory as PRD)
✅ PRD YAML updated with implementation plan link
✅ User approved (YAML `status: Approved`)

---

## Common Mistakes to Avoid

**❌ Insufficient analysis:**
- "We'll figure out the POMs as we go" → NO, analyze upfront

**❌ Missing dependencies:**
- Not noting which scenarios depend on previous infrastructure

**❌ Vague POM descriptions:**
- "Add some locators" → Be specific: field names, selectors, methods

**❌ Ignoring test control:**
- "We'll seed data manually" → Plan test control endpoints upfront

**❌ No implementation order:**
- "Implement in any order" → Sequence matters for building incrementally

**❌ Skipping risk assessment:**
- "Nothing can go wrong" → Always identify technical/testing risks

---

## Next Step

After this plan is approved (`status: Approved` in YAML):
- Proceed to **Step 11 (Functional Tests)** in Implementation Workflow
- Implement scenarios ONE AT A TIME per implementation order
- Update status in this document as each scenario completes
