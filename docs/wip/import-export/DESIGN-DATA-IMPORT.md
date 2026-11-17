---
status: Approved
related_docs:
  - PRD-TENANT-DATA-ADMIN.md
  - DESIGN-TENANT-DATA-ADMIN.md
  - DESIGN-DATA-IMPORT-PAYEES.md
  - DESIGN-NATIVE-PAYEE-EXPORT-IMPORT.md
  - DESIGN-MANAGE-WORKSPACE.md
ado: Story 2221, Scenario 2233, Scenario 2279
---

# Technical Design Document: Import Data with Automatic Detection

## Scope

This design covers Story 2221, **User - Import Data with Automatic Detection**, including:

- Scenario 2233, already implemented: original YoFi `Payee` worksheets.
- Scenario 2279: native YoFi.V3 payee matching-rule worksheets.
- Original YoFi transaction and split worksheets.
- Native YoFi.V3 `Transactions` and `Splits` worksheets.

The design expands the existing shared [`TenantDataImportFeature`](../../../src/Application/Features/TenantDataImportFeature.cs) and [`ImportDataPane.vue`](../../../src/FrontEnd.Nuxt/app/components/workspace/ImportDataPane.vue). Scenario 2233 is retained as the create-only original payee behavior. It is not redesigned or changed to native Key-based update behavior.

In scope:

- Format detection for native YoFi.V3 and original YoFi workbooks.
- Automatic discovery of registered supported worksheets.
- Preview and confirmation for one or more supported worksheets in the same workbook.
- Original YoFi payees and transactions.
- Native YoFi.V3 payee matching rules and transactions, including splits.
- Key-based native upsert and legacy-key handling for original transactions.
- Dependency-safe transaction-before-splits persistence.
- Worksheet-level atomicity and explicit per-workbook results.

Out of scope:

- Original YoFi budgets, receipts, category maps, or other entity types.
- Native data types not already exported and registered.
- Bank OFX/QFX review behavior in [`ImportController`](../../../src/Controllers/ImportController.cs).
- Deletion of omitted native records. Native imports update/create only records represented in the workbook; omitted records are unchanged except that omitted splits for an imported native transaction are reconciled as described below.
- Import templates and asynchronous import for large workbooks.
- Data type discovery at runtime. The implementation uses a concrete list of registered format detectors and worksheet handlers, as permitted by the PRD.

## Architectural Considerations

The existing extensible import pipeline remains unchanged: format detectors identify the source format, worksheet handlers map one sheet to one V3 data type, and `TenantDataImportFeature` coordinates preview and confirmed persistence. This design adds native and original transaction handlers and a native format detector; no new architectural layer or third-party dependency is introduced.

The `ClosedXML` workbook parser already used by preview and export remains the workbook implementation. Workbook parsing, formula rejection, and the 10 MiB request limit follow the existing tenant administration contract.

For a multi-worksheet workbook, handlers execute in dependency order: payees or rules may execute independently; transactions execute before their dependent splits. `TenantDataImportFeature` owns ordering through an explicit handler dependency declaration. Each worksheet remains a separate database transaction. Previously committed worksheets remain committed if a later worksheet fails; the response for a persistence failure reports the failure explicitly rather than returning a success-shaped result.

No entity, migration, or `IDataProvider` capability is added. All write operations use the existing tenant-scoped `IDataProvider`, `ITenantProvider`, and `ITenantRepository` APIs.

## Design Decisions

