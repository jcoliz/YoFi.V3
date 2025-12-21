# Product Requirements Document: Multi-Entity Import/Export

**Status**: Draft
**Created**: 2025-12-21
**Owner**: James Coliz
**Target Release**: V3.1
**ADO**: TBD

---

## Problem Statement

Users need to transfer data between YoFi and external systems (banks, budgeting tools, spreadsheets, accounting software). Without comprehensive import/export capabilities across all major data types, users must manually enter data, cannot migrate from other systems efficiently, and lack backup/restore options for their complete dataset.

---

## Goals & Non-Goals

### Goals
- [ ] Enable import/export of **Transactions** from bank formats (OFX and QFX)
- [ ] Enable import/export of **Transactions** with **Splits** from/to XLSX format
- [ ] Enable import/export of **Budget Transactions** from/to XLSX format
- [ ] Enable import/export of **Payees** from/to XLSX format
- [ ] Enable **complete data export** in single file for GDPR compliance and backup
- [ ] Enable **complete data import** in single file for V1 migration and restore
- [ ] Provide consistent UI/UX patterns across all import/export operations
- [ ] Provide clear validation feedback when import files are malformed
- [ ] Maintain data integrity and tenant isolation during all operations
- [ ] Support bulk operations efficiently (hundreds to thousands of records)
- [ ] Protect against duplicate transaction imports from bank formats
- [ ] Protect against duplicate imports of all entities from XLSX by using entity Key

### Non-Goals
- Real-time bank integration via APIs (Plaid, etc.) - separate feature
- Import/export of Receipts (attachments require different handling)
- Import/export of Splits independently (included with transactions, not standalone)
- Scheduled/automated exports
- Cross-tenant import/export (security concern)

---

## Duplicate Detection and Key Handling Principles

**All entities use GUID Keys, never database IDs.** Import/export operations work exclusively with business keys (GUIDs), not auto-increment database IDs.

### **XLSX Import/Export (All Entities)**

When importing from XLSX format (Transactions, Budget Transactions, Payees):
- **Key field is always included** in exports
- **If imported record has a Key that matches existing record**: Update operation (edit existing)
- **If imported record has no Key or Key doesn't match**: Insert operation (create new)
- **Records are imported directly** - no review workflow needed
- **This enables the "export → edit in Excel → re-import" workflow**

### **Bank Format Import (OFX/QFX for Transactions Only)**

Bank file formats may or may not include unique transaction identifiers:
- **If bank provides unique transaction ID**: Use as Key for duplicate detection
- **If no unique ID provided**: Generate hash from (Date + Amount + Payee) as duplicate detection key
- **Duplicate detection happens in import review workflow**, not during initial parse
- **Import review workflow is ONLY for bank format imports** (not XLSX)

### **Import Review Workflow (Bank Imports Only)**

Bank format imports (OFX/QFX) go through a review stage before becoming permanent:
- **Imported transactions are stored in temporary "import review" state**
- **User can return to review state indefinitely** - no time pressure
- **Separate API capability** provides import candidates for review
- **Three categories of import candidates**:
  1. **New transactions** - No conflict, ready to import (selected by default)
  2. **Exact duplicates** - Same Key/hash, same data (deselected by default)
  3. **Potential duplicates** - Same Key/hash, different data (highlighted for user review, deselected by default)
- **User explicitly approves** which transactions to accept into permanent storage
- **Import review transactions not included** in reports, exports, or analytics until accepted
- **XLSX imports do NOT use this workflow** - they import directly with Key-based create/update logic

### **Export Filtering**

Exports support **date range filtering only**:
- User can specify start date and/or end date
- Category filtering, amount filtering NOT supported in initial release
- All records within date range are exported (no sampling or limits)

### **Timezone Handling**

- **Imported dates assumed to be local time** unless timestamp includes timezone information
- **Exported dates use ISO 8601 format** with timezone when available
- **Bank formats (OFX/QFX)** follow their standard specifications for date/time representation

---

## User Stories

