---
status: Approved
layer: Application
parent: DESIGN-BANK-IMPORT.md
created: 2025-12-28
related_docs:
  - DESIGN-BANK-IMPORT.md
  - DESIGN-BANK-IMPORT-DATABASE.md
  - PRD-BANK-IMPORT.md
  - VISUAL-DESIGN-BANK-IMPORT.md
  - MOCKUP-BANK-IMPORT.md
---

# Application Layer Design: Bank Import Feature

## Overview

This document provides the complete application layer design for the Bank Import feature, including business logic, DTOs, and the [`ImportReviewFeature`](src/Application/Import/Features/ImportReviewFeature.cs) class. The application layer orchestrates the import workflow: parsing OFX files, detecting duplicates, managing pending review transactions, and accepting transactions into the main transaction table.

**Key components:**
- **DTOs** - [`ImportReviewTransactionDto`](src/Application/Import/Dto/ImportReviewTransactionDto.cs), [`ImportResultDto`](src/Application/Import/Dto/ImportResultDto.cs), [`CompleteReviewResultDto`](src/Application/Import/Dto/CompleteReviewResultDto.cs)
- **ImportReviewFeature** - Business logic for import workflow orchestration
- **Duplicate Detection Strategy** - Two-phase detection using ExternalId and field matching
- **Service Registration** - Integration with [`ServiceCollectionExtensions`](src/Application/ServiceCollectionExtensions.cs)

**Layer responsibilities:**
- Parse OFX files using [`OfxParsingService`](src/Application/Import/Services/OfxParsingService.cs) (already implemented)
- Detect duplicates against existing transactions and pending imports
- Store parsed transactions in [`ImportReviewTransaction`](src/Entities/Models/ImportReviewTransaction.cs) staging table
- Provide read operations for pending review transactions
- Complete review workflow: accept selected transactions via [`TransactionsFeature`](src/Application/Features/TransactionsFeature.cs) and delete all pending imports
- Leverage TransactionsFeature to ensure consistent transaction creation (including default splits)

## Data flow

Frontend uploads OFX File
  → API Controllers (POST /api/import/upload)
    → ImportReviewFeature.ImportFileAsync()
      → OfxParsingService.ParseAsync()
        → TransactionImportDto[] (returned to ImportReviewFeature)
          → ImportReviewTransaction entities (created and stored by ImportReviewFeature)
            → Database

Later, frontend requests review data:
Frontend
  → API Controllers (GET /api/import/review)
    → ImportReviewFeature.GetPendingReviewAsync()
      → Database query for ImportReviewTransaction entities
        → ImportReviewTransactionDto[] (mapped and returned)
          → API Controllers
            → Frontend

## DTOs

### ImportReviewTransactionDto

Location: `src/Application/Import/Dto/ImportReviewTransactionDto.cs`

```csharp
namespace YoFi.V3.Application.Import.Dto;

/// <summary>
/// Presents information about an imported transaction for user review.
/// </summary>
/// <param name="Key">The unique identifier for the import review transaction.</param>
/// <param name="Date">Transaction date as reported by the bank.</param>
/// <param name="Payee">Payee or merchant name for the transaction.</param>
/// <param name="Category">Matched category (placeholder for future Payee Matching rules feature, empty for now).</param>
/// <param name="Amount">Transaction amount (positive for deposits, negative for withdrawals).</param>
/// <param name="DuplicateStatus">Status indicating whether this transaction is new or a duplicate.</param>
/// <param name="DuplicateOfKey">Key of the existing transaction if this is detected as a duplicate.</param>
/// <remarks>
/// <para>
/// This DTO contains only the fields necessary for the import review UI:
/// </para>
/// <list type="bullet">
/// <item><strong>Key</strong> - Unique identifier for selection tracking and accept/delete operations</item>
/// <item><strong>Date, Payee, Category, Amount</strong> - Displayed in the review table columns</item>
/// <item><strong>DuplicateStatus, DuplicateOfKey</strong> - Control visual highlighting and default checkbox state</item>
/// </list>
/// <para>
/// <strong>Fields NOT included</strong> (available in ImportReviewTransaction entity but not needed for UI):
/// </para>
/// <list type="bullet">
/// <item><strong>Source</strong> - Not displayed; user already knows which file/account they uploaded</item>
/// <item><strong>ExternalId</strong> - Internal duplicate detection field, not relevant for user review</item>
/// <item><strong>Memo</strong> - Not displayed in review UI to keep table simple; available after accepting transaction</item>
/// <item><strong>ImportedAt</strong> - No UI need; transactions already ordered by Date</item>
/// </list>
/// <para>
/// The Category field is displayed in the "Matched Category" column. For the initial implementation,
/// this will always be empty. In the future, it will be populated by the Payee Matching rules feature.
/// </para>
/// </remarks>
public record ImportReviewTransactionDto(
    Guid Key,
    DateOnly Date,
    string Payee,
    string Category,
    decimal Amount,
    DuplicateStatus DuplicateStatus,
    Guid? DuplicateOfKey
);
```

