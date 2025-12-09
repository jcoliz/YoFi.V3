# YoFi.V3 Data Layer - SQLite

This project provides the SQLite implementation of the data layer for YoFi.V3.

## Important SQLite Behavior Notes

### Type Affinity and Constraint Enforcement

SQLite uses **type affinity** rather than strict type enforcement. This has important implications for data validation:

#### String Length Constraints Are NOT Enforced

When you configure a property with `.HasMaxLength(100)` in EF Core, SQLite **does not enforce** this constraint:

```csharp
entity.Property(a => a.Name)
    .IsRequired()
    .HasMaxLength(100);  // ⚠️ SQLite ignores this at runtime
```

**What this means:**
- SQLite will accept and store strings of ANY length, regardless of the configured max length
- A string of 500 characters will be stored in a `VARCHAR(100)` column without error
- The max length is purely advisory metadata in SQLite

**Why this matters:**
- Your application **must validate** at the business logic layer
- Production databases (PostgreSQL, SQL Server) **will enforce** max length constraints
- Code that works in development (SQLite) may fail in production if it exceeds length limits

**Example from our codebase:**

We use data annotations on DTOs to define validation rules in one place:

[`TransactionEditDto`](../../Application/Dto/TransactionEditDto.cs):
```csharp
public record TransactionEditDto(
    DateOnly Date,

    [Range(typeof(decimal), "-999999999", "999999999")]
    decimal Amount,

    [Required]
    [MaxLength(200)]  // This matches the EF Core configuration
    string Payee
);
```

Then validate using the built-in `Validator` class in [`TransactionsFeature.ValidateTransactionEditDto()`](../../Application/Features/TransactionsFeature.cs):

```csharp
var validationContext = new ValidationContext(transaction);
var validationResults = new List<ValidationResult>();

if (!Validator.TryValidateObject(transaction, validationContext, validationResults, validateAllProperties: true))
{
    var errors = string.Join("; ", validationResults.Select(r => r.ErrorMessage));
    throw new ArgumentException($"Validation failed: {errors}");
}
```

This approach:
- ✅ Keeps validation rules with the data model (single source of truth)
- ✅ Makes the max length visible to both developers and EF Core
- ✅ Provides consistent validation regardless of database provider
- ✅ Eliminates magic numbers scattered through validation code

#### Integration Tests Document This Behavior

The integration tests in [`tests/Integration.Data`](../../../tests/Integration.Data) explicitly test and document this SQLite behavior:

- [`Tenant_NameMaxLengthConfigured()`](../../../tests/Integration.Data/TenantTests.cs) - Documents that 101 chars are stored despite 100 char EF Core configuration
- [`UserTenantRoleAssignment_UserIdMaxLengthConfigured()`](../../../tests/Integration.Data/UserTenantRoleAssignmentTests.cs) - Documents that 451 chars are stored despite 450 char configuration

### Other SQLite Quirks

- **CHECK constraints**: Supported and enforced
- **FOREIGN KEY constraints**: Supported but must be explicitly enabled (`PRAGMA foreign_keys = ON`)
- **UNIQUE constraints**: Supported and enforced
- **NOT NULL constraints**: Supported and enforced
- **DEFAULT values**: Supported

## Best Practices

1. **Always validate in application code** - Never rely solely on database constraints
2. **Write integration tests** - Verify actual database behavior, not just EF Core configuration
3. **Test against production database** - Periodically test migrations and queries against your production database type
4. **Document assumptions** - Note when behavior differs between SQLite and production databases

## References

- [SQLite Data Types and Type Affinity](https://www.sqlite.org/datatype3.html)
- [SQLite Constraints](https://www.sqlite.org/lang_createtable.html#constraints)
