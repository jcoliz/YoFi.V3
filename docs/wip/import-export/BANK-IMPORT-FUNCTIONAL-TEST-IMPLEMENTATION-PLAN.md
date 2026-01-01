---
status: Approved (scenarios 1-7)
references:
  - PRD-BANK-IMPORT.md
  - DESIGN-BANK-IMPORT.md
  - DESIGN-BANK-IMPORT-TESTING.md
---

# Bank Import Functional Test Implementation Plan

## Overview

Implements 12 functional test scenarios for Bank Import feature covering upload workflow, review operations, selection management, duplicate detection, authorization, and error handling.

**Reference Test Plan:** [`DESIGN-BANK-IMPORT-TESTING.md`](./DESIGN-BANK-IMPORT-TESTING.md)

**Total Scenarios:** 12 scenarios (priority-ordered 1-12)

**Estimated Complexity:** High (new ImportPage POM, new upload patterns, new test control endpoints needed)

## Implementation Requirements Summary

### Page Object Models

**New POMs to Create:**
- `ImportPage.cs` - Import review page with upload, transaction list, selection controls, and actions
  - Key locators: File upload input, transaction table, selection checkboxes, accept/delete buttons
  - Key methods: Upload file, select/deselect transactions, get transaction counts, verify duplicate badges

**Existing POMs to Use:**
- `TransactionsPage.cs` - For navigation from transactions page and verification after import
- `BasePage.cs` - For common page patterns and navigation

### Step Definitions

**Existing Steps to Use:**
- `CommonGivenSteps.GivenIAmLoggedIn()` - User login (will need Editor role variant)
- `CommonWhenSteps.WhenUserLaunchesSite()` - Site launch
- `CommonThenSteps.ThenPageLoadedOk()` - Page load verification

**New Steps to Create:**
- `GivenIAmLoggedInAsUserWithEditorRole()` - Setup with Editor role in workspace
- `GivenIHaveExistingTransactionsFrom(string dateRange)` - Seed existing transactions via test control
- `GivenIAmOnTheImportReviewPage()` - Navigate to import review page
- `GivenPageDisplaysTransactions(int count)` - Verify transaction count
- `WhenIClickImportFromBankButton()` - Open import workflow
- `WhenIUploadOFXFile(string filename)` - Upload OFX file
- `WhenIClickAcceptSelectedTransactionsButton()` - Accept selected
- `WhenIClickDeleteAllButton()` - Delete all pending
- `WhenIClickSelectAllButton()` - Select all transactions
- `WhenIClickDeselectAllButton()` - Deselect all transactions
- `WhenIDeselectTransactions(int count)` - Deselect specific count
- `WhenISelectTransaction(int index)` - Select specific transaction
- `WhenINavigateToTransactionsPage()` - Navigate away
- `WhenILogOut()` - Log out
- `WhenILogBackIn()` - Log back in
- `WhenINavigateToImportReviewPage()` - Return to import review
- `WhenIAttemptToNavigateToImportReviewPage()` - Attempt navigation (for auth test)
- `ThenIShouldBeRedirectedToImportReviewPage()` - Verify redirect to review
- `ThenPageShouldDisplayTransactions(int count)` - Verify transaction count
- `ThenTransactionsShouldBeSelectedByDefault(int count)` - Verify selected count
- `ThenTransactionsShouldBeDeselectedByDefault(int count)` - Verify deselected count
- `ThenIShouldBeRedirectedToTransactionsPage()` - Verify redirect to transactions
- `ThenIShouldSeeNewTransactionsInTransactionList(int count)` - Verify added transactions
- `ThenImportReviewQueueShouldBeCompletelyCleared()` - Verify queue empty
- `ThenPageShouldDisplayTotalTransactions(int total)` - Verify total count
- `ThenTransactionShouldBeVisuallyMarkedAsPotentialDuplicate(int index)` - Verify duplicate badge
- `ThenTransactionsShouldBeSelectedAfterRefresh(int count)` - Verify persistence
- `ThenPageShouldShowNoPendingImports()` - Verify empty state
- `ThenTransactionsShouldBeSelected(int count)` - General selection verification
- `ThenIShouldSeeErrorMessage(string message)` - Verify error message
- `ThenNoTransactionsShouldBeAddedToReviewQueue()` - Verify no transactions added

### Test Control Endpoints

**Existing Endpoints to Use:**
- `POST /testcontrol/users` - Create test user with credentials
- `POST /testcontrol/users/{username}/workspaces/bulk` - Create workspace with Editor role
- `POST /testcontrol/users/{username}/workspaces/{tenantKey}/transactions/seed` - Seed existing transactions
- `DELETE /testcontrol/data` - Clean up test data

