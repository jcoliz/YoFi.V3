---
status: Draft
target_release: TBD
design_document: GENERIC-TABLE-HELPER-DESIGN.md
---

# Generic Table Helper Implementation Plan

## Overview

Step-by-step implementation plan for refactoring [`TransactionTableData`](../../tests/Functional/Pages/TransactionsPage.cs:530-541) into a reusable generic [`TableDataHelper`](../../tests/Functional/Helpers/TableDataHelper.cs) that can be used across all functional test Page Object Models.

Related design document: [`GENERIC-TABLE-HELPER-DESIGN.md`](GENERIC-TABLE-HELPER-DESIGN.md)

## Phase 1: Create Generic Helper (Backend Tests)

### 1.1 Create Helper Classes

**File:** `tests/Functional/Helpers/TableDataHelper.cs` (new)

Create three classes in one file:
- `TableRowData` - Base class for row data
- `TableData<TRow>` - Container for loaded table data
- `TableDataHelper<TRow>` - Main helper class

**Implementation:**
- Constructor: `TableDataHelper(ILocator tableLocator)`
- Core loading: `LoadAsync(bool forceReload)`, `ReloadAsync()`, `ClearCache()`
- Query methods: `GetRowByIdAsync()`, `GetRowLocatorById()`, `GetCellAsync()`, `GetCellTextAsync()`, `GetColumnNamesAsync()`
- Wait methods: `WaitForRowAsync()`
- Regex pattern: `RowIdRegex()` for extracting GUID from `data-test-id="row-{guid}"`

**References:**
- See design document lines 71-252 for complete implementation
- Use `[GeneratedRegex]` pattern from project conventions
- Add comprehensive XML documentation comments

**Dependencies:**
- `using Microsoft.Playwright;`
- `using System.Text.RegularExpressions;`

### 1.2 Create Helper Documentation

**File:** `tests/Functional/Helpers/README.md` (new)

Document:
- Purpose of the helper
- Basic usage examples
- When to use vs. when to use direct Playwright locators
- Caching behavior and when to clear cache
- Multiple table scenarios

### 1.3 Run Tests

Verify the project still builds:
```bash
dotnet build tests/Functional
```

No tests should break yet since we haven't modified any existing code.

## Phase 2: Frontend Pattern Change

### 2.1 Update Transactions Table

**File:** `src/FrontEnd.Nuxt/app/pages/transactions/index.vue` (line 544)

**Change:**
```vue
<!-- Before -->
:data-test-id="`transaction-row-${transaction.key}`"

<!-- After -->
:data-test-id="`row-${transaction.key}`"
```

**Impact:** Update existing functional tests to use the new `row-${transaction.key}` pattern.

### 2.2 Update Import Review Table

**File:** `src/FrontEnd.Nuxt/app/components/import/ImportReviewTable.vue` (line 124)

**Change:**
```vue
<!-- Before -->
:data-test-id="`transaction-row-${transaction.key}`"

<!-- After -->
:data-test-id="`row-${transaction.key}`"
```

### 2.3 Add Header Test IDs

Verify all table headers have `data-test-id` attributes in both files:
- `transactions/index.vue` - date, payee, amount, category, memo (if applicable)
- `import/ImportReviewTable.vue` - check headers

If missing, add them following the pattern:
```vue
<th data-test-id="column-name">Display Name</th>
```

### 2.4 Test Frontend Locally

Start local dev environment and verify tables display correctly:
```bash
pwsh -File ./scripts/Start-LocalDev.ps1
```

Navigate to transactions page and verify:
- Table renders correctly
- Row `data-test-id` attributes are `row-{guid}` format
- All headers have `data-test-id` attributes

## Phase 3: Refactor TransactionsPage (Parallel Path Support)

### 3.1 Add TableDataHelper to TransactionsPage

**File:** `tests/Functional/Pages/TransactionsPage.cs`

**Add field and property after line 27:**
```csharp
/// <summary>
/// Cached transaction table data to avoid reloading on every query
/// </summary>
private TransactionTableData? _cachedTableData;

/// <summary>
/// Table helper for generic table operations
/// </summary>
private TableDataHelper<TableRowData>? _tableHelper;

/// <summary>
/// Gets or creates the table helper instance
/// </summary>
private TableDataHelper<TableRowData> TableHelper =>
    _tableHelper ??= new TableDataHelper<TableRowData>(TransactionsTable);
```

### 3.2 Update GetTransactionRow Method

**Add new overload after line 626:**
```csharp
/// <summary>
/// Gets a transaction row by transaction key
/// </summary>
/// <param name="transactionKey">The key (GUID) of the transaction</param>
/// <returns>Locator for the transaction row</returns>
public ILocator GetTransactionRow(Guid transactionKey)
{
    return TableHelper.GetRowLocatorById(transactionKey);
}
```

Keep existing `GetTransactionRow(string)` method for backwards compatibility with tests still using string keys.

### 3.3 Update WaitForTransactionRowByKeyAsync

