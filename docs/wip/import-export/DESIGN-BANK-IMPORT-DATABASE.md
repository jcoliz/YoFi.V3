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

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using YoFi.V3.Entities.Tenancy.Models;

namespace YoFi.V3.Entities.Models;

/// <summary>
/// Represents a transaction in import review state during the bank import workflow.
/// </summary>
/// <remarks>
/// <para>
/// Import review transactions are temporary staging records created when users upload OFX/QFX bank files.
/// Users review these transactions, check for duplicates, and selectively accept them into the main
/// transaction table. Once accepted or rejected, records are removed from this table.
/// </para>
/// <para>
/// This separate table (rather than a status flag on the main Transaction table) ensures:
/// </para>
/// <list type="bullet">
/// <item>Clean separation of temporary staging data from production transaction data</item>
/// <item>No impact on main transaction queries, reports, or analytics</item>
/// <item>Additional import-specific metadata (DuplicateStatus, DuplicateOfKey, ImportedAt)</item>
/// <item>Simple bulk operations (delete all pending imports for a tenant)</item>
/// </list>
/// <para>
/// See <see href="https://github.com/jcoliz/YoFi.V3/blob/main/docs/wip/import-export/IMPORT-REVIEW-DATA-MODEL.md">IMPORT-REVIEW-DATA-MODEL.md</see>
/// for the complete analysis and decision rationale.
/// </para>
/// </remarks>
[Table("YoFi.V3.ImportReviewTransactions")]
public record ImportReviewTransaction : BaseTenantModel
{
    /// <summary>
    /// Date of the transaction as reported by the bank.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Payee or merchant name for the transaction.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Payee { get; set; } = string.Empty;

    /// <summary>
    /// Transaction amount in the account's currency.
    /// Positive for deposits/credits, negative for withdrawals/debits.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Source of the import (e.g., "Chase Checking", "Capital One Credit Card").
    /// Typically derived from the OFX file's account information.
    /// </summary>
    [MaxLength(200)]
    public string? Source { get; set; }

    /// <summary>
    /// External transaction ID from the bank (OFX FITID field).
    /// Used for duplicate detection across imports.
    /// </summary>
    [MaxLength(100)]
    public string? ExternalId { get; set; }

    /// <summary>
    /// Memo or notes field from the bank statement (OFX MEMO field).
    /// </summary>
    [MaxLength(1000)]
    public string? Memo { get; set; }

    /// <summary>
    /// Duplicate detection status relative to existing transactions.
    /// Determines default selection state in the review UI.
    /// </summary>
    public DuplicateStatus DuplicateStatus { get; set; } = DuplicateStatus.New;

    /// <summary>
    /// Key of the existing transaction if this is detected as a duplicate.
    /// Null for new transactions (DuplicateStatus = New).
    /// </summary>
    public Guid? DuplicateOfKey { get; set; }

    /// <summary>
    /// Timestamp when the import file was uploaded and parsed.
    /// Used for tracking import batches and debugging.
    /// </summary>
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property to the tenant that owns this import review transaction.
    /// </summary>
    public virtual Tenant? Tenant { get; set; }
}
```

> [!TODO]: Reconsider "DuplicateofKey." What if *that* transaction gets deleted? Seems better to me to hang onto the Unique ID. Also, how are we going to get the Unique ID into the Transaction entity, if we aren't holding it here.

> [!TODO]: Let's hold space for payee matching. We need to show user the intended match. So I think app layer will need to take some kind of matching interface. Would we want an IBulkCategoryMatch interface which takes a model and changes it directly? Also note that payee matching has to happen against EITHER in-place transactions OR imported transactions, so we either need a base DTO for it to operate against, or an interface.

### DuplicateStatus Enum

Location: `src/Entities/Models/DuplicateStatus.cs`

```csharp
namespace YoFi.V3.Entities.Models;

/// <summary>
/// Status of a transaction in import review relative to existing transactions.
/// </summary>
/// <remarks>
/// Used during bank import to indicate whether an imported transaction is new or a duplicate
/// of an existing transaction. This status determines the default selection state in the review UI:
/// <list type="bullet">
/// <item><see cref="New"/> transactions are selected by default (user should import)</item>
/// <item><see cref="ExactDuplicate"/> and <see cref="PotentialDuplicate"/> are deselected by default (user should review)</item>
/// </list>
/// </remarks>
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

Location: `src/Data/Sqlite/Migrations/YYYYMMDDHHMMSS_AddImportReviewTable.cs`

