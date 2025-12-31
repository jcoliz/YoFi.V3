---
status: Draft
created: 2024-12-31
issue: Import Selection State Design Flaw
feature_slug: bank-import
related_docs:
  - IMPORT-SELECTION-STATE-FLAW-ANALYSIS.md
  - DESIGN-BANK-IMPORT-DATABASE.md
  - DESIGN-BANK-IMPORT-API.md
  - DESIGN-BANK-IMPORT-APPLICATION.md
  - DESIGN-BANK-IMPORT-FRONTEND.md
---

# Implementation Plan: Fix Import Selection State Design Flaw

## Problem Summary

The current implementation stores transaction selection state in **frontend session storage**, causing massive data loss for imports >50 transactions due to pagination. Only the first page's selections are tracked, resulting in unloaded transactions being incorrectly rejected.

**Example failure:**
- Upload 300 transactions
- Frontend loads page 1 (50 transactions), applies defaults
- User clicks "Import"
- **Result: 40 accepted, 260 rejected** (should be: 250 accepted, 50 rejected)

**Root cause:** Frontend-only state is fundamentally incompatible with paginated data.

**Solution:** Move selection state to database (`IsSelected` column), manage via API endpoints.

See [`IMPORT-SELECTION-STATE-FLAW-ANALYSIS.md`](IMPORT-SELECTION-STATE-FLAW-ANALYSIS.md) for complete analysis.

## Current Implementation Status

**What exists:**
- ✅ [`ImportReviewTransaction`](../../src/Entities/Models/ImportReviewTransaction.cs) entity (WITHOUT `IsSelected` field)
- ✅ [`ImportReviewTransactionDto`](../../src/Application/Dto/ImportReviewTransactionDto.cs) (WITHOUT `IsSelected` field)
- ✅ [`ImportController`](../../src/Controllers/ImportController.cs) with OLD signature: `CompleteReview([FromBody] IReadOnlyCollection<Guid> keys)`
- ✅ [`ImportReviewFeature`](../../src/Application/Features/ImportReviewFeature.cs) with OLD signature: `CompleteReviewAsync(IReadOnlyCollection<Guid> keys)`
- ❌ No `IsSelected` column in database
- ❌ No selection management endpoints (set-selection, select-all, deselect-all, summary)
- ❌ Frontend not implemented yet

**What needs to change:**
- Add `IsSelected` column to entity and database
- Add new DTOs: `ImportReviewSummaryDto`, `SetSelectionRequest`
- Add new endpoints: `POST /set-selection`, `POST /select-all`, `POST /deselect-all`, `GET /summary`
- **BREAKING CHANGE:** Remove `keys` parameter from `CompleteReview` endpoint
- Update feature methods to use database selection state
- Implement frontend with server-side state (no session storage)

## Orchestrator Implementation Plan

Follow [`docs/wip/IMPLEMENTATION-WORKFLOW.md`](../IMPLEMENTATION-WORKFLOW.md) for detailed workflow patterns. Steps correspond to workflow sections.

---

### Step 3: Update Entities

**Mode:** Code

**Instructions:**

> Update [`ImportReviewTransaction`](../../src/Entities/Models/ImportReviewTransaction.cs) entity to add `IsSelected` property. Update [`ImportReviewTransactionDto`](../../src/Application/Dto/ImportReviewTransactionDto.cs) to include `IsSelected`. Build and verify compilation.

**Checklist:**
- [ ] Add `IsSelected` property to `ImportReviewTransaction` entity (type: `bool`, default: `false`)
- [ ] Add XML documentation: "Indicates whether this transaction is selected for import. Set automatically based on DuplicateStatus when created, can be toggled by user."
- [ ] Add `IsSelected` parameter to `ImportReviewTransactionDto` record (type: `bool`)
- [ ] Build: `dotnet build`
- [ ] Run unit tests: `dotnet test tests/Unit`

**Files to modify:**
- `src/Entities/Models/ImportReviewTransaction.cs` - Add `IsSelected` property
- `src/Application/Dto/ImportReviewTransactionDto.cs` - Add `IsSelected` parameter

**Commit template:** `feat(bank-import): add IsSelected to ImportReviewTransaction entity`

---

### Step 4: Update Data Layer

**Mode:** Code