| Decision | Alternatives considered | Rationale |
| -------- | ----------------------- | --------- |
| Recognize native format only from a valid `Metadata` `Format` / `YoFi.V3` marker. | Infer native format from `Transactions` or `PayeeMatchingRules` names. | Rejected: names are not authoritative and can appear in ambiguous or original workbooks. |
| Recognize original format from original worksheet signatures, not guessed from any arbitrary sheet. | Auto-detect solely by one of the known property headers. | The original detector accepts only recognized original signatures and one recognized signature must be present. |
| All transaction and split categories from uploaded workbooks are sanitized with `CategoryHelper.SanitizeCategory` before preview counts and persistence. | Preserve original workbook category text unchanged. | Native V3 and original legacy imports both feed V3 entities, so workbook source format must not bypass V3 category normalization or hierarchy formatting. |
| Original payees keep create-only behavior. | Convert original payees to native-style Key upsert. | Scenario 2233 is already implemented and explicitly defines create-only duplicate behavior. |
| Native payee and transaction rows use GUID Key upsert. | Always create native rows. | Native exports include stable Keys; re-import must update the same records rather than duplicate them. |
| Original transactions use `BankReference`/generated legacy signature as `ExternalId`; matching existing records are skipped, not updated. | Update matching existing transactions. | This preserves legacy import semantics and the user's selected behavior: original exports are append/import records, while native workbooks provide stable Key upsert. |
| Import transactions before Splits. | Import the two worksheets independently or splits before parents. | Parent Key/row mapping and referential-integrity validation require transactions to exist or be staged first. |
| A native transaction's split set is authoritative for the imported transaction. | Merge without deleting omitted splits. | A native workbook represents the transaction graph; omitted split rows must not leave stale allocations after a split-bearing transaction is updated. |
| Each worksheet is atomic. | One transaction across the whole workbook or row-by-row saves. | Matches the existing import architecture and PRD: one worksheet is the smallest all-or-nothing unit. |
| Unsupported worksheets are ignored and reported. | Silently ignore or import by header similarity. | Preview must make the user's consent and unsupported content explicit. |
| Splits reference native transactions by the GUID `TransactionKey`; original splits reference original integer `TransactionID`. | Store internal database IDs in native worksheets or expose both formats uniformly. | Native format is already specified with GUID Key references; original format is identified by the legacy implementation's integer ID. |

## Entities

No entity changes are required.

The existing [`Transaction`](../../../src/Entities/Models/Transaction.cs), [`Split`](../../../src/Entities/Models/Split.cs), and [`PayeeMatchingRule`](../../../src/Entities/Models/PayeeMatchingRule.cs) entities provide all required fields. Imported original transactions map `BankReference` or the legacy-generated signature to `Transaction.ExternalId`; `Transaction.Source` remains the native source value or `null`; `ReceiptUrl` is never imported. Every transaction and split category imported from either source format is sanitized through `CategoryHelper.SanitizeCategory` before validation and persistence.

## Data

No `DbSet`, migration, index, constraint, conversion, or `IDataProvider` method changes are required.

The import handlers use existing tenant-scoped queries and `SaveChangesAsync` transactions:

- Native payee updates query `PayeeMatchingRule` rows by `TenantId` and `Key`.
- Native transaction updates query `Transaction` rows by `TenantId` and `Key`, including `Splits`.
- Original duplicate checks query `Transaction` rows by `TenantId` and `ExternalId`.
- Every created entity uses `ITenantProvider.CurrentTenant.Id` at the persistence boundary.

The database remains authoritative for tenant isolation and transaction commit/rollback; this design adds no data-integrity constraint that cannot be enforced in Application code.

## Data Integration Tests

No Data integration tests are added: no schema, migration, index, constraint, conversion, or provider capability changes are made.

## Application Features

### Existing public feature contract

[`TenantDataImportFeature`](../../../src/Application/Features/TenantDataImportFeature.cs) keeps these public methods:

```csharp
TenantDataImportPreviewDto Preview(byte[] file)
Task<TenantDataImportResultDto> ImportAsync(
    byte[] file,
    string expectedSha256,
    CancellationToken cancellationToken = default)
```

Update `Preview` and `ImportAsync` so format detection and handler ordering support more than Scenario 2233. Their existing exceptions and digest behavior remain unchanged:

