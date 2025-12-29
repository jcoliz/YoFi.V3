---
status: Draft
layer: Database
parent: DESIGN-BANK-IMPORT.md
created: 2025-12-28
related_docs:
  - DESIGN-BANK-IMPORT.md
  - IMPORT-REVIEW-DATA-MODEL.md
  - PRD-BANK-IMPORT.md
---

# Database Layer Design: Bank Import Feature

## Overview

This document provides the complete database schema design for the Bank Import feature, including entity models, EF Core migrations, and DbContext configuration. The design implements Option 2 from [`IMPORT-REVIEW-DATA-MODEL.md`](IMPORT-REVIEW-DATA-MODEL.md): a separate `ImportReviewTransactions` table for staging imported transactions during the review workflow.

**Key components:**
- [`ImportReviewTransaction`](src/Entities/Models/ImportReviewTransaction.cs) entity with duplicate detection metadata
- [`DuplicateStatus`](src/Entities/Models/DuplicateStatus.cs) enum for tracking duplicate state
- EF Core migration creating table with indexes and foreign keys
- [`ApplicationDbContext`](src/Data/Sqlite/ApplicationDbContext.cs) configuration for SQLite compatibility

## Entity Model

### ImportReviewTransaction Record

Location: `src/Entities/Models/ImportReviewTransaction.cs`

**Purpose:** Import review transactions are temporary staging records created when users upload OFX/QFX bank files. Users review these transactions, check for duplicates, and selectively accept them into the main transaction table. Once accepted or rejected, records are removed from this table.

**Why a separate table?** This separate table (rather than a status flag on the main Transaction table) ensures:
- Clean separation of temporary staging data from production transaction data
- No impact on main transaction queries, reports, or analytics
- Additional import-specific metadata (DuplicateStatus, DuplicateOfKey, ImportedAt)
- Simple bulk operations (delete all pending imports for a tenant)

See [`IMPORT-REVIEW-DATA-MODEL.md`](IMPORT-REVIEW-DATA-MODEL.md) for the complete analysis and decision rationale.

```csharp
/// <summary>
/// Represents a transaction in import review state during the bank import workflow.
/// </summary>
[Table("YoFi.V3.ImportReviewTransactions")]
public record ImportReviewTransaction : BaseTenantModel
{
    public DateOnly Date { get; set; }
    public string Payee { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Source { get; set; }
    public string? ExternalId { get; set; }
    public string? Memo { get; set; }
    public DuplicateStatus DuplicateStatus { get; set; } = DuplicateStatus.New;
    public Guid? DuplicateOfKey { get; set; }
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
    public virtual Tenant? Tenant { get; set; }
}
```

**Field descriptions:**
- **Date** - Transaction date as reported by the bank
- **Payee** - Payee or merchant name (required, max 200 chars)
- **Amount** - Transaction amount (positive for deposits, negative for withdrawals)
- **Source** - Import source derived from OFX account info (nullable, max 200 chars)
- **ExternalId** - Bank transaction ID from OFX FITID field for duplicate detection (nullable, max 100 chars)
- **Memo** - Notes field from bank statement (nullable, max 1000 chars)
- **DuplicateStatus** - Duplicate detection status determining default UI selection
- **DuplicateOfKey** - Key of existing transaction if duplicate detected (nullable)
- **ImportedAt** - Upload timestamp for batch tracking
- **Tenant** - Navigation property to owning tenant

### DuplicateStatus Enum

Location: `src/Entities/Models/DuplicateStatus.cs`

**Purpose:** Used during bank import to indicate whether an imported transaction is new or a duplicate of an existing transaction.

**Default selection behavior:** This status determines the default selection state in the review UI:
- `New` transactions are selected by default (user should import)
- `ExactDuplicate` and `PotentialDuplicate` are deselected by default (user should review)

```csharp
/// <summary>
/// Status of a transaction in import review relative to existing transactions.
/// </summary>
public enum DuplicateStatus
{
    /// <summary>
    /// New transaction - no duplicates found in existing transactions or pending imports.
    /// Selected by default for import.
    /// </summary>
    New = 0,

    /// <summary>
    /// Exact duplicate - same ExternalId (FITID) and matching data (Date, Amount, Payee).
    /// Deselected by default. User should NOT import to avoid duplicate records.
    /// </summary>
    ExactDuplicate = 1,

    /// <summary>
    /// Potential duplicate - same ExternalId (FITID) but different data (Date, Amount, or Payee).
    /// Highlighted and deselected by default. User should review carefully before importing.
    /// May indicate a bank correction, amended transaction, or data quality issue.
    /// </summary>
    PotentialDuplicate = 2
}
```

## EF Core Migration

**Migration name:** `AddImportReviewTable`

