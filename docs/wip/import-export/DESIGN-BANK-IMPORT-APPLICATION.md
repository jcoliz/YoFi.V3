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
- **DTOs** - [`ImportReviewTransactionDto`](src/Application/Import/Dto/ImportReviewTransactionDto.cs), [`ImportResultDto`](src/Application/Import/Dto/ImportResultDto.cs), [`CompleteReviewResultDto`](src/Application/Import/Dto/CompleteReviewResultDto.cs), [`PaginatedResultDto<T>`](src/Application/Common/Dto/PaginatedResultDto.cs)
- **ImportReviewFeature** - Business logic for import workflow orchestration
- **Duplicate Detection Strategy** - Two-phase detection using ExternalId and field matching
- **Service Registration** - Integration with [`ServiceCollectionExtensions`](src/Application/ServiceCollectionExtensions.cs)

**Layer responsibilities:**
- Parse OFX files using [`OfxParsingService`](src/Application/Import/Services/OfxParsingService.cs) (already implemented)
- Detect duplicates against existing transactions and pending imports
- Store parsed transactions in [`ImportReviewTransaction`](src/Entities/Models/ImportReviewTransaction.cs) staging table
- Provide paginated read operations for pending review transactions
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

Later, frontend requests review data with pagination:
Frontend
  → API Controllers (GET /api/import/review?pageNumber=1&pageSize=50)
    → ImportReviewFeature.GetPendingReviewAsync(pageNumber, pageSize)
      → Database query for ImportReviewTransaction entities with pagination
        → PaginatedResultDto<ImportReviewTransactionDto> (mapped and returned)
          → API Controllers
            → Frontend

## DTOs

### ImportReviewTransactionDto

Location: `src/Application/Import/Dto/ImportReviewTransactionDto.cs`

**Purpose:** Presents information about an imported transaction for user review.

**Fields included (necessary for import review UI):**
- **Key** - Unique identifier for selection tracking and accept/delete operations
- **Date, Payee, Category, Amount** - Displayed in the review table columns
- **DuplicateStatus, DuplicateOfKey** - Control visual highlighting and default checkbox state

**Fields NOT included** (available in ImportReviewTransaction entity but not needed for UI):
- **Source** - Not displayed; user already knows which file/account they uploaded
- **ExternalId** - Internal duplicate detection field, not relevant for user review
- **Memo** - Not displayed in review UI to keep table simple; available after accepting transaction

**Category field:** Displayed in the "Matched Category" column. For the initial implementation, this will always be empty. In the future, it will be populated by the Payee Matching rules feature.

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

**Purpose:** Result of completing the import review workflow, showing the user an informative display of the outcome.

**CompleteReview operation behavior:** The operation atomically:
1. Accepts (imports) the selected transactions into the main Transaction table
2. Deletes ALL pending import review transactions (both selected and unselected)

**RejectedCount meaning:** Represents the transactions that were available for review but not selected by the user. These transactions are deleted without being imported.

**Example:** If user selects 120 out of 150 transactions to accept:
- AcceptedCount = 120 (imported to main table)
- RejectedCount = 30 (deleted without importing)

```csharp
namespace YoFi.V3.Application.Import.Dto;

/// <summary>
/// Result of completing the import review workflow.
/// </summary>
/// <param name="AcceptedCount">Number of transactions successfully accepted and copied to the main transaction table.</param>
/// <param name="RejectedCount">Number of transactions rejected (not selected for import).</param>
public record CompleteReviewResultDto(
    int AcceptedCount,
    int RejectedCount
);
```

### PaginatedResultDto<T>

Location: `src/Application/Common/Dto/PaginatedResultDto.cs`

**Purpose:** Generic paginated result container for API responses.

**Supported UI patterns:**
- **Page navigation** - Use PageNumber, TotalPages, HasPreviousPage, HasNextPage for numbered page controls
- **Infinite scroll** - Use HasNextPage to determine if more data should be loaded
- **Load more button** - Use HasNextPage to show/hide the "Load More" button
- **Progress display** - Use "Showing {(PageNumber-1)*PageSize + 1}-{min(PageNumber*PageSize, TotalCount)} of {TotalCount}"

**Metadata calculations:**
- TotalPages = ceiling(TotalCount / PageSize)
- HasPreviousPage = PageNumber > 1
- HasNextPage = PageNumber < TotalPages

```csharp
namespace YoFi.V3.Application.Common.Dto;

/// <summary>
/// Generic paginated result container for API responses.
/// </summary>
/// <typeparam name="T">The type of items in the paginated collection.</typeparam>
/// <param name="Items">The collection of items for the current page.</param>
/// <param name="PageNumber">The current page number (1-based).</param>
/// <param name="PageSize">The number of items per page.</param>
/// <param name="TotalCount">The total number of items across all pages.</param>
/// <param name="TotalPages">The total number of pages available.</param>
/// <param name="HasPreviousPage">Indicates whether a previous page exists.</param>
/// <param name="HasNextPage">Indicates whether a next page exists.</param>
public record PaginatedResultDto<T>(
    IReadOnlyCollection<T> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasPreviousPage,
    bool HasNextPage
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

**Batch Query Approach (SQL):**
```sql
-- Optimized queries that select only the most recent transaction per ExternalId
-- Check Transaction table
SELECT ExternalId, Key, Date, Amount, Payee
FROM (
    SELECT ExternalId, Key, Date, Amount, Payee,
           ROW_NUMBER() OVER (PARTITION BY ExternalId ORDER BY Date DESC) AS rn
    FROM Transactions
    WHERE TenantId = @tenantId
      AND ExternalId IN (@uniqueId1, @uniqueId2, ..., @uniqueIdN)
) AS ranked
WHERE rn = 1

