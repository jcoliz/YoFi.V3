namespace YoFi.V3.Tests.Functional.Helpers;

/// <summary>
/// Extension methods for DataTable to support common usage patterns.
/// </summary>
public static class DataTableExtensions
{
    /// <summary>
    /// Gets a value from a key-value table where one column contains keys and another contains values.
    /// </summary>
    /// <param name="table">The data table.</param>
    /// <param name="key">The key to search for.</param>
    /// <param name="keyColumn">The name of the column containing keys (default: "Field").</param>
    /// <param name="valueColumn">The name of the column containing values (default: "Value").</param>
    /// <returns>The value associated with the specified key.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no row with the specified key is found.</exception>
    /// <example>
    /// <code>
    /// // Given a table like:
    /// // | Field    | Value           |
    /// // | Email    | user@example.com |
    /// // | Password | SecurePass123!   |
    ///
    /// var email = table.GetKeyValue("Email");
    /// var password = table.GetKeyValue("Password");
    /// </code>
    /// </example>
    public static string GetKeyValue(this DataTable table, string key, string keyColumn = "Field", string valueColumn = "Value")
    {
        var row = table.SingleOrDefault(r => r[keyColumn] == key);
        if (row == null)
        {
            throw new InvalidOperationException(
                $"No row found with {keyColumn}='{key}'. Available keys: {string.Join(", ", table.Select(r => r[keyColumn]))}");
        }
        return row[valueColumn];
    }

    /// <summary>
    /// Tries to get a value from a key-value table.
    /// </summary>
    /// <param name="table">The data table.</param>
    /// <param name="key">The key to search for.</param>
    /// <param name="value">The value if found; otherwise null.</param>
    /// <param name="keyColumn">The name of the column containing keys (default: "Field").</param>
    /// <param name="valueColumn">The name of the column containing values (default: "Value").</param>
    /// <returns>True if the key was found; otherwise, false.</returns>
    public static bool TryGetKeyValue(this DataTable table, string key, out string? value, string keyColumn = "Field", string valueColumn = "Value")
    {
        var row = table.SingleOrDefault(r => r[keyColumn] == key);
        if (row != null)
        {
            value = row[valueColumn];
            return true;
        }
        value = null;
        return false;
    }

    /// <summary>
    /// Gets all values from the first column (useful for single-column tables).
    /// </summary>
    /// <param name="table">The data table.</param>
    /// <returns>A collection of all values from the first column.</returns>
    /// <example>
    /// <code>
    /// // Given a table like:
    /// // | Username |
    /// // | alice    |
    /// // | bob      |
    ///
    /// var usernames = table.GetFirstColumn(); // ["alice", "bob"]
    /// </code>
    /// </example>
    public static IReadOnlyCollection<string> GetFirstColumn(this DataTable table)
    {
        if (table.ColumnCount == 0)
            throw new InvalidOperationException("Table has no columns");

        return table.Select(row => row[0]).ToList().AsReadOnly();
    }

    /// <summary>
    /// Converts a single-column table to a read-only collection of values.
    /// </summary>
    /// <param name="table">The data table.</param>
    /// <returns>A read-only collection of all values from the single column.</returns>
    /// <exception cref="InvalidOperationException">Thrown when table does not have exactly one column.</exception>
    /// <example>
    /// <code>
    /// // Given a table like:
    /// // | Username |
    /// // | alice    |
    /// // | bob      |
    ///
    /// var usernames = table.ToSingleColumnList(); // ["alice", "bob"]
    /// </code>
    /// </example>
    public static IReadOnlyCollection<string> ToSingleColumnList(this DataTable table)
    {
        if (table.ColumnCount != 1)
            throw new InvalidOperationException($"Table must have exactly one column, but has {table.ColumnCount}");

        return table.Select(row => row[0]).ToList().AsReadOnly();
    }
}