**Purpose:** Creates the `ImportReviewTransactions` table implementing the separate table approach (Option 2) from [`IMPORT-REVIEW-DATA-MODEL.md`](IMPORT-REVIEW-DATA-MODEL.md).

**Required indexes:**
- `IX_ImportReviewTransactions_Key` (unique) - Business key lookup
- `IX_ImportReviewTransactions_TenantId` - Tenant-scoped queries
- `IX_ImportReviewTransactions_TenantId_ExternalId` - Duplicate detection by FITID

**Additional migration needed:** Add `IX_Transactions_TenantId_ExternalId` index to existing Transactions table for duplicate detection performance.

## DbContext Configuration

Location: `src/Data/Sqlite/ApplicationDbContext.cs`

**Required changes:**
- Add `DbSet<ImportReviewTransaction>` property
- Configure entity in `OnModelCreating()` following existing patterns:
  - Standard indexes (Key, TenantId, TenantId+ExternalId composite)
  - DateOnly conversion for SQLite (ISO 8601 format)
  - String length constraints matching entity annotations
  - Decimal precision for Amount field
  - Enum conversion for DuplicateStatus
  - Foreign key with cascade delete to Tenant

Reference existing Transaction entity configuration for patterns.

## Key Design Decisions

### 1. Separate Table vs. Status Flag

**Decision:** Use separate `ImportReviewTransactions` table (not a status flag on `Transactions` table).

**Rationale (from [`IMPORT-REVIEW-DATA-MODEL.md`](IMPORT-REVIEW-DATA-MODEL.md)):**
- **Clean separation** - Main transaction queries unaffected by review state, no need to filter `WHERE Imported = true` on every query
- **No performance impact** - Indexes on main Transactions table not polluted by temporary staging data
- **Import-specific metadata** - Natural place to store `DuplicateStatus`, `DuplicateOfKey`, and `ImportedAt` without cluttering the main entity
- **Simpler reasoning** - Clear workflow: Import → Review (separate table) → Accept (copy to Transactions) → Delete (from ImportReviewTransactions)
- **Safer** - Impossible to accidentally include review transactions in reports, analytics, or exports

**Trade-off accepted:** Some schema duplication (7-8 fields), but the benefits of separation far outweigh the duplication cost.

### 2. Index Strategy for Duplicate Detection

**Indexes created on ImportReviewTransactions table:**
1. `IX_ImportReviewTransactions_Key` (unique) - Standard business key lookup
2. `IX_ImportReviewTransactions_TenantId` - Tenant-scoped queries (get all pending imports)
3. `IX_ImportReviewTransactions_TenantId_ExternalId` - Duplicate detection by FITID

**Index required on Transactions table:**
- `IX_Transactions_TenantId_ExternalId` (composite) - Duplicate detection against existing transactions

**Rationale:**
- Duplicate detection algorithm queries BOTH tables by `(TenantId, ExternalId)` to find matching bank transaction IDs
- Composite indexes on both tables enable fast lookups during import processing (1,000 transactions = 2,000 total queries)
- Tenant isolation ensures index efficiency (no cross-tenant data scanned)
- Without the Transactions index, duplicate detection would require full table scans

**Performance:** Expected duplicate check time <2 seconds for 1,000 transaction import with both indexes in place.

**Implementation Note:** The Transactions table index should be added in a separate migration as part of this feature implementation.

### 3. Cascade Delete for Tenant Isolation

**Decision:** Foreign key to Tenants with `ON DELETE CASCADE`.

**Rationale:**
- When a tenant is deleted, all pending import review transactions must be removed
- Cascade delete handles this automatically without application code
- Prevents orphaned staging data in the database
- Consistent with other tenant-owned entities (Transactions, Splits, etc.)

**Safety:** Import review transactions are temporary staging data, safe to delete with tenant.

### 4. SQLite-Specific Column Types

**Key conversions:**
- `DateOnly` → `TEXT` (ISO 8601 format: "yyyy-MM-dd")
- `decimal` → `TEXT` (SQLite has no native decimal type)
- `Guid` → `TEXT` (SQLite has no native GUID type)
- `DuplicateStatus` enum → `INTEGER`

**Rationale:**
- SQLite type limitations require explicit conversions in EF Core
- EF Core handles conversions transparently via [`HasConversion()`](src/Data/Sqlite/ApplicationDbContext.cs) methods
- Consistent with existing Transaction and Tenant configurations

**Reference:** See [`ApplicationDbContext.cs`](src/Data/Sqlite/ApplicationDbContext.cs) for existing patterns (Transaction.Date uses same DateOnly conversion).

### 5. DuplicateStatus Enum Storage

**Decision:** Store as `int` in database (not string).