- `ValidationException` for unreadable/non-XLSX content or a preview digest mismatch.
- `TenantDataImportValidationException` for failed detection or worksheet validation.
- `OperationCanceledException` for caller cancellation.
- Persistence exceptions are allowed to propagate; controller middleware maps them to an explicit 500 or existing data-conflict response.

The existing [`ImportWorksheetPreviewDto`](../../../src/Application/Dto/TenantDataImportDto.cs), `TenantDataImportPreviewDto`, and `TenantDataImportResultDto` shapes are retained. Each result is worksheet-based, not entity-based, so native `Transactions` and `Splits` remain separately visible.

### Format and handler interfaces

The existing [`IWorkbookImportFormatDetector`](../../../src/Application/Services/IWorkbookImportFormatDetector.cs) and [`IImportWorksheetHandler`](../../../src/Application/Services/IImportWorksheetHandler.cs) remain the extension points.

Add `HandlerDependencyOrder` and `DependencyWorksheetNames` to `IImportWorksheetHandler` as the explicit optional ordering metadata:

```csharp
int DependencyOrder { get; }
IReadOnlyCollection<string> RequiresWorksheetNames { get; }
```

This is implementation metadata, not a public DTO. The coordinator sorts handlers by `DependencyOrder`, rejects a dependent handler when a required worksheet is absent, and blocks it when the prerequisite worksheet is not successfully committed. The existing payee handlers use order 100 and no dependencies; native/original transaction handlers use order 200; split handlers use order 300.

### Format detectors

Add `NativeYoFiV3WorkbookFormatDetector`:

- `FormatId`: `yofi.v3`
- Detects only a worksheet named `Metadata` with case-insensitive `Name` / `Value` headers and exactly one `Format` row whose value is `YoFi.V3`.
- Missing, malformed, unknown, or ambiguous format markers do not identify a native workbook.
- Extra metadata rows are ignored.

Expand `OriginalYoFiWorkbookFormatDetector`:

- `FormatId`: `original-yofi`
- Detects when at least one recognized original signature is present:
  - `Payee` with `Name` and `Category` columns.
  - `Transaction` with required legacy transaction-export headers.
  - `Split` with required legacy split-export headers.
- Detection is signature-based and never uses a native `Metadata` marker. If a native marker exists and is recognized, the native detector wins; multiple recognized format detectors throw the existing ambiguous-format `ValidationException`.

### Worksheet handlers

Existing `OriginalYoFiPayeeWorksheetHandler` is retained unchanged in behavior.

Add:

- `NativeYoFiPayeeWorksheetHandler`, `FormatId = yofi.v3`, `WorksheetName = PayeeMatchingRules`, `DataType = PayeeMatchingRules`.
- `NativeYoFiTransactionWorksheetHandler`, `FormatId = yofi.v3`, `WorksheetName = Transactions`, `DataType = Transactions`.
- `NativeYoFiSplitWorksheetHandler`, `FormatId = yofi.v3`, `WorksheetName = Splits`, `DataType = Splits`.
- `OriginalYoFiTransactionWorksheetHandler`, `FormatId = original-yofi`, `WorksheetName = Transaction`, `DataType = Transactions`.
- `OriginalYoFiSplitWorksheetHandler`, `FormatId = original-yofi`, `WorksheetName = Split`, `DataType = Splits`.

Native split import is not a standalone transaction: it is recognized and previewed only when the native `Transactions` worksheet is present and `Transactions` is committed successfully.

### Native worksheet mapping

Use the contracts already defined in [Native Payee Export and Import](DESIGN-NATIVE-PAYEE-EXPORT-IMPORT.md).

Native payee fields are:

- `Key`: optional GUID; blank creates, matching Key updates, unknown Key creates.
- `PayeePattern`: required text.
- `PayeeIsRegex`: required boolean.
- `Category`: required text, sanitized and validated.
- Managed `CreatedAt`, `ModifiedAt`, `LastUsedAt`, and `MatchCount` fields are ignored.