**Replace method at line 629:**
```csharp
public async Task WaitForTransactionRowByKeyAsync(Guid transactionKey, float timeout = 5000)
{
    await TableHelper.WaitForRowAsync(transactionKey, timeout);
}
```

### 3.4 Add New Helper Methods

**Add after WaitForTransactionRowByKeyAsync:**
```csharp
/// <summary>
/// Gets a specific cell locator by transaction ID and column name
/// </summary>
/// <param name="transactionId">The GUID of the transaction</param>
/// <param name="columnTestId">The data-test-id of the column header</param>
/// <returns>Locator for the table cell</returns>
public async Task<ILocator> GetTransactionCellAsync(Guid transactionId, string columnTestId)
{
    return await TableHelper.GetCellAsync(transactionId, columnTestId);
}

/// <summary>
/// Gets cell text by transaction ID and column name
/// </summary>
/// <param name="transactionId">The GUID of the transaction</param>
/// <param name="columnTestId">The data-test-id of the column header</param>
/// <returns>The cell text content</returns>
public async Task<string> GetTransactionCellTextAsync(Guid transactionId, string columnTestId)
{
    return await TableHelper.GetCellTextAsync(transactionId, columnTestId);
}
```

### 3.5 Update TransactionRows Locator

**Update line 116:**
```csharp
// Before
public ILocator TransactionRows => TransactionsTable.Locator("tbody tr[data-test-id^='transaction-row-']");

// After
public ILocator TransactionRows => TransactionsTable.Locator("tbody tr[data-test-id^='row-']");
```

### 3.6 Keep Old Methods Temporarily

**DO NOT REMOVE YET:**
- `LoadTransactionTableDataAsync()` (line 548)
- `TransactionsTableCell(string, string)` (line 682)
- `TransactionsTableCellText(string, string)` (line 700)
- `GetTransactionRowByPayeeAsync(string)` (line 657)
- `GetTransactionKeyByPayeeAsync(string)` (line 638)

These will be removed after test migration is complete.

### 3.7 Update Cache Invalidation

**Update ReloadTransactionTableDataAsync at line 613:**
```csharp
public async Task ReloadTransactionTableDataAsync()
{
    // Invalidate both caches
    await LoadTransactionTableDataAsync(forceReload: true);
    TableHelper.ClearCache();
}
```

### 3.8 Run Tests

Run functional tests to verify parallel path support:
```bash
pwsh -File ./scripts/Run-FunctionalTestsVsContainer.ps1
```

**Expected:** Tests should still pass using old methods. New methods are available but not yet used.

## Phase 4: Migrate Functional Tests

### 4.1 Update Test Step Classes

Identify all test step classes that use transaction table methods:

**Search for usage:**
```bash
# Find all uses of GetTransactionRowByPayeeAsync
# Find all uses of TransactionsTableCell
# Find all uses of TransactionsTableCellText
```

**Files to check:**
- `tests/Functional/Steps/Transaction/TransactionDataSteps.cs`
- `tests/Functional/Steps/Transaction/TransactionDetailsSteps.cs`
- `tests/Functional/Steps/Transaction/TransactionEditSteps.cs`
- `tests/Functional/Steps/Transaction/TransactionListSteps.cs`
- `tests/Functional/Steps/Transaction/TransactionQuickEditSteps.cs`

### 4.2 Migration Pattern

For each usage, apply this pattern:

**Before (search-based):**
```csharp
// Get cell by searching for payee name
var categoryCell = await transactionsPage.TransactionsTableCell("Acme Corp", "category");
var categoryText = await transactionsPage.TransactionsTableCellText("Acme Corp", "category");
```

**After (ID-based):**
```csharp
// Get transaction ID first (if not already known)
var transactionId = await transactionsPage.GetTransactionKeyByPayeeAsync("Acme Corp");

// Then get cell directly by ID
var categoryCell = await transactionsPage.GetTransactionCellAsync(transactionId, "category");
var categoryText = await transactionsPage.GetTransactionCellTextAsync(transactionId, "category");
```

**Better Pattern (when ID is already known):**
```csharp
// In data steps, store transaction IDs when creating
var transactionId = objectStore.Get<Guid>("TransactionKey");

// Then use ID directly in other steps
var categoryText = await transactionsPage.GetTransactionCellTextAsync(transactionId, "category");
```

### 4.3 Migrate One Step File at a Time

For each step file:
1. Update methods to use new ID-based approach
2. Run tests for that feature
3. Fix any broken tests
4. Commit changes
5. Move to next file

**Recommended order:**
1. `TransactionDataSteps.cs` - Stores IDs, other steps depend on this
2. `TransactionListSteps.cs` - Simple assertions
3. `TransactionDetailsSteps.cs` - Detail view tests
4. `TransactionEditSteps.cs` - Edit operations
5. `TransactionQuickEditSteps.cs` - Quick edit operations

### 4.4 Run Tests After Each Migration

After migrating each step file:
```bash
pwsh -File ./scripts/Run-FunctionalTestsVsContainer.ps1
```

Fix any failures before moving to the next file.

## Phase 5: Remove Old Implementation

### 5.1 Verify No Usage of Old Methods

