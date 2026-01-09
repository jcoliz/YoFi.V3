---
status: Draft
target_release: TBD
related_files:
  - tests/Functional/Pages/TransactionsPage.cs (lines 508-717)
  - tests/Functional/Helpers/TableDataHelper.cs (proposed)
---

# Generic Table Helper Design

## Overview

Refactor the `TransactionTableData` implementation from [`TransactionsPage.cs`](../../tests/Functional/Pages/TransactionsPage.cs) into a reusable generic helper that can be used across all Page Object Models that need to interact with HTML tables in Playwright functional tests.

## Current Implementation Analysis

### Location
[`tests/Functional/Pages/TransactionsPage.cs`](../../tests/Functional/Pages/TransactionsPage.cs) (lines 508-717)

### Current Classes

**`TransactionRowData`** (lines 509-525)
- Represents a single row in the table
- Properties:
  - `Dictionary<string, string> Columns` - Maps column `data-test-id` to cell text
  - `int RowIndex` - Zero-based row position
  - `ILocator RowLocator` - Playwright locator for the row

**`TransactionTableData`** (lines 530-541)
- Container for the loaded table data
- Properties:
  - `List<TransactionRowData> Rows` - All rows in the table
  - `Dictionary<string, int> ColumnIndexMap` - Maps column `data-test-id` to column index

### Key Methods

**`LoadTransactionTableDataAsync()`** (lines 548-604)
- Loads all table data into memory for LINQ querying
- Reads header `data-test-id` attributes to build column mapping
- Iterates through all `tbody tr` rows matching `[data-test-id^='transaction-row-']`
- Extracts text content from each cell
- Supports caching with `forceReload` parameter

**Helper Methods Using TableData:**
- `GetTransactionRowByPayeeAsync()` - Find row by searching payee column
- `TransactionsTableCell()` - Get cell locator by row identifier and column name
- `TransactionsTableCellText()` - Get cell text by row identifier and column name
- `GetTransactionRowDataByPayeeAsync()` - Get full row data by searching

### Current Row Identification Pattern

Frontend uses `data-test-id="transaction-row-{guid}"` pattern:
- [`transactions/index.vue`](../../src/FrontEnd.Nuxt/app/pages/transactions/index.vue:544)
- [`import/ImportReviewTable.vue`](../../src/FrontEnd.Nuxt/app/components/import/ImportReviewTable.vue:124)

**Note:** User plans to refactor frontend to use generic `row-{guid}` pattern instead of `transaction-row-{guid}`.

### Usage Patterns

1. **Query by known identifier** - "Get the category cell for payee 'Acme Corp'"
2. **Search and retrieve** - "Find the row where payee equals 'Acme Corp' and get its data"
3. **LINQ queries** - Load table data once, run multiple in-memory queries
4. **Cache invalidation** - Clear cache after mutations (create, update, delete)

## Proposed Design

### Generic Table Helper Class

Create `TableDataHelper<TRow>` in [`tests/Functional/Helpers/TableDataHelper.cs`](../../tests/Functional/Helpers/TableDataHelper.cs):

