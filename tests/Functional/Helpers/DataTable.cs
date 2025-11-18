using System.Collections;

namespace YoFi.V3.Tests.Functional.Helpers;

/// <summary>
/// Represents a data table for test scenarios with field-value pairs
/// </summary>
/// <example>
/// <code>
/// // Create a table with registration data
/// var registrationData = new Table(
///     ("Email", "user@example.com"),
///     ("Username", "testuser"),
///     ("Password", "SecurePass123!"),
///     ("Confirm Password", "SecurePass123!")
/// );
/// 
/// // Or create and populate manually
/// var credentialsData = new Table();
/// credentialsData.AddRow("Email", "user@example.com");
/// credentialsData.AddRow("Password", "wrongpassword");
/// 
/// // Get values by field name
/// var email = registrationData.GetValue("Email");
/// var username = registrationData.GetValue("Username");
/// 
/// // Use in test methods
/// protected async Task WhenIEnterCredentials(Table credentialsData)
/// {
///     var email = credentialsData.GetValue("Email");
///     var password = credentialsData.GetValue("Password");
///     await loginPage.EnterCredentialsAsync(email, password);
/// }
/// </code>
/// </example>
public class DataTable : IEnumerable<DataTableRow>
{
    private readonly List<DataTableRow> _rows;

    public DataTable()
    {
        _rows = new List<DataTableRow>();
    }

    public DataTable(params (string field, string value)[] data)
    {
        _rows = new List<DataTableRow>();
        foreach (var (field, value) in data)
        {
            AddRow(field, value);
        }
    }

    /// <summary>
    /// Gets all rows in the table
    /// </summary>
    public IReadOnlyList<DataTableRow> Rows => _rows.AsReadOnly();

    /// <summary>
    /// Add a new row to the table
    /// </summary>
    public void AddRow(string field, string value)
    {
        _rows.Add(new DataTableRow(field, value));
    }

    /// <summary>
    /// Get value by field name
    /// </summary>
    /// <param name="fieldName">The name of the field to retrieve</param>
    /// <returns>The value associated with the field name</returns>
    /// <exception cref="ArgumentException">Thrown when the field name is not found</exception>
    public string GetValue(string fieldName)
    {
        var row = _rows.FirstOrDefault(r => r.Field == fieldName);
        if (row == null)
        {
            throw new ArgumentException($"Field '{fieldName}' not found in table data");
        }
        return row.Value;
    }

    /// <summary>
    /// Check if a field exists in the table
    /// </summary>
    public bool HasField(string fieldName)
    {
        return _rows.Any(r => r.Field == fieldName);
    }

    public IEnumerator<DataTableRow> GetEnumerator()
    {
        return _rows.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

/// <summary>
/// Represents a single row in a test data table
/// </summary>
public class DataTableRow
{
    public DataTableRow(string field, string value)
    {
        Field = field ?? throw new ArgumentNullException(nameof(field));
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// The field name
    /// </summary>
    public string Field { get; }

    /// <summary>
    /// The field value
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Indexer to access value by column name (for compatibility with dictionary-like access)
    /// </summary>
    public string this[string columnName]
    {
        get
        {
            return columnName switch
            {
                "Field" => Field,
                "Value" => Value,
                _ => throw new ArgumentException($"Column '{columnName}' not found. Available columns: Field, Value")
            };
        }
    }

    public override string ToString()
    {
        return $"{Field}: {Value}";
    }
}