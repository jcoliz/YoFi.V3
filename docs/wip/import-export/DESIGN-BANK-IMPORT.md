---
status: Draft
created: 2025-12-28
prd: PRD-BANK-IMPORT.md
related_docs:
  - PRD-BANK-IMPORT.md
  - IMPORT-REVIEW-DATA-MODEL.md
  - VISUAL-DESIGN-BANK-IMPORT.md
  - MOCKUP-BANK-IMPORT.md
  - OFX-LIBRARY-EVALUATION.md
  - OFXSHARP-INTEGRATION-DECISION.md
---

# Design Document: Transaction Bank Import

## Overview

This document provides the detailed technical design for implementing the Transaction Bank Import feature as specified in [`PRD-BANK-IMPORT.md`](PRD-BANK-IMPORT.md). The feature enables users to upload OFX/QFX bank files, review imported transactions with duplicate detection, and selectively accept transactions into their workspace.

**Key capabilities:**
- Parse OFX and QFX bank files to extract transaction data
- Store transactions in temporary import review state with duplicate detection
- Provide UI for reviewing, selecting, and accepting transactions
- Maintain tenant isolation and data integrity throughout the workflow

## Architecture Overview

The Bank Import feature follows Clean Architecture principles with clear separation across layers:

### Data Layer
- **ImportReviewTransaction entity** - Separate table for staging transactions during review
- **DuplicateStatus enum** - Tracks whether transaction is New, ExactDuplicate, or PotentialDuplicate
- **Migration** - Creates `YoFi.V3.ImportReviewTransactions` table with tenant isolation and indexes
- **Repository methods** - CRUD operations for import review transactions

### Application Layer
- **OFXParsingService** - Parses OFX/QFX files using OfxSharp library
- **ImportReviewFeature** - Business logic for import workflow, duplicate detection, accept/reject operations
- **DTOs** - ImportReviewTransactionDto, OFXParsingResult, TransactionImportDto
- **Validation** - Ensures parsed transactions meet schema requirements

### API Layer
- **ImportController** - Endpoints for file upload, get pending review, accept, delete operations
- **Authentication/Authorization** - Tenant-scoped access control via TenantContext middleware
- **File upload handling** - Multipart form data processing with validation

### Frontend Layer
- **Import page** (import.vue) - File picker, upload status, transaction review table
- **Components** - Reusable file upload, transaction table with selection, action buttons
- **State management** - Session storage for selection persistence across navigation
- **API client calls** - Uses auto-generated TypeScript client

### Workflow
1. User uploads OFX file → Frontend sends multipart POST to `/api/import/upload`
2. Backend parses file → OFXParsingService extracts transactions
3. Duplicate detection → Compare against existing transactions and pending imports
4. Store in staging → Insert into ImportReviewTransactions with DuplicateStatus
5. Return to UI → Frontend displays transactions grouped by duplicate status
6. User reviews → Selects/deselects transactions, clicks "Import"
7. Accept selected → Backend copies to Transactions table, deletes from ImportReviewTransactions
8. Navigate to transactions → User sees newly imported data in main transaction list

## Layer-Specific Design Documents

Detailed implementation plans for each layer:

1. **[DESIGN-BANK-IMPORT-DATA.md](DESIGN-BANK-IMPORT-DATA.md)** - Entity model, migration, repository methods, duplicate detection queries
2. **[DESIGN-BANK-IMPORT-APPLICATION.md](DESIGN-BANK-IMPORT-APPLICATION.md)** - OFXParsingService, ImportReviewFeature, DTOs, validation logic
3. **[DESIGN-BANK-IMPORT-API.md](DESIGN-BANK-IMPORT-API.md)** - Controller endpoints, request/response contracts, error handling, logging
4. **[DESIGN-BANK-IMPORT-FRONTEND.md](DESIGN-BANK-IMPORT-FRONTEND.md)** - Vue pages, components, state management, API integration, user interactions

## Security Considerations

**Tenant Isolation:**
- All import operations scoped to current authenticated user's tenant via TenantContext middleware
- ImportReviewTransactions table includes TenantId foreign key with cascade delete
- Repository queries always filter by TenantId
- No cross-tenant access to pending imports or accepted transactions

**File Upload Security:**
- File type validation (restrict to .ofx and .qfx extensions)
- File size limits enforced (prevent DoS via large files)
- Content validation via OFX parser (reject malformed files)
- No execution of uploaded content (parse only, no script/code execution)
- Multipart form data sanitization

