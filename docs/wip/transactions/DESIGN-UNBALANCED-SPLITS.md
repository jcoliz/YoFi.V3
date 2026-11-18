---
status: Approved
target_release: Beta 2
prd_document: PRD-TRANSACTION-SPLITS.md
parent: "Feature 1982: Transaction Splits"
ado: "Story 2308: User - Detect Unbalanced Transactions"
---

# Technical Design Document: Detect Unbalanced Transactions

## Scope

This design covers Story 2308, **User - Detect Unbalanced Transactions** in
[`PRD-TRANSACTION-SPLITS.md`](PRD-TRANSACTION-SPLITS.md). It covers all six
acceptance criteria for `unbalanced-splits`:

- a visual indicator in the transactions list;
- a prominent warning in transaction details;
- saving remains allowed while the allocations are unbalanced;
- balance status refreshes after a split amount change;
- balance status refreshes after a transaction amount change; and
- adding a split to an unbalanced transaction follows the existing add-split
  allocation rule and makes the initial allocations balanced.

This design does not change the split add, edit, delete, or bulk-upload rules.
In particular, add and edit operations continue to rebalance atomically as
specified in [`DESIGN-ADD-SPLIT.md`](DESIGN-ADD-SPLIT.md) and
[`DESIGN-EDIT-SPLIT.md`](DESIGN-EDIT-SPLIT.md). Bulk split import, covered by
Story 6, can produce an unbalanced state; this design leaves room for that
workflow by making balance status part of the common transaction response DTOs.

## Architectural Considerations

This follows the existing BFF pattern described in
[`docs/ARCHITECTURE.md`](../../ARCHITECTURE.md): the Application layer computes
balance status from tenant-scoped transactions, and the Vue frontend renders
that status. No balance calculation, schema constraint, new dependency,
background work, cache, or ADR change is required.

The split sum must be computed by the server rather than in the browser. The
paginated list projects only the resulting `IsBalanced` status, while the detail
response also projects `SplitsTotal` for display. This keeps the business
definition in the Application layer and gives both views enough information
without loading the complete split collection or making additional requests.

## Design Decisions

| Decision | Alternatives considered | Rationale |
| --- | --- | --- |
| Define balance as exact equality between the transaction amount and the sum of all split amounts. | Apply a currency tolerance. | All stored allocation amounts are currency amounts representable to the schema's two decimal places. Exact decimal equality avoids hiding a genuine cent difference. |
| Return `IsBalanced` in both result DTOs, and `SplitsTotal` in `TransactionDetailDto` only. | Return the split total in list rows; fetch splits separately for each list row; compute in Vue; add a dedicated balance endpoint. | The list needs only the server-derived status to render its indicator. The detail response also needs the authoritative total to display the amounts and difference, including when its split display list is empty. |
| Treat imbalance as a warning, not a validation error or save blocker. | Reject split or transaction changes that do not balance. | Story 2308 explicitly permits the user to save and correct the allocation later. Existing operations that can calculate a balancing allocation still do so automatically. |
| Use separate `TransactionUnbalancedBadge` and `TransactionSplitBalanceAlert` components for list and detail views. | One component with compact/detail modes; duplicate page-local markup. | The views have materially different presentation and data contracts: the list is a small self-contained badge, while details show a prominent alert with amounts and a difference. |
| Keep the detail warning visible for any unbalanced transaction, including a single-split transaction. | Show it only when `Splits.Count > 1`. | Balance is a property of the transaction, not the number of rows currently included in the detail split collection. Bulk-import and repair scenarios must not hide a single-split mismatch. |

## Entities

No entity changes are required. `Transaction.Amount` and the existing `Split.Amount`
values already contain all data needed to determine balance. The entity model
continues to allow a mismatch; the rule remains advisory.

## Data

No database or `IDataProvider` changes are required. No migration, index,
constraint, conversion, or delete-behavior change is introduced.

