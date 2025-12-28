---
status: Approved
created: 2025-12-28
decision: Use separate ImportReviewTransactions table (Option 2)
related_docs:
  - PRD-BANK-IMPORT.md
  - OFX-LIBRARY-EVALUATION.md
  - OFXSHARP-INTEGRATION-DECISION.md
---

# Import Review State: Data Model Analysis

## Problem Statement

Bank import requires a review workflow where users can:
1. Upload OFX file → transactions enter "import review" state
2. Review transactions → see duplicates, select which to accept
3. Accept selected → transactions move to primary transaction table
4. Delete unwanted → transactions removed from review state

**Key Question:** How should we store transactions in import review state?

## Option 1: Single Table with Status Flag (YoFi V1 Approach)

**Schema:**
```sql
ALTER TABLE [YoFi.V3.Transactions] ADD [Imported] BIT NOT NULL DEFAULT 0;
```

**Entity Model:**
```csharp
public record Transaction : BaseTenantModel
{
    // Existing fields...
    public DateOnly Date { get; set; }
    public string Payee { get; set; }
    public decimal Amount { get; set; }
    public string? Source { get; set; }
    public string? ExternalId { get; set; }
    public string? Memo { get; set; }

    // NEW: Import review flag
    public bool Imported { get; set; } = true; // Default: accepted transactions

    public virtual ICollection<Split> Splits { get; set; }
}
```

**Query Patterns:**
```csharp
// Get accepted transactions (normal view)
var transactions = await context.Transactions
    .Where(t => t.TenantId == tenantId && t.Imported == true)
    .ToListAsync();

// Get pending import review transactions
var pendingImports = await context.Transactions
    .Where(t => t.TenantId == tenantId && t.Imported == false)
    .ToListAsync();

// Accept transactions
transaction.Imported = true;
await context.SaveChangesAsync();

// Delete rejected transactions
context.Transactions.Remove(transaction);
await context.SaveChangesAsync();
```

### Pros

✅ **Simple schema** - Single table, minimal migration
✅ **Code reuse** - Existing Transaction entity, features, and validations work for both states
✅ **Easy queries** - Simple WHERE clause to filter by state
✅ **Proven approach** - Worked in YoFi V1
✅ **Consistent Key management** - Same GUID Key for review and accepted states
✅ **Split support** - Splits work immediately (user could add categories during review)
✅ **Duplicate detection** - Can compare ExternalId across both imported and pending states

### Cons

❌ **Pollutes main table** - Every query must filter `Imported = true` to avoid review transactions
❌ **Index impact** - All indexes must account for Imported flag for performance
❌ **Data integrity risk** - Review transactions visible if filter forgotten (dev error potential)
❌ **Confusion** - "Imported" flag name is misleading (false = "in import review", not "not imported")
❌ **Reports affected** - All reports/analytics must filter out review transactions
❌ **Potential cascade issues** - If splits reference transactions, review transactions have splits that aren't "real" yet

## Option 2: Separate ImportReview Table

**Schema:**
```sql
CREATE TABLE [YoFi.V3.ImportReviewTransactions] (
    [Id] BIGINT PRIMARY KEY IDENTITY,
    [Key] UNIQUEIDENTIFIER NOT NULL,
    [TenantId] BIGINT NOT NULL,
    [Date] DATE NOT NULL,
    [Payee] NVARCHAR(200) NOT NULL,
    [Amount] DECIMAL(18,2) NOT NULL,
    [Source] NVARCHAR(200),
    [ExternalId] NVARCHAR(100),
    [Memo] NVARCHAR(1000),
    [DuplicateStatus] INT NOT NULL, -- 0=New, 1=ExactDuplicate, 2=PotentialDuplicate
    [DuplicateOfKey] UNIQUEIDENTIFIER, -- References existing transaction Key if duplicate
    [ImportedAt] DATETIME2 NOT NULL,
    [CreatedAt] DATETIME2 NOT NULL,
    FOREIGN KEY ([TenantId]) REFERENCES [YoFi.V3.Tenants]([Id])
);

CREATE INDEX IX_ImportReviewTransactions_Tenant
    ON [YoFi.V3.ImportReviewTransactions]([TenantId]);

CREATE INDEX IX_ImportReviewTransactions_ExternalId
    ON [YoFi.V3.ImportReviewTransactions]([TenantId], [ExternalId]);
```

