---
status: In Review
target_release: Beta 1
ado:
  - "[Story 2216](https://dev.azure.com/jcoliz/YoFiV3/_workitems/edit/2216): User - Advanced Filtering"
  - "[Story 2214](https://dev.azure.com/jcoliz/YoFiV3/_workitems/edit/2214): User - Find Uncategorized Transactions"
prd: PRD-TRANSACTION-FILTERING.md
---

# Transaction Advanced Filtering

## Purpose and Scope

Design one filtering experience for Stories 2216 and 2214 from the
[transaction filtering PRD](PRD-TRANSACTION-FILTERING.md). Finding uncategorized
transactions is one option in the advanced panel, not a separate workflow or
implementation phase. This is a design-only deliverable.

**Included**:

- Extract and expand the existing collapsible date-filter panel.
- Field-specific payee, split category, and one combined Memo filter covering
  transaction and split memos.
- Exact transaction amount filtering with the same sign rules as quick search.
- Uncategorized-only filtering across every split.
- Existing custom date bounds, five date presets, and Clear All.
- Composition with quick search, correct pagination, and retained navigation state.
- Retain every applied filter between visits to the transactions page within the
  same app session; no cross-session persistence is required.

**Excluded**:

- Story 2215: default last-12-month filtering and localStorage preferences.
- Story 2217: balanced/unbalanced filtering and indicators.
- Story 2218: report navigation, URL synchronization, and category wildcards.
- Debouncing quick search, named saved presets, query syntax, and filter chips.
- Changes to transaction creation, split editing, or the list result DTO.

The PRD is the requirements source. The broader
[UI recommendations](TRANSACTION-FILTERING-UI-RECOMMENDATIONS.md) are historical
context; this focused design supersedes their proposals for these two stories.

## Current Implementation

- [`transactions/index.vue`](../../../src/FrontEnd.Nuxt/app/pages/transactions/index.vue)
  already renders `SearchFilterBar` with a filter toggle and an animated,
  initially collapsed card identified by `data-test-id="date-range-filters"`.
  This is the panel to expand; do not introduce a second filter panel.
- The page owns expansion state, list loading, pagination, workspace selection,
  errors, and transaction mutation dialogs.
- [`SearchFilterBar.vue`](../../../src/FrontEnd.Nuxt/app/components/SearchFilterBar.vue)
  is shared and submits on Enter or its search button. Quick search is not
  currently debounced; retain that behavior.
- [`transactionSearch.ts`](../../../src/FrontEnd.Nuxt/app/stores/transactionSearch.ts)
  retains search, dates, and page number in memory across navigation.
  Its current `clearFilters()` preserves search.
- [`TransactionsController`](../../../src/Controllers/TransactionsController.cs)
  accepts page number, date bounds, and quick search.
  [`TransactionsFeature`](../../../src/Application/Features/TransactionsFeature.cs)
  applies tenant isolation and filters before counting and paging.
- [`TransactionSearchHelper`](../../../src/Application/Helpers/TransactionSearchHelper.cs)
  already implements literal case-insensitive search and a reusable amount
  parser. Search examines all splits, although the list displays only the first
  split's category.

## User Experience

Keep the existing search bar and filter toggle in place. Replace the date-only
card's contents with the following panel:

```text
Search
[ Search payee, memo, category, or amount... ] [Search] [x] [Filters]

Expanded filter panel
  Date range
  [Last 30d] [Last 3mo] [Last 12mo] [This year] [All time]
  From Date [             ]       To Date [             ]

  Payee [                 ]       Category (any split) [           ]
  Memo [                  ]       Amount [                        ]
  Memo searches the transaction memo and all split memos.
  Unsigned amounts match either sign; use + or - to restrict the sign.
  [ ] Uncategorized only
      Includes transactions with any blank split category.

  [Clear All]
```

- Start collapsed when the filter store is first initialized. Retain the panel's
  expanded/collapsed state in that store between page visits within the app
  session. Expansion alone never changes criteria or sends a request; collapsing
  commits any changed valid drafts as specified below. Retained filters remain
  active while collapsed.
