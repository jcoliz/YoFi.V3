---
status: Approved
related_docs:
  - PRD-TENANT-DATA-ADMIN.md
  - DESIGN-TENANT-DATA-ADMIN.md
  - DESIGN-MANAGE-WORKSPACE.md
ado: Story 2221, Scenario 2233
---

# Design: Import Original YoFi Payees

## Scope

This design covers only **Story 2221, Scenario 2233**: importing payee data
from an original YoFi XLSX export into one YoFi.V3 workspace. The initial
implementation recognizes the legacy `Payee` worksheet and imports its rows as
V3 payee matching rules.

The import is an explicit action on the Owner-only [Manage Workspace page](DESIGN-MANAGE-WORKSPACE.md).
It is not a bank import and does not create transactions or use the bank-import
review workflow.

### In Scope

- Upload, detect, preview, validate, and import the original YoFi `Payee` sheet.
- Map legacy payee names and categories to V3 payee matching rules.
- Apply changes only after the user reviews the preview and confirms.
- Report skipped and invalid rows, including a downloadable CSV error report.
- Keep all reads and writes scoped to the current authorized workspace.

### Out of Scope

- Importing transactions, splits, budgets, or any other worksheet.
- Native YoFi.V3 workbook imports or export behavior.
- Export, templates, sample data, deletion, and changes to bank import.
- Updating, replacing, or deduplicating existing payee matching rules.

## Product Decisions

| Area | Decision |
|------|----------|
| Recognized data | Original YoFi `Payee` worksheet only |
| V3 target | Payee matching rules; V3 has no separate payee entity |
| Legacy `ID` | Ignore; do not expose or persist it |
| Legacy `Selected` | Ignore |
| Blank `Category` cell | Skip the row and report it as skipped |
| Re-import / existing rules | Create each valid row as a new rule; do not update or deduplicate |
| Import review | Preview and validate first; apply directly only after confirmation |
| Persistence boundary | Atomic for the Payee worksheet |
| Authorization | Workspace Owner, enforced by the existing tenant administration API |

Original YoFi records have no V3 `Key`. Each imported rule therefore receives a
fresh V3 Key. Importing the same workbook again creates additional rules; this
is intentional and must be made clear in the confirmation UI.

## Legacy Workbook Mapping

