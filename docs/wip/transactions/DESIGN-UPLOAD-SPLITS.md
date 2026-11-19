---
status: Approved
target_release: Beta 2
prd_document: PRD-TRANSACTION-SPLITS.md
parent: "Feature 1982: Transaction Splits"
ado: "Story 2309: User - Upload splits (upload-splits)"
---

# Technical Design Document: Upload Transaction Splits from Excel

## Scope

This design covers Story 2309, **User - Upload splits (upload-splits)**, in
[`PRD-TRANSACTION-SPLITS.md`](PRD-TRANSACTION-SPLITS.md). It covers the complete
single-transaction workbook upload, template download, validation, atomic
append, rebalancing, and transaction-detail UI workflow.

The design intentionally excludes:

- the general workspace-data import preview and confirmation workflow;
- importing transactions or splits for multiple transactions;
- editing, deleting, or reordering splits after import;
- worksheets after the first worksheet;
- file formats other than `.xlsx`;
- non-currency amount precision or multiple currencies; and
- previewing the split upload before commit.

No other story in the transaction-splits PRD is changed. The feature uses the
existing add/edit/delete balancing conventions and leaves room for the
unbalanced-transaction warning when other workflows create an imbalance.

## Architectural Considerations

This follows the existing Clean Architecture and BFF patterns in
[`docs/ARCHITECTURE.md`](../../ARCHITECTURE.md): the controller accepts an
authenticated multipart request, the Application Feature validates and applies
the workbook atomically, and the Nuxt frontend displays the returned transaction
state.

ClosedXML is already an Application-layer dependency for workbook import and
export. No new package, middleware, cache, background job, schema, or ADR is
required. Workbook bytes are read and parsed in memory because the upload is
limited to 1 MiB and 200 data rows.

The workbook is parsed and fully validated before persistence begins. The
subsequent load/calculate/save operation uses the existing serializable
transaction abstraction so a concurrent split change cannot be overwritten by
a balance calculation based on stale allocations. The endpoint has no preview
operation: a valid upload commits immediately, while any validation error
returns before the transaction is changed.

## Design Decisions

| Decision | Alternatives considered | Rationale |
| -------- | ----------------------- | --------- |
| Limit uploads to 1 MiB and 200 non-empty data rows. | Use the 50 MiB workspace-import limit; 10,000 rows. | This is a single-transaction operation intended for long but human-scale allocations such as a paystub. The smaller cap bounds in-memory parsing and request cost while leaving ample room for the use case. |
| Read only the first worksheet. | Merge all worksheets; reject workbooks with additional worksheets. | This directly follows the PRD and lets users retain supporting sheets without affecting the import. |
| Validate the entire first worksheet before loading or mutating the transaction. | Save valid rows and skip bad rows; validate while persisting. | The PRD requires the entire import to be cancelled on invalid input and clear validation feedback before commit. |
| Accept formula cells when ClosedXML provides a cached result; do not recalculate formulas. | Reject formulas; evaluate formulas on the server. | Cached values preserve normal Excel usage without introducing a formula engine or non-deterministic recalculation. A formula without a usable cached value is a row error. |
| Match headers after trimming, case-insensitively. Reject duplicate recognized headers. | Exact, case-sensitive names. | The documented headers remain `Category`, `Amount`, and `Memo`, while harmless capitalization and surrounding whitespace do not make a workbook fail. Duplicate recognized headers are ambiguous and must fail. |
| Ignore completely blank rows and non-empty rows whose valid `Amount` is exactly zero. | Reject zero rows; import zero-valued splits. | This is the explicit PRD behavior for zero amounts and avoids creating meaningless persisted allocations. |
| Reject a worksheet with no non-zero importable rows. | Return a success-shaped no-op. | A user-selected upload should either add allocations or report that nothing was importable. A no-op success obscures the likely input error. |
| Add imported non-zero splits after all existing splits, then adjust the existing first split by `Order` to balance. | Replace all existing splits; balance by changing an imported split. | The PRD requires append behavior and names the existing first split as the balancing allocation. This is consistent with add/edit/delete operations. |
| Remove the balancing split only when its calculated amount is exactly zero. | Keep zero-valued splits; remove any near-zero value. | All persisted currency values use exact decimal cents, so exact zero is deterministic and follows the invariant to ignore zero allocations. |
| Return one `400 ValidationProblemDetails` with every row and workbook issue. | Return only the first issue; use a preview response. | The user can fix all problems in one pass, and the existing `ErrorDisplay` component already renders structured validation errors inline. |
| Carry the validation title, detail, and grouped errors on a reusable Application exception. | Add a split-specific exception and teach the controller handler how to translate split issues. | Validation shape belongs to the failure, while the API handler should remain a generic exception-to-ProblemDetails adapter. Other Application Features can reuse the same exception without new middleware branches. |
| Use a dedicated `UploadSplitsDialog.vue` component rather than page-local file state. | Add the form directly to the detail page. | Upload has its own file, busy, download, and error lifecycle. Encapsulation keeps the existing transaction page focused and is testable as a component. |
| Generate the template workbook in the backend. | Commit a static binary file; generate a CSV-like workbook in the browser. | A generated XLSX stays aligned with the accepted schema and ClosedXML version, and it is downloaded through the authenticated API client. |

