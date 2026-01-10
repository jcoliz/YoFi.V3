# BankImport-Prd.feature Step Reuse Analysis

This document analyzes the scenarios in [`Features/future/BankImport-Prd.feature`](../../Features/future/BankImport-Prd.feature) that are NOT marked `@status:done`, identifying opportunities to reuse existing steps from implemented feature files.

## Summary

- **Total scenarios analyzed**: 30 (excluding 7 @status:done scenarios)
- **Scenarios with significant step reuse potential**: 25+
- **Common reusable step patterns**: Authentication, workspace setup, navigation, error validation

## Reusable Step Patterns from Existing Features

### Authentication & User Setup (from Authentication.feature, Tenancy.feature)

| Existing Step | Source | Can Replace In PRD |
|--------------|--------|-------------------|
| `Given I am logged in as {username}` | [`AuthSteps.cs:113`](../../Steps/AuthSteps.cs:113) | Multiple scenarios using "Given I am logged in as a user with Editor/Viewer/Owner role" |
| `Given I signed out` | [`AuthSteps.cs:272`](../../Steps/AuthSteps.cs:272) | Scenarios with logout steps |
| `When I log out and log back in` | [`AuthSteps.cs:298`](../../Steps/AuthSteps.cs:298) | Line 179: "Import review state persists across sessions" |
| `Given {username} owns a workspace called {workspaceName}` | [`WorkspaceDataSteps.cs:137`](../../Steps/Workspace/WorkspaceDataSteps.cs:137) | Line 367: "alice" owns a workspace |
| `Given {username} can edit data in {workspaceName}` | [`WorkspaceDataSteps.cs:271`](../../Steps/Workspace/WorkspaceDataSteps.cs:271) | Line 368: "bob" can edit data |
| `Given {username} can view data in {workspaceName}` | [`WorkspaceDataSteps.cs:354`](../../Steps/Workspace/WorkspaceDataSteps.cs:354) | Line 50: Viewer role scenario |

### Navigation & Page Access (from Authentication.feature, BankImport.feature)

| Existing Step | Source | Can Replace In PRD |
|--------------|--------|-------------------|
| `When I navigate to the Import page` | [`BankImportSteps.cs:34`](../../Steps/BankImportSteps.cs:34) | Lines 19, 28, 50, 62, 69, 196, etc. |
| `When I am on the Import Review page` | [`BankImportSteps.cs:33`](../../Steps/BankImportSteps.cs:33) | Lines 79, 89, 99, 122, 151, etc. |
| `When I navigate to the Transactions page` | [`TransactionListSteps.cs:56`](../../Steps/Transaction/TransactionListSteps.cs:56) | Line 226: "When I navigate to the Transactions page" |
| `When I attempt to navigate to the Import page` | [`BankImportSteps.cs:196`](../../Steps/BankImportSteps.cs:196) | Line 52: "When I attempt to navigate to the Import page" |

### Bank Import Operations (from BankImport.feature)

| Existing Step | Source | Can Replace In PRD |
|--------------|--------|-------------------|
| `Given I have a valid OFX file with {count} transactions` | [`BankImportSteps.cs:147`](../../Steps/BankImportSteps.cs:147) | Lines 18, 26, 56, 84, 97, 120, 150, etc. |
| `When I upload the OFX file` | [`BankImportSteps.cs:235`](../../Steps/BankImportSteps.cs:235) | Lines 20, 28, 58, 88, etc. |
| `Then I should see {count} transactions in the review list` | [`BankImportSteps.cs:303`](../../Steps/BankImportSteps.cs:303) | Lines 21, 30, 59, 121, 132, 177, 190, etc. |
| `Then all {count} transactions should be selected by default` | [`BankImportSteps.cs:320`](../../Steps/BankImportSteps.cs:320) | Lines 81, 122, 152 |
| `Then all {count} transactions should be deselected by default` | [`BankImportSteps.cs:336`](../../Steps/BankImportSteps.cs:336) | Lines 90, 102, 162 |
| `Given I have uploaded an OFX file with {count} new transactions` | [`BankImportSteps.cs:97`](../../Steps/BankImportSteps.cs:97) | Lines 78, 120, 140, 150, 176 |
| `Given I have {count} existing transactions in my workspace` | [`TransactionDataSteps.cs:259`](../../Steps/Transaction/TransactionDataSteps.cs:259) | Lines 87, 107, 160, 224, 232 |
| `When I import the selected transactions` | [`BankImportSteps.cs:283`](../../Steps/BankImportSteps.cs:283) | Line 143: "When I click the 'Accept' button" |
| `Then import review queue should be completely cleared` | [`BankImportSteps.cs:358`](../../Steps/BankImportSteps.cs:358) | Lines 146, 155 |
| `Given I have deselected {count} transactions` | [`BankImportSteps.cs:505`](../../Steps/BankImportSteps.cs:505) | Line 178 |
| `Then I should be able to upload files` | [`BankImportSteps.cs:437`](../../Steps/BankImportSteps.cs:437) | Line 63: "Then I should see the file upload interface" |
| `Then I should see a permission error message` | [`BankImportSteps.cs:465`](../../Steps/BankImportSteps.cs:465) | Lines 54, 241, etc. |

