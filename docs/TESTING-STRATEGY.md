---
status: in review
---
# Testing Strategy

**The definitive guide for mapping PRD acceptance criteria to test layers.**

## Introduction

This document provides a comprehensive testing strategy for the YoFi.V3 project. It defines how to map Product Requirements Document (PRD) acceptance criteria to the appropriate test layer, ensuring systematic coverage across our testing pyramid.

**Purpose:** Guide developers in selecting the correct test layer for each type of acceptance criterion, maximizing test effectiveness while maintaining optimal execution speed and maintainability.

**Scope:** This strategy covers all acceptance criteria from PRDs, spanning functional requirements, API contracts, data integrity, and business logic validation.

## The Testing Pyramid

Our testing strategy follows the testing pyramid model with a target distribution of:

```
                    ▲
                   ╱ ╲
                  ╱   ╲
                 ╱     ╲
                ╱  E2E  ╲              15% - Functional Tests (Playwright/BDD)
               ╱ 15%     ╲             End-to-end user workflows
              ╱___________╲            Slowest, most comprehensive
             ╱             ╲
            ╱               ╲
           ╱   Integration   ╲        25% - Integration Tests
          ╱      25%          ╲       Controller + Data layer testing
         ╱                     ╲      Moderate speed, focused scenarios
        ╱_______________________╲
       ╱                         ╲
      ╱                           ╲
     ╱          Unit Tests         ╲  60% - Unit Tests
    ╱             60%                ╲ Application layer logic
   ╱_______________________________  ╲ Fast, isolated, comprehensive
```

**Key Principles:**
- **Fast feedback** - Majority of tests run in milliseconds (unit tests)
- **Targeted coverage** - Each layer tests what it does best
- **Avoid duplication** - Don't test the same thing at multiple layers
- **Pyramid shape** - More tests at lower, faster layers

## Overview of Test Layers

### 1. Functional Tests (Playwright/BDD)

**Location:** [`tests/Functional/`](tests/Functional/)

**Technology:** Playwright + SpecFlow (Gherkin scenarios)

**Purpose:** Validate complete user workflows through the browser, testing the entire stack from UI to database.

**Characteristics:**
- End-to-end scenarios written in Gherkin (Given/When/Then)
- Executes against a running application (container or local)
- Tests user-visible behavior and UI interactions
- Slowest execution (seconds per test)
- Most comprehensive coverage (frontend + backend + database)

**When to Use:**
- Critical user workflows (login, registration, core features)
- Cross-cutting concerns that span multiple layers
- UI-dependent functionality
- User acceptance criteria that describe workflows

### 2. Integration.Controller Tests

**Location:** [`tests/Integration.Controller/`](tests/Integration.Controller/)

**Technology:** NUnit + ASP.NET Core WebApplicationFactory

**Purpose:** Test API endpoints in isolation, verifying HTTP contracts, authentication, authorization, and controller logic.

**Characteristics:**
- Tests the full HTTP request/response cycle
- Uses in-memory database (isolated, fast)
- Validates authentication/authorization policies
- Tests HTTP status codes, response formats, and error handling
- Faster than functional tests (milliseconds to seconds)

**When to Use:**
- API contract validation (endpoints, status codes, response shapes)
- Authentication/authorization requirements
- HTTP-specific behavior (headers, content negotiation)
- Controller-level error handling

### 3. Integration.Data Tests

**Location:** [`tests/Integration.Data/`](tests/Integration.Data/)

**Technology:** NUnit + Entity Framework Core (in-memory database)

**Purpose:** Verify data access layer logic, repository operations, and Entity Framework configurations.

**Characteristics:**
- Tests repository methods against in-memory database
- Validates Entity Framework mappings and relationships
- Tests complex queries and data operations
- Fast execution (milliseconds)

**When to Use:**
- Data access patterns (queries, filters, relationships)
- Entity Framework configurations (indexes, constraints, relationships)
- Repository method behavior
- Data integrity requirements

### 4. Unit Tests (Application Layer)

**Location:** [`tests/Unit/`](tests/Unit/)

**Technology:** NUnit + Moq

**Purpose:** Test business logic in isolation, validating Application Features, DTOs, validation rules, and domain logic.

**Characteristics:**
- Pure logic testing with mocked dependencies
- No database, no HTTP, no external services
- Fastest execution (milliseconds)
- Highest coverage density (60% of all tests)

**When to Use:**
- Business logic and validation rules
- Application Feature methods
- DTO transformations and mappings
- Domain calculations and algorithms
- Error handling and edge cases

## Current Status

**Scope:** ~288 acceptance criteria across 8 Product Requirements Documents (PRDs)

**PRDs Requiring Test Coverage:**
1. Authentication & User Management
2. Workspace Tenancy & Collaboration
3. Transactions Management
4. Reports & Analytics
5. Data Import/Export
6. Budgets & Categories
7. Payee Management
8. System Administration

**Goal:** Map each acceptance criterion to the appropriate test layer(s) and ensure comprehensive coverage across all PRDs.

**Next Steps:**
- ~~Define decision framework for test layer selection~~
- Provide detailed examples for each test layer
- Create comprehensive mapping of existing acceptance criteria
- Track coverage metrics per PRD

## Decision Framework: Which Test Layer?

Use this framework to quickly determine the appropriate test layer for each acceptance criterion.

### Decision Flowchart

```
┌─────────────────────────────────────────────────────────────┐
│  START: Analyzing Acceptance Criterion                      │
└───────────────────────┬─────────────────────────────────────┘
                        │
                        ▼
        ┌───────────────────────────────────┐
        │ Does it involve browser/UI        │
        │ interaction or visual behavior?   │
        └───────────┬───────────────────────┘
                    │
        ┌───────────┴───────────┐
        │ YES                   │ NO
        ▼                       ▼
┌───────────────────┐   ┌───────────────────────────────────┐
│ ✅ FUNCTIONAL     │   │ Does it test HTTP API contract,   │
│ (only if critical │   │ authorization, or request/response │
│ workflow)         │   │ behavior?                          │
│                   │   └───────────┬───────────────────────┘
│ Examples:         │               │
│ • User login flow │   ┌───────────┴───────────┐
│ • Registration    │   │ YES                   │ NO
│ • Core workflows  │   ▼                       ▼
└───────────────────┘   ┌───────────────────┐   ┌───────────────────────────────┐
                        │ ✅ CONTROLLER     │   │ Is it pure business logic,    │
                        │ INTEGRATION       │   │ calculation, validation, or    │
                        │                   │   │ algorithm?                     │
                        │ 🎯 SWEET SPOT!    │   └───────────┬───────────────────┘
                        │                   │               │
                        │ Examples:         │   ┌───────────┴───────────┐
                        │ • GET /api/...    │   │ YES                   │ NO
                        │ • 401 on auth fail│   ▼                       ▼
                        │ • Response format │   ┌───────────────────┐   ┌───────────────────┐
                        │ • Status codes    │   │ ✅ UNIT TEST      │   │ ✅ INTEGRATION    │
                        └───────────────────┘   │                   │   │ (Data or Combined)│
                                                │ Examples:         │   │                   │
                                                │ • Validation rules│   │ Examples:         │
                                                │ • Calculations    │   │ • Complex queries │
                                                │ • DTO mapping     │   │ • EF relationships│
                                                │ • Domain logic    │   │ • Data integrity  │
                                                └───────────────────┘   └───────────────────┘
```

### Quick Reference Table

