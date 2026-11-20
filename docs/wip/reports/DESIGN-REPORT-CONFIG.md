---
status: Approved
target_release: V3.0
ado: Story 2249
parent: Feature 2207 Reports
prd: PRD-REPORTS.md
---

# Technical Design: Configure Report Display (report-config)

## Scope

This design covers the remaining display-configuration acceptance criteria from
[Story 2249 in the Reports PRD](PRD-REPORTS.md):

- choose month-by-month columns plus a row total, or annual totals only;
- choose the displayed category depth; and
- auto-save and reset those settings per report and tenant.

Scenario 2228 (report year selection) is excluded because it is implemented by
[Select Report Year](DESIGN-REPORT-YEAR.md). The report year remains one
workspace-wide setting and is never changed by this feature's Reset action.

Story 2248 report catalog selection, category filtering, aggregation, sorting,
totals, percentages, and navigation are unchanged. Charts, transaction
drill-down, custom reports, and server-side persistence are out of scope. The
design leaves the report response and page structure extensible for those later
features.

All Story 2249 display criteria are satisfied. No PRD acceptance criterion in
scope is intentionally omitted.

## Architectural Considerations

This follows the existing Clean Architecture/BFF flow and the neighboring
[simple reports design](DESIGN-SIMPLE-REPORTS.md). No new cross-cutting
component, database capability, or third-party dependency is introduced.

Display overrides are applied in `ReportsFeature`, not in a controller or in
client-only filtering, because the backend currently truncates rows at the
report definition's default depth. The frontend stores preferences only; the
backend remains stateless with respect to user display settings.

The category-depth maximum offered to users is initially four, as noted in the
PRD, but it is strictly a frontend presentation constraint. A single frontend
constant controls the Depth control's range and persisted-setting validation.
The backend accepts any positive integer depth and naturally returns no rows
beyond the category paths present in the data. Increasing the offered range to
five later requires changing only that frontend constant; controls, validation,
aggregation, and indentation do not otherwise assume a fixed maximum.

No new ADR is required. The offered maximum is presentation policy, not an API,
schema, or domain constraint.

## Design Decisions

| Decision | Alternatives considered | Rationale |
| --- | --- | --- |
| Send display overrides as optional query parameters to the existing report endpoint | Return every category level and always include monthly vectors, then filter client-side; add a second presentation endpoint | Optional parameters preserve one report request and preserve the existing backend as the authority for row truncation, subtotal flags, and response shape. Omitted parameters preserve the old contract and defaults. |
| Keep the offered maximum depth in one frontend constant, initially four | Advertise and validate a maximum in the BFF; scatter 1–4 through Vue controls; infer the maximum from rows in the current response | The range is a presentation choice, while the backend can render any positive depth. A single frontend constant permits a later increase to five without changing the API or domain model. |
| Count depth below the report filter root | Count from the absolute category root | This matches the PRD and Story 2248. `Income:Salary` is depth 1 for the Income report; `Income:Salary:Primary` is depth 2. |
| Persist overrides under a report-specific localStorage key, separate from the workspace year | Put display fields in the existing workspace report settings object | A report-specific key naturally isolates defaults and Reset. The existing `reports:settings` object remains workspace-wide and retains the year unchanged. |
| Reset removes the selected report's display-settings key | Write the definition defaults into localStorage | Removing the key means the definition remains the source of defaults, including after catalog defaults change, and satisfies “clear display settings.” |
| Re-fetch the report after a control change | Mutate returned rows and month arrays in Vue | Re-fetching uses the same authoritative Application logic for depth, subtotals, ordering, and month omission. It also handles category data deeper than the previously returned default. |
| Centralize hierarchy formatting by relative level | Scatter appearance conditions in the template; format rows based on `IsSubtotal`; special-case only two levels | One classifier function assigns semantic scoped-style classes relative to the selected depth, and one scoped CSS block owns their appearance. Either behavior or styling can then be changed in one place without coupling it to the backend or monthly display. |

## Entities

No entity changes are required. Report configuration is browser-local and the
report feature continues to read existing tenant-scoped `Transaction` and
`Split` rows.