### ImportResultDto

Location: `src/Application/Import/Dto/ImportResultDto.cs`

```csharp
namespace YoFi.V3.Application.Import.Dto;

/// <summary>
/// Result of an OFX file import operation, to be shown to the user.
/// </summary>
/// <param name="ImportedCount">Number of transactions successfully imported and stored for review.</param>
/// <param name="NewCount">Number of new transactions (no duplicates detected).</param>
/// <param name="ExactDuplicateCount">Number of exact duplicate transactions detected.</param>
/// <param name="PotentialDuplicateCount">Number of potential duplicate transactions detected.</param>
/// <param name="Errors">Collection of errors encountered during OFX parsing.</param>
/// <remarks>
/// The sum of NewCount, ExactDuplicateCount, and PotentialDuplicateCount equals ImportedCount.
/// Errors indicate problems parsing individual transactions or the OFX file structure, but do not prevent
/// successfully parsed transactions from being imported.
/// </remarks>
public record ImportResultDto(
    int ImportedCount,
    int NewCount,
    int ExactDuplicateCount,
    int PotentialDuplicateCount,
    IReadOnlyCollection<OfxParsingError> Errors
);
```

### CompleteReviewResultDto

Location: `src/Application/Import/Dto/CompleteReviewResultDto.cs`

```csharp
namespace YoFi.V3.Application.Import.Dto;

/// <summary>
/// Result of completing the import review workflow. This is to show the user an informative display
/// of the outcome of the action they took on the reviewed transactions.
/// </summary>
/// <param name="AcceptedCount">Number of transactions successfully accepted and copied to the main transaction table.</param>
/// <param name="RejectedCount">Number of transactions rejected (not selected for import).</param>
/// <remarks>
/// <para>
/// This DTO represents the result of the CompleteReview operation, which atomically:
/// </para>
/// <list type="number">
/// <item>Accepts (imports) the selected transactions into the main Transaction table</item>
/// <item>Deletes ALL pending import review transactions (both selected and unselected)</item>
/// </list>
/// <para>
/// RejectedCount represents the transactions that were available for review but not selected
/// by the user. These transactions are deleted without being imported.
/// </para>
/// <para>
/// Example: If user selects 120 out of 150 transactions to accept, the result will be:
/// AcceptedCount = 120 (imported to main table), RejectedCount = 30 (deleted without importing).
/// </para>
/// </remarks>
public record CompleteReviewResultDto(
    int AcceptedCount,
    int RejectedCount
);
```

## Duplicate Detection Strategy

The ImportReviewFeature detects duplicates by matching the bank-provided transaction identifier (FITID).

### ExternalId (FITID) Matching

Check if the incoming `TransactionImportDto.ExternalId` matches any existing `Transaction.ExternalId` or pending `ImportReviewTransaction.ExternalId`:

- **ExternalId match found** with matching Date, Amount, and Payee → **ExactDuplicate**
  - Same bank transaction identifier (FITID) with identical transaction data
  - High confidence duplicate - should not be imported

- **ExternalId match found** with different Date, Amount, or Payee → **PotentialDuplicate**
  - Same bank identifier (FITID) but different transaction data
  - Could be a bank correction, data issue, or legitimate update
  - Requires user review to decide

- **No ExternalId match found** → **New**
  - Safe to import

### Rationale

**Why ExternalId (FITID) is sufficient:**
- `TransactionImportDto.ExternalId` contains the bank-provided FITID from the OFX file
- FITID uniquely identifies a transaction from the bank's perspective
- If FITID matches an existing `Transaction.ExternalId`, it's definitively the same transaction
- Banks are required to provide stable FITIDs for duplicate detection
- Even if other fields differ, matching FITIDs indicate the same underlying transaction

**Why potential duplicates need review:**
- Same ExternalId (FITID) with different data could be a bank correction (e.g., pending amount updated to final amount)
- User has context to make the right decision

