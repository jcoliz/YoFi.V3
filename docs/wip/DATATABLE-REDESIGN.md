# DataTable Redesign for Functional Tests

## Overview

This document outlines the redesign of the [`DataTable`](tests/Functional/Helpers/DataTable.cs) class to support flexible table structures with varying column counts for Gherkin feature files.

## Current Implementation Analysis

### Current Limitations

The existing [`DataTable`](tests/Functional/Helpers/DataTable.cs:55) implementation is hardcoded for 2-column tables (Field/Value):

```csharp
public class DataTable : IEnumerable<DataTableRow>
{
    public void AddRow(string field, string value) { }
    public string GetValue(string fieldName) { }
}

public class DataTableRow
{
    public string Field { get; }
    public string Value { get; }
    public string this[string columnName] { get; } // Only supports "Field" and "Value"
}
```

### Current Usage Patterns

From [`AuthenticationSteps.cs`](tests/Functional/Steps/AuthenticationSteps.cs:122):

```csharp
// Pattern 1: Direct field access via GetValue
protected async Task WhenIEnterMyCredentials(DataTable credentialsData)
{
    var email = credentialsData.GetValue("Email");
    var password = credentialsData.GetValue("Password");
}

// Pattern 2: Manual row iteration with indexer
private string GetTableValue(DataTable table, string fieldName)
{
    foreach (var row in table.Rows)
    {
        if (row["Field"] == fieldName)
            return row["Value"];
    }
}
```

### Required Table Structures

From [`Tenancy.feature`](tests/Functional/Features/Tenancy.feature):

1. **Single column tables** (line 14):
   ```gherkin
   | Username |
   | alice    |
   | bob      |
   ```

2. **Two column tables** (line 50):
   ```gherkin
   | Workspace Name | My Role |
   | Personal       | Owner   |
   | Family Budget  | Editor  |
   ```

3. **Two column tables with different headers** (line 155):
   ```gherkin
   | Workspace Name  | Owner   |
   | Private Data    | alice   |
   | Charlie's Taxes | charlie |
   ```

## Design Goals

Based on the priority to **emphasize developer experience** with fluent API, LINQ-friendly design, and minimal boilerplate:

1. **Flexible column support** - Any number of columns with any header names
2. **Fluent, LINQ-friendly API** - Natural querying and filtering of rows
3. **Dictionary-like access** - Row indexer with column name for easy value retrieval
4. **Type safety** - Compile-time safety where possible, clear runtime errors otherwise
5. **Minimal boilerplate** - Simple, concise code in step definitions
6. **Backward compatibility** - Support existing Field/Value patterns during migration

## Proposed Design

### DataTable Class