**Entity Model:**
```csharp
/// <summary>
/// Status of a transaction in import review relative to existing transactions.
/// </summary>
public enum DuplicateStatus
{
    /// <summary>
    /// New transaction - no duplicates found. Selected by default for import.
    /// </summary>
    New = 0,

    /// <summary>
    /// Exact duplicate - same ExternalId/FITID and same data (Date, Amount, Payee).
    /// Deselected by default - user should not import.
    /// </summary>
    ExactDuplicate = 1,

    /// <summary>
    /// Potential duplicate - same ExternalId/FITID but different data.
    /// Highlighted and deselected by default - user should review carefully.
    /// </summary>
    PotentialDuplicate = 2
}

[Table("YoFi.V3.ImportReviewTransactions")]
public record ImportReviewTransaction : BaseTenantModel
{
    public DateOnly Date { get; set; }

    [Required]
    public string Payee { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    [MaxLength(200)]
    public string? Source { get; set; }

    [MaxLength(100)]
    public string? ExternalId { get; set; }

    [MaxLength(1000)]
    public string? Memo { get; set; }

    /// <summary>
    /// Duplicate detection status relative to existing transactions.
    /// </summary>
    public DuplicateStatus DuplicateStatus { get; set; } = DuplicateStatus.New;

    /// <summary>
    /// Key of existing transaction if this is a duplicate.
    /// </summary>
    public Guid? DuplicateOfKey { get; set; }

    /// <summary>
    /// When the import file was uploaded.
    /// </summary>
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Tenant? Tenant { get; set; }
}
```

**Query Patterns:**
```csharp
// Get accepted transactions (normal view) - NO FILTER NEEDED
var transactions = await context.Transactions
    .Where(t => t.TenantId == tenantId)
    .ToListAsync();

// Get pending import review transactions
var pendingImports = await context.ImportReviewTransactions
    .Where(t => t.TenantId == tenantId)
    .ToListAsync();

// Accept transactions - COPY and DELETE
var transaction = new Transaction
{
    Key = reviewTransaction.Key,
    TenantId = reviewTransaction.TenantId,
    Date = reviewTransaction.Date,
    Payee = reviewTransaction.Payee,
    Amount = reviewTransaction.Amount,
    Source = reviewTransaction.Source,
    ExternalId = reviewTransaction.ExternalId,
    Memo = reviewTransaction.Memo
};
context.Transactions.Add(transaction);
context.ImportReviewTransactions.Remove(reviewTransaction);
await context.SaveChangesAsync();
```

### Pros

✅ **Clean separation** - Main transaction queries unaffected by review state
✅ **No filter burden** - Reports, analytics, exports work without modifications
✅ **Performance** - Indexes on main table not affected by review data
✅ **Additional metadata** - Can store import-specific data (DuplicateStatus, DuplicateOfKey, ImportedAt)
✅ **Clear semantics** - Table name makes purpose obvious
✅ **Simpler splits** - Don't need to worry about splits on "not-yet-real" transactions
✅ **Bulk operations** - Easy to delete all pending imports: `DELETE FROM ImportReviewTransactions WHERE TenantId = X`

### Cons

❌ **Schema duplication** - Most fields duplicated between ImportReviewTransaction and Transaction
❌ **Code duplication** - Need separate DTOs, validators, queries for import review
❌ **Copy overhead** - Must copy data when accepting (though this is fast for individual records)
❌ **Two tables to maintain** - Schema changes to Transaction may need parallel changes to ImportReviewTransaction
❌ **Duplicate detection** - Must query both tables to check for duplicates
❌ **Key reuse** - Must ensure same Key used when copying from review to main table