**Why check both tables:**
- Checks existing Transaction table (prevents re-importing old transactions)
- Checks pending ImportReviewTransaction table (prevents importing same file twice in one session)
- Ensures comprehensive duplicate detection

### Field Mapping

**Incoming transaction (from OFX file):**
- `TransactionImportDto.ExternalId` - Bank-provided FITID from OFX file

**Existing data (check for duplicates against):**
- `Transaction.ExternalId` - Stored FITID from previously imported transactions
- `ImportReviewTransaction.ExternalId` - Stored FITID from current pending review

**Comparison:** Case-insensitive string match on ExternalId fields

### Performance Strategy

**Batch Query Approach:**
```sql
-- Single batch query for ALL imported transactions
-- Check Transaction table
SELECT ExternalId, Key, Date, Amount, Payee
FROM Transactions
WHERE TenantId = @tenantId
  AND ExternalId IN (@uniqueId1, @uniqueId2, ..., @uniqueIdN)

-- Check ImportReviewTransaction table
SELECT ExternalId, Key, Date, Amount, Payee
FROM ImportReviewTransactions
WHERE TenantId = @tenantId
  AND ExternalId IN (@uniqueId1, @uniqueId2, ..., @uniqueIdN)
```

**Algorithm:**
1. Parse entire OFX file to get all `TransactionImportDto` records
2. Execute batch queries with all ExternalIds (one for Transactions, one for ImportReviewTransactions)
3. Build in-memory lookups from results using `ILookup<string, Transaction>` and `ILookup<string, ImportReviewTransaction>` keyed by ExternalId (supports multiple transactions with same ExternalId)
4. For each imported transaction, lookup importDto.ExternalId in both lookups (O(1) key lookup, returns all matches)
5. For each match found, determine DuplicateStatus (ExactDuplicate, PotentialDuplicate, or New) based on field comparison
6. Create ImportReviewTransaction records with appropriate DuplicateStatus

**Performance Benefits:**
- **2 queries total** regardless of import size (one per table)
- No N+1 query problem
- IN clause uses index seek (efficient for batches of 100-1000)
- Tenant-scoped queries filter to small result sets
- ILookup key lookup is O(1), returns all matches for that key (handles multiple duplicates efficiently)
- For 500 transactions: ILookup = 500 O(1) lookups, Collection iteration = 500 * O(N) scans
- All matching happens in-memory after data retrieval (fast)
- Typical import (100-500 transactions) completes in milliseconds

**Required Indexes:**
- `IX_Transactions_TenantId_ExternalId` (composite)
- `IX_ImportReviewTransactions_TenantId_ExternalId` (composite)

**Why this performs well:**
- Constant query count regardless of import size
- Indexes provide efficient lookups
- Database query optimizer can cache execution plans
- Minimal data transfer (only potential matches returned)

### Example Scenarios

| Scenario | UniqueId Match | Field Match | Result | Reason |
|----------|----------------|-------------|--------|--------|
| Same FITID, same data | ✅ Yes | ✅ Yes | **ExactDuplicate** | Definite duplicate, don't import |
| Same FITID, different amount | ✅ Yes | ❌ No | **PotentialDuplicate** | Bank correction? User decides |
| Different FITID | ❌ No | N/A | **New** | Safe to import |

## ImportReviewFeature Class

Location: `src/Application/Import/Features/ImportReviewFeature.cs`