| Characteristic          | Functional          | Controller Integration ✅ | Unit               |
|------------------------|---------------------|---------------------------|-------------------|
| **Speed**              | Slow (seconds)      | Fast (~100-200ms)         | Fastest (<10ms)   |
| **Coverage**           | E2E workflows       | API contracts             | Business logic    |
| **Brittleness**        | High (UI changes)   | Low                       | Very Low          |
| **Setup Complexity**   | High                | Medium                    | Low               |
| **Best For**           | Critical paths      | Most acceptance criteria  | Algorithms        |
| **Dependencies**       | All (UI→DB)         | Controller→DB (in-memory) | None (mocked)     |
| **Debugging**          | Hard (black box)    | Moderate                  | Easy (isolated)   |
| **Maintenance**        | High effort         | Low effort                | Very low effort   |

### Key Principles

#### 🎯 Controller Integration Tests Are the Primary Layer

**60% of PRD acceptance criteria should map to Controller Integration tests.** This is where you get maximum value:

✅ **Why Controller Integration is the sweet spot:**
- Tests the complete API contract (request → response)
- Validates authentication and authorization
- Verifies HTTP status codes and error handling
- Includes database operations (in-memory, fast)
- Reflects real-world API usage
- Fast enough to run frequently (~100-200ms per test)
- Low maintenance (no UI coupling)

❌ **Don't default to Functional tests for everything:**
- Functional tests are slow and brittle
- UI changes break tests frequently
- Hard to debug failures
- High maintenance overhead
- Reserve for critical user workflows only (15% target)

#### Decision Rules Summary

1. **Browser/UI required?** → Functional (but only if critical)
2. **API endpoint testing?** → Controller Integration ✅ (default choice)
3. **Pure business logic?** → Unit
4. **Complex data queries?** → Integration.Data

#### Example Mappings

**Acceptance Criterion:** "User can retrieve their list of transactions via GET /api/tenant/{id}/transactions"
- ✅ **Controller Integration** - Tests HTTP endpoint, auth, response format
- ❌ Not Functional - No UI interaction needed
- ❌ Not Unit - Involves HTTP contract and database

**Acceptance Criterion:** "Transaction amount validation must reject values outside -$1M to +$1M"
- ✅ **Unit Test** - Pure validation logic
- ❌ Not Controller Integration - No HTTP context needed

**Acceptance Criterion:** "User must be able to log in with email and password via the login page"
- ✅ **Functional** - Critical workflow, UI-dependent
- ✅ **Controller Integration** - Also test the underlying API endpoint
- Dual coverage appropriate for critical paths

**Acceptance Criterion:** "API returns 401 Unauthorized when token is missing"
- ✅ **Controller Integration** - HTTP-specific authorization behavior
- ❌ Not Functional - No UI needed
- ❌ Not Unit - Requires HTTP context

## Detailed Example: PRD-TRANSACTION-SPLITS

This section demonstrates how to map the 33 acceptance criteria from [PRD-TRANSACTION-SPLITS](wip/transactions/PRD-TRANSACTION-SPLITS.md) to tests across all three layers, showing the 60/25/15 distribution in practice.

### Overview

**Feature:** Transaction Splits - Enable transactions to be split across multiple categories with individual amounts.

**Acceptance Criteria Distribution:**
- 6 User Stories
- 33 Total Acceptance Criteria
- Touches all three main test layers

This is an ideal example because it demonstrates:
- How most criteria map to Controller Integration tests (API contracts)
- When to use Functional tests (critical workflows only)
- When to use Unit tests (pure business logic)

### Test Distribution Breakdown

| Test Type | Count | Percentage | Coverage Focus |
|-----------|-------|------------|----------------|
| **Functional** | 3-5 | 9-15% | Critical user workflows requiring UI |
| **Controller Integration** | 20-25 | 60-76% | API contracts, authorization, data operations |
| **Unit** | 5-8 | 15-24% | Business logic, validation, calculations |

**Total estimated tests:** 28-38 tests for 33 acceptance criteria (some criteria require multiple tests, some tests cover multiple criteria).

### Specific Test Mappings

#### Functional Tests (3-5 tests)

These tests validate complete user workflows through the browser. They're the slowest and most brittle, so we only use them for critical paths.

**Test 1: Create transaction with multiple splits**
- **Maps to:** Story 1, AC: "User can add multiple splits to an existing transaction"
- **Why Functional:** Requires UI interaction - click "Add Split" button, fill form fields, see dynamic updates
- **Scenario:**
  ```gherkin
  Scenario: User splits grocery transaction across categories
    Given user is logged in and viewing transactions page
    When user creates a transaction with amount $75.00
    And user adds a split for "Food" with amount $50.00
    And user adds a split for "Home" with amount $25.00
    Then balance warning should not be shown
    And transaction detail should show 2 splits
  ```

**Test 2: Simple single-category workflow (UI hides complexity)**
- **Maps to:** Story 3, AC: "UI hides split complexity for transactions with only one split"
- **Why Functional:** Tests visual UI behavior - split UI elements should be hidden
- **Scenario:**
  ```gherkin
  Scenario: Creating simple transaction doesn't expose split complexity
    Given user is logged in and viewing transactions page
    When user creates a transaction with amount $20.00 and category "Food"
    Then split management UI should not be visible
    And transaction should show single category "Food" in list view
  ```

**Test 3: Upload splits from Excel file**
- **Maps to:** Story 6, AC: "User can upload a spreadsheet (Excel .xlsx) containing split data"
- **Why Functional:** Requires UI file upload control interaction, visual feedback
- **Scenario:**
  ```gherkin
  Scenario: User uploads paystub splits from Excel
    Given user is logged in and viewing transaction detail page
    And transaction has amount $5000.00
    When user uploads Excel file with 8 split rows (Salary, Taxes, Insurance, etc.)
    Then all 8 splits should appear in the split list
    And splits should sum to $5000.00
    And no balance warning should be shown
  ```

**Optional Tests (if time permits):**
- Test 4: Balance warning appears when splits don't sum to transaction amount (Story 4)
- Test 5: Download split template file (Story 6)

#### Controller Integration Tests (20-25 tests)

These tests validate API contracts using HTTP requests against the controller with in-memory database. This is the **sweet spot** for most acceptance criteria.

**Test Group 1: GET /api/tenant/{tenantId}/splits/{splitId} - Retrieve split by ID**

*Maps to: Story 1, AC: "Entire list of splits can be viewed from a transaction detail page"*

```csharp
[Test]
public async Task GetSplitById_AsViewer_ReturnsOK()
{
    // Given: User has Viewer role for tenant
    // And: A transaction with a split exists
    // When: User requests split by ID
    // Then: 200 OK should be returned
    // And: Response should contain split data
}

[Test]
public async Task GetSplitById_DifferentTenant_Returns404()
{
    // Given: User has Editor role for tenant A
    // And: Split belongs to tenant B
    // When: User attempts to access split from tenant B using tenant A context
    // Then: 404 Not Found should be returned (tenant isolation)
}
```

**Test Group 2: POST /api/tenant/{tenantId}/transactions/{txnId}/splits - Create split**

*Maps to: Story 1, AC: "User can add multiple splits to an existing transaction"*

```csharp
[Test]
public async Task CreateSplit_AsEditor_ReturnsCreated()
{
    // Given: User has Editor role for tenant
    // And: A transaction exists
    // And: Valid split data
    // When: User creates a split
    // Then: 201 Created should be returned
    // And: Response should contain the created split
    // And: Location header should point to the created resource
}

[Test]
public async Task CreateSplit_AsViewer_ReturnsForbidden()
{
    // Given: User has Viewer role for tenant (read-only)
    // And: A transaction exists
    // And: Valid split data
    // When: Viewer attempts to create a split
    // Then: 403 Forbidden should be returned
}

[Test]
public async Task CreateSplit_NegativeAmount_ReturnsCreated()
{
    // Given: User has Editor role for tenant
    // And: A transaction exists (e.g., paystub)
    // And: Split with negative amount (deduction)
    // When: User creates a split with negative amount
    // Then: 201 Created should be returned
    // And: Negative amount should be preserved
}
```