## Option 3: Hybrid - Same Table with Separate DbSet View

**Schema:**
Same as Option 1 (add `Imported` flag)

**Entity Model:**
```csharp
public record Transaction : BaseTenantModel
{
    // All existing fields...
    public bool Imported { get; set; } = true;
}
```

**DbContext Configuration:**
```csharp
public class ApplicationDbContext : DbContext
{
    public DbSet<Transaction> Transactions => Set<Transaction>();

    // Filtered view of import review transactions
    public IQueryable<Transaction> ImportReviewTransactions =>
        Transactions.Where(t => t.Imported == false);
}
```

### Pros

✅ **Simpler than Option 1** - Encapsulates filter logic in DbContext
✅ **Code reuse** - Same entity model for both states
✅ **Less duplication than Option 2** - No separate entity/DTO needed

### Cons

❌ **Still pollutes main table** - Index and performance concerns remain
❌ **Filter enforcement** - Developers must remember to use Transactions (not include Imported=false)
❌ **Not a true abstraction** - IQueryable can still be misused

## Recommendation: Option 2 (Separate ImportReview Table)

**Rationale:**

1. **Clean Architecture** - Import review is a temporary staging area, not part of core transaction data
2. **Performance** - Main transaction table remains lean and fast
3. **Import-specific metadata** - DuplicateStatus and DuplicateOfKey are naturally modeled
4. **No hidden filters** - All transaction queries work without thinking about review state
5. **Safer** - Impossible to accidentally include review transactions in reports
6. **Clearer workflow** - Import → Review (separate table) → Accept (copy to main table) → Delete (from review)

**Trade-off Accepted:**

Yes, there's schema/code duplication, but:
- Import review is a bounded context with different concerns than main transactions
- The duplication is manageable (7-8 fields)
- The separation simplifies both codebases (import vs transactions)
- Copy operation on accept is negligible performance cost

### Implementation Approach

**Phase 1: Schema and Entity**
```csharp
// Migration
public partial class AddImportReviewTable : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "YoFi.V3.ImportReviewTransactions",
            columns: table => new
            {
                Id = table.Column<long>(nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                Key = table.Column<Guid>(nullable: false),
                TenantId = table.Column<long>(nullable: false),
                Date = table.Column<DateOnly>(nullable: false),
                Payee = table.Column<string>(maxLength: 200, nullable: false),
                Amount = table.Column<decimal>(type: "decimal(18, 2)", nullable: false),
                Source = table.Column<string>(maxLength: 200, nullable: true),
                ExternalId = table.Column<string>(maxLength: 100, nullable: true),
                Memo = table.Column<string>(maxLength: 1000, nullable: true),
                DuplicateStatus = table.Column<int>(nullable: false),
                DuplicateOfKey = table.Column<Guid>(nullable: true),
                ImportedAt = table.Column<DateTime>(nullable: false),
                CreatedAt = table.Column<DateTime>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ImportReviewTransactions", x => x.Id);
                table.ForeignKey(
                    name: "FK_ImportReviewTransactions_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "YoFi.V3.Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ImportReviewTransactions_Tenant",
            table: "YoFi.V3.ImportReviewTransactions",
            column: "TenantId");

        migrationBuilder.CreateIndex(
            name: "IX_ImportReviewTransactions_ExternalId",
            table: "YoFi.V3.ImportReviewTransactions",
            columns: new[] { "TenantId", "ExternalId" });
    }
}
```

**Phase 2: DTOs**
```csharp
// Result DTO for import review
public record ImportReviewTransactionDto(
    Guid Key,
    DateOnly Date,
    decimal Amount,
    string Payee,
    string? Memo,
    string? Source,
    string? ExternalId,
    DuplicateStatus DuplicateStatus,
    Guid? DuplicateOfKey
);
```