**New Endpoints to Add:**
- `POST /testcontrol/users/{username}/workspaces/{tenantKey}/import/seed` - Seed import review transactions
  - Purpose: Set up pending import state for testing persistence and return-later scenarios
  - Parameters: Count, PayeePrefix, SelectionState (array of bools)
  - Returns: Array of created ImportReviewTransaction DTOs

### Feature File

**Location:** `tests/Functional/Features/BankImport.feature`

**Scenarios:** 12 scenarios from test plan (priority-ordered)

### Test Data Files

**New OFX Files to Create in `tests/Functional/SampleData/Ofx/`:**
- `checking-jan-2024.ofx` - 15 transactions (12 new, 3 exact duplicates)
- `checking-jan-15-31.ofx` - 23 transactions (8 new, 15 duplicates with 1 potential duplicate)
- `invalid.ofx` - Corrupted OFX file (malformed XML)
- `transactions.csv` - CSV file (wrong format for validation error)

**Note:** Test data files should be created specifically for functional tests and placed in `tests/Functional/SampleData/Ofx/` directory.

## Detailed Scenario Analysis

### Scenario 1: User uploads bank file and sees import review page (Priority 1)

**Gherkin:**
```gherkin
Background:
  Given I have an existing account
  And I am logged in
  And I have an active workspace "My Finances"

Scenario: User uploads bank file and sees import review page
  Given I am on the import review page
  When I upload OFX file "checking-jan-2024.ofx"
  Then page should display 15 transactions
  And 12 transactions should be selected by default
  And 3 transactions should be deselected by default
```

**Note:** The Background section applies to all scenarios in the feature file and will be generated as a `[SetUp]` method in the C# test class.

**Page Object Models:**
- **File:** `tests/Functional/Pages/ImportPage.cs` (NEW)
- **New Locators:**
  - `UploadButton` - `data-test-id="upload-import-file-button"`
  - `FileInput` - `input[type="file"]` (for file upload)
  - `TransactionTable` - `data-test-id="import-review-table"`
  - `TransactionRows` - `data-test-id^="import-transaction-row-"`
  - `SelectionCheckbox(key)` - `data-test-id="import-transaction-checkbox-{key}"`
  - `AcceptSelectedButton` - `data-test-id="accept-selected-button"`
  - `DeleteAllButton` - `data-test-id="delete-all-button"`
  - `SelectAllButton` - `data-test-id="select-all-button"`
  - `DeselectAllButton` - `data-test-id="deselect-all-button"`
  - `TransactionCountDisplay` - `data-test-id="transaction-count"`
  - `SelectedCountDisplay` - `data-test-id="selected-count"`
- **New Methods:**
  - `NavigateAsync()` - Navigate to /import
  - `UploadFileAsync(string filePath)` - Upload OFX file
  - `GetTransactionCountAsync()` - Count total transactions
  - `GetSelectedCountAsync()` - Count selected transactions
  - `GetDeselectedCountAsync()` - Count deselected transactions
  - `IsTransactionSelectedAsync(int index)` - Check selection state
  - `SelectTransactionAsync(int index)` - Select transaction
  - `DeselectTransactionAsync(int index)` - Deselect transaction
  - `ClickAcceptSelectedAsync()` - Accept selected
  - `ClickDeleteAllAsync()` - Delete all
  - `ClickSelectAllAsync()` - Select all
  - `ClickDeselectAllAsync()` - Deselect all
  - `HasDuplicateBadgeAsync(int index)` - Check if transaction has duplicate badge
  - `WaitForUploadCompleteAsync()` - Wait for upload API to finish

**Step Definitions:**
- **File:** `tests/Functional/Steps/BankImportSteps.cs` (NEW)
- **Base Class:** Inherits from `TransactionRecordSteps` (to reuse workspace setup)
- **Background Methods (from Background section):**
  - `GivenIHaveAnExistingAccount()` - REUSE from CommonGivenSteps
  - `GivenIAmLoggedIn()` - REUSE from CommonGivenSteps
  - `GivenIHaveAnActiveWorkspace(string workspaceName)` - REUSE workspace setup from TransactionRecordSteps
