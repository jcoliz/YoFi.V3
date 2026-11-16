---
status: Draft
ado: Scenario 2289
parent: Story 2225 [User Can] Split Single Transactions across multiple categories
prd: PRD-TRANSACTION-SPLITS.md
references:
  - DESIGN-EDIT-SPLIT.md
---

# Design: Delete an individual transaction split

## Scope

Allow an Editor or Owner to delete an allocation from a multi-split
transaction. The backend balances the result without changing the
authoritative transaction amount. A transaction must retain at least one
split.

Replace the split list's edit icon with the same borderless grey `...`
action menu used on the Payee Rules page. Extract that page's Edit/Delete
menu into a shared `EditDeleteDropdown.vue` component and use it in both
places. Keep the existing independent
[`EditSplitDialog.vue`](../../../src/FrontEnd.Nuxt/app/components/transaction/EditSplitDialog.vue).

This design does not add bulk deletion, undo, split reordering controls, or
a generic configurable action-menu framework. The transaction list's
Quick Edit/Create Rule menu is different and is outside this extraction.

## Acceptance criteria

- [ ] Editors and Owners see a borderless grey `...` action trigger on every
      multi-split row. Viewers see no action column.
- [ ] The menu contains **Edit** with an edit icon and **Delete** with a trash
      icon and danger styling, matching the Payee Rules page.
- [ ] Edit opens the existing independent split-edit dialog.
- [ ] Delete opens a confirmation dialog identifying the selected split.
      Cancel performs no request and changes no data.
- [ ] Confirming deletes only the selected split and balances the first
      surviving split by `Order`.
- [ ] The transaction amount and other transaction fields do not change.
      All surviving split categories, memos, and keys remain unchanged.
      Only the balancing amount and normalized order values may change.
- [ ] The operation is atomic. Failures leave persisted allocations unchanged
      and display an error without closing the confirmation dialog.
- [ ] The backend rejects deleting the last split, even through a direct API
      call. Missing or unrelated splits produce not-found responses.
- [ ] Deleting from two splits to one restores the normal single-category
      transaction display and editing behavior, including after reload.
- [ ] Payee Rules uses the extracted component without changing its
      authorization, edit/delete handlers, confirmation, or error behavior.

## User experience

### Split list actions

Use the existing
[`Payee Rules action menu`](../../../src/FrontEnd.Nuxt/app/pages/payee-rules/index.vue)
as the visual reference:

- A right-aligned, compact action column.
- A borderless secondary trigger with the `more-horizontal` Feather icon
  at size 18 and an Actions tooltip.
- A right-aligned dropdown with Edit and Delete items, using size 14
  `edit` and `trash-2` icons.
- Danger styling on Delete, not on the trigger.
- Fixed dropdown positioning through `DropDownPortable`, so the last
  row's menu is not clipped by the responsive table container.

The trigger's accessible label identifies its row, for example
“Actions for split Groceries, -$60.00”. An uncategorized split uses
“Uncategorized”. Tests identify rows by split key, not category text,
since categories and amounts need not be unique.

Opening either action closes the dropdown. Preserve Bootstrap keyboard
navigation, Escape dismissal, and focus behavior through the existing
dropdown components. Menu actions must not trigger a containing row's
navigation handler.

### Delete confirmation

Use a separate transaction-specific `DeleteSplitDialog.vue` component,
following the ownership boundary of the existing split editor. It owns
the delete request, loading state, and error display, not the shared menu.

The dialog:

- Is titled **Delete Split**.
- Shows the selected category (or “Uncategorized”), formatted amount,
  and memo when present.
- Explains: “Delete this split? The first remaining split will be adjusted
  to keep the transaction balanced. The transaction amount will not change.”
- Warns that deletion cannot be undone.
- Has Cancel and a danger-styled Delete button; Delete becomes
  “Deleting...” while pending.
- Uses the shared `ModalDialog` and `ErrorDisplay` components.
- Blocks duplicate submissions and all dismissal routes while pending,
  including Escape, close button, and backdrop. Guard dismissal in the
  wrapper because the shared modal does not enforce this itself.
- Preserves the selected split and displays standard API errors on failure.
  Conflict errors ask the user to reload before retrying; do not silently
  retry a stale operation.

The page passes `show`, `tenantKey`, `transactionKey`, and the selected
`split`. The component emits `update:show` and `updated`, with the complete
server-returned `TransactionDetailDto` on success. It does not depend on
the workspace store or page. Document the component, props, and emits.

