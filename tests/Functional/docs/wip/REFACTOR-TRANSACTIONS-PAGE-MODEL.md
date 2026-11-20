# Refactoring the Transactions Page Object Model

> This file is getting very large. Could you recommend a strategy for dividing it into smaller compilation units?
>
> Something I have done for reusable components is to move logic into a Components object model, and then just retain one synbol pointing to the component, that a caller could consume.
>
> Open to other ideas.

I recommend using composition as the long-term design, with partial files as a short-term migration aid.

## Recommended component boundaries

### 1. `TransactionsSearch`

Extract the cohesive `Search region`:

- Root, input, search, clear, and no-results locators
- Search/clear operations
- Search-specific assertions

Expose from the page:

```csharp
public TransactionsSearch Search { get; }
```

### 2. `TransactionsFilters`

Extract the `Filters region`:

- Filter toggle and pane
- All filter inputs
- Date presets
- Uncategorized checkbox
- Apply, clear, expand, and assertion methods

Expose:

```csharp
public TransactionsFilters Filters { get; }
```

The filters component should invalidate its consumer’s table cache after requests that change results. Rather than reaching back into `TransactionsPage`, it could expose an event or accept a small callback such as `Func<Task>` for cache invalidation.

### 3. `TransactionsTable`

Extract most of the `Transactions Table and Row Data region`:

- Table, card, rows, empty state, and badges
- `_cachedTableData`
- `TransactionTableData` and `TransactionRowData`
- Table loading, reload, lookup, and cell methods
- Counts, keys, payees, and row-field queries
- Pagination locators and total-item retrieval

Expose:

```csharp
public TransactionsTable Table { get; }
```

This is currently the strongest extraction candidate because its cache and supporting data types form a natural ownership boundary.

I would move `TransactionRowData` and `TransactionTableData` out of `TransactionsPage` even if they initially remain in the table-related compilation unit.

### 4. Transaction dialog components

Extract the three transaction forms:

- `TransactionCreateDialog` from `Create Transaction`
- `TransactionEditDialog` from `Edit Transaction`
- `TransactionDeleteDialog` from `Delete Transaction`

Each component would own its:

- Root dialog locator
- Form-field locators
- Buttons
- Fill, submit, and cancel operations
- Dialog visibility checks

Expose:

```csharp
public TransactionCreateDialog Create { get; }
public TransactionEditDialog Edit { get; }
public TransactionDeleteDialog Delete { get; }
```

The page should retain high-level workflows that coordinate a row, dialog, API request, and table refresh—for example, `CreateTransactionAsync`, `UpdateTransactionAsync`, and `DeleteTransactionAsync`.

### 5. `CreatePayeeRuleDialog`

Extract `Create Rule from Transaction`:

- Dialog locators
- Form entry
- Toast state and assertions
- Open/submit behavior

Expose:

```csharp
public CreatePayeeRuleDialog CreateRule { get; }
```

The rule action menu may remain in a transaction-row abstraction, with the dialog component owning only the modal workflow.

## Optional row-level component

A `TransactionRow` or `TransactionTableRow` object could encapsulate actions currently scattered across the page:

- Edit button
- Delete button
- Create-rule button
- Row action menu
- Date, payee, amount, category, and memo retrieval
- Permission checks

`TransactionsTable` would return it:

```csharp
var row = await Table.FindRowByPayeeAsync(payee);
await row.EditAsync();
```

This is useful if row-specific behavior continues growing, but I would make it a second-stage extraction rather than adding it immediately.

## Responsibilities left in `TransactionsPage`

Keep only page-level concerns:

- Navigation and page readiness
- `WorkspaceSelector` and `ErrorDisplay`
- Page-level locators and state
- API waiting/coordinated mutation workflows
- Construction and exposure of child component models
- Narrow compatibility methods during migration

The page should be an orchestrator, not the owner of every locator on the screen.

## Handling shared API behavior

Several extracted operations currently call `WaitForApi`. Components should not inherit from `BasePage` solely to gain this behavior.

Two reasonable approaches are:

1. Components receive a small page-test dependency that provides `WaitForApi` and request regexes.
2. Components perform the browser interaction, while `TransactionsPage` wraps that operation with `WaitForApi` for page-level workflows.

I prefer the second approach for small components. It keeps components focused on DOM interaction and leaves request sequencing at the page-orchestration level.

## Compilation-unit alternative

Because this is already a partial class, a fast mechanical split could be:

- `TransactionsPage.cs`
- `TransactionsPage.Search.cs`
- `TransactionsPage.Filters.cs`
- `TransactionsPage.Table.cs`
- `TransactionsPage.Create.cs`
- `TransactionsPage.Edit.cs`
- `TransactionsPage.Delete.cs`
- `TransactionsPage.CreateRule.cs`

This is appropriate as an interim step, but it only reduces file size. It does not reduce coupling or simplify the API, because every compilation unit still manipulates the same class state.

## Suggested migration sequence

1. Split the current regions into partial files so changes become reviewable.
2. Extract `TransactionsTable` and its cache/data types.
3. Extract `TransactionsSearch` and `TransactionsFilters`.
4. Extract the three dialogs.
5. Extract rule creation.
6. Update callers from page forwarding methods to component properties.
7. Remove forwarding methods when no callers remain.

This preserves compatibility while progressively simplifying the page, rather than requiring all step definitions to change at once.
