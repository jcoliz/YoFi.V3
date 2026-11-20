---
status: Approved (Implementation in progress)
---

# Transaction Record Design

## Overview

This document defines the database schema, DTO design, validation rules, and migration strategy for implementing the Transaction Record feature in YoFi.V3. This feature adds essential fields to the Transaction entity to support faithful bank data retention and user augmentation.

## Requirements Summary

### Core Requirements (from PRD)

**Story 1: Represent Imported Data**
- Retain bank date, amount, payee (already exist)
- Add bank account source information (free text field)
- Add bank unique identifier for duplicate detection

**Story 2: User Augmentation**
- Support memo field for additional context

**Story 3: Transaction Management**
- Enable editing all fields
- Enable deleting transactions

### Key Design Decisions

- **Source field**: Free-text string (NOT a separate Account entity) - user flexibility over rigid structure
- **ExternalId field**: For duplicate detection - importer's responsibility
- **Memo field**: 1000 chars, nullable, plain text only
- **No audit trail**: Edit-in-place with no history tracking (future enhancement if needed)

## Current State Analysis

### Existing Transaction Entity

Current [`Transaction`](src/Entities/Models/Transaction.cs:13) entity (lines 13-35):

```csharp
[Table("YoFi.V3.Transactions")]
public record Transaction : BaseTenantModel
{
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public string Payee { get; set; } = string.Empty;
    public decimal Amount { get; set; } = 0;

    // Navigation properties
    public virtual Tenant? Tenant { get; set; }
}
```

**Already satisfies Story 1**:
- ✅ Date retained
- ✅ Amount retained
- ✅ Payee retained

**Missing fields**:
- ❌ Source (bank account information)
- ❌ ExternalId (bank's unique identifier)
- ❌ Memo (user notes)

### Existing DTOs

**[`TransactionResultDto`](src/Application/Dto/TransactionResultDto.cs:14)** (output):
```csharp
public record TransactionResultDto(Guid Key, DateOnly Date, decimal Amount, string Payee);
```

**[`TransactionEditDto`](src/Application/Dto/TransactionEditDto.cs:21)** (input):
```csharp
public record TransactionEditDto(
    [DateRange(50, 5)] DateOnly Date,
    [Range(typeof(decimal), "-999999999", "999999999")] decimal Amount,
    [Required][NotWhiteSpace][MaxLength(200)] string Payee
);
```

Both DTOs need to be updated to include new fields.

## Database Schema

### Updated Transaction Entity

```csharp
using System.ComponentModel.DataAnnotations.Schema;
using YoFi.V3.Entities.Tenancy.Models;

namespace YoFi.V3.Entities.Models;

/// <summary>
/// A financial transaction record tied to a specific tenant.
/// </summary>
/// <remarks>
/// Transactions represent financial events imported from bank/credit card sources
/// or entered manually. Each transaction can be annotated with additional user context.
/// </remarks>
[Table("YoFi.V3.Transactions")]
public record Transaction : BaseTenantModel
{
    /// <summary>
    /// Date the transaction occurred.
    /// </summary>
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>
    /// Recipient or payee of the transaction.
    /// </summary>
    /// <remarks>
    /// Required field. Typically populated from bank data or user entry.
    /// </remarks>
    [Required]
    public string Payee { get; set; } = string.Empty;

    /// <summary>
    /// Amount of the transaction.
    /// </summary>
    /// <remarks>
    /// Can be negative for credits/refunds. YoFi is single-currency for now,
    /// so no currency code is stored.
    /// </remarks>
    public decimal Amount { get; set; } = 0;

    /// <summary>
    /// Source of the transaction (e.g., "MegaBankCorp Checking 1234", "Manual Entry").
    /// </summary>
    /// <remarks>
    /// Free-text field typically populated by importer with bank name, account type,
    /// and last 4 digits of account number. Can be any text. Nullable for manual entries
    /// or when source is unknown.
    /// </remarks>
    [MaxLength(200)]
    public string? Source { get; set; }

    /// <summary>
    /// Bank's unique identifier for this transaction.
    /// </summary>
    /// <remarks>
    /// Used for duplicate detection during import. Format varies by bank/institution.
    /// Nullable for manual entries. Importer is responsible for populating this field
    /// and preventing duplicate imports.
    /// </remarks>
    [MaxLength(100)]
    public string? ExternalId { get; set; }

    /// <summary>
    /// Optional memo for additional transaction context.
    /// </summary>
    /// <remarks>
    /// Plain text field for user notes. Most transactions won't have memos.
    /// Examples: "Reimbursable", "Split with roommate", "Gift for John's birthday".
    /// </remarks>
    [MaxLength(1000)]
    public string? Memo { get; set; }

    // Navigation properties
    public virtual Tenant? Tenant { get; set; }
}
```

### Entity Framework Configuration

Update [`ApplicationDbContext`](src/Data/Sqlite/ApplicationDbContext.cs:1) configuration:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    modelBuilder.Entity<Transaction>(entity =>
    {
        // Existing configuration...

        // Payee is required
        entity.Property(t => t.Payee)
            .IsRequired()
            .HasMaxLength(200);

        // Amount precision for currency
        entity.Property(t => t.Amount)
            .HasPrecision(18, 2);

        // NEW: Source (nullable, max 200 chars)
        entity.Property(t => t.Source)
            .HasMaxLength(200);

        // NEW: ExternalId (nullable, max 100 chars)
        entity.Property(t => t.ExternalId)
            .HasMaxLength(100);

        // NEW: Memo (nullable, max 1000 chars)
        entity.Property(t => t.Memo)
            .HasMaxLength(1000);

        // Existing indexes...
        entity.HasIndex(t => t.Key).IsUnique();
        entity.HasIndex(t => t.TenantId);
        entity.HasIndex(t => new { t.TenantId, t.Date });

        // NEW: Composite index on TenantId + ExternalId for efficient duplicate checks
        entity.HasIndex(t => new { t.TenantId, t.ExternalId });
    });
}
```

### Database Indexes

**Existing Indexes**:
1. `IX_Transactions_Key` (Unique) - Standard Guid lookup
2. `IX_Transactions_TenantId` - Tenant isolation
3. `IX_Transactions_TenantId_Date` (Composite) - Date range queries

**New Index**:
4. **`IX_Transactions_TenantId_ExternalId`** (Composite) - Tenant-scoped duplicate detection
   - **Purpose**: Efficient duplicate checking within tenant during import
   - **Queries**: `WHERE TenantId = @tenantId AND ExternalId = @externalId`
   - **Benefits**: Covering index for duplicate detection (most common import scenario)
   - **Note**: No standalone `ExternalId` index needed since all queries include `TenantId`

## DTO Design

### TransactionResultDto (Output - List View)

Used for list views where full details aren't needed.

```csharp
using System;

