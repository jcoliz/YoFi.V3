---
status: Draft
purpose: Example of generated code for datatable handling in functional test generation
---

# DataTable Generation Example - Option 1

This document shows what generated C# code looks like when using the DataTable helper class approach.

## Input: Gherkin Feature File

```gherkin
Feature: Transaction Record Fields
  As a user managing transactions
  I want to record additional details about each transaction
  So that I can track memo notes, source accounts, and external identifiers

  Background:
    Given the application is running
    And I am logged in as a user with "Editor" role

  Rule: Quick Edit Modal
    The quick edit modal should only show Payee, Category, and Memo fields

    Scenario: Quick edit modal shows Payee, Category, and Memo fields
      Given I have a workspace with a transaction:
        | Field    | Value          |
        | Payee    | Coffee Shop    |
        | Amount   | 5.50           |
        | Category | Beverages      |
        | Memo     | Morning coffee |
      And I am on the transactions page
      When I click the "Edit" button on the transaction
      Then I should see a modal titled "Quick Edit Transaction"
      And I should only see fields for "Payee", "Category", and "Memo"
      And the fields match the expected values
      And I should not see fields for "Date", "Amount", "Source", or "ExternalId"

    Scenario: User creates transaction with all fields populated
      Given I am on the transactions page
      When I click the "Add Transaction" button
      And I fill in the following transaction fields:
        | Field       | Value                        |
        | Date        | 2024-06-15                   |
        | Payee       | Office Depot                 |
        | Amount      | 250.75                       |
        | Category    | Office Supplies              |
        | Memo        | Printer paper and toner      |
        | Source      | Business Card                |
        | External ID | OD-2024-0615-001             |
      And I click "Save"
      Then the modal should close
      And I should see a transaction with Payee "Office Depot"
      And it contains the expected list fields
```

## Output: Generated C# Test File

```csharp
using NUnit.Framework;
using YoFi.V3.Tests.Functional.Steps;
using YoFi.V3.Tests.Functional.Steps.Transaction;
using YoFi.V3.Tests.Functional.Steps.Workspace;
using YoFi.V3.Tests.Functional.Helpers;
using YoFi.V3.Tests.Functional.Infrastructure;

namespace YoFi.V3.Tests.Functional.Features;

/// <summary>
/// Transaction Record Fields
/// As a user managing transactions
/// I want to record additional details about each transaction
/// So that I can track memo notes, source accounts, and external identifiers
/// </summary>
public class TransactionRecordFieldsTests : FunctionalTestBase
{
    #region Step class references

    protected NavigationSteps NavigationSteps => _navigationSteps ??= new(this);
    private NavigationSteps? _navigationSteps;

    protected AuthSteps AuthSteps => _authSteps ??= new(this);
    private AuthSteps? _authSteps;

    protected WorkspaceDataSteps WorkspaceDataSteps => _workspaceDataSteps ??= new(this);
    private WorkspaceDataSteps? _workspaceDataSteps;

    protected TransactionDataSteps TransactionDataSteps => _transactionDataSteps ??= new(this);
    private TransactionDataSteps? _transactionDataSteps;

    protected TransactionListSteps TransactionListSteps => _transactionListSteps ??= new(this);
    private TransactionListSteps? _transactionListSteps;

    protected TransactionQuickEditSteps TransactionQuickEditSteps => _transactionQuickEditSteps ??= new(this);
    private TransactionQuickEditSteps? _transactionQuickEditSteps;

    protected TransactionCreateSteps TransactionCreateSteps => _transactionCreateSteps ??= new(this);
    private TransactionCreateSteps? _transactionCreateSteps;

    protected TransactionSharedSteps TransactionSharedSteps => _transactionSharedSteps ??= new(this);
    private TransactionSharedSteps? _transactionSharedSteps;

    #endregion

    [SetUp]
    public async Task SetupAsync()
    {
        // Given the application is running
        await NavigationSteps.GivenLaunchedSite();

        // And I am logged in as a user with "Editor" role
        await WorkspaceDataSteps.GivenIAmLoggedInAsAUserWithEditorRole();
    }

    #region Rule: Quick Edit Modal
    // The quick edit modal should only show Payee, Category, and Memo fields

    /// <summary>
    /// Scenario: Quick edit modal shows Payee, Category, and Memo fields
    /// </summary>
    [Test]
    public async Task QuickEditModalShowsPayeeCategoryAndMemoFields()
    {
        // Given I have a workspace with a transaction:
        var table = new DataTable(
            ["Field", "Value"],
            ["Payee", "Coffee Shop"],
            ["Amount", "5.50"],
            ["Category", "Beverages"],
            ["Memo", "Morning coffee"]
        );
        await TransactionDataSteps.GivenIHaveAWorkspaceWithATransaction(table);

        // And I am on the transactions page
        await TransactionListSteps.GivenIAmOnTheTransactionsPage();

        // When I click the "Edit" button on the transaction
        await TransactionQuickEditSteps.WhenIClickTheEditButtonOnTheTransaction();

        // Then I should see a modal titled "Quick Edit Transaction"
        await TransactionQuickEditSteps.ThenIShouldSeeAModalTitled("Quick Edit Transaction");

        // And I should only see fields for "Payee", "Category", and "Memo"
        await TransactionQuickEditSteps.ThenIShouldOnlySeeFieldsForPayeeCategoryAndMemo();

        // And the fields match the expected values
        await TransactionQuickEditSteps.ThenTheFieldsMatchTheExpectedValues();

        // And I should not see fields for "Date", "Amount", "Source", or "ExternalId"
        await TransactionQuickEditSteps.ThenIShouldNotSeeFieldsForDateAmountSourceOrExternalId();
    }

    /// <summary>
    /// Scenario: User creates transaction with all fields populated
    /// </summary>
    [Test]
    public async Task UserCreatesTransactionWithAllFieldsPopulated()
    {
        // Given I am on the transactions page
        await TransactionListSteps.GivenIAmOnTheTransactionsPage();

        // When I click the "Add Transaction" button
        await TransactionCreateSteps.WhenIClickTheAddTransactionButton();

        // And I fill in the following transaction fields:
        var fieldsTable = new DataTable(
            ["Field", "Value"],
            ["Date", "2024-06-15"],
            ["Payee", "Office Depot"],
            ["Amount", "250.75"],
            ["Category", "Office Supplies"],
            ["Memo", "Printer paper and toner"],
            ["Source", "Business Card"],
            ["External ID", "OD-2024-0615-001"]
        );
        await TransactionCreateSteps.WhenIFillInTheFollowingTransactionFields(fieldsTable);

        // And I click "Save"
        await TransactionSharedSteps.WhenIClickSave();

        // Then the modal should close
        await TransactionSharedSteps.ThenTheModalShouldClose();

        // And I should see a transaction with Payee "Office Depot"
        await TransactionCreateSteps.ThenIShouldSeeATransactionWithPayee("Office Depot");

        // And it contains the expected list fields
        await TransactionCreateSteps.ThenItContainsTheExpectedListFields();
    }

    #endregion
}
```