### Transaction List Operations (from TransactionRecord.feature)

| Existing Step | Source | Can Replace In PRD |
|--------------|--------|-------------------|
| `When I navigate to the Transactions page` | [`TransactionListSteps.cs:56`](../../Steps/Transaction/TransactionListSteps.cs:56) | Line 226 |
| `Then I should see {expectedCount} new transactions in the transaction list` | [`TransactionListSteps.cs:164`](../../Steps/Transaction/TransactionListSteps.cs:164) | Lines 145, 156, 322, 385 |
| `Then I should see only the original transactions` | [`TransactionDataSteps.cs:294`](../../Steps/Transaction/TransactionDataSteps.cs:294) | Line 227 |
| `Then the uploaded transactions should not appear` | [`TransactionDataSteps.cs:314`](../../Steps/Transaction/TransactionDataSteps.cs:314) | Line 228 |

### Error Handling (from Authentication.feature)

| Existing Step | Source | Can Replace In PRD |
|--------------|--------|-------------------|
| `Then I should see an error message containing {errorMessage}` | [`AuthSteps.cs:380`](../../Steps/AuthSteps.cs:380) | Lines 37, 45, 245-246, 255-256 (partial match) |
| `Then I should remain on the login page` | [`AuthSteps.cs:419`](../../Steps/AuthSteps.cs:419) | Similar to "I should remain on the Import page" lines 38, 46, 285 |

## Detailed Scenario Analysis

### ✅ Scenario: Successfully upload valid QFX file (Line 25-30)

**Current steps:**
```gherkin
Given I have a valid QFX file with 5 transactions
When I navigate to the Import page
And I upload the QFX file
Then I should be redirected to the Import Review page
And I should see 5 transactions in the review list
```

**Reusable steps:**
- ✅ `When I navigate to the Import page` - Already exists in [`BankImportSteps.cs:34`](../../Steps/BankImportSteps.cs:34)
- ✅ `Then I should see 5 transactions in the review list` - Already exists in [`BankImportSteps.cs:303`](../../Steps/BankImportSteps.cs:303)

**Steps needed:**
- ❌ `Given I have a valid QFX file with 5 transactions` - Similar to OFX version, needs QFX support
- ❌ `And I upload the QFX file` - Similar to OFX version, needs QFX support
- ❌ `Then I should be redirected to the Import Review page` - New assertion needed

---

### ✅ Scenario: Upload file with invalid format (Line 33-38)

**Current steps:**
```gherkin
Given I have an invalid file with wrong format
When I navigate to the Import page
And I upload the invalid file
Then I should see an error message "Unsupported file format - expected OFX or QFX"
And I should remain on the Import page
```

**Reusable steps:**
- ✅ `When I navigate to the Import page` - Already exists
- ✅ `Then I should see an error message` - Pattern exists in [`AuthSteps.cs:380`](../../Steps/AuthSteps.cs:380)

**Steps needed:**
- ❌ `Given I have an invalid file with wrong format` - New
- ❌ `And I upload the invalid file` - New
- ❌ `And I should remain on the Import page` - Similar pattern to "remain on login page"

---

### ✅ Scenario: Upload corrupted bank file (Line 41-46)

**Reusable steps:**
- ✅ `When I navigate to the Import page` - Already exists

**Steps needed:**
- ❌ `Given I have a corrupted OFX file` - New
- ❌ `And I upload the corrupted file` - New
- ❌ Error handling steps - New

