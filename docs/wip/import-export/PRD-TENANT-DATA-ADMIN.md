# Product Requirements Document: Tenant Data Administration

**Status**: Draft
**Created**: 2025-12-21
**Owner**: James Coliz
**Target Release**: V3.1
**ADO**: TBD

---

## Problem Statement

Users need comprehensive capabilities to manage their complete tenant dataset throughout its lifecycle - from initial setup and migration, through ongoing bulk updates, to eventual deletion. Without these capabilities, users face several problems:

1. **Migration barrier** - V1 users cannot easily migrate their complete dataset to V3
2. **Bulk editing limitation** - No way to make bulk changes across hundreds of records efficiently
3. **GDPR compliance gap** - Cannot export complete data or delete all data (right to portability and erasure)
4. **Evaluation friction** - New users cannot quickly populate sample data to evaluate the application

Users need a unified data administration feature supporting the complete lifecycle: import, export, bulk update, sample data loading, and complete deletion.

---

## Goals & Non-Goals

### Goals
- [ ] Provide **single unified Data Administration page** for all import/export operations
- [ ] Enable **flexible export selection** - any combination of entity types with year/date filtering
- [ ] Enable **automatic import detection** - system detects entity types in uploaded XLSX
- [ ] Support **Key-based create/update logic** for all imports (no review workflow)
- [ ] Enable **complete data deletion** for GDPR "right to be forgotten" compliance
- [ ] Provide **sample data loading** for new users evaluating the application
- [ ] Support **"export → edit in Excel → re-import" workflow** for bulk updates
- [ ] Be **data-agnostic** - work with any tenant entity types, not hardcoded lists
- [ ] Provide clear validation feedback when import files are malformed
- [ ] Maintain data integrity and referential integrity during all operations
- [ ] Support bulk operations efficiently (hundreds to thousands of records)
- [ ] Maintain tenant isolation during all operations

### Non-Goals
- Real-time sync with external systems (separate feature)
- Import/export of attachments/files (require different handling)
- Scheduled/automated exports
- Cross-tenant import/export (security concern)
- Import from bank formats (OFX/QFX) - covered by Bank Import feature
- Import review workflow - XLSX imports apply directly
- Individual entity-specific pages - all operations on single unified page
- Hardcoded entity types - system should discover available entities dynamically

---

## Key Concepts

### Single Unified Page Approach

All data administration operations happen on one page:
- **No separate pages per entity type** - User doesn't navigate to "Transactions Export" or "Payees Import"
- **Flexible selection model** - User picks what they want (any combination)
- **Intelligent defaults** - Typical use case is to export only transactions for current year, so that's the default.
- **Consistent UX** - Same interface patterns for all entity types

### Data-Agnostic Design

The system works generically with any tenant entity types:
- **Dynamic entity discovery** - System queries available entity types at runtime
- **No hardcoded field lists** - Export includes all fields from entity schema
- **Generic XLSX mapping** - Column headers match entity property names
- **Special handling only where needed** - Splits→Transactions relationship is special case

### Key-Based Create/Update Logic

All XLSX imports use GUID Key matching to determine create vs. update:
- **Key field is always included** in exports
- **If imported record has a Key that matches existing record**: Update operation (edit existing)
- **If imported record has no Key or Key doesn't match**: Insert operation (create new with new GUID)
- **Records are imported directly** - no review workflow needed
- **This enables the "export → edit in Excel → re-import" workflow**

### XLSX Format Standard

All imports and exports use XLSX format exclusively:
- **Multi-worksheet support** - Single file can contain multiple entity types
- **One worksheet per entity type** - Worksheet name matches entity type name
- **Schema validation** - Column headers define expected fields
- **Cell values only** - Formulas are NOT evaluated during import
- **UTF-8 encoding** - Consistent character encoding across all files

### Special Case: Transactions and Splits

Transactions have a special relationship with Splits (one-to-many):
- **Splits always exported with Transactions** - User doesn't select them separately
- **Two worksheets in XLSX** - "Transactions" and "Splits" worksheets
- **TransactionKey foreign key** - Splits reference parent transaction via Key
- **Referential integrity validation** - Warn if splits reference non-existent transactions

---

## User Stories

### Story 1: User - Export Data with Flexible Selection

**As a** YoFi user
**I want** to export any combination of my entity types with flexible filtering
**So that** I can get exactly the data I need without downloading everything