**Phase 3: Feature**
```csharp
public class ImportReviewFeature(
    ITenantProvider tenantProvider,
    IDataProvider dataProvider,
    IOFXParsingService ofxParser)
{
    public async Task<ImportReviewResult> ImportFileAsync(Stream fileStream, string fileName)
    {
        // 1. Parse OFX file
        var parseResult = await ofxParser.ParseAsync(fileStream, fileName);

        // 2. Detect duplicates
        var tenantId = await tenantProvider.GetCurrentTenantIdAsync();
        var existingTransactions = await dataProvider.GetTransactionsByTenantAsync(tenantId);
        var pendingReview = await dataProvider.GetImportReviewTransactionsByTenantAsync(tenantId);

        var reviewTransactions = new List<ImportReviewTransaction>();

        foreach (var parsed in parseResult.Transactions)
        {
            var duplicateStatus = DetectDuplicate(parsed, existingTransactions, pendingReview);

            reviewTransactions.Add(new ImportReviewTransaction
            {
                Key = Guid.NewGuid(),
                TenantId = tenantId,
                Date = parsed.Date,
                Payee = parsed.Payee,
                Amount = parsed.Amount,
                Source = parsed.Source,
                ExternalId = parsed.ExternalId,
                Memo = parsed.Memo,
                DuplicateStatus = duplicateStatus.Status,
                DuplicateOfKey = duplicateStatus.DuplicateOfKey,
                ImportedAt = DateTime.UtcNow
            });
        }

        // 3. Save to ImportReviewTransactions table
        await dataProvider.AddImportReviewTransactionsAsync(reviewTransactions);

        return new ImportReviewResult
        {
            TotalParsed = parseResult.Transactions.Count,
            NewCount = reviewTransactions.Count(t => t.DuplicateStatus == DuplicateStatus.New),
            ExactDuplicateCount = reviewTransactions.Count(t => t.DuplicateStatus == DuplicateStatus.ExactDuplicate),
            PotentialDuplicateCount = reviewTransactions.Count(t => t.DuplicateStatus == DuplicateStatus.PotentialDuplicate)
        };
    }

    public async Task<IReadOnlyCollection<ImportReviewTransactionDto>> GetPendingReviewAsync()
    {
        var tenantId = await tenantProvider.GetCurrentTenantIdAsync();
        return await dataProvider.GetImportReviewTransactionsByTenantAsync(tenantId);
    }

    public async Task AcceptTransactionsAsync(IReadOnlyCollection<Guid> keys)
    {
        var tenantId = await tenantProvider.GetCurrentTenantIdAsync();
        var reviewTransactions = await dataProvider.GetImportReviewTransactionsByKeysAsync(keys, tenantId);

        // Copy to main Transactions table
        var transactions = reviewTransactions.Select(r => new Transaction
        {
            Key = r.Key,  // IMPORTANT: Preserve Key for client-side tracking
            TenantId = r.TenantId,
            Date = r.Date,
            Payee = r.Payee,
            Amount = r.Amount,
            Source = r.Source,
            ExternalId = r.ExternalId,
            Memo = r.Memo,
            Splits = new List<Split>
            {
                // Create default split (single split with no category)
                new Split
                {
                    Key = Guid.NewGuid(),
                    Amount = r.Amount,
                    Order = 0
                }
            }
        }).ToList();

        await dataProvider.AddTransactionsAsync(transactions);

        // Delete from ImportReviewTransactions table
        await dataProvider.DeleteImportReviewTransactionsAsync(keys, tenantId);
    }

    public async Task DeletePendingReviewAsync(IReadOnlyCollection<Guid> keys)
    {
        var tenantId = await tenantProvider.GetCurrentTenantIdAsync();
        await dataProvider.DeleteImportReviewTransactionsAsync(keys, tenantId);
    }

    public async Task DeleteAllPendingReviewAsync()
    {
        var tenantId = await tenantProvider.GetCurrentTenantIdAsync();
        await dataProvider.DeleteAllImportReviewTransactionsAsync(tenantId);
    }
}
```

### Duplicate Detection Strategy

