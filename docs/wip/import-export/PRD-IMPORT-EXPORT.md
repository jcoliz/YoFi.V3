## Complete PRD: Multi-Entity Import/Export

# Product Requirements Document: Multi-Entity Import/Export

**Status**: Draft
**Created**: 2025-12-21
**Owner**: Roo (AI Assistant)
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

## User Stories

### Story 1: User - Import Bank Transactions
**As a** YoFi user
**I want** to upload a QFX or OFX file downloaded from my bank
**So that** I can quickly import my transaction history instead of entering it manually

**Acceptance Criteria**:
- [ ] User can navigate to Import page
- [ ] User can select and upload file (.xlsx, .ofx, .qfx extensions)
- [ ] System validates file format and provides clear error messages
- [ ] System parses file and extracts transaction data (date, payee, amount, memo (optional), source)
- [ ] System creates transactions in user's tenant with proper attribution
- [ ] User sees success message with count of imported transactions
- [ ] Imported transactions in an "import review" page, isolated from
- [ ] User can choose which transactions are approved for import, and which are held back.
- [ ] Duplicate transactions are shown, but deselected by default.
- [ ] Potential duplicate transactions are highlighted, and deselected by default. These are transactions with a duplicate key, *but* a difference in amount, date, or payee.
- [ ] Imported transactions are not considered in any reporting or exports until they are accepted into the primary transactions
- [ ] Imported transactions from XLSX can also include splits to import, in a separate sheet, with Transaction Key mappings back to their owning transaction.

### Story 2: User - Export Transactions to XLSX
**As a** YoFi user
**I want** to export my transactions to XLSX format
**So that** I can analyze them in Excel, import into accounting software, or create backups

**Acceptance Criteria**:
- [ ] User can navigate to Transactions Export page
- [ ] User clicks Export button to download XLSX file
- [ ] System generates file containing all user's transactions in current tenant
- [ ] File includes all fields (Key, Date, Payee, Amount, Category, Memo, Source)
- [ ] File includes all splits, with a descriptor of what transaction (Key, Amount Category, Memo, Transaction Key), so that they can be later re-imported
- [ ] File downloads with descriptive filename (e.g., `transactions-2025-12-21.csv`)

### Story 3: User - Import Budget Transactions
**As a** YoFi user
**I want** to import budget transaction data from XLSX
**So that** I can set up my annual budget from a spreadsheet template

**Acceptance Criteria**:
- [ ] User can navigate to Budget Transactions Import page
- [ ] User can upload XLSX file with budget data (e.g. category, date, frequency, amount)
- [ ] System validates data structure and data types
- [ ] System creates budget transactions in user's tenant
- [ ] User sees summary of imported budget items
- [ ] Budget transactions appear in budget view
- [ ] Duplicate entries (by Key) are updated with imported data

### Story 4: User - Export Budget Transactions
**As a** YoFi user
**I want** to export my budget transactions to XLSX
**So that** I can analyze budget vs. actual in a spreadsheet

**Acceptance Criteria**:
- [ ] User can navigate to Budget Transactions Export page
- [ ] User can download budget data as XLSX
- [ ] XLSX includes all budget fields e.g. category, date, frequency, amount, timestamp)
- [ ] File downloads with descriptive filename (e.g., `budget-2025-12-21.csv`)

### Story 5: User - Export Payees List
**As a** YoFi user
**I want** to export my payees list to XLSX
**So that** I can review, edit, and re-import payee information

**Acceptance Criteria**:
- [ ] User can navigate to Payees Export page
- [ ] User can download payees as XLSX
- [ ] XLSX includes payee fields (e.g. name, category, auto-categorization rules)
- [ ] File downloads with descriptive filename (e.g., `payees-2025-12-21.csv`)
- [ ] Duplicate entries (by Key) are updated with imported data

### Story 6: User - Import Payees List
**As a** YoFi user
**I want** to import a payees list from XLSX
**So that** I can set up automatic categorization rules in bulk

**Acceptance Criteria**:
- [ ] User can navigate to Payees Import page
- [ ] User can upload XLSX file with payee data
- [ ] System validates payee data and creates/updates payees
- [ ] User sees summary of imported payees
- [ ] Payees appear in payees list with proper categorization rules

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