**Search for usage:**
```bash
# Verify these are no longer used:
# - LoadTransactionTableDataAsync
# - TransactionsTableCell(string, string)
# - TransactionsTableCellText(string, string)
# - TransactionTableData class
# - TransactionRowData class
```

**Check:**
- All functional test steps
- Any helper methods in TransactionsPage

### 5.2 Remove Old Classes and Methods

**In TransactionsPage.cs, remove:**
- Line 27: `private TransactionTableData? _cachedTableData;`
- Lines 508-541: `TransactionRowData` and `TransactionTableData` classes
- Lines 548-604: `LoadTransactionTableDataAsync()` method
- Line 613: `ReloadTransactionTableDataAsync()` (replace with helper clear)

**Update ReloadTransactionTableDataAsync:**
```csharp
/// <summary>
/// Reloads the transaction table data from the DOM, bypassing the cache
/// </summary>
public void ReloadTransactionTableData()
{
    TableHelper.ClearCache();
}
```

### 5.3 Update Method Signatures

**Remove string-based overloads if no longer needed:**
- `GetTransactionRow(string)` - Keep only `GetTransactionRow(Guid)`
- Consider removing `GetTransactionRowByPayeeAsync()` if all tests migrated
- Consider removing `TransactionsTableCell(string, string)` overloads

**Decision:** Discuss with team whether to keep any search-based methods for convenience.

### 5.4 Run Full Test Suite

Run all functional tests:
```bash
pwsh -File ./scripts/Run-FunctionalTestsVsContainer.ps1
```

All tests should pass with the refactored implementation.

## Phase 6: Apply to Other Pages (Future)

### 6.1 ImportPage

**File:** `tests/Functional/Pages/ImportPage.cs`

Check if ImportPage has any table-based UI that could use the helper.

If ImportReviewTable is used in tests:
1. Add `TableDataHelper` instance
2. Migrate any existing table query methods
3. Update tests to use helper

### 6.2 Document Pattern

**Update:** `tests/Functional/Pages/README.md`

Add documentation:
- When to use `TableDataHelper`
- Pattern for instantiating helper
- Pattern for clearing cache after mutations
- Examples from TransactionsPage

## Testing Strategy

### Unit Testing the Helper

Consider creating unit tests for `TableDataHelper`:
- **Location:** `tests/Unit/Helpers/TableDataHelperTests.cs` (new)
- **Mock:** Create mock HTML table using Playwright test harness
- **Tests:**
  - Load table data successfully
  - Extract row IDs correctly
  - Build column mappings correctly
  - Cache behavior works correctly
  - Error handling for missing rows/columns

**Decision:** Optional - discuss with team if unit tests are needed or if functional tests provide sufficient coverage.

### Functional Test Coverage

After migration, verify coverage:
- All existing transaction tests still pass
- Cache invalidation works correctly
- Error messages are clear and helpful
- Performance is acceptable

### Manual Testing

Test in local dev environment:
1. Start local dev: `pwsh -File ./scripts/Start-LocalDev.ps1`
2. Run functional tests: `pwsh -File ./scripts/Run-FunctionalTestsVsContainer.ps1`
3. Verify all transaction tests pass
4. Check test execution time (should be similar or faster)

## Rollback Plan

If issues are encountered:

### Phase 1-2 Rollback
- Delete helper files
- Revert frontend changes
- No impact on existing tests

### Phase 3 Rollback
- Revert TransactionsPage changes
- Helper code remains but unused
- Tests continue using old methods

### Phase 4 Rollback
- Revert individual step file changes
- Tests fall back to old methods
- Can rollback incrementally per file

### Phase 5 Rollback
- Restore deleted code from git history
- Re-add old classes and methods
- Tests work with either path

## Success Criteria

- [ ] Generic `TableDataHelper` created and documented
- [ ] Frontend using `row-{guid}` pattern
- [ ] All transaction functional tests migrated to use helper
- [ ] Old implementation removed from TransactionsPage
- [ ] All functional tests passing
- [ ] No performance regression
- [ ] Code is cleaner and more maintainable
- [ ] Pattern documented for future use

## Timeline Estimate

**Note:** Per project policy, no time estimates provided. Tasks are listed in execution order.

## Risks and Mitigations

**Risk:** Breaking existing functional tests during frontend migration
**Mitigation:** Use parallel path support (Phase 3) to keep both patterns working during migration

**Risk:** Performance degradation from helper overhead
**Mitigation:** Helper uses same caching strategy as original implementation; performance should be equivalent

**Risk:** Tests become harder to read with GUID-based approach
**Mitigation:** Use meaningful variable names and comments; consider keeping search-based helper methods for test readability

**Risk:** Other pages may have different table patterns
**Mitigation:** Helper is generic and flexible; can be extended for different patterns if needed

## Follow-up Work

After successful implementation:
1. Consider applying pattern to other list-based UI (cards, etc.)
2. Document `data-test-id` patterns in functional testing guidelines
3. Create template for new pages that use tables
4. Evaluate if similar helpers are needed for other UI patterns
