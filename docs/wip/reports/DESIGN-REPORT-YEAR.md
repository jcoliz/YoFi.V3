---
status: Approved
target_release: V3.0
ado: Scenario 2228
parent: Story 2249 User - Configures report display
prd: PRD-REPORTS.md
---

# Design: Select Report Year

## Scope

Add a workspace-wide year-discovery endpoint and a year selector to the existing
Reports page. Reuse the report endpoint's existing `year` parameter; report
aggregation and response formatting do not change.

Persist the year per report and workspace in browser localStorage. Start a
report-settings object that can later hold the rest of Story 2249's settings,
without implementing depth, month-display, chart controls, or Reset now.

## Year availability and default

The backend owns these rules:

- Available years are the distinct calendar years of the current workspace's
  transaction dates, plus the current UTC year, sorted newest first.
- Always include the current UTC year, even in an empty workspace. It is the
  default year, not necessarily the first item: future-dated transactions may
  produce later years.
- Do not fill gaps between years with data. Do include future years with data.
- Availability is workspace-wide, not filtered by report or category. Changing
  reports must not change the year list. A year may legitimately have no data
  for the selected report.
- Use committed transactions, not import-review records. Transaction dates
  determine the calendar year, matching existing report aggregation.

The frontend restores a saved year when it is still in the available list;
otherwise it uses the backend's default. It must not calculate the current year,
add years, or infer a default from list order.

## API and Application

Add `GET /api/tenant/{tenantKey:guid}/reports/years` to
[`ReportsController`](../../../src/Controllers/ReportsController.cs).
Require the existing Viewer role, which also permits Editors and Owners.

Return HTTP 200 with a `ReportYearsDto` containing:

```json
{
  "years": [2026, 2024, 2022],
  "defaultYear": 2026
}
```

Both properties are required. `years` is a nonempty array of unique integer
calendar years in descending order, and always contains `defaultYear`.
An empty workspace returns only the current year, not an empty response or 404.

The action delegates to a new `GetAvailableYearsAsync` method on
[`ReportsFeature`](../../../src/Application/Features/ReportsFeature.cs):

1. Capture the current UTC year once from the already-injected `TimeProvider`.
2. Query transactions through `IDataProvider`, filtering by
   `ITenantProvider.CurrentTenant.Id` before projecting distinct date years.
3. Execute that scalar query with `ToListAsync`, then add the current year,
   deduplicate, and sort the small result in Application code.
4. Return the years and the captured current year in `ReportYearsDto`, added to
   [`ReportsDto.cs`](../../../src/Application/Dto/ReportsDto.cs).

Use the scalar-capable `ToListAsync`; the provider's `ToListNoTrackingAsync`
requires reference types. Do not materialize full transactions or load splits
just to discover years. No entity, migration, new data-access interface, or cache
is needed. Application and Data remain logging-free.

Keep the controller thin, document the response for OpenAPI, and reuse its
`[LoggerMessage]` start/count boundary logging. Authorization and failures use
the existing middleware and Problem Details conventions; a query failure must
not return a success response containing only the current year.

The literal `years` route takes precedence over the existing `{reportId}` route;
reserve that identifier from the report catalog, just as `available` is reserved.
The existing `GET .../reports/{reportId}?year={year}` contract is unchanged:
omitting the year still defaults to the current UTC year, and explicit valid
calendar years remain accepted even if absent from the discovery list.

## Frontend behavior

Update [`reports.vue`](../../../src/FrontEnd.Nuxt/app/pages/reports.vue):

1. On workspace load, clear the previous report and year state. Fetch report
   definitions and year availability in parallel, once per workspace load.
2. Restore the selected report using the existing behavior. Resolve its year
   from its saved settings, falling back to `defaultYear`.
3. Render a labeled native **Year** select beside the existing **Report** select
   on wide screens and stacked on narrow screens. Use numeric values and
   `data-test-id="report-year-selector"`. Keep it visible for an empty workspace's
   single current-year option.
