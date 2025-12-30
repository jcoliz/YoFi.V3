---
status: Approved
layer: Frontend
parent: DESIGN-BANK-IMPORT.md
created: 2025-12-28
related_docs:
  - DESIGN-BANK-IMPORT.md
  - DESIGN-BANK-IMPORT-API.md
  - DESIGN-BANK-IMPORT-APPLICATION.md
  - VISUAL-DESIGN-BANK-IMPORT.md
  - MOCKUP-BANK-IMPORT.md
  - PRD-BANK-IMPORT.md
---

# Frontend Layer Design: Bank Import Feature

## Overview

This document provides the complete frontend layer design for the Bank Import feature. The frontend layer implements a Vue/Nuxt page for uploading OFX/QFX files, reviewing imported transactions with duplicate detection, and selectively accepting transactions into the main transaction list.

**Key components:**
- **[`import.vue`](src/FrontEnd.Nuxt/app/pages/import.vue)** - Main import page component
- **File upload** - Multi-file selection with `.ofx`/`.qfx` validation
- **Status pane** - Dismissible upload results display
- **Transaction table** - Paginated review with checkbox selection
- **Session storage** - Selection persistence across navigation
- **API integration** - Auto-generated TypeScript client with authentication

**Layer responsibilities:**
- Present file picker UI for OFX/QFX file selection
- Handle file upload and display processing status
- Display transaction review table with pagination
- Manage checkbox selections with session storage persistence
- Call API endpoints via authenticated fetch wrapper
- Navigate to transactions page after successful import

## Page Architecture

Location: [`src/FrontEnd.Nuxt/app/pages/import.vue`](src/FrontEnd.Nuxt/app/pages/import.vue)

The import page uses a **component-based architecture** to break down the complex UI into smaller, manageable pieces. This is a NEW pattern for this project - previous pages (like [`transactions/index.vue`](src/FrontEnd.Nuxt/app/pages/transactions/index.vue)) contain all markup in a single large template, which becomes difficult to maintain.

### Component Breakdown

The page is organized into distinct components, each with a single responsibility:

**Page component:** [`import.vue`](src/FrontEnd.Nuxt/app/pages/import.vue)
- Owns all state (file upload, transactions, selections, pagination)
- Provides state and callbacks to child components via props/events
- Handles API calls and business logic
- Minimal template - mostly just composition of child components

**Child components:**

1. **[`FileUploadSection.vue`](src/FrontEnd.Nuxt/app/components/FileUploadSection.vue)** (reusable)
   - File picker UI with "Choose Files" and "Browse..." buttons
   - Hidden input element with file type restrictions
   - Emits `@filesSelected` event with File[] to parent
   - Props: `disabled` (boolean), `accept` (string), `multiple` (boolean)
   - Generic enough to reuse for other file upload scenarios

2. **[`UploadStatusPane.vue`](src/FrontEnd.Nuxt/app/components/UploadStatusPane.vue)** (reusable)
   - Dismissible Bootstrap alert showing upload status messages
   - Props: `statusMessages` (string[]), `show` (boolean)
   - Emits `@close` to parent when dismissed
   - Can be reused for any multi-step upload/processing workflow

3. **[`PaginationBar.vue`](src/FrontEnd.Nuxt/app/components/PaginationBar.vue)** (reusable)
   - First/Last navigation buttons and page number buttons (displays 3 numbered pages)
   - Props: `pageInfo` (IPaginationMetadata)
   - Emits `@pageUpdated(pageNumber)` to parent
   - Generic component - works with any pagination metadata from the API
   - Displays "Displaying X through Y of Z" text automatically
   - Can be reused across any paginated view (transactions, imports, reports, etc.)

4. **[`ImportReviewTable.vue`](src/FrontEnd.Nuxt/app/components/import/ImportReviewTable.vue)** (import-specific)
   - Transaction table with checkbox column, date, payee, category, amount
   - Props: `transactions` (array), `selectedKeys` (Set), `loading` (boolean)
   - Emits `@toggleSelection(key)`, `@toggleAll()` to parent
   - Row highlighting for potential duplicates
   - Handles empty state display
   - Import-specific because of duplicate status and default selections logic

5. **[`ImportDuplicatesAlert.vue`](src/FrontEnd.Nuxt/app/components/import/ImportDuplicatesAlert.vue)** (import-specific)
   - Bootstrap warning alert explaining potential duplicates
   - Props: `show` (boolean - computed from `hasPotentialDuplicates`)
   - No events needed (informational only)
   - Import-specific because of duplicate detection domain logic