## Data

No Data-layer, `DbContext`, data-provider, index, constraint, conversion, or
migration changes are required. Display settings are not stored on the server.

## Data Integration Tests

No Data integration tests are required because this design adds no schema or
data-access capability. Existing transaction/split tenant and migration tests
remain unchanged.

## Application Features

Update the existing
[`ReportsFeature`](../../../src/Application/Features/ReportsFeature.cs).

### Catalog metadata

Keep the existing shape of
[`ReportDefinitionDto`](../../../src/Application/Dto/ReportsDto.cs). Its
`DefaultDepth` remains definition metadata and `HasMonthColumns` continues to
mean the definition default, not the effective setting returned in a report
result.

Do not add a maximum-depth property to the catalog. The number of depths offered
is a frontend presentation concern rather than report-definition metadata.

### Report query

Change the public feature method to:

```csharp
public Task<ReportResultDto> GetReportAsync(
    string reportId,
    int? year = null,
    int? displayDepth = null,
    bool? showMonths = null);
```

XMLdoc summary: “Gets the current tenant's report data for a calendar year with
optional display overrides.”

Parameters:

| Parameter | Behavior |
| --- | --- |
| `reportId` | Stable built-in report ID. Unknown IDs throw `KeyNotFoundException`. |
| `year` | Existing calendar-year parameter. `null` uses the current UTC year; values outside 1–9999 throw `ValidationException`. |
| `displayDepth` | Number of category levels below the report filter root. `null` uses `DefaultDepth`; values below 1 throw `ValidationException` with field `displayDepth` and message “Display depth must be at least 1.” Any positive `int` is valid; the backend does not impose the frontend's offered maximum. |
| `showMonths` | `true` requests monthly vectors and month columns; `false` requests annual-only values; `null` uses `HasMonthColumns`. |

Resolve the two presentation settings only after the report definition is
found. Apply tenant and year filtering to the existing split query unchanged,
then pass the resolved `displayDepth` and `showMonths` values into report
construction.

`BuildReport` and `AppendRows` must use the effective display settings instead
of the definition defaults. Specifically:

- emit rows only through `displayDepth`;
- set `IsSubtotal` to `true` only when the row has children and
  `depth < displayDepth`;
- populate `MonthlyAmounts` on rows and totals only when `showMonths` is true;
  otherwise use `null`;
- leave filtering, roll-ups, annual totals, percentages, and sibling ordering
  independent of display overrides; amounts below the displayed depth still
  contribute to visible ancestors and the grand total.

### Result DTO

Extend `ReportResultDto` with the effective depth:

```csharp
public record ReportResultDto(
    string Id,
    string Name,
    string? CategoryRoot,
    int Year,
    int DisplayDepth,
    bool HasMonthColumns,
    IReadOnlyList<ReportRowDto> Rows,
    ReportTotalsDto Totals);
```

`DisplayDepth` is the applied value and `HasMonthColumns` reflects the applied
month setting. This makes the response self-describing and gives
`ReportsTable` the correct hierarchy context. The additional result field is
backward compatible with the previously deployed frontend, which ignores
unknown JSON properties.

No Application exceptions are new; use the existing `KeyNotFoundException` and
`ValidationException`. Tenant scoping remains the existing
`transaction.TenantId == ITenantProvider.CurrentTenant.Id` predicate on every
query.

## Application Integration Tests

Extend
[`ReportsFeatureTests`](../../../tests/Integration.Application/ReportsFeatureTests.cs)
with:

1. `GetReportAsync_AppliesDisplayOverridesWithoutChangingTenantScopedAnnualTotals`
   — “Applies effective category depth and month omission to report rows while
   preserving tenant-scoped roll-ups and the annual grand total.” Seed at least
   three category levels in the current tenant and a matching category in
   another tenant. Request display depth 3 with months hidden and then depth 1
   with months shown; verify row shape, subtotal flags, monthly-vector presence,
   tenant isolation, and unchanged annual totals.