The existing split relation uses decimal currency values. The aggregate is
implemented as an EF Core-translatable projection so PostgreSQL calculates the
split sum as part of the existing transaction query. A database constraint is
not added because it would convert the accepted warning state into a hard
validation failure.

## Data Integration Tests

No Data integration tests are required because this design does not change the
schema, EF model, data provider, or persistence behavior.

## Application Features

### Updated result DTOs

Update [`TransactionResultDto`](../../../src/Application/Dto/TransactionResultDto.cs):

```csharp
public record TransactionResultDto(
    Guid Key,
    DateOnly Date,
    decimal Amount,
    string Payee,
    string? Memo,
    string Category,
    bool HasMultipleSplits,
    bool IsBalanced);
```

The summary remains **Transaction data returned from queries (output-only)**.
`IsBalanced` is true when the signed sum of all split amounts equals `Amount`.
`HasMultipleSplits` remains the list indicator for the existing split badge and
no longer serves as a proxy for balance. The list DTO does not include the
split total.

Update [`TransactionDetailDto`](../../../src/Application/Dto/TransactionDetailDto.cs):

```csharp
public record TransactionDetailDto(
    Guid Key,
    DateOnly Date,
    decimal Amount,
    string Payee,
    string? Memo,
    string? Source,
    string? ExternalId,
    string Category,
    decimal SplitsTotal,
    bool IsBalanced)
{
    public IReadOnlyList<TransactionSplitDto> Splits { get; init; } = [];
}
```

The `TransactionSplitDto` shape does not change. In the detail DTO,
`SplitsTotal` is the signed sum of all split amounts, and `IsBalanced` is true
when that sum equals `Amount`. `SplitsTotal` must include the first split that is
omitted from the detail `Splits` collection for a single-split transaction.

### Transaction projections and responses

Update `TransactionsFeature.ToResultDto` and `TransactionsFeature.ToDetailDto`
in [`TransactionsFeature.cs`](../../../src/Application/Features/TransactionsFeature.cs)
to populate the fields during their existing projections. `ToResultDto` projects
`IsBalanced` directly by comparing `Amount` with the sum of all split amounts;
it does not project the total. `ToDetailDto` calculates:

- `SplitsTotal = transaction.Splits.Sum(split => split.Amount)`;
- `IsBalanced = transaction.Amount == SplitsTotal`.

For empty split collections, the sum is zero. That can only represent an
inconsistent legacy record because the existing Application invariant requires
at least one split; a nonzero transaction amount therefore displays as
unbalanced rather than being silently hidden.

The list query must remain a single SQL query. The expression must translate
through PostgreSQL and must not switch to client evaluation. All queries remain
restricted by `_currentTenant.Id`; no balance-only query crosses tenant scope.

No public Feature method, validator, exception, or write rule changes. Successful
add, edit, delete, full transaction update, and quick-edit responses use the
updated projection, so a saved result immediately contains the committed balance
state. A failed save returns the existing error response and leaves the
previously displayed committed state unchanged.

## Application Integration Tests

Extend
[`TransactionsFeatureTests`](../../../tests/Integration.Application/TransactionsFeatureTests.cs)
with these behavior tests. Each runs against the real data provider and verifies
that the aggregate projects and translates correctly.

- **`GetTransactionsAsync_WithUnbalancedSplits_ProjectsBalanceStatus`** — summary:
  `Verifies that the paginated transaction projection returns an unbalanced status.`
  Seed a transaction whose split total differs by one cent and another whose
  total matches. Assert the two `IsBalanced` values in the same page result.
- **`GetTransactionByKeyAsync_WithUnbalancedSingleSplit_ProjectsBalanceStatus`** —
  summary: `Verifies that detail results include all split amounts even when the split display collection is empty.`
  Assert `Splits.Count` is empty, while `SplitsTotal` and `IsBalanced` still
  describe the persisted first split.
