---
status: In Review (Revised)
target_release: Beta 2
ado: Feature 2208
prd: PRD-EXPORT-API.md
---

# Design: Reports Export API

## Overview

Enable desktop Excel users to refresh YoFi's existing built-in reports using
user-scoped, read-only API keys and a provided Power Query template. The normal
reports controller and report aggregation already exist. Add one API-key-only
export action to that controller; do not introduce a second report engine, a
flat-data response, or change the web UI's existing endpoints.

The [export PRD](PRD-EXPORT-API.md) defines the product requirements. This
revised design replaces the earlier proposal for `/spending/by-category`,
arbitrary date ranges, and root-level flat JSON.

## Existing implementation

[`ReportsController`](../../../src/Controllers/ReportsController.cs) has:

| Method and route | Existing response | Purpose |
| --- | --- | --- |
| `GET /api/tenant/{tenantKey}/reports/available` | Array of `ReportDefinitionDto` | Built-in catalog |
| `GET /api/tenant/{tenantKey}/reports/years` | `ReportYearsDto` object | Workspace years and default year |
| `GET /api/tenant/{tenantKey}/reports/{reportId}?year=2026` | `ReportResultDto` object | One report for a calendar year |

These are already protected by the Viewer tenant-role policy. `year` is
optional and defaults to the current UTC year; the supported range is 1 through
9999. Unknown report IDs return 404, and invalid years return 400. Discovery
returns IDs, names, and display settings, not descriptions or example URLs.
The [DTO definitions](../../../src/Application/Dto/ReportsDto.cs) and
[`ReportsFeature`](../../../src/Application/Features/ReportsFeature.cs) are
the source of truth.

Report results have `id`, `name`, `categoryRoot`, `year`, `hasMonthColumns`,
`rows`, and `totals`. Each row includes `categoryPath`, `categoryName`, `depth`,
`isSubtotal`, `annualTotal`, and `percentage`. Detail reports also include a
12-element `monthlyAmounts` array, January first; it is omitted when month
columns are hidden. The same applies to monthly totals. Percentage is a signed
fraction of the report total, and is null when that total is zero.

Rows mirror the built-in report, not individual transactions. Parent subtotal
rows include descendants, so summing every row can double-count. Use the
separately computed `totals` object for the report total. Direct amounts
assigned to a parent category do not get a separate child row; see the known
parity gap in [PRD-REPORTS.md](PRD-REPORTS.md). Additionally, amounts assigned
directly to a report's category root are included in totals but have no
displayed row. Export must not claim to correct either behavior.

For example, a non-detail response can look like:

```json
{
  "id": "income",
  "name": "Income",
  "categoryRoot": "Income",
  "year": 2026,
  "hasMonthColumns": false,
  "rows": [
    {
      "categoryPath": "Income:Salary",
      "categoryName": "Salary",
      "depth": 1,
      "isSubtotal": false,
      "annualTotal": 1000,
      "percentage": 1
    }
  ],
  "totals": { "annualTotal": 1000 }
}
```

## Excel experience

1. A signed-in user creates a named API key in user settings and copies it
   immediately; it is shown in full only once.
2. The user copies the workspace key from the existing Workspaces UI and
   chooses a report ID from the supplied setup guide and a calendar year.
   The signed-in Reports UI can help identify reports and available years;
   it does not currently display the report IDs. Neither report nor workspace
   discovery is accessible using an API key.
3. The user configures the supplied Power Query template with the server URL,
   workspace key, report ID, and year. In Excel's Basic authentication dialog,
   they enter their YoFi username and the API key as the password, not their
   YoFi account password.
4. Excel loads a table and refreshes it using the export endpoint.
   The same key can be used in a separate query for another accessible workspace.

