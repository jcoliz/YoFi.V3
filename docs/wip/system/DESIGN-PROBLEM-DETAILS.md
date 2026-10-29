---
status: Draft
ado: Feature 2186
source_project: ListsWebApp.V3
---

# Design: Global Problem Details Error Handling

## Summary

Port ListsWebApp.V3's (LWA) `useProblemDetails` composable,
`ProblemDetailsViewer` component, and error normalization approach into YoFi.

**Global error state is an intentional product and architecture choice.** All
consumers share the latest reported problem. This design does not retain
independent page, form, or modal problem stores. Replacing local error refs and
visibility flags with one shared problem is a goal, not a regression to avoid.

The migration also hardens normalization and defines display, lifecycle, and
telemetry behavior. Confirmed gaps in the LWA implementation are listed separately
so they can be corrected in that project too.

This is a draft design, not implementation approval for the open decisions below.

## Goals and Non-Goals

### Goals

- One application-wide current problem, dismissible from its active viewer.
- Consistent handling of generated-client, auth/ofetch, network, and local errors.
- Preserve useful Problem Details fields and extensions, especially `traceId`.
- Keep normalization independently usable without displaying or logging a problem.
- Keep errors visible through recovery refreshes without duplicate reporting.
- Preserve existing form validation and import progress information.
- Produce useful, privacy-safe diagnostics.
- Update functional tests to the new component contract.

### Non-goals

- An error queue, notification history, or separate problems per component.
- Replacing inline field validation with global alerts.
- Changing backend exception-to-status mappings or the authentication protocol.
- Reworking token refresh, request timeout policy, or generated API client code.
- Adding a new logging framework or redesigning all telemetry.
- Modifying LWA as part of this YoFi feature; track its fixes separately.

## Reference Implementations and Current Behavior

LWA source locations, relative to its repository:

- `src/FrontEnd.Nuxt/app/composables/useProblemDetails.ts`
- `src/FrontEnd.Nuxt/app/components/ProblemDetailsViewer.vue`
- `src/FrontEnd.Nuxt/app/utils/errorHandler.ts`

Relevant YoFi surfaces:

| Surface | Current behavior / migration concern |
|---|---|
| [Error normalizer](../../../src/FrontEnd.Nuxt/app/utils/errorHandler.ts) | Returns a problem and logs the raw error; dereferences `ApiException.result` even when it is null. |
| [ErrorDisplay](../../../src/FrontEnd.Nuxt/app/components/ErrorDisplay.vue) | Receives `problem` and `show` props; dismissal emits `update:show`. Presentation closely matches LWA. |
| [Transactions](../../../src/FrontEnd.Nuxt/app/pages/transactions/index.vue) and [details](../../../src/FrontEnd.Nuxt/app/pages/transactions/[key].vue) | Local problem and visibility refs; reloads clear them. |
| [Workspaces](../../../src/FrontEnd.Nuxt/app/pages/workspaces.vue) | Separate load/create/update/delete errors and several viewers; these intentionally converge into global state. |
| [Create modal](../../../src/FrontEnd.Nuxt/app/components/TransactionCreateModal.vue) and [edit modal](../../../src/FrontEnd.Nuxt/app/components/TransactionEditModal.vue) | Local alert state plus inline field validation. |
| [Import](../../../src/FrontEnd.Nuxt/app/pages/import.vue) | Uses normalized upload errors in per-file messages and saves/restores an error across refreshes. Some recovery paths currently clear the error they just reported. |
| [Login](../../../src/FrontEnd.Nuxt/app/pages/login.vue) and [registration](../../../src/FrontEnd.Nuxt/app/pages/register.vue) | Separate validation/auth error handling; replacing utility imports alone is insufficient. |
| [Error test page](../../../src/FrontEnd.Nuxt/app/pages/errordisplay-test.vue) | Must exercise the new reporting and viewer contract. |
| [Auth fetch wrapper](../../../src/FrontEnd.Nuxt/app/composables/useAuthFetch.ts) | Converts failures into Responses for NSwag; synthesizes 408 for timeouts and 500 for other network failures. Preserve this behavior in this feature. |
| [Generated client](../../../src/FrontEnd.Nuxt/app/utils/apiclient.ts) | Throws direct `ProblemDetails` for documented errors; otherwise throws `ApiException` containing a raw response and potentially null `result`. |
| [Exception middleware](../../../src/Controllers/Middleware/CustomExceptionHandler.cs) | Adds trace IDs and extensions. Some constructed responses do not explicitly set `Type`. |
| [Functional test component](../../../tests/Functional/Components/ErrorDisplay.cs) | Uses `error-display`; other page objects also contain direct selectors. |