The legacy format is described in the [tenant data administration PRD](PRD-TENANT-DATA-ADMIN.md#original-yofi-export-format).
Recognize the worksheet named `Payee`. The import does not require the workbook
to contain any other particular worksheets. Other worksheets are ignored and
must be identified as not imported in the preview; they must never be treated
as implicit consent to import other data types.

| Original YoFi column | V3 mapping | Handling |
|----------------------|------------|----------|
| `Name` | `PayeePattern` and `PayeeIsRegex` | Required. If the trimmed name starts and ends with `/`, remove that outer pair and set `PayeeIsRegex = true`; otherwise use the name as a substring pattern and set `PayeeIsRegex = false`. Validate the resulting pattern with the existing matching-rule validator. |
| `Category` | `Category` | Required column. Blank cell means skip the row and report it. Nonblank values follow V3 category sanitization and validation. |
| `ID` | — | Ignore; never use as a V3 Key or database identifier. |
| `Selected` | — | Ignore. |

Header matching should tolerate case differences and surrounding whitespace.
`Name` and `Category` must each appear exactly once. A missing or duplicate
required header makes the worksheet invalid. Extra columns are ignored and
reported in preview; they are not imported.

Rows with no meaningful `Name` are invalid. Rows with a blank `Category` are
skipped, even if they have a name, because V3 rules require a category. The
preview reports sanitized/validated rule counts, skipped blank-category rows,
and row-level validation errors separately. A value that becomes empty after
category sanitization is invalid rather than silently skipped.

## User Flow

1. An Owner opens Manage Workspace for the target workspace and chooses the
   import action.
2. The user selects or drops an `.xlsx` file.
3. The server detects the `Payee` worksheet and returns a preview containing:
   - whether a Payee worksheet was found;
   - valid rule count, skipped row count, and validation-error count;
   - a concise summary of ignored worksheets and columns;
   - row/field-specific validation messages.
4. The user reviews the preview. The action is unavailable if no Payee
   worksheet is present or if any row has a validation error. Skipped
   blank-category rows are shown and do not by themselves block importing
   the other valid rows.
5. Before confirmation, explain that the import **adds** rules, does not alter
   existing rules, and will create duplicates if the same workbook is imported
   again. The user explicitly chooses **Import Payees**.
6. On success, show created, skipped, updated, and failed counts. `Updated` is
   always zero for this legacy format. Refresh the workspace's payee-rule list
   and invalidate any cached rule data.
7. On validation failure, show specific messages and offer a CSV error report.
   On persistence or transport failure, show an explicit failure; do not show
   a success-shaped summary or automatically retry.

The interface retains the selected file through preview and confirmation.
Changing the file clears the old preview and requires a new one.

## API and Application Boundaries

Use the existing tenant administration route prefix:
`/api/tenant/{tenantKey:guid}/admin`.

| Method | Route | Responsibility |
|--------|-------|----------------|
| `POST` | `/import/preview` | Parse and validate the uploaded workbook without writing |
| `POST` | `/import` | Re-parse and validate the confirmed workbook, then create rules |

Both operations require the existing tenant Owner authorization and tenant
middleware. Accept the workbook as a multipart upload. The route-established
tenant context is authoritative; never accept tenant IDs from workbook content.

The preview response is informational and is not trusted as an import command.
Return a SHA-256 digest of the uploaded file with the preview. The UI submits
the same file and digest to the import endpoint, which compares the digest and
then re-parses and revalidates the workbook before persistence. Reject a digest
mismatch and require a new preview; this binds user confirmation to the file
they reviewed without storing an uploaded workbook between requests.

Keep HTTP DTOs explicit. The import application flow should map the supported
legacy columns into `PayeeMatchingRuleEditDto`, validate using
`PayeeMatchingRuleEditDtoValidator`, and use the established create-only rule
construction path so imported rules receive a fresh Key, authorized tenant,
sanitized category, and initialized audit/usage fields. Do not copy legacy IDs
or allow workbook fields to set tenant ownership, match counts, timestamps, or
other V3 state.

Implement this as the original YoFi format detector plus a `Payee` worksheet
handler registered with the shared `TenantDataImportFeature`, not as a
payee-specific top-level import service. The preview and result use the generic
per-worksheet import contract defined in
[Tenant Data Administration](DESIGN-TENANT-DATA-ADMIN.md#import-architecture).
The handler owns only this legacy worksheet's mapping and validation; typed
rule creation remains in the shared persistence service.

After a successful commit, invalidate the current tenant's payee matching-rule
cache. A database failure must roll back the entire Payee worksheet. Never
report a partial success if the transaction did not commit.

## Validation and Error Reporting

Validate the file as an XLSX workbook; do not evaluate cell formulas. Reject
unreadable, corrupt, or unsupported workbooks with a clear request-level
ProblemDetails response. Production upload-size and large-workbook limits are
not finalized in the parent design and remain a cross-cutting design task.

When the Payee sheet is present:

- Require exactly one `Name` and one `Category` header.
- Ignore `ID`, `Selected`, and unrecognized columns.
- Skip rows whose Category cell is blank and include a row number and reason
  in the preview summary.
- Validate the name-derived pattern and the sanitized category with the V3
  payee rule validator.
- Treat any non-skipped invalid row as a worksheet validation failure; do not
  persist any rows from that worksheet.

The downloadable CSV error report should contain worksheet name, one-based
spreadsheet row number, field, error code, and user-readable message. Do not
include unrelated workbook data in the report. Logs may include actor,
workspace, operation, outcome, and counts, but must not include payee names,
categories, file contents, or other imported row values.

## Security and Reliability

- Enforce tenant isolation and Owner authorization at both preview and import.
- Use the authorized tenant context for every created rule.
- Do not log workbook contents or row values.
- Do not execute formulas or use legacy `ID` values as update keys.
- Revalidate on import and persist the Payee worksheet atomically.
- On cancellation or persistence failure, report failure explicitly and do not
  automatically retry.
- Ensure unrelated imported worksheets cannot create or modify any data.

## Acceptance Tests

- Original YoFi workbook with valid Payee rows is detected and previewed.
- A workbook without a `Payee` worksheet is not importable.
- Other worksheets are reported as ignored and do not create data.
- Header matching handles case and surrounding whitespace; missing or duplicate
  `Name`/`Category` headers block import.
- Ordinary names become substring rules; names wrapped in `/.../` become
  validated regex rules with only the outer slashes removed.
- Legacy `ID` and `Selected` values do not affect created V3 records.
- Blank-category rows are skipped and counted; valid rows in that workbook can
  still be imported.
- Invalid names or categories block the worksheet write, provide row-level
  errors, and produce a downloadable CSV report.
- A file that differs from the previewed file is rejected and must be previewed
  again before import.
- Re-importing the same workbook creates new rules without updating or
  deduplicating existing rules.
- A failed database transaction leaves no imported rules and does not report
  success; a successful import invalidates the tenant rule cache.
- A non-Owner or a request targeting another tenant cannot preview or import
  workspace data.

## Open Implementation Details

- Confirm the XLSX library and safe workbook parsing limits when implementing.
- Finalize upload size limits and the exact ProblemDetails / import response
  DTOs alongside the broader tenant-data import contract.