Power Query can consume nested JSON; Microsoft's [JSON connector
documentation](https://learn.microsoft.com/en-us/power-query/connectors/json)
describes flattening, and
[`Table.FromRecords`](https://learn.microsoft.com/en-us/powerquery-m/table-fromrecords)
converts a list of records into a table. Automatic detection is not a guarantee
of the desired layout. The provided query must explicitly:

- Extract `rows` and convert it to a table with a stable schema, including for
  an empty list.
- Keep category path, category name, depth, subtotal flag, signed annual total,
  and nullable percentage, with suitable types. Format percentage as a fraction,
  not a value already multiplied by 100.
- For reports with months, map the 12 array positions to January through
  December, preserving numeric types. Handle omitted monthly fields for
  reports without months; do not treat absent values as zero. Specify and test
  the resulting column schema consistently in the template.
- Preserve the report's row order and subtotal flags. Explain why rows must not
  simply be summed. Offer a separate way to load the authoritative totals and
  metadata without inserting a fake row into the data table.
- Refresh the same query, including empty reports and zero-total percentages,
  without silently suppressing errors or changing its schema.

An illustrative template is a deliverable, not yet an implemented or validated
workbook. Test it against actual Excel desktop versions and the live API before
claiming a no-code connection, automatic typing, or unattended refresh.

## Authentication and authorization

### Credential and lifecycle

- Any authenticated user can create, list, and revoke their own named keys.
  Keys belong to users, not workspaces. Viewer, Editor, and Owner roles govern
  report access independently in each workspace.
- Store only a cryptographic hash of a randomly generated, high-entropy secret;
  return the full secret once on creation. Never put secrets in URLs, logs, or
  example workbooks. Use TLS. Allow manual revocation without an expiry in
  Phase 1; a revoked key fails the next request.
- Store a non-secret identifier, name, owner, creation time, status, and last-used
  time for management. If a display prefix is used, it must not reveal enough
  secret material to weaken the key. Restrict key management to normal signed-in
  authentication; API keys cannot manage keys.
- Last-used tracking must not use a fire-and-forget task with request-scoped
  dependencies. Choose a safe, observable update strategy and document its
  performance and failure semantics during implementation.

### Components to create

The following names and responsibilities are the intended implementation, not
existing classes. Use HTTP Basic authentication with the username and API key
for Excel, subject to the validation described below.

| Component | Layer | Responsibility |
| --- | --- | --- |
| `ApiKeyAuthenticationHandler` | Controllers authentication | ASP.NET Core authentication handler for the `ApiKey` scheme. Extract Basic credentials, authenticate the key and its owner, obtain current tenant claims, and construct one authenticated principal. |
| `ApiKeyAuthenticationOptions` | Controllers authentication | Scheme options for the handler, including any Basic challenge configuration. |
| `ApiKeyFeature` | Application features | Use `IDataProvider` to create, list, revoke, and validate keys. Generate secrets securely, hash and look up presented secrets, enforce key status and ownership, and return a non-secret key ID and owner ID for a valid key. |
| `ApiKey` | Entities models | Persisted key record with owner ID, secret hash, scope, status, timestamps, and non-secret management data. |
| `ApiKeyScope` | Entities models | Identify the reports-only scope. Check this scope before accepting a key for report access. |
| `ApiKeysController` | Controllers | Expose signed-in-user key management, delegate to `ApiKeyFeature`, and return the secret only in the creation response. Never accept API-key authentication on these actions. |
| `CreateApiKeyDto`, `ApiKeyResultDto`, `ApiKeyCreatedDto` | Application DTOs | Define the create request, secret-free list result, and one-time creation result, respectively. |

Use the existing ASP.NET Core Identity `UserManager<IdentityUser>` in
`ApiKeyAuthenticationHandler` to load the owner by ID after key validation.
Reject a missing or ineligible owner. Determine and test what account
disablement means in this application; do not assume that `IdentityUser` has a
generic disabled flag. Use the existing
[`IClaimsEnricher`](../../../src/Controllers/Tenancy/Authorization/IClaimsEnricher.cs)
and [`TenantClaimsEnricher`](../../../src/Controllers/Tenancy/Authorization/TenantClaimsEnricher.cs)
to obtain current tenant-role claims for that owner. They query the tenant
repository and already produce the format required by the role handler.

### Export action and authentication

Register the handler as a named `ApiKey` scheme without changing global
authentication defaults. Registration belongs in backend setup after the
existing Identity/NuxtIdentity registration:

```csharp
services.AddAuthentication()
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
        "ApiKey", _ => { });
```

The existing three report actions remain unchanged and use their existing
normal web authentication. Add **one** action to `ReportsController`:

```csharp
[HttpGet("{reportId}/export")]
[Authorize(AuthenticationSchemes = "ApiKey")]
[RequireTenantRole(TenantRole.Viewer)]
public async Task<IActionResult> ExportReport(
    string reportId,
    [FromQuery] int? year = null)
{
    LogStartingReport(reportId);
    var result = await reportsFeature.GetReportAsync(reportId, year);
    LogOkReport(reportId, result.Rows.Count);
    return Ok(result);
}
```

The new route is `GET /api/tenant/{tenantKey}/reports/{reportId}/export?year=2026`.
It returns the same `ReportResultDto`, with the same optional year default,
validation, 400/404 errors, and report semantics as the existing report action.
Add equivalent response metadata, logging, and tests. The action has no
discovery or special data transformation logic.

`[Authorize]` selects only the `ApiKey` scheme and adds an authenticated-user
requirement; `[RequireTenantRole]` adds the Viewer requirement. Neither normal
JWTs nor cookies can authorize this endpoint. API keys do not authorize any of
the existing report actions. Verify this with real-handler tests because
ASP.NET Core combines authorization metadata and may run other schemes if
they are added to the resulting policy. The handler returns no result for
missing or non-Basic credentials and fails malformed, invalid, or revoked
Basic credentials, including duplicate or combined authorization headers.
Provide the appropriate 401 challenge and 403 forbid behavior. Preserve the
authorization-before-tenancy middleware order.

### API-key request flow

1. `ApiKeyAuthenticationHandler` parses a Basic authorization
   header as username and API-key password. Missing credentials produce no
   authentication result; malformed or invalid credentials fail authentication.
   `ApiKeyFeature` hashes the presented API key, looks it up using
   `IDataProvider`, and checks that it is active and has the reports-only
   scope. It returns the key ID and owner ID, never the stored hash or secret.
2. The handler uses `UserManager<IdentityUser>` to retrieve the key's owner and
   checks account eligibility. Compare the supplied username against that owner's
   username using the application's Identity username normalization rules;
   reject a mismatch. Do not authenticate an account password here. It asks
   `IClaimsEnricher` for the owner's current tenant-role claims and builds a
   single principal containing those claims, `ClaimTypes.NameIdentifier`, and
   non-secret key ID and scope claims. Membership is loaded on every request,
   not when the key is created.
3. Authorization executes the existing Viewer policy. `TenantRoleHandler`
   compares the URL's `tenantKey` with this principal's `tenant_role` claims.
   It does not look up the user, key, or memberships. On success it records
   the tenant key and role in `HttpContext.Items`.
4. `TenantContextMiddleware` uses the authorized tenant key to establish the
   tenant context; `ReportsController` then calls the existing `ReportsFeature`.
   A failed credential is 401; a valid owner without the required role is 403.

The key record and account checks are distinct from membership lookup: owning
an active key identifies a user but does not confer access to any workspace.
Data access errors must not be interpreted as valid credentials or an empty
membership list; fail the request observably without exposing secrets.

### API-key entity and database schema

Create `ApiKey` as an Entities model derived from
[`BaseModel`](../../../src/Entities/Models/BaseModel.cs). It is user-scoped:
there is no tenant ID, and the key's owner gains access only through current
workspace memberships. The proposed `ApiKeys` table has these fields:

| Field | Type | Constraints and purpose |
| --- | --- | --- |
| `Id` | `int` | Internal primary key, inherited from `BaseModel`. |
| `Key` | `Guid` | Required, unique, non-secret public record identifier, inherited from `BaseModel`. Used for management and diagnostics; it is **not** the API secret. |
| `UserId` | `string` | Required foreign key to ASP.NET Identity's user ID; use the same representation as existing `UserTenantRoleAssignment.UserId`. |
| `Name` | `string` | Required user-facing label; reject whitespace-only names and limit to 128 characters. |
| `KeyHash` | `string` | Required, unique 64-character lowercase hexadecimal SHA-256 digest of the complete secret. Never store the secret itself. |
| `Scope` | `ApiKeyScope` | Required; `ReadOnlyReports = 1` is the only supported value initially. Reject unknown values rather than granting access. |
| `CreatedAt` | `DateTimeOffset` | Required UTC creation timestamp. |
| `LastUsedAt` | `DateTimeOffset?` | Nullable UTC timestamp; null until usage is recorded. Update strategy and freshness are operational decisions below. |
| `IsActive` | `bool` | Required; true at creation and false after revocation. |

Generate each secret with a cryptographically secure random number generator:
32 random bytes encoded as lowercase hexadecimal, preceded by the fixed
`yofi_user_` marker. Hash the entire UTF-8 encoded secret including the marker
with SHA-256. These randomly generated keys have sufficient entropy for a fast
digest; do not use this scheme for user-chosen passwords. Validate the marker,
length, and encoding before lookup. Handle a unique-hash collision by generating
a new secret, never by overwriting an existing key.

Do not store or display any part of the random secret as a prefix. The fixed
marker is the same for every key and cannot distinguish them. Use the
non-secret `Key` identifier and the user-provided name in the management UI.
The full secret is returned only at creation; list and revoke responses must
not include `KeyHash` or the secret.

Configure these constraints in **both** the SQLite and PostgreSQL data contexts
and generate their normal provider-specific migrations:

- Unique indexes on `Key` and `KeyHash`, including revoked keys; an index on
  `UserId` supports owner-scoped listing and management. Validate that hash
  comparison and uniqueness behave consistently across both providers.
- Required owner foreign key to `AspNetUsers.Id`, with cascade deletion when
  the owner is deleted. This removes the user's key records as well; soft
  revocation preserves records only while the owner exists.
- Require the appropriate non-null fields and configure the name and hash
  lengths. Enforce input and enum validation in the application as well as
  applicable database constraints; do not rely on provider-specific length
  enforcement alone.
- Revocation sets `IsActive` to false rather than deleting the record.
  Validation reads current status on each request; no cache may delay the
  next-request revocation guarantee.

`LastUsedAt` is informational, not the authority for access or revocation.
Key creation and revocation must be persisted before returning success. Add
integration tests for both database providers covering schema constraints,
owner-scoped access, key hash lookup, revocation, and owner deletion.

### Excel Basic authentication: validation gate

Use the [Power Query Web connector's Basic
authentication](https://learn.microsoft.com/en-us/power-query/connectors/web/web),
with the YoFi username as the username and the personal API key as the password.
The key is a credential, not the user's YoFi account password. Basic encodes
credentials but does not encrypt them; require HTTPS end-to-end and do not
expose a Basic challenge or accept a credential over plain HTTP. Never record
the authorization header, username and key pair, or key in URLs or logs.

Do not use the Web API option for this design: Microsoft's
[`Web.Contents` documentation](https://learn.microsoft.com/en-us/powerquery-m/web-contents)
describes `ApiKeyName` as sending the key in a URL query parameter, contrary to
the no-secrets-in-URLs requirement.

Validate the chosen flow in supported desktop Excel versions: create the query,
set Basic credentials, refresh after reopening, rotate/revoke the key, and
inspect where credentials are stored and how their data-source scope applies to
multiple workspaces. Confirm the secret is not embedded in a shareable
workbook or M query. Record platform/version results and limitations. The
user's experience with another service supports the approach, but does not
verify YoFi's implementation. If the tests fail, revisit the approach rather
than silently weakening secret handling.

### Endpoint and tenant access

- Preserve normal web authentication on the existing report actions. API keys
  authenticate only the new read-only export action, with an explicit
  reports-only scope. The existing catalog, years, and report actions,
  `GET /api/tenant`, key management, writes, and other endpoints do not accept
  API keys.
- Do not set the API key scheme as a global default or replace existing normal
  authentication. Apply the API-key scheme to the export action only and
  verify normal JWT and API-key requests through the real pipeline.
- The existing
  [`TenantRoleHandler`](../../../src/Controllers/Tenancy/Authorization/TenantRoleHandler.cs)
  checks `tenant_role` claims of the form `{tenantKey}:{role}` and sets the
  validated context; it does **not** look up membership using the user ID.
  The API key principal carries the authenticated owner's current tenant-role
  claims, obtained through `IClaimsEnricher` on each request. Setting only
  `NameIdentifier` is insufficient. Do not copy stale claims at key creation.
  Preserve normal JWT behavior and the authorization-before-tenancy pipeline.
- An invalid, missing, or revoked key must be rejected with 401; an authenticated
  key whose owner lacks Viewer access to the requested workspace must receive
  403. Removing or changing the owner's membership affects the next key request.
  Avoid allowing one identity's key and another identity's role claims to combine.
- User and key revocation/disablement checks must use current authoritative
  state. Any cache must preserve the next-request revocation guarantees.

## API-key management and persistence

Add a user-level key-management API and UI following the existing controller,
feature, data-provider, and identity patterns. Validate names and impose
reasonable key-count bounds. Persist schema changes via the project's normal
migrations, rather than a database-specific SQL sketch.

`ApiKeyFeature` queries `ApiKey` records through `IDataProvider`, filtering by
owner for management operations and by hash for authentication. It adds and
updates records and saves changes using the existing provider pattern. Register
the entity in both data contexts; no dedicated key repository is needed.

### `ApiKeysController` contract

The proposed controller route is `api/user/apikeys`, as in the earlier design.
It is **not yet implemented**. Every action requires normal signed-in
authentication, never Basic API-key authentication. It obtains the user ID from
the authenticated principal, not the request body or URL. Any authenticated
user may manage their own keys; no workspace or workspace-owner role is
required. The controller delegates to `ApiKeyFeature`, which applies the owner
filter to every operation.

| Method and route | Action | Success |
| --- | --- | --- |
| `GET /api/user/apikeys` | List the current user's keys, including revoked keys. | `200 OK`, array of `ApiKeyResultDto` |
| `POST /api/user/apikeys` | Create a named, reports-only key for the current user. | `201 Created`, `ApiKeyCreatedDto` with the one-time secret |
| `DELETE /api/user/apikeys/{key:guid}` | Revoke the current user's key identified by its non-secret public `Key`. | `204 No Content` |

#### List keys

`GET /api/user/apikeys` returns an empty array when the user has no keys. Each
`ApiKeyResultDto` contains `key`, `name`, `scope`, `createdAt`, nullable
`lastUsedAt`, and `isActive`. `key` is the record's public GUID, not the secret.
The response contains neither the secret nor `keyHash`. Including inactive keys
lets the UI show their revoked status.

```json
[
  {
    "key": "4b89ea80-621c-44c8-9d6f-76b0a95efcc7",
    "name": "Excel - Home Computer",
    "scope": 1,
    "createdAt": "2026-01-15T10:30:00Z",
    "lastUsedAt": null,
    "isActive": true
  }
]
```

#### Create a key

`POST /api/user/apikeys` accepts `CreateApiKeyDto` with a required `name`.
Reject null, whitespace-only, and over-128-character names with `400` and the
API's normal validation response. The initial scope is always
`ReadOnlyReports` (`1`); the client does not choose or expand the scope.
Enforce the eventual key-count limit before creating a record.

```json
{ "name": "Excel - Home Computer" }
```

After persisting the key record, return `201 Created` with
`ApiKeyCreatedDto`: `key`, `name`, `scope`, `createdAt`, and `apiKey`. The
`apiKey` property contains the generated secret **only in this response**;
the illustrative placeholder below is not a valid key.

```json
{
  "key": "4b89ea80-621c-44c8-9d6f-76b0a95efcc7",
  "name": "Excel - Home Computer",
  "scope": 1,
  "createdAt": "2026-01-15T10:30:00Z",
  "apiKey": "<one-time-secret>"
}
```

Set `Location` to the collection URL `/api/user/apikeys`: no individual
key-read endpoint is proposed. Send `Cache-Control: no-store` on the creation
response. The UI shows the secret once and warns the user to copy it; if it is
lost, create another key. Never log the body or secret.

#### Revoke a key

`DELETE /api/user/apikeys/{key:guid}` changes `IsActive` to false and returns
`204` only after the change is persisted. Repeating the request for an already
revoked key owned by the caller also returns `204`. A nonexistent key or one
owned by another user returns `404`, without disclosing which case occurred.
Revocation immediately prevents use on the next API-key request; it does not
revoke normal web sessions or other keys.

All three endpoints return `401` without normal authentication and `500` for
unexpected server errors, following existing API error conventions. The create
action also returns `400` for invalid input or an exceeded key-count limit.
Specify that limit before implementation rather than silently choosing one.
Integration tests must cover owner isolation, response shapes and status codes,
one-time secret exposure, repeated revocation, and rejection of API keys for
all management actions.

Keep application logic dependent on Entities abstractions; infrastructure
implements persistence and hashing dependencies as appropriate. Do not log
secrets or sensitive user data. API-boundary diagnostics must follow
[LOGGING-POLICY.md](../../LOGGING-POLICY.md) and use `LoggerMessage` methods.

## Rate limiting and operational behavior

Apply the PRD targets of 100 requests per minute and 1,000 per hour **per API
key**, enforcing both limits. Return 429 when exceeded, with a useful retry
indication. A single unpartitioned fixed-window policy would not satisfy the
per-key requirement. Rate-limit only the intended API key requests without
changing normal web report traffic. Determine placement after authentication
and before report execution; test 401, 403, 429, and tenant context behavior
with the actual middleware pipeline. An in-memory implementation is per
instance, not a deployment-wide guarantee; select and validate a shared store
if multiple instances must enforce the limits globally.

## Testing and rollout

1. **Excel feasibility:** validate Basic credential storage, connection, reopen
   and refresh in the supported desktop versions before release. Verify
   template conversion with detail and non-detail reports, empty rows, nullable
   percentages, monthly order, metadata, and authoritative totals.
2. **Key lifecycle:** test generation, hashing, one-time secret display, owner
   isolation, invalid and revoked keys, account disablement, and persistence.
3. **Authentication and authorization:** run integration tests using real JWT
   and API key handlers, not only test authentication. Cover the export action
   for Viewer/Editor/Owner, unrelated workspaces, membership changes and
   removal, identity mixing, mismatched usernames, account passwords,
   malformed Basic headers, invalid credentials, and 401 versus 403. Assert
   API keys are rejected by the existing report, catalog and years actions,
   `GET /api/tenant`, transaction endpoints, key management, and writes.
   Assert JWT and cookie credentials alone cannot access export, including a
   different authenticated cookie alongside an invalid Basic credential.
4. **Report contract:** verify API-key export responses match normal JWT
   report responses for the same workspace, report ID, and year, including
   default and invalid years and unknown IDs. Preserve the existing controller
   tests and frontend behavior.
5. **Limits and workflow:** test both per-key rate windows, isolation between
   keys, 429 behavior, and normal web access. Add functional coverage for key
   management and report access; perform a documented desktop Excel smoke test
   because HTTP tests alone cannot establish connector behavior.

## Open implementation questions

- Does Basic authentication pass the Excel validation gate, including safe
  storage and refresh on every supported version?
- What key-management UI placement, quotas, and audit retention fit the existing
  application?
- Does the deployment require shared rate-limit state across instances?
- What safe last-used update strategy meets the desired freshness and load?

## References

- [Export PRD](PRD-EXPORT-API.md)
- [Built-in reports design](DESIGN-SIMPLE-REPORTS.md)
- [Report year design](DESIGN-REPORT-YEAR.md)
- [Microsoft Power Query Web connector](https://learn.microsoft.com/en-us/power-query/connectors/web/web)