```csharp
namespace YoFi.V3.Tests.Functional.Helpers;

/// <summary>
/// Generic helper for loading and querying HTML table data in Playwright tests.
/// </summary>
/// <typeparam name="TRow">Type of the row data class</typeparam>
/// <param name="tableLocator">Playwright locator for the table element</param>
/// <remarks>
/// Loads table data into memory for efficient LINQ querying.
/// Supports caching to avoid repeated DOM queries.
/// </remarks>
public class TableDataHelper<TRow>(ILocator tableLocator) where TRow : TableRowData, new()
{
    private readonly ILocator _tableLocator = tableLocator;
    private TableData<TRow>? _cachedTableData;

    /// <summary>
    /// Loads table data from the DOM
    /// </summary>
    /// <param name="forceReload">If true, bypasses cache and reloads from DOM</param>
    /// <returns>Table data with rows and column mappings</returns>
    public async Task<TableData<TRow>> LoadAsync(bool forceReload = false)
    {
        if (!forceReload && _cachedTableData != null)
        {
            return _cachedTableData;
        }

        var result = new TableData<TRow>();

        // Build column mapping from headers
        var headers = _tableLocator.Locator("thead th");
        var headerCount = await headers.CountAsync();

        for (int i = 0; i < headerCount; i++)
        {
            var header = headers.Nth(i);
            var testId = await header.GetAttributeAsync("data-test-id");
            if (!string.IsNullOrEmpty(testId))
            {
                result.ColumnIndexMap[testId] = i;
            }
        }

        // Load all rows
        var rows = _tableLocator.Locator("tbody tr[data-test-id^='row-']");
        var rowCount = await rows.CountAsync();

        for (int rowIdx = 0; rowIdx < rowCount; rowIdx++)
        {
            var row = rows.Nth(rowIdx);
            var rowData = new TRow
            {
                RowIndex = rowIdx,
                RowLocator = row
            };

            // Extract row ID from data-test-id="row-{guid}"
            var rowTestId = await row.GetAttributeAsync("data-test-id");
            if (!string.IsNullOrEmpty(rowTestId))
            {
                var match = RowIdRegex().Match(rowTestId);
                if (match.Success && Guid.TryParse(match.Groups[1].Value, out var rowId))
                {
                    rowData.RowId = rowId;
                }
            }

            // Load cell data
            var cells = row.Locator("td");
            var cellCount = await cells.CountAsync();

            for (int colIdx = 0; colIdx < cellCount; colIdx++)
            {
                var columnTestId = result.ColumnIndexMap.FirstOrDefault(x => x.Value == colIdx).Key;
                if (!string.IsNullOrEmpty(columnTestId))
                {
                    var cell = cells.Nth(colIdx);
                    var cellText = await cell.TextContentAsync();
                    rowData.Columns[columnTestId] = cellText?.Trim() ?? "";
                }
            }

            result.Rows.Add(rowData);
        }

        _cachedTableData = result;
        return result;
    }

    /// <summary>
    /// Reloads table data from DOM, bypassing cache
    /// </summary>
    public Task<TableData<TRow>> ReloadAsync() => LoadAsync(forceReload: true);

    /// <summary>
    /// Clears the cached table data
    /// </summary>
    public void ClearCache()
    {
        _cachedTableData = null;
    }

    /// <summary>
    /// Gets a row by its GUID identifier
    /// </summary>
    public async Task<TRow?> GetRowByIdAsync(Guid rowId)
    {
        var tableData = await LoadAsync();
        return tableData.Rows.FirstOrDefault(r => r.RowId == rowId);
    }

    /// <summary>
    /// Gets a row locator by its GUID identifier
    /// </summary>
    public ILocator GetRowLocatorById(Guid rowId)
    {
        return _tableLocator.Locator($"tbody tr[data-test-id='row-{rowId}']");
    }

    /// <summary>
    /// Gets a specific cell locator by row ID and column name
    /// </summary>
    public async Task<ILocator> GetCellAsync(Guid rowId, string columnTestId)
    {
        var tableData = await LoadAsync();
        var rowData = tableData.Rows.FirstOrDefault(r => r.RowId == rowId);

        if (rowData == null)
        {
            throw new ArgumentException($"Row with ID '{rowId}' not found");
        }

        if (!tableData.ColumnIndexMap.TryGetValue(columnTestId, out var columnIndex))
        {
            throw new ArgumentException($"Column with data-test-id '{columnTestId}' not found");
        }

        return rowData.RowLocator.Locator("td").Nth(columnIndex);
    }

    /// <summary>
    /// Gets cell text by row ID and column name
    /// </summary>
    public async Task<string> GetCellTextAsync(Guid rowId, string columnTestId)
    {
        var tableData = await LoadAsync();
        var rowData = tableData.Rows.FirstOrDefault(r => r.RowId == rowId);

        if (rowData == null)
        {
            throw new ArgumentException($"Row with ID '{rowId}' not found");
        }

        return rowData.Columns.TryGetValue(columnTestId, out var cellText)
            ? cellText
            : throw new ArgumentException($"Column '{columnTestId}' not found in row");
    }

    /// <summary>
    /// Gets all column names (data-test-id values) from headers
    /// </summary>
    public async Task<IReadOnlyCollection<string>> GetColumnNamesAsync()
    {
        var tableData = await LoadAsync();
        return tableData.ColumnIndexMap.Keys.ToList();
    }

    /// <summary>
    /// Waits for a row with the specified ID to appear in the table
    /// </summary>
    /// <param name="rowId">The GUID of the row to wait for</param>
    /// <param name="timeout">Timeout in milliseconds (default: 5000)</param>
    public async Task WaitForRowAsync(Guid rowId, float timeout = 5000)
    {
        var rowLocator = GetRowLocatorById(rowId);
        await rowLocator.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = timeout });

        // Clear cache to ensure fresh data on next query
        ClearCache();
    }

    [GeneratedRegex(@"row-([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})")]
    private static partial Regex RowIdRegex();
}
```

