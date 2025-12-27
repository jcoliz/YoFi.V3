---
status: Approved
feature: Transaction Splits - Alpha-1 Story 3 (Category Field)
scope: Functional test analysis for single-category workflow
target_release: Alpha-1
---

# Functional Tests Plan: Transaction Splits (Alpha-1 - Category Field)

## IMPORTANT: Analysis Revision Required

**Initial analysis was based on incorrect assumptions about existing test coverage. Actual code review reveals:**

1. Existing TransactionRecord tests do NOT include Category field (confirmed by reading test data tables)
2. Quick edit modal WAS updated to include Category (Payee, Category, Memo) - this is a CHANGE from Transaction Record PRD
3. Category column IS displayed in transactions list (line 524 in index.vue)
4. Create modal DOES include Category field (lines 688-711 in index.vue)

**Revision needed to accurately assess which scenarios require new functional tests.**

## Context

**Feature Scope**: Alpha-1 implementation of Story 3 "Simple Single-Category Workflow" from [`PRD-TRANSACTION-SPLITS.md`](PRD-TRANSACTION-SPLITS.md)

**What was implemented**:
- Added `Category` field to Transaction entity (single category, not splits yet)
- Category field is optional (nullable string)
- Auto-creates single split on transaction creation (backend implementation detail)
- Category sanitization rules applied (whitespace, capitalization)

**Test Coverage Status**:
- ✅ **Controller Integration Tests**: 23 tests covering Category CRUD, validation, sanitization, authorization
- ✅ **Unit Tests**: 18 tests covering CategoryHelper sanitization logic
- ❓ **Functional Tests**: Need to determine if new tests required

**Analysis Date**: 2025-12-27

---

## Testing Strategy Analysis

### Functional Test Philosophy (from TESTING-STRATEGY.md)

**Target**: 10-15% of total coverage
**Purpose**: Verify UI interactions, end-to-end workflows, user-facing behavior
**NOT for**: Edge cases, validation rules, authorization (already covered by lower layers)

**Current Test Distribution**:
- Controller Integration: 23 tests (56%)
- Unit Tests: 18 tests (44%)
- Functional Tests: 0 tests (0%)
- **Total**: 41 tests

**15% functional target** = ~6-7 tests maximum

### Existing Functional Test Coverage

**TransactionRecord.feature** (7 scenarios):
1. Quick edit modal shows only Payee and Memo fields
2. User updates Memo via quick edit modal
3. User navigates from transaction list to details page
4. User edits all fields on transaction details page
5. User returns to list from transaction details page
6. User sees all fields in create transaction modal
7. User creates transaction with all fields populated
8. Created transaction displays all fields on details page

**Key Insight**: Existing tests already cover the transaction CRUD workflows that Category participates in.

---

## Analysis: Potential Functional Test Scenarios

### Scenario 1: Category Field Visible in Create Transaction Modal

**Proposed Test**:
```gherkin
Scenario: Create modal includes Category field
  Given I am on the transactions page
  When I click the "Add Transaction" button
  Then I should see a create transaction modal
  And I should see a "Category" field
  And Category field should be optional
```

**Risk Category**: UI contract - Field presence verification
**Language Tier**: Tier 2 (Implementation-Aware)

**Decision**: ✅ **ADD - Update existing test**

**Rationale**:
- **NOT currently covered**: Existing test "User sees all fields in create transaction modal" (lines 188-210) explicitly lists: Date, Payee, Amount, Memo, Source, External ID - **Category is NOT in the list**
- **New field introduced**: Alpha-1 Story 3 adds Category field to create modal (implemented in index.vue lines 688-711)
- **UI contract verification**: Need to verify Category field appears in create form
- **Update existing test**: Modify line 200-209 field list to include "Category"

---

### Scenario 2: Create Transaction WITH Category

