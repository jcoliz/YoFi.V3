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
---

# Testing Strategy Design: Bank Import Feature

## Overview

This document defines the comprehensive testing strategy for the Bank Import feature as specified in [`PRD-BANK-IMPORT.md`](PRD-BANK-IMPORT.md). The strategy covers all test layers (Unit, Integration.Data, Integration.Controller, and Functional) with specific test cases, file locations, and verification criteria.

**Test Distribution Target:**
- **70% Controller Integration** - Import/review/accept API workflow, authorization, duplicate detection
- **15% Unit** - OFX parsing, duplicate key generation, field extraction
- **15% Functional** - Upload → Review → Accept user workflows

**Total Estimated Tests:** 31-38 tests for 34 acceptance criteria

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

**Method Under Test:** `ImportReviewFeature.DetectDuplicate()` (private static method - test via public ImportFileAsync)

**Test Cases:**

**Test 1: New transaction (no duplicates)**
```csharp
[Test]
public async Task ImportFileAsync_NewTransaction_MarkedAsNew()
{
    // Given: OFX file with transaction that doesn't exist in database
    // And: No pending import review transactions

    // When: File is imported
    var result = await importReviewFeature.ImportFileAsync(fileStream, "test.ofx");

    // Then: Transaction should be marked as DuplicateStatus.New
    Assert.That(result.NewCount, Is.EqualTo(1));
    Assert.That(result.ExactDuplicateCount, Is.EqualTo(0));
    Assert.That(result.PotentialDuplicateCount, Is.EqualTo(0));

    // And: Transaction should have null DuplicateOfKey
    var transaction = result.Transactions.First();
    Assert.That(transaction.DuplicateStatus, Is.EqualTo(DuplicateStatus.New));
    Assert.That(transaction.DuplicateOfKey, Is.Null);
}
```

**Test 2: Exact duplicate (same FITID and same data)**
```csharp
[Test]
public async Task ImportFileAsync_ExactDuplicateWithFITID_MarkedAsExactDuplicate()
{
    // Given: Existing transaction with FITID "FITID12345"
    // And: OFX file with transaction having same FITID and matching data

    // When: File is imported
    var result = await importReviewFeature.ImportFileAsync(fileStream, "test.ofx");

    // Then: Transaction should be marked as DuplicateStatus.ExactDuplicate
    Assert.That(result.ExactDuplicateCount, Is.EqualTo(1));

    // And: Should reference the existing transaction's Key
    var transaction = result.Transactions.First();
    Assert.That(transaction.DuplicateStatus, Is.EqualTo(DuplicateStatus.ExactDuplicate));
    Assert.That(transaction.DuplicateOfKey, Is.EqualTo(existingTransaction.Key));
}
```

**Test 3: Potential duplicate (same FITID, different amount)**
```csharp
[Test]
public async Task ImportFileAsync_SameFITIDDifferentAmount_MarkedAsPotentialDuplicate()
{
    // Given: Existing transaction with FITID "FITID12345" and amount $50.00
    // And: OFX file with same FITID but amount $55.00 (bank correction?)

    // When: File is imported
    var result = await importReviewFeature.ImportFileAsync(fileStream, "test.ofx");

    // Then: Transaction should be marked as DuplicateStatus.PotentialDuplicate
    Assert.That(result.PotentialDuplicateCount, Is.EqualTo(1));

    // And: Should reference the existing transaction's Key for user review
    var transaction = result.Transactions.First();
    Assert.That(transaction.DuplicateStatus, Is.EqualTo(DuplicateStatus.PotentialDuplicate));
    Assert.That(transaction.DuplicateOfKey, Is.EqualTo(existingTransaction.Key));
}
```

**Test 4: Field-level duplicate (no FITID, same Date+Amount+Payee)**
```csharp
[Test]
public async Task ImportFileAsync_NoFITIDButSameData_MarkedAsPotentialDuplicate()
{
    // Given: Existing transaction without FITID (Date: 2024-01-15, Amount: $50.00, Payee: "Amazon")
    // And: OFX file with transaction (no FITID) with matching Date, Amount, Payee

    // When: File is imported
    var result = await importReviewFeature.ImportFileAsync(fileStream, "test.ofx");

    // Then: Transaction should be marked as PotentialDuplicate (likely duplicate)
    Assert.That(result.PotentialDuplicateCount, Is.EqualTo(1));

    // And: Should reference the existing transaction's Key
    var transaction = result.Transactions.First();
    Assert.That(transaction.DuplicateStatus, Is.EqualTo(DuplicateStatus.PotentialDuplicate));
}
```