Native transaction columns are exactly `Key`, `Date`, `Payee`, `Amount`, `Source`, `ExternalId`, `Memo`, `Category`.

For native transaction preview, `Key` may be blank or a GUID. Blank and unknown Keys create; matching Keys update; duplicate nonblank Keys block the worksheet. `Date`, `Payee`, and `Amount` map to `TransactionEditDto`; `Source`, `ExternalId`, and `Memo` map directly. `Category` is handled as either a single split category or the reserved split marker `SPLIT`. A transaction category is considered the `SPLIT` marker only after trimming and case-insensitive comparison; all other category text is passed through `CategoryHelper.SanitizeCategory` before `TransactionEditDto` and split validation. The preview reports the sanitized category that will be stored and a row is invalid if the sanitized category exceeds 200 characters.

Native split columns are exactly `Key`, `TransactionKey`, `Amount`, `Category`, `Memo`, `Order`.

Split validation is worksheet-wide:

- `TransactionKey` must reference a row in `Transactions`.
- A split row may use only a transaction whose native `Category` cell is `SPLIT`.
- `Key` may be blank or GUID; blank creates, matching existing split Key updates, unknown Key creates.
- Duplicate nonblank split Keys block the worksheet.
- `Amount`, `Category`, `Memo`, and `Order` follow `SplitEditDtoValidator`; `Category` is sanitized with `CategoryHelper.SanitizeCategory` before validation and persistence.
- Split amounts must sum to the transaction amount. This is a blocking validation error, not the existing split UI warning, because a native exported transaction graph must remain referentially and financially intact.
- `Order` must be a nonnegative integer and unique within a transaction.
- Repeated `TransactionKey`/`Key` combinations are invalid.

Native transaction persistence reconciles splits:

- Category not `SPLIT`: update or create one split with amount equal to the transaction amount, `Category` sanitized from `Category`, memo `null`, and `Order=0`; remove other splits.
- Category `SPLIT`: update/create rows from `Splits`, preserving matching GUID split Keys and creating unknown/blank Keys; remove split rows not represented by the worksheet for that transaction.
- An existing transaction's omitted splits are removed only when it is present in a native transaction sheet and its complete native split graph can validate and commit.
- An existing transaction without a `Splits` row and Category blank creates or reconciles to one uncategorized split.

### Original worksheet mapping

Original payees remain as implemented: `Name` → regex/substr pattern, `Category` → rule category, `ID` and `Selected` ignored.

Original transaction columns are the legacy export shape used by `C:\Source\jcoliz\yofi\YoFi.Core\Repositories\TransactionRepository.cs`:

| Header | V3 mapping | Handling |
| ------ | ---------- | -------- |
| `ID` | — | Ignored legacy integer identity. |
| `Timestamp` | `Date` | Required date/time; date portion maps to `DateOnly`. |
| `Payee` | `Payee` | Required and trimmed; existing transaction validator rules apply. |
| `Amount` | `Amount` | Required decimal; must be nonzero and <= two decimal places. |
| `Category` | Single split category | Optional. Blank means one uncategorized split unless the `Split` worksheet supplies rows. Nonblank category text is sanitized with `CategoryHelper.SanitizeCategory` before preview and persistence. |
| `Memo` | `Memo` | Optional. |
| `BankReference` | `ExternalId` | Optional; if blank, generate the legacy signature described below. |
| `ReceiptUrl` | — | Ignored; V3 receipt data is not imported. |
| `Hidden`, `Imported`, `Selected` | — | Ignored if present in older exports. |

Header matching is case-insensitive and trims whitespace. Required headers are exactly one of `Timestamp`, `Payee`, and `Amount`. `ID` is ignored. Extra columns are reported as ignored.

Original split columns are: `ID`, `Amount`, `Category`, `Memo`, `TransactionID`.

