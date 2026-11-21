---
status: Approved
target_release: Beta 3
prd_document: PRD-PAYEE-RULES.md
parent: https://dev.azure.com/jcoliz/YoFiV3/_workitems/edit/2205
ado: https://dev.azure.com/jcoliz/YoFiV3/_workitems/edit/2266
related_docs:
  - DESIGN-PAYEE-RULES-STORIES-1-2.md
---

# Technical Design: Manually Trigger Payee Rule Matching

## Scope

This design covers **Story 2266: User - Manually triggers rule matching (manual-rules)** from the [Payee Matching Rules PRD](PRD-PAYEE-RULES.md#story-2266-user---manually-triggers-rule-matching-manual-rules).

In scope:

- Apply the current tenant's complete payee-rule set to one persisted transaction from the Transactions page.
- Replace the transaction's single-split category with the winning rule's category when a rule matches.
- Show the updated category immediately and report the outcome in a toast.
- Hide the action for transactions already known to have multiple splits.
- Return an expected stale-row outcome when the transaction became split before the backend operation ran.
- Preserve the existing category when no rule matches.
- Update the winning rule's usage statistics for a successful match.

Excluded:

- Story 2211 and Scenario 2281, which manage rules and optionally apply one newly created rule to eligible uncategorized transactions.
- Story 2212, which applies rules during bank import.
- General-purpose bulk re-matching, rule matching for multiple transactions, or matching pending import-review transactions.
- Rules that modify source, amount, memo, date, or splits. Future rule dimensions remain represented by the existing matching service boundary and require their own designs.

All Story 2266 acceptance criteria are covered. The implementation must not add bulk semantics or an option to apply only one named rule.

## Architectural Considerations

This follows the existing Clean Architecture and BFF patterns described in [Architecture Overview](../../ARCHITECTURE.md): the Vue page delegates to a thin controller, which delegates to `PayeeMatchingRuleFeature`. The feature coordinates tenant-scoped transaction and rule data through `IDataProvider`; matching precedence remains in the existing Application matching helper and service.

No new cross-cutting component, third-party dependency, cache, authorization policy, background operation, or ADR is required. The operation uses the existing in-memory per-tenant rule cache and a serializable database transaction so the split-count check and category update are validated atomically. One transaction and one user workspace are affected, so load is bounded by the existing cached rule set.

## Design Decisions

| Decision | Alternatives considered | Rationale |
| --- | --- | --- |
| A no-match result leaves every existing category unchanged and returns 200 with `Outcome = NoMatch`. | Clear the category; return an error. | A manual match is best-effort categorization. Clearing a user-entered category when no rule applies would be destructive, while an error would misrepresent a valid no-match outcome. |
| A successful manual match increments the winning rule's `MatchCount` and updates `LastUsedAt`. | Count only import-time matches. | Rule usage statistics should describe actual applications of the rule, regardless of entry point. This also preserves the existing behavior of `ApplyMatchingRulesAsync`. |
| Use `POST /transactions/{key}/apply-payee-rules`, limited to the Editor tenant role. | PUT/PATCH of the whole transaction; endpoint under payee-rules. | This is an idempotent-looking but server-selected command: the chosen category depends on current rules. A POST command route avoids pretending the client supplies a transaction representation and keeps transaction actions together. |
| Return a small BFF result containing the transaction key, typed outcome, resulting category, and previous category. | Return the full transaction; reload the list. | The operation only changes one list field, while the typed outcome also represents a stale multi-split row without misusing HTTP error semantics. The result supports precise toast wording and an immediate local update without a second API request or navigation-context loss. |
| Update the visible row in place and do not reload the whole query after a successful match. | Refresh pagination after every match. | The PRD requires the new category to be shown immediately. This also preserves the current page and any active filter. A later normal refresh reconciles the row with active filters, including `uncategorizedOnly`. |
| A transaction that became multi-split is an expected 200 outcome, not an HTTP error. | Return 409 Conflict; silently do nothing. | This race is an expected stale-UI state, not a server failure. A typed outcome lets the UI show a fixed friendly toast without rendering server-provided ProblemDetails, then refresh the row. Other failures use the normal page ErrorDisplay. |
| Encapsulate the menu action in a focused Vue component. | Keep all request/toast state in the page. | It isolates the API contract and toast branches, makes pending/error behavior testable without mounting the Nuxt page, and keeps the Transactions page responsible only for row visibility and updating its local collection. |

## Entities

No entity changes are required. The operation reads existing `Transaction`, `Split`, and `PayeeMatchingRule` entities and updates only `Split.Category` on a single-split transaction.

## Data

No `DbContext`, schema, migration, index, foreign key, value conversion, or `IDataProvider` capability changes are required.

The Application feature uses existing `IDataProvider` methods to load the tenant-scoped transaction with its splits, execute matching and category updates inside one serializable transaction, and save existing entities.

## Data Integration Tests

No Data integration tests are required because this story does not change the schema or EF Core configuration.

## Application Features

### DTO

Add `src/Application/Dto/PayeeRuleApplicationResultDto.cs` with an outcome enum and result record:

```csharp
public enum PayeeRuleApplicationOutcome
{
    Applied,
    NoMatch,
    TransactionHasMultipleSplits
}

public record PayeeRuleApplicationResultDto(
    Guid TransactionKey,
    PayeeRuleApplicationOutcome Outcome,
    string? Category,
    string? PreviousCategory);
```

Summary: Represents the expected outcome of manually applying all current payee rules to one transaction.

- `TransactionKey`: tenant-scoped transaction key for updating or refreshing the visible row.
- `Outcome`: `Applied` when a rule changed or explicitly confirmed the category, `NoMatch` when no rule applied, or `TransactionHasMultipleSplits` when the persisted state no longer permits categorization by a single rule result.
- `Category`: resulting persisted category for `Applied` and `NoMatch`; null for `TransactionHasMultipleSplits`. For `NoMatch`, it equals `PreviousCategory`.
- `PreviousCategory`: category before the operation for `Applied` and `NoMatch`, including an empty string for an uncategorized transaction; null for `TransactionHasMultipleSplits`.

### IPayee matching behavior

No public change is required to [`IPayeeMatchingService`](../../../src/Application/Services/IPayeeMatchingService.cs). The new feature method invokes the existing `ApplyMatchingRulesAsync` operation with a single internal `IMatchableTransaction` adapter containing the persisted transaction payee. This preserves import matching, precedence, cache, and usage-statistic behavior. A list reload is not used to determine the payee or existing category.

### Feature method

Add to [`PayeeMatchingRuleFeature`](../../../src/Application/Features/PayeeMatchingRuleFeature.cs):

```csharp
public Task<PayeeRuleApplicationResultDto> ApplyRulesToTransactionAsync(
    Guid transactionKey,
    CancellationToken cancellationToken = default);
```

Summary: Applies the current tenant's complete payee rule set to one persisted transaction and updates its category when a rule matches.

Parameters:

- `transactionKey`: key of the transaction in the current tenant.
- `cancellationToken`: token that cancels the operation.

Returns `PayeeRuleApplicationResultDto` for `Applied`, `NoMatch`, and `TransactionHasMultipleSplits` outcomes.

Exceptions:

- `TransactionNotFoundException`: no transaction with the key exists in the current tenant. The controller maps this to 404.
- `ValidationException`: the transaction has no category split or otherwise cannot be represented by the required single-split invariant. The controller maps this to 400.
- `OperationCanceledException`: cancellation was requested.
- `DataConflictException`: a concurrent serializable transaction conflict occurred. The existing middleware maps this to 409 with the standard reload-and-retry message.

Operation order:

1. Begin a serializable `IDataProvider.ExecuteInTransactionAsync` transaction and honor the cancellation token.
2. Load the transaction, including splits, with `TenantId == current tenant Id` and `Key == transactionKey`.
3. Throw `TransactionNotFoundException` when absent.
4. When more than one split is persisted, return `TransactionHasMultipleSplits` before evaluating any rule or changing usage statistics. `Category` and `PreviousCategory` are null. Commit no changes and return HTTP 200.
5. When exactly zero splits are persisted, throw `ValidationException` with message `The transaction does not have a category allocation.`
6. Capture the category from the sole split as `PreviousCategory`.
7. Apply the complete current rule set to the transaction's persisted payee using `ApplyMatchingRulesAsync`.
8. If the result is null, return `NoMatch` with `Category = PreviousCategory`; do not update a split or save transaction state.
9. If the result is non-null, sanitize it with `CategoryHelper.SanitizeCategory`, assign it to the sole split, save the transaction, and return `Applied`.
10. Commit the category update and any `MatchCount`/`LastUsedAt` update together. A failure rolls back both.

The operation reuses the existing conflict precedence implemented by [`PayeeMatchingHelper`](../../../src/Application/Helpers/PayeeMatchingHelper.cs): matching regex over substring, longer substring over shorter, and newer modification time for equal precedence. It must not duplicate precedence logic in `PayeeMatchingRuleFeature` or the controller.

No new Application exceptions are required. Becoming multi-split is represented by `PayeeRuleApplicationOutcome.TransactionHasMultipleSplits` rather than by an exception or ProblemDetails response.

## Application Integration Tests

Add fixture `tests/Integration.Application/PayeeMatchingRuleFeature/PayeeMatchingRuleFeatureManualMatchingTests.cs`, deriving from the existing `PayeeMatchingRuleFeatureTestBase`.

| Test | Summary XMLdoc |
| --- | --- |
| `ApplyRulesToTransactionAsync_MatchPersistsCategoryAndUsageStatistics` | Loads a real tenant rule and transaction, persists the new single-split category, increments the winning rule's statistics, and returns the previous/current categories. |
| `ApplyRulesToTransactionAsync_MultipleSplitsReturnsExpectedOutcomeWithoutWriting` | Loads a persisted multi-split transaction, returns the typed stale-row outcome before rule evaluation, and verifies transaction and rule statistics remain unchanged. |
| `ApplyRulesToTransactionAsync_OtherTenantTransactionThrowsNotFound` | Verifies the tenant predicate is translated by the real provider and does not load or modify another tenant's transaction. |

These are the only new Application integration tests because each depends on real persistence or translated tenant queries. Use two `TestCase` values on the successful-match test for an empty and an existing previous category; the output distinction does not warrant a duplicate fixture method. The test still verifies one persisted split update and the coordinated usage-statistics update for each case.

Add or extend unit tests for the pure orchestration and mapping branches:

- A no-match result maps to `Outcome = NoMatch`, preserves `PreviousCategory`, and performs no entity update or save.
- A matched result with an empty previous category maps to `Outcome = Applied` and sanitizes the assigned category.
- A matched result with an existing category maps to `Outcome = Applied` with the original category retained in `PreviousCategory`.
- The existing [`PayeeMatchingHelperTests`](../../../tests/Unit/Application/PayeeRules/PayeeMatchingHelperTests.cs) continue to cover substring/regex matching and precedence; do not duplicate those cases in the data integration fixture.

## Controllers

Update [`TransactionsController`](../../../src/Controllers/TransactionsController.cs), preserving its existing primary constructor pattern while injecting `PayeeMatchingRuleFeature`.

| Method | Route | Tenant role | Request body | Success response | Error responses |
| --- | --- | --- | --- | --- | --- |
| POST | `/api/tenant/{tenantKey:guid}/transactions/{key:guid}/apply-payee-rules` | Editor | None | 200 `PayeeRuleApplicationResultDto`, including stale multi-split outcome | 400 transaction has no category split; 404 transaction absent from tenant; 409 serializable conflict; 401 unauthenticated; 403 Viewer/outsider; 500 unexpected failure |

Action summary: **Applies the current tenant's payee matching rules to one transaction.**

Use the action name `ApplyPayeeRules`. Reuse the controller's existing `LogStartingKey` and `LogOkKey` `[LoggerMessage]` partial methods, so no new event IDs are added. Do not log payee, category, or other transaction contents.

No exception-handler change is required. A stale multi-split state is serialized as the 200 result outcome above; it is not a ProblemDetails failure. Serializable update conflicts retain the existing generic 409 handling.

Controller integration tests are not enumerated here. The existing [`TransactionsControllerTests`](../../../tests/Integration.Controller/TransactionsControllerTests.cs) fixture covers applied/no-match/multi-split result serialization, 404 tenant isolation, Editor/Viewer/unauthenticated authorization, 400 no-split behavior, and the existing 409 serializable-conflict handling for this route.

## Front End

### API Client

The TypeScript client will be regenerated by [`Generate-ApiClient.ps1`](../../../scripts/Generate-ApiClient.ps1). The generated `TransactionsClient.applyPayeeRules(transactionKey, tenantKey)` method will be used by the frontend. No existing route or response shape changes, so the preceding frontend remains compatible with the new backend. There are no breaking changes.

### `TransactionPayeeRulesAction.vue`

Add `src/FrontEnd.Nuxt/app/components/transactions/TransactionPayeeRulesAction.vue`.

JSDoc summary: `Applies the current workspace's payee rules to one single-split transaction and publishes the resulting notification.`

Props:

| Prop | Type | Required | Purpose |
| --- | --- | --- | --- |
| `transaction` | `TransactionResultDto` | Yes | Supplies the transaction key, payee, and current category used in request/result behavior. |
| `tenantKey` | `string` | Yes | Identifies the current workspace for the generated client call. |
| `disabled` | `boolean` | No, default `false` | Allows the containing page to disable the action while its list is loading. |

Emits:

| Event | Payload | Purpose |
| --- | --- | --- |
| `completed` | `(tenantKey: string, result: PayeeRuleApplicationResultDto)` | Lets the page update a successfully matched/no-match row, or refresh after the persisted transaction became multi-split. |
| `failed` | `(tenantKey: string, problem: IProblemDetails)` | Lets the page display unexpected failures through its existing `ErrorDisplay`. |

Behavior:

- Render a Bootstrap dropdown item with the Feather `zap` icon and text `Apply Matching Rules`.
- Disable while this component's request is in flight or `disabled` is true; guard duplicate clicks in code as well as markup.
- Call `TransactionsClient.applyPayeeRules(transaction.key, tenantKey)` from a client-side click handler.
- Capture the tenant key before starting and ignore the response if the caller has moved to another workspace.
- On `Outcome = NoMatch`, emit `completed` and publish an info toast with title `No matching rule` and fixed message `No payee rule matched this transaction. Its category was not changed.`
- On `Outcome = Applied` with an empty `PreviousCategory`, publish a success toast: `Category set to “{Category}”.`
- On `Outcome = Applied` with a non-empty `PreviousCategory`, publish a success toast: `Replaced category “{PreviousCategory}” with “{Category}”.` This notification is shown even when the two values are equal; the user is informed that the rule was applied.
- On `Outcome = TransactionHasMultipleSplits`, emit `completed` and publish a warning toast with title `Transaction changed` and fixed message `This transaction now has multiple splits, so payee rules were not applied.` Do not display any server-provided detail for this expected outcome.
- On an HTTP/API failure, call `handleApiError(err, 'Apply Rules Failed', 'Payee rules could not be applied. Reload the page and try again.')` and emit `failed` with that ProblemDetails value. Do not publish a toast for unexpected failures; the parent page displays the problem through its standard `ErrorDisplay`.
- Do not navigate or close other modals.

### Transactions page

Update [`src/FrontEnd.Nuxt/app/pages/transactions/index.vue`](../../../src/FrontEnd.Nuxt/app/pages/transactions/index.vue):

- Keep the existing Editor/Owner-only actions menu. Viewer users continue to have neither the actions column nor this item.
- Within the row action menu, render `TransactionPayeeRulesAction` only when `transaction.hasMultipleSplits === false`.
- Pass the row transaction, current tenant key, and `loading` state as props.
- Handle `completed` by confirming the tenant key still equals the current workspace. For `Applied` or `NoMatch`, replace that row's `category` with the non-null `result.category` while retaining all other row values and list position; do not reload. For `TransactionHasMultipleSplits`, reload the current page so the row displays the split badge and loses the action.
- Handle `failed` by ignoring stale-workspace responses. Assign the emitted ProblemDetails to the page's existing `error`, set `showError = true`, and do not publish a toast or reload the page.
- The `open/close` state of the portable dropdown continues to be owned by the existing dropdown component.

No new store or `localStorage` key is introduced.

### Test IDs

| data-test-id | Element | Purpose |
| --- | --- | --- |
| `row-actions-button` | Existing row actions trigger | Opens the transaction action menu; existing ID retained. |
| `row-actions-menu` | Existing action menu | Scopes all transaction actions; existing ID retained. |
| `apply-payee-rules-button` | Apply Matching Rules dropdown item | Locates and activates manual matching. |
| `manual-rules-applied` | Toast body after a previously uncategorized match or equal-category application | Confirms a match was applied. |
| `manual-rules-overwritten` | Toast body after replacing a non-empty category | Confirms the previous category was overwritten. |
| `manual-rules-no-match` | Toast body after no rule matched | Confirms the category was preserved. |
| `manual-rules-outdated` | Toast body when the persisted transaction became multi-split | Confirms the fixed friendly message for the expected stale-row outcome. |

A `transaction-split-badge` remains the existing hook for the multi-split state. No manual-rules button is rendered in a row containing that badge.

## Component Tests

Add `src/FrontEnd.Nuxt/tests/transaction-payee-rules-action.test.ts` using Vitest and Vue Test Utils.

| Test description | Behavior contract |
| --- | --- |
| `it('calls applyPayeeRules once and emits the result')` | Guard duplicate pending requests, call the generated client with the transaction and tenant keys, and emit the exact result. |
| `it('publishes a success toast for an uncategorized match')` | Publish the applied toast ID after `Outcome = Applied` and empty `PreviousCategory`. |
| `it('publishes an overwrite toast when a category existed')` | Publish the overwritten toast ID and include both category values in the message. |
| `it('publishes an info toast and preserves data when no rule matched')` | Publish the no-match toast ID and emit the unchanged result. |
| `it('publishes a fixed stale-row toast for a multi-split outcome')` | Publish the outdated test ID with the fixed client message and emit the typed result, without inspecting or displaying server details. |
| `it('emits converted ProblemDetails without publishing a toast on API failure')` | Convert the API error through `handleApiError`, emit it, and verify the toast store is untouched. |

Page-level visibility, row replacement, unexpected-error `ErrorDisplay`, and stale-row refresh behavior are covered in functional tests rather than by mounting the Nuxt Transactions page.

## Functional Tests

Add the scenarios to [`tests/Functional/Features/PayeeRules.feature`](../../../tests/Functional/Features/PayeeRules.feature). Reuse the existing workspace, rule, transaction, Transactions-page, role, and category assertion steps. Mark implementation-specific missing steps with `# NEW` so the step definitions and [`TransactionsPage`](../../../tests/Functional/Pages/TransactionsPage.cs) additions are explicit.

```gherkin
@category:story-2266
Rule: Manual payee rule matching

  @pri:1
  Scenario: User replaces an existing category with a matching rule
    Given I have a payee rule:
      | Payee  | Category  |
      | Coffee | Cafes     |
    And I have a workspace with a transaction:
      | Payee       | Amount | Category |
      | Coffee Shop | -6.50  | Dining   |
    And I am on the transactions page
    When I apply matching rules to the transaction for "Coffee Shop" # NEW
    Then I should see category "Cafes" for "Coffee Shop" in the transaction list
    And I should see the "manual-rules-overwritten" notification # NEW

  @pri:1
  Scenario: User categorizes a single-split uncategorized transaction
    Given I have a payee rule:
      | Payee    | Category  |
      | Groceries | Food      |
    And I have a workspace with a transaction:
      | Payee             | Amount | Category |
      | Groceries Market  | -42.00 |          |
    And I am on the transactions page
    When I apply matching rules to the transaction for "Groceries Market" # NEW
    Then I should see category "Food" for "Groceries Market" in the transaction list
    And I should see the "manual-rules-applied" notification # NEW

  @pri:1
  Scenario: No matching rule preserves an existing category
    Given I have a workspace with a transaction:
      | Payee          | Amount | Category     |
      | Unknown Vendor | -10.00 | Miscellaneous |
    And I am on the transactions page
    When I apply matching rules to the transaction for "Unknown Vendor" # NEW
    Then I should see category "Miscellaneous" for "Unknown Vendor" in the transaction list
    And I should see the "manual-rules-no-match" notification # NEW

  @pri:2
  Scenario: User cannot apply matching rules to a split transaction
    Given I have a workspace with a transaction for payee "Weekly Shop" having these splits:
      | Category  | Amount |
      | Groceries | -30.00 |
      | Household | -20.00 |
    And I am on the transactions page
    When I open the transaction actions for "Weekly Shop" # NEW
    Then the "Apply Matching Rules" action is not shown # NEW

  @pri:1
  Scenario: User receives a friendly error when a transaction becomes split after page load
    Given I have a payee rule:
      | Payee   | Category |
      | Transit | Travel   |
    And I have a workspace with a transaction:
      | Payee       | Amount | Category |
      | Transit Hub | -8.00  | Commute  |
    And I am on the transactions page
    And another user split the transaction for "Transit Hub" # NEW
    When I apply matching rules to the stale transaction row for "Transit Hub" # NEW
    Then I should see the "manual-rules-outdated" notification # NEW
    And the transaction row for "Transit Hub" shows its split indicator
```

The last scenario uses Test Control to add a split outside the currently loaded browser state; it does not alter the frontend source or disable the normal list action artificially. The new page-object action locators remain scoped to `row-actions-menu` and `apply-payee-rules-button`.

## Open Questions

- No open questions. The no-match and rule-usage-statistics behaviors were confirmed during design review.

## Completion Checklist

- [ ] Every PRD acceptance criterion in scope is traceable to this design.
- [ ] Every section is present; untouched layers say so explicitly.
- [ ] Every endpoint lists its tenant role, request/response types, and error statuses.
- [ ] Every new exception has a status-code mapping.
- [ ] Every index, constraint, conversion, and new data-provider capability in Data has a Data Integration Test; none are introduced here.
- [ ] Backward compatibility is stated and no breaking changes are introduced.
- [ ] Every `data-test-id` used in Gherkin appears in the Test IDs table.
- [ ] Gherkin scenarios have all `When` steps before `Then` steps and mark new steps.
- [ ] No "TBD", "to be finalized", or "work remaining" text appears.
- [ ] Open Questions contains no unresolved entries.
- [ ] All relative links resolve to existing files.