## Entities

No entity changes are required. Existing
[`Transaction`](../../../src/Entities/Models/Transaction.cs) and
[`Split`](../../../src/Entities/Models/Split.cs) fields already represent the
authoritative transaction amount, signed allocation amount, sanitized category,
optional memo, and display order.

The operation continues to enforce the existing invariant that a persisted
transaction has at least one split. The first split may be removed only when at
least one validated uploaded split will remain.

## Data

No database schema, migration, `DbContext`, EF Core configuration, index,
constraint, value conversion, delete behavior, or `IDataProvider` change is
required.

The existing `Transaction -> Split` one-to-many relation, cascade delete, and
serializable transaction abstraction are sufficient. Schema balance is not
enforced by a database constraint because the parent transaction amount remains
authoritative and the wider feature permits advisory imbalance states.

## Data Integration Tests

No Data integration tests are required because this feature changes no schema,
EF model, query provider capability, or provider-specific behavior.

## Application Features

### Workbook and validation DTOs

Add the following records to a new
`src/Application/Dto/TransactionSplitUploadDto.cs`.

```csharp
public record SplitUploadTemplateResultDto(byte[] Content, string FileName);

public record SplitUploadRowDto(
    int RowNumber,
    decimal Amount,
    string Category,
    string? Memo);

public record SplitUploadIssueDto(
    int RowNumber,
    string Field,
    string Code,
    string Message);
```

- `SplitUploadTemplateResultDto` is an output DTO which represents **Generated XLSX
  template bytes and the download filename**.
- `SplitUploadRowDto` is an internal application transfer shape which represents
  **One validated, non-zero allocation from the first worksheet**. It is not
  exposed in the OpenAPI response contract.
- `SplitUploadIssueDto` represents **One workbook, header, or row
  validation issue**. `RowNumber` is one-based; workbook-level issues use `1`.

### Workbook services

Add
`src/Application/Services/TransactionSplitWorkbookService.cs`, which
**Parses and validates transaction-split upload workbooks and creates the
downloadable XLSX template**.

Public methods:

```csharp
public IReadOnlyCollection<SplitUploadRowDto> Parse(byte[] content);

public SplitUploadTemplateResultDto CreateTemplate();
```

`Parse` throws `ValidationProblemsException` when one or more issues exist, and
returns only non-zero, validated rows in source order otherwise. It applies
these rules in this order:

1. Open `content` as a ClosedXML `XLWorkbook`. Invalid ZIP/OpenXML/XML, an empty
   byte array, or a workbook with no worksheet produces a `file` issue with code
   `invalid_workbook` and message `The uploaded file is not a readable XLSX workbook.`