4. Persist the resolved settings and load the report with an explicit year:
   the generated client's existing `getReport(id, year, tenantKey)` call.
5. On a year change, save immediately and reload the selected report. On a
   report change, restore that report's own saved year, or the backend default;
   do not carry the previous report's year into an unconfigured report.

Keep the existing report heading, which already includes the returned year,
and the table's no-data state. Clear old report data while reloading so it is
never shown under a newly selected year.

Preserve the latest-request-wins protection for workspace, report, and now year
changes, including errors and loading state. Stale responses must not update
selections, settings, or rendered data after a workspace change or unmount.
Changing only the report or year does not refetch the catalog or year list.

Discovery failures use the page's existing error display and Retry action;
do not manufacture a client-side year list. Retry discovery for initialization
failures, but retry the same selected report/year for report-load failures.
Treat a malformed discovery response as an explicit load error.

## Persistence

Extend
[`userPreferences.ts`](../../../src/FrontEnd.Nuxt/app/stores/userPreferences.ts)
with a typed `ReportSettings` object, initially containing only optional `year`,
and report-settings read/update helpers.

Use one JSON object per workspace/report:

```text
userPreferences:tenant:{tenantKey}:reports:{reportId}:settings
```

Example value:

```json
{ "year": 2024 }
```

Keep the existing `reports:selectedId` preference unchanged. No migration is
needed because there are no existing saved report settings.

- Persist automatically on selection and when resolving an absent or obsolete
  saved year to the backend default. Save independently of report-request success.
- Validate the stored object and require an integer year present in the current
  discovery list. Missing settings use the default normally. Obsolete years
  also use the default, for example after transactions have been deleted.
- Malformed stored settings must be diagnosed using the existing preference-store
  logging convention before recovering to the default. Storage-access/write
  failures must surface a persistence error; do not claim the setting was saved.
- Merge year updates into the settings object rather than replacing it with a
  year-only object. Later Story 2249 work can add fields using the same key and
  helpers without losing other settings. Do not add speculative fields now.
- A future Reset will remove this report's settings key and resolve defaults
  again; implementing that control remains outside this scenario.

## Implementation and verification

1. Add the Application DTO/query and thin controller action.
2. Regenerate the API client with
   [`Generate-ApiClient.ps1`](../../../scripts/Generate-ApiClient.ps1).
3. Add preference helpers and wire discovery, restoration, and selection into
   the existing page. The table component needs no changes.
4. Add focused coverage:

| Layer | Required coverage |
| --- | --- |
| Application unit | Fixed `TimeProvider`; current-year inclusion/default; no duplicates; descending order with future years; gaps preserved; empty workspace. |
| Application integration | Real data-provider translation of distinct date years; tenant isolation; January 1/December 31 boundaries; multiple transactions in a year. |
| Controller integration | Viewer access; unauthenticated/unauthorized access; exact response shape; literal `/years` route reaches discovery rather than report lookup. |
| Frontend/preferences | Explicit year passed to report call; backend default used even when not first or different from browser year; selector changes results; per-report/per-workspace restoration after remount; obsolete/malformed settings; settings merge; storage failure visibility. |
| Frontend async/error | No workspace means no requests; discovery/report errors and retry; empty report for a valid year; stale discovery/report responses after year/report/workspace changes and unmount. |
| Functional | Seed distinct totals in two years, select a historical year, and reload; verify selected year and report totals are restored. Keep all `When` actions before `Then` assertions. |

Extend the existing report test files rather than introducing a new test harness.
For functional coverage, follow the
[`Functional README`](../../../tests/Functional/README.md) and its linked authoring
guides. Run against the containerized app, not Aspire; leave app containers running.

This design adds one small metadata request per workspace load. It leaves report
query performance, aggregation, authorization, catalog order, and existing
selected-report persistence unchanged.