- **New Methods:**
  - `GivenIAmOnTheImportReviewPage()` - Navigate to /import page and verify ready
  - `WhenIUploadOFXFile(string filename)` - Upload OFX file from test data directory
  - `ThenPageShouldDisplayTransactions(int count)` - Verify total transaction count
  - `ThenTransactionsShouldBeSelectedByDefault(int count)` - Verify selected count
  - `ThenTransactionsShouldBeDeselectedByDefault(int count)` - Verify deselected count

**Test Control Endpoints:**
- `POST /testcontrol/users` - Create Editor user
- `POST /testcontrol/users/{username}/workspaces/bulk` - Create workspace with Editor role
- **NEW ENDPOINT NEEDED:** `POST /testcontrol/users/{username}/workspaces/{tenantKey}/transactions/seed` with FITID parameter
  - Need to seed 3 transactions with specific FITIDs that match the OFX file to create exact duplicates

**Test Data:**
- **File:** `tests/Functional/SampleData/Ofx/checking-jan-2024.ofx`
- **Content:** 15 transactions total
  - 12 new transactions (FITIDs not in database)
  - 3 exact duplicate transactions (FITIDs match existing, all fields match)
- **Seeded Data:** 3 existing transactions via test control with matching FITIDs

**Dependencies:** None (first scenario)

**Status:** ⏳ Not Started

---

### Scenario 2: User accepts selected transactions from import review (Priority 2)

**Gherkin:**
```gherkin
Scenario: User accepts selected transactions from import review
  Given I am on the import review page
  And page displays 15 transactions
  And 12 transactions are selected by default
  When I click "Import" button
  And I confirm the import in the modal dialog
  Then I should be redirected to transactions page
  And I should see 12 new transactions in the transaction list
  And import review queue should be completely cleared
```

**Page Object Models:**
- **File:** `tests/Functional/Pages/ImportPage.cs` (REUSE from Scenario 1)
- **New Locators:**
  - Import Confirmation Modal: `data-test-id="import-confirm-modal"`
  - Import Confirm Button (in modal): `data-test-id="import-confirm-button"`
  - Import Cancel Button (in modal): `data-test-id="import-cancel-button"`
- **New Methods:**
  - `ClickImportButtonAsync()` - Click the Import button (opens confirmation modal)
  - `ConfirmImportAsync()` - Click Import button on confirmation modal
  - `CancelImportAsync()` - Click Cancel button on confirmation modal
  - `IsImportConfirmModalVisibleAsync()` - Check if confirmation modal is displayed
- **Existing Methods:**
  - `GetTransactionCountAsync()` - Verify queue is empty after

**Step Definitions:**
- **File:** `tests/Functional/Steps/BankImportSteps.cs` (REUSE)
- **Existing Steps:**
  - `GivenIAmOnTheImportReviewPage()` - Setup from Scenario 1
  - `GivenPageDisplaysTransactions(int count)` - Verify state
- **New Methods:**
  - `WhenIClickImportButton()` - Click the Import button (opens confirmation modal)
  - `WhenIConfirmTheImportInTheModalDialog()` - Click Import button on confirmation modal
  - `ThenIShouldBeRedirectedToTransactionsPage()` - Verify navigation
  - `ThenIShouldSeeNewTransactionsInTransactionList(int count)` - Count transactions on main page
  - `ThenImportReviewQueueShouldBeCompletelyCleared()` - Navigate back to import review, verify empty

**Test Control Endpoints:**
- **NEW ENDPOINT NEEDED:** `POST /testcontrol/users/{username}/workspaces/{tenantKey}/import/seed`
  - Seed pending import review transactions directly (faster than uploading file)
  - Parameters: Count, SelectionState array, FITIDs, PayeePrefix

**Test Data:**
- No file upload needed - seed import review state directly via test control

**Dependencies:** Scenario 1 (POMs, basic steps, test control patterns)

**Status:** ⏳ Not Started

---

### Scenario 3: Viewer cannot access import page (Priority 3)

**Gherkin:**
```gherkin
Scenario: Viewer cannot access import page
  Given I am logged in as a user with Viewer role for workspace
  When I attempt to navigate to import review page
  Then I should see error message "You do not have permission to import into this workspace"
  And import UI elements should be disabled or hidden
```

**Page Object Models:**
- **File:** `tests/Functional/Pages/ImportPage.cs` (REUSE)
- **New Methods:**
  - `IsUploadButtonVisibleAsync()` - Check if upload button is visible
  - `IsUploadButtonEnabledAsync()` - Check if upload button is enabled
  - `GetErrorMessageAsync()` - Get error message text

