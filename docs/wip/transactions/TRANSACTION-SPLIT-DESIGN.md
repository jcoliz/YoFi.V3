# Transaction Split Design

## Overview

This document defines the database schema, indexing strategy, query patterns, and API design for implementing transaction splits in YoFi.V3. Transactions can be split across multiple categories, allowing users to categorize portions of a single transaction differently.

## Requirements Summary

### Core Concepts

1. **Transaction** remains the primary entity with properties: Date, Payee, Amount, Memo, Source
2. **Split** is a new entity representing a portion of a transaction with properties: Amount, Category, Memo
3. **Transaction.Amount** is authoritative (imported from bank or manually entered)
4. **Every transaction MUST have at least one split** - enforced by database and application
5. **Single-split transactions** hide complexity - user edits transaction directly, backend updates the single split
6. **Multi-split transactions** require explicit split management UI
7. **⚠️ CRITICAL: Unbalanced transactions are a significant error state** - Splits MUST sum to Transaction.Amount
   - Backend always calculates and returns `IsBalanced` flag
   - UI must prominently display warnings for unbalanced transactions
   - UI should make it difficult to ignore unbalanced state (visual indicators, modal warnings, etc.)
   - Goal: Guide user to fix balance discrepancies immediately, not later

### Key Design Decisions from Discussion

- **Source property**: Stays at Transaction level (entire transaction came from one import source)
- **At least one split**: Database enforces via check constraint, application validates at save time
- **Category property**: NOT NULL with empty string for uncategorized (better performance than NULL, consistent with Payee pattern)
- **Split primary key**: Use Guid Key for consistency with Transaction pattern, enables future API flexibility
- **API design**: Individual split CRUD operations (POST/PUT/DELETE) are primary pattern; atomic replacement also supported
- **Amount behavior**: Transaction.Amount is editable (user can correct mistakes on manual entry); splits can total to different amount (warning state for user)

## Database Schema

### Split Entity (New)

```csharp
/// <summary>
/// Represents a portion of a transaction allocated to a specific category.
/// </summary>
/// <remarks>
/// Splits allow transactions to be categorized across multiple categories.
/// Every transaction must have at least one split. The sum of split amounts
/// should match the transaction amount, but this is a validation warning
/// rather than a hard constraint (the imported transaction amount is authoritative).
/// </remarks>
[Table("YoFi.V3.Splits")]
public record Split : BaseModel
{
    /// <summary>
    /// Foreign key to the parent transaction
    /// </summary>
    public long TransactionId { get; set; }

    /// <summary>
    /// Amount allocated to this category
    /// </summary>
    /// <remarks>
    /// Can be negative for credits/refunds. The sum of all splits for a transaction
    /// should match Transaction.Amount, but this is not enforced at the database level.
    /// </remarks>
    public decimal Amount { get; set; }

    /// <summary>
    /// Category for this split
    /// </summary>
    /// <remarks>
    /// Empty string indicates uncategorized. Categories are free-form text
    /// in YoFi (no separate Category table) to support flexible user workflows.
    /// </remarks>
    [Required]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Optional memo specific to this split
    /// </summary>
    /// <remarks>
    /// Split-level memo for notes about this specific categorization.
    /// Most splits won't have a memo (transaction memo is more common).
    /// </remarks>
    public string? Memo { get; set; }

    /// <summary>
    /// Display order for splits within a transaction
    /// </summary>
    /// <remarks>
    /// Zero-based index for stable ordering in UI. Users can reorder splits,
    /// and this preserves their preference across queries.
    /// </remarks>
    public int Order { get; set; }

    // Navigation properties
    public virtual Transaction? Transaction { get; set; }
}
```

### Transaction Entity (Modified)

```csharp
/// <summary>
/// A financial transaction record tied to a specific tenant
/// </summary>
/// <remarks>
/// Transactions represent financial events imported from bank/credit card sources
/// or entered manually. Each transaction has one or more splits for categorization.
/// </remarks>
[Table("YoFi.V3.Transactions")]
public record Transaction : BaseTenantModel
{
    /// <summary>
    /// Date the transaction occurred
    /// </summary>
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>
    /// Recipient or payee of the transaction
    /// </summary>
    [Required]
    public string Payee { get; set; } = string.Empty;

    /// <summary>
    /// Total amount of the transaction (authoritative imported value)
    /// </summary>
    /// <remarks>
    /// This is the amount imported from the bank/source and should not be changed.
    /// Splits may total to a different amount (validation warning for user).
    /// Note that YoFi is single-currency for now, so no currency code is stored.
    /// </remarks>
    public decimal Amount { get; set; } = 0;

    /// <summary>
    /// Optional memo for the entire transaction
    /// </summary>
    public string? Memo { get; set; }

    /// <summary>
    /// Source of the transaction (e.g., "MegaBankCorp Checking 0123456789-00", "Manual Entry")
    /// </summary>
    public string? Source { get; set; }

    // Navigation properties
    public virtual Tenant? Tenant { get; set; }

    /// <summary>
    /// Splits categorizing this transaction
    /// </summary>
    /// <remarks>
    /// Every transaction must have at least one split. In the most common case
    /// (single split), the UI hides split complexity and edits the split directly.
    /// </remarks>
    public virtual ICollection<Split> Splits { get; set; } = new List<Split>();
}
```

### Entity Framework Configuration