**Rationale:**
- Integer storage more efficient than string (4 bytes vs. variable)
- Enum values stable (0=New, 1=ExactDuplicate, 2=PotentialDuplicate)
- Database queries can use numeric comparisons (`DuplicateStatus = 0`)
- Consistent with other enum storage patterns in the project

**Alternative considered:** String storage for readability in database tools, rejected due to storage overhead and no compelling debugging benefit.

## Schema Comparison: ImportReviewTransaction vs. Transaction

| Field | ImportReviewTransaction | Transaction | Notes |
|-------|------------------------|-------------|-------|
| **Id** | ✅ `long` (PK) | ✅ `long` (PK) | Standard surrogate key |
| **Key** | ✅ `Guid` (unique) | ✅ `Guid` (unique) | Business key, preserved on accept |
| **TenantId** | ✅ `long` (FK) | ✅ `long` (FK) | Tenant isolation |
| **Date** | ✅ `DateOnly` | ✅ `DateOnly` | Transaction date |
| **Payee** | ✅ `string(200)` | ✅ `string(200)` | Required |
| **Amount** | ✅ `decimal(18,2)` | ✅ `decimal(18,2)` | Currency precision |
| **Source** | ✅ `string(200)?` | ✅ `string(200)?` | Nullable |
| **ExternalId** | ✅ `string(100)?` | ✅ `string(100)?` | Bank FITID |
| **Memo** | ✅ `string(1000)?` | ✅ `string(1000)?` | Nullable |
| **DuplicateStatus** | ✅ `int` (enum) | ❌ | Import-specific |
| **DuplicateOfKey** | ✅ `Guid?` | ❌ | Import-specific |
| **ImportedAt** | ✅ `DateTime` | ❌ | Import-specific |
| **CreatedAt** | ✅ `DateTime` | ✅ `DateTime` | Audit timestamp |
| **Splits** | ❌ | ✅ `ICollection<Split>` | Added on accept |

**Workflow:** When accepting transactions, copy all matching fields from ImportReviewTransaction → Transaction, generate default Split, then delete from ImportReviewTransactions.

## Testing Considerations

**Unit tests (Data layer):**
- Entity creation and property validation
- Enum conversion (DuplicateStatus)
- DateOnly conversion for SQLite

**Integration tests (Data layer):**
- CRUD operations on ImportReviewTransactions table
- Foreign key cascade delete (delete tenant → import review transactions deleted)
- Index usage verification (query plans for duplicate detection)
- Repository methods (GetByTenantAsync, AddAsync, DeleteAsync)

**Integration tests (Application layer):**
- Duplicate detection algorithm using both Transactions and ImportReviewTransactions
- Accept workflow (atomic copy + delete)
- Bulk operations (delete all pending imports)

**Test data requirements:**
- Sample OFX files with known transaction counts
- Existing transactions with ExternalIds for duplicate testing
- Multiple tenants to verify isolation

**Verification steps:**
- Verify indexes created correctly (`PRAGMA index_list`, `PRAGMA index_info`)

## Migration Checklist

- [ ] Create `ImportReviewTransaction` entity in `src/Entities/Models/`
- [ ] Create `DuplicateStatus` enum in `src/Entities/Models/`
- [ ] Add `DbSet<ImportReviewTransaction>` to [`ApplicationDbContext.cs`](src/Data/Sqlite/ApplicationDbContext.cs)
- [ ] Add entity configuration to [`OnModelCreating()`](src/Data/Sqlite/ApplicationDbContext.cs)
- [ ] Create EF Core migration using `dotnet ef migrations add AddImportReviewTable`
- [ ] Review generated migration SQL for correctness
- [ ] Apply migration to development database

## References

**Design Documents:**
- [`DESIGN-BANK-IMPORT.md`](DESIGN-BANK-IMPORT.md) - Overall feature architecture
- [`IMPORT-REVIEW-DATA-MODEL.md`](IMPORT-REVIEW-DATA-MODEL.md) - Data model analysis and decision rationale
- [`PRD-BANK-IMPORT.md`](PRD-BANK-IMPORT.md) - Product requirements

**Related Code:**
- [`ApplicationDbContext.cs`](src/Data/Sqlite/ApplicationDbContext.cs) - EF Core context with existing entity configurations
- [`Transaction.cs`](src/Entities/Models/Transaction.cs) - Main transaction entity (reference schema)
- [`BaseTenantModel.cs`](src/Entities/Tenancy/Models/BaseTenantModel.cs) - Base class for tenant-owned entities

**Architecture:**
- [`docs/ARCHITECTURE.md`](../../ARCHITECTURE.md) - Clean Architecture layers
- [`docs/TENANCY.md`](../../TENANCY.md) - Multi-tenancy patterns and tenant isolation