**Step Definitions:**
- **File:** `tests/Functional/Steps/BankImportSteps.cs` (REUSE)
- **New Methods:**
  - `GivenIAmLoggedInAsUserWithViewerRole()` - Create user, create workspace with Viewer role, then login (login LAST)
  - `WhenIAttemptToNavigateToImportReviewPage()` - Attempt navigation
  - `ThenIShouldSeeErrorMessage(string message)` - Verify error message
  - `ThenImportUIElementsShouldBeDisabledOrHidden()` - Verify UI state

**Test Control Endpoints:**
- `POST /testcontrol/users` - Create test user
- `POST /testcontrol/users/{username}/workspaces/bulk` - Create workspace with Role="Viewer"

**Test Data:** None needed

**Setup Order:** Create user → Create workspace with Viewer role → Login (login LAST)

**Dependencies:** Scenarios 1 & 2 (POMs, basic navigation)

**Status:** ⏳ Not Started

---

### Scenario 4: User uploads bank file with duplicates and sees marked potential duplicates (Priority 4)

**Gherkin:**
```gherkin
Scenario: User uploads bank file with duplicates and sees marked potential duplicates
  Given I have existing transactions from January 1-15
  And one existing transaction has been modified (payee changed from original import)
  When I navigate to import review page
  And I upload bank file with overlapping dates "checking-jan-15-31.ofx"
  Then page should display 23 total transactions
  And 8 transactions should be selected by default
  And 15 transactions should be deselected by default
  And 1 transaction should be visually marked as "Potential Duplicate" (the modified one)
```

**Page Object Models:**
- **File:** `tests/Functional/Pages/ImportPage.cs` (REUSE)
- **New Locators:**
  - `DuplicateBadge(index)` - `[data-test-id="import-transaction-row-{index}"] [data-test-id="duplicate-badge"]`
  - `PotentialDuplicateBadge(index)` - Badge with specific styling or text for potential duplicates
- **Existing Methods:**
  - `HasDuplicateBadgeAsync(int index)` - Check for duplicate badge
  - `GetDuplicateBadgeTypeAsync(int index)` - Get badge type (Exact/Potential)

**Step Definitions:**
- **File:** `tests/Functional/Steps/BankImportSteps.cs` (REUSE)
- **New Methods:**
  - `GivenIHaveExistingTransactionsFromDateRange(string dateRange)` - Seed transactions for date range
  - `GivenOneExistingTransactionHasBeenModified()` - Modify one transaction's payee
  - `ThenPageShouldDisplayTotalTransactions(int total)` - Verify total count
  - `ThenTransactionShouldBeVisuallyMarkedAsPotentialDuplicate(int index)` - Verify badge

**Test Control Endpoints:**
- `POST /testcontrol/users/{username}/workspaces/{tenantKey}/transactions/seed` - Seed 15 transactions
- **NEW ENDPOINT:** `PATCH /testcontrol/users/{username}/workspaces/{tenantKey}/transactions/{key}` - Modify payee of one transaction

**Test Data:**
- **File:** `tests/Functional/SampleData/Ofx/checking-jan-15-31.ofx`
- **Content:** 23 transactions (8 new, 14 exact duplicates, 1 potential duplicate)

**Dependencies:** Scenarios 1-3 (POMs, upload workflow)

**Status:** ⏳ Not Started

---

### Scenario 5: User accepts selected transactions and rejects duplicates (Priority 5)

**Gherkin:**
```gherkin
Scenario: User accepts selected transactions and rejects duplicates
  Given I am on the import review page
  And page displays 23 transactions
  And 8 transactions are selected by default
  And 15 transactions are deselected by default
  When I click "Import" button
  And I confirm the import in the modal dialog
  Then I should be redirected to transactions page
  And 8 transactions should be added to transaction list
  And import review queue should be completely cleared
```

**Page Object Models:**
- **File:** `tests/Functional/Pages/ImportPage.cs` (REUSE)
- All methods already exist from Scenarios 1-2 (including modal methods)

**Step Definitions:**
- **File:** `tests/Functional/Steps/BankImportSteps.cs` (REUSE)
- All steps already exist from Scenarios 1-2 (including modal confirmation steps)

**Test Control Endpoints:**
- `POST /testcontrol/users/{username}/workspaces/{tenantKey}/import/seed` - Seed review state

**Test Data:**
- Seed import review state with 23 transactions (8 selected, 15 deselected)

**Dependencies:** Scenarios 1-4 (complete workflow patterns)

**Status:** ⏳ Not Started

---

### Scenario 6: User toggles transaction selection in import review (Priority 6)