2. `GetReportAsync_DefaultsDisplaySettingsFromDefinition`
   — “Uses each report definition's default depth and month setting when display
   query parameters are omitted.” This verifies the previous frontend contract
   remains unchanged at the Application boundary.

Validation below 1, plus non-existent report IDs, is unit-tested rather than
duplicated in the PostgreSQL fixture. Add a unit case demonstrating that a
large positive depth is accepted and only real category paths limit the emitted
rows.

## Controllers

Update
[`ReportsController.GetReport`](../../../src/Controllers/ReportsController.cs).
Keep the action thin and add the nullable query parameters to the delegated
feature call.

| Method | Route | Tenant role | Request body | Success response | Error responses |
| --- | --- | --- | --- | --- | --- |
| GET | `/api/tenant/{tenantKey:guid}/reports/{reportId}?year={year?}&displayDepth={int?}&showMonths={bool?}` | Viewer or above | None | `200 ReportResultDto` | `400` for an invalid calendar year, invalid display depth, or model-binding failure; `404` for an unknown report; `401`/`403` from existing auth; `500` for an unhandled failure. All errors use `ProblemDetails`. |

The action XMLdoc summary is “Gets the selected built-in report for a calendar
year with optional display overrides.” Document `reportId`, `year`,
`displayDepth`, and `showMonths`, plus the existing `KeyNotFoundException` and
`ValidationException` conditions. Add no new logger event: the existing
`LogStartingReport` and `LogOkReport` `[LoggerMessage]` partial methods already
cover the boundary. Do not put display-depth or month values in logs.

Both optional query parameters are backward compatible. Existing calls that
omit them continue to receive definition defaults. A previously deployed
frontend can ignore the added `displayDepth` response property.

Regenerate the generated TypeScript client with
[`Generate-ApiClient.ps1`](../../../scripts/Generate-ApiClient.ps1). The frontend
will call `ReportsClient.getReport(reportId, year, displayDepth, showMonths,
tenantKey)` after regeneration. Do not edit
[`apiclient.ts`](../../../src/FrontEnd.Nuxt/app/utils/apiclient.ts) directly.

## Front End

### Preference store

Update
[`userPreferences.ts`](../../../src/FrontEnd.Nuxt/app/stores/userPreferences.ts).
Rename the current `ReportSettings` interface to `WorkspaceReportSettings` so its
scope is explicit; it still contains optional `year`. Add:

```ts
export interface ReportDisplaySettings {
  displayDepth?: number
  showMonths?: boolean
}
```

Use the following helpers:

```ts
function getReportDisplaySettings(
  tenantKey: string,
  reportId: string,
): ReportDisplaySettings

function updateReportDisplaySettings(
  tenantKey: string,
  reportId: string,
  settings: ReportDisplaySettings,
): void

function resetReportDisplaySettings(tenantKey: string, reportId: string): void
```

The key is exactly:

```text
userPreferences:tenant:{tenantKey}:reports:{reportId}:display
```

The stored JSON contains only recognized fields. Validate that
`displayDepth`, when present, is an integer greater than zero and that
`showMonths`, when present, is a boolean. Diagnose malformed JSON or an invalid
object through the existing store logging convention and return `{}`. A valid
depth greater than the frontend `MAX_REPORT_DISPLAY_DEPTH` constant is treated
as obsolete on page load and the definition default is used. Updates merge with
existing valid settings. Reset removes only this report-specific key.

Keep the workspace settings key
`userPreferences:tenant:{tenantKey}:reports:settings` and its `year` behavior
unchanged. Legacy per-report objects that happen to contain `year` are ignored;
the display reader recognizes only `displayDepth` and `showMonths`.

Read, write, and remove failures must propagate to the page so a persistence
failure is shown rather than claiming the preference changed.

### Frontend depth policy

Add `reportDisplay.ts` under the existing `src/FrontEnd.Nuxt/app/utils/` folder
with JSDoc summary: “Defines the depth choices offered by report display
controls.” Export one constant and one predicate:

```ts
export const MAX_REPORT_DISPLAY_DEPTH = 4
export function isSupportedReportDisplayDepth(value: number): boolean
```