## Proposed Architecture

### 1. Pure normalization

Keep the utility name `handleApiErrorImpl` used by LWA:

```typescript
handleApiErrorImpl(
  error: unknown,
  fallbackTitle?: string,
  fallbackDetail?: string,
): IProblemDetails
```

This function returns a plain problem object. It does not update global state,
log, or emit telemetry. Import progress messages and unit tests can use it without
causing presentation or reporting side effects.

Normalization requirements:

1. Guard unknown input before invoking the generated `ApiException` guard.
2. Handle both direct `ProblemDetails` instances and `ApiException` responses.
   Never assume an `ApiException` contains a non-null `result`.
3. Validate parsed JSON and ofetch `data` at runtime. Reject arrays, primitives,
   and invalid standard-field values; a TypeScript assertion is not validation.
   Allow absent standard fields and preserve valid extension fields.
4. Do not require `type` and `title`. RFC 9457 does not make them mandatory.
   Accept recognizable problem objects without treating arbitrary JSON as one.
   Define recognition in tests: at least one valid standard problem field;
   extension-only objects use the transport fallback.
5. Normalize body status when present and valid; otherwise retain transport status.
   Use the same effective status for the returned object and friendly detail.
6. Preserve `type`, `title`, `detail`, `instance`, `traceId`, validation `errors`,
   and other extensions from recognized problem objects.
7. Malformed JSON, HTML, empty bodies, and network failures produce a useful
   fallback, not another exception or a raw HTML message.
8. For ordinary `Error`, prefer the contextual fallback title over generic names
   such as `Error`; use a safe message as detail.
9. Preserve context-aware fallback detail composition without repeated punctuation.
10. Handle null, undefined, strings, and other thrown values safely.

An unparseable response is an expected normalization branch, not an additional
error report. The resulting fallback is reported once at the reporting boundary.
Do not hand-edit generated code to fix its unsafe guard.

### 2. Global composable

Expose LWA's familiar surface:

| Member | Contract |
|---|---|
| `problem` | Shared current problem; undefined means no alert. Callers read it rather than mutating it directly. |
| `clearError()` | Clear presentation state; no error telemetry. |
| `setError(problem)` | Publish an already constructed local problem and report it once. |
| `handleApiError(error, fallbackTitle?, fallbackDetail?)` | Normalize, publish, and report once. |

Prefer Nuxt `useState` inside the composable rather than a module-level mutable
ref. This preserves application-global state in the browser while isolating
server requests and supporting Nuxt hydration. Store plain normalized objects,
not Error instances. Do not persist the problem to localStorage or across reloads.
Import `IProblemDetails` explicitly from the generated client.

The reporting methods need not be async: normalization and state publication are
synchronous. Existing callers must not depend on a returned problem; callers
needing a normalized value use the pure helper.

### 3. Viewer and placement

Port the LWA viewer's Bootstrap alert, dismiss button, expandable extension
fields, server-error auto-expansion, and escaped text rendering. Remove the old
`problem`/`show` props and `update:show` event. Use
`data-test-id="problem-details-viewer"` and retain the child selector names.

**Proposed placement:** one active viewer at a time, consuming the global state.
The normal outlet belongs in the shared layout content area. When a modal is
open, the outlet must be visible and operable inside the active modal rather than
behind its backdrop. Any presentation-host selection is not separate error state.
Do not leave every old viewer in place and display identical alerts throughout
the page.

Cover [default](../../../src/FrontEnd.Nuxt/app/layouts/default.vue),
[chrome](../../../src/FrontEnd.Nuxt/app/layouts/chrome.vue), and
[blank](../../../src/FrontEnd.Nuxt/app/layouts/blank.vue) layouts. Keep form errors,
entered values, and modal open state intact after failed submissions.