**Acceptance Criteria**:
- [ ] User can navigate to Data Administration page (Settings or Workspace menu)
- [ ] Export section displays checkboxes for each available entity type
- [ ] System dynamically discovers entity types (not hardcoded list)
- [ ] Special handling: "Transactions" checkbox automatically includes Splits
- [ ] User can select any combination of entity types (one, some, or all)
- [ ] User can specify date range filter:
  - Year selector dropdown (showing years with data, plus "All Years")
  - Optional: Custom date range with Start Date and End Date
  - Date filter applies only to date-based entities (transactions, budget transactions, etc.)
  - Non-date-based entities (payees, categories) ignore date filter
- [ ] "Select All" and "Clear All" buttons for entity type selection
- [ ] Export button displays count: "Export Data (N types selected)"
- [ ] Quick action: "Export All Data (GDPR / Backup)" button for one-click complete export:
  - Exports ALL entity types for current tenant with ALL date ranges (no filtering)
  - File downloads as `yofi-complete-export-{date}.xlsx`
  - Primary path for GDPR data portability compliance
- [ ] System generates single XLSX file with one worksheet per selected entity type
- [ ] Each worksheet includes ALL fields from entity schema (including Key)
- [ ] Worksheet names match entity type names
- [ ] Column headers match entity property names
- [ ] File downloads with descriptive filename indicating selection and date range
- [ ] User sees confirmation toast with record counts per entity type
- [ ] System logs export operation (who, when, what types, date range, record counts)

### Story 2: User - Import Data with Automatic Detection

**As a** YoFi user
**I want** to import an XLSX file and have the system automatically detect what entity types it contains
**So that** I don't have to specify what I'm importing

**Acceptance Criteria**:
- [ ] User can navigate to Data Administration page
- [ ] Import section has file upload area (drag-and-drop or browse button)
- [ ] User uploads XLSX file
- [ ] System automatically detects worksheets present and maps to entity types:
  - Worksheet name matches entity type name → Will import that entity
  - "Splits" worksheet is special case (requires "Transactions" worksheet)
- [ ] System displays preview of what will be imported with record counts per worksheet
- [ ] System validates XLSX structure (column headers match entity properties)
- [ ] If validation fails, shows specific error messages
- [ ] User reviews preview and clicks "Import Data" button
- [ ] System determines dependency order for import:
  - Entities without foreign keys first
  - Entities with dependencies after their referenced entities
  - Splits always imported after Transactions
- [ ] For each record: Key-based create/update logic applies
- [ ] User sees comprehensive summary showing results for each entity type: "EntityName: X created, Y updated, Z failed"
- [ ] Imported/updated records appear immediately in their respective list views
- [ ] NO import review workflow - changes are applied directly
- [ ] If any records fail, user can download error report (CSV format)
- [ ] System validates referential integrity (e.g., splits reference valid transaction Keys)
- [ ] If referential integrity fails, system shows warning but allows proceeding
- [ ] Import is transactional per worksheet (all or nothing per entity type)
- [ ] System logs import operation (who, when, what types, success/failure counts)

### Story 3: User - Delete All Tenant Data

**As a** YoFi user exercising GDPR rights
**I want** to permanently delete all my data from the system
**So that** I can exercise my "right to be forgotten" or reset my workspace

**Acceptance Criteria**:
- [ ] Data Administration page has "Delete All Data" button in danger zone section
- [ ] System displays strong warning with confirmation dialog:
  - "⚠️ WARNING: This will permanently delete ALL your data"
  - "This action cannot be undone"
  - "You may want to export your data first" (with quick export button)
- [ ] User must type confirmation phrase (e.g., "DELETE ALL DATA") to proceed
- [ ] System deletes all tenant data in correct dependency order (reverse of import order)
- [ ] System logs the deletion operation (who, when, record counts per entity)
- [ ] User sees confirmation with record counts per entity type deleted
- [ ] User is redirected to empty workspace or onboarding flow
- [ ] Tenant membership record is preserved (user remains member of tenant)

### Story 4: User - Load Sample Data

**As a** new YoFi user evaluating the application
**I want** to load pre-built sample data
**So that** I can explore features without manually entering data

**Acceptance Criteria**:
- [ ] Data Administration page (or "Getting Started" page) has "Load Sample Data" section
- [ ] User can choose from pre-built sample datasets (e.g., "Personal Finance", "Family Budget", "Small Business")
- [ ] System displays preview: "This will add approximately X records across Y entity types"
- [ ] User clicks "Load Sample Data"
- [ ] System imports sample data using same Key-based create/update logic
- [ ] Sample data does NOT overwrite existing data (only creates new records, no Keys in sample files)
- [ ] User sees confirmation with record counts per entity type
- [ ] User can immediately explore the application with realistic data
- [ ] User can later delete sample data using "Delete All Data" if desired

### Story 5: User - Handle Import Errors Gracefully