```csharp
// Configure Split entity
modelBuilder.Entity<Split>(entity =>
{
    // Unique Guid key (standard pattern)
    entity.HasIndex(e => e.Key)
        .IsUnique();

    // Foreign key relationship to Transaction
    entity.HasOne(s => s.Transaction)
        .WithMany(t => t.Splits)
        .HasForeignKey(s => s.TransactionId)
        .OnDelete(DeleteBehavior.Cascade); // Delete splits when transaction deleted

    // Index on TransactionId for efficient split queries
    entity.HasIndex(s => s.TransactionId);

    // Composite index on TransactionId + Order for ordered split retrieval
    entity.HasIndex(s => new { s.TransactionId, s.Order });

    // Index on Category for category-based queries and reports
    entity.HasIndex(s => s.Category);

    // Category is required (empty string for uncategorized)
    entity.Property(s => s.Category)
        .IsRequired()
        .HasMaxLength(100);

    // Memo is optional
    entity.Property(s => s.Memo)
        .HasMaxLength(500);

    // Amount precision for currency
    entity.Property(s => s.Amount)
        .HasPrecision(18, 2);
});

// Update Transaction entity configuration
modelBuilder.Entity<Transaction>(entity =>
{
    // ... existing configuration ...

    // Add Memo property
    entity.Property(t => t.Memo)
        .HasMaxLength(500);

    // Add Source property
    entity.Property(t => t.Source)
        .HasMaxLength(100);

    // Amount precision for currency
    entity.Property(t => t.Amount)
        .HasPrecision(18, 2);
});
```

### Database Check Constraint (SQLite)

SQLite doesn't support check constraints that reference other tables. Instead, we'll enforce the "at least one split" rule in the application layer and rely on cascade delete to prevent orphaned transactions.

**Application-level validation**: Before `SaveChangesAsync()`, verify that every transaction has at least one split.

## Indexing Strategy

### Split Indexes

1. **`IX_Splits_Key` (Unique)** - Standard Guid key lookup
   - **Purpose**: Lookup split by external API key
   - **Queries**: `WHERE Key = @key`

2. **`IX_Splits_TransactionId`** - Foreign key index
   - **Purpose**: Retrieve all splits for a transaction
   - **Queries**: `WHERE TransactionId = @transactionId`

3. **`IX_Splits_TransactionId_Order`** - Composite index
   - **Purpose**: Retrieve splits for a transaction in display order
   - **Queries**: `WHERE TransactionId = @transactionId ORDER BY Order`
   - **Benefits**: Covering index for ordered split retrieval (no table lookup needed)

4. **`IX_Splits_Category`** - Category index
   - **Purpose**: Category-based reports and filtering
   - **Queries**: `WHERE Category = @category` or `GROUP BY Category`
   - **Benefits**: Enables efficient category summaries

### Transaction Indexes (Existing + New)

1. **`IX_Transactions_Key` (Unique)** - Existing
2. **`IX_Transactions_TenantId`** - Existing
3. **`IX_Transactions_TenantId_Date`** - Existing (composite for date range queries)
4. **`IX_Transactions_TenantId_Payee`** - NEW (for payee filtering)
   - **Purpose**: Filter transactions by payee within tenant
   - **Queries**: `WHERE TenantId = @tenantId AND Payee = @payee`

## Query Patterns

### Pattern 1: Get Transactions (List View with Indicators)

Most common query - list view with split indicators and balance check.

```csharp
// Query with split indicators and balance check (standard list view)
var transactions = await context.Transactions
    .AsNoTracking()
    .Where(t => t.TenantId == tenantId)
    .Where(t => t.Date >= fromDate && t.Date <= toDate)
    .OrderByDescending(t => t.Date)
    .Select(t => new TransactionResultDto(
        t.Key,
        t.Date,
        t.Amount,
        t.Payee,
        HasMultipleSplits: t.Splits.Count > 1,
        SingleSplitCategory: t.Splits.Count == 1 ? t.Splits.First().Category : null,
        IsBalanced: t.Splits.Sum(s => s.Amount) == t.Amount
    ))
    .ToListAsync();
```

**Index used**: `IX_Transactions_TenantId_Date`, `IX_Splits_TransactionId`
**Performance**: Fast - EF Core generates efficient SQL with subquery for calculations

### Pattern 2: Get Transaction with Splits (Detail View)

Single transaction with all splits loaded.

```csharp
// Query WITH splits (detail view or edit)
var transaction = await context.Transactions
    .AsNoTracking()
    .Include(t => t.Splits.OrderBy(s => s.Order)) // Ordered splits
    .Where(t => t.TenantId == tenantId && t.Key == transactionKey)
    .SingleOrDefaultAsync();

// Map to DTO
var dto = new TransactionDetailDto(
    transaction.Key,
    transaction.Date,
    transaction.Amount,
    transaction.Payee,
    transaction.Memo,
    transaction.Source,
    Splits: transaction.Splits.Select(s => new SplitResultDto(
        s.Key,
        s.Amount,
        s.Category,
        s.Memo
    )).ToList()
);
```

**Indexes used**:
- `IX_Transactions_TenantId` + `IX_Transactions_Key`
- `IX_Splits_TransactionId_Order` (for ordered splits)

**Performance**: One query with single join, efficient due to covering index

### Pattern 3: Filter by Category

Find transactions that have splits in a specific category.

```csharp
var transactions = await context.Transactions
    .AsNoTracking()
    .Where(t => t.TenantId == tenantId)
    .Where(t => t.Splits.Any(s => s.Category == category))
    .OrderByDescending(t => t.Date)
    .ToListAsync();
```

**Indexes used**: `IX_Transactions_TenantId`, `IX_Splits_Category`
**Performance**: Efficient with category index

### Pattern 4: Category Report

Sum amounts by category across all transactions.

```csharp
var categoryTotals = await context.Splits
    .Where(s => s.Transaction!.TenantId == tenantId)
    .Where(s => s.Transaction!.Date >= fromDate && s.Transaction!.Date <= toDate)
    .GroupBy(s => s.Category)
    .Select(g => new CategoryTotalDto(
        Category: g.Key,
        Total: g.Sum(s => s.Amount),
        Count: g.Count()
    ))
    .OrderByDescending(ct => ct.Total)
    .ToListAsync();
```