- `ID` and internal legacy IDs are ignored except `TransactionID` is used as the link key.
- `TransactionID` must match exactly one `ID` in the original `Transaction` worksheet.
- `Amount`, `Category`, and optional `Memo` map to splits. Category text is sanitized with `CategoryHelper.SanitizeCategory` before `SplitEditDto` validation and persistence.
- A transaction with split rows gets all those rows and no category-only split. A transaction with no split rows gets one split from its `Category`, memo `null`, amount equal to its transaction amount.
- Split amounts must sum to the transaction amount; otherwise the worksheet validation fails.

### Original duplicate and legacy-ID behavior

For each original transaction:

1. Use trimmed `BankReference` when nonblank. Otherwise generate the original legacy signature:
   `/<Payee>/<Amount:C2>/<MM/dd/yyyy>` (that is, `/` + Payee or `Null` + `/` + two-decimal currency text + `/` + invariant short date), encoded as UTF-32 and hashed with MD5 using the original implementation, then represented as uppercase hexadecimal.
2. Map the supplied or generated value to `Transaction.ExternalId`.
3. If a transaction already exists in the authorized tenant with that `ExternalId`, skip that source row and count it in `SkippedRecordCount` with a stable duplicate issue code.
4. Otherwise create a fresh V3 transaction Key and associated splits.

This is append-only for original transactions and does not update an existing transaction. Re-importing an unchanged original workbook skips all duplicate records; blank/old rows can still skip consistently when the generated signature matches an existing transaction. Legacy `ID` values are never copied or used as V3 Key values.

### Persistence service

Add methods to [`DataImportService`](../../../src/Application/Services/DataImportService.cs):

```csharp
Task<int> CreateOriginalTransactionsAsync(
    IReadOnlyCollection<OriginalTransactionImportDto> transactions,
    CancellationToken cancellationToken = default)

Task<NativeTransactionImportResultDto> UpsertNativeTransactionsAsync(
    IReadOnlyCollection<NativeTransactionImportDto> transactions,
    IReadOnlyCollection<NativeSplitImportDto> splits,
    CancellationToken cancellationToken = default)
```

`CreateOriginalTransactionsAsync` validates all DTOs first, rejects existing `ExternalId` matches in tenant scope, stages the remaining transaction graphs, and commits once. Duplicate rows are pre-filtered and returned by preview rather than staged.

`UpsertNativeTransactionsAsync` validates and stages all native rows and split graphs, queries tenant-scoped existing records, updates matching transaction/split entities, creates missing entities with fresh Keys, reconciles native split sets, and saves one transaction. The return counts created and updated transactions and splits.

The service invalidates transaction list caches if one exists, and invalidates matching-rule caches after rule writes. It does not expose tenant or internal IDs to handlers.

New application records:

```csharp
public record OriginalTransactionImportDto(
    DateOnly Date,
    decimal Amount,
    string Payee,
    string? Memo,
    string? BankReference,
    IReadOnlyCollection<OriginalSplitImportDto> Splits);

public record OriginalSplitImportDto(decimal Amount, string Category, string? Memo);

public record NativeTransactionImportDto(
    Guid? Key,
    DateOnly Date,
    decimal Amount,
    string Payee,
    string? Memo,
    string? Source,
    string? ExternalId,
    string? Category);

public record NativeSplitImportDto(
    Guid? Key,
    Guid TransactionKey,
    decimal Amount,
    string Category,
    string? Memo,
    int Order);

public record NativeTransactionImportResultDto(int CreatedTransactions, int UpdatedTransactions,
    int CreatedSplits, int UpdatedSplits, int SkippedTransactions);
```

All public Feature methods validate inputs where DTO validation applies. Worksheet handlers sanitize transaction and split category values with `CategoryHelper.SanitizeCategory` before constructing edit DTOs, and validate the sanitized values through the existing transaction/split validators. `DataImportService` throws `ValidationException` for unsanitized or invalid category values, a worksheet that was not validated before persistence, and unsatisfied referential integrity. `TenantDataImportFeature` guarantees handlers receive validated worksheet objects.