**Test 5: Duplicate in pending import review (prevents double import)**
```csharp
[Test]
public async Task ImportFileAsync_DuplicateInPendingImports_MarkedAsDuplicate()
{
    // Given: Pending import review transaction with FITID "FITID12345"
    // And: OFX file with transaction having same FITID

    // When: Second file is imported (same session)
    var result = await importReviewFeature.ImportFileAsync(fileStream, "test2.ofx");

    // Then: Transaction should be marked as duplicate of pending import
    Assert.That(result.ExactDuplicateCount, Is.EqualTo(1));

    // And: Should reference the pending import transaction's Key
    var transaction = result.Transactions.First();
    Assert.That(transaction.DuplicateOfKey, Is.EqualTo(pendingImportTransaction.Key));
}
```

### Test Group 3: Transaction Field Extraction and Validation

**File:** [`tests/Unit/ImportReviewFeatureTests.cs`](../../../tests/Unit/ImportReviewFeatureTests.cs)

**Test 6: Missing required field (amount)**
```csharp
[Test]
public async Task ImportFileAsync_MissingAmount_ReturnsError()
{
    // Given: OFX file with transaction missing amount field

    // When: File is imported
    var result = await importReviewFeature.ImportFileAsync(fileStream, "invalid.ofx");

    // Then: Should return error in parsing result
    Assert.That(result.ImportedCount, Is.EqualTo(0));
    // Note: Error details handled by OFXParsingService
}
```

**Test 7: Missing required field (date)**
```csharp
[Test]
public async Task ImportFileAsync_MissingDate_ReturnsError()
{
    // Given: OFX file with transaction missing date field

    // When: File is imported
    var result = await importReviewFeature.ImportFileAsync(fileStream, "invalid.ofx");

    // Then: Should return error in parsing result
    Assert.That(result.ImportedCount, Is.EqualTo(0));
}
```

## Integration Tests - Data Layer (3-5 tests)

**Location:** [`tests/Integration.Data/`](../../../tests/Integration.Data/)

**File:** [`tests/Integration.Data/ImportReviewTransactionTests.cs`](../../../tests/Integration.Data/ImportReviewTransactionTests.cs) (new file)

**Purpose:** Verify [`ImportReviewTransaction`](../../../src/Entities/Models/ImportReviewTransaction.cs) entity CRUD operations, tenant isolation, and database constraints.

**Test Framework:** NUnit + Entity Framework Core (in-memory database)

### Test Cases

**Test 1: Create import review transaction with tenant isolation**
```csharp
[Test]
public async Task AddImportReviewTransaction_WithTenantId_Success()
{
    // Given: A valid import review transaction with TenantId
    var reviewTransaction = new ImportReviewTransaction
    {
        Key = Guid.NewGuid(),
        TenantId = _testTenantId,
        Date = new DateOnly(2024, 1, 15),
        Payee = "Test Payee",
        Amount = 50.00m,
        ExternalId = "FITID12345",
        DuplicateStatus = DuplicateStatus.New,
        ImportedAt = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow
    };

    // When: Transaction is added to database
    await _dbContext.ImportReviewTransactions.AddAsync(reviewTransaction);
    await _dbContext.SaveChangesAsync();

    // Then: Transaction should be persisted
    var saved = await _dbContext.ImportReviewTransactions
        .FirstOrDefaultAsync(t => t.Key == reviewTransaction.Key);
    Assert.That(saved, Is.Not.Null);
    Assert.That(saved.TenantId, Is.EqualTo(_testTenantId));
}
```

**Test 2: Query filters by tenant (isolation verification)**
```csharp
[Test]
public async Task GetImportReviewTransactions_FiltersByTenant()
{
    // Given: Import review transactions for two different tenants
    var tenant1Id = Guid.NewGuid();
    var tenant2Id = Guid.NewGuid();

    await SeedImportReviewTransaction(tenant1Id, "Tenant1-Transaction");
    await SeedImportReviewTransaction(tenant2Id, "Tenant2-Transaction");

    // When: Querying for tenant1's transactions
    var tenant1Transactions = await _dbContext.ImportReviewTransactions
        .Where(t => t.TenantId == tenant1Id)
        .ToListAsync();

    // Then: Should only return tenant1's transactions
    Assert.That(tenant1Transactions.Count, Is.EqualTo(1));
    Assert.That(tenant1Transactions.First().Payee, Is.EqualTo("Tenant1-Transaction"));
}
```

**Test 3: Cascade delete when tenant is deleted**
```csharp
[Test]
public async Task DeleteTenant_CascadesDeleteImportReviewTransactions()
{
    // Given: A tenant with import review transactions
    var tenant = new Tenant { Key = Guid.NewGuid(), Name = "Test Tenant" };
    await _dbContext.Tenants.AddAsync(tenant);
    await _dbContext.SaveChangesAsync();

    await SeedImportReviewTransaction(tenant.Key, "Test Transaction");

    // When: Tenant is deleted
    _dbContext.Tenants.Remove(tenant);
    await _dbContext.SaveChangesAsync();

    // Then: Import review transactions should be cascade deleted
    var remaining = await _dbContext.ImportReviewTransactions
        .Where(t => t.TenantId == tenant.Key)
        .ToListAsync();
    Assert.That(remaining, Is.Empty);
}
```