**Indexes used**: `IX_Splits_Category`, `IX_Transactions_TenantId_Date`
**Performance**: Efficient - category index enables fast grouping

### Pattern 5: Create Transaction with Single Split (Backend Handles Complexity)

Most common creation pattern - transaction with one split (categorized or uncategorized).

```csharp
var transaction = new Transaction
{
    TenantId = tenantId,
    Date = dto.Date,
    Payee = dto.Payee,
    Amount = dto.Amount,
    Memo = dto.Memo,
    Source = dto.Source,
    Splits = new List<Split>
    {
        new Split
        {
            Amount = dto.Amount,
            Category = dto.Category ?? string.Empty, // Use provided category or empty string
            Memo = null,
            Order = 0
        }
    }
};

context.Transactions.Add(transaction);
await context.SaveChangesAsync();
```

**Database operations**: Single transaction insert + single split insert
**Note**: Backend handles split-making complexity. UI can optionally provide category on creation. User adds more splits later via POST splits endpoint if needed.

### Pattern 6: Update Transaction Properties (Returns Balance State)

Update transaction properties without affecting splits. Returns balance state since Amount changes can cause imbalance.

```csharp
// Load existing transaction with splits to calculate balance
var transaction = await context.Transactions
    .Include(t => t.Splits)
    .Where(t => t.TenantId == tenantId && t.Key == transactionKey)
    .SingleOrDefaultAsync();

if (transaction == null)
    throw new TransactionNotFoundException(transactionKey);

// Update transaction properties
transaction.Payee = dto.Payee;
transaction.Date = dto.Date;
transaction.Memo = dto.Memo;
transaction.Amount = dto.Amount; // Editable to correct manual entry mistakes
// Note: Source is NOT updated (imported source is readonly)

await context.SaveChangesAsync();

// Calculate balance state (critical if Amount was changed)
var splitsTotal = transaction.Splits.Sum(s => s.Amount);
var isBalanced = splitsTotal == transaction.Amount;

return new TransactionUpdateResultDto(
    Key: transaction.Key,
    Amount: transaction.Amount,
    SplitsTotal: splitsTotal,
    IsBalanced: isBalanced
);
```

**Database operations**: Single transaction update
**Performance**: Fast - direct update, balance calculated in memory
**Note**: Returns balance state so UI can immediately warn if Amount change caused imbalance

### Pattern 7: Add Splits to Transaction (Bulk)

Add one or more splits to existing transaction. Handles both single split and bulk split additions.

```csharp
// Load existing transaction to get next order number
var transaction = await context.Transactions
    .Include(t => t.Splits)
    .Where(t => t.TenantId == tenantId && t.Key == transactionKey)
    .SingleOrDefaultAsync();

if (transaction == null)
    throw new TransactionNotFoundException(transactionKey);

// Get starting order number
var nextOrder = transaction.Splits.Any() ? transaction.Splits.Max(s => s.Order) + 1 : 0;

// Create new splits
var newSplits = dtos.Select((dto, index) => new Split
{
    TransactionId = transaction.Id,
    Amount = dto.Amount,
    Category = dto.Category,
    Memo = dto.Memo,
    Order = nextOrder + index
}).ToList();

context.Splits.AddRange(newSplits);
await context.SaveChangesAsync();

return newSplits.Select(s => new SplitResultDto(s.Key, s.Amount, s.Category, s.Memo)).ToList();
```

**Database operations**: N split inserts (batched by EF Core)
**Performance**: Fast - batched insert, no full transaction reload
**Note**: API accepts single SplitEditDto or collection - both use this pattern

### Pattern 8: Update Individual Split

User edits amount, category, or memo on a single split. Returns balance state after update.

```csharp
// Load split with transaction and all splits for balance calculation
var split = await context.Splits
    .Include(s => s.Transaction)
        .ThenInclude(t => t!.Splits)
    .Where(s => s.Key == splitKey)
    .SingleOrDefaultAsync();

if (split == null || split.Transaction?.TenantId != tenantId)
    throw new SplitNotFoundException(splitKey);

// Validate transaction ownership
if (split.Transaction.Key != transactionKey)
    throw new InvalidOperationException("Split does not belong to specified transaction");

// Update split properties
split.Amount = dto.Amount;
split.Category = dto.Category;
split.Memo = dto.Memo;

await context.SaveChangesAsync();

// Calculate updated balance state
var splitsTotal = split.Transaction.Splits.Sum(s => s.Amount);
var isBalanced = splitsTotal == split.Transaction.Amount;

return new SplitOperationResultDto(
    Splits: new[] { new SplitResultDto(split.Key, split.Amount, split.Category, split.Memo) },
    TransactionAmount: split.Transaction.Amount,
    SplitsTotal: splitsTotal,
    IsBalanced: isBalanced
);
```

**Database operations**: Single split update
**Performance**: Fast - direct update, balance calculated in memory

### Pattern 9: Delete Individual Split

User removes a split from transaction. Returns balance state after deletion.

```csharp
// Load split with transaction and sibling splits for validation and balance
var split = await context.Splits
    .Include(s => s.Transaction)
        .ThenInclude(t => t!.Splits)
    .Where(s => s.Key == splitKey)
    .SingleOrDefaultAsync();

if (split == null || split.Transaction?.TenantId != tenantId)
    throw new SplitNotFoundException(splitKey);

// Validate transaction ownership
if (split.Transaction.Key != transactionKey)
    throw new InvalidOperationException("Split does not belong to specified transaction");

// Validation: Cannot delete last split
if (split.Transaction.Splits.Count == 1)
    throw new ValidationException("Cannot delete the last split - transaction must have at least one split");

// Calculate balance state AFTER deletion
var remainingSplits = split.Transaction.Splits.Where(s => s.Id != split.Id).ToList();
var splitsTotal = remainingSplits.Sum(s => s.Amount);
var isBalanced = splitsTotal == split.Transaction.Amount;

context.Splits.Remove(split);
await context.SaveChangesAsync();

return new SplitOperationResultDto(
    Splits: null, // Split was deleted, no splits to return
    TransactionAmount: split.Transaction.Amount,
    SplitsTotal: splitsTotal,
    IsBalanced: isBalanced,
    RemainingCount: remainingSplits.Count
);
```