6. **[`ImportActionButtons.vue`](src/FrontEnd.Nuxt/app/components/import/ImportActionButtons.vue)** (import-specific)
   - "Delete All" and "Import" buttons
   - Props: `hasTransactions`, `hasSelections`, `loading`, `uploading`
   - Emits `@import`, `@deleteAll` to parent
   - **Button states:**
     - "Import" button is **disabled** when `hasSelections === false` (no transactions selected)
     - "Import" button is **disabled** when `loading === true` or `uploading === true`
     - "Delete All" button is **disabled** when `hasTransactions === false`
     - "Delete All" button is **disabled** when `loading === true` or `uploading === true`
   - Import-specific because of button combination and enable/disable logic

### Component Organization

**Directory structure:**
```
src/FrontEnd.Nuxt/app/
├── pages/
│   └── import.vue                              # Page orchestrator (minimal template)
├── components/
│   ├── FileUploadSection.vue                   # Generic file upload (reusable)
│   ├── UploadStatusPane.vue                    # Generic status display (reusable)
│   ├── PaginationBar.vue                       # Generic pagination (reusable)
│   └── import/                                 # Import-specific components
│       ├── ImportReviewTable.vue               # Import-specific table
│       ├── ImportDuplicatesAlert.vue           # Import-specific alert
│       └── ImportActionButtons.vue             # Import-specific actions
```

### Benefits of Component-Based Approach

1. **Reduced cognitive load** - Each component is small enough to hold in your head
2. **Easier testing** - Components can be tested in isolation with Vue Test Utils
3. **Better reusability** - Generic components (FileUploadSection, UploadStatusPane, PaginationBar) can be used elsewhere
4. **Clearer responsibilities** - Each component has a single, well-defined purpose
5. **Simpler templates** - Page template becomes declarative composition of components
6. **Easier maintenance** - Changes to one feature area (e.g., file upload) are isolated to one component

### Architectural Decisions

1. **Component-based decomposition** - NEW for this project. Break large page into focused components rather than single monolithic template.

2. **Props down, events up** - Page owns all state, passes data down via props, receives updates via events. Standard Vue pattern for parent-child communication.

3. **Reusable when possible** - Components like FileUploadSection, UploadStatusPane, PaginationBar are designed generically so they can be extracted for project-wide reuse.

4. **Import prefix when specific** - Components specific to import workflow (ImportReviewTable, ImportActionButtons) are prefixed with "Import" to clarify scope.

5. **Set-based selection tracking** - Uses `Set<string>` to track selected transaction keys rather than an array. Provides O(1) lookup/toggle performance and prevents duplicates naturally.

6. **Sequential file processing** - Processes multiple files one at a time rather than in parallel. Simplifies error handling and status display.