Viewer requirements:

- Dismissal clears the shared problem everywhere.
- Replacing a problem resets expansion deterministically for the new problem.
- Preserve `role="alert"`, an accessible close label, and keyboard interaction.
- Render fields as text, never raw HTML.
- Do not move focus into background page content while a modal owns focus.

Final modal/outlet placement is an open UX decision below.

## Global State Lifecycle

The following are proposed policies to confirm before implementation:

| Event | Proposed behavior |
|---|---|
| User starts a new top-level action or explicit retry | Clear the previous problem once at the action boundary. |
| An action performs several API calls | Nested loaders do not clear state independently. |
| An API/local failure is reported | Replace the global problem; the most recently reported failure wins. No queue. |
| Recovery/background reload succeeds | Do not clear the failure that triggered recovery. |
| Recovery/background reload fails | Report that failure with its own context; it becomes the latest problem. |
| User dismisses an alert | Clear state, without reporting the problem again. |
| Navigation, workspace change, or logout | Clear the old context's problem. |
| A response arrives from an obsolete route/workspace/action | Do not publish it into the new context. |
| Modal closes | Do not implicitly clear a problem; show it at the normal outlet. |

Late-result handling must also cover a successful login followed by an older
failed request. Use existing cancellation where available; otherwise capture a
small context/action generation at the operation boundary and check it before
publishing. Do not add a general-purpose request orchestration framework.
Failures of obsolete operations may still produce safe diagnostics; they must
not overwrite the current context's visible problem.

Concurrent operations in the same active context use report order, not request
start order. This is the intended "latest reported problem" model.

## Import, Forms, and Authentication

- Preserve inline validation and per-file import status lists. These are form
  and operation results, not alternative stores for the global alert.
- Normalize each upload failure once, use its detail in the file status, and
  publish/report it once. Refresh pending items and summary without clearing it.
- Remove the save/restore workaround once refreshes no longer clear state.
  Avoid publishing the same error again after a reload.
- Import parsing warnings returned in successful response DTOs remain import
  results; they are not automatically frontend exceptions.
- Migrate auth errors through the ofetch normalization path and local validation
  summaries through `setError`. Preserve redirects and callback URLs.
- Preserve validation `errors` extensions even if displayed as expandable
  diagnostics; this feature does not introduce new field-error mapping.
- Migrate all error producers, including direct assignments of ad-hoc problem
  objects, not just imports of the existing utility.

## Logging and Application Insights

Use the existing [App Insights wrapper](../../../src/FrontEnd.Nuxt/app/composables/useAppInsights.ts)
and [debug logger](../../../src/FrontEnd.Nuxt/app/composables/useDebugLog.ts).
Follow the [logging policy](../../LOGGING-POLICY.md).

Proposed reporting policy:

- Validation failures and expected rejected credentials are user-correctable
  outcomes, not automatically operational exceptions.
- Unexpected frontend failures, server failures, and transport failures produce
  exception telemetry. Do not classify solely by status; for example, a missing
  required resource may indicate a defect while a user lookup miss may not.
- Use an explicit expected/unexpected classification at reporting call sites
  where necessary, with a conservative documented default.
- Report each failure once. Rendering, normalization, dismissal, and recovery
  success do not emit exceptions.
- Keep event/exception titles stable and developer-authored. Include safe
  dimensions such as operation, source, status, problem type, and trace ID.
- Do not blindly export problem detail/title, instance URLs, arbitrary extensions,
  uploaded filenames, or serialized raw errors. They may contain financial data,
  account identifiers, or user input. Displaying data to its user is distinct
  from sending it to telemetry or persistent debug storage.
- The debug logger's top-level key filter does not sanitize sensitive text inside
  `detail` or nested extensions. Pass an allowlisted diagnostic object instead.
- A debug event and an exception are distinct records; intentionally choose
  sinks so one failure does not accidentally become duplicate exception reports.
- Missing telemetry configuration must not affect display. A failing telemetry
  sink must not throw from the reporting path or recursively report itself;
  isolate that boundary and emit a safe local warning.

