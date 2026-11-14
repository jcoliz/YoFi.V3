# Writing Step Definitions

Step definitions bind Gherkin phrases to C# methods and coordinate the test. They belong under
[`Steps/`](../Steps/), organized by product area or responsibility.

## Add a Step

1. Search for a matching step in the feature files and existing step classes before creating one.
2. Add the precise Gherkin phrase as a `[Given]`, `[When]`, or `[Then]` attribute.
3. Keep the method focused on scenario orchestration. Use a page/component object for Playwright
   work and `TestControlClient` for test setup.
4. Build the functional test project to check generator binding and C# compilation.

Example:

```csharp
[When("I quick edit the transaction")]
public async Task WhenIQuickEditTheTransaction()
{
    var payee = _context.ObjectStore.Get<string>(ObjectStoreKeys.TransactionPayee)
        ?? throw new InvalidOperationException("TransactionPayee not found in object store");
    var page = _context.GetOrCreatePage<TransactionsPage>();
    await page.OpenEditModalAsync(payee);
}
```

Step classes take `ITestContext` in their constructor. The context provides access to the browser
page, Test Control client, object store, credentials, workspaces, and page-object factory. See
[`ITestContext`](../Infrastructure/ITestContext.cs) and existing step classes for the local pattern.

## Test Data Setup

Use backend test-control operations for setup that does not need to be exercised through the UI.
Create test users/workspaces with the existing helpers, then create transaction fixtures through
`TestControl_SeedTransactionsPreciseAsync` using the scenario's exact values. The common single-
transaction step defaults to the test runner's local calendar date. `Days Ago` subtracts calendar
days from today (`0` means today); an explicit `Date` in `yyyy-MM-dd` format takes precedence.
The generated C# client uses `DateOnly` for OpenAPI date fields, without time-zone conversion.
When a test depends on a specific date, use the exact-date setup step or pass `Date` in the table. Avoid random/generated
transaction values when the test depends on a particular amount, external ID, payee, or date.

The established transaction setup steps live in
[`TransactionDataSteps.cs`](../Steps/Transaction/TransactionDataSteps.cs). They read workspace and
user state from the object store, call the precise seed endpoint, then store the returned transaction
values and key for subsequent assertions. See [Test Control APIs](TEST-CONTROLS.md) for details.

## Share State Deliberately

Use `ObjectStoreKeys` rather than ad hoc string literals when steps need to share state. Document
which keys a step requires and provides using the existing `[RequiresObjects]` and `[ProvidesObjects]`
attributes. A subsequent step should only assume a key exists if setup has provided it. See
[Object Store step contracts](OBJECTSTORE-DOCUMENTATION-PATTERN.md).

Keep ownership clear: setup steps create and track resources; assertion steps read the state needed
to verify the outcome. The test base cleans up tracked test users and workspaces after each test.

## Assertions and Errors

Put assertions in `Then` steps. Make failure messages identify the expected behavior or value. Do not
silently convert failed API setup into success-shaped state; surface API failures with useful
diagnostics.

Avoid putting Playwright locators or low-level browser mechanics in step definitions. See
[writing page objects](writing-pages.md).
