---
status: In Review
target_release: Beta 1
ado: "[Story 2213](https://dev.azure.com/jcoliz/YoFiV3/_workitems/edit/2213): User - Quick Text Search"
---

# Story 2213: Transaction Quick Text Search

## Purpose and Scope

Provide one always-visible search input so users can find transactions without
choosing a field. This document consolidates **only Story 2213** from the
[transaction filtering PRD](PRD-TRANSACTION-FILTERING.md#story-2213-user---quick-text-search).
The broader [UI recommendations](TRANSACTION-FILTERING-UI-RECOMMENDATIONS.md)
provide background, not an implementation checklist for this story.

**Included**:

- Search payee, transaction memo, every split's category and memo, and transaction amount.
- Debounced, server-side filtering with correct pagination and tenant isolation.
- An inline clear button, accessible feedback, and search-specific empty/error states.
- Composition with the date range controls that already exist.

**Excluded**:

- Story 2214: uncategorized filtering.
- Story 2215: default 12-month range, date chips, and persisted preferences.
- Story 2216: collapsible panel, field-specific inputs, date presets, and filter badges.
- Story 2217: balance filtering or balance indicators.
- Story 2218: report navigation and hierarchical category wildcard matching.
- URL synchronization, saved searches, keyboard shortcuts, highlighting matches,
  split-detail UI changes, and a general-purpose filter framework.

There is no dependency on implementing those stories first. This is a design-only
deliverable; the changes below describe subsequent implementation work.

## Current Implementation

- The list page is now
  [`app/pages/transactions/index.vue`](../../../src/FrontEnd.Nuxt/app/pages/transactions/index.vue),
  not the older `transactions.vue` location in the recommendations.
- The page owns date filters, pagination, workspace selection, loading, and
  `ErrorDisplay` state. Requests use the generated `TransactionsClient`.
- [`TransactionsController.GetTransactions`](../../../src/Controllers/TransactionsController.cs)
  accepts `pageNumber`, `fromDate`, and `toDate`; dates are `DateOnly`.
- [`TransactionsFeature.GetTransactionsAsync`](../../../src/Application/Features/TransactionsFeature.cs)
  starts with a tenant-scoped query, orders by date then ID descending, counts
  matches, pages them, and projects the existing result DTO.
- Transactions already have a
  [`Splits`](../../../src/Entities/Models/Transaction.cs) navigation collection.
  The list DTO displays only the first split's category, but search must inspect
  **all** splits, independently of what the row displays.

## User Experience

Place a full-width search row immediately below the page heading/error area and
above the existing date filter card. Keep it outside the loading/table conditional
so typing does not remove the input or lose focus.

```text
Transactions                                      [New Transaction]

Search transactions
[ Search payees, categories, memos, or amount...             [x] ]

[ Existing From Date / To Date controls / Clear Filters          ]

12 matching transactions
[ Existing transaction table and pagination                      ]
```

- Show the search row whenever a workspace is selected, including an empty
  workspace and zero-match results. Without a workspace, retain the current
  workspace warning and do not send a request.
- Use Bootstrap form/input-group styling and the existing `FeatherIcon` for
  the clear icon. No new UI dependency or standalone filter component is needed.
- Give the input a persistent label, **Search transactions**. The placeholder is
  supplemental, not its accessible name.
- Show the clear button whenever the raw input contains text, including whitespace.
  Its accessible name is **Clear search**; it is a `type="button"`.
- Keep search editable during requests. Disable it until client hydration is
  ready, consistent with the page's existing readiness pattern.
- On narrow screens the search row remains full-width; no modal or panel is added.
- Do not autofocus on initial load or override browser keyboard shortcuts.

### Input and Loading Behavior

| Action | Required behavior |
|---|---|
| Type or paste | Wait 300 ms after the last edit, then search from page 1. |
| Press Enter | Cancel the timer and search immediately from page 1; do not submit a form. |
| Click clear | Cancel the timer, empty the input, search immediately from page 1, and return focus to the input. Preserve date bounds. |
| Erase all text manually | Apply the empty search after the normal debounce. |
| Change dates | Cancel/flush pending search, use the current input and dates, and load page 1 once. |
| Click existing Clear Filters | Preserve its existing date-only behavior, flush current search, and load page 1 once. Search has its own clear button. |
| Change page | Use the applied search and current dates. If a changed search is pending, apply it on page 1 instead of requesting a page from the previous result set. |
| Change workspace | Cancel pending work, retain the visible search/date values, clear old workspace results, and load page 1 for the new workspace. |
| Remove workspace or leave page | Cancel the timer and invalidate outstanding list requests. |
| Refresh after create/quick edit/rule creation | Reload using current search and date values; preserve existing page-reset behavior. |

Normalize leading/trailing whitespace only when applying search. Keep the user's
raw text in the input. Suppress a normalized no-op only when current results
already represent that term and no pending or invalidated request needs replacing.
Returning to an earlier term while requests are in flight must still settle
loading and produce results for the current context.

Keep the existing loading indicator, but never render a successful "no matches"
message while a request is pending or has failed. After success, announce the
filtered total from pagination metadata in a polite status region, not merely
the number of rows on the current page.

With an active search and zero results, show **No transactions match your
search.** and **Try another search or clear the search.** Do not show
**Create your first transaction** for a search that returned no matches.
With no search, preserve the existing empty-state behavior.

Failures use `handleApiError` and `ErrorDisplay`; do not turn an error into an
empty successful result. Leave the input available so users can edit or resubmit.

## Search Contract

### Matching Rules

Trim the supplied term on the server as well as the client. A missing, empty, or
whitespace-only term means **no search restriction**, not a validation error.

A transaction matches when **any one** of these conditions is true:

| Field | Match |
|---|---|
| Transaction payee | Case-insensitive literal substring |
| Transaction memo | Case-insensitive literal substring; null does not match |
| Any split category | Case-insensitive literal substring |
| Any split memo | Case-insensitive literal substring; null does not match |
| Transaction amount | Exact magnitude for unsigned numeric terms; exact signed equality when an explicit sign is supplied |

Use the entire term, not word tokens. `coffee shop` searches that phrase.
Characters such as `%`, `_`, `*`, quotes, and `c=food` are ordinary text, not
wildcards, SQL, or query syntax. Do not search date, source, external ID, or split
amounts.

Numeric terms still search every text field; the amount comparison is an
additional OR condition. For amount parsing only, accept one optional `$` prefix
alongside an optional explicit sign, then parse once with invariant culture.
Allow a decimal point and comma thousands separators, but not other currency
symbols or exponents. Examples:

- `42`, `42.00`, `+42.00`, and `-42.00` are valid decimal searches.
- Unsigned `42` or `42.00` matches both transaction amounts `42.00` and `-42.00`.
- `$42` and `$42.00` behave like unsigned `42`, matching both signs.
- An explicit sign restricts the amount match: `-42` matches only `-42.00`;
  `+42` matches only `42.00`.
- Accept the sign before or after `$`: `-$42` and `$-42` match only `-42.00`;
  `+$42` and `$+42` match only `42.00`.
- `42` does not match an amount of `142` or `42.50`.
- `1,234.56` and `$1,234.56` behave like `1234.56`, matching both
  `1234.56` and `-1234.56`. Explicit signs still restrict the amount match.
- `1e2` remains a valid text search but does not add an amount condition.
- Unparseable or overflowing numbers are text-only searches, not request errors.

Preserve any explicit sign while removing the optional dollar prefix for parsing.
Accept commas only as thousands separators in the integer portion: an initial
group of one to three digits followed by groups of exactly three digits.
Ungrouped numbers remain valid. Validate grouping before removing commas;
do not interpret a decimal comma such as `42,50` as `4250`, or accept malformed
grouping such as `1,,234`.
Reject repeated dollar symbols or signs for amount parsing rather than stripping
arbitrary characters. Keep the original trimmed term, including `$` and commas,
for text-field searches.
For an unsigned parsed value, compare the transaction amount to that value OR
its negation; for an explicitly signed value, compare to that value only.
These sign rules apply only to the amount condition; text-field matches remain
independent OR conditions.

### Database Target and Unicode Matching

Do not copy the recommendations' assumption that plain `.Contains()` is
automatically case-insensitive. **PostgreSQL is the production target** and will
replace SQLite before production usage. SQLite remains a local development/test
convenience; its limitations do not define the production search contract.

Production search must support case-insensitive matching of accented letters,
for example `CAFÉ` matching `café`. Accent-insensitive matching (`cafe` matching
`café`) is not required. PostgreSQL's behavior depends on its configured
locale/collation; select and verify that configuration during the database
migration. Do not promise universal Unicode case equivalence (such as `ß` matching
`ss`) or equivalence of different Unicode normalization forms.

For PostgreSQL, use database-side case-insensitive substring matching, typically
Npgsql's translation of `ILIKE`. Escape the escape character, `%`, and `_` in the
user term before adding the surrounding `%` wildcards, and specify the escape
character explicitly. Parameterize the resulting pattern and null-check optional
memos. Do not lowercase the term in .NET: let the configured database perform
case matching consistently for both the stored text and the search pattern.

For SQLite-only local runs, database-translatable lowercase normalization followed
by literal `Contains` is an acceptable limited implementation. Normalize both
operands consistently for ASCII; document that SQLite's built-in lowercase
handling does not provide equivalent non-ASCII case matching. Do not add ICU
extensions, custom collations, or a Unicode compatibility framework just to make
SQLite imitate PostgreSQL.

SQLite tests can verify search composition and application behavior, but cannot
establish PostgreSQL matching semantics. PostgreSQL integration tests are a
production-release gate. The database migration itself remains separate from
Story 2213; an initial SQLite implementation does not prove production readiness.

### Composition and Pagination

The effective predicate is:

```text
current tenant
AND existing inclusive date bounds, if supplied
AND (payee OR transaction memo OR any split category OR any split memo
     OR numeric transaction amount match using the sign rules above)
```

Apply the predicate **before** both the count and `Skip`/`Take`. Use navigation
`Any` expressions so multiple matching splits still produce one transaction.
Preserve the existing date/ID ordering, page size, response DTO, and pagination
metadata contract. Never filter only the currently loaded browser page.

## Implementation Design

### API and Application

Add one optional `string? search = null` query parameter to the list action and
feature method, appended after existing parameters to preserve existing positional
callers. Do not introduce the recommendations' multi-story filter DTO.

```text
GET /api/tenant/{tenantKey}/transactions?pageNumber=1&search=coffee
GET /api/tenant/{tenantKey}/transactions?pageNumber=1&fromDate=2026-01-01&toDate=2026-12-31&search=-42.00
```

Existing requests without `search` retain their behavior. Existing Viewer
authorization and tenant scoping are unchanged. Keep current validation and
ProblemDetails handling for date bounds.

In the feature, normalize/parse once and add the search predicate to the existing
query before counting. Keep all matching database-side through `IDataProvider`;
do not materialize transactions to perform search. Retain the current base-query
and DTO projection patterns. `Any` translates to a correlated existence check;
it does not itself require adding an `Include` or flattening splits into rows.

Keep provider-specific matching in the data layer behind the application's data
access boundary; do not introduce Npgsql/EF dependencies into the Application
project. The PostgreSQL migration must wire and test its matching implementation
without changing the public search API. Do not build a general-purpose query
framework for this single search predicate.

Search itself requires no schema migration, new index, or result DTO field.
Provisioning PostgreSQL and selecting its locale/collation belong to the separate
database migration. Ordinary B-tree
indexes are not a general solution for leading-substring searches; measure
before adding database structures.

Regenerate **both** API clients with
[`scripts/Generate-ApiClient.ps1`](../../../scripts/Generate-ApiClient.ps1).
Do not hand-edit generated clients. Update controller XML documentation so
OpenAPI describes the new parameter and its matching behavior.

### Frontend State and Request Ownership

Keep the implementation in the existing transactions page:

- Raw input text and applied normalized search are distinct state.
- Own one 300 ms timer and clear it before scheduling or immediately applying.
- Pass the applied term (or `undefined` when empty) through the generated client
  on every list load, including pagination and mutation refreshes.
- Centralize search/date/workspace/page requests through the page's list loader.
  Avoid duplicate requests from a new search watcher and explicit handlers.
- Maintain a monotonically increasing request generation. Invalidate it on
  input/context changes, not only when the next debounced request starts.
- Each request captures tenant, dates, search, page, and generation. Only the
  current generation may commit results, pagination, loading completion, or errors.
  A late response from an earlier search/workspace must not overwrite current state.
- Superseded requests are deliberately ignored, not reported as user-facing
  failures. Failures for the current request follow normal error handling.
- Dispose the timer and invalidate generations on unmount.

Use the `filters-toggle-button` (in the search bar) as the readiness signal for
functional tests; the date range filters pane, including `clear-filters-button`,
is collapsed by default. Add stable test IDs `transaction-search`, `clear-transaction-search`, and
`transaction-search-status` for the new controls and feedback.

## Verification and Acceptance

Future BDD scenarios are collected in
[`TransactionQuickSearch-Prd.feature`](../../../tests/Functional/Features/future/TransactionQuickSearch-Prd.feature).
They cover only Story 2213, with unique scenario IDs and roughly equal numbers
at four priority levels. Each scenario has one Given/When/Then flow, with no
actions after assertions. They are specifications, not yet wired to executable steps.

| Story 2213 acceptance criterion | Verification |
|---|---|
| Single search bar always visible at top | Browser test: present above dates/table with a selected workspace, including loading and zero results; usable at mobile width. |
| Searches all five field groups | Database integration tests: independent fixtures for payee, transaction memo, split category, split memo, and transaction amount; verify against PostgreSQL before production release. |
| ANY split can match | Database tests: only a non-first split matches; several splits match but return one transaction with total count one. Run against PostgreSQL as well as local SQLite. |
| Results update while typing, debounced | Frontend test with controlled timers: no request before 300 ms; one request after the final keystroke. Browser test confirms results update without submission. |
| Clear button appears when text exists | Frontend/browser tests: absent when empty, present with text, clears immediately, resets page, preserves dates, restores focus. |

Additional regression coverage:

- **Application integration (SQLite)**: mixed-case ASCII, null memos, literal
  wildcard characters, whitespace normalization, numeric/text OR behavior,
  unsigned amounts matching either sign, optional dollar prefixes with and without
  explicit signs, comma-grouped amounts with and without dollar prefixes,
  malformed grouping and decimal commas, malformed/repeated prefixes,
  explicit signs restricting amount
  matches, zero amounts, non-matching split amounts, numeric overflow,
  other-tenant exclusion, inclusive date composition, ordering, and matches
  beyond the original first page with accurate filtered pagination metadata.
- **Production-provider integration (PostgreSQL, required before release)**:
  repeat the database search regressions against the production locale/collation.
  Verify accented case pairs such as `CAFÉ`/`café`, accent-sensitive behavior,
  and literal `%`, `_`, and escape-character searches. Confirm server-side
  translation of split predicates, unsigned/signed decimal comparisons,
  filtered counts, and paging.
  Record the locale/collation used; SQLite-only results are not a substitute.
- **Controller integration**: `search` binding, unchanged no-search response,
  date/search composition, and existing authentication/tenant-role behavior.
- **Frontend**: Enter flush, immediate clear during a pending timer, normalized
  no-op edits, pagination, date changes, mutation refresh, workspace changes,
  unmount cleanup, and out-of-order success/error responses. Include an old
  response arriving during the debounce window and stale `finally` handling.
- **Functional**: payee search, clear, date composition, page reset, searchable
  empty state, keyboard access, and visible current-request failure feedback.
  Use the existing page object and generated API client conventions.

For the PRD's **under 200 ms search-response target at 10,000 transactions**,
measure warm authenticated list requests against PostgreSQL with the intended
production locale/collation and a representative tenant with
multiple splits, including count and page retrieval. Include text, amount, and
no-match cases; record environment and latency distribution. Measure API time
separately from the intentional 300 ms typing debounce. This is a target to
verify, not a performance claim or a reason to introduce pagination (already present).

Story implementation is complete for local development when the five acceptance
criteria and relevant local regressions pass, generated clients are synchronized,
and existing date-only and no-search transaction workflows remain unchanged.
Production readiness additionally requires the PostgreSQL migration and the
PostgreSQL matching, query, and performance verification described above.
