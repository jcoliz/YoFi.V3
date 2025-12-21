# Product Requirements Document: Transaction Splits

**Status**: Approved (Detailed Design Complete)
**Created**: 2025-12-21
**Owner**: James Coliz
**Target Release**: V3.0
**ADO**: [Feature 1982](https://dev.azure.com/jcoliz/YoFiV3/_workitems/edit/1982): Transaction Splits

---

## Problem Statement

Personal finance tracking requires categorizing transactions across multiple categories. For example, a single grocery store purchase might include both groceries (Food) and household supplies (Home). Currently, YoFi.V3 only supports a single category per transaction, forcing users to either create artificial splits or accept imprecise categorization. This limits the value of category-based reports and budgeting.

---

## Goals & Non-Goals

### Goals
- [ ] Enable transactions to be split across multiple categories with individual amounts
- [ ] Maintain simple UX for the common case (single category per transaction)
- [ ] Support category-based reporting (primary use case for personal finance)
- [ ] Preserve imported transaction amount as authoritative source of truth
- [ ] Detect and warn users about unbalanced transactions (splits don't sum to transaction amount)

### Non-Goals
- Category hierarchies or parent/child relationships (future enhancement)
- Split templates or saved patterns (future enhancement)
- ML-based category suggestions (future enhancement)
- Multi-currency support (out of scope for V3)

---

## User Stories

### Story 1: User - Split Grocery Transaction
**As a** personal finance user
**I want** to split a grocery store transaction across "Food" and "Home" categories
**So that** my category reports accurately reflect spending in each area

**Acceptance Criteria**:
- [ ] User can add multiple splits to an existing transaction
- [ ] Each split has its own amount and category
- [ ] Splits can be edited individually (amount, category, memo)
- [ ] Splits can be deleted (except the last one - transactions must have at least one split)
- [ ] UI shows warning when splits don't sum to transaction amount

### Story 2: User - View Category Reports
**As a** personal finance user
**I want** to see total spending by category
**So that** I can understand my spending patterns and make budget decisions

**Acceptance Criteria**:
- [ ] Category report sums amounts across all splits (not transactions)
- [ ] Report shows both category name and total amount
- [ ] Can filter by date range
- [ ] Uncategorized splits are included in report with empty/special indicator

### Story 3: User - Simple Single-Category Workflow
**As a** casual user
**I want** to create and edit transactions without thinking about splits
**So that** I can quickly record transactions without complexity

**Acceptance Criteria**:
- [ ] Creating a transaction automatically creates a single split (user doesn't see split complexity)
- [ ] Editing single-category transaction amount updates the single split automatically
- [ ] UI hides split complexity for transactions with only one split
- [ ] Can optionally provide category on transaction creation (flows to the split)

### Story 4: User - Detect Unbalanced Transactions
**As a** detail-oriented user
**I want** to be warned when my splits don't match the transaction amount
**So that** I can catch and correct data entry errors immediately

**Acceptance Criteria**:
- [ ] List view shows visual indicator for unbalanced transactions
- [ ] Detail view shows transaction amount, splits total, and balance status
- [ ] Warning is prominent but doesn't block saving (user's choice to fix later)
- [ ] After editing split amount, balance status updates immediately

### Story 5: User - Import Transactions with Splits
**As a** power user
**I want** to import transactions from my bank and then categorize them with splits
**So that** I can efficiently process monthly statements

**Acceptance Criteria**:
- [ ] Imported transactions have single uncategorized split by default
- [ ] Imported transaction amount is preserved (authoritative)
- [ ] User can add splits to imported transactions
- [ ] Source field indicates import origin (e.g., "MegaBankCorp Checking 0123456789-00")

---

## Technical Approach

Split transactions will be implemented with a new `Split` entity that has a many-to-one relationship with `Transaction`. This preserves the existing transaction-centric UX while enabling flexible categorization.

**Layers Affected**:
- [x] Frontend (Vue/Nuxt) - Split editor UI, list indicators, balance warnings
- [x] Controllers (API endpoints) - Split CRUD endpoints, updated transaction endpoints
- [x] Application (Features/Business logic) - TransactionsFeature with split operations
- [x] Entities (Domain models) - New Split entity, updated Transaction entity
- [x] Database (Schema changes) - Split table with indexes, Transaction table updates

**Key Components**:
- **New**: `src/Entities/Split.cs` - Split entity with Amount, Category, Memo, Order
- **Modified**: `src/Entities/Transaction.cs` - Add Splits navigation, Memo, Source properties
- **Modified**: `src/Application/Features/TransactionsFeature.cs` - Split CRUD operations
- **Modified**: `src/Controllers/TransactionsController.cs` - Split endpoints
- **New**: `src/Application/Dto/SplitEditDto.cs`, `SplitResultDto.cs` - Split DTOs
- **Modified**: `src/Application/Dto/TransactionResultDto.cs` - Add HasMultipleSplits, SingleSplitCategory, IsBalanced

**Database Design**:
- Split table with foreign key to Transaction (cascade delete)
- Indexes: TransactionId, Category, (TransactionId, Order) composite
- Transaction.Amount remains authoritative (imported/manual entry value)
- Splits sum to Transaction.Amount (validation warning if not balanced)

**API Design Pattern**:
- Individual split CRUD operations (POST/PUT/DELETE) - Primary editing pattern
- Transaction creation automatically creates single split (hides complexity)
- Transaction update does NOT modify splits (separate concerns)
- All mutation operations return balance state (IsBalanced flag)

---

## Open Questions

- [x] **Q**: Should we include a HasSplits flag in the transaction list DTO?
  **A**: YES - Include `HasMultipleSplits`, `SingleSplitCategory`, and `IsBalanced` in `TransactionResultDto`

- [x] **Q**: Should splits have a Guid key?
  **A**: YES - Use Guid Key for consistency with Transaction pattern and API flexibility

- [x] **Q**: Atomic replacement vs. individual split operations?
  **A**: **Individual operations are primary pattern** (POST/PUT/DELETE on splits). Matches typical user workflow: create transaction → add splits → edit individual splits

- [x] **Q**: Should Transaction.Amount be editable after creation?
  **A**: YES - User can correct mistakes on manual entry. Editing Amount doesn't auto-adjust splits (creates imbalance warning)

- [x] **Q**: How to enforce "at least one split" rule?
  **A**: Application-level validation before SaveChangesAsync(). DELETE split endpoint returns 400 if attempting to delete last split

---

## Success Metrics

**Feature Adoption**:
- % of users creating multi-split transactions
- Average splits per transaction (expect ~1.1x, with 90%+ having single split)

**Data Quality**:
- % of unbalanced transactions (target: <1% long-term)
- Time to resolve unbalanced state after creation

**Usage Patterns**:
- Category report usage (primary value proposition)
- Most common split categories
- Distribution of split counts (1, 2, 3-5, 6+)

---

## Dependencies & Constraints

**Dependencies**:
- Entity Framework Core migrations for schema changes
- NSwag API client regeneration for frontend
- Existing tenant isolation and authorization

**Constraints**:
- SQLite database (no check constraints across tables)
- Single-currency assumption (no currency codes stored)
- Must maintain backward compatibility with existing transaction data (migrate to single split per transaction)

---

## Notes & Context

**Design Documents**:
- Comprehensive technical design: [`docs/wip/transactions/TRANSACTION-SPLIT-DESIGN.md`](docs/wip/transactions/TRANSACTION-SPLIT-DESIGN.md)
- Original requirements: [`docs/wip/transactions/TRANSACTION-SPLIT-REQUIREMENTS.md`](docs/wip/transactions/TRANSACTION-SPLIT-REQUIREMENTS.md)

**Key Design Decisions**:
1. **Source property stays at Transaction level** - Entire transaction came from one import source
2. **Category is NOT NULL with empty string** - Better performance than NULL, consistent with Payee pattern
3. **Transaction.Amount is authoritative** - Imported value is source of truth, splits are user's categorization
4. **Unbalanced transactions are warning state** - UI prominently displays warnings, but doesn't block saving (user's choice to resolve)
5. **Order property for stable split display** - Users can reorder splits, preference is preserved

**Migration Strategy**:
1. Clear existing transaction data (app is new, no relevant data to preserve)
2. Create Split table with indexes
3. Add Memo and Source columns to Transaction table
4. Update application code to work with splits
5. Frontend updates (separate task)

**Testing Strategy**:
- Unit tests for TransactionsFeature (split CRUD, balance calculations)
- Integration tests for Controllers (API endpoints, validation)
- Integration tests for Data layer (cascade delete, ordering)
- Follow existing patterns: Gherkin comments, NUnit assertions

---

## Handoff Checklist (for AI implementation)

Implementation completed:
- [x] Database schema designed with indexes
- [x] Entity models created/updated
- [x] DTOs designed for all operations
- [x] API endpoints specified
- [x] Query patterns documented
- [x] Validation rules defined
- [x] Balance calculation logic specified
- [x] Testing strategy documented

**Reference Implementation**: See [`docs/wip/transactions/TRANSACTION-SPLIT-DESIGN.md`](docs/wip/transactions/TRANSACTION-SPLIT-DESIGN.md) for:
- Complete entity definitions with XML comments
- All DTO definitions with validation attributes
- 9 documented query patterns with EF Core syntax
- API endpoint specifications
- Index strategy and performance analysis
- Migration steps
- Comprehensive test scenarios
