---
status: In Review
created: 2026-01-11
stories: Stories 1 & 2
related_docs:
  - PRD-PAYEE-RULES.md
  - PRD-PAYEE-RULES-REVIEW.md
  - ../import-export/DESIGN-BANK-IMPORT.md
---

# Design Document: Payee Matching Rules (Stories 1 & 2)

## Abstract

This document provides the detailed technical design for implementing Stories 1 & 2 of the Payee Matching Rules feature as specified in [`PRD-PAYEE-RULES.md`](PRD-PAYEE-RULES.md):

- **Story 1:** User - Establish payee matching rules (CRUD operations, validation)
- **Story 2:** User - Sees transactions automatically categorized on bank import

## Overview

The Payee Matching Rules feature enables users to automatically categorize transactions based on payee name patterns. Users create rules that match transaction payees (via substring or regex) and assign categories. During bank import, these rules are applied to set the category on imported transactions before review.

**Key capabilities:**
- Create, read, update, and delete payee matching rules with substring or regex patterns
- Validate regex patterns for correctness and ReDoS vulnerabilities
- Apply matching rules during bank import to auto-categorize transactions
- Handle rule conflicts using precedence logic (regex > substring, longer > shorter, newer > older)
- Maintain tenant isolation and role-based access control

## Architecture Overview

The Payee Matching Rules feature follows Clean Architecture principles with clear separation across layers:

### Data Layer
- **PayeeMatchingRule entity** - Stores user-defined rules for matching and categorizing transactions
- **Migration** - Creates `YoFi.V3.PayeeMatchingRules` table with tenant isolation and indexes
- **DbContext** - Exposes PayeeMatchingRules DbSet for Application layer queries

### Application Layer
- **PayeeMatchingRuleFeature** - Business logic for CRUD operations on rules, rule matching, and cache management
- **PayeeMatchingHelper** - Pure matching algorithm (static) for rule precedence resolution
- **RegexValidationService** - Validates regex patterns and tests for ReDoS vulnerabilities
- **DTOs** - PayeeMatchingRuleEditDto, PayeeMatchingRuleResultDto
- **Validation** - Ensures rules meet schema requirements (category required, valid regex)

### API Layer
- **PayeeMatchingRulesController** - Endpoints for CRUD operations on rules
- **Authentication/Authorization** - Tenant-scoped access control, Viewer for GET, Editor for POST/PUT/DELETE
- **Integration with ImportReviewFeature** - Applies matching during bank import workflow

### Integration Points
- **Bank Import workflow** - After parsing OFX, before storing ImportReviewTransactions, apply matching rules to set Category field
- **Transactions page** - "Create Rule from Transaction" action allows users to create rules based on existing transactions

### Workflow (Story 2: Auto-categorize on import)
1. User uploads OFX file → Frontend sends multipart POST to `/api/import/upload`
2. Backend parses file → OFXParsingService extracts transactions
3. **NEW: Apply matching rules** → PayeeMatchingService matches each transaction against tenant's rules
4. **NEW: Set Category** → Matching transaction gets Category field populated (stored in ImportReviewTransaction.Category)
5. Duplicate detection → Compare against existing transactions and pending imports
6. Store in staging → Insert into ImportReviewTransactions with DuplicateStatus and Category
7. Return to UI → Frontend displays transactions with matched categories (read-only in import review)
8. User reviews → Sees auto-assigned categories, selects/deselects transactions, clicks "Import"
9. Accept selected → Backend copies to Transactions table, Category becomes split.Category (sanitized)

## Database Layer Design

### PayeeMatchingRule Entity

**Location:** `src/Entities/Models/PayeeMatchingRule.cs`

**Purpose:** Represents a user-defined rule for matching transaction payees and assigning categories automatically.

**Base class:** Inherits from [`BaseTenantModel`](src/Entities/Models/BaseTenantModel.cs) (provides Id, Key, TenantId)

**Fields:**

| Field | Type | Constraints | Description |
|-------|------|-------------|-------------|
| PayeePattern | string | Required, MaxLength(200) | Pattern to match (substring or regex), case-insensitive |
| PayeeIsRegex | bool | Default: false | If false: substring matching. If true: regex with IgnoreCase and 100ms timeout |
| Category | string | Required, MaxLength(200) | Category to assign when matched, **sanitized on save** (never store unsanitized) |
| CreatedAt | DateTimeOffset | Auto-set | Rule creation timestamp |
| ModifiedAt | DateTimeOffset | Auto-updated | Last modified timestamp (used for conflict resolution) |
| LastUsedAt | DateTimeOffset? | Nullable, auto-updated | Last time this rule matched a transaction (null if never used) |
| MatchCount | int | Default: 0 | Number of times this rule has matched transactions |
| Tenant | Tenant? | Navigation property | Foreign key relationship |

**Future fields (Stories 4-7, not in this design):**
- SourcePattern, SourceIsRegex (Story 4 - match by source)
- AmountExact, AmountMin, AmountMax (Story 4 - match by amount)
- Loan details, split rules (Stories 6-7)

### EF Core Migration

**Migration name:** `AddPayeeMatchingRulesTable`

**Table name:** `YoFi.V3.PayeeMatchingRules`

**Required indexes:**
1. `IX_PayeeMatchingRules_Key` (unique) - Business key lookup
2. `IX_PayeeMatchingRules_TenantId` - Load all rules for tenant (used by both matching service and display endpoints)