**Gherkin:**
```gherkin
Scenario: User toggles transaction selection in import review
  Given I am on the import review page
  And page shows 15 transactions
  And 12 transactions are selected by default
  When I deselect 2 selected transactions
  And I select 1 deselected transaction
  Then 11 transactions should be selected
  And selection state should persist when I navigate away and return
```

**Page Object Models:**
- **File:** `tests/Functional/Pages/ImportPage.cs` (REUSE)
- **Existing Methods:**
  - `SelectTransactionAsync(int index)` - Already defined
  - `DeselectTransactionAsync(int index)` - Already defined
  - `GetSelectedCountAsync()` - Already defined

**Step Definitions:**
- **File:** `tests/Functional/Steps/BankImportSteps.cs` (REUSE)
- **New Methods:**
  - `WhenIDeselectSelectedTransactions(int count)` - Deselect first N selected
  - `WhenISelectDeselectedTransaction()` - Select first deselected
  - `ThenTransactionsShouldBeSelected(int count)` - Verify count
  - `WhenINavigateAwayAndReturn()` - Navigate to transactions then back to import
  - `ThenSelectionStateShouldPersist(int expectedCount)` - Verify count after return

**Test Control Endpoints:**
- `POST /testcontrol/users/{username}/workspaces/{tenantKey}/import/seed` - Seed review state

**Test Data:**
- Seed 15 transactions (12 selected, 3 deselected)

**Dependencies:** Scenarios 1-5 (selection patterns, navigation)

**Status:** ⏳ Not Started

---

### Scenario 7: User selects all transactions in import review (Priority 7)

**Gherkin:**
```gherkin
Scenario: User selects all transactions in import review
  Given I am on the import review page
  And there are 100 total transactions across multiple pages
  And page shows first 50 transactions
  And 80 transactions are selected by default
  When I click "Select All" button
  Then all 50 visible transactions on page 1 should be selected
  And all 50 visible transactions on page 2 should also be selected
```

**Page Object Models:**
- **File:** `tests/Functional/Pages/ImportPage.cs` (REUSE)
- **Existing Methods:**
  - `ClickSelectAllAsync()` - Already defined
  - `GetSelectedCountAsync()` - Count selected checkboxes on current page
  - `GetTransactionCountAsync()` - Get total count on current page
- **New Methods:**
  - `NavigateToPageAsync(int pageNumber)` - Navigate to specific page in pagination

**Step Definitions:**
- **File:** `tests/Functional/Steps/BankImportSteps.cs` (REUSE)
- **New Methods:**
  - `GivenThereAreTotalTransactionsAcrossMultiplePages(int total)` - Verify total count from pagination metadata
  - `GivenPageShowsFirstTransactions(int count)` - Verify current page transaction count
  - `WhenIClickSelectAllButton()` - Click select all
  - `ThenAllVisibleTransactionsOnPageShouldBeSelected(int pageNumber, int count)` - Navigate to page and verify all visible checkboxes are checked (repeat for page 1 and page 2)

**Test Control Endpoints:**
- `POST /testcontrol/users/{username}/workspaces/{tenantKey}/import/seed` - Seed 100 transactions (80 selected, 20 deselected)

**Test Data:**
- Seed 100 transactions (80 selected, 20 deselected)
- Tests that Select All API endpoint selects ALL transactions, not just those on the current page

**Dependencies:** Scenario 6 (selection management patterns)

**Status:** ⏳ Not Started

---

### Scenario 8: User deselects all transactions in import review (Priority 8)

**Gherkin:**
```gherkin
Scenario: User deselects all transactions in import review
  Given I am on the import review page
  And page shows 15 transactions
  And 12 transactions are selected by default
  When I click "Deselect All" button
  Then 0 transactions should be selected
```

**Page Object Models:**
- **File:** `tests/Functional/Pages/ImportPage.cs` (REUSE)
- **Existing Methods:**
  - `ClickDeselectAllAsync()` - Already defined
  - `GetSelectedCountAsync()` - Already defined

**Step Definitions:**
- **File:** `tests/Functional/Steps/BankImportSteps.cs` (REUSE)
- **New Methods:**
  - `WhenIClickDeselectAllButton()` - Click deselect all
  - `ThenNoTransactionsShouldBeSelected()` - Verify zero selected

**Test Control Endpoints:**
- `POST /testcontrol/users/{username}/workspaces/{tenantKey}/import/seed` - Seed review state

**Test Data:**
- Seed 15 transactions (12 selected, 3 deselected)

**Dependencies:** Scenarios 6-7 (selection management patterns)

**Status:** ⏳ Not Started