Functional tests disable App Insights under Playwright, so those tests alone
cannot verify exception classification, dimensions, or reporting counts.

## Confirmed LWA Gaps to Fix Upstream

These are source-confirmed implementation gaps, not objections to global state.
Create corresponding LWA work items; do not silently copy them into YoFi.

| ID | Location in LWA | Gap | Required correction / verification |
|---|---|---|---|
| LWA-1 | `errorHandler.ts`: first `ApiException.isApiException` call; generated guard | The generated guard dereferences its argument. Null/undefined thrown values can make normalization throw. | Guard unknown input before calling it; test null and undefined. Fix the caller or generation template, not generated output manually. |
| LWA-2 | `errorHandler.ts`: `tryParseProblemDetails` | Requires both `type` and `title`; the comment incorrectly states RFC 9457 requires both. Legitimate partial problems lose their details/extensions. | Validate optional standard fields, accept recognizable partial problems, correct the comment, and test missing type/title. |
| LWA-3 | `errorHandler.ts`: `ApiException` and `FetchError` returns | Effective transport status is used for friendly text but not always assigned to the returned problem. Parsed bodies without status and unparsed responses lose it. | Return the effective status explicitly in every transport branch; verify 500 auto-expansion and telemetry status. |
| LWA-4 | `errorHandler.ts`: `FetchError.data` | An unchecked type assertion treats arbitrary response data as Problem Details. | Runtime-validate data, including malformed standard fields, strings, arrays, and non-problem JSON. |
| LWA-5 | `useProblemDetails.ts`: module-level `problem` | Mutable module state is not request-isolated if the composable is used during SSR. This is a conditional SSR risk, not evidence of an observed production leak. | Use Nuxt app/request-scoped global state or demonstrate/enforce client-only use; verify separate app/request instances. |
| LWA-6 | `useProblemDetails.ts`: `setError` / `handleApiError` | Every reported local/API error is an exception, including expected validation/auth outcomes. The source itself notes this distinction is missing. | Establish expected/unexpected classification and test telemetry counts. |
| LWA-7 | `useProblemDetails.ts`: diagnostics | `problemDetail` is sent directly to App Insights and the full problem is passed to the debug logger. Safe content is not established at this boundary. | Review against LWA's own data policy; use allowlisted metadata and prevent sensitive free text from entering sinks. |
| LWA-8 | `useProblemDetails.ts`: direct `trackException` call | No isolation of a throwing telemetry sink; `handleApiError` can reject after setting state, and `setError` can throw. | Test a throwing telemetry stub; preserve display and avoid recursive reporting. |
| LWA-9 | `errorHandler.ts`: ordinary `Error` branch | `err.name ?? fallbackTitle` normally selects generic `Error` instead of the contextual fallback. | Prefer the provided contextual title; test ordinary and named errors. |

Additional shared hardening: reset viewer expansion on every problem replacement
and test consecutive server errors with different extension fields. The current
watcher can retain expansion state when a server error without extensions replaces
one with extensions.

YoFi-specific fixes are separate: its current null `ApiException.result` access,
import recovery clearing, migration of multiple local viewers, and existing
test selectors are not all defects in LWA.

### Bug Filing Details

The following entries are intended to be copied into separate LWA bugs. Source
paths are relative to the LWA repository. Reproductions describe focused test
cases derived from the source; they have not been executed against production.
LWA-5 is conditional on server-side use, LWA-6 requires a reporting-policy
decision, and LWA-7 establishes an unsafe data flow rather than proving that
real sensitive data has already been collected. No production incident or
severity assessment is implied.

#### LWA-1: Error normalization throws for null or undefined input (AB#2189)

**Location:** `src/FrontEnd.Nuxt/app/utils/errorHandler.ts`,
`handleApiErrorImpl`; generated `ApiException.isApiException` in
`src/FrontEnd.Nuxt/app/utils/apiclient.ts`.

**Problem and impact:** The normalizer accepts `unknown` but immediately invokes
a generated guard that reads `obj.isApiException`. For null or undefined input,
this raises a TypeError before fallback handling is reached. The secondary
failure masks the original failure and can prevent an alert from being shown.