**Why no sort-specific indexes?**
- Typical rule sets are ~500 rules (~40KB per tenant)
- Rules are cached in memory for matching performance
- Display endpoints can reuse the same cache and sort in-memory
- In-memory sorting of 500 items is negligible (<1ms)
- Eliminates index maintenance overhead and storage cost
- Simplifies schema evolution (new sort fields don't require new indexes)

**Foreign key:** TenantId → Tenants(Id) with `ON DELETE CASCADE`

**SQLite considerations:**
- DateTimeOffset stored as TEXT in ISO 8601 format ("O" format string)
- Bool stored as INTEGER (0 = false, 1 = true)
- Use EF Core HasConversion for both field types

### DbContext Configuration

**Location:** `src/Data/Sqlite/ApplicationDbContext.cs`

**Changes required:**
1. Add `DbSet<PayeeMatchingRule>` property
2. Configure entity in `OnModelCreating()`:
   - Apply two required indexes (Key unique, TenantId composite)
   - Configure DateTimeOffset conversions for SQLite (CreatedAt, ModifiedAt, LastUsedAt)
   - Set string length constraints (PayeePattern: 200, Category: 200)
   - Configure foreign key with cascade delete

**Reference pattern:** Follow existing Transaction entity configuration

### ImportReviewTransaction Modification

**Location:** `src/Entities/Models/ImportReviewTransaction.cs`

**Add field:**
- `Category` (string?, MaxLength 200) - Populated by PayeeMatchingService during import, displayed read-only in UI, becomes split.Category when accepted

**Note:** ImportReviewTransactionDto already includes Category field per Bank Import design, so no DTO changes needed.

## Application Layer Design

### Data Transfer Objects

#### PayeeMatchingRuleEditDto

**Location:** `src/Application/Dto/PayeeMatchingRuleEditDto.cs`

**Purpose:** Input DTO for creating or updating payee matching rules

**Fields:**
- `PayeePattern` (string, required)
- `PayeeIsRegex` (bool)
- `Category` (string, required)

**Validation:** See PayeeMatchingRuleEditDtoValidator section

#### PayeeMatchingRuleResultDto

**Location:** `src/Application/Dto/PayeeMatchingRuleResultDto.cs`

**Purpose:** Output DTO for displaying payee matching rules

**Fields:**
- `Key` (Guid)
- `PayeePattern` (string)
- `PayeeIsRegex` (bool)
- `Category` (string)
- `CreatedAt` (DateTimeOffset)
- `ModifiedAt` (DateTimeOffset)
- `LastUsedAt` (DateTimeOffset?) - Nullable, null if rule has never matched
- `MatchCount` (int) - Number of times rule has matched transactions

### Validation

#### PayeeMatchingRuleEditDtoValidator

**Location:** `src/Application/Validation/PayeeMatchingRuleEditDtoValidator.cs`

**Framework:** FluentValidation (injected via DI)

**Validation rules:**
1. **PayeePattern** - NotEmpty, MaxLength(200)
2. **Category** - NotEmpty, not whitespace-only, MaxLength(200)
3. **Regex validation (when PayeeIsRegex=true):**
   - Pattern must compile successfully as .NET Regex
   - Pattern tested against ReDoS test string with 100ms timeout
   - Validation failure returns user-friendly error message including .NET Regex error text

**Dependencies:** Requires `IRegexValidationService` injection for regex validation

### Services

#### IRegexValidationService / RegexValidationService

**Location:** `src/Application/Services/IRegexValidationService.cs` and `src/Application/Services/RegexValidationService.cs`

**Purpose:** Validates regex patterns for correctness and ReDoS vulnerabilities

**Method:** `RegexValidationResult ValidateRegex(string pattern)`

**Returns:** `RegexValidationResult(bool IsValid, string? ErrorMessage)`

**Validation process:**
1. Check pattern is not null/whitespace
2. Attempt to compile with `RegexOptions.IgnoreCase | RegexOptions.NonBacktracking`
3. Catch `NotSupportedException` → Pattern uses unsupported features (backreferences, lookahead/lookbehind), return user-friendly error
4. Catch `ArgumentException` → Invalid syntax, return .NET error message including exception text

**Why NonBacktracking:**
- .NET 7+ feature that guarantees linear time complexity O(n)
- Completely eliminates ReDoS vulnerabilities (no timeout testing needed)
- Rejects patterns with advanced features (backreferences, lookahead/lookbehind) that could cause backtracking
- More reliable than timeout-based testing with adversarial strings

**Used by:**
- `PayeeMatchingRuleEditDtoValidator` - Injected via constructor to validate regex patterns during create/update operations
- Future validators (Story 4+) - When source pattern validation is added

**Why a service and not a static helper?**
- **FluentValidation pattern** - Validators use constructor injection for dependencies, requiring DI-registered services
- **Testability** - Can be mocked in validator unit tests to test validation logic independently
- **Reusability** - Single instance shared across all validators that need regex validation
- **Consistent architecture** - Follows project pattern of injecting services into validators (see `IDataProvider` usage in other validators)

**Note:** `PayeeMatchingService` does NOT use this service. It compiles and uses regex patterns directly during matching for performance (avoids double compilation).

**Lifecycle:** Registered as Singleton (stateless service)

#### PayeeMatchingHelper

**Location:** `src/Application/Helpers/PayeeMatchingHelper.cs`

**Purpose:** Pure matching algorithm for finding the best matching rule for a given payee

**Method:** `static string? FindBestMatch(string payee, IReadOnlyCollection<PayeeMatchingRule> rules)`

**Parameters:**
- `payee` - Transaction payee string to match against
- `rules` - Pre-sorted rule list (sorted by ModifiedAt DESC for conflict resolution)

**Returns:** Category string from best matching rule, or null if no match

**Matching algorithm:**
1. Track separate best regex match and best substring match
2. For each rule:
   - If regex: test with `Regex.IsMatch()` using `RegexOptions.IgnoreCase | RegexOptions.NonBacktracking`
   - If substring: test with `payee.Contains(pattern, OrdinalIgnoreCase)`
3. Track longest substring match, first regex match (rules already sorted by ModifiedAt DESC)
4. Return: regex match > substring match > null

**Conflict resolution precedence (per Story 2):**
1. Regex pattern beats substring pattern (always)
2. For substring: longer pattern beats shorter
3. For equal length/both regex: most recently modified wins (first in pre-sorted list)

**Exception handling:**
- `NotSupportedException` during matching → thrown back to caller (shouldn't happen due to validation, but callers must handle)
- `ArgumentException` during matching → thrown back to caller (shouldn't happen due to validation, but callers must handle)
- **Caller responsibility:** PayeeMatchingRuleFeature must catch these exceptions and convert to appropriate application exceptions that CustomExceptionHandler can handle

**Why static?**
- Pure function with no dependencies or state
- Takes all inputs as parameters, returns computed result
- Easily unit testable without mocking
- Lightweight - no need for DI overhead

**Performance:**
- Regex compiled on-demand (not cached initially)
- All matching in-memory
- Expected time: <100ms for 1,000 transactions × 500 rules

**Used by:** `PayeeMatchingRuleFeature.ApplyMatchingRulesAsync()` and `FindBestMatchAsync()`

#### PayeeMatchingRuleFeature

**Location:** `src/Application/Features/PayeeMatchingRuleFeature.cs`

**Purpose:** Business logic for CRUD operations on payee matching rules, rule matching, and cache management

**Dependencies (constructor injection):**
- `ITenantProvider` - For current tenant context, caches `_tenantId`
- `IDataProvider` - For querying and updating PayeeMatchingRule entities
- `IMemoryCache` - For caching rules per tenant (from `Microsoft.Extensions.Caching.Memory`)

**CRUD Methods:**

**`GetRulesAsync(pageNumber, pageSize, sortBy, searchText)`**
- Accepts optional pagination parameters: `pageNumber` (default 1), `pageSize` (default 50, max 1000)
- Accepts optional `sortBy` parameter (enum: PayeePattern, Category, LastUsedAt)
- Accepts optional `searchText` parameter (plain text search across PayeePattern and Category)
- Loads all rules for current tenant from cache (via private `GetRulesForTenantAsync()`)
- **Filters in-memory** if searchText provided: case-insensitive substring match on PayeePattern OR Category
- Sorts in-memory based on `sortBy` parameter (default: PayeePattern ASC)
- **Paginates in-memory** using standard `PaginationHelper.Calculate()` and Skip/Take
- Returns `PaginatedResultDto<PayeeMatchingRuleResultDto>` with items and pagination metadata

**`GetRuleByKeyAsync(key)`**
- Single rule lookup by Key
- Throws `NotFoundException` if not found or wrong tenant

**`CreateRuleAsync(ruleDto)`**
- **Sanitizes Category** using [`CategoryHelper.SanitizeCategory()`](src/Application/Helpers/CategoryHelper.cs)
- Creates new entity with sanitized category, sets TenantId, CreatedAt, ModifiedAt (same timestamp)
- Saves to database, **invalidates cache**
- Returns PayeeMatchingRuleResultDto with generated Key

**`UpdateRuleAsync(key, ruleDto)`**
- Loads existing rule (throws NotFoundException if not found)
- **Sanitizes Category** using [`CategoryHelper.SanitizeCategory()`](src/Application/Helpers/CategoryHelper.cs)
- Updates PayeePattern, PayeeIsRegex, Category (sanitized)
- Updates ModifiedAt to current time
- Saves changes, **invalidates cache**
- Returns updated DTO

**`DeleteRuleAsync(key)`**
- Loads rule, removes, saves changes, **invalidates cache**
- Throws NotFoundException if not found

**Matching Methods:**

**`ApplyMatchingRulesAsync(transactions)`**
- Loads all rules for current tenant from cache (sorted by ModifiedAt DESC)
- Iterates through transactions, calls `PayeeMatchingHelper.FindBestMatch()` for each
- Modifies transaction.Category in-place (mutable DTO)
- **Updates usage statistics:** Increments MatchCount and sets LastUsedAt for matched rules via IDataProvider
- Returns Task (no return value, transactions modified)

**`FindBestMatchAsync(payee)`**
- Single-payee variant for manual matching (Story 3)
- Loads rules from cache, calls `PayeeMatchingHelper.FindBestMatch()`
- Returns category string or null if no match

**Cache Management (private methods):**

**`GetRulesForTenantAsync()`**
- Checks cache first using key pattern: `payee-rules:{_tenantId}`
- On cache miss: queries database (simple TenantId filter), stores in cache
- Returns unsorted list (callers sort in-memory based on their needs)
- No expiration policy (explicit invalidation on changes)

**`InvalidateCache()`**
- Removes cached rules for current tenant
- Called after Create/Update/Delete operations

**Caching Strategy:**
- Cache key pattern: `payee-rules:{tenantId}`
- Cache entry contains complete entity list for tenant (NOT sorted - sorting happens at call site)
- Memory impact: ~500 rules ≈ 40KB per tenant (with GUID keys)
  - 100 concurrent tenants = 4MB total
  - 1000 concurrent tenants = 40MB total
- Performance gain: Eliminates DB query on both import and display (DB query only on cache miss)

**Tenant isolation:** All queries filter by `_tenantId` (from ITenantProvider)

**Lifecycle:** Registered as Scoped (needs IDataProvider which is scoped)

### Integration with Bank Import

**Modified file:** `src/Application/Features/ImportReviewFeature.cs`

**Constructor changes:**
- Add `PayeeMatchingRuleFeature payeeMatchingRuleFeature` parameter (cross-feature dependency)

**ImportFileAsync method changes:**

**New step after OFX parsing:**
```csharp
// 2. Apply payee matching rules to set Category field
await _payeeMatchingRuleFeature.ApplyMatchingRulesAsync(parsingResult.Transactions);
```

**When creating ImportReviewTransaction entities:**
```csharp
Category = transaction.Category, // NEW: Category from matching rules (may be null)
```

**Impact:**
- Cross-feature dependency: ImportReviewFeature now depends on PayeeMatchingRuleFeature
- Minimal change: single method call added after OFX parsing
- Feature gracefully handles zero rules (no-op)
- TenantId automatically scoped via PayeeMatchingRuleFeature's constructor (no parameter needed)

## API Layer Design

### PayeeMatchingRulesController

**Location:** `src/Controllers/PayeeMatchingRulesController.cs`

**Route:** `/api/tenant/{tenantKey:guid}/payee-rules`

**Authorization:**
- GET endpoints (viewing rules): `[RequireTenantRole(TenantRole.Viewer)]`
- POST, PUT, DELETE endpoints (modifying rules): `[RequireTenantRole(TenantRole.Editor)]`
- Owner role not required for any operations

**Base attributes:**
- `[ApiController]`
- `[Produces("application/json")]`
- Standard ProblemDetails responses: 401, 403, 500

**Constructor:** `PayeeMatchingRuleFeature` and `ILogger<PayeeMatchingRulesController>`

**Endpoints:**

| Method | Path | Description | Query Parameters | Request Body | Response | Status Codes |
|--------|------|-------------|------------------|--------------|----------|--------------|
| GET | `/` | Get all rules (paginated) | `pageNumber`, `pageSize`, `sortBy`, `searchText` (all optional) | - | `PaginatedResultDto<PayeeMatchingRuleResultDto>` | 200, 401, 403 |
| GET | `/{key:guid}` | Get rule by key | - | - | `PayeeMatchingRuleResultDto` | 200, 401, 403, 404 |
| POST | `/` | Create rule | - | `PayeeMatchingRuleEditDto` | `PayeeMatchingRuleResultDto` | 201, 400, 401, 403 |
| PUT | `/{key:guid}` | Update rule | - | `PayeeMatchingRuleEditDto` | `PayeeMatchingRuleResultDto` | 200, 400, 401, 403, 404 |
| DELETE | `/{key:guid}` | Delete rule | - | - | None | 204, 401, 403, 404 |

**GET `/` Query Parameters:**
- `pageNumber` (optional, default: 1) - Page number to retrieve (1-based)
- `pageSize` (optional, default: 50, max: 1000) - Number of items per page
- `sortBy` (optional, default: PayeePattern) - Sort order: PayeePattern, Category, or LastUsedAt
- `searchText` (optional) - Plain text search across PayeePattern and Category (case-insensitive substring match)

**Pagination Implementation:**
- Follows standard project pattern using [`PaginatedResultDto<T>`](src/Application/Dto/PaginatedResultDto.cs) and [`PaginationHelper`](src/Application/Helpers/PaginationHelper.cs)
- Filter and sort operations happen in-memory on cached rule set before pagination
- Returns pagination metadata (page numbers, total count, has previous/next page flags)

**Logging pattern:**
- Follow project [`LOGGING-POLICY.md`](../../LOGGING-POLICY.md)
- Use `[LoggerMessage]` attribute with partial methods
- Event IDs: 1 (Debug Starting), 2 (Debug Starting with Key), 3 (Info OK with Key), 4 (Info OK with Count)
- Include `[CallerMemberName]` on all log methods
- Log at entry (Debug) and successful exit (Information)

**Error handling:**
- FluentValidation errors → 400 Bad Request with validation details
- NotFoundException → 404 Not Found via CustomExceptionHandler
- Regex validation errors → 400 Bad Request with regex error message

**POST endpoint specifics:**
- Returns `CreatedAtAction` with location header
- Location points to GET `/{key}` endpoint

### Service Registration

**Application services** (`src/Application/ServiceCollectionExtensions.cs`):
```csharp
services.AddScoped<PayeeMatchingRuleFeature>();
services.AddSingleton<IRegexValidationService, RegexValidationService>();
```

**Controller validators** (`src/Controllers/Extensions/ServiceCollectionExtensions.cs`):
```csharp
services.AddScoped<IValidator<PayeeMatchingRuleEditDto>, PayeeMatchingRuleEditDtoValidator>();
```

**Note:** `PayeeMatchingHelper` is a static class with static methods, so no DI registration needed.

## Frontend Design

### Payee Rules Management Page

**Route:** `/payee-rules` (or similar tenant-scoped route)

**Page Purpose:** Allow users to view, search, sort, and quick-edit their payee matching rules

#### Layout Structure

**Page Header:**
- Workspace selector component (standard tenant switcher - currently separate component)
- Page title: "Payee Matching Rules"
- "New Rule" button (primary action) - Opens create form
  - **Only shown for Editor/Owner roles**
  - Hidden for Viewer role
- Total rules count display (e.g., "120 rules")

**Search and Filter Bar:**
- Search input field with placeholder "Search payee or category..."
- Search button next to input field
- Search executes when user presses Enter key or clicks Search button
- Clear search button (X icon) appears when search text is present
- Sort dropdown with options:
  - "Payee Pattern (A-Z)" (default)
  - "Category (A-Z)"
  - "Last Used"

**Rules Table/List:**
- Displays paginated rules (50 per page by default)
- Columns:
  - **Payee Pattern** - Display pattern text, small badge if regex (e.g., "Regex" pill)
  - **Category** - Display category text (sanitized)
  - **Last Used** - Display date or "Never" if null, formatted as relative time (e.g., "2 days ago")
  - **Match Count** - Display number (e.g., "42 matches")
  - **Actions** - Edit and Delete buttons/icons
    - **Only shown for Editor/Owner roles**
    - Hidden for Viewer role (no Actions column shown at all)

**Pagination Controls:**
- Standard pagination UI at bottom of list
- Shows current page and total pages
- Previous/Next navigation buttons
- Page size fixed at 50 per page (not user-editable)

#### Quick Edit Functionality

**Note:** "Quick Edit" distinguishes this from future detailed edit on dedicated page (Stories 4+)

**Edit Dialog Pattern:**
- Follow same pattern as Transactions page quick edit
- When user clicks Edit button on a row, open a modal dialog
- Dialog contains editable fields:
  - **Payee Pattern** - Text input
  - **Regex checkbox** - Toggle for PayeeIsRegex
  - **Category** - Text input
- Dialog has Save and Cancel buttons
- On Save, validate and submit to API
- On success, close dialog and refresh rule in list

**Validation:**
- Validation occurs on Save button click
- Error messages displayed in dialog (inline or at top)
- Regex validation errors show user-friendly message including .NET Regex error text
- Empty category validation error shown
- Keep dialog open on validation failure

**Create New Rule:**
- Opens same modal dialog pattern as Edit
- "New Rule" button in page header triggers dialog
- Same fields as edit dialog (Payee Pattern, Regex checkbox, Category)
- Submit and Cancel buttons
- Validation on submit with error display
- On success, close dialog and refresh list to show new rule

**Delete Confirmation:**
- Clicking Delete button shows confirmation dialog
- "Are you sure you want to delete this rule? This action cannot be undone."
- Confirm and Cancel buttons
- On confirm, delete via API and remove rule from list

#### Empty States

**No Rules:**
- Empty state illustration or message
- "You haven't created any payee matching rules yet"
- "Create your first rule" button

**No Search Results:**
- "No rules match your search"
- Show current search term
- "Clear search" button to reset

#### Loading States

**Initial Load:**
- Skeleton loaders or spinner while fetching rules from API

**Pagination:**
- Brief loading indicator when changing pages

**Save/Delete:**
- Disable buttons and show spinner during API call
- Success feedback (toast notification or inline message)
- Error feedback if API call fails

#### Responsive Behavior

**Desktop:**
- Full table layout with all columns visible

**Mobile/Responsive:**
- Follow same pattern as Transactions page (whatever is currently implemented)
- Ensure table remains usable on smaller screens (horizontal scroll or responsive columns)

#### Test Automation Support

**data-test-id Attributes:**

All interactive elements and status displays must include `data-test-id` attributes for functional test automation:

**Interactive Elements (user input/clicks):**
- Search input field: `data-test-id="search-input"`
- Search button: `data-test-id="search-button"`
- Clear search button: `data-test-id="clear-search-button"`
- Sort dropdown: `data-test-id="sort-dropdown"`
- New button: `data-test-id="new-button"`
- Pagination controls: Covered by pagination component (no need to specify here)

**Table Structure:**
- Table element: `data-test-id="payee-rules"`
- Table headers: `data-test-id="payee-pattern"`, `data-test-id="category"`, `data-test-id="last-used"`, `data-test-id="match-count"`, `data-test-id="actions"`
- Each rule row (TR element): `data-test-id="row-{key}"`
- Edit button (within row): `data-test-id="edit-button"`
- Delete button (within row): `data-test-id="delete-button"`
- Regex badge (within row): `data-test-id="regex-badge"`

**Modal Dialog Elements:**
- Dialog container: `data-test-id="rule-edit-dialog"` or `data-test-id="rule-create-dialog"`
- Payee Pattern input: `data-test-id="payee-pattern-input"`
- Regex checkbox: `data-test-id="regex-checkbox"`
- Category input: `data-test-id="category-input"`
- Save button: `data-test-id="save-button"`
- Cancel button: `data-test-id="cancel-button"`

**Status/Display Elements:**
- Total rules count: `data-test-id="total-rules-count"`
- Current page number: `data-test-id="current-page"`
- Empty state message: `data-test-id="empty-state"`
- No search results message: `data-test-id="no-results"`
- Validation error display: `data-test-id="validation-error"`
- Success message/toast: `data-test-id="success-message"`

### Create Rule from Transaction (Transactions Page Integration)

**Location:** Transactions page (existing component)

**Purpose:** Allow users to quickly create payee matching rules based on existing transactions

#### User Flow

1. User views transaction in Transactions list
2. User clicks action button/menu item: "Create Rule from This"
3. System opens Quick Edit dialog (same as "New Rule" dialog) **pre-populated** with:
   - **Payee Pattern:** Transaction's payee field (full text)
   - **Regex checkbox:** Unchecked (default to substring)
   - **Category:** Transaction's category if present, empty if not
4. User can edit fields (typically trims payee down to smaller substring)
5. **If user enters/modifies Category:** Category is applied to the source transaction as well
6. User clicks Save → Rule created via API, transaction updated if category changed
7. Success message shown, user can continue working with transactions

**Note:** This combines two operations:
- Create payee matching rule (always happens)
- Update transaction category (only if category field is filled/modified)

#### UI Elements

**Transactions Page:**
- Add "Create Rule" action button/menu item per transaction row
- **Only shown for Editor/Owner roles**
- Hidden for Viewer role
- Button/action labeled: "Create Rule" or "Create Rule from This"

**Dialog:**
- **Reusable Vue component:** Same Quick Edit modal dialog component used on Payee Rules page
- Component should accept props for pre-population and mode (create vs edit)
- Title: "Create Payee Matching Rule"
- Fields pre-populated from transaction
- User can modify all fields before saving
- Standard validation applies (empty category, regex syntax, ReDoS check)

**Component Design:**
- Component name: `PayeeRuleDialog.vue` (or similar)
- Used by both:
  - Payee Rules management page (create/edit rules)
  - Transactions page (create rule from transaction)
- Props: `initialPayeePattern`, `initialCategory`, `initialIsRegex`, `mode` ('create' or 'edit'), `ruleKey` (for edit mode)
- Emits: `save`, `cancel` events

#### API Integration

**Endpoint:** POST `/api/tenant/{tenantKey}/payee-rules`

**Request body:** Standard `PayeeMatchingRuleEditDto` with pre-populated values

**No special endpoint needed:** Reuses existing Create endpoint

#### Typical User Workflow

1. User imports transactions, sees "Amazon Web Services LLC" in payee
2. User realizes they want to auto-categorize AWS charges
3. User clicks "Create Rule" on that transaction row
4. Dialog opens with:
   - Payee Pattern: "Amazon Web Services LLC"
   - Category: "Cloud Services" (if transaction already categorized)
5. User edits Payee Pattern to just "AWS" (shorter, broader match)
6. User clicks Save
7. Rule created, future AWS transactions will auto-categorize

#### Test Automation

**data-test-id Attributes:**
- Create Rule button (on transaction row): `data-test-id="create-rule-button"`
- Dialog uses same test IDs as regular create dialog

### Import Review Display

**Reference:** See [`DESIGN-BANK-IMPORT-FRONTEND.md`](../import-export/DESIGN-BANK-IMPORT-FRONTEND.md) for complete import review UI design

**Integration Point:**
- Import review page displays **Category** column (read-only)
- Category value populated by backend via PayeeMatchingRuleFeature during import
- If category is present, display in dedicated column
- If category is empty, show empty cell or placeholder text
- User cannot edit category in import review (per Story 2 acceptance criteria)
- Category is informational only - helps user decide whether to accept transaction

**Visual Treatment:**
- Category displayed with same styling as other transaction fields
- No special highlighting or indication of "auto-matched" status in Stories 1-2
- Future enhancement (Story 5+): Could add indicator showing which rule matched

## Security Considerations

### Tenant Isolation
- All operations scoped to authenticated user's tenant via TenantContext middleware
- PayeeMatchingRules table has TenantId foreign key with CASCADE DELETE
- All queries filter by TenantId (enforced by Feature layer)
- No cross-tenant access possible

### Regex Security (ReDoS Protection)
- **NonBacktracking engine:** All regex operations use `RegexOptions.NonBacktracking` (available in .NET 7+, guaranteed in .NET 10+)
- **Guaranteed linear time:** NonBacktracking provides O(n) time complexity, completely eliminating ReDoS vulnerabilities
- **Validation-time checking:** Patterns validated during create/update using NonBacktracking compilation
- **Unsupported features:** Patterns using backreferences, lookahead, or lookbehind are rejected with user-friendly error messages
- **Runtime safety:** All regex matching uses NonBacktracking, no timeout needed
- **User feedback:** Invalid patterns rejected with clear error messages including .NET Regex error text

### Authorization
- **Viewer role:** Can view payee rules (GET endpoints)
- **Editor role:** Required for creating, updating, or deleting rules (POST, PUT, DELETE endpoints)
- **Owner role:** Not required for any payee rule operations
- **Import integration:** Rule matching applied during import (already requires Editor role)

### Data Integrity
- **Category sanitization:** Applied via [`CategoryHelper.SanitizeCategory()`](src/Application/Helpers/CategoryHelper.cs) **when rules are created/updated** (stored sanitized in database)
- **Matching uses sanitized value:** PayeeMatchingService returns already-sanitized category from rule
- **Empty category validation:** Empty/whitespace-only categories rejected by FluentValidation
- **Max length enforcement:** Database and validation enforce 200 char limits

## Performance Considerations

### Rule Matching Performance
- **Single query per import:** Rules loaded once per import batch, not per transaction
- **In-memory matching:** All pattern testing after initial load
- **Typical rule set:** ~500 rules (~40KB memory)
- **Expected time:** <100ms for 1,000 transactions
- **No N+1 queries:** Bulk load, bulk match

### Database Indexes
- **Key (unique):** Business key lookup for single-rule operations
- **TenantId:** Load all rules for tenant (used by cache on miss)

**Why so minimal?**
- All rules cached in memory after first load
- Rule sets are small (typically 50-200 rules)
- Sorting happens in-memory (negligible cost for small datasets)
- Display and matching both use the same cached dataset
- Eliminates need for sort-specific indexes (PayeePattern, Category, LastUsedAt, ModifiedAt)
- Simpler schema, faster writes, easier maintenance

### Regex Performance
- **On-demand compilation:** Regex patterns compiled during matching (not cached initially)
- **Future optimization:** Consider caching compiled Regex objects with LRU eviction if profiling shows benefit
- **Timeout protection:** 100ms timeout prevents runaway evaluation
- **Pattern complexity:** Most user patterns are simple (substring or basic regex), compilation overhead minimal

### Import Performance Impact
- **Payee matching overhead:** <100ms added to import workflow
- **Compared to existing:** Minimal compared to OFX parsing (~1 second) and duplicate detection
- **No additional DB queries:** Single rule load at start

## Testing Strategy

### Unit Tests

**PayeeMatchingRuleFeature:**
- CRUD operations work correctly
- Tenant isolation (cannot access other tenant's rules)
- NotFoundException thrown for non-existent rules
- ModifiedAt updated on update operations
- Cache invalidation on Create/Update/Delete
- ApplyMatchingRulesAsync correctly modifies transaction DTOs
- Usage statistics updated (MatchCount, LastUsedAt)

**PayeeMatchingHelper:**
- Substring matching case-insensitive
- Regex matching with IgnoreCase and NonBacktracking
- Conflict resolution: regex > substring
- Conflict resolution: longer > shorter substring
- Conflict resolution: newer > older (equal precedence, based on sort order)
- No rules → no matching (null category)
- Multiple rules → best match selected
- Exception propagation: NotSupportedException and ArgumentException thrown to caller

**RegexValidationService:**
- Valid patterns accepted (simple and complex)
- Invalid syntax rejected with error message
- ReDoS vulnerable patterns timeout and rejected
- Edge cases: null, empty, whitespace patterns

**PayeeMatchingRuleEditDtoValidator:**
- Empty PayeePattern rejected
- Empty Category rejected
- Whitespace-only Category rejected
- Max length violations rejected
- Regex validation triggered when PayeeIsRegex=true
- Valid regex accepted, invalid rejected

### Integration Tests

**Controller + Feature + Database:**
- Create rule → stored in database with correct TenantId
- Get rules → returns only current tenant's rules
- Update rule → ModifiedAt timestamp updated
- Delete rule → removed from database
- Invalid regex pattern → 400 Bad Request with error message
- Tenant isolation → cannot access other tenant's rules

**Import Integration:**
- Import with matching rules → Category set on transactions
- Import without rules → Category remains null
- Multiple matching rules → correct precedence applied
- Cross-feature dependency: ImportReviewFeature → PayeeMatchingRuleFeature works correctly
- Usage statistics updated during import

### Functional Tests

**Gherkin scenarios (Stories 1 & 2):**

**Feature: Payee Matching Rules Management**

```gherkin
Scenario: Create a simple substring rule
  Given the user is authenticated and has Editor role
  When the user creates a payee rule with pattern "Amazon" and category "Shopping"
  Then the rule is saved successfully
  And the rule appears in the rules list

Scenario: Create a regex rule
  Given the user is authenticated and has Editor role
  When the user creates a payee rule with pattern "^AMZN.*" (regex) and category "Shopping"
  Then the rule is saved successfully
  And the rule appears in the rules list

Scenario: Invalid regex pattern rejected
  Given the user is authenticated and has Editor role
  When the user creates a payee rule with pattern "(?<invalid" (regex) and category "Shopping"
  Then the request fails with 400 Bad Request
  And the error message contains "Invalid regex pattern"

Scenario: Empty category rejected
  Given the user is authenticated and has Editor role
  When the user creates a payee rule with pattern "Amazon" and category ""
  Then the request fails with 400 Bad Request
  And the error message contains "Category is required"
```

**Feature: Auto-Categorize on Bank Import**

```gherkin
Scenario: Import transactions with matching rule
  Given the user has a payee rule with pattern "Amazon" and category "Shopping"
  When the user imports an OFX file containing a transaction with payee "Amazon.com"
  Then the transaction is shown in import review with category "Shopping"
  And the category is read-only (displayed but not editable)

Scenario: Import transactions without matching rules
  Given the user has no payee matching rules
  When the user imports an OFX file
  Then the transactions are shown with empty category field

Scenario: Conflict resolution - regex beats substring
  Given the user has two rules:
    | Pattern   | Regex | Category  | Modified Date |
    | Amazon    | No    | Online    | 2026-01-01    |
    | ^AMZN.*   | Yes   | Shopping  | 2026-01-01    |
  When the user imports a transaction with payee "AMZN Marketplace"
  Then the transaction is categorized as "Shopping" (regex rule wins)

Scenario: Conflict resolution - longer substring beats shorter
  Given the user has two rules:
    | Pattern      | Regex | Category     | Modified Date |
    | Amazon       | No    | Online       | 2026-01-01    |
    | Amazon Prime | No    | Subscription | 2026-01-01    |
  When the user imports a transaction with payee "Amazon Prime Video"
  Then the transaction is categorized as "Subscription" (longer pattern wins)

Scenario: Conflict resolution - newer rule beats older
  Given the user has two rules:
    | Pattern | Regex | Category     | Modified Date |
    | Amazon  | No    | Online       | 2026-01-01    |
    | Amazon  | No    | E-Commerce   | 2026-01-10    |
  When the user imports a transaction with payee "Amazon.com"
  Then the transaction is categorized as "E-Commerce" (newer rule wins)
```

## Migration and Rollout

### Database Migration
1. Create PayeeMatchingRules table with all indexes
2. Add Category field to ImportReviewTransaction table (nullable)
3. No changes to Transactions table (category already in splits)

### Rollout Strategy
1. Deploy backend with new table, API endpoints, matching service
2. Regenerate API client for frontend
3. Deploy frontend with Payee Rules management page
4. Monitor logs for regex timeout exceptions and matching errors

### Backward Compatibility
- Category field in ImportReviewTransaction is nullable (existing data unaffected)
- PayeeMatchingService handles zero rules gracefully (no-op)
- No breaking changes to existing APIs

### Feature Flag
Not required - feature is net-new, no existing functionality to replace

## Open Questions

**Q:** Should regex patterns be cached after compilation?
**A:** Deferred to implementation. Initial version compiles on-demand. Future optimization: cache compiled Regex objects with LRU eviction policy.

**Q:** Should users see which rule matched a transaction during import review?
**A:** Not in Stories 1 & 2. Future enhancement could add "MatchedRuleKey" field to ImportReviewTransaction for debugging.

**Q:** How to handle bulk rule updates (e.g., bulk delete, bulk enable/disable)?
**A:** Out of scope for Stories 1 & 2. Consider for Story 5 (rule cleanup).

**Q:** Should there be a "test rule" endpoint to preview matches?
**A:** Not in Stories 1 & 2. Could be useful for debugging, consider as future enhancement.

## Future Enhancements

**Story 3:** Manual rule matching - Apply rules to existing transactions
**Story 4:** Advanced matching - Source pattern, amount matching, multiple criteria
**Story 5:** Rule cleanup - Usage tracking (LastUsedAt, MatchCount), bulk delete operations
**Stories 6-7:** Transaction splits - Loan amortization rules, amount-based split rules

## References

**Requirements:**
- [`PRD-PAYEE-RULES.md`](PRD-PAYEE-RULES.md) - Product requirements and user stories
- [`PRD-PAYEE-RULES-REVIEW.md`](PRD-PAYEE-RULES-REVIEW.md) - PRD review and approval

**Design References:**
- [`DESIGN-BANK-IMPORT.md`](../import-export/DESIGN-BANK-IMPORT.md) - Reference architecture pattern
- [`DESIGN-BANK-IMPORT-DATABASE.md`](../import-export/DESIGN-BANK-IMPORT-DATABASE.md) - Entity and migration patterns
- [`DESIGN-BANK-IMPORT-APPLICATION.md`](../import-export/DESIGN-BANK-IMPORT-APPLICATION.md) - Application layer patterns
- [`DESIGN-BANK-IMPORT-API.md`](../import-export/DESIGN-BANK-IMPORT-API.md) - Controller and API patterns

**Related Features:**
- [`PRD-BANK-IMPORT.md`](../import-export/PRD-BANK-IMPORT.md) - Import workflow integration point
- [`PRD-TRANSACTION-RECORD.md`](../transactions/PRD-TRANSACTION-RECORD.md) - Transaction and category model
- [`PRD-TRANSACTION-SPLITS.md`](../transactions/PRD-TRANSACTION-SPLITS.md) - Split handling and category sanitization

**Existing Code Patterns:**
- [`Transaction.cs`](../../src/Entities/Models/Transaction.cs) - Entity pattern reference
- [`TransactionEditDto.cs`](../../src/Application/Dto/TransactionEditDto.cs) - DTO pattern reference
- [`TransactionEditDtoValidator.cs`](../../src/Application/Validation/TransactionEditDtoValidator.cs) - Validation pattern
- [`TransactionsFeature.cs`](../../src/Application/Features/TransactionsFeature.cs) - Feature pattern reference
- [`TransactionsController.cs`](../../src/Controllers/TransactionsController.cs) - Controller pattern reference
- [`CategoryHelper.cs`](../../src/Application/Helpers/CategoryHelper.cs) - Category sanitization

**Project Standards:**
- [`docs/LOGGING-POLICY.md`](../../LOGGING-POLICY.md) - Logging conventions and patterns
- [`.roorules`](../../.roorules) - Project coding standards and patterns
- [`docs/ARCHITECTURE.md`](../../ARCHITECTURE.md) - Clean Architecture principles
- [`docs/TENANCY.md`](../../TENANCY.md) - Multi-tenancy patterns