### Supporting Classes

**`TableRowData`** - Base class for row data:

```csharp
/// <summary>
/// Base class representing a row in an HTML table
/// </summary>
public class TableRowData
{
    /// <summary>
    /// Dictionary of column data-test-id to cell text content
    /// </summary>
    public Dictionary<string, string> Columns { get; } = new();

    /// <summary>
    /// Zero-based row index in the table
    /// </summary>
    public int RowIndex { get; set; }

    /// <summary>
    /// Playwright locator for this row
    /// </summary>
    public required ILocator RowLocator { get; init; }

    /// <summary>
    /// Row identifier extracted from data-test-id="row-{guid}"
    /// </summary>
    public Guid RowId { get; set; }
}
```

**`TableData<TRow>`** - Container for loaded table data:

```csharp
/// <summary>
/// Container for loaded table data with column mappings
/// </summary>
public class TableData<TRow> where TRow : TableRowData
{
    /// <summary>
    /// All rows in the table
    /// </summary>
    public List<TRow> Rows { get; init; } = new();

    /// <summary>
    /// Mapping from column data-test-id to column index (0-based)
    /// </summary>
    public Dictionary<string, int> ColumnIndexMap { get; init; } = new();
}
```

## Usage Example: TransactionsPage Refactored

**Before:**
```csharp
public partial class TransactionsPage(IPage page) : BasePage(page)
{
    private TransactionTableData? _cachedTableData;

    private async Task<TransactionTableData> LoadTransactionTableDataAsync(bool forceReload = false)
    {
        // 50+ lines of loading logic
    }

    public async Task<ILocator> TransactionsTableCell(string payee, string column)
    {
        var tableData = await LoadTransactionTableDataAsync();
        var rowData = tableData.Rows.FirstOrDefault(r => r.Columns.TryGetValue("payee", out var p) && p == payee);
        // ... more logic
    }
}
```

**After:**
```csharp
public partial class TransactionsPage(IPage page) : BasePage(page)
{
    private TableDataHelper<TableRowData>? _tableHelper;

    private TableDataHelper<TableRowData> TableHelper =>
        _tableHelper ??= new TableDataHelper<TableRowData>(TransactionsTable);

    public async Task<ILocator> TransactionsTableCell(Guid transactionId, string column)
    {
        return await TableHelper.GetCellAsync(transactionId, column);
    }

    public async Task<string> TransactionsTableCellText(Guid transactionId, string column)
    {
        return await TableHelper.GetCellTextAsync(transactionId, column);
    }

    // Search-based methods (if still needed for backwards compatibility)
    public async Task<Guid> GetTransactionKeyByPayeeAsync(string payeeName)
    {
        var tableData = await TableHelper.LoadAsync();
        var rowData = tableData.Rows.FirstOrDefault(
            r => r.Columns.TryGetValue("payee", out var payee) && payee == payeeName);

        if (rowData == null)
        {
            throw new ArgumentException($"Transaction with payee '{payeeName}' not found");
        }

        return rowData.RowId;
    }
}
```

## Benefits

1. **Reusability** - Any page with tables can use this helper
2. **Consistency** - All table interaction follows same pattern
3. **Maintainability** - Table loading logic in one place
4. **Testability** - Helper can be unit tested independently
5. **Type safety** - Generic design allows custom row types if needed
6. **Performance** - Caching reduces DOM queries

## Implementation Considerations

### Row Identifier Pattern

**Current:** `data-test-id="transaction-row-{guid}"` (transaction-specific)
**Target:** `data-test-id="row-{guid}"` (generic)