- Preserve the toggle's active styling whenever any panel criterion is applied.
  Quick search alone does not activate that styling, matching the current store.
  Give the icon-only toggle the accessible name **Filters**.
- Use existing Bootstrap controls and the existing expand transition, including
  reduced-motion behavior. Two columns on medium/wide screens; one on mobile.
  Preset buttons wrap rather than causing horizontal scrolling.
- Keep the panel outside loading/table conditionals, so requests do not remount
  inputs or discard focus. Label the single memo input **Memo** and explain that
  it searches transaction and split memos. Amount is a text input because it
  accepts currency/sign prefixes.
- All workspace roles that can view transactions can filter them.
  Without a workspace, retain the current warning and hide filter controls.
- No chip UI, second toggle, new UI library, or separate uncategorized toolbar.

### Applying and Clearing

There is no Apply button and no request on each keystroke.

| Action | Behavior |
| --- | --- |
| Edit payee, category, memo, or amount | Keep the local draft while typing; commit on Enter or blur. |
| Change a date or checkbox | Commit immediately. |
| Choose a preset | Set both dates atomically and commit once. Preserve all non-date criteria. |
| Submit quick search | Commit current panel drafts and the visible search together. |
| Clear quick search | Clear search, commit current panel drafts, and preserve panel criteria. |
| Click Clear All | Clear search, every panel criterion, drafts, and validation; load page 1 once. Keep the panel open. |
| Collapse panel | Commit valid drafts before hiding; if invalid, keep it open and show the error. |
| Change page | Use applied criteria, not uncommitted drafts. A newly committed filter takes precedence and requests page 1. |
| Leave the list and return, including visits to unrelated pages | Restore applied search, every panel criterion, page, and panel expansion state from the store. |
| Change workspace | Discard uncommitted drafts, retain applied criteria and panel expansion state, clear old results, and load page 1 for the new workspace. |
| Reload after create, quick edit, or rule creation | Use the same applied criteria and existing page-reset behavior. |

Use one commit path: validate the complete draft, update the store atomically,
and ask the page to load page 1. Suppress normalized no-op commits, including
Enter followed by blur. A click that blurs a field and then invokes a preset,
Clear All, search, or pagination must resolve to one final action/request, not
an intermediate blur request followed by another request. Coalesce those
same-interaction events before dispatching.

Keep the current hydration/loading-disabled contract for the filter toggle.
Disable panel action controls consistently while loading; text drafts may remain
editable, but are not applied until the request finishes or a later action.
Do not silently discard a pending committed change.

This deliberately changes **Clear Filters** into **Clear All**, including quick
search, to satisfy Story 2216. The search bar's own clear button remains
search-only. Store actions must distinguish clearing panel criteria from clearing
the whole query; do not change unrelated consumers' clear behavior implicitly.

### Validation and Feedback

- Trim surrounding whitespace when applying ordinary text filters. Sanitize the
  category filter using the shared category rules before matching. A category
  input that sanitizes to empty means no category restriction; use Uncategorized
  only to find empty categories. Blank ordinary text means no restriction.
  An empty Amount means no restriction; invalid nonempty Amount is an error,
  not an ignored filter or a text search.
- Validate date ordering and amount syntax before sending a request. Keep invalid
  drafts visible, show associated inline errors, and focus the first invalid
  field on Enter/explicit actions. Keep the last applied query/results intact.
- Keep a panel with invalid fields open. Existing server validation remains
  authoritative and uses `handleApiError` / `ErrorDisplay`; a failed request is
  never presented as successful zero results.
- With any applied criterion, zero results say **No transactions match your
  filters.** with a suggestion to adjust filters or use Clear All.
  Do not suggest creating a first transaction merely because filters found none.
- Keep a polite result-count status based on the server's total count. Mark
  results busy during a request; do not announce an old total as a new success.
  API errors should retain the controls and offer an explicit retry.

## Matching Contract

All active filters combine with **AND**. Quick search retains its existing **OR**
across fields, and that whole search predicate is ANDed with panel predicates.

