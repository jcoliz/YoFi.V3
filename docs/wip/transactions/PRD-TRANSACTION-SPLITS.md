---
status: Approved (Detailed design complete)
owner: James Coliz
target_release: Beta 2
ado: "[Feature 1982](https://dev.azure.com/jcoliz/YoFiV3/_workitems/edit/1982): Transaction Splits"
---

# Product Requirements Document: Transaction Splits

> **Note**: See [`PRD-GUIDANCE.md`](../PRD-GUIDANCE.md) for guidance on PRD scope. Implementation details are in [`TRANSACTION-SPLIT-DESIGN.md`](TRANSACTION-SPLIT-DESIGN.md).

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

### Story 1: User - Split Single Transaction
**As a** personal finance user
**I want** to split a grocery store transaction across "Food" and "Home" categories
**So that** my category reports accurately reflect spending in each area

**Acceptance Criteria**:
- [ ] User can add multiple splits to an existing transaction
- [ ] Each split has its own amount and category
- [ ] Entire list of splits can be viewed from a "transaction detail" page showing all aspects of a transaction
- [ ] Splits can be edited individually (amount, category, memo)
- [ ] Splits can be deleted (except the last one - transactions must have at least one split)
- [ ] UI shows warning when splits don't sum to transaction amount
- [ ] Split amounts can be negative or positive. Negative indicates money flowing away from us, positive indicates money flowing toward us.

### Story 2: User - View Category Reports [SUPERSEDED]
**As a** personal finance user
**I want** to see total spending by category
**So that** I can understand my spending patterns and make budget decisions

> [!WARNING] This story is superseded by the [Reports](../reports/PRD-REPORTS.md) feature.

### Story 3: User - Simple Single-Category Workflow
**As a** casual user
**I want** to create and edit transactions without thinking about splits
**So that** I can quickly record transactions without complexity