### Story 1: User - Import Bank Transactions with Review (OFX/QFX Only)
**As a** YoFi user
**I want** to upload a QFX or OFX file downloaded from my bank and review it before accepting
**So that** I can avoid duplicates and verify transactions before they enter my records

**Acceptance Criteria**:
- [ ] User can navigate to Transactions Import page
- [ ] User can select and upload bank format file (.ofx or .qfx extensions)
- [ ] System validates file format and provides clear error messages for invalid files
- [ ] System parses bank file and extracts transaction data (date, payee, amount, memo, source)
- [ ] System stores transactions in temporary "import review" state (separate from primary transactions)
- [ ] User is redirected to Import Review page showing all imported transaction candidates
- [ ] Import Review page displays three categories:
  - New transactions (no conflicts, selected by default)
  - Exact duplicates (same Key/hash, same data, deselected by default)
  - Potential duplicates (same Key/hash, different data, highlighted and deselected by default)
- [ ] User can select/deselect individual transactions for import
- [ ] User can click "Accept Selected" to move approved transactions into primary transactions
- [ ] User can leave Import Review page and return later (review state persists)
- [ ] Transactions in import review state are NOT included in reports, exports, or transaction lists
- [ ] System uses bank-provided transaction ID as Key, or generates hash from (Date + Amount + Payee) if no ID

### Story 1b: User - Import Transactions from XLSX (Direct Import)
**As a** YoFi user
**I want** to upload a XLSX file with transactions and splits
**So that** I can bulk create or update transactions with split support

**Acceptance Criteria**:
- [ ] User can navigate to Transactions Import page
- [ ] User can select and upload XLSX file with two worksheets: Transactions and Splits
- [ ] System validates XLSX structure and data types
- [ ] For transactions with matching Key: System updates existing transactions
- [ ] For transactions without Key or non-matching Key: System creates new transactions
- [ ] For splits with matching Key: System updates existing splits
- [ ] For splits without Key: System creates new splits linked to parent transaction via TransactionKey
- [ ] User sees summary: X transactions created, Y updated, Z splits created, W splits updated
- [ ] Imported/updated transactions appear immediately in transaction list
- [ ] NO import review workflow - changes are applied directly

### Story 2: User - Export Transactions to XLSX with Splits
**As a** YoFi user
**I want** to export my transactions to XLSX format including splits
**So that** I can analyze, edit, and re-import complete transaction data with splits

**Acceptance Criteria**:
- [ ] User can navigate to Transactions Export page
- [ ] User can optionally specify date range filter (start date, end date)
- [ ] User clicks Export button to download XLSX file
- [ ] System generates XLSX file with two worksheets: "Transactions" and "Splits"
- [ ] Transactions worksheet includes all fields (Key, Date, Payee, Amount, Category, Memo, Source)
- [ ] Splits worksheet includes (Key, Amount, Category, Memo, TransactionKey) for mapping back to parent transaction
- [ ] File downloads with descriptive filename (e.g., `transactions-2025-12-21.xlsx`)
- [ ] Exported data can be edited and re-imported (Keys preserved for update operations)

### Story 3: User - Import Budget Transactions with Key-Based Updates
**As a** YoFi user
**I want** to import budget transaction data from XLSX
**So that** I can set up or update my annual budget from a spreadsheet

**Acceptance Criteria**:
- [ ] User can navigate to Budget Transactions Import page
- [ ] User can upload XLSX file with budget data (Key, category, date, frequency, amount)
- [ ] System validates XLSX structure and data types
- [ ] For records with matching Key: System updates existing budget transactions
- [ ] For records without Key or non-matching Key: System creates new budget transactions
- [ ] User sees summary: X records created, Y records updated, Z records failed
- [ ] Budget transactions appear immediately in budget view
- [ ] System provides error details for any failed records

### Story 4: User - Export Budget Transactions for Editing
**As a** YoFi user
**I want** to export my budget transactions to XLSX with Keys
**So that** I can edit in Excel and re-import to update existing records

