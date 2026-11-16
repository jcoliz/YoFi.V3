---
status: Draft (Revised)
target_release: Beta 2
ado: Feature 2208
---

# Product Requirements: Reports Export API

## Problem and goal

Users who analyze their finances in desktop Excel need to refresh YoFi report
data without manually copying it. Enable them to connect Power Query to the
existing built-in reports using a secure, user-scoped, read-only API key and a
provided query or workbook template.

## Scope and non-goals

In scope:

- Create, list, and revoke named keys in the signed-in user's settings.
- Export the same built-in calendar-year report data as the web UI, for every
  workspace the key owner can view.
- Provide an Excel Power Query template that turns the existing report response
  into a usable table and retains access to report totals and metadata.
- Support Excel 2016+ and Microsoft 365 desktop, subject to the credential and
  template feasibility checks in the [design](DESIGN-EXPORT-API.md).

Out of scope:

- Transaction CRUD, write access, or access to non-report API endpoints using
  API keys.
- API-key access to workspace discovery; users obtain workspace keys from the
  existing Workspaces UI.
- API-key access to the existing report catalog and year-discovery endpoints.
- Arbitrary date ranges, alternate report definitions or layouts, CSV/PDF
  export, real-time streaming, Excel Web, and non-Excel clients.
- A flat report response or a breaking change to the existing reports API.

## User stories and acceptance criteria

### Connect and refresh reports

**As an** Excel user, **I want** to load and refresh a YoFi report in Power
Query, **so that** I can use its data in my financial analysis.

- [ ] A user can securely configure the supplied Power Query template using a
  server URL, workspace key, built-in report ID, and calendar year. Excel's
  Basic authentication uses the YoFi username and the API key as its password,
  never the user's account password.
- [ ] The template turns the current report's nested rows into a typed table.
  Detail reports expose January through December in order; non-detail reports,
  empty reports, and null percentages work without misleading zero values.
- [ ] Report metadata and authoritative totals are accessible separately.
  Users are warned that subtotal rows overlap descendants and must not simply
  be summed to calculate a grand total.
- [ ] The connection can be refreshed after closing and reopening Excel without
  re-creating the key. Document any verified version-specific limitations.
- [ ] Basic authentication and its credential storage are validated in
  supported desktop Excel before release. Secrets must not be put into URLs or
  embedded in a workbook or query intended for sharing.
- [ ] Each report uses the same definitions, values, and calendar-year behavior
  as the web UI. The year defaults to the current UTC year if omitted; no
  arbitrary `fromDate`/`toDate` filtering is promised.

### Access multiple workspaces

**As a** user with multiple workspaces, **I want** to use one key for each
workspace I can view, **so that** I can maintain separate Excel queries without
managing multiple credentials.

- [ ] One key can access reports in any workspace where its owner currently has
  Viewer, Editor, or Owner access.
- [ ] The user copies workspace keys from the Workspaces UI and can configure a
  query for each workspace. The key cannot access workspace-discovery endpoints.
- [ ] An inaccessible workspace returns 403. Membership removal or a role
  change is enforced on the next API-key request.

### Manage keys

**As an** authenticated user, **I want** to manage my own named keys, **so that**
I can revoke access if a credential is exposed.

- [ ] Any authenticated user can create a key, regardless of workspace role.
- [ ] The full key is shown once at creation and never returned by list or
  subsequent requests.
- [ ] Users can list their own keys with name, creation time, status, and
  last-used time, and can revoke their own keys. They cannot manage another
  user's keys.
- [ ] A revoked key fails with 401 on its next request. Keys do not expire
  automatically in the initial release.
- [ ] Key management requires normal signed-in authentication and is not
  accessible using an API key.

### Discover reports

**As an** Excel user, **I want** to discover built-in reports and years,
**so that** I can choose a supported query.

- [ ] The setup guide lists built-in report IDs and names for configuring the
  template. Users can identify reports and available years in the signed-in
  Reports UI; report IDs are not currently displayed there.
- [ ] Report catalog and year-discovery endpoints do not accept API keys.
  Unknown report IDs and invalid years return clear errors from export.

## Data and security requirements

- Reuse the existing report response: a metadata object with nested `rows` and
  `totals`, and optional monthly arrays. Power Query performs the tabular
  transformation; a root-level flat JSON response is not required.
- Preserve signed numeric values, nullable percentages, category paths,
  hierarchy and subtotal semantics. The authoritative total is not the sum of
  all displayed rows.
- API keys grant access only to the read-only export endpoint, and never expand
  the owner's current workspace permissions. Normal web
  authentication and report behavior must continue to work.
- Send Basic credentials only over HTTPS; Base64 encoding is not encryption.
  Never log or place secrets in URLs. Validate how Excel stores credentials.
  Reject keys paired with a username other than the key owner's.
- Enforce both 100 requests per minute and 1,000 per hour per API key. Return a
  clear rate-limit response. Deployment-wide enforcement needs appropriate
  shared state if the service runs on multiple instances.
- Use standard error responses consistent with the existing API. Avoid exposing
  financial data, key material, or personal information in diagnostic logs.

## Success measures

- 10% of active users generate API keys within three months; track active
  monthly API users and workspaces accessed.
- Report response time under two seconds at P95; error rate below 1% excluding
  user errors. Typical one-year queries should complete within five seconds.
- No security incidents related to compromised keys; Excel integration support
  tickets under 5% of all tickets.
- Desktop Excel users can follow the documented setup and refresh a query
  without manually reshaping the response.

## Dependencies and open decisions

The built-in reports and year-discovery endpoints are implemented. The export
action must reuse the existing report feature and response, not the discovery
endpoints. Current aggregation and the known parity gap apply equally to
exported data; see [PRD-REPORTS.md](PRD-REPORTS.md).

Basic authentication with a username and API key is the chosen approach, but
its behavior and credential storage must still be verified in desktop Excel.
The exact key-management UI/API, audit retention, last-used update strategy,
and deployment-wide rate-limit storage remain implementation decisions.
The [detailed design](DESIGN-EXPORT-API.md) records the feasibility gate and
test plan.