**Data Integrity:**
- Transaction Keys preserved when accepting from review (client-side tracking)
- ExternalId (FITID) stored to prevent re-import of same bank transactions
- Database transactions for atomic accept operations (copy + delete)
- Validation of all parsed data before storage

## Performance Considerations

**Import Processing:**
- Typical bank files: 100-1,000 transactions (1-5 MB)
- Large files: Up to 10,000 transactions (10-50 MB)
- Sequential file processing acceptable for initial release
- Parsing performance: OfxSharp benchmarked at <1 second for typical files

**Duplicate Detection:**
- Query both Transactions and ImportReviewTransactions tables
- Indexes on (TenantId, ExternalId) for fast lookups
- In-memory comparison for field-level duplicate detection
- Expected overhead: <2 seconds for 1,000 transaction import

**UI Rendering:**
- Pagination required (50-100 transactions per page)
- Session storage for selection state (avoids server round-trips)
- Optimistic UI updates for accept/delete operations
- Frontend handles large transaction sets without performance degradation

**Database:**
- ImportReviewTransactions table separate from Transactions (no index pollution)
- Minimal storage overhead (temporary staging only)
- Auto-cleanup via cascade delete when tenant removed

## Migration and Rollout

**Database Migration:**
- Create ImportReviewTransactions table
- Add indexes for tenant isolation and duplicate detection
- No changes to existing Transactions table schema
- Backward compatible (no breaking changes)

**Feature Flag:**
- Not required (feature is net-new, no existing functionality to replace)
- Direct rollout acceptable

**Rollout Strategy:**
1. Deploy backend with new table, API endpoints, and OFX parsing
2. Deploy frontend with Import page and navigation item
3. Monitor logs for parsing errors, file format issues
4. Collect telemetry on file formats, sizes, duplicate rates

**YoFi V1 Migration:**
- V1 used single table with `Imported` flag (not recommended for V3)
- If migrating V1 data: Copy `Imported=false` transactions to ImportReviewTransactions, drop flag

## Future Enhancements

See [`VISUAL-DESIGN-BANK-IMPORT.md`](VISUAL-DESIGN-BANK-IMPORT.md) for deferred UI enhancements:

- Drag-and-drop file upload interface
- Side-by-side comparison view for potential duplicates
- Badge indicator in navigation showing pending import count
- Help text/tooltips explaining import workflow
- Responsive design optimizations for mobile/tablet
- Real-time bank API integration (Plaid, etc.)
- Scheduled/automated imports
- Import from additional formats (CSV, XLSX via Tenant Data Admin)

## References

**Requirements and Planning:**
- [`PRD-BANK-IMPORT.md`](PRD-BANK-IMPORT.md) - Product requirements and user stories
- [`IMPORT-REVIEW-DATA-MODEL.md`](IMPORT-REVIEW-DATA-MODEL.md) - Data model analysis and decision rationale
- [`VISUAL-DESIGN-BANK-IMPORT.md`](VISUAL-DESIGN-BANK-IMPORT.md) - UI design and interaction patterns
- [`MOCKUP-BANK-IMPORT.md`](MOCKUP-BANK-IMPORT.md) - Visual mockups of all page states

**Technical Decisions:**
- [`OFX-LIBRARY-EVALUATION.md`](OFX-LIBRARY-EVALUATION.md) - Analysis of OFX parsing libraries
- [`OFXSHARP-INTEGRATION-DECISION.md`](OFXSHARP-INTEGRATION-DECISION.md) - Selected library and integration approach

**Related Features:**
- [Transaction Record PRD](../transactions/PRD-TRANSACTION-RECORD.md) - Transaction schema and validation
- [Transaction Splits PRD](../transactions/PRD-TRANSACTION-SPLITS.md) - Split handling (not included in bank imports)
- [Tenant Data Admin PRD](PRD-TENANT-DATA-ADMIN.md) - XLSX import/export for all entity types

**Architecture:**
- [`docs/ARCHITECTURE.md`](../../ARCHITECTURE.md) - Overall system architecture
- [`docs/TENANCY.md`](../../TENANCY.md) - Multi-tenancy patterns
- [`docs/TESTING-STRATEGY.md`](../../TESTING-STRATEGY.md) - Testing approach for new features
