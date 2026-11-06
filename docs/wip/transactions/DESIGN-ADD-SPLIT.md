---
status: Draft
ado: Scenario 2252
parent: Story 2225 [User Can] Split Single Transactions across multiple categories
prd: PRD-TRANSACTION-SPLITS.md
---

# Design: Add a single split to an existing transaction

## Scope clarification

Scenario 2252 covers adding one split to an **existing single-category
transaction**. The transaction already has one split, and the user divides that
allocation between its existing category and one new category. Creating a
transaction with multiple splits is a different workflow.

Note that the larger feature allows creating unbalanced splits. However, in the
case of an existing transaction with only one category, it's obvious we can
balance it, so we should do so.

## Acceptance criteria

- [ ] An Editor or Owner can add one split to an existing transaction that has
      one split.
- [ ] The user supplies the new split's category, amount, and optionally a memo.
- [ ] The existing split retains its category and memo and its amount becomes
      the remainder needed to balance the transaction.
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

For example, for a $-100.00 transaction with a $-100.00 Fuel split, entering a
$-10.00 Snacks split adjusts Fuel to $-90.00. The adjusted Fuel split appears
first, followed by the newly entered split, matching the functional test.

## Allocation rule

The transaction amount remains authoritative. Given transaction amount `T` and
the new split amount `N`, set the existing split amount to:

```text
existing split amount = T - N
```

This works for both expenses and income: for a $100.00 transaction, entering
$10.00 leaves $90.00 in the existing split. The formula also restores balance
if the original single split was already out of balance. Do not change the
transaction amount to fit the splits.

The new split receives the entered category and amount and memo, and the last
display order. The existing split keeps its key, category, and memo and is
displayed first. A zero remainder is allowed; the transaction still has two
splits and remains balanced.

## Application/API behavior

Implement this as one tenant-authorized application operation that loads the
transaction and its splits, verifies the transaction has exactly one split,
updates that split to the remainder, and creates the new split. Persist both
changes together. Do not calculate the remainder only in the browser or issue
separate update and create requests: either approach can leave partial changes
or use stale transaction data.

Return the updated transaction detail (including its ordered splits), or an
equivalent operation result with the updated split rows and balance state, so
the page can render the committed result. Use existing category sanitization
and split validation. Reject a missing transaction, a transaction with anything
other than one current split, and malformed amount input using the application's
standard API error responses. Do not silently fall back to appending a split.

The existing general-purpose add-splits operation described in
[`TRANSACTION-SPLIT-DESIGN.md`](TRANSACTION-SPLIT-DESIGN.md) remains distinct:
it appends splits and may leave a multi-split transaction unbalanced. This
scenario's balancing behavior is specifically the one-to-two transition from a
single existing split. General rebalancing of transactions that already have
multiple splits belongs to the balance-warning and balancing-split workflows
in the parent feature.

## Functional test

```gherkin
Background:
    Given the application is running
    And I am logged in as a user with "Editor" role

Scenario: Add a single split to an existing transaction
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
```

## Related cases

- Adding a split to a transaction that already has multiple splits is not
  covered here; use the general split-management and balance-warning behavior.
- Editing or deleting splits, adding several splits at once, reordering, and
  uploading split data are covered by other scenarios in Story 2225.