**Test 4: Index performance on (TenantId, ExternalId)**
```csharp
[Test]
public async Task QueryByTenantAndExternalId_UsesIndex()
{
    // Given: Multiple import review transactions
    var tenantId = Guid.NewGuid();
    await SeedImportReviewTransaction(tenantId, "Transaction1", "FITID001");
    await SeedImportReviewTransaction(tenantId, "Transaction2", "FITID002");
    await SeedImportReviewTransaction(tenantId, "Transaction3", "FITID003");

    // When: Querying by TenantId and ExternalId (index should be used)
    var transaction = await _dbContext.ImportReviewTransactions
        .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.ExternalId == "FITID002");

    // Then: Should efficiently retrieve the correct transaction
    Assert.That(transaction, Is.Not.Null);
    Assert.That(transaction.Payee, Is.EqualTo("Transaction2"));
}
```

**Optional Test 5: DuplicateStatus enum storage**
```csharp
[Test]
public async Task ImportReviewTransaction_StoresDuplicateStatusCorrectly()
{
    // Given: Import review transactions with all DuplicateStatus values

    // When: Stored and retrieved

    // Then: DuplicateStatus should be preserved correctly
}
```

## Integration Tests - Controller Layer (23-26 tests)

**Location:** [`tests/Integration.Controller/`](../../../tests/Integration.Controller/)

**File:** [`tests/Integration.Controller/ImportControllerTests.cs`](../../../tests/Integration.Controller/ImportControllerTests.cs) (new file)

**Purpose:** Test [`ImportController`](../../../src/Controllers/ImportController.cs) API endpoints with complete HTTP request/response cycle, authentication, authorization, and database operations.

**Test Framework:** NUnit + ASP.NET Core WebApplicationFactory + In-Memory Database

**Base Class:** [`AuthenticatedTestBase`](../../../tests/Integration.Controller/TestHelpers/AuthenticatedTestBase.cs)

### Test Group 1: POST /api/tenant/{tenantId}/import/upload

**Endpoint:** Upload OFX/QFX file

**Test 1: Success - Valid OFX file**
```csharp
[Test]
public async Task UploadBankFile_ValidOFX_ReturnsCreated()
{
    // Given: User has Editor role for tenant
    // And: Valid OFX file with 3 transactions
    var ofxContent = File.ReadAllBytes("SampleData/Ofx/Bank1.ofx");
    var formData = new MultipartFormDataContent();
    formData.Add(new ByteArrayContent(ofxContent), "file", "Bank1.ofx");

    // When: User uploads OFX file
    var response = await _client.PostAsync($"/api/tenant/{_tenantId}/import/upload", formData);

    // Then: 201 Created should be returned
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

    // And: Response should contain import summary
    var result = await response.Content.ReadFromJsonAsync<ImportResultDto>();
    Assert.That(result, Is.Not.Null);
    Assert.That(result.ImportedCount, Is.GreaterThan(0));
    Assert.That(result.NewCount, Is.GreaterThan(0));
}
```

**Test 2: Success - QFX format (SGML)**
```csharp
[Test]
public async Task UploadBankFile_QFXFormat_ReturnsCreated()
{
    // Given: User has Editor role for tenant
    // And: Valid QFX file (SGML-like OFX 1.x format)

    // When: User uploads QFX file
    var response = await _client.PostAsync($"/api/tenant/{_tenantId}/import/upload", formData);

    // Then: 201 Created should be returned
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
}
```

**Test 3: Error - Corrupted file**
```csharp
[Test]
public async Task UploadBankFile_CorruptedFile_ReturnsBadRequest()
{
    // Given: User has Editor role for tenant
    // And: Corrupted OFX file (invalid XML)
    var corruptedContent = Encoding.UTF8.GetBytes("<INVALID_XML>");
    var formData = new MultipartFormDataContent();
    formData.Add(new ByteArrayContent(corruptedContent), "file", "corrupted.ofx");

    // When: User uploads corrupted file
    var response = await _client.PostAsync($"/api/tenant/{_tenantId}/import/upload", formData);

    // Then: 400 Bad Request should be returned
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

    // And: Error message should indicate parsing failure
    var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
    Assert.That(problemDetails, Is.Not.Null);
    Assert.That(problemDetails.Detail, Does.Contain("parse"));
}
```

**Test 4: Error - Unsupported format**
```csharp
[Test]
public async Task UploadBankFile_UnsupportedFormat_ReturnsBadRequest()
{
    // Given: User has Editor role for tenant
    // And: Unsupported file format (CSV instead of OFX/QFX)
    var csvContent = Encoding.UTF8.GetBytes("Date,Payee,Amount\n2024-01-15,Test,50.00");
    var formData = new MultipartFormDataContent();
    formData.Add(new ByteArrayContent(csvContent), "file", "transactions.csv");

    // When: User uploads CSV file
    var response = await _client.PostAsync($"/api/tenant/{_tenantId}/import/upload", formData);

    // Then: 400 Bad Request should be returned
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
}
```