| Query parameter | Predicate |
| --- | --- |
| `search` | Existing quick-search behavior, except its split-category predicate uses a sanitized copy of the term. |
| `payee` | Literal, case-insensitive substring in transaction payee. |
| `category` | Sanitize the supplied category, then match a literal, case-insensitive substring in any split category. |
| `memo` | Literal, case-insensitive substring in the transaction memo OR any split memo. |
| `amount` | Exact transaction amount under the sign rules below; never split amounts. |
| `fromDate`, `toDate` | Inclusive transaction date bounds; either can be omitted. |
| `uncategorizedOnly` | When true, any split has `Category == string.Empty`; false or absent adds no restriction. |

Use separate existential predicates for separate split filters. For example,
category `Food`, memo `refund`, and Uncategorized only can be satisfied by
three different splits in the same transaction; the memo may also match at the
transaction level instead. Do not require all criteria to match one split, and
do not duplicate transaction rows for multiple matching splits.

There is one Memo input and one `memo` query parameter, not separate transaction
and split memo controls. Its internal OR predicate is ANDed with the other
active filters.

### Text and Uncategorized

Reuse quick search's provider-translatable lowercase/substring pattern. Null
memos do not match. Retain the existing SQLite ASCII case-folding limitation;
do not promise broader Unicode case folding than quick search provides.