**Acceptance Criteria**:
- [ ] Creating a transaction automatically creates a single split (user doesn't see split complexity)
- [ ] Editing single-category transaction amount updates the single split automatically
- [ ] UI hides split complexity for transactions with only one split
- [ ] Can optionally provide category on transaction creation (flows to the split)
- [ ] Non-compliant categories are automatically sanitized before saving to database (see Category Sanitization rules)

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

### Story 6: User - Upload splits
**As a** detail-oriented user
**I want** to upload an Excel spreadsheet of split data
**So that** I don't have to enter a very long list of splits by hand (e.g. for my paystub)

**Acceptance Criteria**:
- [ ] User can upload a spreadsheet (Excel .xlsx) containing split data, from the "transaction detail" page.
- [ ] Required columns: Category, Amount (optional columns: Memo). Column headers are required.
- [ ] Additional columns. If uploaded sheet has needless columns, they are ignored in this process.
- [ ] Invalid input cancels entire import, with an error message displayed to user.
- [ ] Uploaded splits append to any existing splits for the transaction
- [ ] Validation errors are shown clearly before committing changes
- [ ] User can include any categories (see Transactions Record PRD for discussion of ad-hoc categories). Categories will be silently cleaned up to ensure valid category is saved (even if blank)
- [ ] If sum of uploaded splits does not equal transaction amount, will show balance warning.
- [ ] UI provides a downloadable template file showing expected format
- [ ] Order is assigned automatically based on row sequence in file
- [ ] Upload results are committed immediately, and UI updated. User cannot preview the import, as with bank importing.
- [ ] User cannot upload any other format that Excel .xlsx

**Performance Characteristics**
Limits and constraints will be set during performance testing, as these constraints are functions of the system, not the feature.

- Maximum splits per upload
- Maximum upload

### Stories under consideration

- Drop-down selector for category edit on quick-edit (V1 parity) also should do this on transaction detail edit
- Disable category edit in case of multiple splits

---

## Technical Approach

Split transactions will be implemented with a new `Split` entity that has a many-to-one relationship with `Transaction`. This preserves the existing transaction-centric UX while enabling flexible categorization.

### Layers Affected

- [x] Frontend (Vue/Nuxt) - Split editor UI, list indicators, balance warnings
- [x] Controllers (API endpoints) - Split CRUD endpoints, updated transaction endpoints
- [x] Application (Features/Business logic) - TransactionsFeature with split operations
- [x] Entities (Domain models) - New Split entity, updated Transaction entity
- [x] Database (Schema changes) - Split table with indexes, Transaction table updates

### High-Level Entity Concepts

**Split Entity** (new):
- Amount (allocated to this category)
- Category (empty string for uncategorized)
- Memo (optional, split-specific notes)
- Order (for display sequencing)
- TransactionId (foreign key)

**Transaction Entity** (modified):
- Add Splits navigation collection
- Add Memo property (transaction-level notes)
- Add Source property (import origin)
- Amount remains authoritative (imported value, not sum of splits)

### Key Business Rules

- Every transaction must have at least one split
- Transaction.Amount is authoritative (imported or manually entered)
- Splits sum should match Transaction.Amount (warning if not, but not enforced)
- Category is empty string for uncategorized (not NULL)
- Source property stays at Transaction level (entire transaction from one import)
- Unbalanced transactions are warning state, not error (user's choice to resolve)

#### Category Sanitization

Throughout all of YoFi, categories are free text strings. User is not expected to match against existing categories. Categories are never validated against some list.

If user includes a `:` character in them, this signifies a hierarchy of categories, e.g. "Home:Utilities" signifies that the categorized split concerns "Utilities", and in reports it should be grouped and subtotaled under "Home". See [`../reports/PRD-REPORTS.md`](../reports/PRD-REPORTS.md) for details.

**Whitespace requirements:**
- No whitespace to start or end the category
- No whitespace surrounding a `:` separator
- No more than one consecutive space inside a term

**Capitalization requirements:**
- All words in categories are capitalized, regardless of whether they are "small words". Just to keep it consistent.
- Capitalization inside words is optional

**Empty term requirements:**
- After dividing a category up into delimited terms, and trimming/consolidating whitespace as described above, each term cannot be empty

> [!IMPORTANT] Invalid categories are never rejected, nor are warnings generated. They are simply cleaned up to be valid before saving.

**Examples**

| Input | Cleaned up version saved to storage |
| --- | --- |
| homeAndGarden | HomeAndGarden |
| Home andGarden | Home AndGarden |
| Home and Garden | Home And Garden |
| Home    and Garden | Home And Garden |
| " " | \<blank\> |
| Home: | Home |
| :Home | Home |
| : | \<blank\> |
| Home::Garden | Home:Garden |
| Home :Garden | Home:Garden |
| Home : Garden | Home:Garden |
| \<space\>Home | Home |
| Home\<space\> | Home |

**Expected Implementation**

Best place to handle this cleanup is within the application layer (Transactions Feature or Splits Feature).

**Category Properties:**
- Maximum length: 200 characters
- No pre-seeded categories - User constructs category structure by assigning categories to splits on the fly
- Categories are not "created" or "exist" - they are simply text values assigned to splits
- No maximum distinct categories: If there are many categories, we'll have to handle it on the backend (e.g. most commonly used) later on as a post-implementation refinement.

**Category Matching**
- Categories match without regard for case, e.g. "FOOD" matches "Food"

### Security considerations

- File upload endpoints need size limits and MIME type validation. Exact limits subject to further security research.

### Code Patterns to Follow

- Entity pattern: [`BaseTenantModel`](../../src/Entities/Models/BaseTenantModel.cs) for Split entity, [`BaseTenantModel`](../../src/Entities/Models/BaseTenantModel.cs) for Transaction
- CRUD operations: [`TransactionsController.cs`](../../src/Controllers/TransactionsController.cs) and [`TransactionsFeature.cs`](../../src/Application/Features/TransactionsFeature.cs)
- Tenant-scoped authorization: Existing transaction endpoints
- Testing: NUnit with Gherkin comments (Given/When/Then)

---

## Open Questions

- [x] **Q**: The example mentions "paystub" - typically paystubs have Multiple deduction categories (taxes, insurance, 401k, etc.). Are these positive amounts (deductions from gross pay) or negative amounts (contra-entries)? Should the upload support both positive and negative split amounts?
  **A**: Updated story #1 to point out that all splits allow negative and positive

- [x] **Q**: Category Auto-Creation. If a category doesn't exist yet, should it be created automatically from the upload? Or should only existing categories be allowed (validation error if not found)?
  **A** Categories are not "created". Categories don't "exist". See "Ad-hoc categories" in [](../transactions/PRD-TRANSACTION-RECORD.md)

- [X] **Q**:  Technical Implementation Scope. Backend file parsing - which library? (EPPlus, ClosedXML, CsvHelper?) Maximum file size limits? Maximum number of splits per upload? Is this a new API endpoint or enhancement to existing endpoint?
  **A** These are implementation details, out of scope for "product requirements"

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

**Related Documents**:
- **Design Document**: [`TRANSACTION-SPLIT-DESIGN.md`](TRANSACTION-SPLIT-DESIGN.md) - Complete implementation details (entity definitions, DTOs, API endpoints, query patterns)
- **Original Requirements**: [`TRANSACTION-SPLIT-REQUIREMENTS.md`](TRANSACTION-SPLIT-REQUIREMENTS.md) - Initial requirements gathering

**Historical Context**:
This feature enables the primary use case for personal finance tracking: category-based reporting. Without splits, users are forced to choose a single category for mixed purchases, reducing report accuracy.

**Key Design Decisions** (rationale for requirements):
1. **Source property stays at Transaction level** - Entire transaction came from one import source, not per-split
2. **Category is NOT NULL with empty string** - Better query performance than NULL, consistent with existing Payee pattern
3. **Transaction.Amount is authoritative** - Imported value is source of truth; splits are user's categorization overlay
4. **Unbalanced transactions are warning state** - User can temporarily save unbalanced state (convenience), but UI prominently warns them
5. **At least one split required** - Business rule ensuring every transaction is categorizable
6. **Order property for stable split display** - Users can reorder splits; their preference persists across sessions

---

## Handoff Checklist (for AI implementation)

Implementation completed - feature is working in code:
- [x] All user stories have clear acceptance criteria
- [x] Open questions are resolved or documented as design decisions
- [x] Technical approach section indicates affected layers
- [x] Code patterns to follow are referenced
- [x] Companion design document created with implementation details

**Implementation Reference**: See [`TRANSACTION-SPLIT-DESIGN.md`](TRANSACTION-SPLIT-DESIGN.md) for details.

### Updated Requirements - Excellent Coverage

I've reviewed the updated PRD for Story 6 "Upload splits". The updates comprehensively address all the questions I raised:

**File Format & Validation** (Lines 98-110):
- ✅ Format specified: Excel .xlsx only
- ✅ Required columns: Category, Amount (optional: Memo)
- ✅ Headers required, extra columns ignored
- ✅ Invalid input cancels entire import with error message
- ✅ Validation errors shown before committing
- ✅ Categories cleaned up automatically (ad-hoc categories supported)
- ✅ Downloadable template provided

**UX Flow** (Lines 98, 102, 108):
- ✅ Upload from "transaction detail" page
- ✅ Appends to existing splits (doesn't replace)
- ✅ Results committed immediately (no preview)
- ✅ Order auto-assigned from row sequence

**Technical & Performance** (Lines 112-116, 156-157):
- ✅ Performance limits deferred to testing (appropriate)
- ✅ Security considerations documented (size/MIME validation)
- ✅ Implementation details appropriately marked as out-of-scope

**Business Logic** (Lines 46-50, 170-171):
- ✅ Positive/negative split amounts supported (Story 1 updated)
- ✅ Ad-hoc categories clarified (links to Transaction Record PRD)
- ✅ Balance warning if sum doesn't match transaction amount

## No Further Questions

The PRD is now complete and ready for implementation. All acceptance criteria are clear, open questions are resolved, and the scope is well-defined. The document properly separates product requirements from implementation details, and references related documentation appropriately.

---

## Resuming Implementation

Taking a break from implementation to work on some design questions. Resumption
prompt follows:

```
Continue implementing docs/wip/transactions/PRD-TRANSACTION-SPLITS.md
following docs/wip/IMPLEMENTATION-WORKFLOW.md.

We completed Steps 1-5 (Entities, Data Layer, Data Integration Tests).
Continue with Step 6: Application Layer (DTOs and TransactionsFeature).

Alpha-1 scope: Stories 3 & 5 only (single-split auto-creation, simple workflow).
```