**Test Group 3: PUT /api/tenant/{tenantId}/splits/{splitId} - Update split**

*Maps to: Story 1, AC: "Splits can be edited individually (amount, category, memo)"*

```csharp
[Test]
public async Task UpdateSplit_AsEditor_ReturnsNoContent()
{
    // Given: User has Editor role for tenant
    // And: A transaction with a split exists
    // And: Updated split data
    // When: User updates the split
    // Then: 204 No Content should be returned
    // And: Split should be updated
}
```

**Test Group 4: DELETE /api/tenant/{tenantId}/splits/{splitId} - Delete split**

*Maps to: Story 1, AC: "Splits can be deleted (except the last one - transactions must have at least one split)"*

```csharp
[Test]
public async Task DeleteSplit_MultipleExist_ReturnsNoContent()
{
    // Given: User has Editor role for tenant
    // And: A transaction with 2 splits exists
    // When: User deletes one split
    // Then: 204 No Content should be returned
    // And: Split should no longer exist
}

[Test]
public async Task DeleteSplit_LastSplit_ReturnsBadRequest()
{
    // Given: User has Editor role for tenant
    // And: A transaction with only 1 split exists
    // When: User attempts to delete the last split
    // Then: 400 Bad Request should be returned
    // And: Error message should explain business rule
}
```

**Test Group 5: GET /api/tenant/{tenantId}/transactions - List with balance status**

*Maps to: Story 4, AC: "List view shows visual indicator for unbalanced transactions"*

```csharp
[Test]
public async Task GetTransactions_IncludesBalanceStatus()
{
    // Given: User has Viewer role for tenant
    // And: A balanced transaction exists
    // And: An unbalanced transaction exists
    // When: User requests transactions
    // Then: 200 OK should be returned
    // And: Response should include IsBalanced flag for each transaction
}
```

**Additional Controller Integration Tests (15-20 more):**

- POST /splits with invalid data (missing required fields, validation)
- POST /splits for non-existent transaction (404)
- PUT /splits authorization (Viewer forbidden, Editor/Owner allowed)
- PUT /splits non-existent (404)
- DELETE /splits authorization (Viewer forbidden)
- DELETE /splits non-existent (404)
- GET /transactions/{id} includes HasMultipleSplits flag
- GET /transactions/{id} includes SingleSplitCategory (when applicable)
- POST /transactions automatically creates single split (Story 3)
- PUT /transactions updates single split amount automatically (Story 3)
- POST /transactions/import creates single uncategorized split (Story 5)
- GET /transactions/import preserves transaction amount (Story 5)
- POST /transactions/{id}/splits/upload validates Excel format (Story 6)
- POST /transactions/{id}/splits/upload appends to existing splits (Story 6)
- POST /transactions/{id}/splits/upload validates required columns (Story 6)
- POST /transactions/{id}/splits/upload with invalid data returns 400 (Story 6)
- GET /transactions/{id}/splits/template downloads Excel template (Story 6)

#### Unit Tests (5-8 tests)

These tests validate pure business logic in isolation with mocked dependencies. They're the fastest and test algorithmic complexity.

**Test 1: Split amount validation logic**

*Maps to: Story 1, implicit validation requirements*

```csharp
[Test]
public void ValidateSplit_ValidAmount_ReturnsNoErrors()
{
    // Given: Valid split data
    // When: Split is validated
    // Then: Validation should pass
}

[Test]
public void ValidateSplit_AmountExceedsLimit_ReturnsError()
{
    // Given: Split with amount exceeding limit
    // When: Split is validated
    // Then: Validation should fail with amount error
}
```

**Test 2: Balance calculation algorithm**

*Maps to: Story 4, AC: "Detail view shows transaction amount, splits total, and balance status"*

```csharp
[Test]
public void CalculateBalance_SplitsMatchTransaction_ReturnsBalanced()
{
    // Given: Transaction amount
    // And: Splits that sum to transaction amount
    // When: Balance is calculated
    // Then: Transaction should be balanced
}

[Test]
public void CalculateBalance_SplitsDontMatch_ReturnsUnbalanced()
{
    // Given: Transaction amount
    // And: Splits that don't sum to transaction amount
    // When: Balance is calculated
    // Then: Transaction should be unbalanced
}

[Test]
public void CalculateBalance_WithNegativeSplits_CalculatesCorrectly()
{
    // Given: Transaction amount (paystub gross)
    // And: Splits with positive and negative amounts
    // When: Balance is calculated
    // Then: Should be unbalanced (splits sum to 4000, not 5000)
}
```

**Test 3: Excel parsing edge cases**

*Maps to: Story 6, AC: "Invalid input cancels entire import, with an error message"*

```csharp
[Test]
public void ParseExcelSplits_ValidFormat_ReturnsListOfSplits()
{
    // Given: Valid Excel file stream with 3 splits
    // When: Excel is parsed
    // Then: Should return 3 splits
}

[Test]
public void ParseExcelSplits_MissingRequiredColumn_ReturnsError()
{
    // Given: Excel file missing required "Amount" column
    // When: Excel is parsed
    // Then: Should return error
}

[Test]
public void ParseExcelSplits_ExtraColumns_IgnoresThemSuccessfully()
{
    // Given: Excel file with extra columns
    // When: Excel is parsed
    // Then: Should succeed and ignore extra columns
}
```

**Additional Unit Tests (2-5 more):**

- Category name normalization/cleanup logic (Story 6)
- Split ordering algorithm
- Edge cases: Empty category strings
- Edge cases: Very large split counts (performance)

### Key Insights

**Why this distribution makes sense for Transaction Splits:**

1. **Controller Integration is the sweet spot (60-76%):**
   - Most acceptance criteria are about API contracts ("User can add splits", "Splits can be edited", "Returns 404 when...")
   - Authorization requirements span all CRUD operations (Viewer vs Editor vs Owner)
   - Each HTTP endpoint needs multiple tests (success, authorization, validation, error cases)
   - Fast enough to test extensively without slowing down development

2. **Functional tests are minimal (9-15%):**
   - Only 3-5 tests for the most critical user workflows
   - UI-specific requirements: "UI hides split complexity", "Upload file from page"
   - Most split functionality can be verified via API without browser overhead
   - Avoids brittle UI tests that break when CSS classes change

3. **Unit tests target complexity (15-24%):**
   - Balance calculation algorithm has edge cases (negative amounts, floating point precision)
   - Excel parsing has multiple error conditions to test
   - Validation logic needs comprehensive coverage
   - These are the fastest tests and can be run thousands of times per day

**Coverage Distribution Reality:**
- A feature with 33 acceptance criteria doesn't need 33 tests
- Some tests cover multiple criteria (e.g., GET /transactions covers `IsBalanced`, `HasMultipleSplits`, and `SingleSplitCategory` flags)
- Some criteria need multiple tests (e.g., DELETE split needs "success" + "last split business rule" + "authorization" + "not found" cases)
- Total test count (28-38) provides comprehensive coverage while maintaining fast execution

## Detailed Example 2: PRD-TRANSACTION-ATTACHMENTS (Receipt Matching)

This example demonstrates how a feature with complex matching algorithms shifts toward more unit tests while maintaining appropriate integration and functional coverage.

### Overview

**Feature:** Transaction Attachments - Upload receipt images and match them to transactions using intelligent date/payee/amount matching.

**Acceptance Criteria Distribution:**
- 4 User Stories
- 38 Total Acceptance Criteria
- Algorithm-heavy feature with complex business logic

This example demonstrates:
- When to significantly increase unit test coverage (complex algorithms)
- How integration tests validate file upload and API contracts
- When functional tests focus on workflow rather than computation

### Test Distribution Breakdown