2. Select `workbook.Worksheets.First()`. Do not inspect or validate later sheets.
3. Read the first row as headers. Trim each used header and compare
   case-insensitively. `Category` and `Amount` are required; `Memo` is optional.
   Missing required headers use `missing_header`. Duplicate recognized headers
   use `duplicate_header`. Ignore all other columns.
4. Enumerate non-empty rows below the header in ascending row order. If more
   than 200 non-empty rows exist, add a `row_limit_exceeded` workbook issue and
   do not parse rows after the first 200.
5. Parse `Amount` first. Accept a numeric cell, a decimal text value accepted by
   invariant decimal parsing, or a formula cell with a usable cached numeric
   result. A formula with no cached numeric result uses `invalid_formula_result`.
   Any other missing or non-decimal value uses `invalid_amount`.
6. When the parsed amount is exactly zero, ignore the complete row. Category and
   memo validation is not performed for that ignored row.
7. Read `Category` from the cell value or a formula's cached text result, and
   pass it through `CategoryHelper.SanitizeCategory`. A formula without a usable
   cached text result is an `invalid_formula_result` issue for `Category`. A
   missing value is valid and becomes `string.Empty`.
8. Read optional `Memo` from the cell value or a formula's cached text result,
   trim it, and map whitespace to `null`. A formula without a usable cached
   text result is an `invalid_formula_result` issue for `Memo`.
9. Validate a `SplitEditDto(amount, category, memo)` with the existing
   `SplitEditDtoValidator`. Each FluentValidation failure becomes one issue for
   the row and property with code `invalid_value` and the existing user-facing
   message.
10. Continue collecting issues for all remaining rows within the 200-row cap.
    Do not stop after the first row error.
11. If no errors exist but no non-zero rows remain, add a row-1 `empty_import`
    issue with message `The worksheet does not contain any non-zero splits to import.`
12. If any issues exist, group them by `Row {RowNumber}: {Field}` into an
    `IReadOnlyDictionary<string, string[]>`, preserve source issue order within
    each group, and throw:

    ```csharp
    new ValidationProblemsException(
        "The split workbook failed validation.",
        "Correct the workbook errors and upload it again. No splits were changed.",
        errors);
    ```

    Otherwise return the validated rows in source order.

`CreateTemplate` creates one worksheet named `Splits`, with the first row
exactly `Category`, `Amount`, and `Memo`. It adds header-cell comments explaining
that Category and Amount are required, Amount is a signed currency value, and
Memo is optional. It formats the Amount column for two decimal places, freezes
row 1, sizes columns for normal entry, and returns filename
`yofi-transaction-splits-template.xlsx`. It contains no sample data row, so
downloading and re-uploading an untouched template produces `empty_import`.

### Validation exception

Add
`src/Application/Exceptions/ValidationProblemsException.cs`:

```csharp
public sealed class ValidationProblemsException
    : YoFi.V3.Entities.Exceptions.ValidationException
{
    public string Title { get; }
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationProblemsException(
        string title,
        string detail,
        IReadOnlyDictionary<string, string[]> errors)
        : base(detail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);
        ArgumentNullException.ThrowIfNull(errors);
        if (errors.Count == 0 || errors.Any(entry =>
                string.IsNullOrWhiteSpace(entry.Key)
                || entry.Value is not { Length: > 0 }
                || entry.Value.Any(string.IsNullOrWhiteSpace)))
        {
            throw new ArgumentException(
                "At least one validation problem with one message is required.",
                nameof(errors));
        }

        Title = title;
        Errors = errors.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.ToArray(),
            StringComparer.Ordinal);
    }
}
```