**Database operations**: Single split delete
**Validation**: Prevents deleting last split (business rule)
**Performance**: Balance calculated before deletion for immediate UI feedback

## DTO Design

### TransactionResultDto (List View)

Used for list views where split details aren't shown, but split indicators are needed.

```csharp
/// <summary>
/// Transaction data for list views (output-only).
/// </summary>
/// <param name="Key">Unique identifier for the transaction</param>
/// <param name="Date">Date the transaction occurred</param>
/// <param name="Amount">Transaction amount (authoritative imported value)</param>
/// <param name="Payee">Recipient or payee of the transaction</param>
/// <param name="HasMultipleSplits">True if transaction has more than one split</param>
/// <param name="SingleSplitCategory">Category of the single split (if HasMultipleSplits is false)</param>
/// <param name="IsBalanced">True if splits total matches transaction amount</param>
/// <remarks>
/// List view DTO that provides UI hints:
/// - HasMultipleSplits: Indicates whether to show split indicator icon (allows inline category editing if false)
/// - SingleSplitCategory: For single-split transactions, shows category directly (null if multiple splits)
/// - IsBalanced: CRITICAL - False indicates splits don't add up to transaction amount (serious data quality issue)
///
/// IsBalanced is always calculated and returned to highlight unbalanced transactions in the list view.
/// Unbalanced transactions should be visually flagged to prompt user correction.
///
/// For full transaction details with splits, use <see cref="TransactionDetailDto"/>.
/// </remarks>
public record TransactionResultDto(
    Guid Key,
    DateOnly Date,
    decimal Amount,
    string Payee,
    bool HasMultipleSplits,
    string? SingleSplitCategory,
    bool IsBalanced
);
```

### TransactionDetailDto (Detail/Edit View - With Splits)

Used for detail views and editing.

```csharp
/// <summary>
/// Complete transaction data including splits (output-only).
/// </summary>
/// <param name="Key">Unique identifier for the transaction</param>
/// <param name="Date">Date the transaction occurred</param>
/// <param name="Amount">Transaction amount (authoritative imported value)</param>
/// <param name="Payee">Recipient or payee of the transaction</param>
/// <param name="Memo">Optional memo for the transaction</param>
/// <param name="Source">Source of the transaction (e.g., "MegaBankCorp Checking 0123456789-00")</param>
/// <param name="Splits">Collection of splits categorizing this transaction</param>
/// <param name="SplitsTotal">Calculated sum of all split amounts</param>
/// <param name="IsBalanced">True if SplitsTotal matches Amount</param>
/// <remarks>
/// Complete transaction DTO including all splits. Used for detail views and editing.
///
/// SplitsTotal and IsBalanced help UI show validation warnings when splits
/// don't match the imported transaction amount. The imported Amount is authoritative
/// and never changes, but splits can total to a different amount (user's choice).
/// </remarks>
public record TransactionDetailDto(
    Guid Key,
    DateOnly Date,
    decimal Amount,
    string Payee,
    string? Memo,
    string? Source,
    IReadOnlyCollection<SplitResultDto> Splits,
    decimal SplitsTotal,
    bool IsBalanced
);
```

### TransactionEditDto (Create/Update Input)

Input DTO for creating/updating transactions.

```csharp
/// <summary>
/// Transaction data for creating or updating transactions (input DTO).
/// </summary>
/// <param name="Date">Date the transaction occurred</param>
/// <param name="Amount">Transaction amount (required for creation, readonly after creation)</param>
/// <param name="Payee">Recipient or payee of the transaction</param>
/// <param name="Memo">Optional memo for the transaction</param>
/// <param name="Source">Source of the transaction (e.g., "MegaBankCorp Checking 0123456789-00", "Manual Entry")</param>
/// <param name="Splits">Collection of splits categorizing this transaction</param>
/// <remarks>
/// Input DTO with validation attributes. Used for both creating new transactions
/// and updating existing transactions.
///
/// For updates:
/// - Amount and Source are readonly after creation (imported values)
/// - Date, Payee, Memo, and Splits can be modified
/// - At least one split is required
///
/// Validation rules:
/// - Date: Must be within 50 years in the past and 5 years in the future
/// - Amount: Must be non-zero (for creation only)
/// - Payee: Required, cannot be empty or whitespace, max 200 characters
/// - Splits: At least one split required; each split must have valid Amount and Category
/// </remarks>
public record TransactionEditDto(
    [DateRange(50, 5, ErrorMessage = "Transaction date must be within 50 years in the past and 5 years in the future")]
    DateOnly Date,

    [Range(typeof(decimal), "-999999999", "999999999", ErrorMessage = "Amount must be a valid value")]
    decimal Amount,

    [Required(ErrorMessage = "Payee is required")]
    [NotWhiteSpace(ErrorMessage = "Payee cannot be empty")]
    [MaxLength(200, ErrorMessage = "Payee cannot exceed 200 characters")]
    string Payee,

    [MaxLength(500, ErrorMessage = "Memo cannot exceed 500 characters")]
    string? Memo,

    [MaxLength(100, ErrorMessage = "Source cannot exceed 100 characters")]
    string? Source,

    [MinLength(1, ErrorMessage = "Transaction must have at least one split")]
    IReadOnlyCollection<SplitEditDto> Splits
);
```

### SplitResultDto (Output)

Output DTO for split data.