**Test 5: Authorization - Viewer role forbidden**
```csharp
[Test]
public async Task UploadBankFile_AsViewer_ReturnsForbidden()
{
    // Given: User has Viewer role for tenant (read-only)
    // And: Valid OFX file

    // When: Viewer attempts to upload file
    var response = await _viewerClient.PostAsync($"/api/tenant/{_tenantId}/import/upload", formData);

    // Then: 403 Forbidden should be returned
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
}
```

**Test 6: Authorization - Unauthenticated**
```csharp
[Test]
public async Task UploadBankFile_Unauthenticated_ReturnsUnauthorized()
{
    // Given: No authentication token provided
    // And: Valid OFX file

    // When: Request is made without authentication
    var response = await _unauthenticatedClient.PostAsync($"/api/tenant/{_tenantId}/import/upload", formData);

    // Then: 401 Unauthorized should be returned
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
}
```

**Test 7: Tenant isolation - Different tenant forbidden**
```csharp
[Test]
public async Task UploadBankFile_DifferentTenant_ReturnsForbidden()
{
    // Given: User has Editor role for tenant A
    // And: Valid OFX file

    // When: User attempts to upload to tenant B
    var response = await _client.PostAsync($"/api/tenant/{_otherTenantId}/import/upload", formData);

    // Then: 403 Forbidden should be returned (tenant isolation)
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
}
```

**Test 8: Partial success (some transactions fail validation)**
```csharp
[Test]
public async Task UploadBankFile_PartialFailure_ReturnsPartialSuccess()
{
    // Given: User has Editor role for tenant
    // And: OFX file with 2 valid and 1 invalid transaction (missing amount)

    // When: User uploads file with partial failures
    var response = await _client.PostAsync($"/api/tenant/{_tenantId}/import/upload", formData);

    // Then: 201 Created should be returned (partial success)
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));

    // And: Response should indicate which transactions succeeded/failed
    var result = await response.Content.ReadFromJsonAsync<ImportResultDto>();
    Assert.That(result.ImportedCount, Is.EqualTo(2));
}
```

### Test Group 2: GET /api/tenant/{tenantId}/import/review

**Endpoint:** Get pending import review transactions

**Test 9: Success - Returns pending transactions**
```csharp
[Test]
public async Task GetImportReview_WithPendingTransactions_ReturnsOK()
{
    // Given: User has Viewer role for tenant
    // And: 3 transactions in review state
    await SeedImportReviewTransactions(_tenantId, count: 3);

    // When: User requests import review
    var response = await _client.GetAsync($"/api/tenant/{_tenantId}/import/review");

    // Then: 200 OK should be returned
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    // And: Response should contain 3 pending transactions
    var transactions = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<ImportReviewTransactionDto>>();
    Assert.That(transactions, Is.Not.Null);
    Assert.That(transactions.Count, Is.EqualTo(3));
}
```

**Test 10: Empty result - No pending transactions**
```csharp
[Test]
public async Task GetImportReview_NoPendingTransactions_ReturnsEmptyList()
{
    // Given: User has Viewer role for tenant
    // And: No transactions in review state

    // When: User requests import review
    var response = await _client.GetAsync($"/api/tenant/{_tenantId}/import/review");

    // Then: 200 OK should be returned
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    // And: Response should be empty list
    var transactions = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<ImportReviewTransactionDto>>();
    Assert.That(transactions, Is.Empty);
}
```

**Test 11: Persistence - State persists across requests**
```csharp
[Test]
public async Task GetImportReview_PersistsAcrossSessions_ReturnsOK()
{
    // Given: User has Editor role for tenant
    // And: User uploaded transactions (previous request)
    await UploadOFXFile(_tenantId, "Bank1.ofx");

    // When: User requests import review (new request, simulates new session)
    var response = await _client.GetAsync($"/api/tenant/{_tenantId}/import/review");

    // Then: 200 OK should be returned
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    // And: Pending transactions should still be there
    var transactions = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<ImportReviewTransactionDto>>();
    Assert.That(transactions, Is.Not.Empty);
}
```

**Test 12: Tenant isolation - Only shows user's tenant data**
```csharp
[Test]
public async Task GetImportReview_DifferentTenant_ReturnsEmpty()
{
    // Given: User has Editor role for tenant A
    // And: Tenant B has pending import review transactions
    await SeedImportReviewTransactions(_otherTenantId, count: 5);

    // When: User A requests import review for their tenant
    var response = await _client.GetAsync($"/api/tenant/{_tenantId}/import/review");

    // Then: 200 OK should be returned
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    // And: Should not see tenant B's transactions (isolation)
    var transactions = await response.Content.ReadFromJsonAsync<IReadOnlyCollection<ImportReviewTransactionDto>>();
    Assert.That(transactions, Is.Empty);
}
```