---

## Technical Approach

**Layers Affected**:
- [x] Frontend (Vue/Nuxt) - Import/Export UI pages for each entity
- [x] Controllers (API endpoints) - File upload/download endpoints, and support for UI
- [x] Application (Features/Business logic) - Import/Export features with parsers
- [ ] Entities (Domain models) - Import/export shouldn't affect model design, it uses what's there
- [ ] Database (Schema changes) - None required

**Key Components**:

**Frontend Pages**:
- `src/FrontEnd.Nuxt/app/pages/transactions/import.vue` - Transaction import UI
- `src/FrontEnd.Nuxt/app/pages/transactions/export.vue` - Transaction export UI
- `src/FrontEnd.Nuxt/app/pages/budget/import.vue` - Budget import UI
- `src/FrontEnd.Nuxt/app/pages/budget/export.vue` - Budget export UI
- `src/FrontEnd.Nuxt/app/pages/payees/import.vue` - Payees import UI
- `src/FrontEnd.Nuxt/app/pages/payees/export.vue` - Payees export UI

**API Controllers**:
- `src/Controllers/TransactionsController.cs` - Add Import/Export endpoints
- `src/Controllers/BudgetController.cs` - Add Import/Export endpoints (new controller)
- `src/Controllers/PayeesController.cs` - Add Import/Export endpoints (new controller)

**Application Features**:
- `src/Application/Features/Transactions/ImportTransactionsFeature.cs` (new)
- `src/Application/Features/Transactions/ExportTransactionsFeature.cs` (new)
- `src/Application/Features/Budget/ImportBudgetFeature.cs` (new)
- `src/Application/Features/Budget/ExportBudgetFeature.cs` (new)
- `src/Application/Features/Payees/ImportPayeesFeature.cs` (new)
- `src/Application/Features/Payees/ExportPayeesFeature.cs` (new)

**Parsers (Shared)**:
- `src/Application/Parsers/CsvParser.cs` - Generic CSV parser (new)
- `src/Application/Parsers/OfxParser.cs` - OFX format parser (transactions only)
- `src/Application/Parsers/QfxParser.cs` - QFX format parser (transactions only)

**Writers (Shared)**:
- `src/Application/Writers/CsvWriter.cs` - Generic CSV writer (new)
- `src/Application/Writers/OfxWriter.cs` - OFX format writer (transactions only)
- `src/Application/Writers/QfxWriter.cs` - QFX format writer (transactions only)

**API Endpoints**:
```
# Transactions
POST /api/tenant/{tenantId}/transactions/import
GET  /api/tenant/{tenantId}/transactions/export?format={csv|ofx|qfx}

# Budget Transactions
POST /api/tenant/{tenantId}/budget/import
GET  /api/tenant/{tenantId}/budget/export

# Payees
POST /api/tenant/{tenantId}/payees/import
GET  /api/tenant/{tenantId}/payees/export
```

**Format Support Matrix**:
| Entity | XLSX | OFX | QFX |
|--------|-----|-----|-----|
| Transactions | ✅ Import/Export | ✅ Import/Export | ✅ Import/Export |
| Budget Transactions | ✅ Import/Export | ❌ | ❌ |
| Payees | ✅ Import/Export | ❌ | ❌ |

---

## Open Questions

- [X] Should we support JSON format for API-based integrations? NO, consider in future
- [X] How should we handle duplicate transactions on import? (Detect by date+amount+payee, skip, or allow duplicates?) See above for bank import handling. For XLSX import for all data types, matching key is an edit, non matching key is an add/
- [X] Should exports support filters (date range, category, amount range)? YES, date range only
- [X] What file size limits? (Suggest: 10MB for XLSX, 5MB for OFX/QFX) NO Later, based on real world testing
- [X] Should large imports be async with progress tracking? NO, later if needed
- [X] Should we preserve original Keys from imported files (for re-import)? Yes, for all data types, if an imported line matches an existing key, that's an edit operation.
- [X] How to handle timezone differences in imported dates? Imported dates are assumed to be local time, unless a timezone is in the timestamp
- [X] Should payee import create new payees or update existing ones? Creates new one if no Key. Updates existing ones if Key exists. If Key doesn't match, treat it as new
- [X] Should budget import be additive or replace existing budget? Same as payee. Budget transactions are additive if no key or key not found. For a found key, updates.
- [X] Do we need import templates/examples for users to download? NO, this will be handled as part of Sample Data feature

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
- Transaction, Budget, Payee CRUD operations must be complete
- Authentication/authorization must be working