namespace YoFi.V3.Application.Dto;

/// <summary>
/// Transaction data returned from queries (output-only).
/// </summary>
/// <param name="Key">Unique identifier for the transaction</param>
/// <param name="Date">Date the transaction occurred</param>
/// <param name="Amount">Transaction amount (can be negative for credits/refunds)</param>
/// <param name="Payee">Recipient or payee of the transaction</param>
/// <remarks>
/// This is an output DTO for list views - data is already validated when read from the database.
/// For input/editing, see <see cref="TransactionEditDto"/>.
/// For detail view with all fields, see <see cref="TransactionDetailDto"/>.
/// </remarks>
public record TransactionResultDto(
    Guid Key,
    DateOnly Date,
    decimal Amount,
    string Payee
);
```

### TransactionDetailDto (Output - Detail View)

Used for detail views where all fields are displayed.

```csharp
using System;

namespace YoFi.V3.Application.Dto;

/// <summary>
/// Complete transaction data including all fields (output-only).
/// </summary>
/// <param name="Key">Unique identifier for the transaction</param>
/// <param name="Date">Date the transaction occurred</param>
/// <param name="Amount">Transaction amount (can be negative for credits/refunds)</param>
/// <param name="Payee">Recipient or payee of the transaction</param>
/// <param name="Memo">Optional memo for additional context</param>
/// <param name="Source">Source of the transaction (e.g., "Chase Checking 1234")</param>
/// <param name="ExternalId">Bank's unique identifier for duplicate detection</param>
/// <remarks>
/// Complete transaction DTO including all fields. Used for detail views and editing forms.
/// </remarks>
public record TransactionDetailDto(
    Guid Key,
    DateOnly Date,
    decimal Amount,
    string Payee,
    string? Memo,
    string? Source,
    string? ExternalId
);
```

### TransactionEditDto (Input - Create/Update)

Input DTO for creating or updating transactions.

```csharp
using System;
using System.ComponentModel.DataAnnotations;
using YoFi.V3.Application.Validation;

namespace YoFi.V3.Application.Dto;

/// <summary>
/// Transaction data for creating or updating transactions (input DTO).
/// </summary>
/// <param name="Date">Date the transaction occurred (max 50 years in past, 5 years in future)</param>
/// <param name="Amount">Transaction amount (cannot be zero; can be negative for credits/refunds)</param>
/// <param name="Payee">Recipient or payee of the transaction (required, cannot be whitespace, max 200 chars)</param>
/// <param name="Memo">Optional memo for additional context (max 1000 chars)</param>
/// <param name="Source">Source of the transaction (optional, max 200 chars, typically from importer)</param>
/// <param name="ExternalId">Bank's unique identifier (optional, max 100 chars, for duplicate detection)</param>
/// <remarks>
/// This is an input DTO with validation attributes. All properties are validated before
/// being persisted to the database. For query results, see <see cref="TransactionResultDto"/>
/// or <see cref="TransactionDetailDto"/>.
///
/// Validation rules:
/// - Date: Must be within 50 years in the past and 5 years in the future
/// - Amount: Must be non-zero (enforced in business logic)
/// - Payee: Required, cannot be empty or whitespace, max 200 characters
/// - Memo: Optional, max 1000 characters, plain text only
/// - Source: Optional, max 200 characters, typically set by importer
/// - ExternalId: Optional, max 100 characters, for duplicate detection
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

    [MaxLength(1000, ErrorMessage = "Memo cannot exceed 1000 characters")]
    string? Memo,

    [MaxLength(200, ErrorMessage = "Source cannot exceed 200 characters")]
    string? Source,

    [MaxLength(100, ErrorMessage = "ExternalId cannot exceed 100 characters")]
    string? ExternalId
);
```

## Query Patterns

### Pattern 1: Get Transactions (List View)

Most common query - list view with basic fields.

```csharp
// Query for list view
var transactions = await context.Transactions
    .AsNoTracking()
    .Where(t => t.TenantId == tenantId)
    .Where(t => t.Date >= fromDate && t.Date <= toDate)
    .OrderByDescending(t => t.Date)
    .Select(t => new TransactionResultDto(
        t.Key,
        t.Date,
        t.Amount,
        t.Payee
    ))
    .ToListAsync();
```

**Index used**: `IX_Transactions_TenantId_Date`
**Performance**: Fast - covered by composite index

### Pattern 2: Get Transaction Detail

Single transaction with all fields.

```csharp
// Query for detail view (includes all fields)
var transaction = await context.Transactions
    .AsNoTracking()
    .Where(t => t.TenantId == tenantId && t.Key == transactionKey)
    .Select(t => new TransactionDetailDto(
        t.Key,
        t.Date,
        t.Amount,
        t.Payee,
        t.Memo,
        t.Source,
        t.ExternalId
    ))
    .SingleOrDefaultAsync();
```

**Indexes used**: `IX_Transactions_TenantId`, `IX_Transactions_Key`
**Performance**: Fast - single row lookup

### Pattern 3: Check for Duplicate ExternalId (Importer Use Only)

Query to check for duplicate ExternalId before import. **This is the importer's responsibility, NOT the Transaction API.**

```csharp
// Importer checks for existing transaction with same ExternalId
var exists = await context.Transactions
    .AsNoTracking()
    .AnyAsync(t => t.TenantId == tenantId && t.ExternalId == externalId);

if (exists)
{
    // Importer skips this transaction (duplicate)
    continue;
}

