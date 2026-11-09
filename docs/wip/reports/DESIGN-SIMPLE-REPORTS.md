---
status: Approved
target_release: V3.0
ado: Story 2248
parent: Feature 2207 Reports
prd: PRD-REPORTS.md
---

# Detailed Design: Built-in Income/Expense Report (simple-reports)

## Overview

Story 2248 gives a user a set of built-in reports and a tabular view of categorized
income and expenses for a calendar year. Report definitions select the included
categories and specify the initial category depth and whether month columns are
shown. The report aggregates transaction **splits**, presents category rollups,
and shows row and column totals.

The design is intentionally limited to Story 2248. Year/depth/month controls,
chart views, and transaction drill-down remain owned by their respective stories.
The report contract and page structure leave room for those features without
building them now.

## Scope

### In scope

- A catalog of the built-in reports defined in [PRD-REPORTS.md](PRD-REPORTS.md).
- A user-selectable report and a report table for the currently selected workspace.
- Calendar-year split aggregation, using the current year by default.
- Category filtering, hierarchical rollups, grand totals, row totals, and row
  percentages.
- Inclusion of uncategorized splits according to each report definition.

### Out of scope

- Choosing a different year or changing category depth/month display (Story 2249).
- Charts (Story 3), summary report composition (Story 4), historical multi-year
  reporting (Story 6), and transaction drill-down (Story 7).
- Custom report definitions, export, and database schema
  changes.

## User experience

Add a Reports destination to the authenticated workspace navigation, following
the existing Transactions page conventions. The page requires an active
workspace and uses that workspace's tenant context for every request.

The page contains:

1. A report selector populated from the built-in report catalog.
2. The selected report's name and a table with category rows and totals.
3. Loading, error, and no-data states consistent with existing pages.

Changing the selection loads the selected report and saves its ID in browser
localStorage, scoped to the current workspace. When the user navigates away and
returns, restore that workspace's last selected report. If there is no saved
selection, or the saved ID is no longer in the built-in catalog, select the
catalog's first report, Expenses, and save that selection. The backend catalog
order defines the default; valid saved selections still take precedence.

With no matching splits, show the table's column headers and a clear no-data
message; do not manufacture category rows. This story persists only the selected
report ID. Per-report year, depth, and display settings are added by Story 2249
and should use the same workspace-scoped browser preference mechanism, keyed by
report ID, so returning to a report restores its settings as well.

For reports with month columns, the table is ordered as Category, January through
December, Total, and % of report total. For reports without month columns, it is
Category, Total, and % of report total. Category rows are indented by depth. A
footer at the bottom shows the report grand total for each displayed amount
column; do not add a separate report-root row. The percentage footer is 100%
when the report total is nonzero. Within the table, render each category subtotal
before its descendants, with child categories indented under it. In a two-level
report, style all level-one rows in bold, including rows without displayed
children; level-two rows use normal weight and indentation. In a one-level
report, category rows use normal weight. Formatting depends on configured report
depth, not subtotal status or whether month columns are shown.

## Built-in report definitions

Implement the definitions in the PRD as application code, not database records:

| Report | Category filter | Default depth | Month columns | Uncategorized | Amount sort |
| --- | --- | ---: | --- | --- | --- |
| Income | `Income:*` | 1 | No | No | Descending |
| Taxes | `Taxes:*` | 1 | No | No | Ascending |
| Expenses | Expense categories | 1 | No | Yes | Ascending |
| Savings | `Savings:*` | 1 | No | No | Ascending |
| Transfers | `Transfer:*` | 1 | No | No | Ascending |
| Income Detail | `Income:*` | 2 | Yes | No | Descending |
| Taxes Detail | `Taxes:*` | 2 | Yes | No | Ascending |
| Expenses Detail | Expense categories | 2 | Yes | Yes | Ascending |
| Savings Detail | `Savings:*` | 2 | No | No | Ascending |
| All | `*` | 1 | No | Yes | Ascending |
| All Detail | `*` | 2 | Yes | Yes | Ascending |