This exception **Carries a user-facing title and grouped field
problems for one failed validation**. It is not coupled to workbook uploads:
another feature may supply a different title, detail message, and grouped
errors. Construction requires a non-empty title, detail, and at least one
non-empty group. The exception's base `Message` is the user-facing detail. The
controller exception handler maps it to `400 Bad Request` and
`ValidationProblemDetails`, as specified in [Controllers](#controllers).

### Transaction feature operations

Update
[`TransactionsFeature`](../../../src/Application/Features/TransactionsFeature.cs)
to depend on `TransactionSplitWorkbookService`.

Add:

```csharp
public Task<SplitUploadTemplateResultDto> GetSplitUploadTemplateAsync(
    Guid transactionKey,
    CancellationToken cancellationToken = default);

public Task<TransactionDetailDto> UploadSplitsAsync(
    Guid transactionKey,
    byte[] workbook,
    CancellationToken cancellationToken = default);
```

`GetSplitUploadTemplateAsync` **Verifies a transaction and
creates the supported split-upload workbook template**. `transactionKey`
identifies the tenant-scoped transaction that will receive a later upload, and
`cancellationToken` is observed during the lookup. It returns
`SplitUploadTemplateResultDto` and throws `TransactionNotFoundException` when
the transaction is not found for the current tenant. The method performs the
existing tenant-scoped `GetTransactionByKeyAsync` lookup before delegating
template creation to the workbook service.

`UploadSplitsAsync` **Validates, appends, and balances
transaction splits from an uploaded XLSX workbook**. Parameters are:

- `transactionKey`: the tenant-scoped transaction key;
- `workbook`: exact uploaded bytes, size limits enforced by the caller; and
- `cancellationToken`: cancellation for parsing and persistence.

It returns the committed, ordered `TransactionDetailDto`. It throws:

- `ValidationProblemsException` when the workbook is invalid or has no
  non-zero rows;
- `TransactionNotFoundException` when the transaction is not found for the
  current tenant;
- `ValidationException` when the transaction has no existing split or the
  calculated balancing amount is outside the existing split amount rules;
- `DataConflictException` when a concurrent change prevents the serializable
  read-modify-write;
- `OperationCanceledException` when cancellation is observed.

The operation:

1. Calls `workbookService.Parse(workbook)` before starting a database
   transaction. No entity is loaded or changed while an invalid workbook is
   being parsed.
2. Starts `dataProvider.ExecuteInTransactionAsync(...,
   IsolationLevel.Serializable)`, checks cancellation, and loads the transaction
   through the existing `_currentTenant.Id`-scoped query with splits included.
3. Orders existing splits by `Order`. If none exist, throw `ValidationException`;
   the operation will not manufacture a missing base allocation.
4. Assign `Order` to imported rows starting after the maximum existing order,
   preserving workbook row sequence.
5. Calculate the balancing first split as:

   `transaction.Amount - sum(existing splits except first) - sum(imported rows)`.

6. Validate the calculated balancing amount with `SplitEditDtoValidator`.
   Category and memo from the existing balancing split are not changed.
7. Add each imported `Split` with the pre-sanitized category, trimmed optional
   memo, and assigned order. `dataProvider.Add(newSplit)` stages each one.
8. If the balancing amount is zero, remove the first existing split from both
   the transaction navigation and the data provider, then normalize all
   surviving splits to contiguous zero-based `Order` values while preserving
   their relative sequence. At least one imported non-zero split remains.
9. Otherwise assign the calculated balancing amount to the existing first
   split without changing its key, category, memo, or order.
10. Call `SaveChangesAsync(cancellationToken)` once and return
    `ToDetailDto(transaction)`.

Every load, change, and save remains inside one serializable transaction.
No partial uploaded set can be committed. Tenant isolation is inherited from
the existing base query; the workbook contains no transaction key and cannot
redirect allocations to another transaction or tenant.

Register `TransactionSplitWorkbookService` as a scoped service in
[`ServiceCollectionExtensions`](../../../src/Application/ServiceCollectionExtensions.cs).
The feature remains logging-free.

## Application Integration Tests

Extend
[`TransactionsFeatureTests`](../../../tests/Integration.Application/TransactionsFeatureTests.cs)
for the persistence and tenant-boundary behavior:

- **`UploadSplitsAsync_ValidWorkbook_AppendsRowsAndBalances`** —
  `Verifies a validated workbook appends in row order and adjusts the first existing split.`
  Assert source workbook order, category normalization, memo trimming, unchanged
  transaction fields, persisted balance, and returned detail DTO.
- **`UploadSplitsAsync_ImportedTotalBalancesWithoutFirstSplit_RemovesZeroAllocation`**
  —  `Verifies the zero-valued balancing split is removed and surviving orders are normalized.`
  Seed one existing split, upload rows that sum to the transaction amount, and
  assert the original key is gone while all imported rows remain.
- **`UploadSplitsAsync_ZeroRowsAndAdditionalWorksheet_ImportsOnlyFirstSheetNonZeroRows`**
  —  `Verifies zero rows and later worksheets are ignored while the first-sheet rows append.`
  Assert skipped zero rows do not consume display order.
- **`UploadSplitsAsync_InvalidSecondRow_LeavesTransactionUnchanged`** —
  `Verifies workbook validation completes before any split change is staged.`
  Assert the `ValidationProblemsException` contains the split-upload title,
  detail, and both grouped row errors when applicable, and persisted
  amounts/categories/memos/order remain unchanged.
- **`UploadSplitsAsync_TransactionInAnotherTenant_ThrowsNotFoundWithoutChanges`**
  — `Verifies split upload cannot address a transaction outside the current tenant.`

Parser and arithmetic edge cases that do not need a database are covered by
unit tests:

- required and case-insensitive headers;
- ignored additional columns and blank rows;
- cached formula values and formulas without cached values;
- 200/201 non-empty row boundary;
- malformed workbook;
- zero-only/no-importable rows;
- signed amounts, cent precision, field length limits, and formula text limits;
- template headers, worksheet name, and absence of sample data rows.

## Controllers

Update
[`TransactionsController`](../../../src/Controllers/TransactionsController.cs)
with these endpoints.

| Method | Route | Tenant role | Request body | Success response | Error responses |
| ------ | ----- | ----------- | ------------ | ---------------- | --------------- |
| GET | `/api/tenant/{tenantKey:guid}/transactions/{key:guid}/splits/upload-template` | Viewer | None | `200 FileResult`, `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` | `401`, `403`, or `404` for an invalid tenant-scoped transaction |
| POST | `/api/tenant/{tenantKey:guid}/transactions/{key:guid}/splits/upload` | Editor | `multipart/form-data` field `file` | `200 TransactionDetailDto` | `400` empty/wrong extension/oversize/invalid workbook/row validation; `401`; `403`; `404` transaction not found; `409` data conflict |

`GetSplitUploadTemplate` **Downloads the XLSX template for
uploading splits to the specified transaction**. It verifies the transaction
exists by calling `GetSplitUploadTemplateAsync(key, cancellationToken)` before returning the
template so the route cannot imply a missing resource. The generated file has
`Content-Disposition` attachment filename
`yofi-transaction-splits-template.xlsx`.

`UploadSplits` **Validates and appends uploaded XLSX split rows
to an existing transaction**. It performs HTTP concerns only:

1. Reject a missing or zero-length file with `ValidationException("file",
   "An XLSX workbook is required and cannot be empty.")`.
2. Reject any filename whose extension is not `.xlsx`, case-insensitively, with
   `ValidationException("file", "Only .xlsx workbooks are supported.")`.
3. Set the endpoint's request limit to 1,114,112 bytes (1 MiB workbook plus 64
   KiB multipart overhead). Independently reject `file.Length > 1048576` with
   a message stating that the workbook must not exceed 1 MB.