**Reproduction:**

1. Call `handleApiErrorImpl(null, 'Load Failed')`.
2. Repeat with `undefined`.
3. Observe that both calls throw instead of returning a problem. These values
   are legal JavaScript thrown values even if normal API paths rarely use them.

**Expected behavior:** Return a problem with the supplied fallback title and
useful default detail for either input.

**Suggested correction:** Check that the input is non-null and an appropriate
object before invoking the generated guard. Keep the fix outside generated
output unless changing the generator/template itself.

**Acceptance criteria:**

- [ ] Null and undefined return a fallback problem without throwing.
- [ ] Primitive thrown values also reach safe fallback handling.
- [ ] ApiException and direct ProblemDetails paths still work.
- [ ] Tests run with the actual generated guard, not a null-safe mock of it.

#### LWA-2: Valid partial Problem Details responses are discarded (AB#2190)

**Location:** `src/FrontEnd.Nuxt/app/utils/errorHandler.ts`,
`tryParseProblemDetails`.

**Problem and impact:** The parser requires truthy `type` and `title`. RFC 9457
does not require both fields. A legitimate partial response is discarded,
losing specific detail and diagnostic extensions. The accompanying comment
incorrectly describes the RFC requirement.

**Reproduction:**

1. Construct an ApiException with status 404, null result, and response JSON
   `{"title":"Item not found","detail":"The item was deleted.","traceId":"test-trace"}`.
2. Normalize it and observe that the detail and trace ID are lost because
   `type` is absent.
3. Repeat with `{"type":"about:blank","status":404,"detail":"The item was deleted."}`;
   the missing title also causes rejection.

**Expected behavior:** Preserve valid available problem fields and extensions,
filling absent title/status from the applicable fallback or transport context.

**Suggested correction:** Validate optional standard fields rather than requiring
both names. Distinguish recognizable partial problems from unrelated JSON.
Coordinate with LWA-4 so parsed strings and ofetch data use consistent validation.

**Acceptance criteria:**

- [ ] Missing type, missing title, and detail/status-only examples normalize.
- [ ] Available detail, trace ID, and validation extensions survive.
- [ ] Empty objects, arrays, and unrelated JSON follow a documented fallback.
- [ ] The inaccurate RFC comment is corrected.

#### LWA-3: Normalized transport errors lose HTTP status (AB#2191)

**Location:** `src/FrontEnd.Nuxt/app/utils/errorHandler.ts`, ApiException
returns and FetchError return in `handleApiErrorImpl`.

**Problem and impact:** Some branches calculate effective status for friendly
detail text but omit it from the returned object. Consequently the viewer cannot
recognize a server error, and `httpStatus` is absent from exception telemetry,
even though the response status is known.

**Reproduction:** Normalize each of these inputs:

1. ApiException with status 500 and an empty or HTML response.
2. ApiException with status 500 and
   `{"type":"about:blank","title":"Server failure","traceId":"test-trace"}`.
   This passes the current parser, independently reproducing the status bug.
3. FetchError whose response status is 503 and whose data has a title but no status.

**Actual behavior:** Returned status is undefined in all three cases. Friendly
text may still describe a server failure, hiding the inconsistency.

**Expected behavior:** The output contains effective status in addition to
using it to compose detail.

**Suggested correction:** Explicitly assign validated body status or transport
status to the returned problem in each transport branch.

**Acceptance criteria:**

- [ ] All three cases retain their transport status.
- [ ] A valid explicit body status follows the documented precedence rule.
- [ ] Invalid body status does not override a valid transport status.
- [ ] A 500 problem with extensions auto-expands and reports `httpStatus: 500`.

#### LWA-4: FetchError response data is trusted without runtime validation

**Location:** `src/FrontEnd.Nuxt/app/utils/errorHandler.ts`, FetchError branch.

**Problem and impact:** `err.data as IProblemDetails` does not validate network
input. Spreading a string creates numeric character properties; arrays become
indexed extensions. Invalid standard fields can also reach the viewer and
telemetry despite the advertised problem type.

**Reproduction:** Construct FetchError fixtures with a 502 response and these
data values, then normalize them:

1. `"Bad gateway"`: numeric character keys appear in the returned problem.
2. `["unavailable"]`: an array index appears as an extension.
3. `{"title":42,"status":"broken","detail":{"message":"unavailable"}}`:
   non-string title/detail and non-numeric status are trusted.
4. `{"unrelated":"payload"}`: non-problem JSON is treated as problem extensions.

**Expected behavior:** Malformed/non-problem response data produces a useful
fallback using transport context, not arbitrary fields or invalid types.

**Suggested correction:** Add a shared runtime validator for recognizable
problem objects. Preserve valid extensions on accepted problems; do not strip
all extensions as a shortcut. Define how invalid standard fields are rejected
or omitted and test that policy consistently.

**Acceptance criteria:**

- [ ] Strings/arrays never become indexed extension fields.
- [ ] Output standard fields have valid runtime types.
- [ ] Malformed or unrelated data falls back without throwing.
- [ ] Valid auth Problem Details and validation extensions still survive.

#### LWA-5: Module-global problem state is shared across server app instances

**Status**: Not relevant given SSG architecture.

**Location:** `src/FrontEnd.Nuxt/app/composables/useProblemDetails.ts`,
module-level `const problem = ref<IProblemDetails>()`.

**Classification:** Conditional SSR/app-isolation risk. Confirm server execution
and deployment topology before treating this as a production exposure.

**Problem and impact:** A module-scoped ref is shared by consumers of that module
in the same runtime. If separate server requests use it, one request can observe
or clear another request's problem. Application-global browser state is intended;
cross-request server state is not.

**Reproduction / investigation:**

1. Create two isolated Nuxt server app/request contexts in one process, using the
   same imported composable module and stubbed telemetry/logger dependencies.
2. Set a synthetic problem through context A.
3. Read the problem through context B; check whether it contains A's value.
4. Clear through B and check whether A also changes.
5. Establish whether actual LWA routes invoke these methods server-side. Do not
   infer production leakage from the isolated reproduction alone.

**Expected behavior:** Consumers in one browser app share state, but independent
server requests/apps do not share mutable problems.

**Suggested correction:** Use Nuxt app/request-scoped `useState`, or enforce and
document client-only use if that is the supported architecture. Retain global
state within a single browser app.

**Acceptance criteria:**

- [ ] Independent server contexts cannot observe/clear each other's problem.
- [ ] Multiple consumers within one browser app still share the same problem.
- [ ] Hydration and route changes retain the documented lifecycle behavior.
- [ ] Record whether current production deployment actually exercises the risk.

#### LWA-6: Expected user errors are always recorded as operational exceptions

**Status**: This is really more of an improvement rather than a bug fix.
I don't think we need to do this until we have data that we're unhappy with
overly noisy instrumentation.

**Location:** `src/FrontEnd.Nuxt/app/composables/useProblemDetails.ts`,
`setError`, `handleApiError`, and `trackProblemDetails`.

**Classification:** Confirmed reporting behavior; desired classification requires
product/operations agreement. This is not a failure of user-facing display.

**Problem and impact:** Every report emits exception telemetry, regardless of
whether it represents routine validation, rejected credentials, or an unexpected
failure. This adds noise to exception counts and can make alerts less actionable.
The source remarks already identify the missing distinction.

**Reproduction:**

1. Stub `useAppInsights().trackException` and invoke `setError` for a missing
   required field.
2. Report a direct 401 ProblemDetails for an expected credential rejection.
3. Observe an exception call for each, just as for an unexpected 500.

**Expected behavior:** Continue displaying all relevant problems, but apply an
agreed expected/unexpected classification to operational exception reporting.

**Suggested correction:** Add explicit classification where intent cannot be
inferred safely. Do not simply suppress every 4xx: some represent defects.
Decide whether expected outcomes merit a separate safe event or no telemetry.

**Acceptance criteria:**

- [ ] Document expected outcomes and the default classification.
- [ ] Routine validation/credential rejections do not emit operational exceptions.
- [ ] Unexpected frontend, transport, and server failures remain reportable.
- [ ] Tests assert report counts independently of viewer rendering.