| Test Type | Count | Percentage | Coverage Focus |
|-----------|-------|------------|----------------|
| **Unit** | 15-18 | 40% | Matching algorithm, confidence scoring, parsing |
| **Controller Integration** | 15-20 | 45% | Upload/download API, attachment operations |
| **Functional** | 3-5 | 15% | Upload → Match → Attached workflow |

**Total estimated tests:** 33-43 tests for 38 acceptance criteria.

**Why more unit tests?** The receipt matching algorithm has significant computational complexity with multiple edge cases that are best tested in isolation at maximum speed.

### Specific Test Mappings

#### Unit Tests (15-18 tests) - The Matching Algorithm

The matching algorithm is the heart of this feature and requires extensive unit test coverage for all its edge cases.

**Test Group 1: Perfect high-confidence matches**

```csharp
[Test]
public void MatchReceipt_ExactDatePayeeAmount_ReturnsHighConfidence()
{
    // Given: Receipt with date, payee, and amount parsed from filename
    // And: Transaction with exact matching data
    // When: Matching algorithm runs
    // Then: Should return high confidence match
}
```

**Test Group 2: Medium confidence with date tolerance**

```csharp
[Test]
public void MatchReceipt_DateWithin7Days_ReturnsMediumConfidence()
{
    // Given: Receipt dated January 15
    // And: Transaction dated January 20 (5 days later)
    // When: Matching algorithm runs
    // Then: Should return medium confidence match (within ±7 day window)
}

[Test]
public void MatchReceipt_DateBeyond7Days_ReturnsNoMatch()
{
    // Given: Receipt dated January 15
    // And: Transaction dated January 30 (15 days later)
    // When: Matching algorithm runs
    // Then: Should return no match (outside ±7 day window)
}
```

**Test Group 3: Partial payee matching with substring**

```csharp
[Test]
public void MatchReceipt_PartialPayeeMatch_ReturnsReducedConfidence()
{
    // Given: Receipt with short payee name
    // And: Transaction with longer payee name
    // When: Matching algorithm runs
    // Then: Should match with slightly reduced confidence (substring match)
}
```

**Test Group 4: Amount tolerance (±10%)**

```csharp
[Test]
public void MatchReceipt_AmountWithin10Percent_ReturnsMatch()
{
    // Given: Receipt with amount $75.00
    // And: Transaction with amount $77.50 (3.3% higher - added tip)
    // When: Matching algorithm runs
    // Then: Should match with reduced confidence (within ±10% tolerance)
}

[Test]
public void MatchReceipt_AmountBeyond10Percent_ReturnsNoMatch()
{
    // Given: Receipt with amount $75.00
    // And: Transaction with amount $90.00 (20% higher - too different)
    // When: Matching algorithm runs
    // Then: Should not match (beyond ±10% tolerance)
}
```

**Test Group 5: Refund handling (negative amounts)**

```csharp
[Test]
public void MatchReceipt_RefundAmount_MatchesWithAbsoluteValue()
{
    // Given: Receipt showing refund with positive amount
    // And: Transaction showing refund (positive amount in our system)
    // When: Matching algorithm runs
    // Then: Should match using absolute value comparison
}
```

**Test Group 6: Multiple matches scenario**

```csharp
[Test]
public void MatchReceipt_MultipleMatches_ReturnsHighestConfidence()
{
    // Given: Receipt from Safeway
    // And: Multiple transactions from Safeway on similar dates
    // When: Matching algorithm runs
    // Then: Should return the exact match (highest confidence)
}
```

**Test Group 7: Year inference logic**

```csharp
[Test]
public void ParseReceiptFilename_NoYear_InfersCurrentYear()
{
    // Given: Receipt filename with month/day but no year
    // And: Current year is 2024
    // When: Filename is parsed
    // Then: Should infer current year
}

[Test]
public void ParseReceiptFilename_FutureDate_InfersPreviousYear()
{
    // Given: Receipt filename dated December 31
    // And: Current date is January 5, 2024
    // When: Filename is parsed
    // Then: Should infer previous year (2023, not 2024)
}
```

**Additional Unit Tests:**
- Missing amount in filename (confidence penalty)
- Missing payee in filename (confidence penalty)
- Invalid date formats (parsing failures)
- Edge case: Receipt with amount $0.00

#### Controller Integration Tests (15-20 tests)

**Test Group 1: POST /api/tenant/{id}/attachments/upload**

```csharp
[Test]
public async Task UploadAttachment_ValidImage_ReturnsCreated()
{
    // Given: User has Editor role for tenant
    // And: Valid image file (JPEG)
    // When: User uploads attachment
    // Then: 201 Created should be returned
    // And: Response should contain attachment metadata
}

[Test]
public async Task UploadAttachment_InvalidMimeType_ReturnsBadRequest()
{
    // Given: User has Editor role for tenant
    // And: Invalid file type (executable)
    // When: User attempts to upload non-image file
    // Then: 400 Bad Request should be returned
    // And: Error should mention MIME type validation
}

[Test]
public async Task UploadAttachment_ExceedsSizeLimit_ReturnsBadRequest()
{
    // Given: User has Editor role for tenant
    // And: Image exceeding size limit (e.g., 10 MB)
    // When: User attempts to upload oversized file
    // Then: 400 Bad Request should be returned
    // And: Error should mention size limit
}
```

**Test Group 2: GET /api/tenant/{id}/attachments/inbox**

```csharp
[Test]
public async Task GetInbox_UnmatchedReceiptsOnly_ReturnsOK()
{
    // Given: User has Viewer role for tenant
    // And: One unmatched attachment exists
    // And: One matched attachment exists (attached to transaction)
    // When: User requests inbox
    // Then: 200 OK should be returned
    // And: Response should contain only unmatched attachment
}
```

**Test Group 3: POST /api/tenant/{id}/attachments/{attachmentId}/match**

```csharp
[Test]
public async Task MatchAttachment_AsEditor_ReturnsNoContent()
{
    // Given: User has Editor role for tenant
    // And: Unmatched attachment and transaction exist
    // When: User matches attachment to transaction
    // Then: 204 No Content should be returned
    // And: Attachment should no longer appear in inbox
}

[Test]
public async Task MatchAttachment_RaceCondition_ReturnsConflict()
{
    // Given: Two users (both Editors) in same tenant
    // And: Unmatched attachment exists
    // When: User 1 matches the attachment
    // And: User 2 attempts to match same attachment simultaneously
    // Then: First request should succeed
    // And: Second request should return 409 Conflict (already matched)
}
```

**Test Group 4: DELETE /api/tenant/{id}/attachments/{attachmentId}**

```csharp
[Test]
public async Task DeleteAttachment_FromInbox_ReturnsNoContent()
{
    // Given: User has Editor role for tenant
    // And: Unmatched attachment exists in inbox
    // When: User deletes attachment
    // Then: 204 No Content should be returned
    // And: Attachment should no longer exist
}

[Test]
public async Task DeleteAttachment_FromTransaction_ReturnsNoContent()
{
    // Given: User has Editor role for tenant
    // And: Matched attachment exists (attached to transaction)
    // When: User deletes attachment from transaction
    // Then: 204 No Content should be returned
    // And: Transaction should no longer have attachment
}
```

**Additional Controller Integration Tests:**
- GET /attachments/{id} - Download attachment file
- GET /attachments/{id} - Authorization (Viewer can view, different tenant forbidden)
- POST /attachments/upload - As Viewer returns Forbidden
- DELETE /attachments/{id} - As Viewer returns Forbidden
- POST /attachments/{id}/match - Non-existent transaction returns 404
- POST /attachments/{id}/match - Non-existent attachment returns 404

#### Functional Tests (3-5 tests)

**Test 1: Upload receipt → inbox → Match button → attached**

