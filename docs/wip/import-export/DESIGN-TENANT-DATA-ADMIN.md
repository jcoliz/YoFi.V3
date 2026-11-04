---
status: Draft
related_docs:
  - PRD-TENANT-DATA-ADMIN.md
  - DESIGN-SAMPLE-DATA.md
  - DESIGN-MANAGE-WORKSPACE.md
---

# Design: Tenant Data Administration

## Overview

Skeleton design for general workspace data administration. This document sketches
controller ownership and proposed endpoints; it is not an implementation plan or
a finalized request/response contract.

Requirements come from [Tenant Data Administration](PRD-TENANT-DATA-ADMIN.md).
Implement needed pieces directly in their intended final form, without an
intermediate developer-only YAML API. The simple administration PRD is
superseded and is not a dependency of this design.

Retained concepts are captured here: a separate general administration controller,
Owner-only tenant-scoped access, shared per-type counts refreshed after mutations,
GUID Key-based updates that leave unrelated records unchanged, and explicit
import failure reporting. YAML routes, developer-only navigation, per-type
deletion, unrestricted uploads, and permissive validation are not carried forward.

## Controller and Application Boundaries

| Component | Responsibility |
|-----------|----------------|
| `TenantDataAdminController` | Counts, export, import, and deletion of existing tenant data |
| `SampleDataController` | Bundled scenario discovery, preview, append-only sample loading, and sample OFX download |
| `TenantDataAdminFeature` | General administration orchestration across supported entity types |
| `SampleDataFeature` | Sample generation and loading orchestration |

Use the common route prefix `/api/tenant/{tenantKey:guid}/admin`.
Keep existing sample routes under `/sample-data` unchanged. Do not put general
administration actions in `SampleDataController` or add a combined
delete-and-load endpoint.

The [Manage Workspace page](DESIGN-MANAGE-WORKSPACE.md) composes these operations
into one administration experience. The sample-data UI may call general counts
and deletion endpoints without making those operations sample-data concerns.
Share tenant-scoped persistence/deletion helpers where needed, rather than
making general administration depend on sample generation.

Bank OFX/QFX upload and review remain in the existing `ImportController`, outside
this administration API.

## Proposed Endpoints

All suffixes below are relative to `/api/tenant/{tenantKey:guid}/admin`.
All endpoints require the existing tenant Owner authorization policy and tenant
middleware. Resolve the target workspace from the route-established tenant
context, not from tenant identifiers in uploaded records.

### Counts

| Method | Suffix | Purpose | Result sketch |
|--------|--------|---------|---------------|
| GET | (root) | List supported data types and current unfiltered counts | Per-type identifier and record count |

Return counts for the supported tenant entity types, initially transactions and
payee matching rules. Final type identifiers and treatment of dependent splits
remain to be designed. Use this same operation for administration display and
refreshed deletion confirmation counts in the sample-data workflow.

### Complete Data Deletion

| Method | Suffix | Purpose | Result sketch |
|--------|--------|---------|---------------|
| DELETE | `/data` | Delete all workspace data, preserving the workspace and memberships | Committed per-type deletion counts |

