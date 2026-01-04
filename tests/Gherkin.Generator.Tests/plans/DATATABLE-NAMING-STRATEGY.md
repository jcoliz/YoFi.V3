# DataTable Variable Naming Strategy

## Overview

When generating test code from Gherkin features, DataTable arguments need to be assigned variable names. The [`DataTableCrif.VariableName`](../CrifModels.cs:190) property holds this generated name.

## Naming Algorithm

The code generator determines variable names using this strategy:

### 1. Single DataTable Per Test

If a test has only one DataTable, use the simple name:
```
VariableName = "table"
```

**Example:**
```csharp
var table = new DataTable(
    ["Field", "Value"],
    ["Payee", "Coffee Shop"]
);
await TransactionDataSteps.GivenIHaveAWorkspaceWithATransaction(table);
```

### 2. Multiple DataTables Per Test

When a test has multiple DataTables, derive semantic names from the step text or use sequential fallback.

#### Strategy A: Semantic Names from Step Text

Extract meaningful keywords from the step text that describes the table's purpose:

| Step Text | Variable Name |
|-----------|--------------|
| "I fill in the following **transaction fields**:" | `fieldsTable` |
| "I should see the following **fields** in the create form:" | `fieldsTable` |
| "Given the following **users** exist:" | `usersTable` |
| "Then I should see these **transactions**:" | `transactionsTable` |

The generator should:
1. Look for keywords before/after "following" or "these"
2. Convert to camelCase
3. Append "Table" suffix

**Example with semantic names:**
```csharp
// Step: "When I fill in the following transaction fields:"
var fieldsTable = new DataTable(
    ["Field", "Value"],
    ["Date", "2024-06-15"]
);
await TransactionCreateSteps.WhenIFillInTheFollowingTransactionFields(fieldsTable);

// Step: "And I should see the following validation errors:"
var errorsTable = new DataTable(
    ["Field", "Error"],
    ["Payee", "Required"]
);
await ValidationSteps.ThenIShouldSeeTheFollowingValidationErrors(errorsTable);
```

#### Strategy B: Sequential Fallback

If semantic name extraction fails or names would collide, use sequential numbering:
```
VariableName = "table1", "table2", "table3", etc.
```

**Example with sequential fallback:**
```csharp
var table1 = new DataTable(
    ["Field", "Value"],
    ["Payee", "Coffee Shop"]
);
await TransactionDataSteps.GivenIHaveAWorkspaceWithATransaction(table1);

var table2 = new DataTable(
    ["Field", "Value"],
    ["Payee", "Gas Station"]
);
await TransactionDataSteps.GivenIHaveAWorkspaceWithATransaction(table2);
```

## Implementation Guidelines

### Semantic Name Extraction

The code generator should implement this logic when building the CRIF:

```csharp
string DeriveDataTableVariableName(Step step, int tableIndexInTest)
{
    // Single table in test? Use simple name
    if (tableIndexInTest == 0 && TotalTablesInTest() == 1)
        return "table";

    // Try to extract semantic name from step text
    var semanticName = ExtractSemanticName(step.Text);
    if (!string.IsNullOrEmpty(semanticName) && !IsNameCollision(semanticName))
        return semanticName;

    // Fall back to sequential numbering (1-based for user readability)
    return $"table{tableIndexInTest + 1}";
}

string ExtractSemanticName(string stepText)
{
    // Look for patterns like:
    // "following [keyword]:"
    // "these [keyword]:"
    // "the [keyword] are:"

    var patterns = new[]
    {
        @"following\s+(\w+)\s*:",
        @"these\s+(\w+)\s*:",
        @"the\s+(\w+)\s+(?:are|is):"
    };

    foreach (var pattern in patterns)
    {
        var match = Regex.Match(stepText, pattern, RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var keyword = match.Groups[1].Value;
            return ToCamelCase(keyword) + "Table";
        }
    }

    return null; // No semantic name found
}
```

### Name Collision Detection

The generator must track all variable names used in a test method to avoid collisions:

```csharp
class TestVariableTracker
{
    private HashSet<string> _usedNames = new();

    public bool IsNameCollision(string name)
    {
        return _usedNames.Contains(name);
    }

    public void RegisterName(string name)
    {
        _usedNames.Add(name);
    }
}
```

## Real-World Examples

### Example 1: Single Table (Simple Name)
```gherkin
Scenario: Quick edit modal shows transaction data
  Given I have a workspace with a transaction:
    | Field    | Value       |
    | Payee    | Coffee Shop |
    | Amount   | 5.50        |
```

**Generated:**
```csharp
var table = new DataTable(
    ["Field", "Value"],
    ["Payee", "Coffee Shop"],
    ["Amount", "5.50"]
);
await TransactionDataSteps.GivenIHaveAWorkspaceWithATransaction(table);
```

### Example 2: Multiple Tables (Semantic Names)
```gherkin
Scenario: User creates transaction with validation
  Given I am on the transactions page
  When I fill in the following transaction fields:
    | Field  | Value        |
    | Payee  | Office Depot |
    | Amount | 250.75       |
  And I click "Save"
  Then I should see the following validation errors:
    | Field | Error    |
    | Date  | Required |
```

**Generated:**
```csharp
// Step: "When I fill in the following transaction fields:"
var fieldsTable = new DataTable(
    ["Field", "Value"],
    ["Payee", "Office Depot"],
    ["Amount", "250.75"]
);
await TransactionCreateSteps.WhenIFillInTheFollowingTransactionFields(fieldsTable);

// Step: "Then I should see the following validation errors:"
var errorsTable = new DataTable(
    ["Field", "Error"],
    ["Date", "Required"]
);
await ValidationSteps.ThenIShouldSeeTheFollowingValidationErrors(errorsTable);
```

### Example 3: Multiple Tables (Sequential Fallback)
```gherkin
Scenario: Comparing two transactions
  Given I have a transaction:
    | Field  | Value       |
    | Payee  | Coffee Shop |
  And I have a transaction:
    | Field  | Value       |
    | Payee  | Gas Station |
```

**Generated (same step text, so semantic extraction produces collision):**
```csharp
var table1 = new DataTable(
    ["Field", "Value"],
    ["Payee", "Coffee Shop"]
);
await TransactionDataSteps.GivenIHaveATransaction(table1);

var table2 = new DataTable(
    ["Field", "Value"],
    ["Payee", "Gas Station"]
);
await TransactionDataSteps.GivenIHaveATransaction(table2);
```

## Summary

The naming strategy prioritizes:
1. **Simplicity** - Use "table" for single-table tests
2. **Readability** - Extract semantic names from step text when possible
3. **Reliability** - Fall back to sequential numbering to avoid collisions

This approach balances human-readable code with robust generation logic.