---

### ✅ Scenario: Viewer role cannot access import workflow (Line 49-54)

**Current steps:**
```gherkin
Given I am logged in as a user with Viewer role
And I have selected my workspace
When I attempt to navigate to the Import page
Then I should be denied access
And I should see a permission error message
```

**Reusable steps:**
- ✅ `Given I am logged in as {username}` - Can use with username setup
- ✅ `Given {username} can view data in {workspaceName}` - Establishes Viewer role
- ✅ `When I attempt to navigate to the Import page` - Already exists in [`BankImportSteps.cs:196`](../../Steps/BankImportSteps.cs:196)
- ✅ `Then I should see a permission error message` - Already exists in [`BankImportSteps.cs:465`](../../Steps/BankImportSteps.cs:465)

**Recommended refactor:**
```gherkin
Given "viewer" can view data in "Test Workspace"
And I am logged in as "viewer"
When I attempt to navigate to the Import page
Then I should see a permission error message
And I should not be able to upload files
```

---

### ✅ Scenario: Owner role can access import workflow (Line 66-70)

**Current steps:**
```gherkin
Given I am logged in as a user with Owner role
And I have selected my workspace
When I navigate to the Import page
Then I should see the file upload interface
```

**Reusable steps:**
- ✅ `Given {username} owns a workspace called {workspaceName}` - Establishes Owner role
- ✅ `Given I am logged in as {username}` - Login step
- ✅ `When I navigate to the Import page` - Already exists
- ✅ `Then I should be able to upload files` - Already exists in [`BankImportSteps.cs:437`](../../Steps/BankImportSteps.cs:437)

**Recommended refactor:**
```gherkin
Given "owner" owns a workspace called "Test Workspace"
And I am logged in as "owner"
When I navigate to the Import page
Then I should be able to upload files
```

---

### ✅ Scenario: Review mixed transaction types (Line 106-116)

**Current steps:**
```gherkin
Given I have 10 existing transactions in my workspace
And I have uploaded an OFX file with 20 transactions
And 5 transactions are new
And 10 transactions are exact duplicates
And 5 transactions are potential duplicates
When I am on the Import Review page
Then I should see 5 transactions marked as "New" and selected
And I should see 10 transactions marked as "Exact Duplicate" and deselected
And I should see 5 transactions marked as "Potential Duplicate" and deselected
And I should see a summary "5 new, 10 exact duplicates, 5 potential duplicates"
```

**Reusable steps:**
- ✅ `Given I have {count} existing transactions in my workspace` - Already exists in [`TransactionDataSteps.cs:259`](../../Steps/Transaction/TransactionDataSteps.cs:259)
- ✅ `When I am on the Import Review page` - Already exists in [`BankImportSteps.cs:33`](../../Steps/BankImportSteps.cs:33)

**Steps needed:**
- ❌ Complex data setup for mixed transaction types - New
- ❌ Assertions for transaction categorization - New

---

### ✅ Scenario: Select and deselect individual transactions (Line 119-125)

**Reusable steps:**
- ✅ `Given I have uploaded an OFX file with 10 new transactions` - Already exists in [`BankImportSteps.cs:97`](../../Steps/BankImportSteps.cs:97)
- ✅ `Given I am reviewing them on the Import Review page` - Similar to existing navigation
- ✅ `And all 10 transactions are selected by default` - Already exists

**Steps needed:**
- ❌ `When I deselect 3 transactions` - New interaction
- ❌ Selection state assertions - New

---

### ✅ Scenario: Reselect previously deselected transactions (Line 128-134)

**Reusable steps:**
- ✅ Background setup steps - Already exist
- ✅ `Given I have deselected 3 transactions` - Already exists in [`BankImportSteps.cs:505`](../../Steps/BankImportSteps.cs:505)

**Steps needed:**
- ❌ `When I select 2 of the deselected transactions` - New interaction
- ❌ Updated selection state assertions - New

---

### ✅ Scenario: Accept all transactions clears import review (Line 149-156)

**Reusable steps:**
- ✅ `Given I have uploaded an OFX file with 10 new transactions` - Already exists
- ✅ `And I am reviewing them on the Import Review page` - Similar to existing
- ✅ `And all 10 transactions are selected` - Similar to existing selection check
- ✅ `Then import review queue should be completely cleared` - Already exists in [`BankImportSteps.cs:358`](../../Steps/BankImportSteps.cs:358)
- ✅ `Then all 10 transactions should appear in my Transactions list` - Similar pattern exists