-- Check ImportReviewTransaction table
SELECT ExternalId, Key, Date, Amount, Payee
FROM (
    SELECT ExternalId, Key, Date, Amount, Payee,
           ROW_NUMBER() OVER (PARTITION BY ExternalId ORDER BY Date DESC) AS rn
    FROM ImportReviewTransactions
    WHERE TenantId = @tenantId
      AND ExternalId IN (@uniqueId1, @uniqueId2, ..., @uniqueIdN)
) AS ranked
WHERE rn = 1
```

**Equivalent LINQ Queries (Server-Side Translation):**
```csharp
// Get most recent existing transaction per ExternalId
// Using subquery approach that translates reliably to SQL
var existingTransactions = await dataProvider
    .Query<Transaction>()
    .Where(t => t.TenantId == tenantId && externalIds.Contains(t.ExternalId))
    .Where(t => !dataProvider.Query<Transaction>()
        .Any(t2 => t2.TenantId == tenantId
                && t2.ExternalId == t.ExternalId
                && t2.Date > t.Date))  // No other transaction with same ExternalId and later date
    .ToDictionaryAsync(t => t.ExternalId, t => t);

// Get most recent pending import per ExternalId
var pendingImports = await dataProvider
    .Query<ImportReviewTransaction>()
    .Where(t => t.TenantId == tenantId && externalIds.Contains(t.ExternalId))
    .Where(t => !dataProvider.Query<ImportReviewTransaction>()
        .Any(t2 => t2.TenantId == tenantId
                && t2.ExternalId == t.ExternalId
                && t2.Date > t.Date))
    .ToDictionaryAsync(t => t.ExternalId, t => t);
```

**Note:** These queries use a subquery with NOT EXISTS pattern that reliably translates to efficient SQL in all EF Core versions. The pattern selects transactions where no other transaction exists with the same ExternalId and a later Date, effectively selecting the most recent per ExternalId.

**Translation:** EF Core translates this to SQL similar to:
```sql
SELECT * FROM Transactions t1
WHERE TenantId = @tenantId
  AND ExternalId IN (...)
  AND NOT EXISTS (
    SELECT 1 FROM Transactions t2
    WHERE t2.TenantId = t1.TenantId
      AND t2.ExternalId = t1.ExternalId
      AND t2.Date > t1.Date
  )
