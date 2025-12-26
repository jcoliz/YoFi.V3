---
status: Approved
feature: Transaction Record Fields (Memo, Source, ExternalId)
prd: PRD-TRANSACTION-RECORD.md
implementation_plan: TRANSACTION-RECORD-FUNCTIONAL-TEST-IMPLEMENTATION-PLAN.md
approved_scenarios: 8 scenarios (Priority 1 and 2)
deferred_scenarios: Priority 3 (Memo truncation)
estimated_coverage: 10-15% of acceptance criteria
---

# Functional Test Plan: Transaction Record Fields

## Testing Philosophy

**Coverage Target**: 10-15% of total tests (2-3 scenarios maximum)

**Focus**: UI-dependent workflows only - features that require actual browser interaction to verify correct behavior.

**Avoid**: Testing API behavior already covered by Controller Integration tests (validation, persistence, authorization).

## Already Covered by Controller Integration Tests

The following acceptance criteria are **already adequately tested** and do NOT need functional test coverage:

- ✅ **Story 1** (Imported data fields): All fields (Date, Amount, Payee, Source, ExternalId) tested via API contracts
- ✅ **Story 3** (Management): Edit/Delete operations tested via PUT/DELETE endpoints
- ✅ **Field validation**: Max lengths, required fields tested at API layer
- ✅ **Authorization**: Viewer/Editor/Owner role permissions tested
- ✅ **Tenant isolation**: Transaction data scoping tested
- ✅ **Data persistence**: Database operations tested in Integration.Data layer

**Why these don't need functional tests**: Controller Integration tests verify the complete request → database → response cycle. Adding UI tests would be redundant and increase maintenance burden without adding value.

## Critical UI Workflows Requiring Functional Tests

### Priority 1: Two-Tier Editing UX ✅ APPROVED FOR IMPLEMENTATION

**Justification**: This is a unique UX pattern (quick edit vs full edit) that requires verifying:
- Clicking row navigates to details page (cannot be tested at API level)
- Quick edit modal only updates Payee/Memo (UI workflow verification)
- Full edit page allows editing all fields (complete user journey)
- Navigation between list → details → list works correctly

**Risk if not implemented**: Users could experience broken navigation, modal behavior issues, or confusion between edit modes. This UX pattern is central to the user experience and cannot be verified through API tests alone.

**Scenarios**: Split into focused, single-purpose tests

```gherkin
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
```

### Priority 2: New Transaction with All Fields ✅ APPROVED FOR IMPLEMENTATION

**Justification**: Verifies the complete create workflow including:
- All new fields (Memo, Source, ExternalId) are visible and editable in create modal
- Form validation displays errors correctly in the UI
- Character counters work for long text fields
- Created transaction displays correctly in list view with Memo column

**Risk if not implemented**: Users might encounter issues with the create form that wouldn't be caught by API tests (e.g., form not populating, validation messages not displaying, character counters not working).

**Scenarios**: Split into focused, single-purpose tests

```gherkin
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

### Priority 3: Memo Truncation in List View ⏸️ DEFERRED (Future Consideration)

**Justification**: Verifies UI-specific behavior:
- Long memos truncate with ellipsis in table view
- Hover shows full memo text in tooltip
- Clicking row shows full memo on details page

**Risk if not implemented**: Moderate - table layout could break with long text, but this is primarily a CSS/display issue that's easily caught in manual testing. Controller Integration tests already verify memo field storage and retrieval.

**Decision**: **DEFERRED** - This is primarily a CSS concern that can be verified through manual testing or visual regression testing. The functional test would be fragile (dependent on exact CSS pixel widths) and high maintenance for low value. Retained in this document for future consideration if truncation issues are reported by users.

## Implementation Plan

**Approved for Implementation**:
- ✅ **Priority 1**: Two-Tier Editing UX - 5 scenarios
- ✅ **Priority 2**: New Transaction with All Fields - 3 scenarios

**Deferred (Future Consideration)**:
- ⏸️ **Priority 3**: Memo Truncation - manual testing sufficient for now

**Total Scenarios**: 8 focused scenarios
**Estimated Coverage**: ~10% of acceptance criteria (appropriate for functional tests)
**Test Execution Time**: ~40-60 seconds total (all scenarios)
**Maintenance Burden**: Low (stable UI patterns, minimal external dependencies)

**Gherkin Best Practices Applied**:
- ✅ Each scenario tests ONE specific behavior
- ✅ Clear Given/When/Then structure
- ✅ Minimal "And" clauses in Then statements
- ✅ Descriptive scenario names that explain what is being tested
- ✅ Scenarios are independent and can run in any order
- ✅ Easy to identify which behavior failed when a test breaks

## Test Infrastructure Requirements

**Page Object Models Needed**:
- ✅ `TransactionsPage` (already exists - needs updates for new fields)
- ✅ `TransactionDetailsPage` (NEW - needs creation)

**Test Data Helpers**:
- Use existing `TestControlController` endpoints for seeding transactions with all fields
- Extend `TransactionSeedRequest` to include Memo, Source, ExternalId (if needed)

**Browser Requirements**:
- Chromium only (existing functional test setup)
- No special browser features required (standard form input/navigation)