// Importer creates transaction via API
await transactionApi.CreateTransactionAsync(tenantId, transactionDto);
```

**Index used**: `IX_Transactions_TenantId_ExternalId` (covering index)
**Performance**: Very fast - composite index covers query entirely
**Note**: The Transaction API itself does NOT enforce ExternalId uniqueness. This is the importer's responsibility per the PRD.

### Pattern 4: Create Transaction

Create new transaction with all fields.

```csharp
var transaction = new Transaction
{
    TenantId = tenantId,
    Date = dto.Date,
    Payee = dto.Payee,
    Amount = dto.Amount,
    Memo = dto.Memo,
    Source = dto.Source,
    ExternalId = dto.ExternalId
};

context.Transactions.Add(transaction);
await context.SaveChangesAsync();

return new TransactionDetailDto(
    transaction.Key,
    transaction.Date,
    transaction.Amount,
    transaction.Payee,
    transaction.Memo,
    transaction.Source,
    transaction.ExternalId
);
```

**Database operations**: Single transaction insert
**Performance**: Fast - single row insert

### Pattern 5: Update Transaction

Update existing transaction (all fields editable per Story 3).

```csharp
var transaction = await context.Transactions
    .Where(t => t.TenantId == tenantId && t.Key == transactionKey)
    .SingleOrDefaultAsync();

if (transaction == null)
    throw new TransactionNotFoundException(transactionKey);

// Update all fields (all editable per Story 3)
transaction.Date = dto.Date;
transaction.Payee = dto.Payee;
transaction.Amount = dto.Amount;
transaction.Memo = dto.Memo;
transaction.Source = dto.Source;
transaction.ExternalId = dto.ExternalId;

await context.SaveChangesAsync();

return new TransactionDetailDto(
    transaction.Key,
    transaction.Date,
    transaction.Amount,
    transaction.Payee,
    transaction.Memo,
    transaction.Source,
    transaction.ExternalId
);
```

**Database operations**: Single transaction update
**Performance**: Fast - direct update

## API Endpoints

### GET /api/tenant/{tenantKey}/transactions

Returns list of transactions.

**Response**: `IReadOnlyCollection<TransactionResultDto>`

**Query parameters**:
- `fromDate` (optional): Filter by start date
- `toDate` (optional): Filter by end date

**Example Response**:
```json
[
  {
    "key": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "date": "2024-12-20",
    "amount": -42.50,
    "payee": "Acme Grocery Store"
  },
  {
    "key": "7b9e4a1c-8d23-4f96-a542-1e8f3b2c4d5e",
    "date": "2024-12-19",
    "amount": -125.00,
    "payee": "Electric Company"
  }
]
```

### GET /api/tenant/{tenantKey}/transactions/{transactionKey}

Returns single transaction with all fields.

**Response**: `TransactionDetailDto`

**Example Response**:
```json
{
  "key": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "date": "2024-12-20",
  "amount": -42.50,
  "payee": "Acme Grocery Store",
  "memo": "Weekly grocery shopping",
  "source": "Chase Checking 1234",
  "externalId": "TXN20241220-ABC123"
}
```

### POST /api/tenant/{tenantKey}/transactions

Creates new transaction.

**Request body**: `TransactionEditDto`
**Response**: `TransactionDetailDto` (201 Created)

**Example Request**:
```json
{
  "date": "2024-12-20",
  "amount": -42.50,
  "payee": "Acme Grocery Store",
  "memo": "Weekly grocery shopping",
  "source": "Chase Checking 1234",
  "externalId": "TXN20241220-ABC123"
}
```

**Validation**:
- All `TransactionEditDto` validation rules apply
- ExternalId uniqueness NOT enforced at database level (importer's responsibility)

### PUT /api/tenant/{tenantKey}/transactions/{transactionKey}

Updates existing transaction (all fields editable per Story 3).

**Request body**: `TransactionEditDto`
**Response**: `TransactionDetailDto` (200 OK)

**Behavior**:
- All fields are editable (Date, Payee, Amount, Memo, Source, ExternalId)
- Source and ExternalId can be updated (user may correct import errors)

### DELETE /api/tenant/{tenantKey}/transactions/{transactionKey}

Deletes transaction.

**Response**: `204 No Content`

## Validation Rules

### Transaction Validation

1. **Date range** - Within 50 years past, 5 years future (`DateRangeAttribute`)
2. **Payee required** - Not null, not whitespace, max 200 chars
3. **Amount non-zero** - Business rule (validated in feature)
4. **Memo max length** - 1000 characters (nullable)
5. **Source max length** - 200 characters (nullable)
6. **ExternalId max length** - 100 characters (nullable)

### ExternalId Validation

- ✅ Nullable (manual entries don't have ExternalId)
- ✅ No uniqueness constraint at database level (importer's responsibility)
- ✅ Max 100 chars (accommodates various bank formats)

**Duplicate detection**: Importer should query for existing `TenantId + ExternalId` before creating transaction.

## Migration Strategy

### Phase 1: Database Schema Changes

1. **Add new columns to Transaction table**:
   - `Source` (nvarchar(200), nullable)
   - `ExternalId` (nvarchar(100), nullable)
   - `Memo` (nvarchar(1000), nullable)

2. **Create new index**:
   - `IX_Transactions_TenantId_ExternalId` (composite)

3. **Update existing rows** (if any):
   - All new columns are nullable, so no data migration needed
   - Existing transactions will have `NULL` for new fields

**Entity Framework Migration Command**:
```powershell
.\scripts\Add-Migration.ps1 -Name AddTransactionRecordFields
```

### Phase 2: Update Application Code

1. **Update Transaction entity** in [`src/Entities/Models/Transaction.cs`](src/Entities/Models/Transaction.cs:13):
   - Add Source, ExternalId, Memo properties
   - Add XML documentation comments
   - Add validation attributes

2. **Update Entity Framework configuration** in [`src/Data/Sqlite/ApplicationDbContext.cs`](src/Data/Sqlite/ApplicationDbContext.cs:1):
   - Configure max lengths for new properties
   - Add new indexes

3. **Create new DTOs** in [`src/Application/Dto/`](src/Application/Dto/):
   - Create `TransactionDetailDto` (all fields)
   - Update `TransactionEditDto` (add all new fields with validation)

4. **Update TransactionsFeature** in [`src/Application/Features/TransactionsFeature.cs`](src/Application/Features/TransactionsFeature.cs:19):
   - Update mapping to include new fields
   - Update queries to project new DTOs

5. **Update TransactionsController** in [`src/Controllers/TransactionsController.cs`](src/Controllers/TransactionsController.cs:28):
   - Update endpoint return types (new DTOs)
   - Update ProducesResponseType attributes

6. **Regenerate API client**:
   - Run WireApiHost to regenerate TypeScript client with new DTOs
   - New fields will be available in frontend

### Phase 3: Testing

1. **Integration tests** in [`tests/Integration.Controller/`](tests/Integration.Controller/):
   - Test CRUD operations with all new fields
   - Test validation rules for new fields

3. **Integration tests** in [`tests/Integration.Data/`](tests/Integration.Data/):
   - Test entity persistence with new fields
   - Test indexes (externalId)
   - Test nullable field handling

### Phase 4: Frontend Updates

#### 4.1 Regenerate API Client

**Location**: `src/FrontEnd.Nuxt/app/utils/apiclient.ts` (auto-generated, do not edit manually)

**Action**: Build WireApiHost to regenerate TypeScript client with updated DTOs

**Command**:
```bash
# From src/WireApiHost directory
dotnet build
```

**Note**: The build process automatically generates a new `apiclient.ts` file as build output. See [`src/WireApiHost/README.md`](src/WireApiHost/README.md) for details.

**Expected Changes**:
- `TransactionResultDto` remains unchanged (list view doesn't need new fields)
- `TransactionEditDto` gains three new optional properties: `memo`, `source`, `externalId`
- New `TransactionDetailDto` interface created with all fields

#### 4.2 Update Transaction List View - Add Memo Column

**Location**: `src/FrontEnd.Nuxt/app/pages/transactions.vue`

**Current Behavior**: List view shows Date, Payee, Amount only
**New Behavior**: Add Memo column to list view

**Decision**:
- ✅ **Memo** - Add to list view (user notes are valuable at a glance)
- ❌ **Source** - Do NOT add to list view (internal tracking detail, not needed in list)
- ❌ **ExternalId** - Do NOT add to list view (internal tracking detail, not needed in list)

**Backend Change Required**: Update `GetTransactions` endpoint to return `TransactionDetailDto` instead of `TransactionResultDto` OR create a new intermediate DTO that includes Memo but not Source/ExternalId.

**Recommended Approach**: Create new `TransactionListDto` with Date, Payee, Amount, Memo (excluding Source and ExternalId to minimize data transfer).

**Frontend List View Update**:
```vue
<template>
  <table>
    <thead>
      <tr>
        <th>Date</th>
        <th>Payee</th>
        <th>Amount</th>
        <th>Memo</th> <!-- NEW -->
      </tr>
    </thead>
    <tbody>
      <tr v-for="txn in transactions" :key="txn.key">
        <td>{{ formatDate(txn.date) }}</td>
        <td>{{ txn.payee }}</td>
        <td>{{ formatCurrency(txn.amount) }}</td>
        <td class="memo-cell">{{ txn.memo || '' }}</td> <!-- NEW -->
      </tr>
    </tbody>
  </table>
