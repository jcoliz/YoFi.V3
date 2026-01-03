---
status: Implemented
created: 2024-12-31
issue_type: Design Flaw
severity: Critical
related_docs:
  - DESIGN-BANK-IMPORT-FRONTEND.md
  - DESIGN-BANK-IMPORT-DATABASE.md
  - DESIGN-BANK-IMPORT-API.md
  - DESIGN-BANK-IMPORT-APPLICATION.md
---

# Import Selection State Design Flaw Analysis

## Problem Statement

The current bank import design stores transaction selection state (checkboxes) in **frontend session storage**. This creates a critical flaw: when importing hundreds or thousands of transactions, the frontend only loads the first page (e.g., 50 transactions), so **only the first page's selections are persisted**. When the user clicks "Import," unloaded transactions beyond page 1 are not selected, resulting in mass rejection of valid transactions.

## Reproduction Scenario

**Steps to reproduce:**

1. User uploads OFX file with 300 transactions
2. Backend parses and stores all 300 in `ImportReviewTransactions` table
3. Frontend loads page 1 (50 transactions)
4. Frontend applies default selections: New transactions selected, duplicates deselected
5. **Frontend only knows about page 1 (50 transactions)** - stores 40 selected keys in session storage
6. User reviews page 1, clicks "Import"
7. Frontend sends selected keys array: `[key1, key2, ..., key40]` (only from page 1)
8. Backend accepts 40 transactions, deletes all 300
9. **Result: 40 accepted, 260 rejected** (expected: ~250 accepted, ~50 duplicates rejected)

## Root Cause Analysis

### Current Design (Flawed)

**Selection state location:** Frontend session storage (per-tenant key)

**How default selections work:**
```typescript
// Frontend loads page 1 only
const paginatedResult = await importClient.getPendingReview(1, 50)

// Frontend sets default selections ONLY for loaded transactions
paginatedResult.items.forEach(tx => {
  if (tx.duplicateStatus === DuplicateStatus.New) {
    selectedKeys.add(tx.key)  // Only 50 transactions max!
  }
})

// Session storage saves selected keys
sessionStorage.setItem(`import-review-selections-${tenantKey}`, JSON.stringify([...selectedKeys]))
```

**Why it fails:**
- Pagination is **lazy** - frontend doesn't load all pages
- Default selections applied only to **loaded transactions**
- Unloaded transactions (pages 2, 3, 4+) have **no selection state**
- When user clicks "Import," only loaded selections are sent to backend
- Backend interprets missing keys as **rejection** (not selected)

### Why Session Storage Was Chosen (Original Rationale)