```gherkin
Scenario: User uploads receipt and matches to transaction
  Given user is logged in and viewing transactions page
  And user has a transaction for "Safeway" with amount $75.43
  When user clicks "Upload Receipt" button
  And user selects file "Safeway-01-15-$75.43.jpg"
  And upload completes successfully
  Then receipt should appear in "Receipt Inbox" with suggested match
  When user clicks "Match" button on suggested transaction
  Then receipt should be attached to transaction
  And receipt should no longer appear in inbox
  And transaction detail should show receipt thumbnail
```

**Test 2: Upload receipt → inbox → Assign button → review page**

```gherkin
Scenario: User manually assigns receipt to different transaction
  Given user is logged in and has receipt in inbox
  And receipt has low confidence match (or no match)
  When user clicks "Assign to Transaction" button
  Then user should see transaction selection page
  When user searches for and selects target transaction
  And user clicks "Attach Receipt"
  Then receipt should be attached to selected transaction
  And receipt should no longer appear in inbox
```

**Test 3: Upload multiple receipts → bulk operations**

```gherkin
Scenario: User uploads multiple receipts and processes inbox
  Given user is logged in and viewing receipt inbox
  When user uploads 5 receipt images
  Then inbox should show 5 unmatched receipts
  And each receipt should show match confidence indicator
  When user clicks "Match All High Confidence" button
  Then receipts with high confidence matches should be attached
  And inbox should only show remaining unmatched receipts
```

**Optional Tests:**
- Test 4: Delete receipt from inbox
- Test 5: View receipt full-size in modal

### Key Insights

**Why this distribution makes sense for Transaction Attachments:**

1. **Unit tests dominate the algorithm (40%):**
   - Receipt matching has complex computational logic with many edge cases
   - Confidence scoring requires precise threshold testing
   - Filename parsing has multiple format variations
   - Year inference has business logic edge cases (future dates → previous year)
   - Testing algorithms in isolation is 1000x faster than integration tests
   - Unit tests enable rapid iteration on matching accuracy

2. **Controller Integration tests validate the API (45%):**
   - File upload requires multipart form data testing
   - MIME type and size validation are HTTP-layer concerns
   - Authorization applies to all attachment operations (Viewer vs Editor)
   - Race condition testing (two users match same receipt) requires database state
   - Inbox filtering (unmatched only) is API contract behavior

3. **Functional tests focus on workflow (15%):**
   - Upload → Inbox → Match is the primary user workflow
   - Visual confidence indicators need browser verification
   - Bulk operations require UI interaction testing
   - Matching algorithm results don't need functional tests (covered by unit tests)

**Contrast with PRD-TRANSACTION-SPLITS:**
- Splits are primarily CRUD operations → Integration-test heavy (60%)
- Attachments have complex algorithm → Unit-test heavy (40%)
- Both features have similar functional test coverage (15%)

---

## Detailed Example 3: PRD-BANK-IMPORT (Import Workflow)

This example demonstrates how a feature that's primarily API orchestration with minimal UI complexity and minimal algorithmic logic becomes Integration-test heavy.

### Overview

**Feature:** Bank Import - Upload OFX/QFX files and review transactions before accepting into workspace.

**Acceptance Criteria Distribution:**
- 4 User Stories
- 34 Total Acceptance Criteria
- API-heavy workflow with persistent review state

This example demonstrates:
- When integration tests dominate (70%+ of coverage)
- How to test stateful API workflows across multiple requests
- When unit tests are minimal (simple parsing logic only)

### Test Distribution Breakdown

| Test Type | Count | Percentage | Coverage Focus |
|-----------|-------|------------|----------------|
| **Controller Integration** | 23-26 | 70% | Import/review/accept API workflow |
| **Unit** | 5-7 | 15% | OFX parsing, duplicate key generation |
| **Functional** | 3-5 | 15% | Upload → Review → Accept workflow |

**Total estimated tests:** 31-38 tests for 34 acceptance criteria.

**Why so Integration-heavy?** The feature is primarily about API state management (upload → review → accept), duplicate detection (database queries), and multi-request workflows - all perfect for integration testing.

### Specific Test Mappings

#### Controller Integration Tests (23-26 tests) - The Sweet Spot

**Test Group 1: POST /api/tenant/{id}/import/upload - Upload OFX/QFX file**

```csharp
[Test]
public async Task UploadBankFile_ValidOFX_ReturnsCreated()
{
    // Given: User has Editor role for tenant
    // And: Valid OFX file with 3 transactions
    // When: User uploads OFX file
    // Then: 201 Created should be returned
    // And: Response should indicate 3 transactions ready for review
}

[Test]
public async Task UploadBankFile_QFXFormat_ReturnsCreated()
{
    // Given: User has Editor role for tenant
    // And: Valid QFX file (SGML-like OFX 1.x format)
    // When: User uploads QFX file
    // Then: 201 Created should be returned
    // And: QFX should be parsed successfully
}

[Test]
public async Task UploadBankFile_CorruptedFile_ReturnsBadRequest()
{
    // Given: User has Editor role for tenant
    // And: Corrupted OFX file (invalid XML)
    // When: User uploads corrupted file
    // Then: 400 Bad Request should be returned
    // And: Error message should indicate parsing failure
}

[Test]
public async Task UploadBankFile_UnsupportedFormat_ReturnsBadRequest()
{
    // Given: User has Editor role for tenant
    // And: Unsupported file format (CSV instead of OFX/QFX)
    // When: User uploads CSV file
    // Then: 400 Bad Request should be returned
    // And: Error message should indicate unsupported format
}

[Test]
public async Task UploadBankFile_PartialFailure_ReturnsPartialSuccess()
{
    // Given: User has Editor role for tenant
    // And: OFX file with 2 valid and 1 invalid transaction (missing amount)
    // When: User uploads file with partial failures
    // Then: 201 Created should be returned (partial success)
    // And: Response should indicate which transactions succeeded/failed
}
```

**Test Group 2: GET /api/tenant/{id}/import/review - Get pending transactions**

```csharp
[Test]
public async Task GetImportReview_WithPendingTransactions_ReturnsOK()
{
    // Given: User has Viewer role for tenant
    // And: 3 transactions in review state
    // When: User requests import review
    // Then: 200 OK should be returned
    // And: Response should contain 3 pending transactions
}

[Test]
public async Task GetImportReview_NoPendingTransactions_ReturnsEmptyList()
{
    // Given: User has Viewer role for tenant
    // And: No transactions in review state
    // When: User requests import review
    // Then: 200 OK should be returned
    // And: Response should be empty list
}

[Test]
public async Task GetImportReview_PersistsAcrossSessions_ReturnsOK()
{
    // Given: User has Editor role for tenant
    // And: User uploaded transactions yesterday
    // When: User logs out and logs back in (new session)
    // And: User requests import review
    // Then: 200 OK should be returned
    // And: Pending transactions should still be there
}
```

**Test Group 3: GET /api/tenant/{id}/import/review/categorized - Duplicate detection**

```csharp
[Test]
public async Task GetReviewCategorized_WithNewTransactions_ReturnsOK()
{
    // Given: User has Viewer role for tenant
    // And: Imported transactions that are all new (no duplicates)
    // When: User requests categorized review
    // Then: 200 OK should be returned
    // And: All transactions should be in "New" category
}

[Test]
public async Task GetReviewCategorized_WithExactDuplicates_ReturnsOK()
{
    // Given: User has Editor role for tenant
    // And: Existing transaction in workspace
    // And: Imported transaction that's exact duplicate (same FITID, same data)
    // When: User requests categorized review
    // Then: 200 OK should be returned
    // And: Transaction should be in "ExactDuplicates" category
    // And: Exact duplicate should be deselected by default
}

[Test]
public async Task GetReviewCategorized_WithPotentialDuplicates_ReturnsOK()
{
    // Given: User has Editor role for tenant
    // And: Existing transaction in workspace
    // And: Imported transaction with same FITID but different payee
    // When: User requests categorized review
    // Then: 200 OK should be returned
    // And: Transaction should be in "PotentialDuplicates" category
    // And: Potential duplicate should be deselected by default
    // And: Should include comparison data (existing vs. imported)
}

[Test]
public async Task GetReviewCategorized_WithMixedCategories_ReturnsOK()
{
    // Given: User has Editor role for tenant
    // And: Existing transaction (exact duplicate)
    // And: Existing transaction (potential duplicate - different amount)
    // And: Import with 1 new, 1 exact duplicate, 1 potential duplicate
    // When: User requests categorized review
    // Then: 200 OK should be returned
    // And: Transactions should be properly categorized
    // And: New transactions should be selected by default
    // And: Duplicates should be deselected by default
}
```

