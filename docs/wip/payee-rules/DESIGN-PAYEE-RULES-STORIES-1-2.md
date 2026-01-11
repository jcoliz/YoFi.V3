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
- **PayeeMatchingRuleFeature** - Business logic for CRUD operations on rules
- **PayeeMatchingService** - Core matching logic and rule precedence resolution
- **RegexValidationService** - Validates regex patterns and tests for ReDoS vulnerabilities
- **DTOs** - PayeeMatchingRuleEditDto, PayeeMatchingRuleResultDto
- **Validation** - Ensures rules meet schema requirements (category required, valid regex)

### API Layer
- **PayeeMatchingRulesController** - Endpoints for CRUD operations on rules
- **Authentication/Authorization** - Tenant-scoped access control, Editor role required
- **Integration with ImportReviewFeature** - Applies matching during bank import workflow

### Integration Points
- **Bank Import workflow** - After parsing OFX, before storing ImportReviewTransactions, apply matching rules to set Category field
- **Transaction create from Transactions page** - Future enhancement (Story 1 acceptance criteria mentions this)

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
| Category | string | Required, MaxLength(200) | Category to assign when matched, sanitized when applied |
| CreatedAt | DateTimeOffset | Auto-set | Rule creation timestamp |
| ModifiedAt | DateTimeOffset | Auto-updated | Last modified timestamp (used for conflict resolution) |
| Tenant | Tenant? | Navigation property | Foreign key relationship |

**Future fields (Stories 4-7, not in this design):**
- SourcePattern, SourceIsRegex (Story 4 - match by source)
- AmountExact, AmountMin, AmountMax (Story 4 - match by amount)
- LastUsedAt, MatchCount (Story 5 - usage tracking for cleanup)
- Loan details, split rules (Stories 6-7)

### EF Core Migration

**Migration name:** `AddPayeeMatchingRulesTable`

**Table name:** `YoFi.V3.PayeeMatchingRules`

**Required indexes:**
1. `IX_PayeeMatchingRules_Key` (unique) - Business key lookup
2. `IX_PayeeMatchingRules_TenantId_ModifiedAt` (composite, DESC on ModifiedAt) - Tenant-scoped queries ordered by most recently modified (critical for conflict resolution)
3. `IX_PayeeMatchingRules_TenantId_PayeeIsRegex` (composite) - Efficient filtering by pattern type

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
   - Apply all three indexes as specified above
   - Configure DateTimeOffset conversions for SQLite
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

**Location:** `src/Application/PayeeRules/Dto/PayeeMatchingRuleEditDto.cs`

**Purpose:** Input DTO for creating or updating payee matching rules

**Fields:**
- `PayeePattern` (string, required)
- `PayeeIsRegex` (bool)
- `Category` (string, required)

**Validation:** See PayeeMatchingRuleEditDtoValidator section

#### PayeeMatchingRuleResultDto

**Location:** `src/Application/PayeeRules/Dto/PayeeMatchingRuleResultDto.cs`

**Purpose:** Output DTO for displaying payee matching rules

**Fields:**
- `Key` (Guid)
- `PayeePattern` (string)
- `PayeeIsRegex` (bool)
- `Category` (string)
- `CreatedAt` (DateTimeOffset)
- `ModifiedAt` (DateTimeOffset)

### Validation

#### PayeeMatchingRuleEditDtoValidator

**Location:** `src/Application/PayeeRules/Validation/PayeeMatchingRuleEditDtoValidator.cs`

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

**Location:** `src/Application/PayeeRules/Services/`

**Purpose:** Validates regex patterns for correctness and ReDoS vulnerabilities

**Method:** `RegexValidationResult ValidateRegex(string pattern)`

**Returns:** `RegexValidationResult(bool IsValid, string? ErrorMessage)`

**Validation process:**
1. Check pattern is not null/whitespace
2. Attempt to compile with `RegexOptions.IgnoreCase` and 100ms timeout
3. Test compiled pattern against adversarial string: `"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa!"`
4. Catch `RegexMatchTimeoutException` → ReDoS vulnerable, return error
5. Catch `ArgumentException` → Invalid syntax, return .NET error message

