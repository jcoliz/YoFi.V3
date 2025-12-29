---
status: Approved
owner: James Coliz
target_release: Beta 2
ado: TBD
references:
- VISUAL-DESIGN-BANK-IMPORT.md
- MOCKUP-BANK-IMPORT.md
- IMPORT-REVIEW-DATA-MODEL.md
- OFX-LIBRARY-EVALUATION.md

---

# Product Requirements Document: Transaction Bank Import

## Problem Statement

Users regularly download transaction data from their banks in OFX or QFX formats and need to import this data into YoFi. Without a proper import workflow, users face two major problems:
1. **Manual data entry** - Tedious and error-prone to manually enter each transaction
2. **Duplicate transactions** - Re-downloading and importing the same date range creates duplicates

Users need a streamlined import process with duplicate detection and review capabilities before transactions are permanently added to their workspace.

---

## Goals & Non-Goals

### Goals
- [ ] Enable import of **Transactions** from bank formats (OFX and QFX)
- [ ] Provide **import review workflow** where users can inspect transactions before accepting
- [ ] Detect and highlight **duplicate transactions** (exact matches and potential conflicts)
- [ ] Support **persistent review state** so users can return to pending imports across sessions
- [ ] Provide clear validation feedback when import files are malformed
- [ ] Maintain data integrity and tenant isolation during all operations
- [ ] Support typical bank download sizes efficiently (hundreds to thousands of records)