---

### Scenario 9: User uploads corrupted file and sees error message (Priority 9)

**Gherkin:**
```gherkin
Scenario: User uploads corrupted file and sees error message
  Given I am on the import review page
  When I upload corrupted OFX file "invalid.ofx"
  Then I should see error message "File appears corrupted - unable to parse transaction data"
  And no transactions should be added to review queue
```

**Page Object Models:**
- **File:** `tests/Functional/Pages/ImportPage.cs` (REUSE)
- **Existing Methods:**
  - `UploadFileAsync(string filePath)` - Already defined
  - `GetErrorMessageAsync()` - Already defined
  - `GetTransactionCountAsync()` - Already defined

**Step Definitions:**
- **File:** `tests/Functional/Steps/BankImportSteps.cs` (REUSE)
- **New Methods:**
  - `WhenIUploadCorruptedOFXFile(string filename)` - Upload invalid file
  - `ThenNoTransactionsShouldBeAddedToReviewQueue()` - Verify count is zero

**Test Control Endpoints:**
- None needed (just file upload)

**Test Data:**
- **File:** `tests/Functional/SampleData/Ofx/invalid.ofx`
- **Content:** Malformed XML (missing closing tags, invalid structure)

**Dependencies:** Scenario 1 (upload patterns)

**Status:** ⏳ Not Started

---

### Scenario 10: User uploads unsupported file format (Priority 10)

**Gherkin:**
```gherkin
Scenario: User uploads unsupported file format
  Given I am on the import review page
  When I upload CSV file "transactions.csv"
  Then I should see error message "Unsupported file format - expected OFX or QFX"
  And no transactions should be added to review queue
```

**Page Object Models:**
- **File:** `tests/Functional/Pages/ImportPage.cs` (REUSE)
- All methods already exist from Scenario 9

**Step Definitions:**
- **File:** `tests/Functional/Steps/BankImportSteps.cs` (REUSE)
- **New Methods:**
  - `WhenIUploadCSVFile(string filename)` - Upload CSV file

**Test Control Endpoints:**
- None needed

**Test Data:**
- **File:** `tests/Functional/SampleData/Ofx/transactions.csv`
- **Content:** CSV format transaction data

**Dependencies:** Scenario 9 (error handling patterns)

**Status:** ⏳ Not Started

---

### Scenario 11: User returns to pending import review after session interruption (Priority 11)

**Gherkin:**
```gherkin
Scenario: User returns to pending import review after session interruption
  Given I am on the import review page
  And page shows 15 pending transactions
  And 10 transactions are selected
  When I navigate to transactions page
  And I log out
  And I log back in
  And I navigate to import review page
  Then page should still show 15 pending transactions
  And 10 transactions should still be selected
```

**Page Object Models:**
- **File:** `tests/Functional/Pages/ImportPage.cs` (REUSE)
- **Existing Methods:** All already exist

**Step Definitions:**
- **File:** `tests/Functional/Steps/BankImportSteps.cs` (REUSE)
- **New Methods:**
  - `WhenINavigateToTransactionsPage()` - Navigate away
  - `WhenILogOut()` - Log out via UI
  - `WhenILogBackIn()` - Log back in
  - `WhenINavigateToImportReviewPage()` - Navigate back to import review
  - `ThenPageShouldStillShowPendingTransactions(int count)` - Verify persistence
  - `ThenTransactionsShouldStillBeSelected(int count)` - Verify selection persistence

**Test Control Endpoints:**
- `POST /testcontrol/users/{username}/workspaces/{tenantKey}/import/seed` - Seed with specific selection state

**Test Data:**
- Seed 15 transactions (10 selected, 5 deselected)

**Dependencies:** Scenarios 1-6 (navigation, selection, state management)

**Status:** ⏳ Not Started

---

### Scenario 12: User deletes entire import review queue (Priority 12)

**Gherkin:**
```gherkin
Scenario: User deletes entire import review queue
  Given I am on the import review page
  And page shows 15 pending transactions
  When I click "Delete All" button
  Then page should show "No pending imports"
  And import review queue should be empty
```

**Page Object Models:**
- **File:** `tests/Functional/Pages/ImportPage.cs` (REUSE)
- **New Locators:**
  - `EmptyStateMessage` - `data-test-id="no-pending-imports-message"`
- **New Methods:**
  - `GetEmptyStateMessageAsync()` - Get empty state message text
  - `IsEmptyStateVisibleAsync()` - Check if empty state is shown

