---
status: Implemented
target_release: Alpha 1
design_document: TRANSACTION-RECORD-DESIGN.md
functional_test_plan: TRANSACTION-RECORD-FUNCTIONAL-TEST-PLAN.md
functional_test_implementation_plan: TRANSACTION-RECORD-FUNCTIONAL-TEST-IMPLEMENTATION-PLAN.md
ado: "[Link to ADO Item]"
implementation_notes: |
  All 8 functional test scenarios implemented and passing in both local and container environments.
  Backend API endpoints, Application Features, Entity models, and database schema all complete.
  Frontend UI for transaction details page implemented with full CRUD support for all fields.
---

# Product Requirements Document: Transaction Record

## Problem Statement

Users need a local copy of their financial transactions from various institutions, collected
in one convenient place. They need a faithful record of the information discovered from the
bank, and also need the ability to augment the transactions with additional context based
on their understanding of each transaction.

---

## Goals & Non-Goals

### Goals
- Enable users to import bank transaction data and retain original information
- Allow users to categorize transactions for reporting and analysis
- Support adding contextual notes (memos) to transactions
- Enable full CRUD operations on transaction records

### Non-Goals

All of these will be considered as separate features. The goal of this is to get started with the basics.

- Split transactions (see [Transaction Splits PRD](./PRD-TRANSACTION-SPLITS.md))
- Automated categorization (see [Payee Matching Rules PRD](../payee-rules/PRD-PAYEE-RULES.md))
- Transaction attachments (receipts, images) (see [Receipts Inbox PRD](./PRD-TRANSACTION-ATTACHMENTS.md))
- Generation of reports from categorized transactions (see [Reports PRD](../reports/PRD-REPORTS.md))
- Budget tracking or alerts (see [Budgets PRD](../budgets/PRD-BUDGETS.md))
- Recurring transaction templates
- Multi-currency support
- Automatic machine learning suggestions

---

## User Stories

### Story 1: User - Represent imported data
**As a** user
**I want** retain a faithful copy of bank information locally
**So that** I can later reason over all of my financial records in one place

**Acceptance Criteria**:
- [x] Bank date information retained (`Date` field - DateOnly)
- [x] Bank transaction amount retained (`Amount` field - decimal)
- [x] Bank account source information retained: what bank and account did this come from? Free text, typically populated with Bank Name, Account Type, and Account number, but can be any text. (`Source` field - string?, 200 chars)
- [x] Bank payee retained (`Payee` field - string, required)
- [x] Bank unique identifier (to protect against duplicates) (`ExternalId` field - string?, 100 chars)

### Story 2: User - Add additional information
**As a** user
**I want** add additional information on each transaction
**So that** I can later group and sort, or search by that information, or so that I can later remember more details about it.

**Acceptance Criteria**:
- [ ] Can add free text categories, at unlimited depth, separated by `:` *** [Superseded] *** Categories are attached to the Splits (see [`PRD-TRANSACTION-SPLITS.md`](./PRD-TRANSACTION-SPLITS.md))
- [x] Can add a memorandum (memo) field with additional text to provide additional context (`Memo` field - string?, 1000 chars, backend complete)

### Story 3: User - Manage transactions
**As a** user
**I want** to edit transaction details or delete them
**So that** I can reconcile my system to my understanding of what actually happened

**Acceptance Criteria**:
- [x] Edit all fields (PUT endpoint implemented, all fields editable via API)
- [x] Delete a transaction (DELETE endpoint implemented)

---

## Technical Approach (Optional)

**Layers Affected**:
- [X] Frontend (Vue/Nuxt)
- [X] Controllers (API endpoints)
- [X] Application (Features/Business logic)
- [X] Entities (Domain models)
- [X] Database (Schema changes)

**Key Components**:
- This is largely to cover the rationale for the **schema** of a transaction, so it's the requirements for the Transaction entity.
- Of course, the added fields will need to be plumbed up through the stack.

### Key Business Rules

#### Ad-hoc categories [Moved to PRD-TRANSACTION-SPLITS.md]

Categories are now attached to Splits, not Transactions. See the "Category Sanitization" section in [`PRD-TRANSACTION-SPLITS.md`](./PRD-TRANSACTION-SPLITS.md) for detailed category cleanup rules.

---

## Open Questions

- [x] **Q**: Will you support multiple accounts per tenant (e.g., Checking, Savings, Credit Card)?
  **A**: YES - account source is not rigorously defined. The idea is that user brings all transactions from all bank accounts into one set of transactions.

