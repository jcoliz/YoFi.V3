# Object Store Step Contracts

The object store carries per-test state between step methods. Use the `RequiresObjects` and
`ProvidesObjects` attributes to make each step's state contract visible.

## Required and Provided Keys

- **Requires** lists keys that should already be present before the step runs.
- **Provides** lists keys the step creates or updates for later steps.
- Use constants from [`ObjectStoreKeys.cs`](../Infrastructure/ObjectStoreKeys.cs), not string
  literals.
- Add a note in XML documentation when a key is conditional or has a meaningful default.

Example from transaction setup:

```csharp
[RequiresObjects(ObjectStoreKeys.CurrentWorkspace, ObjectStoreKeys.LoggedInAs)]
[ProvidesObjects(
    ObjectStoreKeys.TransactionPayee,
    ObjectStoreKeys.TransactionAmount,
    ObjectStoreKeys.TransactionCategory,
    ObjectStoreKeys.TransactionMemo,
    ObjectStoreKeys.TransactionKey)]
public async Task GivenIHaveAWorkspaceWithATransaction(DataTable transactionTable)
{
    // Read workspace/user context, seed the transaction, then store returned values.
}
```

The attributes are metadata for documentation and possible validation; they do not themselves
populate the store or guarantee step ordering. The implementation must still read required values
with appropriate error handling and write the values it promises to provide.

When modifying a step, keep its attributes and XML remarks aligned with actual object-store access.
See [writing steps](writing-steps.md) and
[`TransactionDataSteps.cs`](../Steps/Transaction/TransactionDataSteps.cs) for current examples.