4. Copy the file to a byte array and pass it to
   `transactionsFeature.UploadSplitsAsync`.
5. Return `Ok(updatedTransaction)`.

For the template route, call the new feature method that both verifies the
transaction and creates the template. Do not trust a client-provided MIME type.
The `.xlsx` extension is an early check; ClosedXML parsing is the authoritative
content check.

Add one exception-handler branch in
[`CustomExceptionHandler`](../../../src/Controllers/Middleware/CustomExceptionHandler.cs),
before the general `ValidationException` branch:

```csharp
ValidationProblemsException exception
    => await HandleValidationProblemsExceptionAsync(
        httpContext, exception, cancellationToken)
```

`HandleValidationProblemsExceptionAsync` returns:

- status `400`;
- `Title = exception.Title`;
- `Detail = exception.Message`;
- `Errors = exception.Errors`;
- the standard request instance and `traceId`.

The handler contains no workbook, row, field-name, or split-upload logic. Grouping
and issue wording are the responsibility of the feature that creates the
exception. This mapping is sufficient for the frontend's existing
[`ErrorDisplay.vue`](../../../src/FrontEnd.Nuxt/app/components/ErrorDisplay.vue)
to render every row issue inline. The new branch is checked before
`ValidationException` because `ValidationProblemsException` derives from it.