- **`AddSplitAsync_WithPreviouslyUnbalancedTransaction_ReturnsBalancedStatus`** —
  summary: `Verifies that the committed add-split response replaces an unbalanced state with the server-derived balanced status.`
  Extend the existing imbalance test to assert `SplitsTotal` and `IsBalanced`.
- **`UpdateTransactionAsync_WithSplitChange_ReturnsUpdatedBalanceStatus`** —
  summary: `Verifies that the full transaction update response contains balance status from the committed split allocations.`
  Assert the returned status after the existing first-split reallocation logic.

## Controllers

No routes, methods, tenant roles, authorization, logging events, or error mappings
change. The existing GET and mutation endpoints simply return the expanded output
DTOs:

| Method | Route | Tenant role | Request body | Success response | Error responses |
| --- | --- | --- | --- | --- | --- |
| GET | `/api/tenant/{tenantKey:guid}/transactions` | Viewer | None | `200 PaginatedResultDto<TransactionResultDto>` | `403` when tenant access is denied |
| GET | `/api/tenant/{tenantKey:guid}/transactions/{key:guid}` | Viewer | None | `200 TransactionDetailDto` | `403` access denied; `404` transaction not found in tenant |
| POST | `/api/tenant/{tenantKey:guid}/transactions/{transactionKey:guid}/splits` | Editor | `SplitEditDto` | `200 TransactionDetailDto` | Existing `400`, `404`, and `409` split-operation responses |
| PUT | `/api/tenant/{tenantKey:guid}/transactions/{transactionKey:guid}/splits/{splitKey:guid}` | Editor | `SplitEditDto` | `200 TransactionDetailDto` | Existing `400`, `404`, and `409` split-operation responses |
| PUT | `/api/tenant/{tenantKey:guid}/transactions/{key:guid}` | Editor | `TransactionEditDto` | `200 TransactionDetailDto` | Existing `400`, `404`, and `409` transaction responses |

[`TransactionsController`](../../../src/Controllers/TransactionsController.cs)
retains its thin delegation model and existing method XMLdoc summaries. No new
`[LoggerMessage]` event is needed because no controller decision or boundary
error is introduced.

The backend change is backward compatible: JSON adds `IsBalanced` to both DTOs
and `SplitsTotal` to the detail DTO, without removing, renaming, or changing
existing fields. A previously deployed frontend ignores the additions.
Regenerate the TypeScript client with
[`Generate-ApiClient.ps1`](../../../scripts/Generate-ApiClient.ps1) after the
OpenAPI shape changes. The frontend continues to call `getTransactions`,
`getTransactionById`, `addSplit`, `updateSplit`, and `updateTransaction`; it
reads the new generated `TransactionResultDto` and `TransactionDetailDto`
properties.

## Front End

### `app/components/transaction/TransactionUnbalancedBadge.vue`

Add a component summarized as **Renders a compact unbalanced status badge for
transaction list rows.** The component requires no props and emits no events. It
renders a small danger badge with an alert icon, the text `Unbalanced`, and a
static tooltip explaining that the split amounts do not match the transaction
amount. It does not receive, calculate, or display amounts. The list page renders
it only when `transaction.isBalanced` is false.

### `app/components/transaction/TransactionSplitBalanceAlert.vue`

Add a component summarized as **Renders the prominent split-balance alert in
transaction details.**

```ts
type Props = {
  transactionAmount: number
  splitsTotal: number
}
```

The component emits no events. It renders `BaseAlert` with `variant="danger"`,
the message `Splits do not match the transaction amount.`, and detail text for
the transaction amount, split total, and signed difference
(`transactionAmount - splitsTotal`). The detail page renders it only when
`transaction.isBalanced` is false; the component does not infer balance status.
It is non-blocking and never disables an edit or save control.

Neither component calculates `IsBalanced` or `SplitsTotal`. The pages supply
authoritative status and values from the relevant API DTO. Use the existing
currency formatter rather than duplicating locale configuration if it is
extracted by the implementation.