### Test Group 3: GET /api/tenant/{tenantId}/import/review/categorized

**Endpoint:** Get pending transactions grouped by duplicate status

**Test 13: All new transactions**
```csharp
[Test]
public async Task GetReviewCategorized_WithNewTransactions_ReturnsOK()
{
    // Given: User has Viewer role for tenant
    // And: Imported transactions that are all new (no duplicates)
    await SeedImportReviewTransactions(_tenantId, count: 5, status: DuplicateStatus.New);

    // When: User requests categorized review
    var response = await _client.GetAsync($"/api/tenant/{_tenantId}/import/review/categorized");

    // Then: 200 OK should be returned
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    // And: All transactions should be in "New" category
    var result = await response.Content.ReadFromJsonAsync<CategorizedImportResultDto>();
    Assert.That(result.NewCount, Is.EqualTo(5));
    Assert.That(result.ExactDuplicateCount, Is.EqualTo(0));
    Assert.That(result.PotentialDuplicateCount, Is.EqualTo(0));
}
```

**Test 14: Exact duplicates detection**
```csharp
[Test]
public async Task GetReviewCategorized_WithExactDuplicates_ReturnsOK()
{
    // Given: User has Editor role for tenant
    // And: Existing transaction in workspace
    var existingTxn = await CreateTransaction(_tenantId, externalId: "FITID12345");

    // And: Imported transaction that's exact duplicate (same FITID, same data)
    await SeedImportReviewTransaction(_tenantId, externalId: "FITID12345",
        status: DuplicateStatus.ExactDuplicate, duplicateOfKey: existingTxn.Key);

    // When: User requests categorized review
    var response = await _client.GetAsync($"/api/tenant/{_tenantId}/import/review/categorized");

    // Then: 200 OK should be returned
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    // And: Transaction should be in "ExactDuplicates" category
    var result = await response.Content.ReadFromJsonAsync<CategorizedImportResultDto>();
    Assert.That(result.ExactDuplicateCount, Is.EqualTo(1));

    // And: Exact duplicate should be deselected by default
    var duplicate = result.ExactDuplicates.First();
    Assert.That(duplicate.Selected, Is.False);
}
```

**Test 15: Potential duplicates with comparison data**
```csharp
[Test]
public async Task GetReviewCategorized_WithPotentialDuplicates_IncludesComparisonData()
{
    // Given: User has Editor role for tenant
    // And: Existing transaction in workspace
    var existingTxn = await CreateTransaction(_tenantId, externalId: "FITID12345", amount: 50.00m);

    // And: Imported transaction with same FITID but different payee (bank correction?)
    await SeedImportReviewTransaction(_tenantId, externalId: "FITID12345", amount: 55.00m,
        status: DuplicateStatus.PotentialDuplicate, duplicateOfKey: existingTxn.Key);

    // When: User requests categorized review
    var response = await _client.GetAsync($"/api/tenant/{_tenantId}/import/review/categorized");

    // Then: 200 OK should be returned
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    // And: Transaction should be in "PotentialDuplicates" category
    var result = await response.Content.ReadFromJsonAsync<CategorizedImportResultDto>();
    Assert.That(result.PotentialDuplicateCount, Is.EqualTo(1));

    // And: Should include comparison data (existing vs. imported)
    var duplicate = result.PotentialDuplicates.First();
    Assert.That(duplicate.ExistingTransaction, Is.Not.Null);
    Assert.That(duplicate.ImportedTransaction, Is.Not.Null);
}
```

**Test 16: Mixed categories**
```csharp
[Test]
public async Task GetReviewCategorized_MixedCategories_ReturnsOK()
{
    // Given: User has Editor role for tenant
    // And: Various import scenarios
    await SeedImportReviewTransactions(_tenantId, count: 2, status: DuplicateStatus.New);
    await SeedImportReviewTransactions(_tenantId, count: 1, status: DuplicateStatus.ExactDuplicate);
    await SeedImportReviewTransactions(_tenantId, count: 1, status: DuplicateStatus.PotentialDuplicate);

    // When: User requests categorized review
    var response = await _client.GetAsync($"/api/tenant/{_tenantId}/import/review/categorized");

    // Then: 200 OK should be returned
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    // And: Transactions should be properly categorized
    var result = await response.Content.ReadFromJsonAsync<CategorizedImportResultDto>();
    Assert.That(result.NewCount, Is.EqualTo(2));
    Assert.That(result.ExactDuplicateCount, Is.EqualTo(1));
    Assert.That(result.PotentialDuplicateCount, Is.EqualTo(1));

    // And: New transactions should be selected by default
    Assert.That(result.NewTransactions.All(t => t.Selected), Is.True);

    // And: Duplicates should be deselected by default
    Assert.That(result.ExactDuplicates.All(t => !t.Selected), Is.True);
    Assert.That(result.PotentialDuplicates.All(t => !t.Selected), Is.True);
}
```