**As a** YoFi user
**I want** clear feedback when my import file has errors
**So that** I can correct the issues and successfully import my data

**Acceptance Criteria**:
- [ ] System displays specific error messages during validation (worksheet name, row number, field name, error reason)
- [ ] System continues processing valid rows when possible (partial success allowed)
- [ ] User sees summary: "X records imported successfully, Y records failed"
- [ ] User can download detailed error report as CSV (worksheet, row, field, error, original data)
- [ ] System does not create partial/incomplete records (validation happens before insert)
- [ ] XLSX imports only accept cell values (formulas are NOT evaluated)
- [ ] If foreign key references fail (e.g., splits reference non-existent transaction), system shows warnings but allows proceeding
- [ ] Standard controller logging is applied (who imported, when, entity types, success/failure counts)

---

## Dependencies & Constraints

**Dependencies**:
- Multi-tenancy framework must be fully functional (tenant isolation)
- All entity types must implement `ITenantModel` interface for discovery
- All entity types must have GUID Key property
- Authentication/authorization must be working

**Constraints**:
- Must maintain tenant data isolation (users cannot import into other tenants)
- Import/export operations should complete within reasonable time for typical file sizes (up to 10,000 records per entity)
- Must handle UTF-8 encoding consistently
- File uploads must be validated for security (file type, size, malware scanning)
- Only GUID Keys are used in import/export - no database auto-increment IDs exposed
- Sample data must be representative but not too large
- Complete data deletion is irreversible - strong warnings required
- XLSX cell formulas are NOT evaluated (values only)
- Single page must remain performant and not overwhelming with too many options
- Generic approach must support current and future entity types without code changes

---

## Open Questions

- [ ] What file size limits should we enforce? (Defer until real-world testing)
- [X] Should we provide import templates as downloadable XLSX files? YES, sounds helpful.
- [X] Should we version the XLSX schema (e.g., include version metadata in file)? NO, keep it simple
- [ ] How many sample data sets should we provide initially? (Implementation detail, can be decided when implementing sample data)
- [ ] Should complete data export be async with progress tracking for very large datasets? (Implementation detail)
- [ ] Should we auto-expire deleted tenant data or keep tombstone records for audit? (Defer for later)
- [X] Should year selector show only years with actual data, or all years? -> Only years with active data
- [X] How to handle entity types with complex navigation properties? -> Design detail. The PRD is entity agnostic, the implementation doesn't need to be.
- [X] Should we support JSON format in addition to XLSX for API-friendly workflows? NO, API workflows would be their own feature.

---

## Success Metrics

- **Export Adoption**: 50%+ of users export data at least once
- **Flexible Export Usage**: 60%+ of exports use custom selection (not "Export All")
- **Bulk Update Usage**: 40%+ of users who export also re-import (export→edit→import workflow)
- **Sample Data Adoption**: 60%+ of new users load sample data during first session
- **V1 Migration Success**: Track how many V1 users successfully migrate using import
- **Error Rate**: <3% of imports fail with validation errors
- **GDPR Requests**: Track complete deletion requests (should be rare but available)
- **Page Usability**: <5% of users navigate away without completing intended action
- **Future Entity Support**: New entity types work without PRD changes (measure success of generic approach)

---

## Notes & Context

**Data-Agnostic Philosophy**:
This PRD intentionally avoids specifying exact entity types or field lists. The system discovers available entities at runtime and works generically with any tenant entity type. This makes the feature future-proof as new entities are added to the application.

**YoFi V1 Reference**:
The original YoFi V1 application (https://github.com/jcoliz/yofi) implemented import/export with hardcoded entity types. This V3 PRD modernizes with:
- **Data-agnostic design** - Works with any entity type
- **Single unified page** - One place for all operations
- **Flexible selection model** - Any combination of entities
- **Year-based filtering** - Easier date range selection
- **XLSX format** - Better than CSV for multi-entity
- **GUID Keys throughout** - No database IDs
- **Generic Key-based create/update** - Same pattern for all entities

**GDPR Compliance**:
This feature addresses two key GDPR requirements:
- **Right to Data Portability** (Article 20) - Complete data export in structured format
- **Right to Erasure** (Article 17) - Complete data deletion on user request

**Related Features**:
- **Bank Import** (separate PRD) - Handles OFX/QFX import with review workflow
- Entity type schemas are defined in their respective PRDs/design docs

---

## Handoff Checklist (for AI implementation)

When handing this off for detailed design/implementation:
- [ ] All user stories have clear acceptance criteria
- [ ] Open questions are resolved or documented as design decisions
- [ ] Technical approach section indicates affected layers
- [ ] Any existing code patterns or files to reference are noted