```csharp
using YoFi.V3.Application.Import.Dto;
using YoFi.V3.Application.Import.Services;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Providers;
using YoFi.V3.Entities.Tenancy.Providers;

namespace YoFi.V3.Application.Import.Features;

/// <summary>
/// Provides business logic for the bank import review workflow.
/// </summary>
/// <param name="tenantProvider">Provider for accessing tenant context information.</param>
/// <param name="dataProvider">Repository for data operations on import review transactions.</param>
/// <param name="transactionsFeature">Feature for managing transactions (used when accepting imports).</param>
/// <param name="ofxParsingService">Service for parsing OFX/QFX files.</param>
/// <remarks>
/// <para>
/// This feature orchestrates the complete import workflow:
/// </para>
/// <list type="number">
/// <item>Parse OFX file to extract transaction data</item>
/// <item>Detect duplicates against existing transactions and pending imports</item>
/// <item>Store transactions in ImportReviewTransaction staging table with duplicate status</item>
/// <item>Provide operations to retrieve pending review transactions and complete the review workflow</item>
/// </list>
/// <para>
/// <strong>Tenant Isolation:</strong> All operations are scoped to the current authenticated user's tenant
/// via ITenantProvider. The TenantId is automatically applied to all queries and inserts.
/// </para>
/// <para>
/// <strong>Duplicate Detection:</strong> Uses a two-phase strategy (ExternalId matching, then field matching)
/// to classify transactions as New, ExactDuplicate, or PotentialDuplicate. See DetectDuplicate method for details.
/// </para>
/// <para>
/// <strong>Transaction Creation via TransactionsFeature:</strong> When accepting transactions, this feature
/// delegates to TransactionsFeature.AddTransactionAsync() to ensure consistent transaction creation including
/// default splits. This follows clean architecture principles by reusing existing business logic rather than
/// duplicating transaction creation code.
/// </para>
/// </remarks>
public class ImportReviewFeature(
    ITenantProvider tenantProvider,
    IDataProvider dataProvider,
    TransactionsFeature transactionsFeature,
    IOfxParsingService ofxParsingService)
{
    /// <summary>
    /// Imports an OFX file, parses transactions, detects duplicates, and stores them for review.
    /// </summary>
    /// <param name="fileStream">The stream containing the OFX/QFX file data.</param>
    /// <param name="fileName">The name of the uploaded file.</param>
    /// <returns>An <see cref="ImportResultDto"/> containing import statistics and any parsing errors.</returns>
    /// <remarks>
    /// All operations are scoped to the current tenant via ITenantProvider.TenantId.
    /// If parsing errors occur, they are included in the OFXParsingResult but do not prevent
    /// import of successfully parsed transactions.
    /// </remarks>
    public async Task<ImportResultDto> ImportFileAsync(Stream fileStream, string fileName)
    {
        // Parse the OFX file using OfxParsingService

        // Execute batch queries to check for duplicate ExternalIds (FITID) in both Transaction and ImportReviewTransaction tables

        // For each parsed transaction, detect duplicates using ExternalId (FITID) matching strategy

        // Create ImportReviewTransaction records with appropriate DuplicateStatus

        // Store all import review transactions in the database

        // Return summary statistics and any parsing errors
    }

    /// <summary>
    /// Retrieves all pending import review transactions for the current tenant.
    /// </summary>
    /// <returns>A collection of <see cref="ImportReviewTransactionDto"/> ordered by date descending.</returns>
    public async Task<IReadOnlyCollection<ImportReviewTransactionDto>> GetPendingReviewAsync()
    {
        // Query all ImportReviewTransaction records for current tenant

        // Order by date descending

        // Map to ImportReviewTransactionDto and return
    }

    /// <summary>
    /// Completes the import review by accepting selected transactions and deleting all pending review transactions.
    /// </summary>
    /// <param name="keys">The collection of transaction keys to accept (import into main transaction table).</param>
    /// <returns>A <see cref="CompleteReviewResultDto"/> with counts of accepted and rejected transactions.</returns>
    /// <remarks>
    /// <para>
    /// <strong>Why use TransactionsFeature?</strong> Follows clean architecture principles by reusing existing
    /// transaction creation logic. TransactionsFeature handles:
    /// </para>
    /// <list type="bullet">
    /// <item>Default split creation (Amount = transaction.Amount, Category = empty, Order = 0)</item>
    /// <item>Category sanitization via CategoryHelper</item>
    /// <item>Tenant assignment</item>
    /// <item>Database persistence</item>
    /// </list>
    /// <para>
    /// <strong>Why delete all transactions?</strong> This matches the UI workflow where clicking "Import"
    /// completes the review session. Unselected transactions are rejected (not imported but deleted),
    /// preventing orphaned transactions from accumulating and providing a clean slate for the next import.
    /// </para>
    /// <para>
    /// <strong>Example:</strong> If review table has 150 transactions and user selects 120 to accept:
    /// </para>
    /// <list type="bullet">
    /// <item>120 transactions are copied to main Transaction table</item>
    /// <item>All 150 transactions are deleted from ImportReviewTransaction table</item>
    /// <item>Result: AcceptedCount = 120, RejectedCount = 30</item>
    /// </list>
    /// </remarks>
    public async Task<CompleteReviewResultDto> CompleteReviewAsync(IReadOnlyCollection<Guid> keys)
    {
        // Retrieve import review transactions from database by keys (with tenant isolation)

        // For each selected transaction, convert to TransactionEditDto and call TransactionsFeature.AddTransactionAsync()

        // Delete ALL import review transactions for the current tenant (not just the selected ones)

        // Return counts of accepted and rejected transactions
    }

    /// <summary>
    /// Deletes all pending import review transactions for the current tenant without accepting any.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use this method when the user wants to completely cancel/discard the current import
    /// without accepting any transactions (the "Delete All" functionality).
    /// </para>
    /// <para>
    /// This is distinct from CompleteReview, which accepts selected transactions before deleting all.
    /// </para>
    /// </remarks>
    public async Task DeleteAllAsync()
    {
        // Delete all ImportReviewTransaction records for the current tenant
    }

    /// <summary>
    /// Detects duplicate status for a single imported transaction by checking it against existing data.
    /// </summary>
    /// <param name="importDto">The imported transaction to check.</param>
    /// <param name="existingTransactionsByExternalId">Lookup of existing transactions grouped by ExternalId (from batch query).</param>
    /// <param name="pendingImportsByExternalId">Lookup of pending imports grouped by ExternalId (from batch query).</param>
    /// <returns>
    /// A tuple containing the duplicate status and the key of the duplicate transaction (if any).
    /// Returns (DuplicateStatus.New, null) if no duplicates are found.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This is a helper method called once per imported transaction. The lookups passed to this method
    /// are built from batch query results in ImportFileAsync - this method performs O(1) key lookups only.
    /// </para>
    /// <para>
    /// Performs O(1) lookup of importDto.ExternalId (FITID) in both lookups, which returns all transactions
    /// with that ExternalId (handles multiple duplicates). For each match found, compares Date/Amount/Payee
    /// to determine ExactDuplicate vs PotentialDuplicate. See "Duplicate Detection Strategy" section for
    /// detailed logic and example scenarios.
    /// </para>
    /// </remarks>
    private static (DuplicateStatus Status, Guid? DuplicateOfKey) DetectDuplicate(
        TransactionImportDto importDto,
        ILookup<string, Transaction> existingTransactionsByExternalId,
        ILookup<string, ImportReviewTransaction> pendingImportsByExternalId);
}
```