### Test Group 4: POST /api/tenant/{tenantId}/import/accept

**Endpoint:** Accept selected transactions from review into main transaction table

**Test 17: Success - Accept selected transactions**
```csharp
[Test]
public async Task AcceptImport_SelectedTransactions_ReturnsOK()
{
    // Given: User has Editor role for tenant
    // And: 3 transactions in review state
    var reviewKeys = await SeedImportReviewTransactions(_tenantId, count: 3);

    // And: User selects 2 of the 3 transactions
    var acceptRequest = new AcceptTransactionsRequestDto { Keys = reviewKeys.Take(2).ToList() };

    // When: User accepts selected transactions
    var response = await _client.PostAsJsonAsync($"/api/tenant/{_tenantId}/import/accept", acceptRequest);

    // Then: 200 OK should be returned
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    // And: Response should indicate 2 transactions accepted
    var result = await response.Content.ReadFromJsonAsync<AcceptTransactionsResultDto>();
    Assert.That(result.AcceptedCount, Is.EqualTo(2));
    Assert.That(result.DeletedCount, Is.EqualTo(2));

    // And: Accepted transactions should appear in main transaction list
    var transactions = await GetTransactions(_tenantId);
    Assert.That(transactions.Count, Is.GreaterThanOrEqualTo(2));

    // And: Review queue should have 1 transaction remaining
    var remainingReview = await GetImportReview(_tenantId);
    Assert.That(remainingReview.Count, Is.EqualTo(1));
}
```

**Test 18: Authorization - Viewer forbidden**
```csharp
[Test]
public async Task AcceptImport_AsViewer_ReturnsForbidden()
{
    // Given: User has Viewer role for tenant (read-only)
    // And: Transactions exist in review state
    var reviewKeys = await SeedImportReviewTransactions(_tenantId, count: 2);
    var acceptRequest = new AcceptTransactionsRequestDto { Keys = reviewKeys };

    // When: Viewer attempts to accept transactions
    var response = await _viewerClient.PostAsJsonAsync($"/api/tenant/{_tenantId}/import/accept", acceptRequest);

    // Then: 403 Forbidden should be returned
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
}
```

**Test 19: Validation - Empty selection**
```csharp
[Test]
public async Task AcceptImport_EmptySelection_ReturnsBadRequest()
{
    // Given: User has Editor role for tenant
    // And: Transactions exist in review state
    var acceptRequest = new AcceptTransactionsRequestDto { Keys = new List<Guid>() };

    // When: User accepts with empty selection
    var response = await _client.PostAsJsonAsync($"/api/tenant/{_tenantId}/import/accept", acceptRequest);

    // Then: 400 Bad Request should be returned
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

    // And: Error should indicate empty selection
    var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
    Assert.That(problemDetails.Detail, Does.Contain("empty"));
}
```

**Test 20: Tenant isolation - Cannot accept other tenant's transactions**
```csharp
[Test]
public async Task AcceptImport_DifferentTenantTransactions_Returns404()
{
    // Given: User has Editor role for tenant A
    // And: Tenant B has pending import review transactions
    var otherTenantKeys = await SeedImportReviewTransactions(_otherTenantId, count: 2);
    var acceptRequest = new AcceptTransactionsRequestDto { Keys = otherTenantKeys };

    // When: User A attempts to accept tenant B's transactions
    var response = await _client.PostAsJsonAsync($"/api/tenant/{_tenantId}/import/accept", acceptRequest);

    // Then: Should return error (tenant isolation - no transactions found)
    Assert.That(response.StatusCode, Is.Not.EqualTo(HttpStatusCode.OK));
}
```

### Test Group 5: DELETE /api/tenant/{tenantId}/import/review

**Endpoint:** Clear entire import review queue

**Test 21: Success - Delete all pending transactions**
```csharp
[Test]
public async Task DeleteReviewQueue_AsEditor_ReturnsNoContent()
{
    // Given: User has Editor role for tenant
    // And: 5 transactions in review state
    await SeedImportReviewTransactions(_tenantId, count: 5);

    // When: User deletes entire review queue
    var response = await _client.DeleteAsync($"/api/tenant/{_tenantId}/import/review");

    // Then: 204 No Content should be returned
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

    // And: Review queue should be empty
    var remaining = await GetImportReview(_tenantId);
    Assert.That(remaining, Is.Empty);
}
```