### `app/pages/transactions/index.vue`

Update the existing Transactions page summary to **display server-derived
unbalanced status beside the existing split indicator for list rows.**

For every transaction row, render `TransactionUnbalancedBadge` in the Category
cell when `transaction.isBalanced` is false. The existing `Split` badge
remains present only when `hasMultipleSplits` is true; an unbalanced
single-split transaction therefore still gets a warning even without a `Split`
badge. Loading, empty, no-results, request error, and pagination behavior remain
unchanged. No additional API call, store, or localStorage key is introduced.

### `app/pages/transactions/[key].vue`

Update the existing Transaction Details page summary to **display authoritative
split totals and warning state returned with transaction details.**

Remove the local `splitsTotal` and `isBalanced` client computations and replace
the existing conditional `BaseAlert` in the Splits card with
`TransactionSplitBalanceAlert`. Render the alert after the split controls and
before the split table when `transaction.isBalanced` is false, using the updated
generated DTO fields. The alert shows for an unbalanced transaction whether or
not the detail DTO's optional split display list contains multiple rows.

After every successful `addSplit`, split edit, split delete, or full transaction
update, continue replacing the entire `transaction` ref with the response DTO.
That atomic replacement updates the warning and totals immediately without a
second GET. Add-split, edit, and validation failures leave the warning at the
last committed state while the existing form or `ErrorDisplay` remains open.
Saving is never disabled by `isBalanced`.

When the user selects **Add split** for an already-unbalanced transaction, the
page initializes the new split's Amount field to the amount needed to balance
the transaction: `transaction.amount - transaction.splitsTotal`. The user may
change that amount before saving. If they submit the suggested amount unchanged,
the new allocation completes the existing split total without requiring an
adjustment to the first split. If they enter another amount, the existing
server-side add-split rule still atomically adjusts the first split so the
committed allocations sum to the transaction amount.

The page uses the two authoritative values returned in `TransactionDetailDto`
to calculate only this suggested input value; it does not recalculate the split
total from the displayed split rows. Replacing `transaction` with the successful
response removes the alert and refreshes the split rows in the same render.

### Test IDs

| data-test-id | Element | Purpose |
| --- | --- | --- |
| `transaction-unbalanced-badge` | Compact list indicator inside a transaction row | Verifies the list exposes an unbalanced state without blocking row navigation. |
| `transaction-split-balance-alert` | `TransactionSplitBalanceAlert` in transaction details | Verifies the danger alert is visible and retains the existing detail hook. |
| `transaction-split-total` | Split total text inside the expanded warning | Verifies the server-projected aggregate is displayed. |
| `transaction-split-difference` | Difference text inside the expanded warning | Verifies the amount needed to balance is displayed with its sign. |

The pages continue to rely on the existing `row-{key}`, `transaction-split-badge`,
`transaction-amount`, split form, and edit/delete controls.

## Component Tests

Add focused component tests under
[`src/FrontEnd.Nuxt/tests`](../../../src/FrontEnd.Nuxt/tests):

- `TransactionUnbalancedBadge` renders the compact danger badge, alert icon,
  label, and tooltip without requiring amount props.
- `TransactionSplitBalanceAlert` renders the danger alert with transaction
  amount, split total, and signed difference.
- Neither component emits an event or disables surrounding controls.

Conditional rendering based on `IsBalanced` is a page responsibility and is
covered at the page or functional-test level. Tests assert component contracts
and rendered values, not Bootstrap classes or page-level layout.

## Functional Tests

Add the following scenarios to
[`TransactionSplits.feature`](../../../tests/Functional/Features/TransactionSplits.feature),
in priority order. The first scenario uses a new seed arrangement that accepts a
transaction amount independent of its split amounts; it deliberately creates an
unbalanced persisted state through the existing Test Control API rather than a
user mutation.