```csharp
/// <summary>
/// Represents a table of test data with flexible column structure.
/// </summary>
/// <remarks>
/// Supports tables with any number of columns. The first row is treated as the header row
/// defining column names. Provides LINQ-friendly enumeration and dictionary-like column access.
/// </remarks>
/// <example>
/// <code>
/// // Create from jagged array (header + data rows)
/// var users = new DataTable(
///     ["Username", "Role"],
///     ["alice", "Owner"],
///     ["bob", "Editor"]
/// );
///
/// // Query with LINQ
/// var owners = users.Where(row => row["Role"] == "Owner");
/// var firstUser = users.First()["Username"];
///
/// // Single column table
/// var names = new DataTable(
///     ["Username"],
///     ["alice"],
///     ["bob"]
/// );
/// var allNames = names.Select(row => row["Username"]).ToList();
///
/// // Check if column exists
/// if (users.HasColumn("Email"))
/// {
///     var email = users.First()["Email"];
/// }
///
/// // Find row by column value
/// var aliceRow = users.Single(row => row["Username"] == "alice");
/// var aliceRole = aliceRow["Role"];
///
/// // Get all values from a column
/// var allUsernames = users.GetColumn("Username");
/// </code>
/// </example>
public class DataTable : IEnumerable<DataTableRow>
{
    private readonly List<DataTableRow> _rows;
    private readonly IReadOnlyList<string> _headers;

    /// <summary>
    /// Creates a new DataTable from header row and data rows.
    /// </summary>
    /// <param name="headers">The column names (first row).</param>
    /// <param name="dataRows">The data rows (subsequent rows).</param>
    /// <exception cref="ArgumentException">
    /// Thrown when headers are empty, data rows have inconsistent column counts,
    /// or data row count doesn't match header count.
    /// </exception>
    public DataTable(string[] headers, params string[][] dataRows)
    {
        if (headers == null || headers.Length == 0)
            throw new ArgumentException("Headers cannot be null or empty", nameof(headers));

        _headers = Array.AsReadOnly(headers);
        _rows = new List<DataTableRow>(dataRows.Length);

        foreach (var dataRow in dataRows)
        {
            if (dataRow.Length != _headers.Count)
            {
                throw new ArgumentException(
                    $"Data row has {dataRow.Length} columns but expected {_headers.Count} to match headers",
                    nameof(dataRows));
            }

            _rows.Add(new DataTableRow(_headers, dataRow));
        }
    }

    /// <summary>
    /// Gets the column headers for this table.
    /// </summary>
    public IReadOnlyList<string> Headers => _headers;

    /// <summary>
    /// Gets all rows in the table.
    /// </summary>
    public IReadOnlyList<DataTableRow> Rows => _rows.AsReadOnly();

    /// <summary>
    /// Gets the number of rows in the table (excluding header).
    /// </summary>
    public int RowCount => _rows.Count;

    /// <summary>
    /// Gets the number of columns in the table.
    /// </summary>
    public int ColumnCount => _headers.Count;

    /// <summary>
    /// Checks if a column with the specified name exists in the table.
    /// </summary>
    /// <param name="columnName">The column name to check.</param>
    /// <returns>True if the column exists; otherwise, false.</returns>
    public bool HasColumn(string columnName)
    {
        return _headers.Contains(columnName);
    }

    /// <summary>
    /// Gets all values from a specific column.
    /// </summary>
    /// <param name="columnName">The name of the column.</param>
    /// <returns>A collection of all values in the specified column.</returns>
    /// <exception cref="ArgumentException">Thrown when the column name does not exist.</exception>
    public IReadOnlyCollection<string> GetColumn(string columnName)
    {
        if (!HasColumn(columnName))
            throw new ArgumentException($"Column '{columnName}' not found. Available columns: {string.Join(", ", _headers)}", nameof(columnName));

        return _rows.Select(row => row[columnName]).ToList().AsReadOnly();
    }

    /// <summary>
    /// Gets a row by its zero-based index.
    /// </summary>
    /// <param name="index">The zero-based index of the row.</param>
    /// <returns>The row at the specified index.</returns>
    /// <exception cref="IndexOutOfRangeException">Thrown when index is out of range.</exception>
    public DataTableRow this[int index] => _rows[index];

    public IEnumerator<DataTableRow> GetEnumerator() => _rows.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Creates a string representation of the table for debugging.
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"DataTable ({_rows.Count} rows x {_headers.Count} columns)");
        sb.AppendLine("| " + string.Join(" | ", _headers) + " |");
        foreach (var row in _rows)
        {
            sb.AppendLine("| " + string.Join(" | ", row.Values) + " |");
        }
        return sb.ToString();
    }
}
```

### DataTableRow Class