- [x] **Q**: Should `Source` be a separate Account entity with its own table?
  **A**: NO. This will be populated by the importer, but can be any test. Precise account source isn't something that needs ongoing tracking.

- [x] **Q**: Category as Single string field ("Bills:Utilities:Electric") or normalized table? [Moved to PRD-TRANSACTION-SPLITS.md]
  **A**: Single string field. Categories are now on Splits. See [`PRD-TRANSACTION-SPLITS.md`](./PRD-TRANSACTION-SPLITS.md).

- [x] **Q**: Category max length? [Moved to PRD-TRANSACTION-SPLITS.md]
  **A**: 200 characters. See [`PRD-TRANSACTION-SPLITS.md`](./PRD-TRANSACTION-SPLITS.md).

- [x] **Q**: Pre-seeded common categories? [Moved to PRD-TRANSACTION-SPLITS.md]
  **A**: No seeding. See [`PRD-TRANSACTION-SPLITS.md`](./PRD-TRANSACTION-SPLITS.md).

- [x] **Q**: Memo length
  **A**: 1000 probably fine

- [x] **Q**: Memo rich text
  **A**: NO. Overkill. Plain text is plenty

- [x] **Q**: Memo nullable
  **A**: YES. Most transactions will not have memos.

- [x] **Q**: Do you need an audit trail for edited transactions?
  **A**: NO. Not in this feature. Can consider as an additional feature later.

- [x] **Q**: Question: How will you handle duplicate imports (same transaction imported twice)?
  **A**: Record will include a `Bank unique identifier`. Importer responsible for filling that in, and keeping out duplicates

- [x] **Q**: ImportSource (string, nullable - e.g., "chase_checking_2024-12.csv")
  **A**: OK

- [x] **Q**: public DateTime? ImportedDate { get; set; }    // When imported
  **A**: NO. I have not found myself wanting this information

---

## Success Metrics

- All imported bank fields are visible and editable in UI
- Users can successfully categorize 100% of transactions
- Edit/delete operations complete within 200ms
- Zero data loss during edit operations

---

## Dependencies & Constraints

**Dependencies**:
- Related to: Assigning multiple categories to a single transaction is covered separately in docs\wip\transactions\PRD-TRANSACTION-SPLITS.md.
- How the importing mechanism works will be covered separately in design of importer feature.
- Depends on: Multi-tenancy infrastructure (implemented)
- Depends on: Transaction CRUD API endpoints (partially implemented)

**Constraints**:
- Constraint: NOT REQUIRED: Backward compatibility with existing Transaction data not important. No existing actual real data to be preserved.
- Constraint: Schema migration required (will affect existing data)

---

## Notes & Context

[Any additional context, links to related documents, or background information]

---

## ✅ Handoff Checklist Review: Transaction Record PRD

### Checklist Status

- ✅ **All user stories have clear acceptance criteria**
  - Story 1: 5 specific criteria for imported data (Date, Amount, Source, Payee, ExternalId)
  - Story 2: 2 criteria for user augmentation (Category, Memo)
  - Story 3: 2 criteria for management (Edit, Delete)

- ✅ **Open questions are resolved or documented as design decisions**
  - All 12 questions answered with clear decisions
  - Key decisions: Source as string (not entity), Category as string with `:` delimiters, Memo 1000 chars nullable, no audit trail, ExternalId for duplicates

- ✅ **Technical approach section indicates affected layers**
  - All 5 layers marked as affected (Frontend, Controllers, Application, Entities, Database)
  - Key components identified: Transaction entity schema, plumbing through stack

- ✅ **Any existing code patterns or files to reference are noted**
  - Links to related [`PRD-TRANSACTION-SPLITS.md`](docs/wip/transactions/PRD-TRANSACTION-SPLITS.md:1)

### Summary

**The PRD is complete and ready for implementation.** All handoff checklist items are satisfied. The document provides clear requirements for adding three new fields to the Transaction entity:
- `Memo` (string?, 1000 chars) - Additional context
- `Source` (string?, 200 chars) - Bank account source
- `ExternalId` (string?, 100 chars) - Bank's unique ID

Note: Categories are now attached to Splits (see [`PRD-TRANSACTION-SPLITS.md`](./PRD-TRANSACTION-SPLITS.md)), not directly to Transactions.

Existing fields (Date, Payee, Amount) already satisfy Story 1 requirements. The feature can be implemented by updating the entity schema and plumbing the new fields through the existing CRUD stack.
