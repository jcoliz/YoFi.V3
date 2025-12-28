# Entities

This describes the data which is moved around the application, and how the data can be found.
Only code-free records, and interfaces, should be found here.

## Domain Models

### Transaction

Represents a financial transaction with date, amount, payee, memo, and other fields. Each transaction belongs to a tenant and has a collection of splits for categorization.

See [`docs/wip/transactions/PRD-TRANSACTION-RECORD.md`](../../docs/wip/transactions/PRD-TRANSACTION-RECORD.md) for the complete Transaction Record MVP specification.

### Split

Represents a portion of a transaction allocated to a specific category. Every transaction must have at least one split.

**Alpha-1 Design Pattern:**
- Every transaction has exactly one split at Order=0
- Split amount matches transaction amount
- Split category contains the transaction's category
- This simplified design supports the Transaction Record MVP while establishing the database schema for future multi-split support

**Key Properties:**
- `TransactionId` - Foreign key to parent transaction
- `Amount` - Amount allocated to this category (can be negative)
- `Category` - Free-form category text (empty string indicates uncategorized)
- `Memo` - Optional memo specific to this split
- `Order` - Display order within transaction (zero-based)

**Tenant Isolation:**
- Splits inherit from `BaseModel` (not `BaseTenantModel`)
- Tenant isolation comes from the parent `Transaction` entity
- Cascade delete ensures splits are removed when transaction is deleted

See [`docs/wip/transactions/PRD-TRANSACTION-SPLITS.md`](../../docs/wip/transactions/PRD-TRANSACTION-SPLITS.md) for complete specification and future multi-split design.