**Constraints**:
- Must maintain tenant data isolation (users cannot import into other tenants)
- Import/export should complete within 30 seconds for typical file sizes
- Must handle common CSV encoding issues (UTF-8, UTF-16, ISO-8859-1)
- OFX/QFX parsing should be lenient to handle bank variations
- File uploads must be validated for security (file type, size, content)

---

## Notes & Context

**YoFi V1 Implementation Reference**:
The YoFi V1 application has comprehensive import/export across multiple entities:

**Controllers with Import/Export**:
- [`TransactionsController.cs`](https://github.com/jcoliz/yofi/blob/master/YoFi.AspNet/Controllers/TransactionsController.cs) - Full CSV/OFX/QFX support
- [`BudgetTxController.cs`](https://github.com/jcoliz/yofi/blob/master/YoFi.AspNet/Controllers/BudgetTxController.cs) - CSV import/export for budgets
- [`PayeesController.cs`](https://github.com/jcoliz/yofi/blob/master/YoFi.AspNet/Controllers/PayeesController.cs) - CSV export for payees

**Implementation Details**:
- Importers: `YoFi.Core/Importers/` (CsvImporter, OfxImporter, QfxImporter)
- Writers: `YoFi.Core/Writers/` (CsvWriter, OfxWriter, QfxWriter)
- Repository pattern with ImportAsync/AsExportAsync methods
- Views: `YoFi.AspNet/Views/{Entity}/Import.cshtml` and `Export.cshtml`

**CSV Field Mappings**:

*Transactions*:
- Date → Transaction.Date
- Payee → Transaction.Payee
- Amount → Transaction.Amount
- Category → Transaction.Category
- Memo → Transaction.Memo

*Budget Transactions*:
- Category → BudgetTx.Category
- Month → BudgetTx.Month
- Amount → BudgetTx.Amount
- Timestamp → BudgetTx.Timestamp

*Payees*:
- Name → Payee.Name
- Category → Payee.Category
- (Auto-categorization rules TBD)

---

## Handoff Checklist (for AI implementation)

When handing this off for detailed design/implementation:
- [ ] All user stories have clear acceptance criteria
- [ ] Open questions resolved (duplicate detection, filters, async processing)
- [ ] CSV column mapping strategy defined for each entity
- [ ] Error handling strategy defined (fail fast vs partial import)
- [ ] File size limits and security considerations documented
- [ ] Test data samples prepared for all formats (CSV, OFX, QFX)
- [ ] Decide if parsers/writers should be shared or entity-specific
- [ ] Define validation rules for each entity type
- [ ] UI/UX patterns documented (consistent across entities)
- [ ] Import preview/confirmation workflow defined

---

## Recommended Implementation Phases

**Phase 1 - Transactions CSV Import/Export (MVP)**:
- Transaction CSV parser and writer
- Simple upload/download UI
- Synchronous processing
- Basic validation

**Phase 2 - Transactions OFX/QFX Support**:
- OFX 2.x (XML) parser and writer
- QFX (OFX 1.x) parser and writer
- Enhanced error handling

**Phase 3 - Budget Transactions**:
- Budget CSV import/export
- Budget-specific validation
- UI integrated with budget views

**Phase 4 - Payees**:
- Payee CSV import/export
- Payee validation and deduplication
- Auto-categorization rule import

**Phase 5 - Advanced Features**:
- Export filters (date range, category)
- Import preview with confirmation
- Duplicate detection
- Async processing with progress tracking

This PRD should be saved to [`docs/wip/import-export/PRD-IMPORT-EXPORT.md`](docs/wip/import-export/PRD-IMPORT-EXPORT.md) and added to the PRD index.