Frontend changes required:
- [`transactions/index.vue`](../../src/FrontEnd.Nuxt/app/pages/transactions/index.vue:544) - Change to `row-${transaction.key}`
- [`import/ImportReviewTable.vue`](../../src/FrontEnd.Nuxt/app/components/import/ImportReviewTable.vue:124) - Change to `row-${transaction.key}`

**Migration Strategy:**
1. Create generic helper with `row-{guid}` pattern
2. Update frontend to use new pattern
3. Update TransactionsPage to use helper
4. Update functional tests to pass GUIDs instead of search strings
5. Remove old search-based methods after migration

### Backwards Compatibility

During migration, may need to support both patterns temporarily:
- Keep search-based helper methods (`GetTransactionRowByPayeeAsync()`)
- Add new ID-based methods (`GetTransactionRow(Guid)`)
- Gradually migrate tests to use GUIDs
- Remove search methods once all tests migrated

### Column Name Handling

Headers must have `data-test-id` attributes:
```vue
<th data-test-id="date">Date</th>
<th data-test-id="payee">Payee</th>
<th data-test-id="amount">Amount</th>
<th data-test-id="category">Category</th>
```

Verify all table headers have proper test IDs.

### Error Handling

Helper should provide clear error messages:
- "Row with ID '{guid}' not found"
- "Column with data-test-id '{name}' not found"
- "Table has no rows"
- "Table headers missing data-test-id attributes"

### Performance

Current implementation loads entire table into memory. This is appropriate because:
- Tables in tests are typically small (< 100 rows)
- Enables efficient LINQ queries
- Caching reduces repeated DOM access
- Tests often need multiple queries per table

For very large tables, consider lazy loading or pagination support.

## API Summary

### TableDataHelper<TRow>

**Constructor:**
- `TableDataHelper(ILocator tableLocator)` - Initialize with table locator

**Core Methods:**
- `Task<TableData<TRow>> LoadAsync(bool forceReload = false)` - Load/get cached data
- `Task<TableData<TRow>> ReloadAsync()` - Force reload from DOM
- `void ClearCache()` - Clear cached data

**Query Methods:**
- `Task<TRow?> GetRowByIdAsync(Guid rowId)` - Get row data by ID
- `ILocator GetRowLocatorById(Guid rowId)` - Get row locator by ID
- `Task<ILocator> GetCellAsync(Guid rowId, string columnTestId)` - Get cell locator
- `Task<string> GetCellTextAsync(Guid rowId, string columnTestId)` - Get cell text
- `Task<IReadOnlyCollection<string>> GetColumnNamesAsync()` - Get all column names

**Wait Methods:**
- `Task WaitForRowAsync(Guid rowId, float timeout = 5000)` - Wait for row to appear

### Requirements Coverage

All requested features are covered:
1. ✅ Construct with `<table>` locator
2. ✅ Get column names (strings)
3. ✅ Get row by ID
4. ✅ Get cell by row ID & column
5. ✅ Wait for row (added based on user feedback)

## File Organization

```
tests/Functional/
├── Helpers/
│   ├── TableDataHelper.cs       (new - generic helper)
│   └── README.md                (new - usage documentation)
├── Pages/
│   ├── TransactionsPage.cs      (refactor - use helper)
│   ├── WorkspacesPage.cs        (no changes - uses cards not tables)
│   └── ImportPage.cs            (potential user - may have tables)
```

## Migration Plan

See implementation todos for step-by-step migration.

## Design Decisions (Answered)

1. **Custom Row Types:** Use base `TableRowData` with string dictionary for simplicity. Individual steps handle type conversion from strings as needed.

2. **Search Methods:** Rely on LINQ queries in Page Objects rather than building search into the helper. Can be refactored later if common patterns emerge.

3. **Wait Methods:** Include `WaitForRowAsync(Guid)` in the helper. The helper is conceptually similar to a Component Object Model, so it has design permission to take Playwright actions.

4. **Multi-table Pages:** Each table gets its own helper instance. Pages with multiple tables can instantiate multiple `TableDataHelper` instances.

5. **Performance/Pagination:** All tables in this project are paginated with max 25-50 rows per page. Loading entire table into memory is appropriate and performant.

## Related Work

- Frontend pattern change: `transaction-row-{guid}` → `row-{guid}`
- Consider similar patterns for other list-based UI elements (cards, lists)
- Document the `data-test-id` pattern in functional testing guidelines