#### LWA-7: Raw problem text and extensions flow into diagnostic sinks

**Status:** Problem Details contents are already logger-friendly by design
when created on the server side.

**Location:** `src/FrontEnd.Nuxt/app/composables/useProblemDetails.ts`,
`trackProblemDetails` and logger calls; filtering in
`src/FrontEnd.Nuxt/app/composables/useDebugLog.ts`.

**Classification:** Confirmed unfiltered-content data flow. Actual sensitive-data
collection and policy violation require validation against LWA's own data policy.

**Problem and impact:** The composable sends `problemDetail` and `problemTitle`
directly to App Insights and passes the entire problem to the debug logger.
The logger filters top-level names, not values or nested objects. Sensitive
content inside `detail` or an innocuously named extension can therefore reach
enabled console, localStorage, and Application Insights sinks.

**Reproduction using synthetic data only:**

1. Configure sink spies/stubs; do not send test payloads to live telemetry.
2. Report a problem with `detail: "Account: synthetic-private-marker"` and
   an extension `context: { email: "synthetic@example.invalid" }`.
3. Inspect the direct exception properties and enabled debug sink payloads.
4. Observe that the detail and nested marker survive top-level key filtering.
   Also test dynamic fallback text used as the exception message.

**Expected behavior:** Diagnostics contain only data approved for the destination.
The viewer may still show the user's problem without exporting the full payload.

**Suggested correction:** Define an allowlist of safe metadata at the reporting
boundary. Review titles, details, fallback messages, instance URLs, and extensions,
not only fields named password/email. Keep trace IDs and stable operation names.
Do not treat YoFi's policy as automatically binding on LWA.

**Acceptance criteria:**

- [ ] LWA's permitted diagnostic fields and destinations are documented.
- [ ] Synthetic restricted markers are absent from every enabled sink.
- [ ] Safe correlation metadata remains available.
- [ ] The original user-visible problem remains useful and intact.

#### LWA-8: A throwing telemetry sink escapes the error reporting path

**Location:** `src/FrontEnd.Nuxt/app/composables/useProblemDetails.ts`,
direct `ai.trackException` call; wrapper in
`src/FrontEnd.Nuxt/app/composables/useAppInsights.ts`.

**Classification:** Confirmed missing fault boundary; a throwing SDK/stub is the
failure precondition. No actual SDK exception incident has been established.

**Problem and impact:** The optional App Insights wrapper handles a missing SDK
but does not catch SDK exceptions. If `trackException` throws, `setError` throws
and async `handleApiError` rejects. State is assigned first, so an alert may
already be visible, but caller control flow still receives a secondary failure.
An unawaited call may become an unhandled rejection.

**Reproduction:**

1. Provide an App Insights stub whose `trackException` throws
   `new Error('Synthetic telemetry failure')`.
2. Call `setError` with a valid problem; observe the propagated exception.
3. Await `handleApiError` with valid input; observe rejection after state changes.

**Expected behavior:** Reporting preserves the original problem and completes
without propagating failures from optional telemetry infrastructure.

**Suggested correction:** Isolate the telemetry boundary; emit a safe local
warning that cannot route back through the same failing sink. Keep normalization
defects visible rather than wrapping the entire reporter in a broad catch.

**Acceptance criteria:**

- [ ] Both reporting methods preserve state and complete with a throwing stub.
- [ ] No recursive telemetry attempt or unhandled rejection occurs.
- [ ] A safe local diagnostic records sink failure without problem payloads.
- [ ] Missing SDK and functioning SDK paths still behave correctly.

#### LWA-9: Generic Error.name overrides the contextual fallback title (AB#2192)

**Location:** `src/FrontEnd.Nuxt/app/utils/errorHandler.ts`, ordinary Error branch.

**Problem and impact:** `err.name ?? fallbackTitle` normally selects `"Error"`
because ordinary errors already have that name. An action-specific title such
as `"Load Failed"` is ignored, making the alert less informative. This also
differs from the utility's documented fallback behavior for unexpected errors.

**Reproduction:**