### Non-Goals
- Real-time bank integration via APIs (Plaid, etc.) - separate feature
- Import from XLSX format - covered by Tenant Data Administration feature
- Export to OFX/QFX formats (bank formats are import-only)
- Import/export of Splits independently (bank formats don't include split data)
- Scheduled/automated imports
- Cross-tenant import (security concern)
- Import of other entity types (Budget Transactions, Payees) - bank formats only contain transactions

---

## User Stories

### Story 1: User - Upload Bank File for Import

**As a** YoFi user
**I want** to upload an OFX or QFX file downloaded from my bank
**So that** I can import my transactions into YoFi without manual entry

**Acceptance Criteria**:
- [ ] User can navigate to Transactions Import page
- [ ] User can select and upload bank format file (.ofx or .qfx extensions)
- [ ] System validates file format and provides clear error messages for invalid files
- [ ] System parses bank file and extracts transaction data (date, payee, amount, memo)
- [ ] System stores transactions in temporary "import review" state (separate from primary transactions)
- [ ] User is redirected to Import Review page showing all imported transaction candidates
- [ ] System extracts bank-provided transaction ID as Key when available
- [ ] System generates hash Key from (Date + Amount + Payee) when bank ID not available

### Story 2: User - Review Imported Transactions Before Accepting

**As a** YoFi user
**I want** to review imported transactions and see which are duplicates
**So that** I can avoid adding duplicate transactions to my records

**Acceptance Criteria**:
- [ ] Import Review page displays all transactions from most recent import
- [ ] Transactions are categorized into three groups:
  - **New transactions** - No conflicts, selected by default
  - **Exact duplicates** - Same Key/hash and same data, deselected by default
  - **Potential duplicates** - Same Key/hash but different data, highlighted and deselected by default
- [ ] Each category is clearly labeled with count (e.g., "12 New Transactions", "3 Exact Duplicates")
- [ ] User can expand/collapse each category section
- [ ] User can select/deselect individual transactions for import
- [ ] User can select/deselect all transactions in a category
- [ ] Potential duplicates show comparison view (imported vs. existing data)
- [ ] User can click "Accept Selected" to move approved transactions into primary transactions
- [ ] System shows confirmation: "X transactions accepted, Y transactions remain in review"

### Story 3: User - Manage Import Review State

**As a** YoFi user
**I want** to return to my pending import review later
**So that** I can review transactions when convenient without time pressure

**Acceptance Criteria**:
- [ ] User can leave Import Review page at any time
- [ ] Import review state persists across browser sessions
- [ ] User can return to Import Review page to see pending transactions
- [ ] User can upload additional bank files while previous import is still in review
- [ ] Multiple imports are merged into single review queue (not independent batches)
- [ ] User can click "Delete All" to wipe entire import review queue and start fresh
- [ ] Transactions in import review state are NOT included in:
  - Transaction list pages
  - Reports and analytics
  - Exports
  - Balance calculations

### Story 4: User - Handle Import Errors Gracefully

**As a** YoFi user
**I want** clear feedback when my import file has errors
**So that** I can correct the issues and successfully import my data

**Acceptance Criteria**:
- [ ] System displays specific error messages for common issues:
  - "Unsupported file format - expected OFX or QFX"
  - "File appears corrupted - unable to parse transaction data"
  - "Transaction X: Invalid date format"
  - "Transaction X: Missing required field (amount/date)"
- [ ] System continues processing valid transactions when possible
- [ ] User sees summary: "X transactions imported for review, Y transactions failed"
- [ ] User can see details of failed transactions with reasons
- [ ] System does not create partial/incomplete transaction records
- [ ] Standard controller logging is applied (who imported, when, how many records)

---

## Technical Approach

### Duplicate Detection Strategy

**Bank formats may or may not include unique transaction identifiers:**
- **If bank provides unique transaction ID** (FITID in OFX/QFX): Use as Uniqueness Key for duplicate detection
- **If no unique ID provided**: Generate hash from `(Date + Amount + Payee)` as duplicate detection key
- **Hash algorithm**: Use consistent, deterministic hash (e.g., SHA256 of concatenated values)

**Comparison logic for categorization:**
1. **New transaction**: Key/hash not found in existing transactions or import review state
2. **Exact duplicate**: Key/hash matches AND all transaction fields match (Date, Amount, Payee, Memo)
3. **Potential duplicate**: Key/hash matches BUT one or more fields differ

### File Format Support

- OFX 2.x (XML-based)
- QFX / OFX 1.x (SGML-like)

**Category handling**:
- Bank files typically don't include category data; field left blank initially

### Layers Affected

- **Frontend** - UI for file upload, import review interface with categorization
- **API** - Endpoints for file upload, review state query, accept/delete operations
- **Application** - Business logic for parsing, validation, duplicate detection
- **Database** - Transactions under review will need to be identified in some way

---

## Dependencies & Constraints

**Dependencies**:
- Multi-tenancy framework must be fully functional (tenant isolation)
- Transaction CRUD operations must be complete
- Authentication/authorization must be working
- Database schema must support import review state. During detailed design, we will decide whether a unique table is better, or adding a flag to all transactions.

**Constraints**:
- Must maintain tenant data isolation (users cannot see other users' pending imports)
- Import/export operations should complete within reasonable time for typical file sizes (up to 10,000 transactions)
- Must handle common file encoding variations (UTF-8, Windows-1252)
- OFX/QFX parsing should be lenient to handle variations across different banks
- File uploads must be validated for security (file type, size, content scanning)
- Import review state must be isolated per user/tenant
- Only GUID Keys are used - no database auto-increment IDs exposed

---

## Open Questions

- [ ] What is the exact field name for unique transaction ID in OFX and QFX formats? (FITID appears standard, verify during implementation)
- [ ] What file size limits should we enforce? (Defer until real-world testing provides data)
- [X] Should we provide user guidance/tooltips explaining the import review workflow for first-time users? YES, in the future
- [X] Should there be a limit on how many pending transactions a user can have in review state? NO
- [X] Should we expire/auto-delete old import review records after N days of inactivity? NO. User can just hit delete button and wipe it out if they don't want it.
- [X] Do we need pagination for import review page if user imports very large files? YES for sure. It's easy to get an unwieldy volume of data.

---

## Success Metrics

- **Adoption**: 60%+ of active users utilize bank import within first 30 days
- **Volume**: Users import average of 100+ transactions per session
- **Error Rate**: <5% of import attempts fail with validation errors
- **Duplicate Prevention**: 80%+ of users mark some transactions as duplicates during review
- **Format Distribution**: Track OFX vs. QFX usage to inform priorities
- **Review Completion Time**: Average time from upload to acceptance <5 minutes

---

## Notes & Context

**YoFi V1 Reference**:
The original YoFi V1 application (https://github.com/jcoliz/yofi) implemented OFX/QFX import functionality. This V3 PRD modernizes that foundation with:
- Persistent import review workflow (V1 was immediate import)
- Duplicate detection with categorization (New/ExactDuplicate/PotentialDuplicate)
- GUID Keys throughout (no database IDs)
- Improved error handling and user feedback

**Related Features**:
- [Tenant Data Administration](./PRD-TENANT-DATA-ADMIN.md) - Handles XLSX import/export for all entity types
- [Transaction Record](../transactions/PRD-TRANSACTION-RECORD.md) - Defines transaction schema and validation rules
- [Transaction Splits](../transactions/PRD-TRANSACTION-SPLITS.md) - Bank formats don't include split data; splits must be added after import

---

## Handoff Checklist Review for PRD-BANK-IMPORT

Reviewed [`docs/wip/import-export/PRD-BANK-IMPORT.md`](docs/wip/import-export/PRD-BANK-IMPORT.md) against the handoff checklist from [`docs/wip/PRD-TEMPLATE.md`](docs/wip/PRD-TEMPLATE.md).

### ✅ All Checklist Items Pass

**✅ All user stories have clear acceptance criteria**
- Story 1 (Upload Bank File): 8 testable criteria
- Story 2 (Review Imported Transactions): 10 detailed criteria
- Story 3 (Manage Import Review State): 9 criteria
- Story 4 (Handle Import Errors): 7 criteria

**✅ Open questions resolved or documented as design decisions**
- 3 questions resolved with clear YES/NO decisions and rationale
- 2 remaining questions appropriately deferred to implementation/testing

**✅ Technical approach indicates affected layers**
- All layers clearly identified: Frontend, API, Application, Database
- Duplicate detection strategy specified (FITID or hash-based)
- File format support detailed (OFX 2.x, QFX/OFX 1.x)

**✅ Existing code patterns and files referenced**
- YoFi V1 implementation referenced with GitHub link
- Three related PRDs linked: Tenant Data Admin, Transaction Record, Transaction Splits
- Clear notes on V3 improvements over V1

### Overall Assessment

**READY FOR HANDOFF** - The PRD successfully meets all handoff criteria and provides sufficient detail for implementation. The document balances specification completeness with implementation flexibility, making it suitable for AI-assisted development.
