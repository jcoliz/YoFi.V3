---
status: Draft
related_docs:
  - PRD-TENANT-DATA-ADMIN.md
  - DESIGN-TENANT-DATA-ADMIN.md
  - DESIGN-DATA-IMPORT-PAYEES.md
  - DESIGN-MANAGE-WORKSPACE.md
ado: Story 2220, Scenario 2278; Story 2221, Scenario 2279
---

# Design: Native Payee Export and Import

## Overview

This design combines **Scenario 2278 (export native payees)** and
**Scenario 2279 (import native payees)** into one delivery, tracked as Scenario
2279. The export produces the native YoFi.V3 workbook that the import accepts,
so both directions share one documented worksheet schema and one format
identifier.

The feature operates on V3 payee matching rules, the application's native
representation of payees. It is an Owner-only action in the Data Administration
section of [Manage Workspace](DESIGN-MANAGE-WORKSPACE.md). Import uses the
shared tenant-data import preview and confirmation flow; it does not replace
the separate original YoFi Payee import described in
[Import Original YoFi Payees](DESIGN-DATA-IMPORT-PAYEES.md).

### In Scope

- Export all payee matching rules for the current workspace to a native XLSX
  workbook.
- Import a native workbook's payee matching rules after preview and explicit
  confirmation.
- Support Key-based update/create behavior, preserving unrelated existing rules.
- Validate the complete supported worksheet before making any changes.
- Keep export and import tenant-scoped and Owner-authorized.

### Out of Scope

- Exporting or importing transactions, splits, budgets, or other data types.
- Importing original YoFi payees (covered by Scenario 2233).
- Export date/year filtering; payee rules are not date-based.
- Deleting rules omitted from an import workbook.
- Importing tenant ownership, internal database IDs, audit state, or usage state.
- Changing bank import, payee matching behavior, or the payee-rule management UI.

## Product Decisions

| Area | Decision |
|------|----------|
| Workspace UI | Data Administration on Manage Workspace |
| Export type | Payee Matching Rules only in this delivery |
| Export selection | One selectable `Payee Matching Rules` type; no unsupported entity types are shown as available |
| Export scope | All rules in the current workspace, with no date filter |
| Native format ID | `YoFi.V3`, identified by the reserved `Metadata` worksheet |
| Data worksheet | `PayeeMatchingRules` |
| Writable fields | `Key`, `PayeePattern`, `PayeeIsRegex`, and `Category` |
| Managed fields | Exported for reference but ignored on import: `CreatedAt`, `ModifiedAt`, `LastUsedAt`, and `MatchCount` |
| Tenant/internal fields | Never exported or accepted from a workbook |
| Import behavior | Existing same-workspace Key updates; absent or unknown Key creates with a new Key |
| Missing records | Rules omitted from the workbook remain unchanged |
| Transaction boundary | The complete payee worksheet is atomic |
| Re-import | Re-importing an unchanged exported workbook updates its matching rules; it does not duplicate them |

## Native Workbook Contract

### Metadata Worksheet

Every native export includes a `Metadata` worksheet. It has `Name` and `Value`
headers in columns A and B, and includes a `Format` / `YoFi.V3` pair. This
identifies the workbook format; it is not schema-version metadata. Do not add a
schema version as part of this scenario.

The native format detector treats a valid marker as authoritative. A malformed
or unrecognized marker is not a native workbook. If no marker is present, the
existing original YoFi detector may recognize its own supported signatures;
otherwise the workbook is unknown and must not be guessed as native based only
on a worksheet name.

### Payee Worksheet

The `PayeeMatchingRules` worksheet contains one row per rule. The exporter
writes columns in this stable order:

| Column | XLSX representation | Import behavior |
|--------|---------------------|-----------------|
| `Key` | GUID in canonical `D` format, stored as text | Match an existing rule in the current workspace; blank means create |
| `PayeePattern` | Text | Required; editable |
| `PayeeIsRegex` | Boolean | Required; editable |
| `Category` | Text | Required; editable and sanitized/validated by the existing rule validator |
| `CreatedAt` | UTC ISO-8601 round-trip text, or blank | Ignored; retained on update and set by the server on create |
| `ModifiedAt` | UTC ISO-8601 round-trip text | Ignored; set by the server on update and create |
| `LastUsedAt` | UTC ISO-8601 round-trip text, or blank | Ignored; retained on update and initialized to null on create |
| `MatchCount` | Integer | Ignored; retained on update and initialized to zero on create |