**Lifecycle:** Registered as Singleton (stateless service)

#### IPayeeMatchingService / PayeeMatchingService

**Location:** `src/Application/PayeeRules/Services/`

**Purpose:** Applies payee matching rules to transactions

**Key methods:**

**`ApplyMatchingRulesAsync(transactions, tenantId)`**
- Loads all rules for tenant ordered by ModifiedAt DESC
- Iterates through transactions, finds best match for each
- Modifies transaction.Category in-place (mutable DTO)
- Returns Task (no return value, transactions modified)

**`FindBestMatchAsync(payee, tenantId)`**
- Single-payee variant for manual matching (Story 3)
- Returns category string or null if no match

**Matching algorithm (FindBestMatch):**
1. Track separate best regex match and best substring match
2. For each rule:
   - If regex: test with `Regex.IsMatch()` (100ms timeout, IgnoreCase)
   - If substring: test with `payee.Contains(pattern, OrdinalIgnoreCase)`
3. Track longest substring match, first regex match (rules sorted by ModifiedAt DESC)
4. Return: regex match > substring match > null

**Conflict resolution precedence (per Story 2):**
1. Regex pattern beats substring pattern (always)
2. For substring: longer pattern beats shorter
3. For equal length/both regex: most recently modified wins (first in sorted list)

**Performance:**
- Rules loaded once per import batch (single query)
- All matching in-memory after load
- Regex compiled on-demand (not cached initially)
- Expected time: <100ms for 1,000 transactions