**Step Definitions:**
- **File:** `tests/Functional/Steps/BankImportSteps.cs` (REUSE)
- **New Methods:**
  - `WhenIClickDeleteAllButton()` - Click delete all
  - `ThenPageShouldShowNoPendingImports()` - Verify empty state message
  - `ThenImportReviewQueueShouldBeEmpty()` - Verify count is zero

**Test Control Endpoints:**
- `POST /testcontrol/users/{username}/workspaces/{tenantKey}/import/seed` - Seed review state

**Test Data:**
- Seed 15 transactions

**Dependencies:** Scenarios 1-2 (queue management)

**Status:** ⏳ Not Started

---

## Implementation Order

**Recommended sequence based on dependency and complexity:**

1. **Scenario 1:** User uploads bank file and sees import review page
   - **Reason:** Foundational - establishes ImportPage POM, upload workflow, test data files
   - **Complexity:** High (new POM, new patterns, test control endpoint enhancement)
   - **Blocks:** All other scenarios
   - **Estimated effort:** Significant (new infrastructure)

2. **Scenario 2:** User accepts selected transactions from import review
   - **Reason:** Completes core happy path (upload → review → accept)
   - **Complexity:** Medium (reuses POM, adds accept workflow, needs new test control endpoint)
   - **Depends on:** Scenario 1
   - **Estimated effort:** Moderate

3. **Scenario 3:** Viewer cannot access import page
   - **Reason:** Critical authorization verification, relatively simple
   - **Complexity:** Low (reuses POM, tests auth only)
   - **Depends on:** Scenario 1
   - **Estimated effort:** Quick

4. **Scenario 9:** User uploads corrupted file and sees error message
   - **Reason:** Error handling before more complex scenarios
   - **Complexity:** Low (reuses upload, adds error verification)
   - **Depends on:** Scenario 1
   - **Estimated effort:** Quick

5. **Scenario 10:** User uploads unsupported file format
   - **Reason:** Second error case, similar to Scenario 9
   - **Complexity:** Low (reuses error patterns)
   - **Depends on:** Scenarios 1, 9
   - **Estimated effort:** Quick

6. **Scenario 7:** User selects all transactions
   - **Reason:** Simple selection operation, builds toward complex selection tests
   - **Complexity:** Low (uses existing select-all method)
   - **Depends on:** Scenario 1
   - **Estimated effort:** Quick

7. **Scenario 8:** User deselects all transactions
   - **Reason:** Completes bulk selection patterns
   - **Complexity:** Low (uses existing deselect-all method)
   - **Depends on:** Scenarios 1, 7
   - **Estimated effort:** Quick

8. **Scenario 6:** User toggles transaction selection
   - **Reason:** Individual selection management with persistence testing
   - **Complexity:** Medium (individual selection + navigation + persistence)
   - **Depends on:** Scenarios 1, 7, 8
   - **Estimated effort:** Moderate

9. **Scenario 4:** User uploads with duplicates and sees marked potential duplicates
   - **Reason:** Duplicate detection UI verification
   - **Complexity:** High (needs duplicate seeding, badge verification)
   - **Depends on:** Scenario 1 (may need test control enhancement)
   - **Estimated effort:** Significant

10. **Scenario 5:** User accepts selected and rejects duplicates
    - **Reason:** Accept workflow with mixed selection (builds on Scenario 4)
    - **Complexity:** Medium (reuses accept workflow with duplicate context)
    - **Depends on:** Scenarios 2, 4
    - **Estimated effort:** Moderate

11. **Scenario 11:** User returns to pending import after session interruption
    - **Reason:** State persistence verification (complex navigation flow)
    - **Complexity:** High (logout/login + navigation + state verification)
    - **Depends on:** Scenarios 1, 6
    - **Estimated effort:** Significant

12. **Scenario 12:** User deletes entire import review queue
    - **Reason:** Queue cleanup operation
    - **Complexity:** Low (delete workflow + empty state)
    - **Depends on:** Scenario 1
    - **Estimated effort:** Quick

## Risk Assessment

### Technical Risks

**Risk 1: File Upload Complexity in Playwright**
- **Description:** File uploads can be tricky in Playwright, especially with Vue/Nuxt SSR applications. The file input might be hidden or wrapped in a custom component.
- **Mitigation:** Use Playwright's `setInputFiles()` method on the input element. Test early. Consider adding a `data-test-id` directly on the file input for reliable selection.