**Acceptance Criteria**:
- [ ] User can navigate to Budget Transactions Export page
- [ ] User can optionally specify date range filter
- [ ] User clicks Export to download XLSX file
- [ ] XLSX includes all budget fields (Key, category, date, frequency, amount, timestamp)
- [ ] File downloads with descriptive filename (e.g., `budget-2025-12-21.xlsx`)
- [ ] Keys are preserved so edited records update existing data on re-import

### Story 5: User - Export Payees List for Bulk Editing
**As a** YoFi user
**I want** to export my payees list to XLSX with Keys
**So that** I can bulk edit payee information and categorization rules

**Acceptance Criteria**:
- [ ] User can navigate to Payees Export page
- [ ] User clicks Export to download XLSX file
- [ ] XLSX includes payee fields (Key, name, category, auto-categorization rules)
- [ ] File downloads with descriptive filename (e.g., `payees-2025-12-21.xlsx`)
- [ ] Keys are preserved so edited records update existing payees on re-import

### Story 6: User - Import Payees with Key-Based Updates
**As a** YoFi user
**I want** to import a payees list from XLSX
**So that** I can bulk create or update payees and auto-categorization rules

**Acceptance Criteria**:
- [ ] User can navigate to Payees Import page
- [ ] User can upload XLSX file with payee data (Key, name, category, rules)
- [ ] System validates payee data
- [ ] For records with matching Key: System updates existing payees
- [ ] For records without Key or non-matching Key: System creates new payees
- [ ] User sees summary: X payees created, Y payees updated, Z records failed
- [ ] Payees appear immediately in payees list with updated categorization rules

### Story 7: User - Handle Import Errors Gracefully
**As a** YoFi user
**I want** clear feedback when my import file has errors
**So that** I can correct the issues and successfully import my data

**Acceptance Criteria**:
- [ ] System displays specific error messages (e.g., "Row 5: Invalid date format")
- [ ] System continues processing valid rows when possible
- [ ] User sees summary: X records imported, Y records failed
- [ ] User can download error report with failed rows and reasons
- [ ] System does not create partial/incomplete records

### Story 8: User - Export Complete Data Archive (GDPR Compliance)
**As a** YoFi user
**I want** to export ALL my data in one comprehensive download
**So that** I can comply with data portability requirements (GDPR) or create a complete backup

**Acceptance Criteria**:
- [ ] User can navigate to Settings or Account page
- [ ] User can click "Export All Data" or "Download My Data"
- [ ] System generates single XLSX file with multiple worksheets:
  - Transactions (with all fields and Keys)
  - Transaction Splits (with parent transaction Keys)
  - Budget Transactions (with all fields and Keys)
  - Payees (with all fields, categorization rules, and Keys)
  - Account settings/preferences (if applicable)
- [ ] Export includes ALL data for current tenant (no filtering)
- [ ] File downloads with descriptive filename (e.g., `yofi-complete-export-2025-12-21.xlsx`)
- [ ] User sees confirmation message with record counts for each entity type
- [ ] Export includes all Keys for re-importability

### Story 9: User - Import Complete Data Archive (V1 Migration)
**As a** YoFi V1 user migrating to V3
**I want** to import a complete data export from V1 in one operation
**So that** I can migrate all my data without manually importing each entity type separately

**Acceptance Criteria**:
- [ ] User can navigate to Settings or Account page
- [ ] User can click "Import All Data" or "Restore from Backup"
- [ ] User can upload multi-worksheet XLSX file containing all entity types
- [ ] System validates file structure contains required worksheets
- [ ] System processes all worksheets in correct order:
  1. Payees first (referenced by transactions)
  2. Transactions second
  3. Transaction Splits third (reference parent transactions)
  4. Budget Transactions last
- [ ] For each entity: Key-based create/update logic applies
- [ ] User sees comprehensive summary showing results for each entity type:
  - Transactions: X created, Y updated, Z failed
  - Splits: X created, Y updated, Z failed
  - Budget: X created, Y updated, Z failed
  - Payees: X created, Y updated, Z failed