7. **Session storage for selections** - Stores checkbox selections in browser session storage scoped by tenant key. See [Session Storage Pattern](#session-storage-pattern) section for details.

8. **Computed permissions** - Uses Vue computed properties to derive permission state (`canImport`) from user preferences store.

## Authorization Requirements

### Role-Based Access Control

**Editor role required for all import operations:**
- Users must have Editor or Owner role in the current workspace to access any import functionality
- Viewer role users cannot interact with importing in any way
- API enforces authorization via `[RequireTenantRole(TenantRole.Editor)]` on [`ImportController`](src/Controllers/ImportController.cs)

**Frontend enforcement:**

**1. Navigation bar visibility:**
- Import link shown only when `canImport` is `true` (computed from user preferences)
- `canImport` checks `userPrefs.currentTenant?.role` is `TenantRole.Editor` or `TenantRole.Owner`
- Viewer role users will NOT see Import link in navigation bar

**2. Direct navigation protection:**
- If Viewer role user attempts to navigate directly to [`/import`](src/FrontEnd.Nuxt/app/pages/import.vue) (e.g., via bookmark or URL manipulation)
- Page displays error message using [`ErrorDisplay`](src/FrontEnd.Nuxt/app/components/ErrorDisplay.vue) component
- Error message: "You do not have permission to import into this workspace. Editor role is required."
- All import UI elements (file upload, transaction table, action buttons) are hidden via `v-if="canImport"`
- Empty state message is also hidden
- Page remains accessible but shows only error message and workspace selector

**3. Backend validation:**
- Even if frontend protection is bypassed, API returns 403 Forbidden for unauthorized users
- Frontend error handling displays ProblemDetails response to user

**Benefits:**
- Defense in depth - protection at navigation, page, and API layers
- Clear user feedback when permission denied via error message
- No exposure of import UI to unauthorized users
- Consistent with existing authorization patterns (similar to transactions page hiding edit buttons for Viewers)

## Key Features

### File Upload

**Multi-file selection:**
- Users can select multiple `.ofx` or `.qfx` files at once
- File input has `multiple` attribute enabled
- Files are processed sequentially (simpler implementation)

**Validation:**
- Client-side extension validation before upload (`.ofx`, `.qfx` only)
- Server-side validation via API endpoint
- Clear error messages for invalid files

**Upload flow:**
1. User clicks "Browse..." button
2. File picker dialog opens (triggered by hidden input element)
3. User selects one or more files
4. Files are validated and uploaded sequentially
5. Status pane displays progress and results

### Upload Status Pane

**Visibility:**
- Shown when files are being processed
- Dismissible via close button (Bootstrap alert-dismissible)
- Remains visible after completion until user dismisses

**Content formats:**
- **During upload:** "⏳ filename.ofx: Importing..."
- **Complete success:** "✓ filename.ofx: 150 transactions added"
- **Partial success (with errors):** "⚠ filename.ofx: 140 transactions added, 10 errors detected [View Errors]"
- **Complete failure:** "❌ filename.ofx: Upload failed"
- Multiple files show multiple status lines

**Error details handling:**
- When parsing errors occur (partial success), status line includes clickable "[View Errors]" link
- Clicking link opens a modal dialog showing detailed error list
- Modal title: "Import Errors - filename.ofx"
- Modal body: Scrollable list of error messages from `ImportResultDto.Errors[]`
- Each error shows: line number (if available), transaction date/amount (if parsed), error message
- Modal footer: "Close" button only (no action needed, errors are informational)
- Use existing [`ModalDialog`](src/FrontEnd.Nuxt/app/components/ModalDialog.vue) component

**Error modal content example:**
```
Import Errors - checking-2024-01.ofx

1. Line 245: Invalid date format 'INVALID'
2. Line 312: Missing required field FITID
3. Transaction 2024-01-15 $125.50: Amount exceeds maximum allowed value
4. Line 423: Duplicate transaction ID 'ABC123' (skipped)
...
```

**Styling:**
- Uses Bootstrap `.alert .alert-info` styling for normal status
- Uses `.alert-warning` for partial success (has errors)
- Dismissible via `.alert-dismissible` class
- "[View Errors]" link styled as `btn-link btn-sm` for subtle appearance

**API Response:**
- `ImportResultDto` includes `Errors` property (array of error detail objects)
- `ImportResultDto.ImportedCount` reflects successful transactions only
- Frontend checks `Errors.length > 0` to determine if "[View Errors]" link should appear

### Potential Duplicates Alert

**Visibility:**
- Shown when at least one transaction has `DuplicateStatus.PotentialDuplicate`
- Computed property `hasPotentialDuplicates` checks all transactions

**Content:**
- Warning icon (⚠) on the left
- Explanation text: "Note: Potential duplicates detected and highlighted. Transactions have the same identifier as another transaction, but differ in payee or amount."

**Styling:**
- Uses Bootstrap `.alert .alert-warning` styling
- Yellow/warning background color

### Transaction Table

**Columns:**
1. Checkbox (selection)
2. Date (with warning icon for potential duplicates)
3. Payee
4. Category (placeholder for future Payee Matching rules feature)
5. Amount (right-aligned)

**Row highlighting:**
- **Normal rows** (New, ExactDuplicate): Default white background
- **Potential duplicate rows**: Yellow background (`table-warning` class)

**Selection:**
- Checkbox in header row toggles all visible transactions on current page
- Individual checkboxes toggle single transaction selection
- Selection state tracked in `selectedKeys` Set (by transaction key)

**Default selections:**
- **New transactions**: Selected by default
- **Exact duplicates**: Deselected by default
- **Potential duplicates**: Deselected by default

### Pagination

**PaginationBar Component:**

The [`PaginationBar.vue`](src/FrontEnd.Nuxt/app/components/PaginationBar.vue) component is designed to work with pagination metadata from any API response.

**Props:**
```typescript
interface Props {
  pageInfo: IPaginationMetadata  // Pagination metadata from API response
}
```

**Usage:**
```vue
<PaginationBar
  :page-info="paginatedResult.metadata"
  @pageUpdated="handlePageChange"
/>
```

**Component uses metadata properties:**
- `pageNumber` - Current page (1-based)
- `totalPages` - Total number of pages
- `totalCount` - Total items across all pages
- `firstItem` - First item number on current page
- `lastItem` - Last item number on current page

**Display elements:**
- First/Last page buttons (chevrons) - Only shown when not in numbered page range
- Three numbered page buttons - Displays current page and up to 2 adjacent pages
- "Displaying X through Y of Z" text - Shows `firstItem`, `lastItem`, and `totalCount`

**Behavior:**
- Hidden when `pageInfo` is null or no pages exist
- Automatically adjusts numbered pages based on current position (first, last, or middle)
- Emits `@pageUpdated(pageNumber)` when user clicks any page button

**Reusability:**
- Works with `PaginatedResultDto<ImportReviewTransactionDto>.metadata` on import page
- Works with `PaginatedResultDto<TransactionDto>.metadata` on transactions page
- Works with any API response that includes `IPaginationMetadata`
- No import-specific logic - purely generic pagination control

**Example integration:**
```typescript
// In parent component
const paginatedResult = ref<PaginatedResultDto<ImportReviewTransactionDto> | null>(null)
const currentPage = ref(1)

const loadPage = async (pageNumber: number) => {
  paginatedResult.value = await importClient.getPendingReview(pageNumber, 50)
  currentPage.value = pageNumber
}

const handlePageChange = (pageNumber: number) => {
  loadPage(pageNumber)
}
```

### Import Success Modal

**Trigger:**
- After successful completion of import review (POST /api/import/review/complete)

**Content:**
- Title: "Import Complete"
- Success message with statistics from [`CompleteReviewResultDto`](src/Application/Import/Dto/CompleteReviewResultDto.cs):
  - "Successfully imported {acceptedCount} transactions."
  - "Removed {totalDeletedCount} transactions from review."
- Example: "Successfully imported 120 transactions. Removed 150 transactions from review."

**Actions:**
- Single "OK" button (primary/success variant)
- Clicking OK navigates to transactions page ([`/transactions`](src/FrontEnd.Nuxt/app/pages/transactions/index.vue)) so user can see newly imported transactions
- Modal cannot be dismissed by clicking outside or pressing Escape (must click OK)

**Implementation:**
- Use existing [`ModalDialog`](src/FrontEnd.Nuxt/app/components/ModalDialog.vue) component
- Set `show` prop based on success state
- Handle `@confirm` event to navigate: `await navigateTo('/transactions')`
- Clear session storage for selections after modal is dismissed

**UX Flow:**
1. User clicks "Import" button
2. API call completes successfully with `CompleteReviewResultDto`
3. Modal appears showing import statistics
4. User clicks "OK"
5. Navigation to transactions page occurs automatically
6. User sees newly imported transactions in the list

### Delete Confirmation Modal

**Trigger:**
- "Delete All" button above transaction table

**Content:**
- Title: "Delete All Pending Imports"
- Warning message: "Are you sure you want to delete all pending import transactions? This cannot be undone."
- Count display: "378 transactions will be deleted."

**Actions:**
- Cancel button (closes modal)
- Delete button (red/danger variant, calls API)

### Empty State

**Shown when:**
- No pending import review transactions exist
- User has workspace selected and Editor/Owner role

**Content:**
- Inbox icon (📥)
- "No pending imports" message
- "Upload bank files to get started" helper text

## Session Storage Pattern

### Key Format

**Template:** `import-review-selections-{tenantKey}`

**Examples:**
- `import-review-selections-550e8400-e29b-41d4-a716-446655440000`
- `import-review-selections-660e8400-e29b-41d4-a716-446655440001`

**Rationale:**
- Tenant-scoped keys prevent cross-workspace selection leakage
- Workspace change automatically switches to correct selection state

### Value Format

**JSON array of selected transaction keys:**
```json
["guid1", "guid2", "guid3"]
```

### When to Save/Restore/Clear

**Save:**
- After every checkbox toggle (`toggleSelection`, `toggleAll`)
- After setting default selections (`setDefaultSelections`)

**Restore:**
- On page mount (after loading pending review)
- After workspace change (watch on `currentTenantKey`)

**Clear:**
- After successful import (`acceptTransactions`)
- After delete all (`deleteAllTransactions`)
- When workspace changes to null

### Why Session Storage Over Database

**Rationale:**
- Selection state is **UI state**, not domain data
- No need for server-side tracking or persistence
- Simpler implementation (no API calls on every checkbox toggle)
- Automatically cleared when user closes browser/tab
- Works offline (no network dependency for checkbox changes)
- No database migrations or additional tables required

**Tradeoffs:**
- Selections lost on browser close (acceptable for temporary review state)
- Not synchronized across browser tabs (acceptable for single-session workflow)
- Limited storage capacity (acceptable - only storing GUIDs)

## Navigation Integration

### Adding to layouts/default.vue

The Import page uses the `default` layout, which includes the [`SiteHeader`](src/FrontEnd.Nuxt/app/components/SiteHeader.vue) component. To add Import to the primary navigation:

**Location:** [`src/FrontEnd.Nuxt/app/components/SiteHeader.vue`](src/FrontEnd.Nuxt/app/components/SiteHeader.vue)

**Add navigation item:**
```vue
<NuxtLink
  to="/import"
  class="nav-link"
  :class="{ active: $route.path === '/import' }"
  data-test-id="import-nav-link"
>
  Import
</NuxtLink>
```

**Placement:**
- After "Transactions" link
- Before "Workspaces" link (if present)

**No badge indicator:**
- Deferred to future enhancement (too complex for initial release)
- Would require polling API or WebSocket for real-time count updates

## Component Reuse

### From Existing Pages

The import page reuses components and patterns from [`transactions/index.vue`](src/FrontEnd.Nuxt/app/pages/transactions/index.vue):

**Components:**
- [`ModalDialog`](src/FrontEnd.Nuxt/app/components/ModalDialog.vue) - For delete confirmation modal
- [`ErrorDisplay`](src/FrontEnd.Nuxt/app/components/ErrorDisplay.vue) - For API error messages
- [`BaseSpinner`](src/FrontEnd.Nuxt/app/components/BaseSpinner.vue) - For loading states
- [`WorkspaceSelector`](src/FrontEnd.Nuxt/app/components/WorkspaceSelector.vue) - Standard workspace selector (if using chrome layout)
- [`FeatherIcon`](src/FrontEnd.Nuxt/app/components/FeatherIcon.vue) - For icons (folder, check, trash, alert)

**Bootstrap styling:**
- `.table .table-hover` - Transaction table
- `.alert .alert-warning` - Warning messages
- `.alert .alert-info` - Upload status pane
- `.btn .btn-primary` - Primary action button
- `.btn .btn-danger` - Delete button
- `.btn .btn-secondary` - Secondary action button
- `.card .card-body` - Content container

**Patterns:**
- File upload handling (hidden input + button trigger)
- API error handling via `handleApiError` utility
- Loading states with spinner and disabled buttons
- Empty state messaging
- Generic pagination controls via PaginationBar (works with any `IPaginationMetadata`)

## State Management

The page manages state using Vue 3 Composition API patterns:

- **File upload state** - Tracks selected files, upload progress, and status messages
- **Transaction review state** - Loaded transactions, loading/error states
- **Selection state** - `Set<string>` of selected transaction keys (persisted to session storage)
- **Pagination state** - Current page, page size, total count, navigation flags
- **Computed properties** - Workspace context, permissions (`canImport`), duplicate detection
- **Watchers** - Workspace changes trigger reload and selection restoration

## Type Safety

### Importing Types from apiclient.ts

All API client types are imported from the auto-generated [`apiclient.ts`](src/FrontEnd.Nuxt/app/utils/apiclient.ts):

```typescript
import {
  ImportClient,
  TenantRole,
  type ImportReviewTransactionDto,
  type PaginatedResultDto,
  type ImportResultDto,
  type IProblemDetails,
  DuplicateStatus,
} from '~/utils/apiclient'
```

**Key types:**
- `ImportClient` - API client class (instantiated with baseUrl and authFetch)
- `ImportReviewTransactionDto` - Transaction in review state
- `ImportResultDto` - Upload result statistics
- `PaginatedResultDto<T>` - Generic paginated response
- `DuplicateStatus` - Enum (New, ExactDuplicate, PotentialDuplicate)
- `TenantRole` - Enum (Owner, Editor, Viewer)
- `IProblemDetails` - Error response contract

### TypeScript Strict Mode

**Null safety:**
- Optional chaining for transaction keys: `transaction.key!`
- Null checks before API calls: `if (!currentTenantKey.value) return`
- Type guards for computed properties

**Type annotations:**
- Function parameters: `(key: string)`
- Return types: `: Promise<void>`
- Event handlers: `(event: Event)`

## API Integration

### Authentication

**useAuthFetch composable:**
```typescript
const authFetch = useAuthFetch()
const importClient = new ImportClient(baseUrl, authFetch)
```

**Behavior:**
- Automatically adds `Authorization: Bearer <token>` header to all requests
- Handles token refresh if expired
- Throws errors for authentication failures (401)

### Error Handling

**handleApiError utility:**
```typescript
error.value = handleApiError(err, 'Upload Failed', `Failed to upload ${file.name}`)
showError.value = true
```

**Benefits:**
- Extracts ProblemDetails from API responses
- Provides default fallback messages
- Returns standardized error object for ErrorDisplay component

### API Client Methods Used

**Upload file:**
```typescript
const result: ImportResultDto = await importClient.uploadFile(file)
```

**Get pending review (paginated):**
```typescript
const result: PaginatedResultDto<ImportReviewTransactionDto> =
  await importClient.getPendingReview(currentPage.value, pageSize.value)

// Store result with metadata for PaginationBar component
paginatedTransactions.value = result
```

**PaginationBar usage:**
```vue
<PaginationBar
  :page-info="paginatedTransactions.metadata"
  @pageUpdated="loadPage"
/>
```

The component uses the nested `metadata` property from `PaginatedResultDto<T>`, making it reusable across any paginated view without modification.

**Complete review (accept selected transactions):**
```typescript
const keysArray = Array.from(selectedKeys.value)
await importClient.completeReview(keysArray)
```

**Delete all:**
```typescript
await importClient.deleteAllPendingReview()
```

## Implementation Checklist

- [ ] Create [`import.vue`](src/FrontEnd.Nuxt/app/pages/import.vue) page component
- [ ] Implement file upload section with multi-file support
- [ ] Add upload status pane with dismissible alert
- [ ] Implement transaction review table with pagination
- [ ] Add checkbox selection with session storage persistence
- [ ] Implement potential duplicates alert
- [ ] Add action buttons (Import, Delete All)
- [ ] Create delete all confirmation modal
- [ ] Add navigation item to SiteHeader component

## References

**Design Documents:**
- [`DESIGN-BANK-IMPORT.md`](DESIGN-BANK-IMPORT.md) - Overall feature architecture
- [`DESIGN-BANK-IMPORT-API.md`](DESIGN-BANK-IMPORT-API.md) - API layer design (ImportController)
- [`DESIGN-BANK-IMPORT-APPLICATION.md`](DESIGN-BANK-IMPORT-APPLICATION.md) - Application layer design (ImportReviewFeature)
- [`DESIGN-BANK-IMPORT-DATABASE.md`](DESIGN-BANK-IMPORT-DATABASE.md) - Database schema and entities
- [`PRD-BANK-IMPORT.md`](PRD-BANK-IMPORT.md) - Product requirements

**Visual Design:**
- [`VISUAL-DESIGN-BANK-IMPORT.md`](VISUAL-DESIGN-BANK-IMPORT.md) - UI design and interaction patterns
- [`MOCKUP-BANK-IMPORT.md`](MOCKUP-BANK-IMPORT.md) - Visual mockups of all page states

**Project Standards:**
- [`.roorules`](../../.roorules) - Project coding standards and patterns
- [`src/FrontEnd.Nuxt/.roorules`](src/FrontEnd.Nuxt/.roorules) - Frontend-specific patterns

**Related Code:**
- [`transactions/index.vue`](src/FrontEnd.Nuxt/app/pages/transactions/index.vue) - Reference page implementation
- [`apiclient.ts`](src/FrontEnd.Nuxt/app/utils/apiclient.ts) - Auto-generated API client
- [`useAuthFetch.ts`](src/FrontEnd.Nuxt/app/composables/useAuthFetch.ts) - Authentication wrapper
- [`errorHandler.ts`](src/FrontEnd.Nuxt/app/utils/errorHandler.ts) - API error handling utility