**Proposed Test**:
```gherkin
Scenario: User creates transaction with category
  Given I am on the transactions page
  When I click the "Add Transaction" button
  And I fill in Date, Payee, Amount, and Category "Food"
  And I click "Create"
  Then transaction should appear in list
  And transaction should show category "Food"
```

**Risk Category**: UI workflow - End-to-end data flow verification
**Language Tier**: Tier 2 (Implementation-Aware)

**Decision**: ✅ **ADD - New scenario needed**

**Rationale**:
- **NOT currently covered**: Existing scenario "User creates transaction with all fields populated" (lines 216-244) fills in Date, Payee, Amount, Memo, Source, External ID - **Category is NOT included**
- **New data flow**: Category field was added in Alpha-1, need to verify it flows from create modal → API → database → list display
- **Critical user journey**: Creating transactions with categories is the PRIMARY use case for Story 3
- **Controller Integration coverage exists**: But functional test verifies complete UI→API→UI workflow

---

### Scenario 3: Category Column Visible in Transaction List

**Proposed Test**:
```gherkin
Scenario: Transaction list displays category column
  Given I have a workspace with transactions
  And some transactions have categories assigned
  When I view the transactions page
  Then I should see a Category column in the table header
  And transactions with categories should display their category value
  And transactions without categories should show empty cell
```

**Risk Category**: UI contract - Visual display verification
**Language Tier**: Tier 2 (Implementation-Aware)

**Decision**: ✅ **ADD - New scenario needed**

**Rationale**:
- **New column added**: Category column was added to transactions table (index.vue line 524)
- **Visual verification needed**: Need to confirm column header appears and values display correctly
- **Critical for user workflow**: Users need to SEE categories in list view to make the feature useful
- **Simple test**: Low maintenance burden, high value for verifying basic UI contract

---

### Scenario 4: Quick Edit Modal Includes Category Field

**Proposed Test**:
```gherkin
Scenario: Quick edit modal includes Payee, Category, and Memo fields
  Given I have a workspace with a transaction
  When I click the "Edit" button on the transaction
  Then I should see a modal titled "Quick Edit Transaction"
  And I should see fields for "Payee", "Category", and "Memo"
  And I should not see fields for "Date", "Amount", "Source", or "ExternalId"
```

**Risk Category**: UI contract - Modal field presence verification
**Language Tier**: Tier 2 (Implementation-Aware)

**Decision**: ✅ **ADD - Update existing test**

**Rationale**:
- **CHANGED from Transaction Record**: TransactionRecord PRD had quick edit with ONLY Payee and Memo. Transaction Splits Story 3 ADDS Category to quick edit (lines 764-820 in index.vue, lines 294-298 use TransactionQuickEditDto with category)
- **Existing test is now incorrect**: Test "Quick edit modal shows only Payee and Memo fields" (lines 32-57) needs update to include Category
- **PRD evolution**: Transaction Splits PRD line 119 lists "category edit on quick-edit" as "under consideration" but it was IMPLEMENTED in Alpha-1
- **Critical UX change**: Adding Category to quick edit is a significant workflow improvement

---

### Scenario 5: Edit Category via Quick Edit Dialog

**Proposed Test**:
```gherkin
Scenario: User edits category via quick edit
  Given I have a workspace with a transaction
  And transaction has category "Food"
  When I quick edit the transaction
  And I change Category to "Home"
  And I click "Update"
  Then modal should close
  And transaction should show category "Home" in list
```

**Risk Category**: UI workflow - End-to-end edit data flow
**Language Tier**: Tier 2 (Implementation-Aware)

**Decision**: ✅ **ADD - New scenario needed**

**Rationale**:
- **New workflow**: Category editing via quick edit is NEW functionality in Alpha-1 Story 3
- **Critical user journey**: Quick edit is the primary way users update transactions (faster than navigating to details page)
- **Verify complete data flow**: Category change → API → database → list display
- **Page Object Model ready**: `TransactionsPage.EditCategoryInput` locator exists (data-test-id="edit-transaction-category" at line 811)