**Instructions:**

> Update [`ApplicationDbContext.cs`](../../src/Data/Sqlite/ApplicationDbContext.cs) configuration for `ImportReviewTransaction` to include `IsSelected` column. Add composite index `IX_ImportReviewTransactions_TenantId_IsSelected` for selection queries. Create migration. Verify with data integration tests.

**Checklist:**
- [ ] Update `OnModelCreating()` in ApplicationDbContext to configure `IsSelected` column
- [ ] Add composite index: `.HasIndex(t => new { t.TenantId, t.IsSelected })`
- [ ] Build: `dotnet build`
- [ ] Create migration: `.\scripts\Add-Migration.ps1 -Name "AddIsSelectedToImportReviewTransaction"`
- [ ] Review generated migration SQL
- [ ] Run data tests: `dotnet test tests/Integration.Data`
- [ ] Fix any failures

**Files to modify:**
- `src/Data/Sqlite/ApplicationDbContext.cs` - Add `IsSelected` configuration
- New migration file created in `src/Data/Sqlite/Migrations/`

**Commit template:** `feat(bank-import): add IsSelected column with index`

---

### Step 5: Data Integration Tests

**Mode:** Code

**Instructions:**

> Add data integration tests for `IsSelected` column behavior. Verify default value, updates, and index usage. Run tests until all pass.