```gherkin
Scenario: List shows an indicator for an unbalanced transaction
    Given the application is running
    And I am logged in as a user with "Editor" role
    And I have a transaction for payee "Out of balance" with amount -100.00 and these splits: # NEW
        | Amount | Category  |
        | -90.00 | Groceries |
        | -5.00  | Household |
    When I view my transactions
    Then the transaction row for "Out of balance" shows an unbalanced indicator # NEW

Scenario: Details show transaction and split totals for an unbalanced transaction
    Given the application is running
    And I am logged in as a user with "Editor" role
    And I have a transaction for payee "Out of balance" with amount -100.00 and these splits: # NEW
        | Amount | Category  |
        | -90.00 | Groceries |
        | -5.00  | Household |
    And I am on the transactions detail page for that transaction
    Then the split balance warning is visible # NEW
    And the split balance warning shows transaction amount -100.00 and split total -95.00 # NEW

Scenario: Split edit immediately clears the warning
    Given the application is running
    And I am logged in as a user with "Editor" role
    And I have a transaction for payee "Repairable balance" with amount -100.00 and these splits: # NEW
        | Amount | Category  |
        | -90.00 | Groceries |
        | -5.00  | Household |
    And I am on the transactions detail page for that transaction
    And the split balance warning is visible # NEW
    When I edit the "Household" split amount to -10.00
    Then the transaction is balanced
    And the split balance warning is not visible # NEW

Scenario: Transaction amount edit immediately refreshes balance status
    Given the application is running
    And I am logged in as a user with "Editor" role
    And I have an unbalanced transaction for payee "Amount repair" # NEW
    And I am on the transactions detail page for that transaction
    When I click the "Edit" button
    And I change Amount to -95.00 # NEW
    And I click "Update"
    Then the transaction amount remains -95.00
    And the split balance warning is not visible # NEW

Scenario: Adding a split repairs an existing imbalance through the usual allocation rule
    Given the application is running
    And I am logged in as a user with "Editor" role
    And I have a transaction for payee "Add to balance" with amount -100.00 and these splits: # NEW
        | Amount | Category  |
        | -80.00 | Groceries |
        | -10.00 | Household |
    And I am on the transactions detail page for that transaction
    And the split balance warning is visible # NEW
    When I add a new split for category "Parking" without changing its initial amount # NEW
    Then the transaction is balanced
    And the split balance warning is not visible # NEW
```

The existing `I view my transactions`, `I am on the transactions detail page for
that transaction`, `I edit...`, `I click...`, and `the transaction is balanced`
phrases are reused. The new add-split step verifies that the Amount input was
initialized to the balancing difference before submitting it. New warning,
independent-amount seeding, initial-amount, and amount-entry steps are marked
`# NEW`; implement them with data-test IDs from the table above and no
visual-class selectors.

## Open Questions

No open questions remain. The PRD's requirement that imbalance not block saving
and the existing automatic balancing rules are compatible: operations that can
correct the balance continue to do so, while imported or legacy inconsistent
allocations are persisted and warned about rather than rejected.

## Completion Checklist

- [x] Every PRD acceptance criterion in scope is traceable to a section of this design.
- [x] Every section is present; untouched layers say so explicitly.
- [x] Every endpoint lists its tenant role, request/response types, and error statuses.
- [x] Every new exception has a status-code mapping; no new exception is introduced.
- [x] Every index, constraint, conversion, and new data-provider capability in Data has a Data Integration Test; no such change is introduced.
- [x] Backward compatibility with the previous frontend is stated; no breaking response change is introduced.
- [x] Every `data-test-id` used in Gherkin has an entry in the Test IDs table or is an existing test hook identified below the table.
- [x] Gherkin scenarios have all `When` steps before `Then` steps and reuse existing catalog phrases where possible.
- [x] No unresolved implementation placeholders appear anywhere.
- [x] Open Questions is empty.
- [x] All relative links target existing files.
- [x] The author instruction block is not present.