**Test 22: Authorization - Viewer forbidden**
```csharp
[Test]
public async Task DeleteReviewQueue_AsViewer_ReturnsForbidden()
{
    // Given: User has Viewer role for tenant (read-only)
    // And: Transactions in review state
    await SeedImportReviewTransactions(_tenantId, count: 3);

    // When: Viewer attempts to delete review queue
    var response = await _viewerClient.DeleteAsync($"/api/tenant/{_tenantId}/import/review");

    // Then: 403 Forbidden should be returned
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
}
```

**Test 23: Idempotent - Delete empty queue succeeds**
```csharp
[Test]
public async Task DeleteReviewQueue_EmptyQueue_ReturnsNoContent()
{
    // Given: User has Editor role for tenant
    // And: No transactions in review state

    // When: User deletes empty review queue
    var response = await _client.DeleteAsync($"/api/tenant/{_tenantId}/import/review");

    // Then: 204 No Content should be returned (idempotent)
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
}
```

### Additional Integration Test Scenarios

**Test 24: Multiple uploads merge into single review queue**
```csharp
[Test]
public async Task UploadMultipleFiles_MergesIntoSingleQueue()
{
    // Given: User uploads first OFX file (3 transactions)
    // When: User uploads second OFX file (2 transactions)
    // Then: Review queue should contain 5 total transactions
}
```

**Test 25: Transactions in review not included in transaction list**
```csharp
[Test]
public async Task GetTransactions_ExcludesReviewState()
{
    // Given: User has 10 accepted transactions
    // And: User has 5 pending import review transactions
    // When: User requests transaction list
    // Then: Should return only 10 accepted transactions (not pending imports)
}
```

**Test 26: Pagination for large import review lists**
```csharp
[Test]
public async Task GetImportReview_LargeImport_SupportsPagination()
{
    // Given: User has 500 pending import review transactions
    // When: User requests first page (50 items)
    // Then: Should return 50 transactions with pagination metadata
}
```

## Functional Tests (3-5 tests)

**Location:** [`tests/Functional/`](../../../tests/Functional/)

**Technology:** Playwright + SpecFlow (Gherkin scenarios)

**Purpose:** Validate complete end-to-end user workflows through the browser, testing the entire stack from UI to database.

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

  Scenario: User imports bank file and accepts new transactions
    Given I am on the transactions page
    When I click "Import from Bank" button
    And I upload OFX file "checking-jan-2024.ofx"
    Then I should be redirected to "Import Review" page
    And page should show "12 New Transactions"
    And page should show "3 Exact Duplicates"
    And new transactions should be selected by default
    And exact duplicates should be deselected by default
    When I click "Accept Selected Transactions" button
    Then 12 transactions should be added to transaction list
    And import review page should show "0 transactions remaining"

  Scenario: User reviews duplicates and accepts only new transactions
    Given I have existing transactions from January 1-15
    And I am on the import review page
    When I upload bank file with overlapping dates "checking-jan-15-31.ofx"
    Then Import Review page should show three categories:
      | Category              | Count |
      | New Transactions      | 8     |
      | Exact Duplicates      | 14    |
      | Potential Duplicates  | 1     |
    And "New Transactions" section should be expanded by default
    And "Exact Duplicates" section should be collapsed
    When I expand "Potential Duplicates" section
    Then I should see comparison view showing existing vs. imported data
    When I click "Accept Selected" button
    Then 8 transactions should be added
    And import review queue should be cleared

  Scenario: User returns to pending import review after leaving
    Given I have 15 pending import review transactions
    And I am on the import review page
    When I navigate to transactions page
    And I log out
    And I log back in the next day
    And I navigate to import review page
    Then page should still show 15 pending transactions
    And previous selection state should be preserved
    When I click "Delete All" button
    Then review queue should be cleared
    And page should show "No pending imports"

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

### Page Object Model

**File:** [`tests/Functional/Pages/ImportPage.cs`](../../../tests/Functional/Pages/ImportPage.cs) (new file)