## Application Integration Tests

Extend [`TenantDataImportFeatureTests`](../../../tests/Integration.Application/TenantDataImportFeatureTests.cs) with:

- Native format with a valid `Metadata` marker preview/imports native transactions and splits in dependency order.
- Native format is authoritative: a native workbook containing a `Payee` worksheet does not use the original payee handler.
- Native transaction Key updates the existing tenant transaction and reconciles single/multiple splits without changing omitted transactions.
- Blank or unknown native transaction/split Keys create fresh records in the authorized tenant.
- Duplicate and malformed native GUID Keys block the worksheet and persist nothing.
- Native split TransactionKey mismatch, split sum mismatch, duplicate Order, and non-SPLIT split rows block persistence.
- Original transaction/split workbook creates transactions and splits, strips legacy IDs, and maps nonblank `BankReference` to `ExternalId`.
- Blank original `BankReference` generates the same legacy signature consistently and skips an existing duplicate.
- Original re-import skips existing ExternalId matches and creates no duplicate transactions.
- Original split `TransactionID` must match a transaction `ID`; orphan splits block the worksheet.
- Original and native transaction/split categories are sanitized consistently before preview and persistence, including hierarchy whitespace and whitespace consolidation; native `split` marker recognition is case-insensitive but stored categories are never persisted as `SPLIT`.
- Original and native worksheets in the same workbook execute transactions before splits and preserve worksheet-level rollback boundaries.
- Non-Owner or cross-tenant test setup cannot see or modify records.

## Controllers

The existing [`TenantDataAdminController`](../../../src/Controllers/TenantDataAdminController.cs) routes and response shapes are unchanged:

| Method | Route | Tenant role | Request body | Success response | Error responses |
| ------ | ----- | ----------- | ------------ | ---------------- | --------------- |
| `POST` | `/api/tenant/{tenantKey:guid}/admin/import/preview` | Owner | multipart `file` | `200 TenantDataImportPreviewDto` | `400 ProblemDetails` unreadable/ambiguous/unsupported workbook |
| `POST` | `/api/tenant/{tenantKey:guid}/admin/import` | Owner | multipart `file` plus `expectedSha256` | `200 TenantDataImportResultDto` | `400 ProblemDetails` digest mismatch or validation; `500 ProblemDetails` persistence failure |

Keep the existing tenant middleware and Owner policy. `tenantKey` remains route-established; workbook legacy IDs, native Keys, and ExternalId values are never trusted as tenant identity.

No new `[LoggerMessage]` event IDs are needed for transport; extend existing preview/import methods to log detected format and outcome counts for each recognized data type without logging row values or workbook contents.

The TypeScript client is regenerated in Front End phase 8. Existing method names `previewImport` and `importData` remain. The generated preview/result DTO fields become usable by the generalized component; no hand edits are made to `apiclient.ts`.

## Front End

Expand [`ImportDataPane.vue`](../../../src/FrontEnd.Nuxt/app/components/workspace/ImportDataPane.vue); do not add a second per-format import page.

JSDoc summary:

```ts
/**
 * Previews and confirms automatic tenant-data imports from original or native YoFi workbooks.
 */
```

Keep props:

```ts
workspace: TenantRoleResultDto
```

Keep emits:

```ts
imported: [result: TenantDataImportResultDto]
'update:busy': [value: boolean]
```

UI behavior:

- File copy is generalized to "Choose a YoFi .xlsx workbook".
- Preview identifies `FormatId` (`YoFi.V3` or `Original YoFi`) and renders one result section per recognized worksheet.
- Unsupported worksheets and extra columns appear in dedicated ignored lists.
- Original payee preview retains create-only warning.
- Native payee preview explains matching-Key update, blank/unknown-Key create, and unchanged omitted rules.
- Transaction previews show valid, skipped, and error counts plus row/field issues.
- Native split preview is subordinate to transaction preview and is not independently confirmed.
- The confirmation button is disabled unless `preview.canImport` is true and at least one supported worksheet is present.
- Confirmation text summarizes all recognized worksheets and their expected create/update/skip counts.
- Success renders all `result.worksheets` rows and emits `imported` once.
- Validation or digest mismatch clears the preview and directs the user to preview again.
- Persistence/transport errors remain in confirmation or problem-detail UI; no success summary is shown.
- Changing the file clears preview, result, confirmation state, and previous errors.

### Test IDs

| data-test-id | Element | Purpose |
| ------------ | ------- | ------- |
| `import-data-card` | Import card | Existing import card root. |
| `import-data-title` | Card title | Existing title. |
| `import-file-dropzone` | Drop zone | Existing drop target. |
| `import-file-input` | File input | Existing workbook chooser. |
| `import-selected-file` | Selected file text | Existing selected file content. |
| `preview-import-button` | Preview button | Existing preview action. |
| `import-preview` | Preview region | Existing preview root. |
| `import-format` | Detected format text | Identifies native or original format. |
| `import-worksheet-preview-list` | Preview list | Groups recognized worksheet summaries. |
| `import-worksheet-preview-{dataType}` | One worksheet summary | Displays a specific worksheet preview. |
| `import-row-issues` | Issue list | Existing row/field issue region. |
| `download-import-errors-button` | CSV report action | Downloads all blocking/skipped issues for the preview. |
| `import-ignored-columns` | Ignored column text | Lists extra columns. |
| `import-ignored-worksheets` | Ignored sheet text | Lists unsupported sheets. |
| `import-result` | Import result | Existing per-worksheet success summary. |
| `import-data-button` | Confirmation action | Confirms the generalized workbook import. |
| `import-data-confirm-modal` | Confirmation modal | Reviews create/update/skip scope. |
| `import-data-confirm-button` | Confirm import | Executes import. |
| `import-data-cancel-button` | Cancel import | Dismisses confirmation. |

Existing payee-specific test IDs may remain only for compatibility, but new Gherkin-facing UI work uses the generalized names above.

## Component Tests

No component test file is changed until implementation of this design begins; the implementation phase must add focused tests only for code added for this design.

Required component behavior contracts:

- `ImportDataPane` renders native and original format labels correctly.
- The confirmation action enables only when the preview has supported worksheets and no validation errors.
- The pane summarizes transactions and splits separately and never confirms a native split sheet without transactions.
- Original and native payee warnings differ and are accurate.
- Changing files clears preview/result/confirmation state.
- Upload, preview, confirmation, digest mismatch, and persistence failures enter their documented UI states.
- Ignored worksheets and columns are reported.
- CSV error download includes only preview issue rows and formula-safe values.

## Functional Tests

Prioritized Gherkin scenarios for the complete Story 2221 scope:

```gherkin
Feature: Automatic data import
  As a workspace owner
  I want to preview and import supported data from an XLSX workbook
  So that I can restore or transfer YoFi data without selecting entity types manually

  Background:
    Given I am logged in
    And I am on the workspace management page # NEW

  @category:story-2221
  Rule: Original YoFi payees are imported

    @category:scenario-2233
    @priority:1
    Scenario: Import original YoFi Payees into workspace
      When I upload a file containing original YoFi Payees
      And I preview and confirm the upload
      Then I can see many payee matching rules

  @category:story-2221
  Rule: Native YoFi workbooks are detected automatically

    @priority:2
    Scenario: Import native payee matching rules
      Given I have a native YoFi payee workbook # NEW
      When I upload a native YoFi workbook # NEW
      And I preview and confirm the upload
      Then I should see the detected format "YoFi.V3" # NEW
      And I should see the payee worksheet preview # NEW
      And I can see the imported payee matching rules # NEW

  @priority:3
  Scenario: Import native transactions and their splits
    Given I have a native YoFi transaction workbook with one split transaction # NEW
    When I upload a native YoFi workbook # NEW
    And I preview and confirm the upload
    Then I should see the Transactions worksheet preview # NEW
    And I should see the Splits worksheet preview # NEW
    And the imported transaction should appear in the transaction list # NEW
    And the transaction should have the sanitized imported split category # NEW

  @priority:4
  Scenario: Re-import native transactions updates matching records
    Given I have imported a native YoFi transaction workbook # NEW
    And I change the native transaction and split data in the workbook # NEW
    When I upload the native YoFi workbook again # NEW
    And I preview and confirm the upload
    Then I should see one existing transaction updated # NEW
    And I should see the updated split allocation # NEW
    And no duplicate native transaction should be created # NEW

  @category:story-2221
  Rule: Original YoFi transactions are imported safely

    @priority:5
    Scenario: Import original transactions and skip duplicate records
      Given I have an original YoFi workbook with transactions and splits # NEW
      When I upload an original YoFi workbook # NEW
      And I preview and confirm the upload
      And I upload the same original YoFi workbook again # NEW
      And I preview and confirm the upload
      Then I should see the original transaction records in the transaction list # NEW
      And I should see the sanitized original split categories # NEW
      And duplicate original transactions should be skipped # NEW
```

The following new steps are required; steps not marked `# NEW` reuse the existing step catalog:

- `Given I am on the workspace management page` — `# NEW`
- `Given I have a native YoFi payee workbook` — `# NEW`
- `Given I have a native YoFi transaction workbook with one split transaction` — `# NEW`
- `Given I have imported a native YoFi transaction workbook` — `# NEW`
- `Given I change the native transaction and split data in the workbook` — `# NEW`
- `Given I have an original YoFi workbook with transactions and splits` — `# NEW`
- `When I upload a native YoFi workbook` — `# NEW`
- `When I upload an original YoFi workbook` — `# NEW`
- `When I upload the native YoFi workbook again` — `# NEW`
- `When I upload the same original YoFi workbook again` — `# NEW`
- `Then I should see the detected format {format}` — `# NEW`
- `Then I should see the payee worksheet preview` — `# NEW`
- `Then I can see the imported payee matching rules` — `# NEW`
- `Then I should see the Transactions worksheet preview` — `# NEW`
- `Then I should see the Splits worksheet preview` — `# NEW`
- `Then the imported transaction should appear in the transaction list` — `# NEW`
- `Then the transaction should have the sanitized imported split category` — `# NEW`
- `Then I should see one existing transaction updated` — `# NEW`
- `Then I should see the updated split allocation` — `# NEW`
- `Then no duplicate native transaction should be created` — `# NEW`
- `Then I should see the original transaction records in the transaction list` — `# NEW`
- `Then I should see the sanitized original split categories` — `# NEW`
- `Then duplicate original transactions should be skipped` — `# NEW`

## Open Questions



## Completion Checklist

- [ ] Every PRD acceptance criterion in scope is traceable to a section of this design.
- [ ] Every section is present; untouched layers say so explicitly.
- [ ] Every endpoint lists its tenant role, request/response types, and error statuses.
- [ ] Every new exception has a status-code mapping.
- [ ] Every index, constraint, conversion, and new data-provider capability in Data has a Data Integration Test.
- [ ] Backward compatibility with the previous frontend is stated; breaking changes are itemized.
- [ ] Every `data-test-id` used in Gherkin appears in the Test IDs table.
- [ ] Gherkin scenarios have all `When` steps before `Then` steps and reuse catalog steps where possible.
- [ ] No placeholder wording or deferred implementation list appears anywhere.
- [ ] Open Questions is empty, or every entry has a proposed answer.