From [`DESIGN-BANK-IMPORT-FRONTEND.md`](DESIGN-BANK-IMPORT-FRONTEND.md#session-storage-pattern):

> **Why Session Storage Over Database**
>
> **Rationale:**
> - Selection state is **UI state**, not domain data
> - No need for server-side tracking or persistence
> - Simpler implementation (no API calls on every checkbox toggle)
> - Automatically cleared when user closes browser/tab
> - Works offline (no network dependency for checkbox changes)
> - No database migrations or additional tables required

**This rationale is WRONG for paginated data.** It only works when all data is loaded into memory (non-paginated scenarios).

## Why This Is Critical

### User Impact

**Worst-case scenario (real-world):**
- User uploads annual credit card statement: 1,200 transactions
- 1,000 are new (should be imported)
- 200 are duplicates (should be rejected)
- Frontend loads page 1 (50 transactions): 40 new, 10 duplicates
- User clicks "Import" after reviewing page 1
- **Result: 40 imported, 1,160 rejected** (96% data loss!)

**User expectation:**
- Default selections should apply to **ALL transactions**, not just page 1
- "Import" should accept **all selected transactions across all pages**

**Actual behavior:**
- Only page 1 transactions can be selected
- Pages 2+ transactions are always rejected (never loaded)

### Data Consistency

The `DuplicateStatus` field in the database indicates **which transactions should be selected by default**:
- `New` → Selected by default (should import)
- `ExactDuplicate` → Deselected by default (should reject)
- `PotentialDuplicate` → Deselected by default (needs review)

**With frontend-only state, this metadata is useless for unloaded pages.** The database knows the correct default state, but the frontend can't apply it without loading every page.

## Solution Requirements

### Must-Have Properties

1. **Server-side truth:** Selection state stored in database alongside transaction data
2. **Default selection persistence:** When transactions are imported (POST /upload), server sets default `IsSelected` based on `DuplicateStatus`
3. **Paginated selection queries:** GET /review returns `IsSelected` flag with each transaction
4. **Selective toggle:** API endpoint to toggle selection for specific transactions (user overrides)
5. **Bulk operations:** API endpoints for "Select All" and "Deselect All"
6. **Accept uses database state:** POST /review/complete reads `IsSelected` from database, not from request body

### Design Principles

1. **Database is source of truth** - Selection state lives with transaction data
2. **Default selections automatic** - Server applies defaults based on `DuplicateStatus` on import
3. **User overrides explicit** - API calls to toggle/bulk-change selections
4. **No client-side state management** - Frontend renders what server provides
5. **Pagination-safe** - Selection state exists for ALL transactions, not just loaded ones

## Proposed Solution

### Database Schema Changes

Add `IsSelected` column to `ImportReviewTransaction` table:

```csharp
public record ImportReviewTransaction : BaseTenantModel
{
    // ... existing fields ...

    /// <summary>
    /// Indicates whether this transaction is selected for import.
    /// Set automatically based on DuplicateStatus when transaction is created.
    /// Can be toggled by user during review.
    /// </summary>
    public bool IsSelected { get; set; }
}
```

**Default value logic (set on insert):**
- `DuplicateStatus.New` → `IsSelected = true`
- `DuplicateStatus.ExactDuplicate` → `IsSelected = false`
- `DuplicateStatus.PotentialDuplicate` → `IsSelected = false`

**EF Core migration:** Add non-nullable `IsSelected` column with default value `true` (safe for existing data).

### API Changes

#### Modified Endpoints

**GET /api/tenant/{tenantKey}/import/review** - Add `IsSelected` to response

Response now includes selection state:
```json
{
  "items": [
    {
      "key": "guid1",
      "date": "2024-01-15",
      "payee": "Amazon",
      "amount": -50.00,
      "duplicateStatus": "New",
      "isSelected": true  // ← NEW FIELD
    }
  ],
  "pageNumber": 1,
  "pageSize": 50,
  "totalCount": 150
}
```

**POST /api/tenant/{tenantKey}/import/review/complete** - Remove request body

Before (FLAWED):
```typescript
// Frontend sends selected keys (only loaded transactions!)
await importClient.completeReview([key1, key2, key3, ...])
```

After (CORRECT):
```typescript
// No parameters - backend reads IsSelected from database
await importClient.completeReview()
```

Backend implementation:
```csharp
public async Task<IActionResult> CompleteReview()
{
    // Query ALL transactions where IsSelected = true (across all pages)
    var selectedTransactions = await importReviewFeature.GetSelectedTransactionsAsync();

    // Accept selected transactions
    await importReviewFeature.AcceptTransactionsAsync(selectedTransactions);

    // Delete ALL transactions (cleanup review table)
    await importReviewFeature.DeleteAllAsync();

    return Ok(new CompleteReviewResultDto(...));
}
```

#### New Endpoints

**POST /api/tenant/{tenantKey}/import/review/toggle** - Toggle selection for specific transaction(s)

Request:
```json
{
  "keys": ["guid1", "guid2"]
}
```

Response: 204 No Content

Backend:
```csharp
[HttpPost("review/toggle")]
public async Task<IActionResult> ToggleSelection([FromBody] IReadOnlyCollection<Guid> keys)
{
    await importReviewFeature.ToggleSelectionAsync(keys);
    return NoContent();
}
```

**POST /api/tenant/{tenantKey}/import/review/select-all** - Select all transactions

Response: 204 No Content

Backend:
```csharp
[HttpPost("review/select-all")]
public async Task<IActionResult> SelectAll()
{
    await importReviewFeature.SelectAllAsync();
    return NoContent();
}
```

**POST /api/tenant/{tenantKey}/import/review/deselect-all** - Deselect all transactions

Response: 204 No Content

Backend:
```csharp
[HttpPost("review/deselect-all")]
public async Task<IActionResult> DeselectAll()
{
    await importReviewFeature.DeselectAllAsync();
    return NoContent();
}
```

### Application Layer Changes

#### ImportReviewFeature New Methods

```csharp
/// <summary>
/// Toggles the selection state for the specified transactions.
/// </summary>
public async Task ToggleSelectionAsync(IReadOnlyCollection<Guid> keys)
{
    var transactions = await dataProvider
        .Query<ImportReviewTransaction>()
        .Where(t => t.TenantId == tenantId && keys.Contains(t.Key))
        .ToListAsync();

    foreach (var tx in transactions)
    {
        tx.IsSelected = !tx.IsSelected;
    }

    await dataProvider.SaveChangesAsync();
}

/// <summary>
/// Selects all pending import review transactions for the current tenant.
/// </summary>
public async Task SelectAllAsync()
{
    await dataProvider
        .Query<ImportReviewTransaction>()
        .Where(t => t.TenantId == tenantId)
        .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.IsSelected, true));
}

/// <summary>
/// Deselects all pending import review transactions for the current tenant.
/// </summary>
public async Task DeselectAllAsync()
{
    await dataProvider
        .Query<ImportReviewTransaction>()
        .Where(t => t.TenantId == tenantId)
        .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.IsSelected, false));
}

/// <summary>
/// Retrieves all selected transactions for the current tenant (across all pages).
/// </summary>
private async Task<IReadOnlyCollection<ImportReviewTransaction>> GetSelectedTransactionsAsync()
{
    return await dataProvider
        .Query<ImportReviewTransaction>()
        .Where(t => t.TenantId == tenantId && t.IsSelected)
        .ToListAsync();
}
```

#### ImportReviewFeature Modified Methods

**ImportFileAsync** - Set default `IsSelected` on insert:

```csharp
public async Task<ImportResultDto> ImportFileAsync(Stream fileStream, string fileName)
{
    // ... parse OFX, detect duplicates ...

    var reviewTransactions = parsedTransactions.Select(dto => new ImportReviewTransaction
    {
        // ... map fields ...
        DuplicateStatus = detectedStatus,

        // Set default selection based on duplicate status
        IsSelected = detectedStatus == DuplicateStatus.New
    });

    await dataProvider.AddRangeAsync(reviewTransactions);
    await dataProvider.SaveChangesAsync();

    // ...
}
```

**CompleteReviewAsync** - Read selections from database:

```csharp
public async Task<CompleteReviewResultDto> CompleteReviewAsync()
{
    // Query selected transactions from database (not from parameter)
    var selectedTransactions = await GetSelectedTransactionsAsync();

    // Accept selected transactions
    foreach (var tx in selectedTransactions)
    {
        var editDto = MapToTransactionEditDto(tx);
        await transactionsFeature.AddTransactionAsync(editDto);
    }

    // Count total before deletion
    var totalCount = await dataProvider
        .Query<ImportReviewTransaction>()
        .Where(t => t.TenantId == tenantId)
        .CountAsync();

    // Delete all review transactions
    await DeleteAllAsync();

    return new CompleteReviewResultDto(
        AcceptedCount: selectedTransactions.Count,
        RejectedCount: totalCount - selectedTransactions.Count
    );
}
```

#### ImportReviewTransactionDto Modified

Add `IsSelected` property:

```csharp
public record ImportReviewTransactionDto(
    Guid Key,
    DateOnly Date,
    string Payee,
    string Category,
    decimal Amount,
    DuplicateStatus DuplicateStatus,
    Guid? DuplicateOfKey,
    bool IsSelected  // ← NEW FIELD
);
```

### Frontend Changes

#### Remove Session Storage Pattern

Delete all session storage code:
- No `sessionStorage.setItem()`
- No `sessionStorage.getItem()`
- No local `selectedKeys` Set

#### Render Server State

```typescript
// Component state now reflects server state
const transactions = ref<ImportReviewTransactionDto[]>([])

// Load page data
const loadPage = async (pageNumber: number) => {
  const result = await importClient.getPendingReview(pageNumber, 50)
  transactions.value = result.items  // Each has isSelected property
}

// Checkbox rendering
<input
  type="checkbox"
  :checked="transaction.isSelected"
  @change="handleToggle(transaction.key)"
/>

// Toggle handler - calls API
const handleToggle = async (key: Guid) => {
  await importClient.toggleSelection([key])

  // Refresh current page to show updated state
  await loadPage(currentPage.value)
}

// "Select All" button
const handleSelectAll = async () => {
  await importClient.selectAll()
  await loadPage(currentPage.value)  // Refresh to show changes
}

// "Deselect All" button
const handleDeselectAll = async () => {
  await importClient.deselectAll()
  await loadPage(currentPage.value)
}

// Import button - no parameters needed
const handleImport = async () => {
  const result = await importClient.completeReview()  // No keys parameter!

  // Show success modal
  showSuccessModal.value = true
  successMessage.value = `Imported ${result.acceptedCount} transactions. Rejected ${result.rejectedCount}.`
}
```

#### Import Action Buttons Update

**"Import" button state:**
- Enabled when **ANY transaction is selected** (backend knows the count)
- Need new API endpoint: `GET /api/tenant/{tenantKey}/import/review/summary`

Response:
```json
{
  "totalCount": 300,
  "selectedCount": 250,
  "newCount": 250,
  "exactDuplicateCount": 30,
  "potentialDuplicateCount": 20
}
```

Frontend uses `selectedCount > 0` to enable/disable Import button.

## Migration Strategy

### Breaking Changes

1. **API Contract Change:** `POST /api/tenant/{tenantKey}/import/review/complete` no longer accepts request body
2. **Frontend State Management:** Remove session storage, use server state
3. **Database Schema:** Add `IsSelected` column to `ImportReviewTransaction`

### Deployment Steps

1. **Database Migration:** Add `IsSelected` column with default `true`
2. **Backend Deployment:** Deploy updated API and Application layers
3. **Frontend Deployment:** Deploy updated frontend with server-side state
4. **Validation:** Test with large imports (500+ transactions)

### Backward Compatibility

**This is a breaking change.** Frontend and backend must be deployed together:
- Old frontend + new backend = broken (completeReview parameter mismatch)
- New frontend + old backend = broken (missing endpoints)

**Deployment approach:** Deploy backend first with feature flag, then deploy frontend, then enable feature.

## Performance Considerations

### Database Operations

**Toggle single transaction:** 1 UPDATE query (indexed by Key)
```sql
UPDATE ImportReviewTransactions
SET IsSelected = NOT IsSelected
WHERE TenantId = @tenantId AND Key = @key
```

**Select/Deselect all:** 1 bulk UPDATE query (indexed by TenantId)
```sql
UPDATE ImportReviewTransactions
SET IsSelected = @value
WHERE TenantId = @tenantId
```

**Get selected count:** 1 COUNT query (indexed by TenantId + IsSelected)
```sql
SELECT COUNT(*) FROM ImportReviewTransactions
WHERE TenantId = @tenantId AND IsSelected = 1
```

**Required index:** Add composite index `IX_ImportReviewTransactions_TenantId_IsSelected` for count queries.

### Network Overhead

**Toggle operation:**
- Request: ~100 bytes (JSON with key)
- Response: 204 No Content (minimal)
- **Cost:** 1 round-trip per checkbox toggle (acceptable UX)

**Select/Deselect All:**
- Request: Empty POST
- Response: 204 No Content
- **Cost:** 1 round-trip (very fast)

**Page load:**
- Response includes `isSelected` per transaction: +1 boolean per item (~5 bytes each)
- **Cost:** Negligible (50 booleans = 250 bytes)

### Optimizations

**Debounce checkbox toggles** (optional):
- Batch multiple checkbox changes into single API call
- Example: User clicks 5 checkboxes rapidly → 1 API call with 5 keys

**Optimistic UI updates** (optional):
- Update checkbox state immediately in UI
- Call API in background
- Revert on error
- **Trade-off:** Complexity vs. instant feedback

## Testing Strategy

### Unit Tests

**ImportReviewFeature:**
- `ToggleSelectionAsync` - Verify state changes
- `SelectAllAsync` - Verify all transactions selected
- `DeselectAllAsync` - Verify all transactions deselected
- `CompleteReviewAsync` - Verify only selected transactions accepted

**Default selection logic:**
- Verify `IsSelected = true` for `DuplicateStatus.New`
- Verify `IsSelected = false` for `DuplicateStatus.ExactDuplicate`
- Verify `IsSelected = false` for `DuplicateStatus.PotentialDuplicate`

### Integration Tests

**End-to-end import workflow:**

1. Upload OFX file with 300 transactions (250 new, 50 duplicates)
2. Verify database has 300 records with correct `IsSelected` defaults
3. Call `GET /review?pageNumber=1&pageSize=50`
4. Verify response includes 50 transactions with `isSelected` field
5. Toggle selection for transaction on page 1
6. Verify database updated
7. Call `POST /review/complete` (no body)
8. Verify 250 transactions accepted (all with `IsSelected = true` across all pages)
9. Verify 50 transactions rejected (all with `IsSelected = false`)
10. Verify all 300 transactions deleted from review table

**Pagination correctness:**

1. Upload 300 transactions
2. Load page 1 only
3. Call `POST /review/complete`
4. **Verify 250 transactions accepted** (NOT just page 1!)
5. This is the key test - proves pagination bug is fixed

### Functional Tests (Playwright)

**Large import scenario:**

```gherkin
Scenario: Import large file with default selections
  Given user uploads OFX file with 500 transactions
  When user reviews page 1 (50 transactions shown)
  And user clicks "Import" button without changing selections
  Then 450 transactions should be accepted
  And 50 duplicates should be rejected
  And success modal should show correct counts
```

**Toggle selection across pages:**

```gherkin
Scenario: User toggles selections across multiple pages
  Given user uploads OFX file with 100 transactions
  When user loads page 1 and deselects 5 transactions
  And user loads page 2 and selects 3 additional transactions
  And user clicks "Import" button
  Then correct number of transactions should be accepted
  And deselected transactions should be rejected
```

## Validation Checklist

Before approving this solution, validate:

- [ ] Database schema supports server-side selection state
- [ ] Default selections applied automatically on import
- [ ] API endpoints support toggle/select-all/deselect-all operations
- [ ] CompleteReview reads from database, not request body
- [ ] Frontend removed all session storage code
- [ ] Frontend calls API for every selection change
- [ ] Import button works correctly with 1000+ transactions
- [ ] Performance acceptable for checkbox toggles
- [ ] Integration tests cover large import scenario
- [ ] Migration strategy accounts for breaking changes

## Conclusion

The current frontend session storage design is **fundamentally incompatible with paginated data**. Selection state must be **server-side** to ensure all transactions (across all pages) can be selected or deselected, even if they're never loaded by the frontend.

**Key insight:** Selection state is not "UI state" when pagination is involved. It's **domain state** that belongs in the database alongside the transaction data it describes.

**Recommendation:** Implement server-side selection tracking before any user-facing deployment. The current design will cause massive data loss for any import over ~50 transactions.