**Test Group 4: POST /api/tenant/{id}/import/accept - Accept selected transactions**

```csharp
[Test]
public async Task AcceptImport_SelectedTransactions_ReturnsOK()
{
    // Given: User has Editor role for tenant
    // And: 3 transactions in review state
    // And: User selects 2 of the 3 transactions
    // When: User accepts selected transactions
    // Then: 200 OK should be returned
    // And: Response should indicate 2 transactions accepted
    // And: Accepted transactions should appear in main transaction list
    // And: Review queue should have 1 transaction remaining
}

[Test]
public async Task AcceptImport_AsViewer_ReturnsForbidden()
{
    // Given: User has Viewer role for tenant (read-only)
    // And: Transactions exist in review state
    // When: Viewer attempts to accept transactions
    // Then: 403 Forbidden should be returned
}

[Test]
public async Task AcceptImport_EmptySelection_ReturnsBadRequest()
{
    // Given: User has Editor role for tenant
    // And: Transactions exist in review state
    // When: User accepts with empty selection
    // Then: 400 Bad Request should be returned
    // And: Error should indicate empty selection
}
```

**Test Group 5: DELETE /api/tenant/{id}/import/review - Clear entire queue**

```csharp
[Test]
public async Task DeleteReviewQueue_AsEditor_ReturnsNoContent()
{
    // Given: User has Editor role for tenant
    // And: 5 transactions in review state
    // When: User deletes entire review queue
    // Then: 204 No Content should be returned
    // And: Review queue should be empty
}

[Test]
public async Task DeleteReviewQueue_AsViewer_ReturnsForbidden()
{
    // Given: User has Viewer role for tenant (read-only)
    // When: Viewer attempts to delete review queue
    // Then: 403 Forbidden should be returned
}
```

**Additional Controller Integration Tests:**
- Multiple uploads merge into single review queue
- Transactions in review state not included in transaction list
- Transactions in review state not included in reports
- Authorization tests for all endpoints (Editor vs Viewer vs different tenant)
- Hash-based duplicate detection when FITID not provided
- Pagination for large import review lists

#### Unit Tests (5-7 tests) - Simple Parsing Logic

**Test 1: OFX/QFX format parsing**

```csharp
[Test]
public void ParseOFXFile_ValidFormat_ReturnsTransactions()
{
    // Given: Valid OFX 2.x file (XML-based)
    // When: OFX is parsed
    // Then: Should return 1 transaction
    // And: Transaction data should be extracted correctly
}

[Test]
public void ParseQFXFile_SGMLFormat_ReturnsTransactions()
{
    // Given: Valid QFX file (SGML-like OFX 1.x format)
    // When: QFX is parsed
    // Then: Should return 1 transaction
    // And: Transaction data should be extracted correctly
}
```

**Test 2: Duplicate key generation (FITID vs hash-based)**

```csharp
[Test]
public void GenerateDuplicateKey_WithFITID_UsesFITID()
{
    // Given: Transaction data with FITID provided
    // When: Duplicate key is generated
    // Then: Should use FITID as key
}

[Test]
public void GenerateDuplicateKey_WithoutFITID_GeneratesHash()
{
    // Given: Transaction data without FITID
    // When: Duplicate key is generated
    // Then: Should generate hash from Date + Amount + Payee
}

[Test]
public void GenerateDuplicateKey_SameData_GeneratesSameHash()
{
    // Given: Two transactions with identical data but no FITID
    // When: Duplicate keys are generated
    // Then: Should generate identical hashes (deterministic)
}
```

**Test 3: Transaction field extraction and validation**

```csharp
[Test]
public void ExtractTransactionData_MissingAmount_ReturnsError()
{
    // Given: OFX transaction element missing amount field
    // When: Transaction data is extracted
    // Then: Should return error
}

[Test]
public void ExtractTransactionData_MissingDate_ReturnsError()
{
    // Given: OFX transaction element missing date field
    // When: Transaction data is extracted
    // Then: Should return error
}
```

**Additional Unit Tests:**
- OFX date format parsing (YYYYMMDD, YYYYMMDDHHMMSS)
- Payee extraction fallback (NAME vs MEMO)
- Memo field extraction

#### Functional Tests (3-5 tests)

**Test 1: Upload bank file → review page → accept selected → transactions appear**

```gherkin
Scenario: User imports bank file and accepts new transactions
  Given user is logged in and viewing transactions page
  When user clicks "Import from Bank" button
  And user selects OFX file "checking-jan-2024.ofx"
  And upload completes successfully
  Then user should be redirected to "Import Review" page
  And page should show "12 New Transactions, 3 Exact Duplicates"
  And new transactions should be selected by default
  And exact duplicates should be deselected by default
  When user clicks "Accept Selected Transactions" button
  Then 12 transactions should be added to transaction list
  And review page should show "Import complete - 0 transactions remaining"
```

**Test 2: Upload with duplicates → see categorization → accept only new**

```gherkin
Scenario: User reviews duplicates and accepts only new transactions
  Given user is logged in with existing transactions
  And user previously imported January statement
  When user uploads overlapping bank file (January 15-31)
  Then Import Review page should show three categories:
    | Category              | Count |
    | New Transactions      | 8     |
    | Exact Duplicates      | 14    |
    | Potential Duplicates  | 1     |
  And "New Transactions" section should be expanded by default
  And "Exact Duplicates" section should be collapsed
  When user expands "Potential Duplicates" section
  Then user should see comparison view showing existing vs. imported data
  When user accepts only new transactions
  Then 8 transactions should be added
  And review queue should be cleared
```

**Test 3: Upload → leave review → return later → state persists**

```gherkin
Scenario: User returns to pending import review after leaving
  Given user is logged in and viewing Import Review page
  And review page shows 15 pending transactions
  When user navigates away to transactions page
  And user logs out
  And user logs back in the next day
  And user navigates to Import Review page
  Then page should still show 15 pending transactions
  And previous selection state should be preserved
  When user clicks "Delete All" button
  Then review queue should be cleared
  And page should show "No pending imports"
```

**Optional Tests:**
- Test 4: Upload multiple files and merge into single review queue
- Test 5: Handle corrupted file with error message

### Key Insights

**Why this distribution makes sense for Bank Import:**

1. **Integration tests dominate (70%):**
   - The feature is primarily about API state management (upload → review → accept)
   - Duplicate detection requires database queries (existing transactions vs. imported)
   - Multi-request workflows are best tested at integration level
   - Authorization applies to all operations (Viewer vs Editor)
   - Persistent state across sessions requires database verification
   - Categorization logic (New/Exact/Potential) requires full database context

2. **Unit tests are minimal (15%):**
   - OFX/QFX parsing is straightforward (simple XML/SGML extraction)
   - Duplicate key generation is a simple algorithm (FITID or hash)
   - Field validation is basic (required fields present?)
   - No complex business logic or calculations

3. **Functional tests focus on workflow (15%):**
   - Upload → Review → Accept is the primary user workflow
   - Visual categorization (New/Exact/Potential) needs browser verification
   - Persistent state needs E2E verification (logout → login → still there)
   - Most import behavior can be verified via API without browser