**Checklist:**
- [ ] Add test file: `tests/Integration.Data/ImportReviewTransactionTests.cs` (if doesn't exist)
- [ ] Add test: `IsSelected_DefaultValue_IsFalse()`
- [ ] Add test: `IsSelected_CanBeUpdated_PersistsCorrectly()`
- [ ] Add test: `QueryByIsSelected_UsesIndex()` (verify query plan)
- [ ] Run tests: `dotnet test tests/Integration.Data`
- [ ] Iterate until all pass

**Files to modify:**
- `tests/Integration.Data/ImportReviewTransactionTests.cs`

**Commit template:** `test(integration): add IsSelected column tests`

---

### Step 6: Update Application Layer - Part 1 (New DTOs)

**Mode:** Code

**Instructions:**

> Create new DTOs: `ImportReviewSummaryDto` and `SetSelectionRequest`. Build and verify compilation.

**Checklist:**
- [ ] Create `src/Application/Dto/ImportReviewSummaryDto.cs` per design
- [ ] Create `src/Application/Dto/SetSelectionRequest.cs` per design
- [ ] Add XML documentation to both DTOs
- [ ] Build: `dotnet build`
- [ ] Run unit tests: `dotnet test tests/Unit`

**Files to create:**
- `src/Application/Dto/ImportReviewSummaryDto.cs`
- `src/Application/Dto/SetSelectionRequest.cs`

**Commit template:** `feat(bank-import): add selection management DTOs`

---

### Step 6: Update Application Layer - Part 2 (Feature Methods)

**Mode:** Code

**Instructions:**

> Update [`ImportReviewFeature`](../../src/Application/Features/ImportReviewFeature.cs):
> 1. Modify `ImportFileAsync` to set `IsSelected` based on `DuplicateStatus`
> 2. **BREAKING:** Change `CompleteReviewAsync()` to remove `keys` parameter and query `WHERE IsSelected = true`
> 3. Add new methods: `SetSelectionAsync`, `SelectAllAsync`, `DeselectAllAsync`, `GetSummaryAsync`
>
> Build and verify existing tests still compile (they will fail, but should compile).

**Checklist:**
- [ ] Update `ImportFileAsync`: Set `IsSelected = (DuplicateStatus == DuplicateStatus.New)` when creating entities
- [ ] **BREAKING:** Change `CompleteReviewAsync(IReadOnlyCollection<Guid> keys)` → `CompleteReviewAsync()` (no parameters)
- [ ] Update `CompleteReviewAsync` implementation to query `WHERE IsSelected = true` from database
- [ ] Add `SetSelectionAsync(IReadOnlyCollection<Guid> keys, bool isSelected)` method
- [ ] Add `SelectAllAsync()` method (bulk update using `ExecuteUpdateAsync`)
- [ ] Add `DeselectAllAsync()` method (bulk update using `ExecuteUpdateAsync`)
- [ ] Add `GetSummaryAsync()` method returning `ImportReviewSummaryDto`
- [ ] Add XML documentation to all new/modified methods
- [ ] Build: `dotnet build`
- [ ] **Expected:** Compilation succeeds, but tests will fail (controller signature changed)

**Files to modify:**
- `src/Application/Features/ImportReviewFeature.cs`

**Commit template:** `feat(bank-import): implement server-side selection management`

---

### Step 7: Unit Tests

**Mode:** Code

**Instructions:**

> Add unit tests for new selection management methods in `ImportReviewFeature`. Test default selection logic, bulk operations, summary calculations. Run tests until all pass.

**Checklist:**
- [ ] Add test file: `tests/Unit/ImportReviewFeatureTests.cs` (if doesn't exist)
- [ ] Add test: `ImportFileAsync_NewTransaction_IsSelectedTrue()`
- [ ] Add test: `ImportFileAsync_Duplicate_IsSelectedFalse()`
- [ ] Add test: `SetSelectionAsync_UpdatesDatabase()`
- [ ] Add test: `SelectAllAsync_SetsAllToTrue()`
- [ ] Add test: `DeselectAllAsync_SetsAllToFalse()`
- [ ] Add test: `GetSummaryAsync_ReturnsCorrectCounts()`
- [ ] Add test: `CompleteReviewAsync_OnlyAcceptsSelected()`
- [ ] Run tests: `dotnet test tests/Unit`
- [ ] Iterate until all pass

**Files to modify:**
- `tests/Unit/ImportReviewFeatureTests.cs`

**Commit template:** `test(unit): add selection management tests`

---

### Step 8: Update Controllers

**Mode:** Code

**Instructions:**

> Update [`ImportController`](../../src/Controllers/ImportController.cs):
> 1. **BREAKING:** Remove `[FromBody] IReadOnlyCollection<Guid> keys` parameter from `CompleteReview`
> 2. Add new endpoints: `SetSelection`, `SelectAll`, `DeselectAll`, `GetReviewSummary`
> 3. Update logging, XML docs, `[ProducesResponseType]` attributes
>
> Build and verify compilation. Existing controller tests will fail (expected).

**Checklist:**
- [ ] **BREAKING:** Change `CompleteReview([FromBody] IReadOnlyCollection<Guid> keys)` → `CompleteReview()` (no parameters)
- [ ] Update `CompleteReview` implementation: Remove `keys` parameter, call `importReviewFeature.CompleteReviewAsync()`
- [ ] Add `SetSelection([FromBody] SetSelectionRequest request)` endpoint
- [ ] Add `SelectAll()` endpoint
- [ ] Add `DeselectAll()` endpoint
- [ ] Add `GetReviewSummary()` endpoint
- [ ] Add XML documentation to all new/modified methods
- [ ] Add `[ProducesResponseType]` attributes
- [ ] Add LoggerMessage methods for new endpoints
- [ ] Build: `dotnet build`
- [ ] **Expected:** Compilation succeeds, controller tests will fail (endpoint contracts changed)

**Files to modify:**
- `src/Controllers/ImportController.cs`

**Commit template:** `feat(bank-import): add selection management endpoints`

---

### Step 8.5: Regenerate API Client

**Mode:** Code

**Instructions:**

> Regenerate TypeScript API client to include new endpoints and updated DTOs. Verify generation succeeded.

**Checklist:**
- [ ] Regenerate client: `pwsh -File ./scripts/Generate-ApiClient.ps1`
- [ ] Verify new methods in `src/FrontEnd.Nuxt/app/utils/apiclient.ts`:
  - `setSelection(request: SetSelectionRequest)`
  - `selectAll()`
  - `deselectAll()`
  - `getReviewSummary()`
  - `completeReview()` (NO parameters)
- [ ] Verify `ImportReviewTransactionDto` includes `isSelected: boolean`
- [ ] Verify `ImportReviewSummaryDto` generated correctly
- [ ] Build frontend: `pnpm run build` (from FrontEnd.Nuxt/)
- [ ] **Expected:** Frontend build may fail if import page already exists with old API

**Files to verify:**
- `src/FrontEnd.Nuxt/app/utils/apiclient.ts` (auto-generated)

**Commit template:** `build(bank-import): regenerate API client with selection endpoints`

---

### Step 9: Controller Integration Tests

**Mode:** Code

**Instructions:**

> Update existing controller integration tests for breaking changes. Add new tests for selection management endpoints. This is the PRIMARY test layer - aim for 60-70% of acceptance criteria here.

**Checklist:**
- [ ] **FIX BREAKING CHANGE:** Update `CompleteReview` test to remove `keys` parameter
- [ ] Add test: `SetSelection_ValidRequest_Returns204()`
- [ ] Add test: `SetSelection_EmptyKeys_Returns400()`
- [ ] Add test: `SelectAll_SetsAllSelected_Returns204()`
- [ ] Add test: `DeselectAll_SetsAllDeselected_Returns204()`
- [ ] Add test: `GetReviewSummary_ReturnsCorrectCounts_Returns200()`
- [ ] Add test: `CompleteReview_OnlyAcceptsSelected_AcrossAllPages()` ← **KEY TEST** verifying pagination fix
- [ ] Add test: `SetSelection_ViewerRole_Returns403()` (authorization)
- [ ] Add test: `CompleteReview_TenantIsolation_CannotAccessOtherTenant()` (security)
- [ ] Run tests: `dotnet test tests/Integration.Controller`
- [ ] Iterate until all pass

**Files to modify:**
- `tests/Integration.Controller/ImportControllerTests.cs`

**Commit template:** `test(integration): add selection management API tests`

---

### Step 10: Frontend Implementation

**Mode:** Code

**Instructions:**

> Implement frontend import page per [`DESIGN-BANK-IMPORT-FRONTEND.md`](DESIGN-BANK-IMPORT-FRONTEND.md). **CRITICAL:** Remove all session storage code. Use server-side state from API. Add `data-test-id` to all interactive elements.

**Checklist:**
- [ ] Review `src/FrontEnd.Nuxt/.roorules` for patterns
- [ ] Implement `src/FrontEnd.Nuxt/app/pages/import.vue` page
- [ ] **Remove all session storage code** (no `sessionStorage.setItem`, `.getItem`)
- [ ] Render checkboxes from `transaction.isSelected` (server state)
- [ ] On checkbox change: Call `setSelection({ keys: [key], isSelected: newValue })`, then refresh page
- [ ] On "Select All": Call `selectAll()`, then refresh page
- [ ] On "Deselect All": Call `deselectAll()`, then refresh page
- [ ] On page load: Call `getReviewSummary()` to get selected count for button state
- [ ] On "Import" click: Call `completeReview()` (NO parameters)
- [ ] Add `data-test-id` to all buttons, checkboxes, inputs
- [ ] Format: `pnpm format` (from FrontEnd.Nuxt/)
- [ ] Lint: `pnpm lint` (from FrontEnd.Nuxt/)
- [ ] Build: `pnpm run build` (from FrontEnd.Nuxt/)

**Key implementation notes:**
- **NO local state tracking** - Render from API response
- **Refresh pattern** - After selection changes, refresh current page to show updated checkboxes
- **Summary for button state** - Use `summary.selectedCount > 0` to enable/disable Import button

**Files to create/modify:**
- `src/FrontEnd.Nuxt/app/pages/import.vue`
- Supporting components if needed

**Commit template:** `feat(bank-import): implement import page with server-side selection`

---

**Note:** Steps 10.4 and beyond (Functional Tests, Documentation, Wrap-up) are deferred. Return to main implementation workflow after Step 10 for those phases.

---

## Implementation Summary

**Status:** Not yet implemented

**When complete, add:**
- Test distribution counts
- Breaking changes summary
- Key technical decisions made during implementation
- Any deviations from original design
- Known limitations or follow-up work needed

**Mode:** Code

**Instructions:**

> Ensure new frontend doesn't break existing functionality. Run all functional tests.

**Checklist:**
- [ ] **Ask user** to run: `.\scripts\Start-LocalDev.ps1`
- [ ] **Wait for confirmation** app is running
- [ ] Run existing tests: `dotnet test tests/Functional`
- [ ] **If all pass:** Proceed to Step 10.5
- [ ] **If failures:** Fix frontend breaking changes immediately
- [ ] Re-run tests until all pass

**Commit template (if fixes needed):** `fix(bank-import): resolve functional test failures`