**Defensive error handling:**
- `RegexMatchTimeoutException` during matching → treat as non-match
- `ArgumentException` during matching → treat as non-match (shouldn't happen due to validation)

**Lifecycle:** Registered as Scoped (needs IDataProvider)

#### PayeeMatchingRuleFeature

**Location:** `src/Application/PayeeRules/Features/PayeeMatchingRuleFeature.cs`

**Purpose:** Business logic for CRUD operations on payee matching rules

**Constructor:** Accepts `ITenantProvider` and `IDataProvider`, caches `_tenantId`

**Methods:**

**`GetRulesAsync()`**
- Returns all rules for current tenant
- Ordered by ModifiedAt DESC
- Projection to PayeeMatchingRuleResultDto
- Uses no-tracking query

**`GetRuleByKeyAsync(key)`**
- Single rule lookup by Key
- Throws `NotFoundException` if not found or wrong tenant

**`CreateRuleAsync(ruleDto)`**
- Creates new entity, sets TenantId, CreatedAt, ModifiedAt (same timestamp)
- Saves to database, returns PayeeMatchingRuleResultDto with generated Key

**`UpdateRuleAsync(key, ruleDto)`**
- Loads existing rule (throws NotFoundException if not found)
- Updates PayeePattern, PayeeIsRegex, Category
- Updates ModifiedAt to current time
- Saves changes, returns updated DTO

**`DeleteRuleAsync(key)`**
- Loads rule, removes, saves changes
- Throws NotFoundException if not found

**Tenant isolation:** All queries filter by `_tenantId` (from ITenantProvider)

**Lifecycle:** Registered as Scoped

### Integration with Bank Import

**Modified file:** `src/Application/Import/Features/ImportReviewFeature.cs`

**Constructor changes:**
- Add `IPayeeMatchingService payeeMatchingService` parameter

**ImportFileAsync method changes:**

**New step after OFX parsing:**
```
// 2. Apply payee matching rules to set Category field
await payeeMatchingService.ApplyMatchingRulesAsync(
    parsingResult.Transactions,
    tenantProvider.CurrentTenant.Id);
```

**When creating ImportReviewTransaction entities:**
```
Category = transaction.Category, // NEW: Category from matching rules (may be null)
```

**Impact:** Minimal change, single method call added. Matching service gracefully handles zero rules (no-op).

## API Layer Design

### PayeeMatchingRulesController

**Location:** `src/Controllers/PayeeMatchingRulesController.cs`

**Route:** `/api/tenant/{tenantKey:guid}/payee-rules`

**Authorization:** `[RequireTenantRole(TenantRole.Editor)]` at controller level

**Base attributes:**
- `[ApiController]`
- `[Produces("application/json")]`
- Standard ProblemDetails responses: 401, 403, 500

**Constructor:** `PayeeMatchingRuleFeature` and `ILogger<PayeeMatchingRulesController>`

**Endpoints:**

| Method | Path | Description | Request Body | Response | Status Codes |
|--------|------|-------------|--------------|----------|--------------|
| GET | `/` | Get all rules | - | `IReadOnlyCollection<PayeeMatchingRuleResultDto>` | 200, 401, 403 |
| GET | `/{key:guid}` | Get rule by key | - | `PayeeMatchingRuleResultDto` | 200, 401, 403, 404 |
| POST | `/` | Create rule | `PayeeMatchingRuleEditDto` | `PayeeMatchingRuleResultDto` | 201, 400, 401, 403 |
| PUT | `/{key:guid}` | Update rule | `PayeeMatchingRuleEditDto` | `PayeeMatchingRuleResultDto` | 200, 400, 401, 403, 404 |
| DELETE | `/{key:guid}` | Delete rule | - | None | 204, 401, 403, 404 |

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
```
services.AddScoped<PayeeMatchingRuleFeature>();
services.AddSingleton<IRegexValidationService, RegexValidationService>();
services.AddScoped<IPayeeMatchingService, PayeeMatchingService>();
```

**Controller validators** (`src/Controllers/Extensions/ServiceCollectionExtensions.cs`):
```
services.AddScoped<IValidator<PayeeMatchingRuleEditDto>, PayeeMatchingRuleEditDtoValidator>();
```

## Security Considerations

### Tenant Isolation
- All operations scoped to authenticated user's tenant via TenantContext middleware
- PayeeMatchingRules table has TenantId foreign key with CASCADE DELETE
- All queries filter by TenantId (enforced by Feature layer)
- No cross-tenant access possible

### Regex Security (ReDoS Protection)
- **Validation-time testing:** Patterns tested against adversarial input with 100ms timeout during create/update
- **Runtime timeout:** All regex matching uses 100ms timeout via `new Regex(pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100))`
- **Defensive handling:** Timeout exceptions during matching treated as non-match (defensive, shouldn't occur if validation works)
- **User feedback:** Invalid patterns rejected with clear error messages including .NET Regex error text
- **Test string:** `"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa!"` designed to trigger catastrophic backtracking

### Authorization
- **Editor role required:** All payee rule endpoints require Editor or Owner role
- **Viewer restriction:** Viewer role cannot access any payee rule endpoints
- **Import integration:** Rule matching applied during import (already requires Editor role)

### Data Integrity
- **Category sanitization:** Applied via [`CategoryHelper.SanitizeCategory()`](src/Application/Helpers/CategoryHelper.cs) when transactions are accepted (not during matching)
- **Empty category validation:** Empty/whitespace-only categories rejected by FluentValidation
- **Max length enforcement:** Database and validation enforce 200 char limits

## Performance Considerations

### Rule Matching Performance
- **Single query per import:** Rules loaded once per import batch, not per transaction
- **In-memory matching:** All pattern testing after initial load
- **Typical rule set:** 50-200 rules (~50KB memory)
- **Expected time:** <100ms for 1,000 transactions
- **No N+1 queries:** Bulk load, bulk match

### Database Indexes
- **TenantId+ModifiedAt (DESC):** Primary query index, enables efficient "most recent first" sorting for conflict resolution
- **TenantId+PayeeIsRegex:** Optional filtering by pattern type (may not be used initially)
- **Key (unique):** Standard business key lookup

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

**RegexValidationService:**
- Valid patterns accepted (simple and complex)
- Invalid syntax rejected with error message
- ReDoS vulnerable patterns timeout and rejected
- Edge cases: null, empty, whitespace patterns

**PayeeMatchingService:**
- Substring matching case-insensitive
- Regex matching with IgnoreCase
- Conflict resolution: regex > substring
- Conflict resolution: longer > shorter substring
- Conflict resolution: newer > older (equal precedence)
- No rules → no matching (null category)
- Multiple rules → best match selected

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
- Regex timeout during import → transaction not matched (defensive)

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