- [ ] User can download error report if any records failed
- [ ] System validates referential integrity (splits reference valid transaction Keys)
- [ ] Import is transactional per entity type (all or nothing per worksheet)
- [ ] User can navigate to each entity's list to verify imported data

---

## Technical Approach

**Layers Affected**:
- Frontend - UI for file upload, import review, format selection, and download
- API - Endpoints for file upload, import review state management, and file generation
- Application - Business logic for parsing, validation, duplicate detection, and file generation
- Database - Temporary storage for import review state

**Required Capabilities**:

1. **File Format Support**:
   - Parse XLSX files (multi-worksheet support for transactions + splits)
   - Parse OFX 2.x format (XML-based financial data)
   - Parse QFX/OFX 1.x format (SGML-like financial data)
   - Generate XLSX files with proper formatting and multiple worksheets

2. **Import Review Workflow (Bank Imports Only)**:
   - Temporary storage for imported transactions from OFX/QFX in "review" state
   - Categorization of transactions: new, exact duplicates, potential duplicates
   - User selection interface for accepting/rejecting individual transactions
   - Persistent review state (users can return later)
   - Move accepted transactions from review to primary storage
   - XLSX imports do NOT use this workflow - they import directly

3. **Duplicate Detection**:
   - For XLSX: Match by GUID Key field
   - For bank formats: Use bank transaction ID or generate hash from (Date + Amount + Payee)
   - Highlight exact matches (same Key, same data)
   - Highlight potential conflicts (same Key, different data)

4. **Key-Based Create/Update Logic**:
   - If imported record has Key matching existing: Update existing record
   - If imported record has no Key or non-matching Key: Create new record
   - Supports "export → edit → re-import" workflow for bulk updates

5. **Export Filtering**:
   - Date range filtering (start date and/or end date)
   - Apply to individual entity exports

6. **Complete Data Export/Import**:
   - Generate single XLSX file with multiple worksheets containing ALL user data
   - Support for complete data portability (GDPR compliance)
   - Import processing in correct order to maintain referential integrity
   - Validation of cross-worksheet references (e.g., splits reference valid transactions)
   - Comprehensive error reporting across all entity types
   - Suitable for V1 to V3 migration and backup/restore scenarios

**Format Support Matrix**:
| Entity | XLSX | OFX | QFX |
|--------|------|-----|-----|
| Transactions | ✅ Import/Export | ✅ Import only | ✅ Import only |
| Transaction Splits | ✅ Import/Export (with transactions) | ❌ | ❌ |
| Budget Transactions | ✅ Import/Export | ❌ | ❌ |
| Payees | ✅ Import/Export | ❌ | ❌ |

**Note**: OFX and QFX are bank-specific formats for importing into YoFi (one-way). XLSX is the universal format for all import/export operations including the edit-and-re-import workflow.

---

## Open Questions

- [ ] What is the exact field name for unique transaction ID in OFX and QFX formats? (Need to identify during parser implementation)
- [X] Should import review batches expire after a certain time period, or persist indefinitely? NO, they persist indefinitely. In YoFi V1, we added an "Imported" flag to the entity.
- [ ] What file size limits should we enforce? (Defer until real-world testing provides data)
- [X] Should we support Excel formulas in XLSX imports, or require values only? NO values only
- [X] How should we handle splits when their parent transaction Key doesn't exist during import? This is a warning that should be surfaced to the user.
- [X] Should category values be validated against existing categories, or create new ones automatically? NO There is no concept of "creating categories". Categories are 100% ad hoc.
- [X] What level of logging/auditing is needed for import operations? (Track who imported what, when) Standard controller logging matching existing patterns

---

## Success Metrics

- **Adoption**: 60%+ of active users utilize import within first 30 days
- **Volume**: Users import average of 100+ transactions per session
- **Error Rate**: <5% of import attempts fail with validation errors
- **Export Usage**: 30%+ of users export data at least once per month
- **Format Distribution**: Track which formats are most used (informs priorities)
- **Multi-entity Usage**: Track how many users import multiple entity types