**Contrast with other features:**
- **PRD-TRANSACTION-ATTACHMENTS** (40% Unit): Complex matching algorithm
- **PRD-TRANSACTION-SPLITS** (60% Integration): CRUD operations heavy
- **PRD-BANK-IMPORT** (70% Integration): Stateful workflow heavy

**Why NOT more unit tests?**
- OFX parsing libraries handle the complexity (we're just calling a library)
- Duplicate detection logic is database-dependent (can't mock effectively)
- State transitions (review → accepted) require database context
- Categorization rules depend on comparing imported vs. existing records

**This is normal:** Features that are primarily about API orchestration, state management, and database queries will naturally be Integration-test heavy. Reserve unit tests for complex algorithms and calculations.

---

## Complete PRD Acceptance Criteria Mapping

This section provides the comprehensive mapping of all ~288 acceptance criteria across 8 Product Requirements Documents (PRDs) to specific test strategies and estimated test counts.

### Summary Table: All PRDs

| PRD | Stories | Criteria | Functional | Integration | Unit | Notes |
|-----|---------|----------|------------|-------------|------|-------|
| **Transaction Record** | 3 | 7 | 0-1 | 5-6 | 0-1 | Schema validation, CRUD operations |
| **Transaction Splits** | 6 | 33 | 3-5 | 20-25 | 5-8 | Complex UI + API, balanced mix |
| **Transaction Filtering** | 6 | 29 | 2-4 | 20-23 | 3-5 | Query logic heavy, integration focus |
| **Transaction Attachments** | 4 | 38 | 3-5 | 15-20 | 15-18 | Matching algorithm drives high unit % |
| **Budgets** | 5 | 43 | 4-6 | 25-30 | 10-15 | Calculations + reports, balanced |
| **Bank Import** | 4 | 34 | 3-5 | 23-26 | 5-7 | Import workflow, integration heavy |
| **Tenant Data Admin** | 5 | 54 | 2-4 | 40-45 | 8-12 | Data operations, CRUD heavy |
| **Payee Rules** | 5 | 50+ | 3-5 | 35-40 | 10-15 | Rule matching + CRUD, balanced |
| **TOTALS** | **38** | **~288** | **20-35** | **183-215** | **56-81** | **259-331 tests** |

### Overall Test Distribution

Based on the comprehensive mapping above:

- **Functional Tests:** 7-13% (20-35 tests) - Critical user workflows only
- **Controller Integration Tests:** 60-70% (183-215 tests) - Primary test layer (sweet spot)
- **Unit Tests:** 19-25% (56-81 tests) - Algorithm complexity and business logic

**Total test range:** 259-331 tests for ~288 acceptance criteria (approximately 1.1x multiplier due to authorization variants, validation cases, and edge conditions).

### Per-PRD Strategic Notes

#### Transaction Record (7 criteria → 6-8 tests)
**Distribution:** Integration-heavy (70%)
- Basic CRUD operations map directly to Controller Integration tests
- Schema validation (required fields, data types) tested at integration level
- Minimal UI complexity → few functional tests needed
- No complex algorithms → minimal unit testing

#### Transaction Splits (33 criteria → 28-38 tests)
**Distribution:** Balanced (60% Integration, 15% Unit, 15% Functional)
- API contract dominates: POST/PUT/DELETE splits endpoints
- Complex UI interactions: Add split button, balance warnings → functional tests
- Balance calculation algorithm → unit tests for edge cases
- Excel import parsing logic → unit tests for format variations
- Authorization variants (Viewer/Editor) multiply integration tests

#### Transaction Filtering (29 criteria → 25-32 tests)
**Distribution:** Integration-heavy (70%)
- Query logic best tested against database (date ranges, text search, category filters)
- Pagination, sorting, filtering parameters → integration tests
- Minimal algorithmic complexity → few unit tests
- Filter UI interactions → limited functional tests for critical paths

#### Transaction Attachments (38 criteria → 33-43 tests)
**Distribution:** Unit-heavy (40% Unit, 45% Integration, 15% Functional)
- Receipt matching algorithm has significant complexity: date tolerance, payee substring matching, amount variance (±10%)
- Year inference logic (future dates → previous year) → unit tests
- Confidence scoring thresholds → unit tests for precision
- File upload/download API → integration tests
- Inbox workflow (upload → match → attached) → functional tests

#### Budgets (43 criteria → 39-51 tests)
**Distribution:** Balanced (60% Integration, 25% Unit, 15% Functional)
- Budget calculation algorithms → unit tests for accuracy
- Period calculations (monthly, quarterly, annual) → unit tests
- Spending reports and aggregations → integration tests (database required)
- Budget vs. actual comparisons → integration tests
- Budget management UI → functional tests for critical workflows

#### Bank Import (34 criteria → 31-38 tests)
**Distribution:** Integration-heavy (70%)
- Stateful workflow (upload → review → accept) requires database persistence
- Duplicate detection (FITID matching, hash-based fallback) → integration tests
- Multi-request workflow best tested end-to-end via API
- OFX/QFX parsing is straightforward → minimal unit tests
- Import review UI → functional tests for workflow verification

#### Tenant Data Admin (54 criteria → 50-61 tests)
**Distribution:** Integration-heavy (75%)
- Data operations: Export all transactions, import historical data, bulk delete
- CRUD operations dominate → Controller Integration tests
- File format handling (CSV, Excel, JSON) → integration tests with file I/O
- Authorization critical (Owner-only operations) → many integration variants
- Minimal algorithmic complexity → few unit tests

#### Payee Rules (50+ criteria → 48-60 tests)
**Distribution:** Balanced (70% Integration, 20% Unit, 10% Functional)
- Rule matching logic (pattern matching, priority ordering) → unit tests
- Rule CRUD operations → integration tests
- Auto-categorization on transaction import → integration tests
- Rule application order and priority → unit tests for algorithm
- Rule management UI → functional tests for critical paths

### Key Insights

#### Test Count Multiplier (~1.1x)

The estimated 259-331 tests for ~288 acceptance criteria reflects a **1.1x multiplier** because:

1. **Authorization variants:** Each API endpoint typically requires 3-4 tests:
   - Success case (authorized user)
   - Forbidden case (insufficient permissions)
   - Unauthorized case (no authentication)
   - Tenant isolation case (different tenant)

2. **Validation variants:** Each create/update operation requires:
   - Success case (valid data)
   - Missing required fields (400 Bad Request)
   - Invalid data formats (400 Bad Request)
   - Business rule violations (400 or 409)

3. **Edge cases:** Features with algorithmic complexity require additional tests:
   - Boundary conditions (empty lists, maximum values)
   - Error handling (network failures, timeouts)
   - Race conditions (concurrent operations)

4. **Some criteria map to multiple tests:** Complex acceptance criteria may require 2-3 tests to fully verify behavior across different scenarios.

5. **Some tests cover multiple criteria:** Well-designed integration tests often verify multiple related acceptance criteria in a single test (e.g., GET /transactions verifies pagination, sorting, and filtering simultaneously).

#### Controller Integration Is the Primary Layer (60-70%)

**Why Integration tests dominate:**
- Most PRD acceptance criteria describe API contracts ("User can retrieve...", "System returns 404 when...")
- Authorization requirements span all CRUD operations
- Database context required for realistic testing (relationships, constraints, queries)
- Fast execution (~100-200ms) enables extensive coverage
- Low maintenance burden compared to functional tests

**Features that deviate:**
- **More Unit tests:** Attachments (40%) due to matching algorithm complexity
- **More Integration tests:** Tenant Data Admin (75%), Bank Import (70%) due to stateful workflows and data operations

#### Implementation Guidance

**Start with Integration tests first (fastest ROI):**
1. Create Controller Integration tests for all API endpoints
2. Add authorization variants (Viewer/Editor/Owner/different tenant)
3. Add validation error cases (required fields, format validation)
4. Verify response formats match DTOs

**Add Functional tests only for critical user paths:**
1. Login/registration workflows
2. Primary feature workflows (e.g., upload → review → accept)
3. Complex UI interactions that can't be verified via API alone
4. Visual behavior (warnings, indicators, dynamic UI updates)

**Add Unit tests for algorithmic complexity:**
1. Business logic calculations (budgets, balances, aggregations)
2. Matching algorithms (attachments, payee rules, duplicates)
3. Parsing logic with multiple format variations
4. Edge cases and boundary conditions
5. Pure logic that doesn't require database or HTTP context

**Expected test growth:** As features are implemented, expect the total test count to trend toward the upper end of the range (320-331 tests) as edge cases and error conditions are discovered during development.

---

## Test Documentation Standards

All tests in YoFi.V3 must follow consistent documentation patterns to ensure readability and maintainability across the entire test suite.

### Gherkin-Style Comments Required

**All tests (unit, integration, functional) MUST use Gherkin-style comments (Given/When/Then/And) to document test scenarios.** Do NOT use the traditional Arrange/Act/Assert pattern.

**Comment Format:**
- `// Given:` - Describes the initial context or preconditions
- `// When:` - Describes the action being tested
- `// Then:` - Describes the expected outcome
- `// And:` - Continues or adds to the previous step type

**Benefits:**
- More readable and business-focused
- Better describes the scenario being tested
- Aligns with behavior-driven development (BDD) practices
- Makes test intent clearer to non-technical stakeholders
- Natural progression through test flow

### NUnit Assertion Patterns

YoFi.V3 uses **NUnit** as the standard testing framework for all unit and integration tests.

**Use constraint-based assertion syntax:**
```csharp
Assert.That(actual, Is.EqualTo(expected));
Assert.That(collection, Is.Not.Empty);
Assert.That(result, Is.Null);
Assert.That(value, Is.GreaterThan(0));
Assert.That(statusCode, Is.EqualTo(HttpStatusCode.OK));
```

**Common NUnit attributes:**
- `[Test]` - Marks a test method
- `[TestFixture]` - Marks a test class
- `[SetUp]` and `[TearDown]` - Test lifecycle
- `[OneTimeSetUp]` and `[OneTimeTearDown]` - Fixture lifecycle
- `[TestCase]` - Parameterized tests
- `[Explicit]` - Tests that should not run by default (requires reason string)

### Example: Well-Documented Test

```csharp
[Test]
public async Task GetTransactions_InvalidTenantIdFormat_Returns404WithProblemDetails()
{
    // Given: A request with an invalid tenant ID format (not a valid GUID)
    // When: API Client requests transactions with invalid tenant ID format
    // Then: 404 Not Found should be returned
    // And: Response should contain problem details (not empty body)
    // And: Response should be valid problem details JSON
}
```

**Key Characteristics:**
- Clear Gherkin-style comments for each test phase
- Descriptive test method name follows pattern: `MethodName_Scenario_ExpectedResult`
- NUnit constraint-based assertions with descriptive failure messages
- Multiple assertions organized with `And:` comments
- Test documents both the API contract and expected behavior

**Reference:** See [`.roorules`](../.roorules) for complete testing patterns and conventions.

---

## Related Documentation

### Test Project READMEs
- [`tests/Functional/README.md`](../tests/Functional/README.md) - BDD/Playwright patterns and page object models
- [`tests/Integration.Controller/README.md`](../tests/Integration.Controller/README.md) - API testing with authentication
- [`tests/Integration.Controller/TESTING-GUIDE.md`](../tests/Integration.Controller/TESTING-GUIDE.md) - Practical controller testing patterns
- [`tests/Integration.Data/README.md`](../tests/Integration.Data/README.md) - Data layer testing with in-memory database
- [`tests/Unit/README.md`](../tests/Unit/README.md) - Unit testing patterns for Application layer

### Product Requirements Documents (PRDs)
- [`docs/wip/transactions/PRD-TRANSACTION-RECORD.md`](wip/transactions/PRD-TRANSACTION-RECORD.md) - Transaction record schema and CRUD
- [`docs/wip/transactions/PRD-TRANSACTION-SPLITS.md`](wip/transactions/PRD-TRANSACTION-SPLITS.md) - Split transactions across categories
- [`docs/wip/transactions/PRD-TRANSACTION-FILTERING.md`](wip/transactions/PRD-TRANSACTION-FILTERING.md) - Advanced filtering and search
- [`docs/wip/transactions/PRD-TRANSACTION-ATTACHMENTS.md`](wip/transactions/PRD-TRANSACTION-ATTACHMENTS.md) - Receipt matching and attachments
- [`docs/wip/budgets/PRD-BUDGETS.md`](wip/budgets/PRD-BUDGETS.md) - Budget management and tracking
- [`docs/wip/import-export/PRD-BANK-IMPORT.md`](wip/import-export/PRD-BANK-IMPORT.md) - OFX/QFX bank file import workflow
- [`docs/wip/import-export/PRD-TENANT-DATA-ADMIN.md`](wip/import-export/PRD-TENANT-DATA-ADMIN.md) - Bulk data operations
- [`docs/wip/payee-rules/PRD-PAYEE-RULES.md`](wip/payee-rules/PRD-PAYEE-RULES.md) - Auto-categorization rules
- [`docs/wip/reports/PRD-REPORTS.md`](wip/reports/PRD-REPORTS.md) - Reporting and analytics

### Architecture Documentation
- [`docs/ARCHITECTURE.md`](ARCHITECTURE.md) - System architecture overview and key design decisions
- [`docs/TENANCY.md`](TENANCY.md) - Multi-tenancy architecture and security model
- [`docs/LOGGING-POLICY.md`](LOGGING-POLICY.md) - Logging standards and sensitive data handling
- [`.roorules`](../.roorules) - Project rules including testing patterns and conventions

---

## Summary

### Testing Strategy at a Glance

**Target Distribution (60/25/15):**
- **60% Controller Integration tests** - Primary layer for API contract verification
- **25% Unit tests** - Business logic and algorithms
- **15% Functional tests** - Critical end-to-end user workflows

**Controller Integration Is the Sweet Spot:**
- Tests complete API contracts (request → response)
- Validates authentication and authorization
- Verifies HTTP status codes and error handling
- Includes database operations (in-memory, fast)
- Reflects real-world API usage
- Fast execution (~100-200ms per test)
- Low maintenance burden

**Key Principles:**
1. **Default to Controller Integration** - Most PRD acceptance criteria map here
2. **Reserve Functional for Critical Paths** - UI workflows only (login, core features)
3. **Unit Test Algorithmic Complexity** - Calculations, matching logic, validation
4. **Use Gherkin Comments** - Given/When/Then for all tests
5. **Run Tests After Changes** - Verify all pass before completing tasks

**This is guidance, not rigid rules.** The distribution will vary by feature:
- **Algorithm-heavy features** (Attachments) → More unit tests (40%)
- **Workflow-heavy features** (Bank Import) → More integration tests (70%)
- **CRUD-heavy features** (Splits) → Balanced mix (60% integration, 25% unit, 15% functional)

**Success metric:** Comprehensive coverage that provides fast feedback, minimizes maintenance burden, and accurately reflects system behavior. The percentages are targets to guide decision-making, not quotas to meet.

**Related:** See [`docs/ARCHITECTURE.md`](ARCHITECTURE.md) for complete testing infrastructure and [`tests/Integration.Controller/TESTING-GUIDE.md`](../tests/Integration.Controller/TESTING-GUIDE.md) for practical implementation patterns.