---

### Scenario 6: Details Page Displays Category Field

**Proposed Test**:
```gherkin
Scenario: Transaction details page displays category
  Given I have a workspace with a transaction
  And transaction has category "Food"
  When I navigate to transaction details page
  Then I should see "Food" displayed as the Category
  And Category should be visible in view mode
```

**Risk Category**: UI contract - Details page display verification
**Language Tier**: Tier 2 (Implementation-Aware)

**Decision**: ✅ **ADD - New scenario needed**

**Rationale**:
- **NOT currently covered**: Existing scenario "User navigates from transaction list to details page" (lines 99-123) verifies navigation and "all expected fields displayed" but does NOT specifically verify Category (test was written before Category existed)
- **Need explicit verification**: "All expected fields" step needs update OR new scenario to explicitly verify Category displays
- **Critical for completeness**: Users need to see Category on details page

### Scenario 7: Edit Category on Details Page

**Proposed Test**:
```gherkin
Scenario: User edits category on transaction details page
  Given I am viewing transaction details page for a transaction with category "Food"
  When I click "Edit" button
  And I change Category to "Home"
  And I click "Save"
  Then I should see "Home" as the Category
  And page should return to view mode
```

**Risk Category**: UI workflow - Details page edit data flow
**Language Tier**: Tier 2 (Implementation-Aware)

**Decision**: ✅ **ADD - New scenario needed**

**Rationale**:
- **NOT currently covered**: Existing scenario "User edits all fields on transaction details page" (lines 129-159) edits Source and ExternalId but **NOT Category** (Category didn't exist when test was written)
- **Complete edit coverage needed**: Need to verify Category can be edited on details page
- **Different from quick edit**: Details page allows editing ALL fields (not just Payee, Category, Memo)

---

### Scenario 8: Category Sanitization Visible to User

**Proposed Test**:
```gherkin
Scenario: User sees sanitized category after save
  Given I am on the transactions page
  When I create transaction with category " food and garden "
  And I save the transaction
  Then transaction should show category "Food And Garden"
```

**Risk Category**: Business logic - Sanitization visibility
**Language Tier**: Tier 2 (Implementation-Aware)

**Decision**: ❌ **SKIP - Unit tests sufficient**

**Rationale**:
- **Unit test coverage**: 18 CategoryHelper unit tests comprehensively cover sanitization rules
- **Controller Integration coverage**: Tests verify sanitization is applied end-to-end
- **Value vs. Cost**: Low value - functional test would just duplicate unit test coverage
- **Not a critical workflow**: Sanitization is transparent data processing

---

## Summary: Final Consolidated Scenarios (Maximum Efficiency)

**Strategy**: Update existing test to include Category instead of creating duplicates.

| # | Scenario | What it Covers | Action |
|---|----------|----------------|------|
| 1 | **Update**: User creates transaction with all fields (add Category) | Create modal + List display + Column visibility | UPDATE EXISTING |
| 2 | User edits category via quick edit and sees it in list | Quick edit modal + Edit workflow + List update | NEW |
| 3 | Transaction details page displays category | Details page view mode | NEW |
| 4 | User edits category on details page | Details page edit mode | NEW |

**Scenarios Eliminated by Smart Updates**:
- ❌ "Create modal includes Category field" → Covered by updating existing "create with all fields" test
- ❌ "Category column visible in list" → Covered by existing + updated tests (list is verified after create/edit)
- ❌ "Quick edit includes Category field" → Covered by updating existing quick edit test + new quick edit workflow test

**Skipped Scenarios**:
- ❌ "Category sanitization visible" → Unit tests sufficient for algorithm verification

---

## Document History

| Date | Author | Change |
|------|--------|--------|
| 2025-12-27 | Architect Mode | Initial functional test analysis for Alpha-1 Story 3 |