Reuse the controller's existing `[LoggerMessage]` events:

- `LogStartingKey` for either operation;
- `LogOkKey` after a template or committed upload response.

Do not log the filename, category, memo, amount, workbook bytes, or row content.
Application and Data remain logging-free.

The change is backward compatible: two routes are added, existing routes and
DTOs are unchanged. Regenerate the TypeScript client with
[`Generate-ApiClient.ps1`](../../../scripts/Generate-ApiClient.ps1). The
generated methods are:

- `getSplitUploadTemplate(key, tenantKey)` returning `FileResponse`;
- `uploadSplits(key, tenantKey, file)` returning `TransactionDetailDto`.

Controller integration tests will verify Editor/Owner authorization, Viewer
rejection for upload, file-extension and size rejection, multipart response
contracts, row-validation ProblemDetails, missing-transaction `404`, and
generated-file content type. Unit and controller implementation details remain
outside this design's test list per the project template.

## Front End

### `app/components/transaction/UploadSplitsDialog.vue`

Add a component which **Presents XLSX selection, template download, upload,
and validation-error display for user to upload splits for a transaction**.

Props:

```ts
type Props = {
  show: boolean
  tenantKey: string
  transactionKey: string
}
```

Emits:

```ts
type Emits = {
  'update:show': [value: boolean]
  'update:busy': [value: boolean]
  uploaded: [transaction: TransactionDetailDto]
}
```

The component:

- renders `ModalDialog` with title `Upload splits`;
- resets its selected file and errors whenever `show` transitions from false to
  true;
- provides an `.xlsx` file input and shows the selected filename;
- rejects non-`.xlsx` names and files larger than 1 MiB client-side with an
  `IProblemDetails` title of `Unsupported workbook`, without calling the API;
- calls `TransactionsClient.getSplitUploadTemplate` from the template button
  and downloads `response.fileName` / `response.data` with
  `URL.createObjectURL`;
- calls `TransactionsClient.uploadSplits` once from the primary button and
  sends the selected file as the generated multipart file parameter;
- disables file selection, close, cancel, upload, and template download while
  an API call is active; ignores duplicate submissions;
- uses `handleApiError` and `ErrorDisplay` for all API and local validation
  errors. On a `400`, the generated ProblemDetails errors render as an inline
  row-by-row list;
- keeps the selected file and dialog open after any failure;
- emits `uploaded` with the returned committed DTO and closes after success;
- emits `update:busy` while either upload or template download is active; and
- does not call preview endpoints or calculate split totals itself.

Closing the dialog never commits data. The component never stores workbook bytes
in localStorage or a shared store.

### `app/pages/transactions/[key].vue`

Update the transaction detail page summary to **open the split-upload dialog and
replace local transaction state with the committed upload result**.

Add an **Upload** action next to the existing Add action in the Splits card,
visible only to Editors and Owners. It is disabled while the transaction is
loading or any transaction/split operation is active.

The page:

- owns `showUploadSplits`;
- tracks the dialog's `update:busy` event in the existing
  `splitOperationActive` calculation so Add, Edit, Delete, and transaction Edit
  actions cannot race the upload;
- opens the component for the current tenant and transaction;
- replaces `transaction` with the `uploaded` event payload, which immediately
  refreshes rows and server-provided balance state; and
- resets/closes upload state when workspace or transaction route parameters
  change.

The page does not parse workbook data or call the upload/template endpoints
directly.

### User-visible states

- **Idle**: file input, Upload button, Cancel button, and Download template
  action are available; Upload is disabled until a file is selected.
- **Pending**: Upload or template download shows a spinner, duplicate actions
  and dismissal are disabled, and the selected file remains visible.
- **Success**: the dialog closes, the page immediately displays the server's
  ordered split list and balance state, and the toast text is
  `Transaction splits uploaded`.
- **400 validation**: the dialog remains open; the `ErrorDisplay` shows the
  ProblemDetails detail and every `Row N: Field` message; no page state changes.
- **409 conflict**: the dialog remains open with the standard data-conflict
  message. The user closes/reopens or reloads the transaction before retrying.
- **403**: the standard permission error is shown; the operation remains closed
  to unauthorized roles by the UI and rejected by the API.
- **404**: the standard not-found message is shown. No success-shaped fallback
  is returned.
- **500**: the standard server error and trace diagnostic are shown by
  `ErrorDisplay`.

### Test IDs

| data-test-id | Element | Purpose |
| ------------ | ------- | ------- |
| `upload-splits-button` | Splits card Upload action | Opens the upload dialog |
| `upload-splits-dialog` | Modal root | Identifies and waits for the upload workflow |
| `download-splits-template-button` | Template download button/link | Downloads the authenticated XLSX template |
| `upload-splits-file` | File input | Selects the `.xlsx` workbook |
| `upload-splits-selected-file` | Selected filename text | Confirms the current file |
| `upload-splits-help` | Format/limit help text | Describes headers and limits |
| `upload-splits-error` | Dialog `ErrorDisplay` wrapper | Locates upload validation errors |
| `upload-splits-submit-button` | Modal primary action | Commits the selected workbook |
| `upload-splits-cancel-button` | Modal secondary action | Closes without uploading |
| `upload-splits-pending` | Pending status/spinner region | Materially identifies in-progress operation |

The existing `transaction-splits`, `transaction-splits-table`, split row, and
balance-alert test IDs continue to identify the committed result.

## Component Tests

Add
`src/FrontEnd.Nuxt/tests/transaction-upload-splits.test.ts`
with these behavioral tests:

1. `it('uploads the selected workbook and emits the returned transaction')`
   mounts the dialog with valid props, selects a `.xlsx` `File`, activates the
   primary action, and verifies one `uploadSplits` call plus the `uploaded` and
   `update:show(false)` events.
2. `it('downloads the generated template through the authenticated client')`
   verifies `getSplitUploadTemplate`, `URL.createObjectURL`, and the generated
   filename without making the upload request.
3. `it('rejects unsupported names and files larger than 1 MiB locally')`
   verifies inline errors and no API call for both client boundaries.
4. `it('keeps the selection open and renders structured 400 row errors')`
   provides a generated-client rejection with `errors` and verifies the
   `upload-splits-error`, selected-file state, and retry availability.
5. `it('guards duplicate submissions and dismissal while an operation is pending')`
   defers the upload promise and verifies one request, pending state, disabled
   actions, and `update:busy(true/false)`.

Do not mount the Nuxt transaction page in a component test. Page integration and
navigation are covered by the functional tests below.

## Functional Tests