## References

**Design Documents:**
- [`DESIGN-BANK-IMPORT.md`](DESIGN-BANK-IMPORT.md) - Overall feature architecture
- [`DESIGN-BANK-IMPORT-DATABASE.md`](DESIGN-BANK-IMPORT-DATABASE.md) - Entity model and data layer design
- [`PRD-BANK-IMPORT.md`](PRD-BANK-IMPORT.md) - Product requirements

**Related Code:**
- [`OfxParsingService.cs`](src/Application/Import/Services/OfxParsingService.cs) - OFX file parsing (already implemented)
- [`TransactionImportDto.cs`](src/Application/Import/Dto/TransactionImportDto.cs) - Parsed transaction DTO
- [`TenantFeature.cs`](src/Application/Tenancy/Features/TenantFeature.cs) - Reference Feature implementation pattern
- [`ServiceCollectionExtensions.cs`](src/Application/ServiceCollectionExtensions.cs) - Service registration

**Architecture:**
- [`docs/ARCHITECTURE.md`](../../ARCHITECTURE.md) - Clean Architecture layers
- [`docs/wip/APPLICATION-FEATURE-RETURN-VALUES.md`](../APPLICATION-FEATURE-RETURN-VALUES.md) - Collection return type guidance

## Future Considerations

### Field-Level Duplicate Detection (Consider for future)

**Current Status:** Not implemented in initial release

**Potential Enhancement:** Add fallback duplicate detection for transactions without reliable FITIDs.

**Approach:**
- If no ExternalId (FITID) match found, check for matching Date + Amount + Payee
- Mark as PotentialDuplicate if all three fields match
- Requires additional composite indexes: `IX_Transactions_TenantId_Date_Amount_Payee`

**Pros:**
- Catches duplicates when banks don't provide reliable FITIDs
- Provides additional safety net for duplicate detection

**Cons:**
- Adds complexity to duplicate detection logic
- Requires additional database indexes (storage + maintenance overhead)
- Field matching is less reliable (legitimate transactions can have same date/amount/payee)
- More false positives requiring user review
- Performance impact on field-level queries

**Decision:** Defer until user feedback indicates FITID-only detection is insufficient. Most banks provide reliable FITIDs per OFX specification, making field-level matching unnecessary for the majority of use cases.

**Implementation Notes (if needed in future):**
- Use batch query with OR conditions: `(Date = @d1 AND Amount = @a1 AND Payee = @p1) OR ...`
- Add composite indexes before deploying
- Consider making it optional/configurable per tenant
- Monitor false positive rate and adjust matching criteria if needed