---

## Dependencies & Constraints

**Dependencies**:
- Multi-tenancy framework must be fully functional (tenant isolation)
- Transaction, Budget, Payee CRUD operations must be complete before implementing the corresponding import/export stories.
- Authentication/authorization must be working
- Database schema must support import review state (temporary storage for import batches)

**Constraints**:
- Must maintain tenant data isolation (users cannot import into other tenants)
- Import/export operations should complete within reasonable time for typical file sizes
- Must handle common file encoding variations
- OFX/QFX parsing should be lenient to handle variations across different banks
- File uploads must be validated for security (file type, size, content scanning)
- Import review state must be isolated per user/tenant (cannot see other users' pending imports)
- Only GUID Keys are used in import/export - no database auto-increment IDs exposed
- Import review batches must be queryable and manageable (users can have multiple pending imports), but they don't need to be independent. If I import three files, that's just one set of pending imports.

---

## Notes & Context

**YoFi V1 Reference**:
The original YoFi V1 application (https://github.com/jcoliz/yofi) implemented comprehensive import/export functionality across multiple entities (Transactions, Budget Transactions, Payees) with support for CSV, OFX, and QFX formats. This V3 PRD builds on that proven foundation while modernizing the approach with:
- XLSX instead of CSV for better split transaction support
- Import review workflow for duplicate management
- GUID Keys throughout (no database IDs)
- Consistent Key-based update pattern across all entities


---

## Handoff Checklist

Before proceeding to detailed design and implementation:
- [x] All user stories have clear acceptance criteria
- [x] Duplicate detection strategy defined
- [x] Import review workflow documented
- [x] Key-based create/update logic specified
- [x] Export filtering requirements specified
- [x] Complete data export/import requirements specified (GDPR, V1 migration)
- [ ] Validation strategy defined (what to validate, how to report errors)
- [ ] Test data samples identified for all formats
- [ ] UI/UX requirements for import review documented
- [ ] Error handling and user feedback requirements defined
- [ ] Security requirements for file upload specified
- [ ] Import batch lifecycle policy decided (expiration, cleanup)

---

## Recommended Implementation Phases

**Phase 1 - Bank Format Import (OFX/QFX) - MVP**:
- OFX 2.x (XML) parser for transactions
- QFX (OFX 1.x) parser for transactions
- Extract or generate transaction Keys
- Duplicate detection via hash (Date+Amount+Payee) if no bank ID
- Import review workflow with categorization (new/exact duplicate/potential duplicate)
- Import review UI with selection interface
- Accept selected transactions functionality
- Basic validation and error reporting

**Phase 2 - Transaction XLSX Import/Export**:
- Transaction XLSX parser and writer (single worksheet)
- Direct import with Key-based create/update logic
- Date range filtering on export
- Upload UI and download functionality
- No import review (direct import)

**Phase 3 - Transaction Splits Support**:
- Extend XLSX to two worksheets (Transactions, Splits)
- Parser handles TransactionKey foreign key relationships
- Export includes splits linked to transactions
- Import validates split references

**Phase 4 - Budget Transactions**:
- Budget XLSX import/export
- Key-based create/update logic
- Budget-specific validation
- UI integrated with budget views

**Phase 5 - Payees**:
- Payee XLSX import/export
- Key-based create/update logic
- Auto-categorization rule import
- Payee validation

**Phase 6 - Complete Data Export/Import**:
- Multi-worksheet XLSX generation with all entity types
- "Export All Data" functionality for GDPR compliance
- "Import All Data" functionality for V1 migration
- Correct import ordering to maintain referential integrity
- Cross-worksheet validation (splits reference valid transactions)
- Comprehensive multi-entity error reporting
- Backup/restore capabilities

**Future Considerations (Not in Initial Release)**:
- Export filters beyond date range (category, amount)
- Import templates/examples (handled by Sample Data feature)
- JSON format for API integrations
- Async processing with progress tracking for large files
- File size limits (determine from real-world usage)
- Scheduled/automated exports
