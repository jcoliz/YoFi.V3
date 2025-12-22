# Product Requirements Document: Transaction Record

**Status**: Approved (Detailed design complete)
**Created**: 2025-12-20
**Owner**: James Coliz
**Target Release**: V3.0
**ADO**: [Link to ADO Item]

---

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

- Split transactions (single transaction into multiple categories)
- Automated categorization or machine learning suggestions
- Transaction attachments (receipts, images)
- Multi-currency support
- Recurring transaction templates
- Budget tracking or alerts
- Generation of reports from categorized transactions

---

## User Stories

### Story 1: User - Represent imported data
**As a** user
**I want** retain a faithful copy of bank information locally
**So that** I can later reason over all of my financial records in one place

**Acceptance Criteria**:
- [ ] Bank date information retained
- [ ] Bank transaction amount retained
- [ ] Bank account source information retained: what bank and account did this come from? Free text, typically populated with Bank Name, Account Type, and Account number, but can be any text.
- [ ] Bank payee retained
- [ ] Bank unique identifier (to protect against duplicates)

### Story 2: User - Add additional information
**As a** user
**I want** add additional information on each transaction
**So that** I can later group and sort, or search by that information, or so that I can later remember more details about it.

**Acceptance Criteria**:
- [ ] Can add free text categories, at unlimited depth, separated by `:`
- [ ] Can add a memorandum (memo) field with additional text to provide additional context

Note that valid categories are described by the following regular expression. The only **invalid** category value is one with excess whitespace at the start or end, or around a separator. We will always accept "invalid" values, and just trim the erroneous whitespace.

```re
^(?:\S(?:[^:])*?\S|\S)(?::(?:\S(?:[^:])*?\S|\S))*$
```

### Story 3: User - Manage transactions
**As a** user
**I want** to edit transaction details or delete them
**So that** I can reconcile my system to my understanding of what actually happened

**Acceptance Criteria**:
- [ ] Edit all fields
- [ ] Delete a transaction

---

## Technical Approach (Optional)

[Brief description of the intended technical approach, if you have one in mind]

**Layers Affected**:
- [X] Frontend (Vue/Nuxt)
- [X] Controllers (API endpoints)
- [X] Application (Features/Business logic)
- [X] Entities (Domain models)
- [X] Database (Schema changes)

**Key Components**:
- This is largely to cover the rationale for the **schema** of a transaction, so it's the requirements for the Transaction entity.
- Of course, the added fields will need to be plumbed up through the stack.
---

## Open Questions

- [x] **Q**: Will you support multiple accounts per tenant (e.g., Checking, Savings, Credit Card)?
  **A**: YES - account source is not rigorously defined. The idea is that user brings all transactions from all bank accounts into one set of transactions.

- [x] **Q**: Should `Source` be a separate Account entity with its own table?
  **A**: NO. This will be populated by the importer, but can be any test. Precise account source isn't something that needs ongoing tracking.

- [x] **Q**: Category as Single string field ("Bills:Utilities:Electric") or normalized table?
  **A**: Single string field. A normalized table is overkill. User needs to rapidly enter whatever they want, and we'll figure it out.

- [x] **Q**: Category max length?
  **A**: 200 is probably fine

- [x] **Q**: Pre-seeded common categories?
  **A**: No seeding because there's no data. User constructs category structure by assigning categories to transactions on the fly

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
  - References existing [`Transaction`](src/Entities/Models/Transaction.cs:13) entity
  - References existing [`TransactionEditDto`](src/Application/Dto/TransactionEditDto.cs:21) and [`TransactionResultDto`](src/Application/Dto/TransactionResultDto.cs:14)
  - References multi-tenancy infrastructure (implemented)
  - References Transaction CRUD API endpoints (partially implemented)
  - Links to related [`PRD-TRANSACTION-SPLITS.md`](docs/wip/transactions/PRD-TRANSACTION-SPLITS.md:1)

### Summary

**The PRD is complete and ready for implementation.** All handoff checklist items are satisfied. The document provides clear requirements for adding four new fields to the Transaction entity:
- `Category` (string?, 200 chars) - User categorization
- `Memo` (string?, 1000 chars) - Additional context
- `Source` (string?, 200 chars) - Bank account source
- `ExternalId` (string?, 100 chars) - Bank's unique ID

Existing fields (Date, Payee, Amount) already satisfy Story 1 requirements. The feature can be implemented by updating the entity schema and plumbing the new fields through the existing CRUD stack.
