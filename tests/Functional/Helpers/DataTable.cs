using System.Collections;
using System.Text;

namespace YoFi.V3.Tests.Functional.Helpers;

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