```csharp
using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace YoFi.V3.Data.Sqlite.Migrations
{
    /// <summary>
    /// Creates the ImportReviewTransactions table for bank import workflow staging.
    /// </summary>
    /// <remarks>
    /// This migration implements the separate table approach (Option 2) from IMPORT-REVIEW-DATA-MODEL.md.
    /// The table stores transactions during the import review workflow, with duplicate detection metadata
    /// and tenant isolation. Once transactions are accepted or rejected, they are removed from this table.
    /// </remarks>
    public partial class AddImportReviewTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "YoFi.V3.ImportReviewTransactions",
                columns: table => new
                {
                    // Primary key - surrogate integer for database efficiency
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),

                    // Business key - GUID for client-side tracking and API references
                    Key = table.Column<Guid>(type: "TEXT", nullable: false),

                    // Tenant isolation - foreign key to Tenants table
                    TenantId = table.Column<long>(type: "INTEGER", nullable: false),

                    // Transaction core fields (match Transaction table schema)
                    Date = table.Column<string>(type: "TEXT", nullable: false), // DateOnly stored as TEXT in SQLite
                    Payee = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Amount = table.Column<decimal>(type: "TEXT", nullable: false), // Decimal stored as TEXT in SQLite
                    Source = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ExternalId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Memo = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),

                    // Import-specific metadata
                    DuplicateStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    DuplicateOfKey = table.Column<Guid>(type: "TEXT", nullable: true),
                    ImportedAt = table.Column<DateTime>(type: "TEXT", nullable: false),

                    // Audit timestamp (inherited from BaseTenantModel)
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YoFi.V3.ImportReviewTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_YoFi.V3.ImportReviewTransactions_YoFi.V3.Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "YoFi.V3.Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Index: Unique business key (standard pattern for all entities)
            migrationBuilder.CreateIndex(
                name: "IX_YoFi.V3.ImportReviewTransactions_Key",
                table: "YoFi.V3.ImportReviewTransactions",
                column: "Key",
                unique: true);

            // Index: Tenant-scoped queries (most common operation)
            migrationBuilder.CreateIndex(
                name: "IX_YoFi.V3.ImportReviewTransactions_TenantId",
                table: "YoFi.V3.ImportReviewTransactions",
                column: "TenantId");

            // Index: Duplicate detection by ExternalId within tenant
            // Used by duplicate detection algorithm to find matching FITID
            migrationBuilder.CreateIndex(
                name: "IX_YoFi.V3.ImportReviewTransactions_TenantId_ExternalId",
                table: "YoFi.V3.ImportReviewTransactions",
                columns: new[] { "TenantId", "ExternalId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "YoFi.V3.ImportReviewTransactions");
        }
    }
}
```

## DbContext Configuration

Location: `src/Data/Sqlite/ApplicationDbContext.cs`

Add to the `#region Data` section:

```csharp
/// <summary>
/// Import review transactions awaiting user acceptance or rejection.
/// </summary>
public DbSet<ImportReviewTransaction> ImportReviewTransactions { get; set; }
```

Add to the [`OnModelCreating(ModelBuilder)`](src/Data/Sqlite/ApplicationDbContext.cs) method:

```csharp
// Configure ImportReviewTransaction
modelBuilder.Entity<ImportReviewTransaction>(entity =>
{
    // Unique Guid key (standard pattern for all entities)
    entity.HasIndex(e => e.Key)
        .IsUnique();

    // Index on TenantId for efficient tenant-scoped queries
    entity.HasIndex(e => e.TenantId);

    // Composite index on TenantId + ExternalId for duplicate detection
    entity.HasIndex(e => new { e.TenantId, e.ExternalId });

    // DateOnly conversion for SQLite (stored as TEXT in ISO 8601 format)
    entity.Property(e => e.Date)
        .HasConversion(
            v => v.ToString("yyyy-MM-dd"),           // DateOnly → string
            v => DateOnly.Parse(v));                 // string → DateOnly

    // Payee is required (max 200 chars)
    entity.Property(e => e.Payee)
        .IsRequired()
        .HasMaxLength(200);

    // Amount precision for currency (stored as TEXT in SQLite)
    entity.Property(e => e.Amount)
        .HasPrecision(18, 2);

    // Source (nullable, max 200 chars)
    entity.Property(e => e.Source)
        .HasMaxLength(200);

    // ExternalId (nullable, max 100 chars)
    entity.Property(e => e.ExternalId)
        .HasMaxLength(100);

    // Memo (nullable, max 1000 chars)
    entity.Property(e => e.Memo)
        .HasMaxLength(1000);

    // DuplicateStatus enum stored as int in database
    entity.Property(e => e.DuplicateStatus)
        .HasConversion<int>();

    // ImportedAt timestamp (required)
    entity.Property(e => e.ImportedAt)
        .IsRequired();

    // Foreign key relationship to Tenant with cascade delete
    // When a tenant is deleted, all pending import review transactions are automatically removed
    entity.HasOne(e => e.Tenant)
        .WithMany()
        .HasForeignKey(e => e.TenantId)
        .OnDelete(DeleteBehavior.Cascade);
});
```

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

