---
status: Approved
ado: Scenario 2251
parent: Story 2225 [User Can] Split Single Transactions across multiple categories
prd: PRD-TRANSACTION-SPLITS.md
---

# Design: Edit an individual transaction split

## Scope

This design covers editing the amount, category, or memo of one split on an
existing multi-split transaction. Every successful edit rebalances the
transaction without changing its authoritative transaction amount.

Split editing is initiated from the splits list on the transaction detail page.
That list and its Edit actions are available only when the transaction has
multiple splits. A transaction with one split continues to use the existing
transaction-edit workflow; it does not expose an Edit Split action.

## Acceptance criteria

- [ ] An Editor or Owner can edit the amount, category, and memo of any split
      on a transaction with multiple splits.
- [ ] The split list has an Edit action for each split; selecting it opens an
      independent split-edit dialog populated with that split's current values.
- [ ] Saving updates the selected split and automatically balances the
      transaction by adjusting a different split.
- [ ] If the edited split is not first by `Order`, the first split is adjusted
      to balance. If the edited split is first, the next split by `Order` is
      adjusted instead. The entered amount is always honored.
- [ ] The transaction amount and all non-edited split fields remain unchanged.
      Only the edited split's requested fields and the balancing split's amount
      may change.
- [ ] The edit is atomic. On success, the dialog closes and the updated ordered
      split list is displayed and the page recomputes balance status. On
      failure, the dialog stays open with entered values and shows the API
      error.
- [ ] Cancel closes the dialog without changing data.
- [ ] Categories are sanitized using the rules in
      [`PRD-TRANSACTION-SPLITS.md`](PRD-TRANSACTION-SPLITS.md); blank categories
      remain valid. Memo remains optional.

## User experience

1. On the transaction detail page, each row in the multi-split list has an
   **Edit** action, available to Editors and Owners.
2. Selecting **Edit** opens a standalone **Edit Split** dialog with Amount,
   Category, and Memo fields prefilled from that split.
3. Amount accepts positive, negative, and zero currency values with cents.
   Category may be blank, and Memo is optional.
4. Selecting **Save** submits one update. The page replaces its transaction
   detail with the returned result. The dialog remains open with its values if
   the request fails; Cancel never submits changes.

The dialog is an independent Vue component under
`app/components/transaction/` (for example, `EditSplitDialog.vue`), not an
inline form or dialog-specific state embedded in the transaction detail page.
It owns the form state, validation/error display, loading state, and update
request, and emits the updated `TransactionDetailDto` to its parent on success.
The transaction detail page owns only which split is selected and whether the
dialog is visible, then updates its displayed transaction from that event.
Keep this dialog separate from the add-split form so that each operation has
its own fields, lifecycle, and error state.

## Allocation rule

Let `T` be the authoritative transaction amount, `E` the edited split, and `B`
the balancing split. Select `B` as the first split by `Order` other than `E`.
The resulting amount for `B` is:

```text
B.amount = T - sum(amount of every split except E and B) - E.requestedAmount
```

Set the edited split amount to the requested amount, and leave all other split
amounts unchanged. If a category or memo is also edited, apply those values to
the edited split. Sanitize its category before persistence. Do not change the
balancing split's category, memo, key, or order, or any other split's fields.

For example, for a `-$100.00` transaction with ordered splits of `-$60.00`
Groceries and `-$40.00` Household, editing Groceries to `-$70.00` makes
Household `-$30.00`. Editing Household to `-$50.00` instead makes Groceries
`-$50.00`. Both results remain balanced at `-$100.00`.

The detail page only offers split editing when at least two splits exist, so
there is always a distinct balancing split. Rebalancing also resolves an
existing imbalance as part of the edit. The transaction amount is never
modified to match the splits.

## API and application operation

Add a split-update operation alongside the existing transaction split
operations:

```http
PUT /api/tenant/{tenantKey}/transactions/{transactionKey}/splits/{splitKey}
```

Use the existing `SplitEditDto` request shape:

```csharp
public record SplitEditDto(decimal Amount, string? Category, string? Memo);
```

Return `200 OK` with the updated `TransactionDetailDto`, including the full
ordered split list. The page derives balance status from this result and the
authoritative transaction amount. Require the Editor tenant role, use the route
tenant scope, and load the transaction through the current-tenant query in the
application feature.

Implement an application operation such as
`TransactionsFeature.UpdateSplitAsync(transactionKey, splitKey, split)` that:

1. Loads the tenant-scoped transaction and its splits, ordered by `Order`.
2. Finds the requested split and a distinct balancing split. Return the
   repository-standard not-found response when the transaction or split does
   not exist; reject an invalid request rather than returning a success-shaped
   response.
3. Sets the requested split fields and computes the balancing amount using
   the allocation rule above.
4. Persists both changes in one `SaveChangesAsync` call, using the existing
   concurrency approach so a competing edit cannot cause a stale balance
   calculation to be committed.
5. Returns the updated transaction detail.

The controller delegates to this feature operation; allocation logic does not
belong in the controller or browser. Reuse the split request validator and
standard category sanitization. Validation failures return `400 Bad Request`;
missing tenant-scoped transactions or splits return `404 Not Found`.

### Backend implementation