Use stable, URL-safe report IDs independent of display names. Category matching
uses the top-level category segment, case-insensitively, and requires a segment
boundary: `Income:Salary` matches `Income:*`, but `IncomeTax` does not. The
reserved top-level names are Income, Taxes, Savings, and Transfer. Expenses
means all other categorized splits, plus uncategorized splits when enabled.
Uncategorized means a split whose category is the empty string and is displayed
as a synthetic top-level `Uncategorized` row.

## Category aggregation rules

1. Select splits whose parent transaction belongs to the current tenant and falls
   within the requested calendar year, inclusive of January 1 and December 31.
2. Apply the report's category filter to each split. A split is the unit of
   aggregation: do not substitute the parent transaction amount or count a
   multi-split transaction only once.
3. Group matching splits by their exact category and sum their stored amounts.
   Preserve signs; credits, refunds, and unexpected signs are not inverted.
4. Build category nodes from the grouped category paths. Each displayed node's
   amount is the sum of its own direct splits and all descendant splits. A node
   with descendants is therefore a subtotal. Do not add a synthetic report-root
   category row; the report root total is shown once in the bottom totals footer.
5. Display category nodes through the report definition's default depth below the
   filter root. Deeper categories contribute to the deepest displayed ancestor.
   Include the synthetic Uncategorized row only where the definition enables it.
6. Compute each report/month grand total from the matching exact-category groups
   once, not by adding displayed parent and child rows. This prevents hierarchy
   rollups from double-counting amounts.
7. Order rows in depth-first category order, placing each parent before its
   descendants. Sort siblings independently by signed annual total, including
   direct amounts and all descendants even below the displayed depth. The internal
   report definition selects ascending order by default; Income and Income Detail
   use descending order. Do not sort by absolute value, individual month totals,
   or globally across parents. Equal totals use case-insensitive category-name
   ordering, then ordinal name ordering as a stable tie-breaker. Uncategorized
   participates in the same sibling ordering. The backend supplies this order;
   the frontend preserves it.

For each displayed category row, calculate its percentage as its annual row
total divided by the annual report grand total. This remains an annual share for
month-column reports. If the annual grand total is zero, return a null percentage
and display an em dash; otherwise preserve the signed ratio. This makes refunds
or mixed-sign categories visible without silently converting them to absolute
values.

## Backend design

Follow the existing Clean Architecture and tenant-scoped BFF patterns:

- **Application:** Add a Reports feature and DTOs. Keep built-in definitions and
  category/report aggregation in the Application layer. Use the existing data
  provider to query splits and their parent transactions, scoped by the current
  tenant from `ITenantProvider`.
- **Controllers:** Add a thin tenant-scoped Reports controller. Require at least
  the Viewer role, return typed DTOs, and use the repository's
  `[LoggerMessage]` partial-method pattern for boundary logging. Do not log
  transaction or category contents.
- **Data:** No new entity or migration is required. Start with an indexed,
  tenant-and-date-constrained query over the existing Transaction/Split
  relationship. Aggregate exact category totals in the database where supported;
  build the small category tree in Application code. The expected volume and
  <500 ms single-year target in the PRD do not justify adding a cache or schema
  change up front.
- **Frontend:** Add a Reports page that requests definitions and results through
  the generated API client. Regenerate the client from the controller contract
  using the existing project workflow.

### API shape

Use the existing tenant route convention:

- `GET /api/tenant/{tenantKey:guid}/reports/available` — report ID, display name, and
  definition metadata needed by the selector.
- `GET /api/tenant/{tenantKey:guid}/reports/{reportId}?year={year}` — result for the
  requested report and calendar year. `year` is optional and defaults to the
  current calendar year for Story 2248.

The year parameter provides an integration point for Story 2249; the Story 2248
page does not expose a year picker. Unknown report IDs return the standard 404
problem response. Invalid years use the standard validation problem response.
Tenant authorization and error handling use existing middleware and policies.

