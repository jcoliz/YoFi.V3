# ObjectStore Documentation Pattern

## Overview

Step methods that interact with the ObjectStore should document their dependencies (required keys) and outputs (provided keys) in a consistent, concise format.

## Documentation Pattern

```csharp
/// <summary>
/// Brief description of what the step does.
/// </summary>
/// <param name="paramName">Parameter description if applicable.</param>
/// <remarks>
/// Detailed explanation of behavior, side effects, and usage notes.
///
/// Requires Keys
/// - KeyName1
/// - KeyName2
///
/// Provides Keys
/// - KeyName3
/// - KeyName4 (conditional note if not always set)
/// </remarks>
[RequiresObjects(ObjectStoreKeys.KeyName1, ObjectStoreKeys.KeyName2)]
[ProvidesObjects(ObjectStoreKeys.KeyName3, ObjectStoreKeys.KeyName4)]
public async Task MethodName(...)
```

## Key Sections

### Requires Objects
Lists ObjectStore keys that **must be present** before this method executes. The method will throw if these are missing.

### Provides Objects
Lists ObjectStore keys that this method **adds or updates**. These become available for subsequent steps.

### Conditional Objects
If a key is only provided under certain conditions, add a note in parentheses:
```
Provides Objects
- TransactionSource (if specified in table)
- TransactionExternalId (if non-empty)
```

## Attributes (Future Enhancement)

Custom attributes for machine-readable metadata:

```csharp
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class RequiresObjectsAttribute : Attribute
{
    public string[] Keys { get; }
    public RequiresObjectsAttribute(params string[] keys) => Keys = keys;
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class ProvidesObjectsAttribute : Attribute
{
    public string[] Keys { get; }
    public ProvidesObjectsAttribute(params string[] keys) => Keys = keys;
}
```

These could enable:
- Test validation at startup (check all required keys are available)
- Dependency graph visualization
- Test execution order optimization
- Documentation generation

## Complete Examples

### Example 1: Setup Step (Provides Keys)

```csharp
/// <summary>
/// Creates test user credentials and logs them in.
/// </summary>
/// <param name="shortName">The username (without __TEST__ prefix).</param>
/// <remarks>
/// Creates user via Test Control API, performs login via UI, and stores
/// the full username for later reference.
///
/// Provides Objects
/// - LoggedInAs
/// </remarks>
[ProvidesObjects(ObjectStoreKeys.LoggedInAs)]
public async Task GivenIAmLoggedInAs(string shortName = "I")
{
    var cred = _context.GetUserCredentials(shortName);
    var loginPage = _context.GetOrCreatePage<LoginPage>();
    await loginPage.NavigateAsync();
    await loginPage.LoginAsync(cred.Username, cred.Password);
    await _context.Page.WaitForURLAsync(url => !url.Contains("/login"), new() { Timeout = 10000 });
    _context.ObjectStore.Add(ObjectStoreKeys.LoggedInAs, cred.Username);
}
```

### Example 2: Operation Step (Requires and Provides)

```csharp
/// <summary>
/// Seeds a transaction with specified fields into the current workspace.
/// </summary>
/// <param name="transactionTable">DataTable with Field/Value columns containing transaction properties.</param>
/// <remarks>
/// Parses transaction data from table, seeds via Test Control API, and stores transaction
/// details in object store for later verification. Does NOT navigate to transactions page -
/// use separate navigation step. Default amount is 100.00 if not specified.
///
/// Table format (Field/Value):
/// | Field    | Value           |
/// | Payee    | Coffee Shop     |
/// | Amount   | 5.50            |
/// | Category | Beverages       |
/// | Memo     | Morning coffee  |
///
/// Requires Objects
/// - CurrentWorkspace
/// - LoggedInAs
///
/// Provides Objects
/// - TransactionPayee
/// - TransactionAmount
/// - TransactionCategory
/// - TransactionMemo
/// - TransactionSource (if specified)
/// - TransactionExternalId (if specified)
/// - TransactionKey
/// </remarks>
[RequiresObjects(ObjectStoreKeys.CurrentWorkspace, ObjectStoreKeys.LoggedInAs)]
[ProvidesObjects(
    ObjectStoreKeys.TransactionPayee,
    ObjectStoreKeys.TransactionAmount,
    ObjectStoreKeys.TransactionCategory,
    ObjectStoreKeys.TransactionMemo,
    ObjectStoreKeys.TransactionKey)]
public async Task GivenIHaveAWorkspaceWithATransaction(DataTable transactionTable)
{
    // Implementation...
}
```

### Example 3: Assertion Step (Requires Keys Only)

```csharp
/// <summary>
/// Verifies that the updated memo appears in the transaction list.
/// </summary>
/// <remarks>
/// Retrieves the payee and new memo from object store, waits for page to update,
/// and verifies the memo in the transaction list matches the updated value.
///
/// Requires Objects
/// - TransactionPayee
/// - TransactionMemo
/// </remarks>
[RequiresObjects(ObjectStoreKeys.TransactionPayee, ObjectStoreKeys.TransactionMemo)]
public async Task ThenIShouldSeeTheUpdatedMemoInTheTransactionList()
{
    var payee = _context.ObjectStore.Get<string>(ObjectStoreKeys.TransactionPayee)
        ?? throw new InvalidOperationException($"{ObjectStoreKeys.TransactionPayee} not found in object store");
    var expectedMemo = _context.ObjectStore.Get<string>(ObjectStoreKeys.TransactionMemo)
        ?? throw new InvalidOperationException($"{ObjectStoreKeys.TransactionMemo} not found in object store");

    var transactionsPage = _context.GetOrCreatePage<TransactionsPage>();
    await transactionsPage.WaitForLoadingCompleteAsync();

    var actualMemo = await transactionsPage.GetTransactionMemoAsync(payee);
    Assert.That(actualMemo?.Trim(), Is.EqualTo(expectedMemo));
}
```

## Benefits

1. **Discoverability**: Developers can quickly see what data a step needs/provides
2. **Documentation**: Clear contract for step composition
3. **Maintainability**: Easy to track data flow through test scenarios
4. **Future Tooling**: Attributes enable validation and visualization
5. **Concise**: Minimal verbosity while maintaining clarity

## Migration Strategy

1. Add documentation to new steps immediately
2. Update existing steps opportunistically (when editing for other reasons)
3. Prioritize high-usage composite steps first
4. Consider tooling to auto-generate from code analysis

## See Also

- [`ObjectStoreKeys.cs`](../Infrastructure/ObjectStoreKeys.cs) - Central key registry
- [`docs/wip/functional-tests/OBJECTSTORE-KEY-PATTERNS-ANALYSIS.md`](../../../docs/wip/functional-tests/OBJECTSTORE-KEY-PATTERNS-ANALYSIS.md) - Analysis of current usage