```csharp
private (DuplicateStatus Status, Guid? DuplicateOfKey) DetectDuplicate(
    TransactionEditDto parsed,
    IEnumerable<Transaction> existing,
    IEnumerable<ImportReviewTransaction> pending)
{
    // Strategy 1: Match by ExternalId (FITID)
    if (!string.IsNullOrWhiteSpace(parsed.ExternalId))
    {
        // Check existing transactions
        var existingMatch = existing.FirstOrDefault(t =>
            t.ExternalId == parsed.ExternalId);

        if (existingMatch != null)
        {
            // Found in main table - check if exact or potential duplicate
            bool isExact = existingMatch.Date == parsed.Date &&
                          existingMatch.Amount == parsed.Amount &&
                          existingMatch.Payee == parsed.Payee;

            return (isExact ? DuplicateStatus.ExactDuplicate : DuplicateStatus.PotentialDuplicate,
                   existingMatch.Key);
        }

        // Check pending import review
        var pendingMatch = pending.FirstOrDefault(t =>
            t.ExternalId == parsed.ExternalId);

        if (pendingMatch != null)
        {
            // Found in review table - treat as exact duplicate (no need to import twice)
            return (DuplicateStatus.ExactDuplicate, pendingMatch.Key);
        }
    }

    // Strategy 2: Match by (Date + Amount + Payee) hash
    // This handles cases where bank doesn't provide FITID
    var matchByFields = existing.FirstOrDefault(t =>
        t.Date == parsed.Date &&
        t.Amount == parsed.Amount &&
        t.Payee.Equals(parsed.Payee, StringComparison.OrdinalIgnoreCase));

    if (matchByFields != null)
    {
        // Found match - treat as exact duplicate
        return (DuplicateStatus.ExactDuplicate, matchByFields.Key);
    }

    // No duplicates found
    return (DuplicateStatus.New, null);
}
```

## Alternative Considered: Status Enum Instead of Boolean

Could use enum instead of boolean flag:
```csharp
public enum TransactionStatus
{
    Accepted = 0,
    PendingReview = 1
}

public TransactionStatus Status { get; set; } = TransactionStatus.Accepted;
```

**Why not recommended:**
- Still has all cons of Option 1 (table pollution, filter burden)
- Enum adds complexity without solving core issues
- If we ever need more states (Archived, Deleted, etc.), separate table is better foundation

## Migration Path from YoFi V1

If migrating from YoFi V1 with `Imported` flag:

1. Create new `ImportReviewTransactions` table
2. Migrate any `Imported=false` transactions to ImportReviewTransactions
3. Drop `Imported` column from Transactions table
4. Update all queries to use new table

**Migration SQL:**
```sql
-- Step 1: Create new table (see migration above)

-- Step 2: Copy pending review transactions
INSERT INTO [YoFi.V3.ImportReviewTransactions]
    (Key, TenantId, Date, Payee, Amount, Source, ExternalId, Memo, DuplicateStatus, ImportedAt, CreatedAt)
SELECT
    Key, TenantId, Date, Payee, Amount, Source, ExternalId, Memo,
    0 as DuplicateStatus, -- Default to New
    CreatedAt as ImportedAt,
    CreatedAt
FROM [YoFi.V3.Transactions]
WHERE Imported = 0;

-- Step 3: Delete pending review transactions from main table
DELETE FROM [YoFi.V3.Transactions] WHERE Imported = 0;

-- Step 4: Drop Imported column
ALTER TABLE [YoFi.V3.Transactions] DROP COLUMN Imported;
```

## Summary

**Recommendation:** Separate `ImportReviewTransactions` table (Option 2)

**Key Benefits:**
- Clean separation of concerns
- No impact on existing transaction queries
- Natural place for import-specific metadata
- Simpler to reason about and maintain

**Acceptable Trade-offs:**
- Some schema duplication (7-8 fields)
- Copy operation on accept (negligible performance cost)

**Next Steps:**
1. Create ImportReviewTransaction entity and migration
2. Create ImportReviewFeature with import/accept/delete operations
3. Implement duplicate detection logic
4. Design API endpoints for import workflow