This route replaces the temporary development/testing reset described in the
[sample-data design](DESIGN-SAMPLE-DATA.md#proposed-api). Ship the production
contract and safeguards below together; do not simply remove an environment
check from the temporary contract.

Delete in dependency-safe order and atomically within the deletion operation.
Never delete just the sample entity types when the request is to clear all
workspace data. Sample loading remains a separate operation and commit.

## Story 2222: Delete All Workspace Data

### Confirmed Product Decisions

| Area | Decision |
|------|----------|
| Entry | Standalone danger zone on Manage Workspace |
| Access | Workspace Owner, in all environments |
| Scope | All workspace data, including transactions, splits, payee matching rules, and pending bank-import reviews |
| Preserved | Workspace identity/settings, all memberships, and user accounts |
| Confirmation | Enter the target workspace's name; display it with a copy button and permit paste |
| Export dependency | Deletion may ship before complete export; add the export action when available |
| Success | Stay on Manage Workspace; retain the selected workspace, show committed deleted counts, refresh counts, and expose the sample-data action |
| Sample loading | Remove the temporary reset choice; deletion and sample loading are independent actions |

This operation clears workspace data. It does not delete the user's account or
promise erasure from backups, operational logs, or other retention systems.
Do not present it as complete account-level GDPR erasure.

### Confirmation and Request Contract

Show the target workspace name, refreshed per-type counts, and an irreversible
deletion warning. Explicitly state that the workspace and memberships remain.
Provide **Cancel** and **Delete All Workspace Data** actions.

Place an accessible copy button next to the displayed workspace name. Copying
must not populate the input or submit the dialog automatically; the user may
paste the copied name. Report clipboard failure visibly and allow manual entry.
Enable submission only when the entered name matches the displayed name.

The proposed request includes `confirmationWorkspaceName`; the server must
validate it against the current name of the route-targeted workspace, in addition
to enforcing Owner authorization. A stale or incorrect name must not authorize
deletion. Final matching rules and the error contract remain technical design
work. This name is a confirmation safeguard, not an authorization credential.

The backend compares the supplied name exactly, including case and whitespace,
against the workspace name established in the current tenant context before
starting the deletion transaction. A mismatch returns validation ProblemDetails
(HTTP 400) without deleting data. A rename racing after this check does not cancel
deletion. Success returns committed counts for transactions, splits, payee matching
rules, and pending import-review transactions.

Advise exporting a backup before deletion. Until complete export exists, do not
show a dead button or imply a backup was created; explain that deletion cannot
be undone. Export is optional, not a prerequisite or an automatic side effect.

### Execution and Outcome

Disable repeated submission and prevent workspace switching from retargeting a
pending deletion. Delete all supported workspace data in one database transaction
and return actual committed per-type deletion counts, not the preflight counts.
Log the operation and its outcome without logging financial records.

On success, close the dialog, show the deleted counts, refresh administration
counts, and invalidate stale workspace-data views. Stay on Manage Workspace with
the same workspace selected and sample loading available. Do not automatically
load sample data or navigate to Transactions.

On failure, show an explicit error and do not claim the workspace is cleared.
If the outcome is uncertain because the connection was lost, refresh counts
before allowing another destructive attempt; do not automatically retry.

### Temporary Reset Transition

When Story 2222 ships, remove the reset choice and destructive confirmation from
the sample-loading wizard in every environment. Use the standalone production
danger-zone action instead. Sample loading remains append-only and never grants
permission to delete. Remove the temporary confirmation contract rather than
retaining a development-only bypass of the production safeguards.

The equally destructive or more destructive workspace deletion action uses the
same workspace-name entry and copy-button UX, with its own distinct warning and
outcome; see [Manage Workspace](DESIGN-MANAGE-WORKSPACE.md).

## XLSX Import and Export

These are proposed final-form endpoints; implementation may be staged by story.

| Method | Suffix | Purpose | Result sketch |
|--------|--------|---------|---------------|
| GET | `/export` | Export selected types with optional year/date filtering; support an explicit all-data selection | XLSX attachment with one worksheet per type |
| POST | `/import/preview` | Detect worksheets and validate an uploaded XLSX file without writing | Detected types, record counts, and validation feedback |
| POST | `/import` | Apply an explicitly confirmed XLSX import using Key-based create/update rules | Per-type created/updated/failed counts and detailed errors |

Transactions include their splits on export and preserve their relationship on
import. An all-data export must bypass date filtering.

Imports use GUID Key matching: update matching records in the current tenant,
otherwise insert with a new Key, leaving unrelated records unchanged. Validate
before persistence and report per-type created/updated/failed counts with
worksheet, row, and field errors. The full PRD permits partial success; final
HTTP status rules and persistence boundaries remain to be designed rather than
inherited from the superseded developer workflow. Do not expose internal numeric
database IDs or evaluate spreadsheet formulas.

## Cross-Cutting Requirements

- Enforce tenant isolation and Owner authorization on every action.
- Use explicit request/response DTOs and the generated frontend API client.
- Return standard ProblemDetails for request and operation failures; never
  disguise failures as empty data or successful deletion.
- Log actor/workspace, operation, affected types, committed counts, and outcome
  using existing logging conventions, without logging financial records.
- Refresh counts after imports and deletions. Counts shown before confirmation
  are informational, not a guarantee of how many records will be deleted.
- Preserve transaction split invariants and account for dependent data,
  including pending bank-import reviews, during deletion.

## Design Work Remaining

- Finalize DTOs, confirmation payloads, and response/error status contracts.
- Finalize workspace-name matching and concurrent rename/write behavior for deletion.
- Define safe export/import fields and dependency ordering for each supported type.
- Specify upload validation, persistence boundaries, and duplicate-Key behavior.
- Define XLSX selection/filter parameters and how preview relates to confirmed
  import without trusting client-supplied preview results.
- Define year/filter metadata and downloadable import-error report contracts.
- Resolve production file limits and large-operation behavior.
- Detail controller/application tests for Owner-only access, tenant isolation,
  partial imports, dependency-safe deletion, and unchanged sample routes.
- Cover deletion confirmation mismatch, clipboard failure, preserved memberships,
  staying on Manage Workspace, and removal of the temporary sample-reset flow.