The dedicated Category filter must follow
[Category Sanitization](PRD-TRANSACTION-SPLITS.md#category-sanitization), using
[`CategoryHelper.SanitizeCategory`](../../../src/Application/Helpers/CategoryHelper.cs)
in Application before constructing the query. Do not duplicate its rules in
frontend code or normalize every stored category during query execution.
This trims/consolidates whitespace, removes empty hierarchy terms and whitespace
around separators, and capitalizes words. For example:

| Category input | Sanitized search term |
| --- | --- |
| ` home : utilities ` | `Home:Utilities` |
| `Home::Garden` | `Home:Garden` |
| `Home    and Garden` | `Home And Garden` |
| `Home:` | `Home` |
| ` : ` | Empty; no category restriction |

After sanitization, matching remains a case-insensitive substring search, not
category-list validation or hierarchical wildcard matching. `%`, `_`, `*`, and
quotes remain literal characters; `:` follows category sanitization rules.
In particular, `Food:*` has no wildcard meaning in this story.

For regular quick search, sanitize a separate copy of the term with
`CategoryHelper.SanitizeCategory` before constructing the split-category
predicate, then lowercase that copy for case-insensitive substring matching.
Keep the original trimmed term for payee, transaction memo, split memo, and
amount parsing. Do not change the visible search input or sanitize stored
categories within the query.

If the sanitized copy is empty, omit the category OR predicate; do not use
`Contains("")`, which would match every split category. Other quick-search
predicates still apply. For example, ` home : utilities ` searches categories
for `Home:Utilities`, while payee/memo matching uses `home : utilities`.
Quick search `:` omits category matching but can still match literal colons
in payee or memos. This category-predicate refinement is the only intentional
change to regular quick-search matching in these stories.

Stored categories already obey the sanitization invariant. Uncategorized means
`Category == string.Empty`, not null or an unsanitized whitespace string.
Whitespace-only input and empty hierarchy terms are cleaned to the empty string
before saving, not stored as alternative uncategorized representations.
A categorized first split with an empty later split must match; a zero-amount
empty-category split also matches. Transactions with no splits do not match:
the model requires at least one split, and this is not a data-repair filter.

Use the simple server-side predicate
`transaction.Splits.Any(split => split.Category == string.Empty)`.
No whitespace scanning, provider-specific trimming, or data migration is needed
for this filter.

### Amount

Use the existing `TransactionSearchHelper.TryParseAmount` grammar and sign rules,
as selected during design review:

- `42`, `42.00`, and `$42` match transaction amounts `42.00` and `-42.00`.
- `+42` / `+$42` match only `42.00`; `-42` / `$-42` match only `-42.00`.
- Comma thousands separators and invariant decimal points follow the parser's
  existing rules. Zero is an active amount filter.
- Exact decimal equality only: no rounding, tolerance, magnitude substring,
  split amount comparison, or matching numeric text in other fields.
- Malformed or out-of-decimal-range amounts produce a field validation error.

Keep `amount` a **string query parameter**, not `decimal?`: binding a decimal
would lose the difference between unsigned `42` and explicitly signed `+42`.
Use the generated client to URL-encode it (`+` must not become a space).
Invalid amount syntax is a 400 response, unlike quick search where nonnumeric
input remains a valid text search.

### Date Presets

Compute dates at selection time using the user's local calendar date, not UTC.
Store the resulting concrete dates, not a rolling preset name.

| Preset | From (inclusive) | To (inclusive) |
| --- | --- | --- |
| Last 30d | Today minus 29 days | Today |
| Last 3mo | Today minus 3 calendar months, then plus 1 day | Today |
| Last 12mo | Today minus 12 calendar months, then plus 1 day | Today |
| This year | January 1 of the current year | December 31 of the current year |
| All time | Empty | Empty |

Clamp calendar-month subtraction to the target month's last day before adding
the day; do not use overflowing JavaScript month arithmetic. This year includes
future-dated transactions in that year; it is not year-to-date. Custom one-sided
bounds remain valid. Initial dates remain empty: this does not implement 2215.

Use and extend [`dateOnly.ts`](../../../src/FrontEnd.Nuxt/app/utils/dateOnly.ts)
for local-date/preset operations and `fromDateInputValue` for API serialization.
Replace the list page's `new Date('YYYY-MM-DD')` conversion as part of wiring the
panel, so dates are not shifted in negative UTC offsets.

## Component and State Boundaries

Create `app/components/TransactionFilterPanel.vue` for the expanded panel,
including its transition/card, date presets, inputs, validation, and Clear All.
Move the date panel's existing expand styles with it.

The panel must be an **independent Vue component**, not inline page markup or
a composable. Its public contract consists of typed props, emitted events, and
the explicit validation/commit operation described below. It must be mountable
and testable without the transactions page, Pinia, a selected workspace, or an
API client. Pass all required state through that contract; do not reach into
page state, inject page callbacks implicitly, or import the transaction store.

- **Page**: keeps `SearchFilterBar`, its slotted toggle, API
  client, workspace handling, pagination, errors, dialogs, and request ownership.
  It reads/writes expansion state through the filter store and passes
  expanded/disabled state and the applied filter model to the panel.
- **Panel**: accepts a typed applied model, maintains a local editable draft,
  emits a validated complete model on commit and a separate `clear-all` intent.
  It performs no API calls and has no workspace/store dependency. Expose a
  validation/commit operation for parent search/collapse actions; a failed
  commit must prevent the parent action, not silently use older criteria.
- **Store**: retains the applied query and panel expansion state, not
  invalid/half-entered drafts. Add `showFilters: boolean`, defaulting to `false`,
  alongside search, filters, and page number; replace the page-local ref.
  Define/export a `TransactionAdvancedFilters` interface containing date and
  text strings plus `uncategorizedOnly: boolean`. Use one `filters` object,
  replacing the two standalone date refs and updating their page consumers.
  Keep search and page number alongside it. Extend `hasActiveFilters` to all
  panel dimensions. Preserve in-memory lifetime and existing reset boundaries.
  Do not reset criteria or expansion on page mount/unmount or ordinary navigation. Dates,
  payee, category, memo, amount (including its explicit sign), and
  Uncategorized only must all survive leaving and revisiting the list.
  Clear All clears query criteria but keeps the panel open. An explicit full
  store `reset()` also restores `showFilters` to `false`.
  A full reload/new app session may reset them; do not introduce localStorage,
  sessionStorage, or a saved-preset mechanism for this requirement.
- **Shared search component**: retain its general-purpose contract. Do not add
  transaction-specific fields or backend behavior to `SearchFilterBar`.

The page coordinates panel commits and search actions so they cause one state
update/request. Snapshot tenant, query, and page for each load. Only the latest
request may update results, errors, page, or loading. Invalidate outstanding
loads on workspace removal and unmount. An old request must not overwrite newer
filter results or expose the previous workspace's rows.

Preserve `filters-toggle-button`, its `:disabled="loading || !ready"` readiness
signal, `aria-controls="date-range-filters"`, `id="date-range-filters"`,
`data-test-id="date-range-filters"`, `fromDate`, `toDate`, and
`clear-filters-button`. The legacy date-named hook now identifies the entire
expanded panel. Add semantic hooks for each new field and preset, scoped to
that panel. Update page objects for the intentional Clear All behavior change.

## Backend and API

Extend the existing GET endpoint; do not create an advanced-search endpoint:

```http
GET /api/tenant/{tenantKey}/transactions?pageNumber=1&search=coffee&payee=Starbucks&fromDate=2024-01-01&toDate=2024-12-31&uncategorizedOnly=true
GET /api/tenant/{tenantKey}/transactions?category=Food&memo=trip&amount=%2B42
```

Add optional query parameters `payee`, `category`, `memo`, `amount`
and `uncategorizedOnly` to the controller. Omitted values preserve existing
behavior and Viewer authorization. Keep the existing response DTO and pagination.

Add an Application `TransactionFilterDto` for the five new criteria and pass it
as an optional final argument to `GetTransactionsAsync`. Existing feature calls
remain valid. Keep HTTP attributes out of Application and mapping in the thin
controller. Parse and validate the amount in Application so non-HTTP callers
receive the same rules; use the existing field-keyed `ValidationException` flow.

Compose a small `TransactionFilterHelper` with the existing search helper:

1. Start with the existing tenant-scoped, date/ID-descending transaction query.
2. Apply quick search, inclusive dates, and all advanced predicates.
3. Count the complete filtered query.
4. Apply Skip/Take and the existing DTO projection.

Use `Any`/EXISTS for split predicates, without loading split collections or
filtering already-paginated DTOs. Keep all LINQ expressions provider-translatable.
Application depends on Entities abstractions, not EF/Npgsql APIs. Preserve
boundary-only, source-generated logging; do not log filter values.

Regenerate [`apiclient.ts`](../../../src/FrontEnd.Nuxt/app/utils/apiclient.ts)
with [`Generate-ApiClient.ps1`](../../../scripts/Generate-ApiClient.ps1), then
update every generated-method caller, including test setup if affected by
positional argument changes. Never edit generated client code manually.
No schema migration or speculative indexes are required. Ordinary indexes do
not solve leading-substring search; measure representative queries first.

## Verification and Delivery

Deliver Stories 2216 and 2214 together. Suggested implementation order:

1. Add the filter DTO/helper and focused backend tests, including dedicated
   category-filter sanitization and empty-category matching. Preserve existing
   quick-search behavior except for its category-predicate refinement above.
2. Extend the controller and regenerate the API client.
3. Extend the store and date helpers; extract and expand the existing panel.
4. Wire one commit/load path, validation, Clear All, and filtered empty states.
5. Write the Gherkin feature for all planned functional scenarios at once,
   within the five-scenario budget. Reuse existing step phrases/definitions
   wherever possible and use consistent, reusable wording for new steps.
6. Implement one functional scenario at a time: add only the missing step
   definitions and page/component model support required by that scenario,
   then run it against the containerized app and iterate until it passes.
   Do not implement support for later scenarios in advance. Proceed to the
   next scenario only after the current one passes, reusing support already
   implemented.
7. Once all scenarios pass individually, run them together with the focused
   checks below to verify the complete change.

### Focused Test Coverage

| Layer | Required checks |
| --- | --- |
| Application unit | Amount parser reuse and invalid-value validation; dedicated Category filter and quick search's category predicate reuse the sanitizer; empty sanitized category disables only that predicate; quick search preserves the original trimmed term for other fields and amount parsing; normalization and active zero amount. Existing quick-search parser tests remain valid. |
| Application integration | Every field independently; all filters ANDed with search/dates; one memo term matches transaction memo or any split memo, including a later split, without duplicates when both match; null memos do not match; independent split predicates; empty later split; mixed categories; fully categorized excluded; sanitized category terms match canonical stored values in both search modes; whitespace/separator-only Category input adds no category restriction; quick search with an empty sanitized category term does not match all rows and still searches payee/memos literally; zero-amount empty-category split; no duplicate rows; signed/unsigned amounts; tenant isolation; filtered count before pagination. |
| Controller integration | Optional defaults; combined query binding; encoded plus amount; false/true checkbox; invalid amount/date range yields 400; Viewer access and tenant authorization regressions. |
| Frontend component/store | Initially collapsed; expanded/collapsed state survives page unmount/remount and workspace changes; draft/commit/validation; Enter+blur deduplication; action/blur coalescing; All time preserves non-date fields; Clear All clears search too and keeps the panel open; search clear preserves panel; every criterion survives unmount/remount within the same store session; full reset clears criteria and collapses the panel. |
| Date helpers | Month ends, leap years, year boundaries, inclusive 30-day count, and unchanged serialization in positive/negative UTC offsets. |
| Functional | At most five focused scenarios for this entire design, shared across Stories 2216 and 2214; see the scenario budget below. Exhaustive matching, boundary cases, and state permutations belong in faster lower-layer tests. |
| Request coordination | Rapid commits and workspace switch with out-of-order success/failure; only the latest context updates results/loading/errors; failed request can be retried. |

Extend the existing
[Application search integration tests](../../../tests/Integration.Application/TransactionSearchFeatureTests.cs)
and [controller search tests](../../../tests/Integration.Controller/TransactionSearchControllerTests.cs),
or add adjacent advanced-filter test classes if that keeps them focused.
SQLite integration tests alone do not prove PostgreSQL query translation;
exercise combined split/category predicates against the containerized backend.

Write all planned scenarios together in
[`TransactionFiltering.feature`](../../../tests/Functional/Features/TransactionFiltering.feature)
before implementing new step definitions or page models. Then follow the
one-scenario-to-passing loop above, adding locators/actions to
[`TransactionsPage.cs`](../../../tests/Functional/Pages/TransactionsPage.cs)
only as each scenario requires them.
Follow the [functional-test guidance](../../../tests/Functional/README.md).

### Functional Scenario Budget

Keep functional tests narrowly focused to control execution time. **No more than
five scenarios may be added or expanded to cover this whole design**, across
both stories, not five per story or test file. Scenario Outline examples count
individually toward this limit; do not hide a larger suite in parameterization
or long multi-workflow scenarios. Existing unchanged quick-search scenarios
remain regression coverage, not an invitation to duplicate them.

Use the following five end-to-end checks:

1. Combined quick search and advanced payee/category/Memo/date criteria return
   the expected transactions, exercising the extracted panel and backend wiring.
2. Uncategorized only includes a mixed-split transaction with an empty later
   category and excludes fully categorized transactions.
3. An amount filter with one representative date preset returns expected results.
4. Clear All removes an applied search and advanced criteria, restores results,
   and leaves the panel open.
5. Leaving the list for another page and returning retains criteria and panel
   expansion state and reloads the expected results.

Cover memo-source permutations, amount signs, all date presets and boundaries,
sanitization details, no-match states, pagination arithmetic, request races,
and other combinations in unit, component/store, or integration tests rather
than adding browser scenarios. The five scenarios are a ceiling, not a quota.
Put all `When` actions before `Then` assertions; arrange applied filters in
`Given` steps for the Clear All scenario instead of asserting an intermediate
state and resuming actions.

Run focused Application/controller tests, relevant component/date/store tests,
frontend lint/type checks, and client-generation build checks. For end-to-end
validation, build/start containers with
`.\scripts\Start-Container.ps1 -Build -NoBrowser`, then run focused functional
tests against those containers using the repository test workflow. Never use
Aspire/development runsettings or tear down app containers.

For the PRD's performance target, measure server filtering with 10,000+
transactions and multiple splits, including combined substring/uncategorized
criteria and late result pages. Report warm-query timings against the <200 ms
target with environment and fixture details; do not claim that threshold based
only on UI responsiveness or a passing small-fixture test.