The backend operation is implemented by `TransactionsController.UpdateSplit`
and `TransactionsFeature.UpdateSplitAsync`. A direct request to edit the sole
remaining split returns `400 Bad Request`, since no distinct balancing split
exists.

The shared `SplitEditDto` validator accepts the existing signed amount range,
category length of 200, and memo length of 500. Amounts must be representable
in cents (trailing decimal zeros are allowed). This applies to both adding and
editing an individual split and prevents database rounding from changing an
allocation or introducing an imbalance.

The feature reuses `ITenantRepository.ExecuteTenantTransactionAsync`, with an
optional isolation-level argument set to `Serializable` for split edits.
Existing callers that omit this argument retain the provider's default
isolation. This transaction helper remains on the tenant repository pending
its intended move to `IDataProvider`; no parallel transaction abstraction is
introduced.

The transaction includes the database read, calculation, single save, and
commit. PostgreSQL serialization failures and deadlocks are translated to
`DataConflictException`, which the API maps to `409 Conflict` with
ProblemDetails asking the caller to reload and retry. The operation does not
silently retry a stale calculation.

The existing single-split addition uses the same serializable boundary without
changing its allocation rule. Both read-modify-write workflows must participate
so that a concurrent addition cannot commit an old balance calculation after
an edit succeeds.

Full transaction updates use the same serializable boundary. They balance the
first split by `Order` against the requested transaction amount and the sum of
all other splits. For multi-split transactions, every split's category and memo
and all other split amounts remain unchanged; transaction-level category
updates are rejected. This also repairs an existing imbalance rather than
preserving it.

Backend coverage is in
[`TransactionSplitUpdateTests.cs`](../../../tests/Integration.Application/TransactionSplitUpdateTests.cs)
and
[`TransactionSplitUpdateControllerTests.cs`](../../../tests/Integration.Controller/TransactionSplitUpdateControllerTests.cs),
including persisted allocations, rollback on save failure, competing edits,
authorization, tenant/transaction scope, and request validation.

## Frontend integration

- Add a separately rendered split-edit dialog component in
  `src/FrontEnd.Nuxt/app/components/transaction/`.
- The detail page passes the selected split and visibility state to the
  component, and handles its updated-transaction event.
- Add row-level Edit controls only when the transaction has multiple splits
  and the current user can edit transactions.
- Regenerate the API client from the backend API definition; the dialog uses
  the generated split-update operation rather than duplicating endpoint
  details.
- Preserve the existing Add Split workflow and its allocation behavior.

### Frontend implementation

[`EditSplitDialog.vue`](../../../src/FrontEnd.Nuxt/app/components/transaction/EditSplitDialog.vue)
accepts `show`, `tenantKey`, `transactionKey`, and `split` props. It emits
`update:show` for visibility changes and `updated` with the server-returned
`TransactionDetailDto` after a successful save. The component owns a local
draft and calls the generated `TransactionsClient.updateSplit` operation;
it does not depend on the transaction page or workspace store.

The dialog validates signed currency amounts with cents, category length,
and memo length before submitting. API errors, including conflicts, keep
the draft open for correction or retry. Enter submits through the form,
memo supports newlines, and Cancel or Escape dismisses without saving.
Dismissal and duplicate submissions are blocked while a save is pending.

The detail page renders row-level Edit actions only for Editors and Owners
on multi-split transactions. It replaces its transaction detail with the
successful response and computes the displayed split total and balance in
integer cents. A danger alert above the splits list appears only when the
splits are unbalanced; balanced transactions show no balance message.
Changing the workspace or transaction clears the selected
split and closes the dialog. The existing Add Split form remains separate.

Component contract coverage is in
[`transaction-edit-split.test.ts`](../../../src/FrontEnd.Nuxt/tests/transaction-edit-split.test.ts),
including request and event payloads, validation boundaries, error/retry
behavior, draft resets, keyboard handling, and pending-save guards.

## Functional test cases

```gherkin
Background:
    Given the application is running
    And I am logged in as a user with "Editor" role

Scenario: Edit a non-first split amount
    Given I have a workspace with a transaction for payee "Maverick Markets" having these splits:
        | Amount  | Category  | Memo |
        | -60.00  | Groceries |      |
        | -40.00  | Household |      |
    And I am on the transactions detail page for that transaction
    When I edit the "Household" split amount to -50.00
    Then the following splits are displayed:
        | Amount  | Category  | Memo |
        | -50.00  | Groceries |      |
        | -50.00  | Household |      |
    And the transaction amount remains -100.00
    And the transaction is balanced

Scenario: Edit the first split amount
    Given I have a workspace with a transaction for payee "Maverick Markets" having these splits:
        | Amount  | Category  | Memo |
        | -60.00  | Groceries |      |
        | -40.00  | Household |      |
    And I am on the transactions detail page for that transaction
    When I edit the "Groceries" split amount to -70.00
    Then the following splits are displayed:
        | Amount  | Category  | Memo |
        | -70.00  | Groceries |      |
        | -30.00  | Household |      |
    And the transaction amount remains -100.00
    And the transaction is balanced

Scenario: Edit split category and memo without changing other splits
    Given I have a workspace with a transaction having multiple splits
    And I am on the transactions detail page for that transaction
    When I edit one split's category and memo
    Then that split's category and memo are changed
    And only the balancing split's amount may also change
    And the transaction is balanced
```