The predicate accepts integers from 1 through `MAX_REPORT_DISPLAY_DEPTH`.
Only this constant determines the range currently offered to the user and the
maximum restored from localStorage. Changing it to five enables five-level UI
choices without a backend, DTO, OpenAPI, or database change. Unit tests verify
both the current boundaries and that the predicate rejects non-integers and
zero.

### Report display controls

Add `ReportDisplayControls.vue` under the existing `src/FrontEnd.Nuxt/app/components/reports/`
folder with JSDoc summary: “Edits the category-depth and month-display settings
for one report.”

Props:

| Prop | Type | Meaning |
| --- | --- | --- |
| `definition` | `ReportDefinitionDto` | Current report metadata, including default depth and default month display. |
| `displayDepth` | `number` | Effective selected depth. |
| `showMonths` | `boolean` | Effective month-column setting. |
| `disabled` | `boolean` (default `false`) | Disables all controls while a report request is pending. |

Emits:

| Event | Payload | Meaning |
| --- | --- | --- |
| `update:displayDepth` | `number` | User selected a depth. |
| `update:showMonths` | `boolean` | User selected monthly or annual presentation. |
| `reset` | none | User requested definition defaults. |

Render a native **Depth** select with integer options from 1 through
`MAX_REPORT_DISPLAY_DEPTH`; use the selected value, not zero-based numbering.
The component imports that shared frontend constant rather than accepting a
maximum-depth prop. Render a native **Display** select with “Annual only”
(`false`) and “Months and total” (`true`). Render a **Reset** button. The
controls remain visible while a report request is loading, disabled so rapid
conflicting requests are avoided.

### Reports page

Update [`reports.vue`](../../../src/FrontEnd.Nuxt/app/pages/reports.vue):

1. On report selection, resolve `displayDepth` and `showMonths` from that
   report's display-settings key.
2. Use the frontend `isSupportedReportDisplayDepth` predicate plus
   `definition.defaultDepth` and `definition.hasMonthColumns` to validate and
   fill each missing/obsolete setting. Do not reuse another report's override.
3. Pass the resolved depth and month setting with year to the regenerated
   `getReport` client method, including on initial load, Retry, and every
   report/year/display change.
4. On a depth or display change, persist the merged per-report setting before
   requesting the report. A successful user change is therefore saved even if
   the subsequent API request fails.
5. On Reset, remove the selected report's display-settings key, restore the
   definition defaults, and request the report with those defaults. Read the
   selected workspace year from its existing state and pass it unchanged.
6. Keep the existing latest-request-wins version guard. Late responses after a
   depth, month, report, year, workspace, or unmount event must not update
   report content or settings.
7. Clear old report content while loading so a table with the old display mode
   is not shown beside new control values.
8. Surface storage read/write/remove failures with `handleApiError` and the
   existing `ErrorDisplay`, using title `Persistence Failed` and detail `Failed
   to save report preferences` or `Failed to read report preferences` as
   appropriate. A failed reset leaves the old control values.

Discovery failures, invalid catalog metadata, report-request failures, and
Retry behavior remain as designed by Story 2248 and the year scenario. A report
request failure retries the currently selected year and current effective
display settings without re-fetching catalog metadata.

### Report table

Update [`Table.vue`](../../../src/FrontEnd.Nuxt/app/components/reports/Table.vue).
Remove the caller-supplied `displayDepth` prop and read the effective
`report.displayDepth`. The component JSDoc summary remains “Displays an annual
report with optional monthly columns, category hierarchy, and totals.”

Month headers, cells, footer cells, and the empty-state colspan continue to use
`report.hasMonthColumns`, which now reflects the user override. Category names
and indent remain based on `row.depth` and `row.categoryName`.

Hierarchy formatting is centralized in one place in `Table.vue` rather than
expressed through scattered Bootstrap or inline-style conditions. Add a local
function with the following contract:

```ts
function getRowHeaderLevelClass(depth: number, displayDepth: number): string
```

It calculates the row's distance from the deepest displayed level and returns
exactly one of these semantic scoped-style classes:

- `report-header-level-4` — three or more levels above the deepest level;
- `report-header-level-3` — two levels above the deepest level;
- `report-header-level-2` — one level above the deepest level;
- `report-header-level-1` — the deepest displayed level.

The scoped `<style scoped>` block is the single owner of the visual mapping for
those four semantic classes. Initially, levels 4 through 1 map respectively to
strong highlight, light highlight, bold, and normal text. Thus one-level
reports are plain; two-level reports have bold level-one rows as today; and a
four-level report has strong level one, highlighted level two, bold level three,
and plain level four.

Changing color, weight, background, or other visual treatment for a category
level requires editing only its corresponding scoped rule. If the semantic
classification must change, edit only `getRowHeaderLevelClass`. Avoid putting
hierarchy appearance in templates, utility-wide Bootstrap classes, or individual
row metadata. Indentation remains calculated separately from `row.depth`.
`IsSubtotal` and monthly visibility do not control formatting.

### Test IDs

| data-test-id | Element | Purpose |
| --- | --- | --- |
| `report-selector` | Existing Report select | Existing report choice; retained. |
| `report-year-selector` | Existing Year select | Existing workspace-wide year; retained and unchanged by Reset. |
| `report-display-controls` | Controls container | Locates the display controls. |
| `report-depth-selector` | Depth select | Selects category levels and verifies restoration. |
| `report-period-selector` | Display select | Chooses annual-only or months plus total. |
| `report-reset` | Reset button | Restores definition defaults without changing year. |
| `report-result` | Loaded report region | Locates the current report result. |
| `report-table` | Report table | Verifies the effective display mode. |
| `report-row` | Category row | Verifies included depth and hierarchy. |
| `report-grand-total` | Grand-total amount | Verifies totals do not change with presentation. |

## Component Tests

Extend focused frontend tests in
[`reports.test.ts`](../../../src/FrontEnd.Nuxt/tests/reports.test.ts), and add a
behavior contract for the new `ReportDisplayControls.vue` in the same file or a
dedicated `report-display-controls.test.ts` as follows:

| Test | Required behavior |
| --- | --- |
| `emits the selected depth and month display` | Controls generate 1 through the shared frontend `MAX_REPORT_DISPLAY_DEPTH`, emit numeric depth and boolean month settings, and disable all actions while `disabled` is true. |
| `validates frontend depth boundaries` | The report-display utility accepts the configured range and rejects zero, non-integers, and values above the frontend maximum. |
| `passes effective display settings to the generated client` | Page calls `getReport` with report ID, year, resolved depth, resolved month flag, and tenant key. |
| `restores per-report settings independently` | A depth/month override for one report is not used for another; reselecting and remounting restore the original report's settings. |
| `uses definition defaults for missing or obsolete settings` | Missing settings, malformed settings, zero depth, and depth above the frontend maximum recover to definition defaults. |
| `auto-saves each display change before loading` | Updating either control merges the field into the existing per-report key without writing the workspace-year key. |
| `resets only one report without changing year` | Reset removes the current key, restores the catalog defaults, leaves other report keys and `year` intact, and re-requests the current report/year. |
| `shows persistence failures` | Read, write, and remove failures use `Persistence Failed`; a failed Reset does not alter displayed settings. |
| `uses the current response hierarchy and period` | Table receives effective `displayDepth` and `hasMonthColumns`, including annual-only responses with `monthlyAmounts` absent. |
| `classifies hierarchy styling relative to display depth` | Rows receive the expected semantic scoped-style class from the classifier for one-, two-, and four-level configurations; appearance is not conditional on `IsSubtotal` or month visibility. |
| `ignores stale display requests` | Rapid depth/month/report/workspace changes and unmount ignore older success or failure responses. |

Prefer direct component/store tests for control contracts and functional tests
for page interaction. Do not assert Bootstrap class names or exact layout.

## Functional Tests

Add the following prioritized Gherkin to
[`Reports.feature`](../../../tests/Functional/Features/Reports.feature). Keep each
new `When` before all `Then` assertions. Reuse the existing selection, reload,
category, total, and period steps; mark new actions/assertions `# NEW`.