```gherkin
Background:
    Given the application is running
    And I am logged in as a user with "Editor" role

Scenario: Upload valid split rows, append them, and remove the zero balancing split
    Given I have a workspace with a transaction:
        | Payee          | Amount  | Category |
        | Paystub        | 1000.00 | Income   |
    And I am on the transactions detail page for that transaction
    When I upload a split workbook with these first-worksheet rows: # NEW
        | Category | Amount | Memo     |
        | Salary   | 800.00 | Base pay |
        | Bonus    | 200.00 | Annual   |
    Then the following splits are displayed:
        | Amount | Category | Memo     |
        | 800.00 | Salary   | Base pay |
        | 200.00 | Bonus    | Annual   |
    And the transaction is balanced

Scenario: Append uploaded splits to existing allocations and preserve workbook order
    Given I have a workspace with a transaction for payee "Paystub" having these splits:
        | Amount | Category | Memo          |
        | 1000.00 | Income  | Original row  |
        |  -25.00 | Tax     | Existing item |
    And I am on the transactions detail page for that transaction
    When I upload a split workbook with these first-worksheet rows: # NEW
        | Category  | Amount | Memo        |
        | Deduction | -50.00 | Healthcare  |
        | Bonus     | 100.00 | Performance |
    Then the following splits are displayed:
        | Amount  | Category  | Memo          |
        |  975.00 | Income    | Original row  |
        |  -25.00 | Tax       | Existing item |
        |  -50.00 | Deduction | Healthcare    |
        |  100.00 | Bonus     | Performance   |
    And the transaction is balanced

Scenario: Invalid workbook row changes no splits
    Given I have a workspace with a transaction for payee "Paystub" having these splits:
        | Amount | Category | Memo |
        | 1000.00 | Income  |      |
    And I am on the transactions detail page for that transaction
    When I upload a split workbook with these first-worksheet rows: # NEW
        | Category | Amount | Memo       |
        | Salary   | 900.00 | Valid row  |
        | Bonus    | nope   | Bad amount |
    Then the split upload error identifies row 3 and Amount # NEW
    And the following splits are displayed:
        | Amount  | Category | Memo |
        | 1000.00 | Income   |      |

Scenario: Ignore zero rows and all worksheets after the first
    Given I have a workspace with a transaction for payee "Paystub" having these splits:
        | Amount | Category | Memo |
        | 100.00 | Income   |      |
    And I am on the transactions detail page for that transaction
    When I upload a split workbook with these worksheets: # NEW
        | Worksheet | Category | Amount | Memo       |
        | First     | Ignored  |   0.00 | Zero split |
        | First     | Salary   |  40.00 | Base       |
        | Second    | Bonus    |  60.00 | Do not use |
    Then the following splits are displayed:
        | Amount | Category | Memo |
        |  60.00 | Income   |      |
        |  40.00 | Salary   | Base |
    And the transaction is balanced

Scenario: Download the split upload template
    Given I have a workspace with a transaction:
        | Payee   | Amount | Category |
        | Paystub | 100.00 | Income   |
    And I am on the transactions detail page for that transaction
    When I download the transaction split upload template # NEW
    Then an XLSX file named "yofi-transaction-splits-template.xlsx" is downloaded # NEW
    And its first worksheet contains only the headers "Category", "Amount", and "Memo" # NEW
```

## Open Questions

None. The workbook limits, formula behavior, no-importable-row behavior, and
validation presentation were resolved in [Design Decisions](#design-decisions).

## Completion Checklist

- [x] Every PRD acceptance criterion in scope is traceable to a section of this design.
- [x] Every section is present; untouched layers say so explicitly.
- [x] Every endpoint lists its tenant role, request/response types, and error statuses.
- [x] Every new exception has a status-code mapping.
- [x] No index, constraint, conversion, or data-provider capability is introduced; the Data section states that no Data integration tests apply.
- [x] Backward compatibility with the previous frontend is stated.
- [x] Every `data-test-id` used in Gherkin is covered by the functional step contract or existing split test IDs.
- [x] Gherkin scenarios put all `When` steps before `Then` steps.
- [x] No "TBD" or "to be finalized" text appears.
- [x] The Open Questions section is empty.
- [x] All relative links point to existing source or document paths.
- [x] The template author instruction block is not present.