```csharp
/// <summary>
/// Split data returned from queries (output-only).
/// </summary>
/// <param name="Key">Unique identifier for the split</param>
/// <param name="Amount">Amount allocated to this category</param>
/// <param name="Category">Category for this split (empty string for uncategorized)</param>
/// <param name="Memo">Optional memo specific to this split</param>
/// <remarks>
/// Output DTO for split data. Order is implicitly defined by collection order.
/// </remarks>
public record SplitResultDto(
    Guid Key,
    decimal Amount,
    string Category,
    string? Memo
);
```

### SplitEditDto (Input)

Input DTO for split data.

```csharp
/// <summary>
/// Split data for creating or updating splits (input DTO).
/// </summary>
/// <param name="Amount">Amount allocated to this category</param>
/// <param name="Category">Category for this split (empty string for uncategorized)</param>
/// <param name="Memo">Optional memo specific to this split</param>
/// <remarks>
/// Input DTO with validation attributes. Used within <see cref="TransactionEditDto"/>
/// and for individual split operations (POST/PUT).
///
/// Validation rules:
/// - Amount: Must be non-zero
/// - Category: Max 100 characters (empty string allowed for uncategorized)
/// - Memo: Optional, max 500 characters
/// </remarks>
public record SplitEditDto(
    [Range(typeof(decimal), "-999999999", "999999999", ErrorMessage = "Split amount must be a valid value")]
    decimal Amount,

    [MaxLength(100, ErrorMessage = "Category cannot exceed 100 characters")]
    string Category,

    [MaxLength(500, ErrorMessage = "Memo cannot exceed 500 characters")]
    string? Memo
);
```

### SplitReorderDto (Input)

Input DTO for reordering splits.

```csharp
/// <summary>
/// Data for reordering splits within a transaction.
/// </summary>
/// <param name="Splits">Array of split keys with new order values</param>
/// <remarks>
/// Used by PATCH endpoint to update split display order.
/// Typically called after user drag-and-drop in UI.
/// </remarks>
public record SplitReorderDto(
    IReadOnlyCollection<SplitOrderDto> Splits
);

/// <summary>
/// Individual split order assignment.
/// </summary>
/// <param name="Key">Split identifier</param>
/// <param name="Order">New zero-based order value</param>
public record SplitOrderDto(
    Guid Key,
    int Order
);
```

## API Endpoints

### Existing Endpoints (Updated to Support Splits)

#### GET /api/tenant/{tenantKey}/transactions

Returns list of transactions with split indicators and balance check.

**Response**: `IReadOnlyCollection<TransactionResultDto>`

**Query parameters**:
- `fromDate` (optional): Filter by start date
- `toDate` (optional): Filter by end date

**DTO includes**:
- `HasMultipleSplits`: Boolean flag for UI to show split indicator
- `SingleSplitCategory`: Category for single-split transactions (for inline display)
- `IsBalanced`: **CRITICAL** - False indicates splits don't match transaction amount (data quality issue requiring user attention)

#### GET /api/tenant/{tenantKey}/transactions/{transactionKey}

Returns single transaction WITH splits (detail view).

**Response**: `TransactionDetailDto`

#### POST /api/tenant/{tenantKey}/transactions

Creates new transaction with single uncategorized split (backend handles split complexity).

**Request body**: `TransactionCreateDto` (Amount, Payee, Date, optional Memo/Source/Category)
**Response**: `TransactionDetailDto` (201 Created)

**Behavior**:
- Backend automatically creates single split: Amount = transaction amount, Category = provided category or empty string
- If category provided in request, assign to the split; otherwise split is uncategorized
- User can add more splits later via POST splits endpoint
- **Backend handles split-making complexity** - UI doesn't need to know about splits on creation

**Validation**:
- All transaction property validation rules (Date, Amount, Payee)

#### PUT /api/tenant/{tenantKey}/transactions/{transactionKey}

Updates transaction properties and returns balance state (Amount changes can cause imbalance).

**Request body**: `TransactionEditDto` (no splits array)
**Response**: `TransactionUpdateResultDto` (200 OK)

**Response includes**:
- Updated transaction key
- Updated amount
- Sum of all splits
- **IsBalanced flag** - Critical when Amount is edited