**Structure:**
```csharp
public class ImportPage : BasePage
{
    // Locators
    private ILocator UploadButton => Page.Locator("[data-testid='upload-button']");
    private ILocator FileInput => Page.Locator("input[type='file']");
    private ILocator AcceptButton => Page.Locator("[data-testid='accept-button']");
    private ILocator DeleteAllButton => Page.Locator("[data-testid='delete-all-button']");
    private ILocator NewTransactionsSection => Page.Locator("[data-testid='new-transactions']");
    private ILocator ExactDuplicatesSection => Page.Locator("[data-testid='exact-duplicates']");
    private ILocator PotentialDuplicatesSection => Page.Locator("[data-testid='potential-duplicates']");

    // Actions
    public async Task NavigateAsync() => await Page.GotoAsync("/import");
    public async Task UploadFileAsync(string filePath);
    public async Task ClickAcceptSelectedAsync();
    public async Task ClickDeleteAllAsync();
    public async Task ExpandSectionAsync(string sectionName);

    // Assertions
    public async Task<int> GetNewTransactionCountAsync();
    public async Task<int> GetExactDuplicateCountAsync();
    public async Task<int> GetPotentialDuplicateCountAsync();
    public async Task<bool> IsTransactionSelectedAsync(Guid key);
    public async Task<string> GetErrorMessageAsync();
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
        // Seed transactions via API
    }

    [Given(@"I have (.*) pending import review transactions")]
    public async Task GivenIHavePendingImportReviewTransactions(int count)
    {
        // Seed import review transactions via API
    }

    [When(@"I click ""(.*)"" button")]
    public async Task WhenIClickButton(string buttonName)
    {
        if (buttonName == "Import from Bank")
            await _importPage.ClickUploadButtonAsync();
        // ...
    }

    [When(@"I upload OFX file ""(.*)""")]
    public async Task WhenIUploadOFXFile(string fileName)
    {
        var filePath = Path.Combine("SampleData", "Ofx", fileName);
        await _importPage.UploadFileAsync(filePath);
    }

    [Then(@"page should show ""(.*) New Transactions""")]
    public async Task ThenPageShouldShowNewTransactions(int count)
    {
        var actualCount = await _importPage.GetNewTransactionCountAsync();
        Assert.That(actualCount, Is.EqualTo(count));
    }

    [Then(@"(.*) transactions should be added to transaction list")]
    public async Task ThenTransactionsShouldBeAdded(int count)
    {
        // Verify via API or UI
    }
}
```

### Test Data Management

**Sample OFX Files for Functional Tests:**

**Location:** [`tests/Functional/SampleData/Ofx/`](../../../tests/Functional/SampleData/Ofx/) (copy from Unit tests)

**Files:**
- `checking-jan-2024.ofx` - Standard checking account with 15 transactions (12 new, 3 duplicates)
- `checking-jan-15-31.ofx` - Overlapping date range for duplicate detection testing
- `invalid.ofx` - Corrupted file for error handling
- `transactions.csv` - Wrong format for validation testing

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
- [ ] Create import review transaction with tenant isolation
- [ ] Query filters by TenantId (isolation verification)
- [ ] Cascade delete when tenant is deleted
- [ ] Index performance on (TenantId, ExternalId)
- [ ] DuplicateStatus enum storage and retrieval

### Integration Tests - Controller Layer (26 tests)

**Upload Endpoint (8 tests):**
- [ ] Upload valid OFX file - Returns 201 Created
- [ ] Upload valid QFX file - Returns 201 Created
- [ ] Upload corrupted file - Returns 400 Bad Request
- [ ] Upload unsupported format (CSV) - Returns 400 Bad Request
- [ ] Upload as Viewer - Returns 403 Forbidden
- [ ] Upload unauthenticated - Returns 401 Unauthorized
- [ ] Upload to different tenant - Returns 403 Forbidden
- [ ] Upload with partial failures - Returns 201 with partial success

**Get Review Endpoint (4 tests):**
- [ ] Get pending transactions - Returns 200 OK with transactions
- [ ] Get when empty - Returns 200 OK with empty list
- [ ] Get persists across sessions - Returns same data
- [ ] Get with different tenant - Returns empty (isolation)

**Get Categorized Review Endpoint (4 tests):**
- [ ] Get with all new transactions - Returns proper categorization
- [ ] Get with exact duplicates - Shows deselected by default
- [ ] Get with potential duplicates - Includes comparison data
- [ ] Get with mixed categories - Proper categorization and selection state

**Accept Endpoint (4 tests):**
- [ ] Accept selected transactions - Returns 200 OK, adds to main table
- [ ] Accept as Viewer - Returns 403 Forbidden
- [ ] Accept with empty selection - Returns 400 Bad Request
- [ ] Accept different tenant's transactions - Returns error (isolation)

**Delete Endpoint (3 tests):**
- [ ] Delete all pending transactions - Returns 204 No Content
- [ ] Delete as Viewer - Returns 403 Forbidden
- [ ] Delete empty queue - Returns 204 No Content (idempotent)

**Additional Scenarios (3 tests):**
- [ ] Multiple uploads merge into single review queue
- [ ] Transactions in review excluded from transaction list
- [ ] Pagination for large import review lists

### Functional Tests (5 tests)
- [ ] Upload → Review → Accept workflow - Complete happy path
- [ ] Review duplicates → Accept only new - Duplicate detection UI
- [ ] Return to pending import later - State persistence
- [ ] Upload corrupted file - Error handling
- [ ] Upload unsupported format - Validation

## Test Execution Strategy

### Running Tests Locally

**Unit Tests Only:**
```powershell
dotnet test tests/Unit --filter "FullyQualifiedName~ImportReview"
```

**Integration Tests (Data + Controller):**
```powershell
pwsh -File ./scripts/Run-Tests.ps1
```

**Functional Tests (Against Container):**
```powershell
pwsh -File ./scripts/Run-FunctionalTestsVsContainer.ps1
```

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