```csharp
/// <summary>
/// Represents a single row in a DataTable with column-based access.
/// </summary>
public class DataTableRow
{
    private readonly IReadOnlyList<string> _headers;
    private readonly IReadOnlyList<string> _values;
    private readonly Dictionary<string, int> _columnIndex;

    /// <summary>
    /// Creates a new DataTableRow.
    /// </summary>
    /// <param name="headers">The column headers.</param>
    /// <param name="values">The row values (must match header count).</param>
    internal DataTableRow(IReadOnlyList<string> headers, string[] values)
    {
        if (headers.Count != values.Length)
            throw new ArgumentException($"Value count ({values.Length}) does not match header count ({headers.Count})");

        _headers = headers;
        _values = Array.AsReadOnly(values);

        // Build column name to index mapping for O(1) lookups
        _columnIndex = new Dictionary<string, int>(headers.Count);
        for (int i = 0; i < headers.Count; i++)
        {
            _columnIndex[headers[i]] = i;
        }
    }

    /// <summary>
    /// Gets all values in this row in column order.
    /// </summary>
    public IReadOnlyList<string> Values => _values;

    /// <summary>
    /// Gets the value for the specified column name.
    /// </summary>
    /// <param name="columnName">The name of the column.</param>
    /// <returns>The value at the specified column.</returns>
    /// <exception cref="ArgumentException">Thrown when the column name does not exist.</exception>
    public string this[string columnName]
    {
        get
        {
            if (!_columnIndex.TryGetValue(columnName, out int index))
            {
                throw new ArgumentException(
                    $"Column '{columnName}' not found. Available columns: {string.Join(", ", _headers)}",
                    nameof(columnName));
            }
            return _values[index];
        }
    }

    /// <summary>
    /// Gets the value at the specified column index.
    /// </summary>
    /// <param name="columnIndex">The zero-based column index.</param>
    /// <returns>The value at the specified index.</returns>
    /// <exception cref="IndexOutOfRangeException">Thrown when index is out of range.</exception>
    public string this[int columnIndex] => _values[columnIndex];

    /// <summary>
    /// Checks if a column with the specified name exists in this row.
    /// </summary>
    /// <param name="columnName">The column name to check.</param>
    /// <returns>True if the column exists; otherwise, false.</returns>
    public bool HasColumn(string columnName)
    {
        return _columnIndex.ContainsKey(columnName);
    }

    /// <summary>
    /// Tries to get the value for the specified column name.
    /// </summary>
    /// <param name="columnName">The name of the column.</param>
    /// <param name="value">The value if found; otherwise null.</param>
    /// <returns>True if the column exists; otherwise, false.</returns>
    public bool TryGetValue(string columnName, out string? value)
    {
        if (_columnIndex.TryGetValue(columnName, out int index))
        {
            value = _values[index];
            return true;
        }
        value = null;
        return false;
    }

    /// <summary>
    /// Creates a string representation of the row.
    /// </summary>
    public override string ToString()
    {
        return string.Join(" | ", _values);
    }
}
```

## Usage Examples

### Example 1: Single Column Table (Usernames)

**Gherkin:**
```gherkin
Given these users exist:
    | Username |
    | alice    |
    | bob      |
    | charlie  |
```

**Step Definition:**
```csharp
protected async Task GivenTheseUsersExist(DataTable users)
{
    // LINQ-friendly: Get all usernames
    var usernames = users.Select(row => row["Username"]).ToList();

    // Or: Use GetColumn helper
    var usernames = users.GetColumn("Username");

    // Create each user
    foreach (var username in usernames)
    {
        await testControlClient.CreateUserAsync(username);
    }
}
```

### Example 2: Two Column Table (Workspace + Role)

**Gherkin:**
```gherkin
Given I have access to these workspaces:
    | Workspace Name | My Role |
    | Personal       | Owner   |
    | Family Budget  | Editor  |
    | Tax Records    | Viewer  |
```

**Step Definition:**
```csharp
protected async Task GivenIHaveAccessToTheseWorkspaces(DataTable workspaces)
{
    // LINQ query: Filter and project
    var ownedWorkspaces = workspaces
        .Where(row => row["My Role"] == "Owner")
        .Select(row => row["Workspace Name"])
        .ToList();

    // Iterate all rows
    foreach (var row in workspaces)
    {
        var workspaceName = row["Workspace Name"];
        var role = row["My Role"];
        await SetupWorkspaceAccess(workspaceName, role);
    }

    // Or: Index by numeric position
    foreach (var row in workspaces)
    {
        var workspaceName = row[0]; // First column
        var role = row[1];          // Second column
        await SetupWorkspaceAccess(workspaceName, role);
    }
}
```

### Example 3: Finding Specific Rows

**Gherkin:**
```gherkin
And there are other workspaces in the system:
    | Workspace Name  | Owner   |
    | Private Data    | alice   |
    | Charlie's Taxes | charlie |
```

**Step Definition:**
```csharp
protected async Task AndThereAreOtherWorkspacesInTheSystem(DataTable workspaces)
{
    // Find specific row
    var aliceWorkspace = workspaces.Single(row => row["Owner"] == "alice");
    var aliceWorkspaceName = aliceWorkspace["Workspace Name"];

    // Check if column exists before accessing
    if (workspaces.HasColumn("Description"))
    {
        var description = workspaces.First()["Description"];
    }

    // Safe access with TryGetValue
    foreach (var row in workspaces)
    {
        if (row.TryGetValue("Description", out var description))
        {
            // Use description
        }
    }
}
```