</template>

<style scoped>
.memo-cell {
  max-width: 200px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
```

**Note**: Memo column should truncate long text with ellipsis in list view. Full memo visible in detail view.

#### 4.3 Update Transaction Detail/Edit Form (Required)

**Location**: `src/FrontEnd.Nuxt/app/pages/transactions/[id].vue` or similar

**Current Form Fields**: Date, Payee, Amount
**New Form Fields**: Memo (textarea), Source (text input), ExternalId (text input, read-only for imported transactions)

**Example Form Implementation**:
```vue
<template>
  <form @submit.prevent="saveTransaction">
    <!-- Existing Fields -->
    <div class="form-group">
      <label for="date">Date</label>
      <input type="date" id="date" v-model="form.date" required />
    </div>

    <div class="form-group">
      <label for="payee">Payee</label>
      <input type="text" id="payee" v-model="form.payee" required maxlength="200" />
    </div>

    <div class="form-group">
      <label for="amount">Amount</label>
      <input type="number" id="amount" v-model="form.amount" step="0.01" required />
    </div>

    <!-- NEW FIELDS -->
    <div class="form-group">
      <label for="source">Source</label>
      <input
        type="text"
        id="source"
        v-model="form.source"
        placeholder="e.g., Chase Checking 1234"
        maxlength="200"
      />
      <small class="form-help">Bank account this transaction came from (optional)</small>
    </div>

    <div class="form-group">
      <label for="memo">Memo</label>
      <textarea
        id="memo"
        v-model="form.memo"
        placeholder="Add notes about this transaction..."
        maxlength="1000"
        rows="3"
      ></textarea>
      <small class="form-help">{{ form.memo?.length || 0 }} / 1000 characters</small>
    </div>

    <div class="form-group" v-if="form.externalId">
      <label for="externalId">External ID</label>
      <input
        type="text"
        id="externalId"
        v-model="form.externalId"
        maxlength="100"
        :readonly="isImportedTransaction"
        :title="isImportedTransaction ? 'Cannot edit bank transaction ID' : ''"
      />
      <small class="form-help" v-if="isImportedTransaction">
        Bank transaction ID (read-only for imported transactions)
      </small>
    </div>

    <button type="submit" :disabled="!isValid">Save Transaction</button>
  </form>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { apiClient } from '~/utils/apiclient'

const router = useRouter()
const props = defineProps<{ id?: string }>()

const form = ref({
  date: '',
  payee: '',
  amount: 0,
  source: null as string | null,
  memo: null as string | null,
  externalId: null as string | null
})

// If editing existing transaction, load it
if (props.id) {
  const transaction = await apiClient.getTransactionById(tenantKey, props.id)
  form.value = {
    date: transaction.date,
    payee: transaction.payee,
    amount: transaction.amount,
    source: transaction.source,
    memo: transaction.memo,
    externalId: transaction.externalId
  }
}

const isImportedTransaction = computed(() => {
  return !!form.value.externalId && !!form.value.source
})

const isValid = computed(() => {
  return form.value.payee.trim().length > 0 &&
         form.value.amount !== 0 &&
         (form.value.memo?.length || 0) <= 1000 &&
         (form.value.source?.length || 0) <= 200 &&
         (form.value.externalId?.length || 0) <= 100
})

async function saveTransaction() {
  try {
    if (props.id) {
      await apiClient.updateTransaction(tenantKey, props.id, form.value)
    } else {
      await apiClient.createTransaction(tenantKey, form.value)
    }
    router.push('/transactions')
  } catch (error) {
    // Handle validation errors
    console.error('Failed to save transaction:', error)
  }
}
</script>
```

#### 4.4 UI/UX Considerations

**Field Visibility**:
- **Source**: Show only if transaction has Source OR user explicitly expands "Advanced Details" section
- **Memo**: Always visible (common field for user notes)
- **ExternalId**: Show only if present (imported transactions). Hide for manual entries.

**Field Editability**:
- **Source**: Editable (user may correct import errors or add manually)
- **Memo**: Always editable
- **ExternalId**: Read-only for imported transactions (to preserve bank reference), editable for manual entries if user wants to add it

**Validation Feedback**:
- Show character count for Memo (xxx / 1000)
- Show validation errors inline below each field
- Disable Save button if validation fails

**Empty State Messaging**:
- If Memo is empty: No message needed
- If Source is empty: Show placeholder "e.g., Chase Checking 1234" as hint
- If ExternalId is empty: Hide the field entirely (not relevant for manual entries)

#### 4.5 Import Workflow Integration (Future Feature)

**When Bank Import feature is implemented**, the importer will:

1. **Populate Source field** with bank name + account type + last 4 digits
   - Example: "Chase Checking 1234", "Amex Card 9876"

2. **Populate ExternalId field** with bank's FITID or transaction ID
   - Example: "20241220.ABC123", "TXN-2024-001234"

3. **Optionally populate Memo field** if bank provides transaction notes/memo in OFX/QFX file
   - Some banks include memo/notes fields that could be imported
   - User can edit or add to memo after reviewing imported transactions

**Frontend Import Review Page** (separate task):
- Show Source and ExternalId for each imported transaction
- Allow user to edit Source if importer guessed wrong
- Do NOT allow editing ExternalId (preserve bank reference)
- Allow user to add Memo during review

#### 4.6 Testing Frontend Changes

**Manual Testing Checklist**:
- [ ] Create transaction with all fields populated
- [ ] Create transaction with minimal fields (Date, Payee, Amount only)
- [ ] Edit transaction and update Memo
- [ ] Verify character limits enforced (Memo 1000, Source 200, ExternalId 100)
- [ ] Verify validation errors shown for invalid input
- [ ] Verify nullable fields can be cleared (set to null)
- [ ] Verify ExternalId is read-only for imported transactions
- [ ] Verify detail view displays all fields correctly
- [ ] Verify list view displays Memo column with truncation
- [ ] Verify list view does NOT display Source or ExternalId columns

**Functional Test Updates**:
- Update `TransactionsPage.cs` page object to include new form fields
- Functional test scenarios will be determined during implementation following the test strategy

## Testing Strategy

### Unit Tests (Application Layer)

Follow existing pattern from [`tests/Unit/Tests/TransactionsTests.cs`](tests/Unit/Tests/TransactionsTests.cs:1):

**Transaction Creation with New Fields**:
- `AddTransactionAsync_AllFields_CreatesTransaction()` - Verify all fields persisted
- `AddTransactionAsync_MinimalFields_CreatesTransaction()` - Only required fields
- `AddTransactionAsync_NullableFields_AllowsNull()` - Memo, Source, ExternalId nullable

**Transaction Updates**:
- `UpdateTransactionAsync_UpdatesAllFields()` - All fields editable (Story 3)
- `UpdateTransactionAsync_NullFields_ClearsValues()` - Can clear optional fields

**Query Operations**:
- `GetTransactionByKeyAsync_ReturnsAllFields()` - Verify TransactionDetailDto includes all fields

**ExternalId Handling**:
- `AddTransactionAsync_DuplicateExternalId_AllowsCreation()` - API does NOT reject duplicates
- `AddTransactionAsync_SameExternalIdDifferentTenants_AllowsCreation()` - ExternalId can exist across tenants
- `AddTransactionAsync_NullExternalId_AllowsCreation()` - Manual entries without ExternalId

**Note**: Duplicate detection tests belong in the **importer feature tests**, not Transaction API tests. The Transaction API allows duplicate ExternalId values per the PRD.

**Validation**:
- `AddTransactionAsync_MemoTooLong_ThrowsValidationException()` - Max 1000 chars
- `AddTransactionAsync_SourceTooLong_ThrowsValidationException()` - Max 200 chars
- `AddTransactionAsync_ExternalIdTooLong_ThrowsValidationException()` - Max 100 chars

**Tenant Isolation**:
- `GetTransactionAsync_OtherTenant_ReturnsNull()` - Cannot access other tenant's transactions
- `UpdateTransactionAsync_OtherTenant_ThrowsNotFoundException()` - Cannot update other tenant's transactions

Follow existing test patterns:
- Gherkin-style comments (Given/When/Then/And)
- NUnit attributes and constraint-based assertions
- InMemoryDataProvider for fast, isolated tests
- TestTenantProvider for tenant context

### Integration Tests (Controller Layer)

Test API endpoints with new fields:

**CRUD Operations**:
- POST transaction with all fields
- POST transaction with minimal fields (only required)
- GET transaction list
- GET transaction detail (verify all fields included)
- PUT transaction (update all fields)
- DELETE transaction

**Validation** (Feature throws ArgumentException → Middleware returns 400 Bad Request):
- POST with missing required fields (Payee empty/whitespace)
- POST with field length violations (Memo > 1000 chars, Source > 200 chars, ExternalId > 100 chars, Payee > 200 chars)
- POST with invalid date range (Date outside 50 years past / 5 years future)
- POST with zero amount (business rule violation)

**Tenant Isolation**:
- Cannot access other tenant's transactions
- Cannot update other tenant's transactions
- Same ExternalId can exist across different tenants
- Same ExternalId can exist within same tenant (API allows duplicates)

### Integration Tests (Data Layer)

Test database operations:

**Persistence**:
- Create transaction with all fields
- Create transaction with nullable fields null
- Update transaction fields
- Delete transaction

**Indexes**:
- Composite TenantId+ExternalId index exists and performs efficiently
- Index supports queries filtering by both TenantId and ExternalId

**Queries**:
- Query for existing ExternalId within tenant (for importer use)
- No uniqueness constraint enforced (allows duplicates)

## Performance Considerations

### Index Coverage

- **List view queries**: Covered by `IX_Transactions_TenantId_Date` (no new fields needed)
- **Duplicate detection**: Covered by `IX_Transactions_TenantId_ExternalId` (composite)

### Query Optimization

- **AsNoTracking**: Always use for read-only queries
- **Projection**: Use `.Select()` to project to DTOs (avoid loading unused fields)

### Storage Considerations

New columns add minimal storage overhead:
- Source: ~30 chars average = 60 bytes
- ExternalId: ~20 chars average = 40 bytes
- Memo: ~100 chars average = 200 bytes (most null)

**Total**: ~300 bytes per transaction (acceptable overhead)

## Future Enhancements

1. **ExternalId uniqueness enforcement** - Optional database constraint (if importers become reliable)
2. **Transaction attachments** - Link receipts/documents to transactions
3. **Audit trail** - Track who changed what and when (if needed)

## Summary

This design provides:

✅ **Complete schema** - All Story 1 and Story 2 requirements met
✅ **Efficient queries** - Index coverage for common query patterns
✅ **Simple API** - RESTful endpoints with clear DTOs
✅ **Flexible validation** - User flexibility balanced with data quality
✅ **Migration path** - Clear steps for adding fields to existing transactions
✅ **Testing strategy** - Comprehensive coverage of all new functionality
✅ **Consistent patterns** - Follows established project conventions
✅ **Frontend implementation** - Complete Vue component examples and UX guidance

**Design is complete and ready for implementation.** All PRD requirements are addressed with clear implementation guidance for each layer of the stack: Database → Entities → Application → Controllers → API → Frontend → Tests.

## Ready for Break - Resume Instructions

Perfect timing to pause! Here's what to say when you're ready to resume:

### To Resume at Step 10 (Frontend Implementation):

```
Continue implementing docs/wip/transactions/PRD-TRANSACTION-RECORD.md,
starting at Step 10 (Frontend). Steps 1-9 are complete and committed.
```

### Current Status Summary

**✅ Completed & Committed (Steps 1-9):**
- Step 1-2: Scope & Design ✅
- Step 3: Entity (Transaction with Memo, Source, ExternalId) ✅
- Step 4: Data Layer (EF config, migration) ✅
- Step 5: Data Integration Tests ✅
- Step 6: Application Layer (TransactionsFeature) ✅
- Step 7: Unit Tests (98 tests passing) ✅
- Step 8: Controllers (updated DTOs) ✅
- Step 8.5: API Client Regeneration & Frontend Compatibility ✅
- Step 9: Controller Integration Tests (69 tests passing) ✅
- Step 10: Frontend UI (implement transaction pages with new fields) ✅

**⏭️ Next Steps (10-12):**
- Step 11: Functional Tests (critical user workflows)
- Step 12: Wrap-up (final verification, PRD status update)

### PRD Acceptance Criteria Status:
- Story 1 (Represent imported data): ✅ 100% complete
- Story 2 (Add additional info): ⚠️ 50% complete (memo backend done, UI pending)
- Story 3 (Manage transactions): ✅ Backend 100% complete, UI pending

**Backend implementation is fully complete.** Only frontend UI remains.

Enjoy your break! 🎉

## Scenario 2235 Addendum

Addendum Status: In review

### Scope

This addendum covers Scenario 2235, **Drop-down picker for categories**. It adds
category suggestions to every current user-editable category field:

- transaction quick edit;
- transaction detail edit, add-split, and edit-split forms;
- transaction creation;
- payee-rule creation and editing, including the rule dialog opened from a
  transaction (the current UI's quick-edit and detail-page equivalents for payees);
  and
- the Transactions filter pane.

The category remains free-form text. This change does not replace category values,
create a Category entity, alter category sanitization, or change transaction
filter semantics. Import review, reports, and worksheets remain unchanged because
they do not currently provide editable category fields.

### Acceptance Criteria

- [x] Every current category-entry field uses the shared category picker described
  below.
- [x] The picker is available in transaction quick edit and on the transaction
  detail page, including add-split and edit-split forms.
- [x] The picker is available when creating or editing a payee matching rule and
  when opening that dialog from a transaction.
- [x] The picker is available in the Transactions filter pane.
- [x] Suggestions are not loaded or displayed for an initially populated field.
- [x] User entry of the first non-blank character loads suggestions; subsequent
  edits refresh them, and clearing the field hides and cancels pending work.
- [x] Suggestions contain existing tenant data and selecting one fills the field
  without submitting the surrounding form.

### Architectural Considerations

Follow the existing tenant-scoped BFF pattern: the existing Transactions
Application feature queries persisted split categories, the Transactions
controller exposes them, and a reusable Vue component owns suggestion
interaction. No third-party
autocomplete or component dependency is introduced.

The picker is a reusable presentation component rather than duplicated logic in
each form. It must not own or mutate any form's persistence state; it exposes a
string through `v-model` and the surrounding form remains responsible for
validation and saving.

Category lookups can be issued on every input change, so the component debounces
requests for 200 ms and ignores results from a superseded request. The response
is capped at 20 suggestions. The endpoint returns only distinct non-empty values;
it does not page results. No cache is added because recent edits must become
visible to subsequent lookups without cache invalidation after transaction
writes.

### Design Decisions

| Decision | Alternatives considered | Rationale |
| -------- | ----------------------- | --------- |
| Keep suggestions free-form and select only a full existing value | Suggest an automatically created hierarchy or require selection from the list | Categories are intentionally stored as free-form text; selection must not block new categories. |
| Read categories only from tenant transaction splits | Also read payee matching rules, or read only categories visible on the current transaction page | A category used by a rule but never by a transaction is too small an edge case to justify joining another persisted source. Transaction splits are the established source of categories actually used by the user. |
| Match the sanitized whole input as a case-insensitive prefix and sort results ordinally | Case-sensitive prefix or arbitrary substring search | Prefix matching is predictable for a type-ahead picker and supports hierarchical values such as `Food:Groceries`; case-insensitive matching avoids duplicate casing choices. |
| Add category suggestions to the Transactions feature and controller | Create a separate Categories feature and controller | Suggestions read transaction split data and should remain a small tenant-scoped Transactions capability. Avoiding new feature/controller types keeps the change proportional and uses the existing dependency wiring and API client. |
| Load only after an input event with at least one sanitized character | Load all choices when the component mounts or on focus | This preserves the specified behavior for prefilled fields and avoids unnecessary requests. |
| On lookup failure, keep the entered text and hide the suggestion list | Show a blocking form error or require retry | Suggestions are assistive. Failure of the lookup must never prevent an otherwise valid category from being saved. |

### Entities

No entity changes are required. Suggested categories are read from existing
[`Split`](../../../src/Entities/Models/Split.cs) records. Payee matching rule
categories are not a suggestion source. No Category entity or relationship is
introduced.

### Data

No database, `DbContext`, migration, index, or constraint changes are required.
The existing transaction/split indexes are unaffected; this is a small,
input-driven read whose result is capped after distinct values are found.

[`TransactionsFeature`](../../../src/Application/Features/TransactionsFeature.cs)
must form one no-tracking `IQueryable<string>` projection of `Split.Category`
for splits whose parent `Transaction.TenantId` equals the current tenant. The
query excludes empty values, matches the lower-cased sanitized input as a
prefix, selects only the category, and applies provider-side `Distinct`. It then
orders with `StringComparer.OrdinalIgnoreCase` and `Ordinal` and returns at most
20 values. Normalization for lookup must not alter the text returned to or
persisted by the caller.

### Data Integration Tests

No Data-layer configuration is added, so no schema or EF-configuration tests are
required. Query translation and tenant isolation are covered by the Application
integration tests below.

### Application Features

Add the following public operation to
[`TransactionsFeature`](../../../src/Application/Features/TransactionsFeature.cs):

```csharp
public Task<IReadOnlyCollection<string>> GetCategorySuggestionsAsync(
    string term,
    CancellationToken cancellationToken = default);
```

Its summary is "Gets case-insensitive prefix suggestions for the categories
persisted in the current tenant." The `term` parameter is the current user input;
the return value is an alphabetically ordered, case-insensitively distinct
collection of at most 20 existing category strings. The method throws
`ArgumentException` when `term` sanitizes to an empty value and
`OperationCanceledException` when the cancellation token is canceled. It applies
the tenant predicate described in [Data](#data) to the split query.

The operation does not invent parent category paths. A persisted
`Food:Groceries` value is eligible when the sanitized term is, for example,
`food`, `food:`, or `food:gr`. It remains free-form input, so no server validation
change is made to any create or edit DTO.

### Application Integration Tests

Extend
[`TransactionsFeatureTests`](../../../tests/Integration.Application/TransactionsFeatureTests.cs)
with these tests and Summary text:

- `Gets category suggestions from splits for the current tenant only` - seeds
  splits in the current and another tenant, then proves the source set,
  non-empty filtering, ordering, and tenant isolation;
- `Matches category prefixes case-insensitively and sanitizes the term` - proves
  normalized partial and hierarchical prefixes while excluding values that only
  contain the term later in the string;
- `De-duplicates category suggestions case-insensitively` - proves repeated
  categories across transaction splits produce one value;
- `Limits category suggestions to twenty` - proves the response is bounded after
  ordering; and
- `Rejects a blank category search term` - proves the feature rejects blank or
  whitespace-only input.

These tests must use the real configured provider so EF translation is exercised.
No Application-layer logging is added.

### Controllers

Add the following endpoint to
[`TransactionsController`](../../../src/Controllers/TransactionsController.cs):

| Method | Route | Tenant role | Request body | Success response | Error responses |
| ------ | ----- | ----------- | ------------ | ---------------- | --------------- |
| `GET` | `/api/tenant/{tenantKey:guid}/transactions/categories?term={term}` | Viewer | None; required query `term` | `200 OK`, `IReadOnlyCollection<string>` | `400` blank/whitespace term or sanitized term longer than 200 characters; `401`; `403`; `500` |

Its endpoint summary is "Gets existing transaction category suggestions for the
current tenant." The controller action has the signature
`Task<IActionResult> GetCategorySuggestions([FromQuery] string? term, CancellationToken cancellationToken)`.
`term` is a required query parameter. Controller validation must reject the
null/blank value before calling the feature and map all errors to the standard
`ProblemDetails` response. No new exception or exception-handler mapping is
needed.

Use `[LoggerMessage]`-attributed partial methods at this API boundary: a debug
"Starting" event and an information event recording only the returned count. Do
not log the term or category values. Add the generated client method
`TransactionsClient.getCategorySuggestions(term, tenantKey)` by running
[`Generate-ApiClient.ps1`](../../../scripts/Generate-ApiClient.ps1); do not edit
[`apiclient.ts`](../../../src/FrontEnd.Nuxt/app/utils/apiclient.ts) directly.

The endpoint is additive and requires no frontend rollout guard: older frontends
continue using their existing routes. New frontend calls use only the generated
client.

### Front End

Add
[`CategoryPicker.vue`](../../../src/FrontEnd.Nuxt/app/components/) as the shared
input component. Its JSDoc summary is "Provides free-form category entry with
accessible, tenant-scoped suggestions from persisted categories."

```ts
defineProps<{
  modelValue: string
  inputId: string
  inputTestId: string
  tenantKey: string
  maxlength?: number
  disabled?: boolean
  placeholder?: string
  invalid?: boolean
  describedBy?: string
}>()

defineEmits<{
  'update:modelValue': [value: string]
  blur: [event: FocusEvent]
  keydown: [event: KeyboardEvent]
  keyup: [event: KeyboardEvent]
}>()
```

Defaults are `maxlength: 200`, `disabled: false`, `placeholder: ''`,
`invalid: false`, and `describedBy: undefined`. The component renders a text
input with the existing Bootstrap validation classes. It preserves the input's
current string exactly and makes no sanitization change to the form value.

On an actual `input` event, the component emits the complete value. If the value
sanitizes to empty, it immediately clears the list, closes it, and increments the
request generation so any late response is ignored. Otherwise it debounces for
200 ms and calls `TransactionsClient.getCategorySuggestions(term, tenantKey)`. A sequence
number rejects stale responses; changing tenant also clears all suggestions.
Programmatic `v-model` initialization or prop updates do not issue a request, so a
prefilled category does not open on first render. Typing, deleting to another
non-blank value, or editing a prefilled value refreshes the suggestions.

The popup is rendered only when the latest lookup succeeded with at least one
suggestion. It is a `role="listbox"`; each option has `role="option"`, exposes
`aria-selected`, and is selectable by mouse, Enter, or Tab. Up/Down moves the
active option, Escape closes without changing text, and ordinary typing remains
uninterrupted. Choosing an option emits that exact string, closes the list, and
does not bubble a submit action. A failed request silently closes the list and
does not add form-level validation because category input remains valid. The
list closes on focus leaving the component.

Replace category `<input>` elements with `CategoryPicker` while retaining each
form's existing model, labels, maximum length, validation, save handler, and
`data-test-id`:

- [`EditModal.vue`](../../../src/FrontEnd.Nuxt/app/components/transaction/EditModal.vue)
  for transaction quick edit;
- [`EditPane.vue`](../../../src/FrontEnd.Nuxt/app/components/transaction/EditPane.vue),
  the add-split form in
  [`transactions/[key].vue`](../../../src/FrontEnd.Nuxt/app/pages/transactions/[key].vue),
  and [`EditSplitDialog.vue`](../../../src/FrontEnd.Nuxt/app/components/transaction/EditSplitDialog.vue)
  for transaction detail entry;
- [`CreateModal.vue`](../../../src/FrontEnd.Nuxt/app/components/transaction/CreateModal.vue)
  for new transactions;
- [`Dialog.vue`](../../../src/FrontEnd.Nuxt/app/components/payee-rules/Dialog.vue)
  for both create/edit rule uses; and
- [`TransactionFilterPanel.vue`](../../../src/FrontEnd.Nuxt/app/components/TransactionFilterPanel.vue)
  for filtering. Its picker forwards `blur` so the panel's existing `commitOnBlur`
  and Enter handling remain unchanged.

The category picker has no store dependency. Each caller supplies its current
tenant key; when no tenant is available, the surrounding screen does not render
an editable form under its existing workspace rules. No localStorage key changes.

#### Test IDs

`CategoryPicker` keeps the caller-provided `data-test-id` on the text input.
Popup identifiers are derived from `inputTestId` so multiple category pickers
cannot collide on one page.

| data-test-id | Element | Purpose |
| ------------ | ------- | ------------ |
| Existing IDs: `edit-transaction-category`, `edit-category`, `add-split-category`, `edit-split-category`, `create-transaction-category`, `category-input`, `category-filter` | Category text input | Identifies each existing editable field and remains unchanged. |
| `<input-test-id>-suggestions` | Suggestion `ul`/`listbox` | Verifies the current picker has opened. |
| `<input-test-id>-suggestion` | Suggestion option | Selects a suggestion; each picker has only one active list at a time. |

No popup is displayed when the latest lookup has no matches, when the field is
initially populated, or after the field is cleared before a request.

### Component Tests

Add focused Vitest coverage in
[`category-picker.test.ts`](../../../src/FrontEnd.Nuxt/tests/) for the new
component, following the behavior contracts in
[`tests/README.md`](../../../src/FrontEnd.Nuxt/tests/README.md):

- an initially populated model makes no API call and displays no list;
- typing the first non-blank character calls the generated client after debounce;
- clearing the field cancels visibility and suppresses an older result;
- every subsequent input refreshes the query, and only the latest response wins;
- mouse selection and Enter emit the suggestion without submitting;
- Escape and blur close the list while preserving the entered value;
- changing tenant clears the prior tenant's suggestions;
- API failure leaves the value editable and emits no form error; and
- the component forwards `maxlength`, disabled, invalid, and `aria-describedby`
  states to the input.

Add one wiring test to the relevant existing test files for each integrated form,
asserting that its current category test ID is the picker input and that selection
updates the same value submitted by its existing save/filter behavior. This
covers transaction quick edit, transaction detail edit, add/edit split, transaction
create, payee-rule create/edit, and transaction filtering without testing page
navigation or markup layout.

### Functional Tests

The Scenario 2235 user-visible behavior is specified with one scenario outline.
Examples identify the required surfaces; the step implementation may reuse the
existing workspace seeding and each page's current navigation steps.

```gherkin
Scenario Outline: Category picker suggests existing values while editing
    Given the workspace contains category "Groceries"
    And I am editing a category in the <surface>
    And the category field currently contains ""
    When I enter "g" in the category field # NEW
    Then the "Groceries" category suggestion is displayed # NEW

    Examples:
        | surface                         |
        | transaction quick edit          |
        | transaction detail page         |
        | payee rule quick edit           |
        | payee rule detail page          |
        | transaction filter pane         |
```

A separately seeded prefilled field is covered in the component tests. The
functional outline intentionally tests the acceptance-criterion surfaces and the
specified update/hide rules without coupling to a particular suggestion request
implementation. Transaction creation is covered by the component wiring test;
its category input uses the same picker even though it is not listed separately
in the scenario's acceptance-criterion bullets.

### Open Questions

None.

### Completion Checklist

- [x] Every PRD acceptance criterion in scope is traceable to this addendum.
- [x] Every affected layer is covered; untouched Entity, Data, and migration work
  is explicitly identified.
- [x] The category endpoint lists its tenant role, response type, and error
  statuses.
- [x] No new exception or status-code mapping is introduced.
- [x] No index, constraint, conversion, migration, or data-provider capability is
  introduced; query behavior has Application integration coverage.
- [x] The additive endpoint preserves compatibility with the previous frontend.
- [x] Every `data-test-id` used by the functional behavior is derived from the
  test-ID contract above.
- [x] The Gherkin scenario outline places all `When` steps before each subsequent
  `Then` group and never starts a later action after the final assertions.
- [x] No unresolved or deferred implementation text is present.
- [x] Open Questions is empty.
- [x] All links point to existing project files or directories.