```gherkin
@pri:1
@category:scenario-2249
Scenario: Configure period display and retain the workspace year
  Given I have a workspace with the following transactions:
    | Date       | Payee   | Amount | Category       |
    | 2024-01-15 | Employer | 125.00 | Income:Salary |
    | 2024-02-15 | Grocery  | -40.00 | Food:Groceries |
  When user visits the "Reports" page
  And I select report year 2024
  And I select the "Income Detail" report
  And I select annual-only report display # NEW
  Then the report year should be 2024
  And the report should show annual amounts
  And the report grand total should be 125.00

@pri:1
@category:scenario-2249
Scenario: Configure category depth and preserve hidden roll-ups
  Given I have a workspace with the following transactions:
    | Date       | Payee  | Amount | Category             |
    | 2024-03-15 | Grocer | -40.00 | Home:Utilities:Power |
    | 2024-04-15 | Bank   | -20.00 | Home:Banking:Checking |
  When user visits the "Reports" page
  And I select the "Expenses Detail" report
  And I select report depth 1 # NEW
  Then the report should include the category Home
  And the report should not include the category Utilities # NEW
  And the report grand total should be -60.00

@pri:1
@category:scenario-2249
Scenario: Restore report-specific display settings after reload
  Given I have a workspace with a transaction:
    | Date       | Payee  | Amount | Category             |
    | 2024-03-15 | Grocer | -40.00 | Home:Utilities:Power |
  When user visits the "Reports" page
  And I select the "Expenses Detail" report
  And I select report year 2024
  And I select report depth 3 # NEW
  And I select annual-only report display # NEW
  And I reload the reports page
  Then the selected report should be "Expenses Detail"
  And the report depth should be 3 # NEW
  And the report period display should be annual only # NEW
  And the report year should be 2024
  And the report should include the category Power

@pri:2
@category:scenario-2249
Scenario: Reset only the selected report display settings
  Given I have a workspace with the following transactions:
    | Date       | Payee   | Amount | Category             |
    | 2024-01-15 | Employer | 125.00 | Income:Salary:Primary |
    | 2024-02-15 | Grocer   | -40.00 | Home:Utilities:Power  |
  When user visits the "Reports" page
  And I select the "Expenses Detail" report
  And I select report year 2024
  And I select report depth 3 # NEW
  And I select monthly report display # NEW
  And I reset report display settings # NEW
  Then the report depth should be 2 # NEW
  And the report period display should be months and total # NEW
  And the report year should be 2024
  And the report should include the category Utilities
  And the report grand total should be -40.00
```

The final scenario deliberately selects values equal to the Expenses Detail
month default but a non-default depth; Reset must still remove the persisted
object rather than only visibly restoring values. A lower-layer store test
verifies the key removal. Functional implementation must follow
[`tests/Functional/README.md`](../../../tests/Functional/README.md) and add page
methods for the listed test IDs without introducing implementation-specific
steps.

## Open Questions

- None. The depth range is initially 1–4, centrally advertised by the backend,
and depth is measured below the report filter root, as confirmed during design.

## Completion Checklist

- [ ] Every Story 2249 display acceptance criterion is traceable to a section above.
- [ ] All layers state whether and how they change.
- [ ] The endpoint documents tenant role, request parameters, response, and every error category.
- [ ] No entity or data schema change is required; no migration or Data integration test is proposed.
- [ ] Existing omitted display parameters and old frontend clients remain supported.
- [ ] Category totals and tenant isolation remain independent of presentation settings.
- [ ] Every index, constraint, conversion, and data-provider change is tested; none is introduced.
- [ ] Every new UI interaction and material result has a stable `data-test-id`.
- [ ] Gherkin has no `When` step after a `Then` step and stays within five scenarios.
- [ ] New functional step definitions are marked `# NEW` in the design.
- [ ] No `TBD`, “to be finalized,” or equivalent placeholder is present.
- [ ] Open Questions is empty of blockers.
- [ ] All relative links resolve to existing files.