### Example 4: Key-Value Pattern (Backward Compatibility)

**Gherkin:**
```gherkin
When I enter my credentials:
    | Field    | Value              |
    | Email    | user@example.com   |
    | Password | SecurePass123!     |
```

**Step Definition (New Pattern):**
```csharp
protected async Task WhenIEnterMyCredentials(DataTable credentials)
{
    // Option 1: Direct LINQ query (most concise)
    var email = credentials.Single(row => row["Field"] == "Email")["Value"];
    var password = credentials.Single(row => row["Field"] == "Password")["Value"];

    // Option 2: Extension method for key-value tables (add to helpers)
    var email = credentials.GetKeyValue("Email");
    var password = credentials.GetKeyValue("Password");

    await loginPage.EnterCredentialsAsync(email, password);
}

// Helper extension method for key-value pattern
public static class DataTableExtensions
{
    public static string GetKeyValue(this DataTable table, string key, string keyColumn = "Field", string valueColumn = "Value")
    {
        return table.Single(row => row[keyColumn] == key)[valueColumn];
    }
}
```

### Example 5: Empty and Single-Row Tables

```csharp
// Empty table (no data rows)
var empty = new DataTable(
    ["Username"]
    // No data rows
);
Assert.That(empty.RowCount, Is.EqualTo(0));

// Single row
var single = new DataTable(
    ["Username", "Role"],
    ["alice", "Owner"]
);
var username = single.First()["Username"];
```

## Migration Strategy

### Phase 1: Add New Implementation (Non-Breaking)

1. Add new [`DataTable`](tests/Functional/Helpers/DataTable.cs:55) and [`DataTableRow`](tests/Functional/Helpers/DataTable.cs:124) classes alongside existing implementation
2. Add extension methods for common patterns (key-value lookup)
3. Update documentation with new usage examples

### Phase 2: Migrate Existing Tests

1. Update [`Tenancy.feature`](tests/Functional/Features/Tenancy.feature) step definitions to use new API
2. Update [`AuthenticationSteps.cs`](tests/Functional/Steps/AuthenticationSteps.cs) to use new API
3. Remove helper methods like `GetTableValue()` that are no longer needed

### Phase 3: Remove Old Implementation

1. Verify all tests pass with new implementation
2. Remove old 2-column-specific implementation
3. Update all documentation

## Design Decisions

### 1. Constructor API: Array-Based

**Decision:** Use `string[]` arrays for headers and rows rather than collection initializer syntax.

**Rationale:**
- **Simple and clear**: `new DataTable(["Header1", "Header2"], ["val1", "val2"])`
- **Type safe**: Compiler enforces string arrays
- **IDE support**: Full IntelliSense and error checking
- **No ambiguity**: Each array is clearly a row

**Alternative considered:** Collection initializer with `List<string[]>`
```csharp
var table = new DataTable {
    { "Header1", "Header2" },
    { "val1", "val2" }
};
```
Rejected because: Ambiguous syntax, no clear distinction between header and data rows, poor IDE support.

### 2. LINQ-Friendly Enumeration

**Decision:** Implement `IEnumerable<DataTableRow>` directly on `DataTable`.

**Rationale:**
- Natural LINQ queries: `table.Where(...)`, `table.Select(...)`, `table.First()`
- Minimal ceremony: No need for `.Rows.Where(...)`
- Matches C# collection idioms

### 3. Column Access: String Indexer on Row

**Decision:** Provide `row["ColumnName"]` indexer for dictionary-like access.

**Rationale:**
- Intuitive: Matches how developers think about tables
- Concise: `row["Email"]` vs `row.GetValue("Email")`
- Common pattern: Matches ADO.NET DataRow, dictionary access

### 4. Error Handling: Fail Fast with Clear Messages

**Decision:** Throw exceptions with helpful messages listing available columns.

**Rationale:**
- **Developer experience**: Clear, actionable error messages
- **Early detection**: Catch typos during test development
- **No silent failures**: Better than returning null or empty string