These are the public rule data fields, not every persistence column. Internal
numeric IDs and tenant IDs are excluded. Export includes managed fields for
backup readability, but only the four explicitly writable fields affect an
import.

The exporter writes a header row even when the workspace has no rules. Import
requires exactly one `PayeePattern`, `PayeeIsRegex`, and `Category` header.
`Key` is optional so a user can add a new row manually; if present, duplicate
headers are invalid. Header comparison is case-insensitive and trims surrounding
whitespace. Extra columns are ignored and reported in preview. Duplicate
non-empty Keys, malformed GUID Keys, formula cells in supported columns, and
invalid field values block the whole worksheet before persistence. A blank
`Key` denotes a new rule; a nonblank malformed Key is not treated as blank.

The workbook may contain other sheets. They are listed as ignored in preview
and never implicitly imported. A native workbook without a supported
`PayeeMatchingRules` sheet has no importable data and cannot be confirmed.

## User Flow

### Export

1. An Owner opens Manage Workspace for the intended workspace.
2. In Data Administration, the Owner selects `Payee Matching Rules` and
   activates Export. With only one supported type in this delivery, the
   selection UI remains consistent with the broader type-selection design
   without presenting unsupported types.
3. The server queries all rules scoped to the authorized workspace, writes the
   `Metadata` and `PayeeMatchingRules` sheets, and returns an XLSX attachment.
4. The filename is `yofi-payee-matching-rules-{date}.xlsx`.
5. On success, the page shows a confirmation with the exported record count.
   On failure, it shows an explicit error and does not report a successful
   download.

### Import

1. An Owner selects or drops an `.xlsx` file in the import section.
2. The page submits it to the shared preview endpoint. Preview identifies the
   source format, reports the payee-rule count, ignored sheets/columns, and
   row/field validation issues, and returns the file digest.
3. The user reviews the preview. Native payee import is unavailable if the
   workbook is not recognized as native or has no supported payee worksheet,
   or if any validation error exists. An original YoFi workbook continues
   through its separately supported handler.
4. Before confirmation, explain that matching Keys update rules in this
   workspace, blank or unknown Keys create new rules, omitted rules are
   unchanged, and managed audit/usage values from the workbook are ignored.
5. The user explicitly confirms Import. The server checks the preview digest,
   reopens and revalidates the exact workbook, then applies the worksheet
   atomically.
6. On success, show created and updated counts, refresh the workspace's rule
   count/list, and invalidate cached matching rules. No unsupported worksheet
   is imported.
7. On validation or persistence failure, show an explicit failure and provide
   the shared CSV error report when row-level issues are available. Do not
   automatically retry.

Changing the selected file clears its preview. Workspace switching is blocked
while an import is pending so the operation cannot be retargeted.

## API and Application Boundaries

Use the existing tenant administration route prefix:
`/api/tenant/{tenantKey:guid}/admin`. All operations require the existing
Owner authorization and tenant middleware. The authorized route context is the
only source of workspace identity.

| Method | Route | Responsibility |
|--------|-------|----------------|
| `GET` | `/export?types=PayeeMatchingRules` | Return the native XLSX attachment for the selected supported type |
| `POST` | `/import/preview` | Detect format and validate the uploaded workbook without writing |
| `POST` | `/import` | Verify the preview digest, revalidate, and apply the confirmed import |

The export endpoint returns the XLSX content type and a safe `Content-Disposition`
filename. It queries only the current workspace and returns all matching rules.
The endpoint should compose with the future shared export selection contract;
this delivery need not implement other entity exporters or date filters.

Import registers a native format detector and a `PayeeMatchingRules`
worksheet handler with the existing `TenantDataImportFeature`. Keep mapping,
validation, and worksheet-level issues in that handler. Use typed application
services for persistence; do not bind workbook columns to an entity or use
reflection-based entity updates.