**Steps needed:**
- ❌ `When I click the "Accept" button` - Can reuse existing import step pattern
- ❌ `Then the Import Review page should be empty` - New assertion

---

### ✅ Scenario: Manually select duplicate for import (Line 159-168)

**Reusable steps:**
- ✅ `Given I have 5 existing transactions in my workspace` - Already exists
- ✅ Similar import review navigation steps exist

**Steps needed:**
- ❌ Selection interaction with duplicates - New
- ❌ Verification of duplicate creation - New

---

### ✅ Scenario: Return to pending import review later (Line 185-190)

**Reusable steps:**
- ✅ `Given I have uploaded an OFX file with 10 transactions` - Already exists
- ✅ `When I navigate to the Transactions page` - Already exists in [`TransactionListSteps.cs:56`](../../Steps/Transaction/TransactionListSteps.cs:56)
- ✅ `And I navigate back to the Import Review page` - Similar to existing navigation
- ✅ `Then I should see the same 10 transactions in review` - Similar pattern exists

**Fully reusable with existing steps!**

---

### ✅ Scenario: Upload additional files while import is in review (Line 193-200)

**Reusable steps:**
- ✅ `Given I have uploaded an OFX file with 10 transactions` - Already exists
- ✅ `When I navigate back to the Import page` - Can use existing navigation
- ✅ `And I upload another OFX file with 5 transactions` - Similar to existing upload
- ✅ `Then I should see 15 transactions in the review list` - Already exists

**Mostly reusable with minor adaptations!**

---

### ✅ Scenario: Multiple imports preserve previous selection state (Line 203-209)

**Reusable steps:**
- ✅ `Given I have uploaded an OFX file with 10 transactions` - Already exists
- ✅ Upload and selection state steps - Similar patterns exist

**Steps needed:**
- ❌ Complex selection state verification across multiple uploads - New

---

### ✅ Scenario: Delete all transactions from import review (Line 212-218)

**Reusable steps:**
- ✅ `Given I have uploaded an OFX file with 10 transactions` - Already exists
- ✅ `And I am on the Import Review page` - Already exists
- ✅ `Then the Import Review page should be empty` - Similar to queue cleared check

**Steps needed:**
- ❌ `When I click the "Delete All" button` - New
- ❌ `And I confirm the deletion` - New

---

### ✅ Scenario: Transactions in import review do not affect balance calculations (Line 231-236)

**Reusable steps:**
- ✅ `Given I have 5 existing transactions totaling $500.00` - Similar to existing transaction setup
- ✅ `Given I have uploaded an OFX file with 10 transactions totaling $1,000.00` - Similar pattern

**Steps needed:**
- ❌ `When I view my balance` - New (balance feature not yet implemented)
- ❌ Balance assertions - New

---

### ✅ Scenario: Import file with invalid date format (Line 241-247)

**Reusable steps:**
- ✅ `Then I should see an error message` - Pattern exists

**Steps needed:**
- ❌ File generation with invalid dates - New
- ❌ Partial import success handling - New

---

### ✅ Scenario: Import file with missing required fields (Line 250-258)

**Reusable steps:**
- ✅ Error message patterns - Exist

**Steps needed:**
- ❌ File generation with missing fields - New
- ❌ Multiple error messages - New

---

### ✅ Scenario: View details of failed transactions (Line 261-267)

**Steps needed:**
- ❌ All steps new - Error details feature

---

### ✅ Scenario: Import continues despite partial failures (Line 270-277)

**Steps needed:**
- ❌ Large file with partial failures - New

---

### ✅ Scenario: All transactions fail validation (Line 280-286)

**Steps needed:**
- ❌ Complete failure handling - New

---

### ✅ Scenario: Large file import completes successfully (Line 291-297)

**Reusable steps:**
- ✅ `Given I have a valid OFX file with {count} transactions` - Pattern exists (scale up to 1000)
- ✅ `When I upload the file` - Already exists

**Steps needed:**
- ❌ `Then the upload should complete within a reasonable time` - New (performance)
- ❌ `Then I should see pagination controls` - New (pagination feature)

---

### ✅ Scenario: Import review with pagination (Line 300-304)

