---
status: Approved
ado: Scenario 2252
parent: Story 2225 [User Can] Split Single Transactions across multiple categories
prd: PRD-TRANSACTION-SPLITS.md
---

# Design: Add a single split to an existing transaction

## Scope clarification

Scenario 2252 covers adding one split to an **existing transaction**, whether
it currently has one split or several. The new amount is allocated from the
first split, preserving the existing split order and all other allocations.
Creating a transaction with multiple splits is a different workflow.

Note that the larger feature allows creating unbalanced splits. However, in the
case of adding a single split through this workflow, we always rebalance the
transaction. Later split editing and split uploads may still create unbalanced
transactions.

## Acceptance criteria

- [ ] An Editor or Owner can add one split to an existing transaction with any
      number of splits.
- [ ] The user supplies the new split's category, amount, and optionally a memo.
- [ ] The first existing split retains its key, category, memo, and order; its
      amount is adjusted to balance the transaction after the new split is added.
- [ ] All other existing splits retain their keys, amounts, categories, memos,
      and order.
- [ ] The transaction amount, memo, and other transaction-level fields do not change.
- [ ] The operation is saved atomically, and the updated splits are displayed
      after success.
- [ ] Split categories are cleaned up using the category sanitization rules in
      the PRD; a blank category remains valid as uncategorized.

## User experience

1. On the transaction detail page, the user selects **Add split** in the Splits
   section. The action is available to Editors and Owners, consistent with
   transaction editing.
2. A small form or dialog asks for the new split's Category, Amount, and Memo. Category
   is optional because uncategorized splits are valid. Amount accepts positive
   or negative currency values, including cents. Memo is optional.
3. On save, replace the displayed split list with the server's updated result.
   On failure, keep the form open with its entered values and show the API error.
   Cancel makes no changes.

The first existing split is the balancing allocation. For a -\$100.00
transaction with a -\$100.00 Fuel split, entering a -\$10.00 Snacks split adjusts
Fuel to -\$90.00. For a transaction with several splits, only its first split
is adjusted; all other existing splits remain unchanged. The new split appears
last.

## Allocation rule

The transaction amount remains authoritative. Given transaction amount `T`,
new split amount `N`, and all existing splits except the first, set the first
split amount to:

```text
first split amount = T - sum(other existing split amounts) - N
```

This works for both expenses and income. For example, for a -\$250.00
transaction with ordered existing splits of -\$125.00, -\$50.00, and -\$75.00,
adding a -\$25.00 split adjusts only the first split to -\$100.00. The other
existing splits remain -\$50.00 and -\$75.00, so the four splits sum to -\$250.00.
The formula also corrects any prior imbalance by assigning the balancing
difference to the first split. Do not change the transaction amount to fit the
splits.

The new split receives the entered category, amount, and memo, and is appended
after the existing splits with the next display order. The first existing split
keeps its key, category, and memo and remains first. All other existing splits
are unchanged. A zero amount for the new split is allowed if valid under split
validation; the final allocations must still sum to the transaction amount.

## Endpoint and DTO

Use the existing split endpoint for a single split:

```http
POST /api/tenant/{tenantKey}/transactions/{transactionKey}/splits
```

Require the Editor tenant role. The endpoint is scoped to the tenant in the
route, and the application feature must also load the transaction through its
current-tenant query.

The single-split request uses the existing `SplitEditDto`:

```csharp
public record SplitEditDto(decimal Amount, string? Category, string? Memo);
```

`Amount` accepts positive, negative, or zero values within the split amount
range already used by `SplitEditDto`. `Category` may be blank for uncategorized
and has the existing 200-character limit. `Memo` is optional and has the split
memo's existing 500-character limit. Use the existing DTO validator. Sanitize
the category in the application feature before persisting it.

The single-item POST action in `TransactionsController` requires the Editor
role and delegates to `TransactionsFeature.AddSplitAsync`:

```csharp
[HttpPost("{key:guid}/splits")]
[RequireTenantRole(TenantRole.Editor)]
[ProducesResponseType(typeof(TransactionDetailDto), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
public async Task<IActionResult> AddSplit(
    Guid key,
    [FromBody] SplitEditDto split)
{
    var updated = await transactionsFeature.AddSplitAsync(key, split);
    return Ok(updated);
}
```