Example error:
```
Column 'Emial' not found. Available columns: Email, Username, Password
```

### 5. Performance: O(1) Column Lookups

**Decision:** Use `Dictionary<string, int>` for column name to index mapping.

**Rationale:**
- Test tables are small (typically < 100 rows)
- Setup cost is negligible
- Enables constant-time lookups for cleaner code
- Worth the small memory overhead for better developer experience

## Benefits Over Current Implementation

1. **Flexibility**: Supports any number of columns, not just 2
2. **Less boilerplate**: No more manual helper methods like `GetTableValue()`
3. **LINQ-friendly**: Natural querying and filtering
4. **Type-safe**: Compile-time checking where possible
5. **Clear errors**: Helpful exception messages with available column names
6. **Better tooling**: Full IntelliSense support for LINQ operations
7. **Consistent**: One table class for all scenarios

## Testing Strategy

### Unit Tests for DataTable

```csharp
[Test]
public void Constructor_WithValidData_CreatesTable()
{
    var table = new DataTable(
        ["Name", "Age"],
        ["Alice", "30"],
        ["Bob", "25"]
    );

    Assert.That(table.RowCount, Is.EqualTo(2));
    Assert.That(table.ColumnCount, Is.EqualTo(2));
}

[Test]
public void Indexer_WithValidColumnName_ReturnsValue()
{
    var table = new DataTable(
        ["Name", "Age"],
        ["Alice", "30"]
    );

    Assert.That(table.First()["Name"], Is.EqualTo("Alice"));
    Assert.That(table.First()["Age"], Is.EqualTo("30"));
}

[Test]
public void Indexer_WithInvalidColumnName_ThrowsWithHelpfulMessage()
{
    var table = new DataTable(
        ["Name", "Age"],
        ["Alice", "30"]
    );

    var ex = Assert.Throws<ArgumentException>(() =>
    {
        var _ = table.First()["Email"];
    });

    Assert.That(ex.Message, Does.Contain("Email"));
    Assert.That(ex.Message, Does.Contain("Name, Age"));
}

[Test]
public void GetColumn_ReturnsAllValuesInColumn()
{
    var table = new DataTable(
        ["Name", "Age"],
        ["Alice", "30"],
        ["Bob", "25"]
    );

    var names = table.GetColumn("Name");

    Assert.That(names, Is.EquivalentTo(new[] { "Alice", "Bob" }));
}

[Test]
public void LinqWhere_FiltersRows()
{
    var table = new DataTable(
        ["Name", "Role"],
        ["Alice", "Owner"],
        ["Bob", "Editor"],
        ["Charlie", "Owner"]
    );

    var owners = table.Where(row => row["Role"] == "Owner").ToList();

    Assert.That(owners.Count, Is.EqualTo(2));
    Assert.That(owners[0]["Name"], Is.EqualTo("Alice"));
    Assert.That(owners[1]["Name"], Is.EqualTo("Charlie"));
}
```

## Design Decisions (Approved)

1. **Null/Empty Values**: Allow empty strings, treat null as empty string
   - Empty strings are valid cell values
   - Null parameters are converted to empty strings for consistency

2. **Type Conversion**: Start with strings only, add typed extensions later if needed
   - All cell values are strings (matching Gherkin table semantics)
   - Future enhancement: Add extension methods like `GetInt()`, `GetBool()` if needed

3. **Mutable vs Immutable**: Keep immutable for test reliability
   - Tables are read-only after construction
   - Ensures scenario data remains consistent throughout test execution

4. **Column Name Comparison**: Case-sensitive to match Gherkin table headers exactly
   - Column names must match headers precisely
   - Prevents subtle bugs from case mismatches

## Summary

The redesigned DataTable provides:

- ✅ **Flexible column structure** - Any number of columns
- ✅ **Fluent, LINQ-friendly API** - Natural querying with LINQ
- ✅ **Dictionary-like access** - Intuitive `row["ColumnName"]` syntax
- ✅ **Clear error messages** - Lists available columns on errors
- ✅ **Minimal boilerplate** - Concise code in step definitions
- ✅ **Type safe** - Compile-time array checking
- ✅ **Developer-friendly** - Full IntelliSense support

This design prioritizes developer experience while maintaining flexibility for various table structures in Gherkin scenarios.