**Indexes created:**
1. `IX_ImportReviewTransactions_Key` (unique) - Standard business key lookup
2. `IX_ImportReviewTransactions_TenantId` - Tenant-scoped queries (get all pending imports)
3. `IX_ImportReviewTransactions_TenantId_ExternalId` - Duplicate detection by FITID

**Rationale:**
- Duplicate detection algorithm queries by `(TenantId, ExternalId)` to find matching bank transaction IDs
- Composite index enables fast lookups during import processing (1,000 transactions = 1,000 duplicate checks)
- Tenant isolation ensures index efficiency (no cross-tenant data scanned)

**Performance:** Expected duplicate check time <2 seconds for 1,000 transaction import.

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

## Repository Interface Extensions

Location: `src/Entities/Providers/IDataProvider.cs`

The following methods will be added to [`IDataProvider`](src/Entities/Providers/IDataProvider.cs) to support import review operations:

```csharp
/// <summary>
/// Retrieves all import review transactions for a specific tenant.
/// </summary>
/// <param name="tenantId">The tenant ID to filter by.</param>
/// <returns>Collection of import review transactions ordered by date descending.</returns>
Task<IReadOnlyCollection<ImportReviewTransaction>> GetImportReviewTransactionsByTenantAsync(long tenantId);

/// <summary>
/// Retrieves specific import review transactions by their keys within a tenant.
/// </summary>
/// <param name="keys">The transaction keys to retrieve.</param>
/// <param name="tenantId">The tenant ID to filter by (security).</param>
/// <returns>Collection of import review transactions matching the keys.</returns>
Task<IReadOnlyCollection<ImportReviewTransaction>> GetImportReviewTransactionsByKeysAsync(
    IReadOnlyCollection<Guid> keys,
    long tenantId);

/// <summary>
/// Adds a collection of import review transactions.
/// </summary>
/// <param name="transactions">The transactions to add.</param>
Task AddImportReviewTransactionsAsync(IReadOnlyCollection<ImportReviewTransaction> transactions);

/// <summary>
/// Deletes specific import review transactions by their keys within a tenant.
/// </summary>
/// <param name="keys">The transaction keys to delete.</param>
/// <param name="tenantId">The tenant ID to filter by (security).</param>
Task DeleteImportReviewTransactionsAsync(IReadOnlyCollection<Guid> keys, long tenantId);

/// <summary>
/// Deletes all import review transactions for a specific tenant.
/// </summary>
/// <param name="tenantId">The tenant ID to clear imports for.</param>
Task DeleteAllImportReviewTransactionsAsync(long tenantId);
```

Implementation location: `src/Data/Sqlite/ApplicationDbContext.cs`

These methods will be implemented as explicit interface implementations in [`ApplicationDbContext`](src/Data/Sqlite/ApplicationDbContext.cs), following the existing pattern for Transaction and Tenant operations.

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

## Migration Checklist

- [ ] Create `ImportReviewTransaction` entity in `src/Entities/Models/`
- [ ] Create `DuplicateStatus` enum in `src/Entities/Models/`
- [ ] Add `DbSet<ImportReviewTransaction>` to [`ApplicationDbContext.cs`](src/Data/Sqlite/ApplicationDbContext.cs)
- [ ] Add entity configuration to [`OnModelCreating()`](src/Data/Sqlite/ApplicationDbContext.cs)
- [ ] Create EF Core migration using `dotnet ef migrations add AddImportReviewTable`
- [ ] Review generated migration SQL for correctness
- [ ] Add repository methods to [`IDataProvider`](src/Entities/Providers/IDataProvider.cs) interface
- [ ] Implement repository methods in [`ApplicationDbContext`](src/Data/Sqlite/ApplicationDbContext.cs)
- [ ] Write unit tests for entity validation
- [ ] Write integration tests for CRUD operations
- [ ] Apply migration to development database
- [ ] Verify indexes created correctly (`PRAGMA index_list`, `PRAGMA index_info`)

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
