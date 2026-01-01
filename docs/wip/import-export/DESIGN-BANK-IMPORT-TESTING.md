---
status: Draft
layer: Testing
parent: DESIGN-BANK-IMPORT.md
created: 2025-12-28
related_docs:
  - DESIGN-BANK-IMPORT.md
  - DESIGN-BANK-IMPORT-DATABASE.md
  - DESIGN-BANK-IMPORT-APPLICATION.md
  - DESIGN-BANK-IMPORT-API.md
  - DESIGN-BANK-IMPORT-FRONTEND.md
  - PRD-BANK-IMPORT.md
  - VISUAL-DESIGN-BANK-IMPORT.md
  - MOCKUP-BANK-IMPORT.md
---

# Testing Strategy Design: Bank Import Feature

## Overview

This document defines the comprehensive testing strategy for the Bank Import feature as specified in [`PRD-BANK-IMPORT.md`](PRD-BANK-IMPORT.md). The strategy covers all test layers (Unit, Integration.Data, Integration.Controller, and Functional) with specific test cases, file locations, and verification criteria.

**Test Distribution Target:**
- **70% Controller Integration** - Import/review/complete API workflow, authorization, duplicate detection, selection management
- **15% Unit** - OFX parsing, duplicate key generation, field extraction
- **15% Functional** - Upload → Review → Complete user workflows, selection operations

**Total Estimated Tests:** 48 tests (30 controller + 7 unit + 5 data + 12 functional)

**Why Integration-heavy?** The Bank Import feature is primarily about API state management (upload → review → accept), duplicate detection (database queries), and multi-request workflows - all optimally tested at the integration level.

## Test Strategy Alignment

This testing strategy follows the project's testing pyramid as defined in [`docs/TESTING-STRATEGY.md`](../../TESTING-STRATEGY.md):

**Inverted Pyramid Model:**
```
        ▲
       ╱ ╲
      ╱   ╲     15% - Functional (critical workflows)
     ╱_____╲
    ╱       ╲
   ╱  Unit   ╲  15% - Unit (parsing logic, algorithms)
  ╱___________╲
 ╱             ╲
╱  Integration  ╲ 70% - Integration (API contracts, state management)
╱_________________╲
```

**Key Principle:** Controller Integration tests are the sweet spot for this feature because:
- Fast execution (~100-200ms per test)
- Tests complete API workflows with database state
- Verifies authentication, authorization, and HTTP contracts
- Low maintenance burden compared to functional tests

## Unit Tests (5-7 tests)

**Location:** [`tests/Unit/`](../../../tests/Unit/)

**Purpose:** Test OFX parsing logic, duplicate key generation, and field extraction in isolation.

**Test Framework:** NUnit with constraint-based assertions