### Response model

Return typed report metadata and rows rather than a matrix of untyped JSON values:

- Report ID, name, year, and nullable `CategoryRoot` from the report definition.
  Use the category root as the first-column header, falling back to `Category`
  when null (Expenses and All reports). The report title remains the report name.
- Column metadata describing whether the report has month columns.
- Ordered rows containing the full category path, a backend-supplied local category
  name for display, depth, subtotal status, twelve monthly
  amounts when enabled, annual total, and nullable percentage.
- A totals object containing each displayed month total and the annual grand
  total.

For reports without month columns, omit monthly amounts/columns. Preserve numeric
amounts as decimals. This response shape makes the table straightforward to
render and can later support user-selected display options without changing the
meaning of report totals.

## Frontend table behavior

Use one table component for both annual-only and month-column reports. Derive
headers from response metadata, render category indentation from the row depth,
and apply hierarchy styling using the catalog's configured report depth. Keep
`IsSubtotal` as a semantic indicator of totals over other displayed rows, not a
formatting flag. Use the explicit totals object for the bottom
footer. Do not derive the footer by summing displayed rows, because parent
subtotals overlap with child rows.

Render the backend-supplied category name rather than the full path: ancestor
names are already represented by parent rows or by the report itself. For example,
the Income report displays `Salary` for `Income:Salary`, and Expenses Detail
displays `Movies` beneath `Entertainment` for `Entertainment:Movies`. Keep the full
category path in the response for identity and future transaction drill-down.

Format amounts using the app's established currency/number formatting. Display
the row percentage after the annual total; render null as an em dash. The table
does not make cells interactive in this story. Keep report identity and row
category data in the response so Story 7 can add drill-down without parsing
formatted labels.

## Validation and tests

Add focused coverage at each layer:

- **Application unit tests:** all built-in definitions; category matching
  (including case and segment boundaries); reserved categories; uncategorized
  inclusion; depth truncation; direct amounts plus descendant rollups; signed
  amounts; zero denominators; totals that do not double-count hierarchy rows;
  per-report sort direction; sibling ordering by annual roll-up including hidden
  descendants; and deterministic equal-total ordering.
- **Application integration tests:** tenant and calendar-year constraints on
  split aggregation, including a split transaction and dates at the year
  boundaries.
- **Controller integration tests:** Viewer access, denial without tenant access,
  invalid report ID, and validation errors.
- **Frontend tests:** use fake report data to verify report content, backend row
  order, month visibility, explicit totals, selection persistence, empty data,
  loading, and API error states. Prefer stable `data-test-id` selectors. Do not
  assert CSS classes, indentation, exact number formatting, or other cosmetic
  details; those should remain free to change without breaking tests.

Confirm the single-year response meets the PRD's <500 ms target on representative
tenant data. If database profiling reveals a bottleneck, optimize the query and
indexes based on the measured plan rather than adding speculative infrastructure.

## Follow-on integration points

- **Story 2249:** The query already takes a year; later add selected year, depth,
  and month-display options and persist per-report preferences as that story
  requires. Original YoFi supports at most four displayed levels because deeper
  formatting becomes difficult to interpret. For a four-level report, level four
  is normal weight, level three is bold, level two is highlighted, and level one
  is highlighted more strongly. Preserve that guidance when adding configurable
  depth; the initial reports only require the one- and two-level styles above.
- **Story 3:** Keep the report response structured so chart series can be
  derived from rows and month values. Charts remain outside this page change.
- **Story 7:** Preserve report ID, category path, month, and year as data fields
  for future transaction filters. No navigation or filters are added now.
- **Story 4:** The stable report catalog and totals can later back summary
  sections without embedding summary composition in this feature.

## Open questions

No product questions block this design. Story 2248 uses the current calendar year,
the default depth and month-display settings from the report definition, signed
percentages, and an em dash where a zero grand total makes percentage undefined.