The page replaces its transaction detail from the successful response;
it must not calculate balances or optimistically remove a row. Changing
workspace or transaction clears the selection and dialog. An outstanding
response from an old context must not replace the new page's transaction.
Mutually exclude add, edit, and delete split operations while one is saving.

### Returning to a single split

Deleting one of two splits leaves a single allocation whose amount equals
the transaction amount. The API returns that allocation's category as the
transaction category and an empty `Splits` collection, following the
existing detail DTO contract.

The page consequently hides the split list and its action menus, restores
the transaction-level category display and edit field, and preserves Add
Split. The remaining allocation's memo stays persisted even though the
single-category view does not display split-level memos.

Balanced transactions show no balance message. Any existing unbalanced
state is repaired by deletion; the danger `BaseAlert` remains available
above the split list for other unbalanced data.

## Shared EditDeleteDropdown component

Add `app/components/EditDeleteDropdown.vue`, extracting the existing
Payee Rules markup rather than adding a second implementation.

| Input/output | Contract |
|---|---|
| `ariaLabel: string` | Required accessible label identifying the row. |
| `disabled?: boolean` | Defaults to false; disables the trigger and both actions and guards event emission. |
| `edit` event | No payload; the parent closes over its row and opens the editor. |
| `delete` event | No payload; the parent closes over its row and opens confirmation. |

The component always has exactly those two actions. It owns the trigger,
menu markup, icons, and compact 28px trigger sizing. It wraps
[`DropDownPortable`](../../../src/FrontEnd.Nuxt/app/components/DropDownPortable.vue)
with fixed positioning; it does not implement its own Bootstrap lifecycle.
Do not add API clients, selected-row state, role checks, confirmation dialogs,
or domain DTO props.

Preserve the Payee Rules selectors `row-actions-button`, `row-actions-menu`,
`edit-button`, and `delete-button` inside the component. Repeated selectors
are scoped to their containing row. Split tests locate
`transaction-split-{splitKey}` first, then its action. Update existing
edit-split selectors/callers to this row-scoped menu contract.

Keep the narrow action-column styling at each table; move the duplicated
trigger styling into the component. Payee Rules still decides whether the
column is visible and routes events to its existing handlers. Component
JSDoc documents props, events, and the absence of confirmation/API behavior.

## Allocation and ordering

Load splits ordered by `Order`, then locate the target. Let `R` be the
ordered surviving splits and `T` the authoritative transaction amount.
Reject the operation if deleting the target would leave `R` empty.

```text
R[0].amount = T - sum(amount of R[1..])
```

Recompute from the authoritative total, not by adding the deleted amount:
the original splits may already be unbalanced. Signed and zero values
remain valid. If the first split is deleted, the next surviving split
becomes the balancing split.

Normalize surviving `Order` values to `0..count-1` in the same relative
order. Keys, categories, and memos are preserved. This explicitly keeps
the last surviving split at `Order = 0`: current single-category
projections and Quick Edit locate that allocation by order zero. Without
normalization, deleting the original first split could hide the remaining
category or prevent editing it.

Examples for a `-$100.00` transaction:

| Before, in order | Delete | After, in order |
|---|---|---|
| Groceries -60, Household -30, Supplies -10 | Household | Groceries -90, Supplies -10 |
| Groceries -60, Household -30, Supplies -10 | Groceries | Household -90, Supplies -10 |
| Groceries -60, Household -40 | Groceries | Household -100 |
| Groceries -50, Household -30, Supplies -10 (unbalanced) | Household | Groceries -90, Supplies -10 |

## Backend contract

```http
DELETE /api/tenant/{tenantKey}/transactions/{key}/splits/{splitKey}
```

No request body. Return **200 OK** with the updated `TransactionDetailDto`,
not 204, so the page receives the committed balancing result and the
single-split transition without another fetch.

| Response | Meaning |
|---|---|
| 200 | Split removed; remaining allocations balanced and ordered. |
| 400 | Attempt to delete the last split; standard validation ProblemDetails. |
| 401 / 403 | Existing authentication and Editor/Owner tenant-role rules. |
| 404 | Tenant-scoped transaction or its requested split not found. |
| 409 | Competing change; standard conflict ProblemDetails asks for reload. |

Add `TransactionsFeature.DeleteSplitAsync(transactionKey, splitKey,
cancellationToken)`. Keep the controller thin and use the existing
two-key `LogStartingKeys`/`LogOkKeys` logging pair. Do not log allocation
values, categories, or memos.

The feature:

1. Uses `ITenantRepository.ExecuteTenantTransactionAsync` at serializable
   isolation, as add-split, edit-split, and full transaction updates do.
   Keep this existing boundary pending its intended move to `IDataProvider`.
2. Loads the transaction and splits through the current-tenant query.
3. Checks membership before the last-split guard. Use
   `TransactionNotFoundException`, `SplitNotFoundException`, and the
   repository-standard `ValidationException` as appropriate.
4. Removes the selected entity through the data-provider abstraction and
   from the transaction's in-memory split collection. Both are needed for
   correct persistence and response mapping.
5. Balances the first survivor and normalizes ordering.
6. Saves once, maps the updated detail, and returns it only after commit.

Removal, balancing, and ordering are one atomic operation. Reuse existing
serialization/deadlock translation to `DataConflictException`. Competing
deletions must never commit a zero-split transaction, and stale add/edit/
amount-update calculations must not overwrite the deletion result.

No schema change or new transaction abstraction is needed. Application and
Data remain logging-free. Add OpenAPI response metadata, regenerate the
TypeScript API client, and update API reference and HTTP request examples.

## Verification plan

### Backend implementation

The backend is implemented by `TransactionsController.DeleteSplit` and
`TransactionsFeature.DeleteSplitAsync`. It uses the existing serializable
tenant transaction helper, removes the entity and navigation entry, balances
the first survivor, normalizes orders, and saves once. The endpoint returns
the committed detail with the existing validation, not-found, and conflict
responses. No schema changes are required.

Coverage is in
[`TransactionSplitDeleteTests.cs`](../../../tests/Integration.Application/TransactionSplitDeleteTests.cs)
and
[`TransactionSplitDeleteControllerTests.cs`](../../../tests/Integration.Controller/TransactionSplitDeleteControllerTests.cs).
The generated TypeScript client exposes `deleteSplit`; frontend menu and
confirmation-dialog implementation remain separate work.

### Application integration

- Delete first, middle, and last allocations; verify exact persisted
  amounts, categories, memos, keys, relative order, and normalized orders.
- Cover two-to-one transitions, including deleting the order-zero split,
  then read list/detail, Quick Edit category, and update transaction amount.
- Cover positive, negative, zero, mixed-sign, and cents allocations.
- Repair a pre-existing imbalance using the authoritative amount.
- Reject last-split deletion and missing/cross-transaction/cross-tenant keys
  without modifying persisted data.
- Force save failure; verify deletion, balancing, and ordering roll back.
- Test competing deletes of a two-split transaction, plus delete versus
  add, edit, and transaction amount update; verify atomic success or a
  conflict and at least one balanced survivor.
- Verify cancellation does not commit changes.

### Controller integration

- Editor and Owner success, Viewer rejection, unauthenticated requests,
  and tenant isolation.
- Correct 200 detail payloads, 400 last-split rejection, 404 membership
  checks, and 409 ProblemDetails/OpenAPI metadata.
- GET after success verifies persisted allocations and category projection.

### Frontend component and browser coverage

- Shared dropdown emits the correct action, guards disabled actions, and
  preserves row-specific accessible labels.
- Delete dialog calls the generated client with the correct three keys,
  emits the returned detail, cancels without requests, displays errors,
  and blocks duplicate requests and dismissal while pending.
- Payee Rules edit/delete flows continue working after extraction.
- Browser verification covers first/last-row menu positioning, keyboard
  access, focus, split-edit regression, confirmation cancel/error/success,
  and the two-to-one transition after reload.
- Verify workspace/transaction navigation cannot apply a stale response.
- Do not unit-test Nuxt pages or assert exact CSS/HTML structure; use
  component contracts and containerized browser/functional coverage.

Suggested functional scenarios:

```gherkin
Scenario: Delete the first allocation
    Given a transaction amount of -100.00 with splits of -60.00 Groceries and -40.00 Household
    When I open the Groceries split actions menu
    And I choose Delete
    And I confirm deleting the split
    Then the transaction amount remains -100.00
    And the transaction category is Household
    And the split list is hidden

Scenario: Cancel deleting an allocation
    Given a transaction with multiple splits
    When I choose Delete from a split actions menu
    And I cancel the confirmation
    Then all transaction splits remain unchanged

Scenario: Delete a non-first allocation
    Given a transaction amount of -100.00 with splits of -60.00 Groceries, -30.00 Household, and -10.00 Supplies
    When I delete and confirm the Household split
    Then the splits are -90.00 Groceries and -10.00 Supplies
    And no unbalanced alert is displayed
```