The native handler maps supported values to validated payee-rule inputs and
Keys. The persistence path resolves each non-empty Key only within the current
tenant. A Key found in this tenant updates that rule's pattern, regex flag, and
sanitized category. A Key not found in this tenant creates a new rule with a
server-generated Key; do not reveal whether the supplied Key belongs to another
tenant. Blank Keys also create new rules. Updates preserve `CreatedAt`,
`MatchCount`, and `LastUsedAt`; creates initialize audit and usage state on the
server. Every successful write sets `ModifiedAt` from the server clock.

Validate all rows, including duplicate Keys, before staging writes. Persist the
worksheet in one transaction. If a database failure occurs, roll back all its
changes and do not return success-shaped counts. Invalidate the current
tenant's payee-rule cache only after commit.

## Validation, Results, and Logging

- Parse XLSX cell values only; formulas are not evaluated.
- Reject unreadable or corrupt workbooks with the standard request-level
  ProblemDetails response.
- Run `PayeeMatchingRuleEditDtoValidator` for `PayeePattern`, regex behavior,
  and `Category`; validate `PayeeIsRegex` as a boolean cell/value.
- Do not allow workbook values to set tenant ownership, internal IDs, or
  managed timestamps and usage counters.
- A row-level validation error blocks the complete payee worksheet. Skipped
  or unknown worksheets do not become writes.
- Return counts that distinguish created from updated records. Report no
  partial success for a rolled-back worksheet.
- Offer the shared CSV error report with worksheet, one-based row, field,
  error code, and user-readable message. Do not include unrelated workbook
  contents.
- Use the repository's `[LoggerMessage]`-attributed logging pattern at the API
  boundary. Log actor/workspace, operation, outcome, and counts only; never log
  payee patterns, categories, file contents, or row values.
- Apply the existing upload size limit and safe XLSX parsing policy; do not
  broaden them in this scenario.

## Acceptance Tests

### Export

- Owner can export payee rules from Manage Workspace; non-Owners cannot use
  the endpoint.
- Export contains the native `Metadata` marker and a `PayeeMatchingRules`
  worksheet with the documented stable columns.
- Export contains every rule from the selected workspace and no rules from a
  different workspace.
- Empty workspaces export a valid workbook with the worksheet headers and
  report a count of zero.
- GUID Keys, booleans, categories, and managed fields are represented as
  documented; internal numeric IDs and tenant IDs are absent.
- The response uses the documented filename and XLSX content type; a failed
  export is surfaced rather than shown as success.

### Import

- A native export is recognized and previews the supported worksheet.
- A valid native row with a Key belonging to the current workspace updates
  only its editable fields and preserves its created/usage state.
- A blank Key or a well-formed Key not found in the current workspace creates
  a rule with a fresh server-generated Key.
- Re-importing an unchanged export updates matching rules and creates no
  duplicates.
- Rules omitted from a workbook remain unchanged.
- Imported audit and usage values cannot overwrite server-managed state.
- Missing/duplicate required headers, duplicate Keys, malformed nonblank Keys,
  invalid values, and formulas in supported cells block all writes.
- Unknown worksheets/columns are reported and never imported.
- Missing, malformed, or unsupported native format metadata is not guessed as
  a native workbook; an original YoFi workbook continues through its existing
  separate handler.
- A workbook modified after preview is rejected and must be previewed again.
- Database failure leaves no worksheet changes and does not invalidate the
  cache or report success; a committed import invalidates the tenant cache.
- Cross-tenant Keys cannot read or modify another tenant's rules.

## Implementation Notes

- Extend the existing shared import pipeline and result contracts rather than
  adding a payee-specific controller or alternate preview flow.
- Implement export as a typed projection and workbook writer. Keep export
  selection extensible at the tenant administration boundary without adding
  entity reflection or speculative abstractions.
- Preserve the original YoFi handler's mapping and create-only behavior; native
  Key-based updates are specific to the native format.
- Add focused unit and integration coverage for detector behavior, workbook
  mapping, tenant-scoped upsert, rollback, cache invalidation, export shape,
  and Owner authorization.