```

This approach is semantically equivalent to the ROW_NUMBER() window function but translates reliably across all EF Core versions and database providers.

**Algorithm:**
1. Parse entire OFX file to get all `TransactionImportDto` records
2. Execute batch queries with all ExternalIds (one for Transactions, one for ImportReviewTransactions)
   - Queries use ROW_NUMBER() window function to select only the most recent transaction per ExternalId
3. Build in-memory dictionaries from results using `Dictionary<string, Transaction>` and `Dictionary<string, ImportReviewTransaction>` keyed by ExternalId
4. For each imported transaction, lookup importDto.ExternalId in both dictionaries (O(1) key lookup, returns single most recent match)
5. For the match found (if any), determine DuplicateStatus (ExactDuplicate, PotentialDuplicate, or New) based on field comparison
6. Create ImportReviewTransaction records with appropriate DuplicateStatus

**Performance Benefits:**
- **2 queries total** regardless of import size (one per table)
- No N+1 query problem
- IN clause uses index seek (efficient for batches of 100-1000)
- ROW_NUMBER() window function efficiently selects most recent per ExternalId
- Tenant-scoped queries filter to small result sets
- Dictionary key lookup is O(1), returns single most recent match
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

**Import workflow orchestration:**
1. Parse OFX file to extract transaction data
2. Detect duplicates against existing transactions and pending imports
3. Store transactions in ImportReviewTransaction staging table with duplicate status
4. Provide operations to retrieve pending review transactions and complete the review workflow

**Tenant Isolation:** All operations are scoped to the current authenticated user's tenant via ITenantProvider. The TenantId is automatically applied to all queries and inserts.

**Duplicate Detection:** Uses a two-phase strategy (ExternalId matching, then field matching) to classify transactions as New, ExactDuplicate, or PotentialDuplicate. See DetectDuplicate method for details.

**Transaction Creation via TransactionsFeature:** When accepting transactions, this feature delegates to TransactionsFeature.AddTransactionAsync() to ensure consistent transaction creation including default splits. This follows clean architecture principles by reusing existing business logic rather than duplicating transaction creation code.

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
    /// Retrieves pending import review transactions for the current tenant with pagination support.
    /// </summary>
    /// <param name="pageNumber">The page number to retrieve (1-based, default: 1).</param>
    /// <param name="pageSize">The number of items per page (default: 50, maximum: 1000).</param>
    /// <returns>A <see cref="PaginatedResultDto{T}"/> containing transactions and pagination metadata.</returns>
    public async Task<PaginatedResultDto<ImportReviewTransactionDto>> GetPendingReviewAsync(
        int pageNumber = 1,
        int pageSize = 50)
    {
        // Validate and normalize pagination parameters

        // Query total count of ImportReviewTransaction records for current tenant

        // Calculate pagination metadata (totalPages, hasPreviousPage, hasNextPage)

        // Query paginated ImportReviewTransaction records using SKIP/TAKE

        // Order by date descending

        // Map to ImportReviewTransactionDto

        // Return PaginatedResultDto with items and metadata
    }

    /// <summary>
    /// Completes the import review by accepting selected transactions and deleting all pending review transactions.
    /// </summary>
    /// <param name="keys">The collection of transaction keys to accept (import into main transaction table).</param>
    /// <returns>A <see cref="CompleteReviewResultDto"/> with counts of accepted and rejected transactions.</returns>
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
    public async Task DeleteAllAsync()
    {
        // Delete all ImportReviewTransaction records for the current tenant
    }

    /// <summary>
    /// Detects duplicate status for a single imported transaction by checking it against existing data.
    /// </summary>
    /// <param name="importDto">The imported transaction to check.</param>
    /// <param name="existingTransactionsByExternalId">Dictionary of most recent existing transactions by ExternalId (from batch query with ROW_NUMBER filter).</param>
    /// <param name="pendingImportsByExternalId">Dictionary of most recent pending imports by ExternalId (from batch query with ROW_NUMBER filter).</param>
    /// <returns>
    /// A tuple containing the duplicate status and the key of the duplicate transaction (if any).
    /// Returns (DuplicateStatus.New, null) if no duplicates are found.
    /// </returns>
    internal static (DuplicateStatus Status, Guid? DuplicateOfKey) DetectDuplicate(
        TransactionImportDto importDto,
        IReadOnlyDictionary<string, Transaction> existingTransactionsByExternalId,
        IReadOnlyDictionary<string, ImportReviewTransaction> pendingImportsByExternalId);
}
```

### GetPendingReviewAsync Implementation Notes

**Pagination parameter validation:**
- pageNumber < 1 → Defaults to 1
- pageSize < 1 → Defaults to 50
- pageSize > 1000 → Clamped to 1000 (prevents excessive data transfer)

**Ordering:** Transactions are ordered by Date descending (newest first).

**Empty pages:** If the requested page number exceeds available pages, an empty Items collection is returned with accurate pagination metadata.

### CompleteReviewAsync Implementation Notes

**Why use TransactionsFeature?** Follows clean architecture principles by reusing existing transaction creation logic. TransactionsFeature handles:
- Default split creation (Amount = transaction.Amount, Category = empty, Order = 0)
- Category sanitization via CategoryHelper
- Tenant assignment
- Database persistence

**Why delete all transactions?** This matches the UI workflow where clicking "Import" completes the review session. Unselected transactions are rejected (not imported but deleted), preventing orphaned transactions from accumulating and providing a clean slate for the next import.

**Example:** If review table has 150 transactions and user selects 120 to accept:
- 120 transactions are copied to main Transaction table
- All 150 transactions are deleted from ImportReviewTransaction table
- Result: AcceptedCount = 120, RejectedCount = 30

### DeleteAllAsync Implementation Notes

Use this method when the user wants to completely cancel/discard the current import without accepting any transactions (the "Delete All" functionality). This is distinct from CompleteReview, which accepts selected transactions before deleting all.

### DetectDuplicate Implementation Notes

This is a helper method called once per imported transaction. The dictionaries passed to this method are built from batch query results in `ImportFileAsync` - the database queries use ROW_NUMBER() window functions to select only the most recent transaction per ExternalId, so this method performs simple O(1) dictionary lookups.

**Algorithm:**
1. Perform O(1) lookup of `importDto.ExternalId` (FITID) in `existingTransactionsByExternalId` dictionary
2. If found, compare Date/Amount/Payee to determine ExactDuplicate vs PotentialDuplicate
3. If not found in existing transactions, check `pendingImportsByExternalId` dictionary
4. If found in pending imports, compare fields to determine duplicate status
5. If not found in either dictionary, return DuplicateStatus.New

**Handling Multiple Duplicate Matches:**

The database queries (using ROW_NUMBER() with ORDER BY Date DESC) pre-select the **most recent transaction** per ExternalId before data reaches this method. This optimization:
- Simplifies the duplicate detection logic (single match per ExternalId)
- Ensures the most relevant duplicate is selected (most recent by date)
- Eliminates the need for in-memory multiple-match handling
- Improves performance (smaller dictionaries, faster lookups)

The batch query approach handles the rare edge case of multiple duplicates with the same ExternalId at the database level, providing a clean single-match result to the application logic.

See "Duplicate Detection Strategy" section for detailed matching logic and example scenarios.

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