**Behavior**:
- Updates transaction properties (Date, Payee, Memo, Amount)
- **Does NOT update Source** (imported source is readonly, not in DTO)
- **Does NOT modify splits** - use individual split endpoints to manage splits
- Editing Amount does NOT automatically adjust splits (creates imbalance if splits don't match new amount)
- **Returns balance state** so UI can immediately warn if Amount change caused imbalance

#### DELETE /api/tenant/{tenantKey}/transactions/{transactionKey}

Deletes transaction and all splits (cascade delete).

**Response**: `204 No Content`

### Split Management Endpoints (Primary Editing Pattern)

Individual split operations are the **most common editing pattern**. Users typically:
1. Create transaction with single split
2. Add splits to existing transaction
3. Edit individual splits to adjust amounts/categories
4. Delete splits when correcting mistakes

#### GET /api/tenant/{tenantKey}/transactions/{transactionKey}/splits

Gets all splits for a transaction.

**Response**: `IReadOnlyCollection<SplitResultDto>` (200 OK)

**Returns splits in Order sequence**. Used when editing splits in the UI.

#### POST /api/tenant/{tenantKey}/transactions/{transactionKey}/splits

Adds one or more splits to existing transaction.

**Request body**: `SplitEditDto` (single) OR `IReadOnlyCollection<SplitEditDto>` (bulk)
**Response**: `SplitOperationResultDto` - 201 Created

**Response includes**:
- Created split(s) with assigned Key(s)
- Updated transaction balance state (IsBalanced flag)
- SplitsTotal (sum of all split amounts after addition)

**Behavior**:
- Accepts single split or array of splits
- Assigns Order = max(existing Order) + 1, +2, +3, etc.
- Transaction must exist and user must have Editor role
- Returns created split(s) AND updated balance state

#### PUT /api/tenant/{tenantKey}/transactions/{transactionKey}/splits/{splitKey}

Updates a specific split's amount, category, or memo.

**Request body**: `SplitEditDto`
**Response**: `SplitOperationResultDto` - 200 OK

**Response includes**:
- Updated split data
- Updated transaction balance state (IsBalanced flag)
- SplitsTotal (sum of all split amounts after update)

**Validation**:
- Split must belong to specified transaction (security check)
- All `SplitEditDto` validation rules apply

#### DELETE /api/tenant/{tenantKey}/transactions/{transactionKey}/splits/{splitKey}

Deletes a specific split.

**Response**: `SplitOperationResultDto` - 200 OK

**Response includes**:
- Updated transaction balance state (IsBalanced flag after deletion)
- SplitsTotal (sum of remaining split amounts)
- Remaining splits count

**Validation**:
- Cannot delete the last split (transaction must have at least one)
- Returns `400 Bad Request` with ProblemDetails if attempting to delete last split
- Split must belong to specified transaction (security check)

#### PATCH /api/tenant/{tenantKey}/transactions/{transactionKey}/splits/reorder

Reorders splits within a transaction.

**Request body**: `SplitReorderDto` - array of `{ Key: Guid, Order: int }`
**Response**: `204 No Content`

**Behavior**:
- Updates Order property for specified splits
- Used when user drags-and-drops splits in UI
- All specified splits must belong to the transaction

## Migration Strategy

### Phase 1: Database Schema Changes

1. **Clear existing transaction data** (app is new, no relevant data to preserve)
2. **Create Split table** with all indexes
3. **Add Memo and Source columns to Transaction table**

### Phase 2: Update Application Code

1. **Create Split entity** class (inherits from BaseModel)
2. **Update Transaction entity**:
   - Add Splits navigation collection
   - Add Memo and Source properties
   - Keep Amount property (authoritative value)
3. **Update EF Core configuration**:
   - Configure Split entity (indexes, relationships, cascade delete)
   - Update Transaction entity configuration
4. **Create new DTOs**:
   - TransactionCreateDto, TransactionEditDto, TransactionUpdateResultDto
   - SplitEditDto, SplitResultDto, SplitOperationResultDto
5. **Update TransactionsFeature** to work with splits:
   - Pattern 5: Create transaction with single split
   - Pattern 6: Update transaction properties
   - Pattern 7, 8, 9: Split CRUD operations
6. **Update TransactionsController** to return new DTOs with balance state

### Phase 3: Frontend Updates (Separate Task)

1. Update transaction list view to show split indicators and balance warnings
2. Update transaction detail view to show splits with balance state
3. Add split editor UI with immediate balance feedback
4. Implement prominent warnings for unbalanced transactions
5. Add category reports (primary use case)

## Answers to Original Questions

### Question 1: HasSplits Flag in DTO?

**Answer**: YES - `TransactionResultDto` (primary list view DTO) includes `HasMultipleSplits`, `SingleSplitCategory`, and `IsBalanced`.

**Benefits**:
- UI can show indicator icon when `HasMultipleSplits` is true
- UI can decide whether to allow inline category editing (single split) or require split editor (multiple splits)
- Includes `SingleSplitCategory` for convenience when displaying single-split transactions
- **CRITICAL**: `IsBalanced` flag highlights unbalanced transactions (serious data quality issue requiring user correction)

**Implementation**: Always calculated and returned in list view (no query parameter needed).

### Question 2: Should Splits Have a Guid Key?

**Answer**: YES - Use Guid Key for consistency and future flexibility.

**Rationale**:
- Consistent with Transaction pattern (all entities have Guid Key for external API access)
- Enables future individual split endpoints if needed (POST/PUT/DELETE on specific splits)
- No downside: Guid is indexed, lookup is fast
- API endpoints can still use composite path: `/api/tenant/{key}/transactions/{key}/splits/{key}`

**Not recommended**: Exposing bigint Id externally breaks API consistency pattern established by Transaction.

### Question 3: Atomic vs. Individual Split Operations?

**Answer**: **Individual split operations** are the primary pattern (most common user workflow).

**Rationale**:
- User workflow: Create transaction → Add splits → Edit individual splits → Delete splits
- More natural UX: Edit one split at a time without loading/saving entire transaction
- Better performance: Single split updates don't reload all splits
- Simpler frontend state management: Only track split being edited

**Atomic replacement** is still available for bulk operations, imports, and scenarios where replacing all splits is more efficient.

**Initial implementation**: Include both patterns - individual split CRUD (POST/PUT/DELETE) and atomic replacement (PUT transaction with full splits array).

## Performance Considerations

### Index Coverage

- **List view queries**: Covered by `IX_Transactions_TenantId_Date` (no split access needed)
- **Detail view queries**: Covered by `IX_Transactions_Key` + `IX_Splits_TransactionId_Order`
- **Category queries**: Covered by `IX_Splits_Category` + `IX_Transactions_TenantId_Date`

### Query Optimization

- **Avoid N+1**: Use `.Include(t => t.Splits)` when loading transactions with splits
- **Projection**: Use `.Select()` to project to DTOs (avoid loading full entities when not needed)
- **AsNoTracking**: Always use for read-only queries (no change tracking overhead)

### Expected Query Patterns

1. **Most common**: Category reports and summaries (efficient with category index) - Core purpose of personal finance tracking
2. **Very common**: List transactions with split indicators (fast, subquery for balance check)
3. **Common**: Get single transaction WITH splits (single join, efficient indexes)

### Scaling Considerations

- **Typical transaction**: 1 split (90%+ of cases)
- **Split transactions**: 2-5 splits (9% of cases)
- **Complex transactions**: 6+ splits (<1% of cases)

**Result**: Split table will be ~1.1x size of transaction table (minimal overhead)

## Validation Rules

### Transaction Validation

1. **At least one split required** - Enforced before `SaveChangesAsync()`
2. **Date range** - Within 50 years past, 5 years future
3. **Payee required** - Not null, not whitespace, max 200 chars
4. **Amount readonly** - After creation, Amount cannot be changed (imported value)
5. **Source readonly** - After creation, Source cannot be changed (imported value)

### Split Validation

1. **Amount non-zero** - Business rule (validation attribute)
2. **Category max length** - 100 characters (empty string allowed)
3. **Memo max length** - 500 characters (optional)

### Balance Validation (Warning Only)

- **Calculate**: `SplitsTotal = Splits.Sum(s => s.Amount)`
- **Compare**: `IsBalanced = (SplitsTotal == Transaction.Amount)`
- **UI behavior**: Show warning icon/message when `!IsBalanced`
- **Backend behavior**: Allow saving unbalanced transactions (user's choice to resolve)

## Testing Strategy

### Unit Tests (Application Layer)

Test all TransactionsFeature methods with the following patterns from the existing [`tests/Unit/Tests/TransactionsTests.cs`](tests/Unit/Tests/TransactionsTests.cs:1):

**Transaction Creation with Split**:
- `AddTransactionAsync_ValidTransaction_CreatesTransactionWithSingleSplit()` - Verify single split created automatically
- `AddTransactionAsync_WithCategory_CreatesSplitWithCategory()` - Category parameter flows to split
- `AddTransactionAsync_WithoutCategory_CreatesSplitWithEmptyCategory()` - Default uncategorized behavior
- `AddTransactionAsync_ValidTransaction_SplitAmountMatchesTransaction()` - Balance validation on creation

**Transaction Updates (Properties Only)**:
- `UpdateTransactionAsync_UpdatesProperties_PreservesSplits()` - Splits unchanged by transaction update
- `UpdateTransactionAsync_AmountChanged_ReturnsBalanceState()` - Verify TransactionUpdateResultDto includes IsBalanced
- `UpdateTransactionAsync_AmountChanged_DoesNotAdjustSplits()` - Editing Amount creates imbalance if splits don't match

**Split CRUD Operations**:
- `AddSplitAsync_ValidSplit_AddsToTransaction()` - Single split addition
- `AddSplitAsync_MultipleSplits_AddsAllWithCorrectOrder()` - Bulk split addition with order assignment
- `AddSplitAsync_ReturnsBalanceState()` - Verify SplitOperationResultDto includes balance calculation
- `UpdateSplitAsync_ValidUpdate_UpdatesSplitProperties()` - Amount, category, memo updates
- `UpdateSplitAsync_AmountChanged_ReturnsUpdatedBalanceState()` - Balance recalculated after update
- `UpdateSplitAsync_WrongTransaction_ThrowsInvalidOperationException()` - Security: split must belong to specified transaction
- `DeleteSplitAsync_ValidSplit_RemovesSplit()` - Successful deletion
- `DeleteSplitAsync_LastSplit_ThrowsValidationException()` - Cannot delete last split (business rule)
- `DeleteSplitAsync_ReturnsBalanceStateAfterDeletion()` - Balance calculated with remaining splits
- `DeleteSplitAsync_WrongTransaction_ThrowsInvalidOperationException()` - Security check

**Query Operations with Balance Validation**:
- `GetTransactionsAsync_ReturnsBalanceIndicators()` - Verify HasMultipleSplits, SingleSplitCategory, IsBalanced
- `GetTransactionsAsync_UnbalancedTransaction_IsBalancedFalse()` - Detect imbalance in list view
- `GetTransactionByKeyAsync_IncludesSplits()` - Verify splits loaded in correct order
- `GetTransactionByKeyAsync_CalculatesBalanceState()` - Verify TransactionDetailDto includes SplitsTotal and IsBalanced

**Balance Calculation Logic**:
- `CalculateBalance_MatchingAmounts_ReturnsTrue()` - Splits sum equals transaction amount
- `CalculateBalance_DifferentAmounts_ReturnsFalse()` - Splits sum doesn't match transaction amount
- `CalculateBalance_EmptySplits_ThrowsValidationException()` - Edge case: no splits present
- `CalculateBalance_NegativeAmounts_HandlesCorrectly()` - Credits and refunds

**Tenant Isolation**:
- `AddSplitAsync_OtherTenantTransaction_ThrowsNotFoundException()` - Security
- `UpdateSplitAsync_OtherTenantSplit_ThrowsNotFoundException()` - Security
- `DeleteSplitAsync_OtherTenantSplit_ThrowsNotFoundException()` - Security

Follow the existing test pattern from TransactionsTests.cs:
- Gherkin-style comments (Given/When/Then/And)
- NUnit attributes and constraint-based assertions
- InMemoryDataProvider for fast, isolated tests
- TestTenantProvider for tenant context
- Comprehensive coverage of validation rules and edge cases

### Integration Tests (Controller Layer)

**Transaction endpoints**:
- POST transaction with single split
- POST transaction with multiple splits
- PUT transaction (atomic split replacement)
- GET transaction list (with split indicators and balance check)
- GET transaction detail (with splits)
- DELETE transaction (verify cascade delete of splits)
- Validation: Reject transaction with zero splits

**Split endpoints**:
- GET transaction splits (ordered by Order property)
- POST split to transaction (verify Order assignment)
- PUT split (update amount, category, memo)
- DELETE split (verify not last split)
- DELETE split validation: Reject deletion of last split (400 Bad Request)
- PATCH split reorder (verify Order updates)
- Security: Verify split belongs to transaction in PUT/DELETE operations

### Integration Tests (Data Layer)

- Cascade delete: Delete transaction, verify splits deleted
- Foreign key: Cannot create split without valid TransactionId
- Ordering: Splits retrieved in Order sequence
- Category index: Filter splits by category

## Future Enhancements

1. **Category autocomplete** - Track distinct categories for dropdown suggestions
2. **Split templates** - Save common split patterns (e.g., "Rent: 50% Housing, 50% Business")
3. **Bulk split operations** - Apply category to multiple transactions at once
4. **Category hierarchy** - Support parent/child categories (e.g., "Housing:Rent")
5. **Split validation rules** - Configurable rules (e.g., "Splits must balance" vs. "Splits warning only")
6. **Split history** - Track changes to splits over time (audit log)
7. **Smart split suggestions** - ML-based category suggestions based on payee/amount patterns

## Known Design Gaps (Frontend Implementation)

This design is comprehensive for backend implementation but has intentional gaps for frontend details that should be addressed during UI development:

### 1. Balance Warning Visual Treatment (Partial Specification)

**What's specified**:
- Backend always returns `IsBalanced` flag in all mutation operations
- Lines 17-21: "UI must prominently display warnings", "make it difficult to ignore", "visual indicators, modal warnings, etc."
- Goal: "Guide user to fix balance discrepancies immediately, not later"

**What's NOT specified**:
- Exact icon type (warning triangle? exclamation? info icon?)
- Color scheme (red for error? yellow for warning? orange?)
- List view treatment (inline badge? row highlighting? subtle icon?)
- Detail view treatment (banner at top? inline warning next to splits? modal on save?)
- User flow for resolving imbalance (step-by-step wizard? simple calculation helper? just show the numbers?)

**Recommendation**: Address during frontend UI design phase. Consider user testing for warning prominence vs. annoyance balance.

### 2. Category Text Normalization (Not Specified)

**What's NOT specified**:
- Case sensitivity: Is "Food" the same as "food"? Should categories be case-insensitive for matching?
- Whitespace handling: Should "Food" and " Food " be treated as the same category?
- Trimming: Should leading/trailing whitespace be stripped on save?
- Duplicate detection: Should UI warn when creating near-duplicate categories ("Groceries" vs "Grocery")?
- Maximum distinct categories: At what point does the category dropdown become unwieldy?

**Impact**: Without normalization rules, users may create duplicate categories unintentionally, degrading category report quality.

**Recommendation**: Define category normalization rules (case-insensitive comparison, trim on save) and implement in backend validation before frontend implementation.

### 3. Split Reordering User Experience (Limited Rationale)

**What's specified**:
- PATCH `/splits/reorder` endpoint exists
- Lines 736, 895: Comments mention "drag-and-drop in UI"
- Order property preserves user's display preference

**What's NOT specified**:
- WHY users would want to reorder splits (importance-based? amount-based? just preference?)
- How prominent reordering should be in UI (always-visible drag handles? hidden in edit mode?)
- Alternative interaction patterns (up/down arrow buttons on mobile? context menu?)
- Whether reordering is common enough to warrant prominent UI real estate

**Recommendation**: Gather user feedback during beta testing. Consider starting with minimal reordering UI and enhancing based on usage patterns.

### 4. Bulk Categorization Workflows (Out of Scope)

**What's specified**:
- POST endpoint accepts single OR collection of splits (line 840)
- Line 969: "Atomic replacement is still available for bulk operations"
- Listed as future enhancement (line 1108)

**What's NOT specified**:
- Bulk categorization workflow: How to apply category to multiple transactions at once?
- Filter-then-categorize pattern: Can user filter by payee, then bulk-assign category to all results?
- Import workflow: How to efficiently categorize newly-imported transactions?
- Copy splits workflow: Copy split pattern from one transaction to multiple others?

**Impact**: Power users importing 50+ transactions monthly may find individual categorization tedious.

**Recommendation**: Track user feedback during beta testing. If bulk categorization is a common pain point, design and implement in future sprint.

### 5. Mobile/Responsive Design (Not Covered)

**What's NOT specified**:
- How split editor works on small screens (< 600px width)
- Touch interactions for adding/removing splits (tap-and-hold? swipe? buttons?)
- Simplified mobile view (collapse splits to summary? hide split memo field?)
- Mobile balance warning treatment (inline vs. modal vs. notification?)
- Category dropdown on mobile (native picker? custom autocomplete? full-screen modal?)

**Impact**: Split editor may be difficult to use on mobile devices without responsive design considerations.

**Recommendation**: Use mobile-first design approach. Consider progressive disclosure (show transaction → tap to expand splits → tap to edit individual split).

### 6. Error Recovery and Optimistic UI (Not Covered)

**What's NOT specified**:
- Optimistic UI updates: Should UI immediately reflect changes before server confirmation?
- Failed save handling: What happens if split operation fails mid-save?
- Offline editing: Should app support offline split editing with sync on reconnect?
- Concurrent editing: What if two users edit the same transaction simultaneously?
- Undo/redo: Should there be undo for split operations?

**Impact**: Without error recovery strategy, users may lose work or create inconsistent state.

**Recommendation**: Start with pessimistic UI (wait for server response). Consider optimistic updates for single-split operations only (lower risk). Add offline support in future if user demand exists.

## Summary

This design provides:

✅ **Clean schema** - Split entity with proper relationships and indexes
✅ **Efficient queries** - Index coverage for all common query patterns
✅ **Simple API** - Atomic replacement matches "can't edit independently" constraint
✅ **Flexible DTOs** - Lightweight list view, detailed edit view, split indicators
✅ **Migration path** - Clear steps for adding splits to existing transactions
✅ **Validation** - Business rules enforced, balance warnings for user resolution
✅ **Consistent patterns** - Follows established project conventions (Guid keys, tenancy, etc.)

**Backend design is complete and ready for implementation.** Frontend design gaps are intentional and should be addressed during UI development phase based on usability testing and user feedback.

**Next steps**: Review design with user, then proceed to backend implementation.