The response is `200 OK` with `TransactionDetailDto`, including all splits
ordered by `Order`. Reuse this existing result DTO rather than defining a
scenario-specific response type. Bulk additions remain a distinct operation
and may create an imbalance.

**Errors**:

- `400 Bad Request` for invalid request data.
- `400 Bad Request` when the transaction has no existing split to rebalance.
- `404 Not Found` when the transaction is not found in the tenant.

## Application feature operation

Implement this as one tenant-authorized application operation that loads the
transaction and its splits, adjusts the first split to the remainder, and
creates the new split. Persist both changes together. Do not calculate the
remainder only in the browser or issue separate update and create requests:
either approach can leave partial changes or use stale transaction data.

Add `TransactionsFeature.AddSplitAsync(Guid transactionKey, SplitEditDto split)`
to perform the single-split operation:

1. Load the transaction and splits using the existing tenant-scoped transaction
   query. Throw the standard transaction-not-found exception if no matching
   transaction exists. The transaction must have at least one split, as required
   by the feature's invariant.
2. Select the first split by `Order` (the import/category allocation). Sanitize
   the requested category, then set the first split amount to
   `transaction.Amount - sum(other existing split amounts) - split.Amount`.
   Leave its key, category, memo, and order unchanged.
3. Create the new split with the requested amount, sanitized category, optional
   memo, and the next order after the current last split.
4. Persist the first-split update and new split together in a single
   `SaveChangesAsync` call. Use the repository's concurrency approach to detect
   competing split changes; do not commit a calculation based on stale splits.
5. Return `ToDetailDto(transaction)` after saving. Its ordered `Splits`
   collection supplies the committed state to the page.

The controller action should delegate to this feature method and return
`Ok(result)`. It should not implement the allocation rule itself.

The bulk `AddSplitsAsync` operation described in
[`TRANSACTION-SPLIT-DESIGN.md`](TRANSACTION-SPLIT-DESIGN.md) remains distinct:
it appends splits without reallocating the first split and may leave a
transaction unbalanced. `AddSplitAsync` always adds exactly one split and
rebalances by adjusting the first existing split. Editing individual splits or
uploading a set of splits may still result in an imbalance, handled by the
parent feature's balance-warning workflow.

## Functional tests

```gherkin
Background:
    Given the application is running
    And I am logged in as a user with "Editor" role

Scenario: Add a single split to an existing transaction with one split
    Given I have a workspace with a transaction:
        | Payee             | Amount  | Category |
        | Maverick Markets  | -100.00 | Fuel     |
    And I am on the transactions detail page for that transaction
    When I add a new split with the following details:
        | Category | Amount | Memo |
        | Snacks   | -10.00 | I was hungry |
    Then the following splits are displayed:
        | Category | Amount  | Memo         |
        | Fuel     | -90.00  |              |
        | Snacks   | -10.00  | I was hungry |
    And the transaction amount remains -100.00
    And the transaction is balanced

Scenario: Add a single split to an existing transaction with multiple splits
    Given I have a workspace with a transaction for payee "Super Duper Mart" having these splits:
        | Amount  | Category | Memo |
        | -125.00 | Groceries     | |
        | -50.00 | Supplies     | |
        | -75.00 | Healthcare:Prescriptions | Can't forget my meds |
    And I am on the transactions detail page for that transaction
    When I add a new split with the following details:
        | Category | Amount | Memo |
        | Dining Out   | -25.00 | Also had dinner there |
    Then the following splits are displayed:
        | Amount  | Category | Memo |
        | -100.00 | Groceries     | |
        | -50.00 | Supplies     | |
        | -75.00 | Healthcare:Prescriptions | Can't forget my meds |
        | -25.00 | Dining Out | Also had dinner there |
    And the transaction amount remains -250.00
    And the transaction is balanced
```

## Related cases

- Editing or deleting splits, adding several splits at once, reordering, and
  uploading split data are covered by other scenarios in Story 2225.