**Risk 2: Duplicate Detection Test Data Alignment**
- **Description:** Creating OFX files with specific FITIDs that match seeded test data requires careful alignment. Mismatch will cause false test failures.
- **Mitigation:** Use consistent FITID format in both test control seeding and OFX files. Document the FITID mapping clearly. Consider generating OFX files programmatically from test control data.

**Risk 3: New Test Control Endpoint Complexity**
- **Description:** The `POST /testcontrol/.../import/seed` endpoint needs to create ImportReviewTransaction records with correct tenant context, selection state, and duplicate detection fields.
- **Mitigation:** Implement and test the endpoint thoroughly before using in functional tests. Verify tenant isolation. Consider adding validation to the endpoint.

**Risk 4: API Client Regeneration**
- **Description:** Adding new test control endpoints requires regenerating the C# API client for functional tests.
- **Mitigation:** Follow existing patterns in `TestControlController.cs`. Run `dotnet build` on functional tests project to regenerate client. Verify new methods appear in generated client.

**Risk 5: Selection State Persistence**
- **Description:** Verifying selection state persists across navigation/logout requires careful timing and state checks. Race conditions possible.
- **Mitigation:** Use explicit waits. Verify state immediately after each action. Add retries for state verification if needed.

### Testing Risks

**Risk 1: OFX File Format Variations**
- **Description:** Real bank OFX files have many variations. Test files might not cover all edge cases.
- **Mitigation:** Use existing `Bank1.ofx` as reference. Start with simple OFX 1.x format (SGML). Test OFX 2.x (XML) separately. Add more variations as issues are discovered.

**Risk 2: Large Transaction Counts**
- **Description:** Scenarios mention "hundreds to thousands" of transactions but tests use 15-23 transactions for speed.
- **Mitigation:** Focus functional tests on workflow correctness, not scale. Rely on integration/performance tests for large volume testing.

**Risk 3: Timing Issues with File Upload and Parsing**
- **Description:** File upload → parsing → redirect → page render involves multiple async operations. Timing issues likely.
- **Mitigation:** Use API interception waits in ImportPage methods. Wait for specific API responses (`/import/upload`, `/import/review`). Add explicit wait for page ready state.

### Dependency Risks

**Risk 1: Backend Implementation Incomplete**
- **Description:** Functional tests assume backend APIs exist and work correctly. If backend is incomplete, tests will fail.
- **Mitigation:** Coordinate with backend implementation. Run integration tests first to verify API contracts. Use `[Explicit]` attribute on tests until backend is ready.

**Risk 2: Frontend Components Not Ready**
- **Description:** Tests require specific UI elements (`data-test-id` attributes, buttons, tables) that may not exist yet.
- **Mitigation:** Review MOCKUP-BANK-IMPORT.md for UI design. Coordinate with frontend implementation. Add test IDs to frontend requirements. Use `[Explicit]` until frontend is ready.

**Risk 3: Test Control Endpoint Authorization**
- **Description:** New import seed endpoint must use same anonymous tenant access pattern as transaction seed endpoint.
- **Mitigation:** Follow existing `SeedTransactions` endpoint pattern. Apply `[Authorize("AllowAnonymousTenantAccess")]` attribute. Test tenant context is set correctly.

## Pre-Implementation Checklist

Before starting Step 11 (Functional Tests implementation):

- [x] All scenarios analyzed above
- [x] POMs identified (new ImportPage.cs)
- [x] Step definitions identified (new BankImportSteps.cs, ~35 new step methods)
- [x] Test control endpoints identified (1 new endpoint needed, enhancements to existing)
- [x] Test data files specified (4 new OFX/CSV files)
- [x] Implementation order decided (1 → 2 → 3 → 9 → 10 → 7 → 8 → 6 → 4 → 5 → 11 → 12)
- [x] Risks assessed with mitigations
- [ ] User reviewed and approved this plan
- [ ] YAML status changed to `Approved`

## Next Steps

1. **Review this plan** with user for approval
2. **Update YAML status** to `Approved` after user review
3. **Proceed to Step 11** in Implementation Workflow
4. **Implement scenarios one at a time** following the order above
5. **Run tests after each scenario** to verify they pass
6. **Update scenario status** in this document as implementation progresses

## Notes

- **Preserve Priority Order:** When implementing, follow priority 1-12 from test plan, but use implementation order for actual work
- **Incremental Development:** Each scenario should be fully working before moving to next
- **Test Data Coordination:** OFX files and seeded data must have matching FITIDs/dates/amounts for duplicate detection
- **Frontend Coordination:** UI must include required `data-test-id` attributes per POM specifications
- **Backend Coordination:** APIs must exist and return expected responses per acceptance criteria