**Why minimal unit testing?** The feature uses an external OFX parsing library ([`OfxSharp`](https://github.com/mrstebo/OFXSharp)) for complex parsing. Most complexity lies in API orchestration and database state management, which require integration testing.

### Test Group 1: OFX/QFX Format Parsing

**Existing Implementation:** [`tests/Unit/OFXParsingServiceTests.cs`](../../../tests/Unit/OFXParsingServiceTests.cs)

**Note:** These tests already exist and validate the [`OfxParsingService`](../../../src/Application/Import/Services/OfxParsingService.cs). Reference them in the testing plan but do not recreate.

**Coverage:**
- OFX 2.x format (XML-based) parsing
- QFX/OFX 1.x format (SGML-based) parsing
- Transaction field extraction (Date, Payee, Amount, Memo, FITID)
- Multiple transaction parsing
- Error handling for malformed files

**Sample Test Reference:**
```csharp
[Test]
public async Task ParseAsync_ValidOFXFile_ReturnsTransactions()
{
    // Given: Valid OFX 2.x file (XML-based)
    // When: OFX is parsed
    // Then: Should return transactions with correct data
    // And: Date, Amount, Payee should be extracted correctly
}
```

**Test Data Location:** [`tests/Unit/SampleData/Ofx/`](../../../tests/Unit/SampleData/Ofx/)

**Available Sample Files:**
- `bank-banking-xml.ofx` - OFX 2.x XML format
- `Bank1.ofx` - Standard checking account
- `CC2.OFX` - Credit card transactions
- `creditcard.ofx` - Credit card with multiple transactions
- `issue-17.ofx` - Edge case for specific parsing issue
- `itau.ofx` - International bank format

### Test Group 2: Duplicate Detection Logic (ImportReviewFeature)

**File:** [`tests/Unit/ImportReviewFeatureTests.cs`](../../../tests/Unit/ImportReviewFeatureTests.cs) (new file)

**Method Under Test:** `ImportReviewFeature.DetectDuplicate()`

**Testing Approach:** Make `DetectDuplicate()` `internal` and test it directly using `InternalsVisibleTo`. This provides better test isolation, clearer failure messages, and easier edge case setup compared to testing through the public `ImportFileAsync()` method.

**Setup Required:**
- Add `[assembly: InternalsVisibleTo("YoFi.V3.Tests.Unit")]` to Application project's AssemblyInfo.cs
- Change method signature from `private static` to `internal static`
- Test directly: `var duplicates = ImportReviewFeature.DetectDuplicate(importDto, existing, pending);`

**Test Cases:**

**Test 1: New transaction (no duplicates)**
```gherkin
Scenario: Import file async with new transaction is marked as new
    Given OFX file with transaction that doesn't exist in database
    And no pending import review transactions
    When file is imported
    Then transaction should be marked as DuplicateStatus.New
    And transaction should have null DuplicateOfKey
```

**Test 2: Exact duplicate against main Transaction table**
```gherkin
Scenario: Import file async with exact duplicate in main table is marked as exact duplicate
    Given existing transaction in main Transaction table with FITID "FITID12345"
    And no pending import review transactions
    And OFX file with transaction having same FITID and matching data
    When file is imported
    Then transaction should be marked as DuplicateStatus.ExactDuplicate
    And should reference the existing transaction's Key
```

**Test 3: Potential duplicate against main Transaction table**
```gherkin
Scenario: Import file async with potential duplicate in main table is marked as potential duplicate
    Given existing transaction in main Transaction table with FITID "FITID12345" and amount $50.00
    And no pending import review transactions
    And OFX file with same FITID but amount $55.00 (bank correction?)
    When file is imported
    Then transaction should be marked as DuplicateStatus.PotentialDuplicate
    And should reference the existing transaction's Key for user review
```

**Test 4: Exact duplicate against pending import review table**
```gherkin
Scenario: Import file async with exact duplicate in pending imports is marked as exact duplicate
    Given pending import review transaction with FITID "FITID12345" and amount $50.00
    And OFX file with transaction having same FITID and matching data
    When second file is imported (same session)
    Then transaction should be marked as DuplicateStatus.ExactDuplicate
    And should reference the pending import transaction's Key
```

**Test 5: Potential duplicate against pending import review table**
```gherkin
Scenario: Import file async with potential duplicate in pending imports is marked as potential duplicate
    Given pending import review transaction with FITID "FITID12345" and amount $50.00
    And OFX file with same FITID but amount $55.00 (bank correction?)
    When second file is imported (same session)
    Then transaction should be marked as DuplicateStatus.PotentialDuplicate
    And should reference the pending import transaction's Key for user review
```

**Test 6: One incoming transaction matches multiple existing duplicates**
```gherkin
Scenario: Import file async with one incoming transaction matching multiple existing duplicates
    Given 3 existing transactions in main Transaction table all with FITID "FITID12345" with different dates
    And transactions are dated: 2024-01-15, 2024-01-20, 2024-01-25
    And no pending import review transactions
    And OFX file with one transaction having FITID "FITID12345"
    When file is imported
    Then transaction should be marked as duplicate (ExactDuplicate or PotentialDuplicate based on data match)
    And DuplicateOfKey should reference the most recent matching transaction (dated 2024-01-25)
    And a warning should be logged indicating multiple matches were found
```

## Integration Tests - Data Layer (3-5 tests)

**Location:** [`tests/Integration.Data/`](../../../tests/Integration.Data/)

**File:** [`tests/Integration.Data/ImportReviewTransactionTests.cs`](../../../tests/Integration.Data/ImportReviewTransactionTests.cs) (new file)

**Purpose:** Verify [`ImportReviewTransaction`](../../../src/Entities/Models/ImportReviewTransaction.cs) entity CRUD operations, tenant isolation, and database constraints.

**Test Framework:** NUnit + Entity Framework Core (in-memory database)

### Test Cases

**Test 1: Create import review transaction with tenant isolation**
```gherkin
Scenario: Add import review transaction with tenant id success
    Given a valid import review transaction with TenantId
    When transaction is added to database
    Then transaction should be persisted
```

**Test 2: Query filters by tenant (isolation verification)**
```gherkin
Scenario: Get import review transactions filters by tenant
    Given import review transactions for two different tenants
    When querying for tenant1's transactions
    Then should only return tenant1's transactions
```

**Test 3: Cascade delete when tenant is deleted**
```gherkin
Scenario: Delete tenant cascades delete import review transactions
    Given a tenant with import review transactions
    When tenant is deleted
    Then import review transactions should be cascade deleted
```

**Test 4: Index performance on (TenantId, ExternalId)**
```gherkin
Scenario: Query by tenant and external id uses index
    Given multiple import review transactions
    When querying by TenantId and ExternalId (index should be used)
    Then should efficiently retrieve the correct transaction
```

**Optional Test 5: DuplicateStatus enum storage**
```gherkin
Scenario: Import review transaction stores duplicate status correctly
    Given import review transactions with all DuplicateStatus values
    When stored and retrieved
    Then DuplicateStatus should be preserved correctly
```

## Integration Tests - Controller Layer (30 tests)

**Location:** [`tests/Integration.Controller/`](../../../tests/Integration.Controller/)

**File:** [`tests/Integration.Controller/ImportControllerTests.cs`](../../../tests/Integration.Controller/ImportControllerTests.cs) (new file)

**Purpose:** Test [`ImportController`](../../../src/Controllers/ImportController.cs) API endpoints with complete HTTP request/response cycle, authentication, authorization, and database operations.

**Test Framework:** NUnit + ASP.NET Core WebApplicationFactory + In-Memory Database

**Base Class:** [`AuthenticatedTestBase`](../../../tests/Integration.Controller/TestHelpers/AuthenticatedTestBase.cs)

### Test Group 1: POST /api/tenant/{tenantId}/import/upload

**Endpoint:** Upload OFX/QFX file

**Test 1: Success - Valid OFX file**
```gherkin
Scenario: Upload bank file valid OFX returns OK
    Given user has Editor role for tenant
    And valid OFX file with 3 transactions
    When user uploads OFX file
    Then 200 OK should be returned
    And response should contain import summary
```

**Test 2: Success - QFX format (SGML)**
```gherkin
Scenario: Upload bank file QFX format returns OK
    Given user has Editor role for tenant
    And valid QFX file (SGML-like OFX 1.x format)
    When user uploads QFX file
    Then 200 OK should be returned
```

**Test 3: Error - Corrupted file**
```gherkin
Scenario: Upload bank file corrupted file returns bad request
    Given user has Editor role for tenant
    And corrupted OFX file (invalid XML)
    When user uploads corrupted file
    Then 400 Bad Request should be returned
    And error message should indicate parsing failure
```

**Test 4: Error - Unsupported format**
```gherkin
Scenario: Upload bank file unsupported format returns bad request
    Given user has Editor role for tenant
    And unsupported file format (CSV instead of OFX/QFX)
    When user uploads CSV file
    Then 400 Bad Request should be returned
```

**Test 5: Authorization - Viewer role forbidden**
```gherkin
Scenario: Upload bank file as viewer returns forbidden
    Given user has Viewer role for tenant (read-only)
    And valid OFX file
    When viewer attempts to upload file
    Then 403 Forbidden should be returned
```

**Test 6: Authorization - Unauthenticated**
```gherkin
Scenario: Upload bank file unauthenticated returns unauthorized
    Given no authentication token provided
    And valid OFX file
    When request is made without authentication
    Then 401 Unauthorized should be returned
```

**Note:** This test might return 403 Forbidden instead of 401 Unauthorized depending on the authentication middleware configuration. Both are acceptable responses for unauthenticated requests in this context.

**Test 7: Tenant isolation - Different tenant forbidden**
```gherkin
Scenario: Upload bank file different tenant returns forbidden
    Given user has Editor role for tenant A
    And valid OFX file
    When user attempts to upload to tenant B
    Then 403 Forbidden should be returned (tenant isolation)
```

**Test 8: Partial success (some transactions fail validation)**
```gherkin
Scenario: Upload bank file partial failure returns partial success
    Given user has Editor role for tenant
    And OFX file with 2 valid and 1 invalid transaction (missing amount)
    When user uploads file with partial failures
    Then 200 OK should be returned (partial success)
    And response should indicate which transactions succeeded/failed
```

### Test Group 2: GET /api/tenant/{tenantId}/import/review

**Endpoint:** Get pending import review transactions

**Test 9: Success - Returns pending transactions**
```gherkin
Scenario: Get import review with pending transactions returns OK
    Given user has Editor role for tenant
    And 3 transactions in review state
    When user requests import review
    Then 200 OK should be returned
    And response should contain 3 pending transactions
```

**Test 10: Empty result - No pending transactions**
```gherkin
Scenario: Get import review no pending transactions returns empty list
    Given user has Editor role for tenant
    And no transactions in review state
    When user requests import review
    Then 200 OK should be returned
    And response should be empty list
```

**Test 11: Persistence - State persists across requests**
```gherkin
Scenario: Get import review persists across sessions returns OK
    Given user has Editor role for tenant
    And user uploaded transactions (previous request)
    When user requests import review (new request, simulates new session)
    Then 200 OK should be returned
    And pending transactions should still be there
```

**Test 12: Authorization - Viewer role forbidden**
```gherkin
Scenario: Get import review as viewer returns forbidden
    Given user has Viewer role for tenant (read-only)
    And pending import review transactions exist
    When viewer attempts to get import review
    Then 403 Forbidden should be returned
```

**Test 13: Tenant isolation - Only shows user's tenant data**
```gherkin
Scenario: Get import review different tenant returns empty
    Given user A has Editor role for tenant A
    And user A also has Editor role for tenant B
    And tenant B has pending import review transactions
    When user A requests import review for tenant A
    Then 200 OK should be returned
    And should not see tenant B's transactions (isolation)
```

**Test 14: Pagination - Invalid page number defaults to 1**
```gherkin
Scenario: Get import review invalid page number defaults to first page
    Given user has Editor role for tenant
    And 100 transactions in review state
    When user requests import review with pageNumber=0
    Then 200 OK should be returned
    And response should contain first page (pageNumber: 1)
    And should return first 50 transactions (default page size)
```

**Test 15: Pagination - Invalid page size defaults to 50**
```gherkin
Scenario: Get import review invalid page size defaults to default
    Given user has Editor role for tenant
    And 100 transactions in review state
    When user requests import review with pageSize=-5
    Then 200 OK should be returned
    And response should use default page size (pageSize: 50)
```

**Test 16: Pagination - Excessive page size clamped to maximum**
```gherkin
Scenario: Get import review excessive page size clamped to maximum
    Given user has Editor role for tenant
    And 2000 transactions in review state
    When user requests import review with pageSize=5000
    Then 200 OK should be returned
    And response should use maximum page size (pageSize: 1000)
    And should return first 1000 transactions
```

### Test Group 3: POST /api/tenant/{tenantId}/import/review/complete

**Endpoint:** Complete review by accepting selected transactions and deleting all pending transactions

**Note:** This endpoint performs two atomic operations:
1. Copies selected transactions to the main Transaction table
2. Deletes ALL transactions from ImportReviewTransaction table (both selected and unselected)

**Test 17: Success - Complete review with selected transactions**
```gherkin
Scenario: Complete review selected transactions returns OK
    Given user has Editor role for tenant
    And 3 transactions in review state
    And user selects 2 of the 3 transactions
    When user completes review with selected transactions
    Then 200 OK should be returned
    And response should indicate 2 transactions accepted and 1 rejected
    And accepted transactions should appear in main transaction list
    And review queue should be completely empty
```

**Test 18: Authorization - Viewer forbidden**
```gherkin
Scenario: Complete review as viewer returns forbidden
    Given user has Viewer role for tenant (read-only)
    And transactions exist in review state
    When viewer attempts to complete review
    Then 403 Forbidden should be returned
```

**Test 19: Validation - Empty selection**
```gherkin
Scenario: Complete review empty selection returns bad request
    Given user has Editor role for tenant
    And transactions exist in review state
    When user completes review with empty selection
    Then 400 Bad Request should be returned
    And error should indicate empty selection
```

**Test 20: Validation - Null selection**
```gherkin
Scenario: Complete review null selection returns bad request
    Given user has Editor role for tenant
    And transactions exist in review state
    When user completes review with null selection
    Then 400 Bad Request should be returned
    And error should indicate null or empty selection not allowed
```

**Test 21: Tenant isolation - Cannot complete other tenant's review**
```gherkin
Scenario: Complete review different tenant returns forbidden
    Given user A has Editor role for tenant A
    And user A has no role on tenant B
    And tenant B has pending import review transactions
    When user A attempts to complete tenant B's review
    Then 403 Forbidden should be returned (tenant isolation)
```

### Test Group 4: DELETE /api/tenant/{tenantId}/import/review

**Endpoint:** Delete all pending import review transactions without accepting any

**Test 22: Success - Delete all pending transactions**
```gherkin
Scenario: Delete review queue as editor returns no content
    Given user has Editor role for tenant
    And 5 transactions in review state
    When user deletes entire review queue
    Then 204 No Content should be returned
    And review queue should be empty
```

**Test 23: Authorization - Viewer forbidden**
```gherkin
Scenario: Delete review queue as viewer returns forbidden
    Given user has Viewer role for tenant (read-only)
    And transactions in review state
    When viewer attempts to delete review queue
    Then 403 Forbidden should be returned
```

**Test 24: Idempotent - Delete empty queue succeeds**
```gherkin
Scenario: Delete review queue empty queue returns no content
    Given user has Editor role for tenant
    And no transactions in review state
    When user deletes empty review queue
    Then 204 No Content should be returned (idempotent)
```

### Test Group 5: POST /api/tenant/{tenantId}/import/review/set-selection

**Endpoint:** Set selection state for specific transaction(s)

**Test 28: Success - Set selection for single transaction**
```gherkin
Scenario: Set selection single transaction returns no content
    Given user has Editor role for tenant
    And 10 transactions in review state
    And transaction with key ABC is currently selected
    When user deselects transaction ABC via set-selection endpoint
    Then 204 No Content should be returned
    And transaction ABC should be deselected in database
```

**Test 29: Success - Set selection for multiple transactions**
```gherkin
Scenario: Set selection multiple transactions returns no content
    Given user has Editor role for tenant
    And 10 transactions in review state
    And 3 specific transactions are currently deselected
    When user selects those 3 transactions via set-selection endpoint
    Then 204 No Content should be returned
    And all 3 transactions should be selected in database
```

**Test 30: Validation - Empty key list**
```gherkin
Scenario: Set selection empty key list returns bad request
    Given user has Editor role for tenant
    And transactions in review state
    When user sends set-selection request with empty key array
    Then 400 Bad Request should be returned
    And error should indicate at least one key required
```

### Test Group 6: POST /api/tenant/{tenantId}/import/review/select-all

**Endpoint:** Select all pending import review transactions

**Test 31: Success - Select all transactions**
```gherkin
Scenario: Select all transactions returns no content
    Given user has Editor role for tenant
    And 100 transactions in review state
    And 60 transactions are currently selected
    When user calls select-all endpoint
    Then 204 No Content should be returned
    And all 100 transactions should be selected in database
```

### Test Group 7: POST /api/tenant/{tenantId}/import/review/deselect-all

**Endpoint:** Deselect all pending import review transactions

**Test 32: Success - Deselect all transactions**
```gherkin
Scenario: Deselect all transactions returns no content
    Given user has Editor role for tenant
    And 100 transactions in review state
    And 60 transactions are currently selected
    When user calls deselect-all endpoint
    Then 204 No Content should be returned
    And all 100 transactions should be deselected in database
```

### Test Group 8: GET /api/tenant/{tenantId}/import/review/summary

**Endpoint:** Get summary statistics for pending import review transactions

**Test 33: Success - Get summary with mixed data**
```gherkin
Scenario: Get summary returns statistics
    Given user has Editor role for tenant
    And 100 total transactions in review state
    And 60 are New (50 selected, 10 deselected)
    And 30 are ExactDuplicate (all deselected)
    And 10 are PotentialDuplicate (5 selected, 5 deselected)
    When user requests summary
    Then 200 OK should be returned
    And response should contain TotalCount: 100
    And response should contain SelectedCount: 55
    And response should contain NewCount: 60
    And response should contain ExactDuplicateCount: 30
    And response should contain PotentialDuplicateCount: 10
```

### Additional Integration Test Scenarios

**Test 34: Multiple uploads merge into single review queue**
```gherkin
Scenario: Upload multiple files merges into single review queue
    Given user has Editor role for tenant
    And user uploads first OFX file with 3 transactions
    When user uploads second OFX file with 2 transactions
    Then review queue should contain 5 total transactions
    And both uploads should be merged into single review session
```

**Test 35: Transactions in review not included in transaction list**
```gherkin
Scenario: Get transactions excludes review state transactions
    Given user has Editor role for tenant
    And user has 10 accepted transactions in main table
    And user has 5 pending import review transactions
    When user requests transaction list via GET /api/tenant/{tenantId}/transactions
    Then should return only 10 accepted transactions
    And pending import review transactions should not be included
```

**Test 36: Pagination for large import review lists**
```gherkin
Scenario: Get import review large import supports pagination
    Given user has Editor role for tenant
    And user has 500 pending import review transactions
    When user requests first page with 50 items per page
    Then should return 50 transactions
    And response should include pagination metadata (total count, page number, page size)
```

## Functional Tests (12 tests)

**Location:** [`tests/Functional/`](../../../tests/Functional/)

**Technology:** Playwright + Gherkin (manually converted to C# test files)

**Purpose:** Validate complete end-to-end user workflows through the browser, testing the entire stack from UI to database.

**Note:** Gherkin feature files are written in [`tests/Functional/Features/`](../../../tests/Functional/Features/) and then manually converted to C# test classes in [`tests/Functional/Tests/`](../../../tests/Functional/Tests/) following the instructions in [`tests/Functional/INSTRUCTIONS.md`](../../../tests/Functional/INSTRUCTIONS.md).

**Why minimal functional testing?** Most import workflow behavior can be verified via API integration tests. Functional tests focus on UI-specific requirements and critical user paths.

### Gherkin Feature File

**File:** [`tests/Functional/Features/BankImport.feature`](../../../tests/Functional/Features/BankImport.feature) (new file)

```gherkin
@import
Feature: Bank Import
  As a YoFi user
  I want to import transactions from my bank's OFX/QFX files
  So that I can avoid manual data entry and review transactions before accepting

  Background:
    Given I have an existing account
    And I am logged in
    And I have an active workspace "My Finances"

  Rule: File Upload and Import Workflow

  Scenario: User uploads bank file and sees import review page
    Given I am on the transactions page
    When I click "Import from Bank" button
    And I upload OFX file "checking-jan-2024.ofx"
    Then I should be redirected to "Import Review" page
    And page should display 15 transactions
    And 12 transactions should be selected by default
    And 3 transactions should be deselected by default

  Scenario: User accepts selected transactions from import review
    Given I am on the import review page
    And page displays 15 transactions
    And 12 transactions are selected by default
    When I click "Accept Selected Transactions" button
    Then I should be redirected to transactions page
    And I should see 12 new transactions in the transaction list
    And import review queue should be completely cleared

  Scenario: User uploads bank file with duplicates and sees marked potential duplicates
    Given I have existing transactions from January 1-15
    And one existing transaction has been modified (payee changed from original import)
    When I navigate to import review page
    And I upload bank file with overlapping dates "checking-jan-15-31.ofx"
    Then page should display 23 total transactions
    And 8 transactions should be selected by default
    And 15 transactions should be deselected by default
    And 1 transaction should be visually marked as "Potential Duplicate" (the modified one)

  Scenario: User accepts selected transactions and rejects duplicates
    Given I am on the import review page
    And page displays 23 transactions
    And 8 transactions are selected by default
    And 15 transactions are deselected by default
    When I click "Accept Selected" button
    Then I should be redirected to transactions page
    And 8 transactions should be added to transaction list
    And import review queue should be completely cleared

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

  Scenario: User deletes entire import review queue
    Given I am on the import review page
    And page shows 15 pending transactions
    When I click "Delete All" button
    Then page should show "No pending imports"
    And import review queue should be empty

  Scenario: User toggles transaction selection in import review
    Given I am on the import review page
    And page shows 15 transactions
    And 12 transactions are selected by default
    When I deselect 2 selected transactions
    And I select 1 deselected transaction
    Then 11 transactions should be selected
    And selection state should persist when I navigate away and return

  Scenario: User selects all transactions in import review
    Given I am on the import review page
    And page shows 15 transactions
    And 12 transactions are selected by default
    When I click "Select All" button
    Then all 15 transactions should be selected

  Scenario: User deselects all transactions in import review
    Given I am on the import review page
    And page shows 15 transactions
    And 12 transactions are selected by default
    When I click "Deselect All" button
    Then 0 transactions should be selected

  Rule: Authorization

  Scenario: Viewer cannot access import page
    Given I am logged in as a user with Viewer role for workspace
    When I attempt to navigate to import review page
    Then I should see error message "You do not have permission to import into this workspace"
    And import UI elements should be disabled or hidden

  Rule: Error Handling

  Scenario: User uploads corrupted file and sees error message
    Given I am on the import review page
    When I upload corrupted OFX file "invalid.ofx"
    Then I should see error message "File appears corrupted - unable to parse transaction data"
    And no transactions should be added to review queue

  Scenario: User uploads unsupported file format
    Given I am on the import review page
    When I upload CSV file "transactions.csv"
    Then I should see error message "Unsupported file format - expected OFX or QFX"
    And no transactions should be added to review queue
```

### Functional Test Priority Ranking

**Priority-ordered implementation plan** - If implementing functional tests incrementally, build them in this order based on business value and risk coverage:

**Priority 1 (Must Have):** Core happy path workflow
- ✅ **"User uploads bank file and sees import review page"** - Validates the entire upload flow and transaction display. This is the foundational workflow that must work for the feature to be usable.

**Priority 2 (Must Have):** Complete the accept workflow
- ✅ **"User accepts selected transactions from import review"** - Completes the upload → review → accept workflow. Without this, imports cannot be finalized.

**Priority 3 (Must Have):** Authorization enforcement
- ✅ **"Viewer cannot access import page"** - Validates that read-only users are properly blocked from import functionality. Critical security requirement.

**Priority 4 (Should Have):** Duplicate detection UI verification
- ✅ **"User uploads bank file with duplicates and sees marked potential duplicates"** - Validates that duplicate detection works correctly and potential duplicates are visually marked in the UI.

**Priority 5 (Should Have):** Accept workflow with mixed selection
- ✅ **"User accepts selected transactions and rejects duplicates"** - Validates that default selection behavior works (new selected, duplicates deselected) and users can complete imports with mixed selections.

**Priority 6 (Should Have):** Selection state management
- ✅ **"User toggles transaction selection in import review"** - Validates that users can change selection state for individual transactions and state persists.

**Priority 7 (Should Have):** Select all operation
- ✅ **"User selects all transactions in import review"** - Validates select-all functionality.

**Priority 8 (Should Have):** Deselect all operation
- ✅ **"User deselects all transactions in import review"** - Validates deselect-all functionality.

**Priority 9 (Should Have):** Error handling for corrupted files
- ✅ **"User uploads corrupted file and sees error message"** - Validates error handling for the most likely user error (corrupted/damaged files).

**Priority 10 (Should Have):** Error handling for unsupported formats
- ✅ **"User uploads unsupported file format"** - Validates error handling for wrong file types, preventing user confusion.

**Priority 11 (Nice to Have):** State persistence verification
- ✅ **"User returns to pending import review after session interruption"** - Important for user experience but not essential for basic functionality.

**Priority 12 (Nice to Have):** Queue management
- ✅ **"User deletes entire import review queue"** - Utility function for cleanup but not essential for basic workflow.

**Minimal Viable Functional Tests:** Priorities 1-3 (upload, accept, and authorization)

**Recommended Functional Test Suite:** Priorities 1-10 (covers core workflows, authorization, duplicate detection, selection management, and error handling)

**Complete Functional Test Suite:** All 12 scenarios (comprehensive coverage including state persistence and utility functions)

### Page Object Model

**File:** [`tests/Functional/Pages/ImportPage.cs`](../../../tests/Functional/Pages/ImportPage.cs) (new file)

**Structure:**
```csharp
public class ImportPage : BasePage
{
    #region Locators
    private ILocator UploadButton => Page.Locator("[data-testid='upload-button']");
    private ILocator FileInput => Page.Locator("input[type='file']");
    private ILocator AcceptButton => Page.Locator("[data-testid='accept-button']");
    private ILocator DeleteAllButton => Page.Locator("[data-testid='delete-all-button']");
    private ILocator NewTransactionsSection => Page.Locator("[data-testid='new-transactions']");
    private ILocator ExactDuplicatesSection => Page.Locator("[data-testid='exact-duplicates']");
    private ILocator PotentialDuplicatesSection => Page.Locator("[data-testid='potential-duplicates']");
    #endregion

    #region Actions
    public async Task NavigateAsync() => await Page.GotoAsync("/import");
    public async Task UploadFileAsync(string filePath);
    public async Task ClickAcceptSelectedAsync();
    public async Task ClickDeleteAllAsync();
    public async Task ExpandSectionAsync(string sectionName);
    #endregion

    #region Assertions
    public async Task<int> GetNewTransactionCountAsync();
    public async Task<int> GetExactDuplicateCountAsync();
    public async Task<int> GetPotentialDuplicateCountAsync();
    public async Task<bool> IsTransactionSelectedAsync(Guid key);
    public async Task<string> GetErrorMessageAsync();
    #endregion
}
```

### Step Definitions

**File:** [`tests/Functional/Steps/BankImportSteps.cs`](../../../tests/Functional/Steps/BankImportSteps.cs) (new file)

**Structure:**
```csharp
public class BankImportSteps : CommonThenSteps
{
    private ImportPage _importPage;

    [Given(@"I have existing transactions from (.*)")]
    public async Task GivenIHaveExistingTransactionsFrom(string dateRange)
    {
        // Seed transactions via API for the specified date range
    }

    [Given(@"I have (.*) pending import review transactions")]
    public async Task GivenIHavePendingImportReviewTransactions(int count)
    {
        // Seed the specified number of import review transactions via API
    }

    [When(@"I click ""(.*)"" button")]
    public async Task WhenIClickButton(string buttonName)
    {
        // Click the specified button on the import page
    }

    [When(@"I upload OFX file ""(.*)""")]
    public async Task WhenIUploadOFXFile(string fileName)
    {
        // Upload the specified OFX file from the SampleData/Ofx directory
    }

    [Then(@"page should show ""(.*) New Transactions""")]
    public async Task ThenPageShouldShowNewTransactions(int count)
    {
        // Verify the page displays the expected count of new transactions
    }

    [Then(@"(.*) transactions should be added to transaction list")]
    public async Task ThenTransactionsShouldBeAdded(int count)
    {
        // Verify the specified number of transactions were added to the transaction list
    }
}
```

### Test Data Management

**Sample OFX Files for Functional Tests:**

**Location:** [`tests/Functional/SampleData/Ofx/`](../../../tests/Functional/SampleData/Ofx/)

**New files to create for functional tests:**
- `checking-jan-2024.ofx` - Standard checking account with 15 transactions (12 new, 3 duplicates) for upload and review scenarios
- `checking-jan-15-31.ofx` - Overlapping date range for duplicate detection testing (23 total transactions: 8 new, 15 duplicates)
- `invalid.ofx` - Corrupted file for error handling tests
- `transactions.csv` - Wrong file format (CSV instead of OFX) for validation error testing

**Existing files from unit tests** (can be reused if appropriate):

**Location:** [`tests/Unit/SampleData/Ofx/`](../../../tests/Unit/SampleData/Ofx/)

- `bank-banking-xml.ofx` - OFX 2.x XML format
- `Bank1.ofx` - Standard checking account
- `CC2.OFX` - Credit card transactions
- `creditcard.ofx` - Credit card with multiple transactions
- `issue-17.ofx` - Edge case for specific parsing issue
- `itau.ofx` - International bank format

**Seeding Strategy:**
- Use Test Control API endpoints to seed existing transactions for duplicate detection scenarios
- Use API to seed pending import review transactions for "return later" scenarios

## Testing Checklist

Comprehensive list of all tests to implement across all layers.

### Unit Tests (7 tests)
- [x] **Reference existing OFXParsingService tests** - Already implemented
- [ ] Import with new transaction (no duplicates) - DuplicateStatus.New
- [ ] Import with exact duplicate (FITID + data match) - DuplicateStatus.ExactDuplicate
- [ ] Import with potential duplicate (FITID match, different data) - DuplicateStatus.PotentialDuplicate
- [ ] Import with field-level duplicate (no FITID, same Date+Amount+Payee) - DuplicateStatus.PotentialDuplicate
- [ ] Import duplicate of pending import transaction - Prevents double import
- [ ] Import with missing amount field - Returns error
- [ ] Import with missing date field - Returns error

### Integration Tests - Data Layer (5 tests)
- [x] Create import review transaction with tenant isolation
- [x] Query filters by TenantId (isolation verification)
- [x] Cascade delete when tenant is deleted
- [x] Index performance on (TenantId, ExternalId)
- [x] DuplicateStatus enum storage and retrieval

### Integration Tests - Controller Layer (36 tests)

**Upload Endpoint (8 tests):**
- [ ] Upload valid OFX file - Returns 200 OK
- [ ] Upload valid QFX file - Returns 200 OK
- [ ] Upload corrupted file - Returns 400 Bad Request
- [ ] Upload unsupported format (CSV) - Returns 400 Bad Request
- [ ] Upload as Viewer - Returns 403 Forbidden
- [ ] Upload unauthenticated - Returns 401 Unauthorized
- [ ] Upload to different tenant - Returns 403 Forbidden
- [ ] Upload with partial failures - Returns 200 with partial success

**Get Review Endpoint (8 tests):**
- [ ] Get pending transactions - Returns 200 OK with transactions
- [ ] Get when empty - Returns 200 OK with empty list
- [ ] Get persists across sessions - Returns same data
- [ ] Get as Viewer - Returns 403 Forbidden
- [ ] Get with different tenant - Returns empty (isolation)
- [ ] Get with pageNumber < 1 - Defaults to page 1
- [ ] Get with pageSize < 1 - Defaults to pageSize 50
- [ ] Get with pageSize > 1000 - Clamped to pageSize 1000

**Complete Review Endpoint (5 tests):**
- [ ] Complete review with selected transactions - Returns 200 OK, accepts selected and deletes all
- [ ] Complete review as Viewer - Returns 403 Forbidden
- [ ] Complete review with empty selection - Returns 200 OK with 0 accepted (no error)
- [ ] Complete review with no transactions in queue - Returns 200 OK with 0 accepted
- [ ] Complete review for different tenant - Returns 403 Forbidden (isolation)

**Set Selection Endpoint (3 tests):**
- [ ] Set selection for single transaction - Returns 204 No Content
- [ ] Set selection for multiple transactions - Returns 204 No Content
- [ ] Set selection with empty key list - Returns 400 Bad Request

**Select All Endpoint (1 test):**
- [ ] Select all transactions - Returns 204 No Content

**Deselect All Endpoint (1 test):**
- [ ] Deselect all transactions - Returns 204 No Content

**Get Summary Endpoint (1 test):**
- [ ] Get summary with mixed data - Returns statistics with all counts

**Delete Endpoint (3 tests):**
- [ ] Delete all pending transactions - Returns 204 No Content
- [ ] Delete as Viewer - Returns 403 Forbidden
- [ ] Delete empty queue - Returns 204 No Content (idempotent)

**Additional Scenarios (3 tests):**
- [ ] Multiple uploads merge into single review queue
- [ ] Transactions in review excluded from transaction list
- [ ] Pagination for large import review lists

### Functional Tests (12 tests)
- [ ] Upload → Review page - Upload and display workflow
- [ ] Accept selected transactions - Complete workflow
- [ ] Viewer cannot access import page - Authorization enforcement
- [ ] Upload with duplicates → See marked potential duplicates - Duplicate detection UI
- [ ] Accept selected and reject duplicates - Selection behavior
- [ ] Toggle transaction selection - Individual selection state management
- [ ] Select all transactions - Bulk select operation
- [ ] Deselect all transactions - Bulk deselect operation
- [ ] Upload corrupted file - Error handling
- [ ] Upload unsupported format - Validation
- [ ] Return to pending import after session interruption - State persistence
- [ ] Delete entire review queue - Queue management

## Test Execution Strategy

### Running Tests Locally

**Testing Workflow (Narrow → Broad):**

When implementing tests, always run in this order:
1. **Specific test** you're working on (fast feedback loop)
2. **Entire test class** containing the test you're working on
3. **Entire test layer** (unit, integration.data, or integration.controller)
4. **Full test suite** (all unit + integration tests)

**1. Run Specific Tests (While Developing):**

```powershell
# Unit tests - specific test class
dotnet test tests/Unit --filter "FullyQualifiedName~ImportReviewFeatureTests"

# Integration.Data - specific test class
dotnet test tests/Integration.Data --filter "FullyQualifiedName~ImportReviewTransactionTests"

# Integration.Controller - specific test class
dotnet test tests/Integration.Controller --filter "FullyQualifiedName~ImportControllerTests"

# Functional - specific scenario
dotnet test tests/Functional --filter "DisplayName~UserUploadsBank"
```

**2. Run Entire Test Layer:**

```powershell
# All unit tests
dotnet test tests/Unit

# All data integration tests
dotnet test tests/Integration.Data

# All controller integration tests
dotnet test tests/Integration.Controller

# All functional tests (requires local dev server running)
.\scripts\Start-LocalDev.ps1  # In separate terminal
dotnet test tests/Functional
```

**3. Run Full Test Suite:**

```powershell
# All unit and integration tests (recommended for commits)
pwsh -File ./scripts/Run-Tests.ps1

# Functional tests against container (for final verification)
pwsh -File ./scripts/Run-FunctionalTestsVsContainer.ps1
```

**Note:** Always use `./scripts/Run-Tests.ps1` for full unit+integration suite per [`docs/wip/IMPLEMENTATION-WORKFLOW.md`](../../IMPLEMENTATION-WORKFLOW.md). This script excludes functional tests which require special setup.

### CI/CD Pipeline

**Stage 1 - Fast Feedback (Unit + Integration):**
- Run on every commit
- Must pass before PR merge
- Target: < 2 minutes total execution

**Stage 2 - E2E Validation (Functional):**
- Run on PR merge to main
- Must pass before deployment
- Target: < 5 minutes total execution

### Test Coverage Goals

- **Unit Tests:** 100% coverage of duplicate detection logic and parsing edge cases
- **Integration Tests:** 100% coverage of all API endpoints with auth variants
- **Functional Tests:** 80%+ coverage of critical user workflows

## References

**Design Documents:**
- [`DESIGN-BANK-IMPORT.md`](DESIGN-BANK-IMPORT.md) - Overall feature architecture
- [`DESIGN-BANK-IMPORT-DATABASE.md`](DESIGN-BANK-IMPORT-DATABASE.md) - Entity model and schema
- [`DESIGN-BANK-IMPORT-APPLICATION.md`](DESIGN-BANK-IMPORT-APPLICATION.md) - Business logic and DTOs
- [`DESIGN-BANK-IMPORT-API.md`](DESIGN-BANK-IMPORT-API.md) - Controller endpoints and contracts
- [`DESIGN-BANK-IMPORT-FRONTEND.md`](DESIGN-BANK-IMPORT-FRONTEND.md) - Vue pages and components

**Requirements:**
- [`PRD-BANK-IMPORT.md`](PRD-BANK-IMPORT.md) - Product requirements and user stories

**Testing Strategy:**
- [`docs/TESTING-STRATEGY.md`](../../TESTING-STRATEGY.md) - Project-wide testing approach
- [`docs/wip/import-export/PRD-BANK-IMPORT.md`](PRD-BANK-IMPORT.md) - 34 acceptance criteria to verify
- [`tests/Integration.Controller/TESTING-GUIDE.md`](../../../tests/Integration.Controller/TESTING-GUIDE.md) - Controller testing patterns
- [`tests/Functional/README.md`](../../../tests/Functional/README.md) - Functional testing architecture

**Existing Test Infrastructure:**
- [`tests/Unit/OFXParsingServiceTests.cs`](../../../tests/Unit/OFXParsingServiceTests.cs) - OFX parsing tests (already exist)
- [`tests/Unit/SampleData/Ofx/`](../../../tests/Unit/SampleData/Ofx/) - Sample OFX files for testing
- [`tests/Integration.Controller/TestHelpers/AuthenticatedTestBase.cs`](../../../tests/Integration.Controller/TestHelpers/AuthenticatedTestBase.cs) - Base class for controller tests
- [`tests/Functional/Infrastructure/FunctionalTestBase.cs`](../../../tests/Functional/Infrastructure/FunctionalTestBase.cs) - Base class for functional tests