## Key Features of Generated Code

### 1. DataTable Instantiation (Lines 70-76, 105-113)

The generator creates a `new DataTable()` call with:
- **Header row** as the first parameter (array initializer)
- **Data rows** as subsequent parameters (variadic params array)
- Each cell is a string literal

### 2. Variable Naming Strategy

- **Simple case**: `table` when there's only one datatable in a test
- **Multiple tables**: `fieldsTable`, `usersTable` etc. derived from step text context
- **Sequential fallback**: `table1`, `table2` if semantic names aren't clear

### 3. Arbitrary Columns Support

The approach handles **any number of columns** because:
- The `DataTable` constructor accepts `string[]` headers and `params string[][]` rows
- No hardcoded column names or mappings needed
- Step methods receive `DataTable` parameter and query it using column indexer: `table["ColumnName"]`

### 4. Step Method Integration

Step methods that accept datatables have this signature:

```csharp
public async Task GivenIHaveAWorkspaceWithATransaction(DataTable table)
{
    // Access columns by name
    var payee = table.Rows[0]["Payee"];
    var amount = table.Rows[0]["Amount"];

    // Or use LINQ
    var hasCategory = table.Rows[0].HasColumn("Category");

    // Or enumerate rows
    foreach (var row in table)
    {
        var field = row["Field"];
        var value = row["Value"];
    }
}
```

## Why This Approach Works

✅ **Type-safe** - Compiler validates DataTable instantiation
✅ **Flexible** - Works with 2 columns, 10 columns, or any number
✅ **Consistent** - Matches existing codebase patterns
✅ **No code generation complexity** - Simple string template substitution
✅ **Runtime querying** - Step implementations can check which columns exist
✅ **Debuggable** - DataTable has `ToString()` for inspection

## Comparison with Raw Gherkin Table

**Gherkin AST** (what parser provides):
```
DataTable {
  Rows: [
    { Cells: ["Field", "Value"] },
    { Cells: ["Payee", "Coffee Shop"] },
    { Cells: ["Amount", "5.50"] }
  ]
}
```

**Generated C# code**:
```csharp
var table = new DataTable(
    ["Field", "Value"],
    ["Payee", "Coffee Shop"],
    ["Amount", "5.50"]
);
```

The transformation is straightforward - each Gherkin row becomes a C# array initializer parameter.