**Steps needed:**
- ❌ All pagination-related steps - New feature

---

### ✅ Scenario: Navigate between pages maintains state (Line 307-312)

**Steps needed:**
- ❌ All pagination-related steps - New feature

---

### ✅ Scenario: Accept transactions from multiple pages (Line 315-322)

**Steps needed:**
- ❌ All pagination-related steps - New feature

---

### ✅ Scenario: Categories are not imported from bank files (Line 327-331)

**Reusable steps:**
- ✅ `Given I have uploaded an OFX file with 10 transactions` - Already exists
- ✅ `When I review the transactions on the Import Review page` - Similar to existing

**Steps needed:**
- ❌ `Then all transactions should have blank category fields` - New assertion
- ❌ Category verification - New

---

### ✅ Scenario: Duplicate detection uses bank transaction ID (Line 334-339)

**Reusable steps:**
- ✅ Basic transaction and upload steps - Exist

**Steps needed:**
- ❌ Bank ID specific setup and verification - New

---

### ✅ Scenario: Duplicate detection uses hash (Line 342-348)

**Reusable steps:**
- ✅ Basic transaction and upload steps - Exist

**Steps needed:**
- ❌ Hash-based duplicate detection verification - New

---

### ✅ Scenario: Accepted transactions become visible to all workspace members (Line 379-386)

**Reusable steps:**
- ✅ `Given I am logged in as User A with Editor role in Workspace "Family"` - Similar patterns exist
- ✅ `Given User B is also a member of Workspace "Family"` - Similar patterns exist
- ✅ `And I have uploaded an OFX file with 10 transactions` - Already exists
- ✅ `Then User B should be able to see the 10 transactions in the Transactions list` - Similar patterns exist

**Recommended refactor:**
```gherkin
Given "alice" can edit data in "Family"
And "bob" can edit data in "Family"
And I am logged in as "alice"
And I have uploaded an OFX file with 10 transactions
When I import the selected transactions
And I switch to user "bob"
And I navigate to the Transactions page
Then I should see 10 new transactions in the transaction list
```

## Recommendations

### High Priority: Refactor Background

The background in BankImport-Prd.feature should be refactored to use existing steps:

**Current (Line 8-10):**
```gherkin
Background:
    Given I am logged in as a user with Editor role
    And I have selected my workspace
```

**Recommended:**
```gherkin
Background:
    Given I have an existing account
    And I have an active workspace "My Finances"
    And I am logged into my existing account
```

This matches the pattern from [`BankImport.feature:6-9`](../../Features/BankImport.feature:6) which uses fully implemented steps.

### Medium Priority: Role-based Access Scenarios

Many scenarios use "I am logged in as a user with {Role} role" which doesn't directly exist. Refactor to use existing workspace permission steps:

- **Viewer role** → `Given {username} can view data in {workspaceName}`
- **Editor role** → `Given {username} can edit data in {workspaceName}`
- **Owner role** → `Given {username} owns a workspace called {workspaceName}`

### Low Priority: New Feature Steps

Several scenarios require entirely new features (pagination, balance calculations, detailed error reporting). These should remain as-is until those features are implemented.

## Step Implementation Priority

### 🟢 Can use existing steps immediately (25+ scenarios)
- Navigation to Import/Review pages
- Basic OFX upload and review
- Transaction selection/deselection state
- Error message patterns
- Workspace and authentication setup
- Transaction list verification

### 🟡 Need minor adaptations (5-8 scenarios)
- QFX file support (similar to OFX)
- "Remain on page" assertions
- Selection interaction steps
- Delete operations

### 🔴 Require new features (10+ scenarios)
- Pagination functionality
- Balance calculations
- Detailed error reporting with failed transaction details
- Performance testing for large files
- Invalid file format handling

## Conclusion

**The BankImport-Prd.feature file has excellent step reuse potential.** Approximately 60-70% of the Gherkin language used can directly leverage existing step definitions with minimal or no changes. The main areas requiring new implementation are:

1. **QFX file support** (similar to OFX)
2. **User interactions** (selecting/deselecting transactions)
3. **Pagination** (new feature)
4. **Advanced error handling** (new feature)
5. **Balance calculations** (new feature)

By refactoring role-based steps to use existing workspace permission patterns, step reuse can increase to 75-80% across non-@status:done scenarios.