1. Call `handleApiErrorImpl(new Error('Connection interrupted'), 'Load Failed')`.
2. Observe `title: 'Error'`, not `'Load Failed'`.
3. Repeat with a named Error such as TypeError to establish the desired policy
   for technical names versus user-facing context.

**Expected behavior:** For an ordinary error, use the contextual fallback title
and retain useful detail. Preserve server-provided titles in the API branches.

**Suggested correction:** Prefer the contextual fallback for ordinary errors;
document whether any named error receives a different user-facing title.

**Acceptance criteria:**

- [ ] The example returns `title: 'Load Failed'` with useful detail.
- [ ] Without an explicit title, the documented default is used.
- [ ] Named-error behavior is explicit and tested.
- [ ] ApiException, direct ProblemDetails, and FetchError titles do not regress.

## Implementation Sequence

1. Confirm the open UX, lifecycle, and telemetry decisions below.
2. Harden the pure normalizer and add focused tests for its complete input matrix.
   Reuse existing test infrastructure; if no frontend unit runner exists, agree
   the smallest appropriate test setup before introducing one.
3. Add global state and reporting with tests for shared identity, isolation,
   replacement, clearing, and telemetry behavior.
4. Port the viewer and wire the chosen layout/modal outlet strategy.
5. Migrate transactions, workspaces, payee rules, import, auth, and the error test
   page, including modal components and all local problem assignments.
6. Update functional component/page objects and assertions. Remove the old
   component/export only after all consumers migrate.
7. Run focused tests, existing frontend lint/build, and the relevant functional
   scenarios. Update directly related documentation and create LWA follow-up items.

Do not leave old and new reporters active for the same operation during staged
migration. No backend mapping or generated-client changes are expected.

## Acceptance and Verification

- [ ] Two independent consumers observe the same problem and dismissal.
- [ ] Separate Nuxt app/SSR request instances do not share mutable problem state.
- [ ] At most one viewer is active, including with an open modal and on auth pages.
- [ ] Modal submissions remain editable and their errors visible without closing.
- [ ] Direct ProblemDetails and ApiException with null result both normalize.
- [ ] Partial problem bodies retain detail, extensions, and transport status.
- [ ] FetchError with/without response data normalizes safely.
- [ ] Empty, HTML, malformed JSON, arrays, primitives, null, and undefined never
      cause the normalizer to throw.
- [ ] 400, 401, 403, 404, 409, 500, timeout, and network paths have useful output.
- [ ] Trace IDs and validation extensions survive normalization and display.
- [ ] Latest reported current-context failure wins; obsolete results do not leak
      into a new route, workspace, login session, or action.
- [ ] Retry clears once; nested/recovery loads do not erase the triggering error.
- [ ] Multi-file upload progress survives, and failures are reported only once.
- [ ] Expected outcomes do not inflate exception counts; diagnostic dimensions
      contain no sensitive free text or arbitrary response payloads.
- [ ] Missing/throwing telemetry leaves the viewer functional.
- [ ] Expand/collapse resets correctly and dismissal works using the keyboard.
- [ ] Updated functional selectors work across all migrated pages.
- [ ] Frontend lint/build and focused automated regression tests pass.

## Open Decisions

1. **Viewer placement with modals:** approve the proposed single active outlet
   moving into the modal, or prefer an always-visible global overlay that remains
   accessible within modal focus management? Multiple simultaneous duplicate
   viewers are not recommended.
2. **Lifecycle:** approve clearing on navigation/workspace/logout and at top-level
   action boundaries, while preserving problems through recovery and modal close?
   This is intentionally more specific than LWA's "clear before each API call."
3. **Expected errors:** approve excluding routine validation/auth rejections from
   exception telemetry? Which outcomes still warrant operational reporting?
4. **Telemetry fields:** is the proposed metadata-only allowlist sufficient for
   support, or is reviewed, sanitized message text required?
5. **Support wording:** retain LWA's "Please contact support immediately" for all
   server errors, including the synthetic 500 used for network failures, or use
   less urgent guidance? Transport policy changes remain outside this feature.
6. **Delivery coordination:** should LWA fixes land first and then be ported,
   or should both implementations be updated in parallel using the same regression
   cases? Record LWA work-item links here when available.
