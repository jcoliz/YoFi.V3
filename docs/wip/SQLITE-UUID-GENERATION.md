# SQLite UUID Generation Strategy

## The Problem

SQLite doesn't have a native `gen_random_uuid()` function like PostgreSQL. We need a cross-database solution that works in both SQLite (development/CI) and PostgreSQL (production).

## Solutions (Best to Worst)

### ✅ Solution 1: Application-Generated UUIDs (RECOMMENDED)

Generate UUIDs in application code before saving to database.

#### Implementation

```csharp
public abstract class BaseModel : IModel
{
    public long Id { get; set; }
    public Guid PublicId { get; set; } = Guid.NewGuid();  // Generate in C#
}

public class Transaction : BaseModel, ITenantModel
{
    public long TenantId { get; set; }
    public decimal Amount { get; set; }
    // ...
}
```

#### Database Configuration

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Transaction>(entity =>
    {
        entity.HasKey(t => t.Id);

        entity.Property(t => t.Id)
            .ValueGeneratedOnAdd();

        // No default value SQL needed - generated in C#
        entity.Property(t => t.PublicId)
            .IsRequired();

        entity.HasIndex(t => t.PublicId)
            .IsUnique();
    });
}
```

#### Migration

```csharp
public partial class AddPublicIds : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "PublicId",
            table: "Transactions",
            type: "TEXT",  // SQLite stores GUIDs as TEXT
            nullable: false,
            defaultValue: Guid.Empty);  // Temporary default

        // Update existing rows with new GUIDs
        migrationBuilder.Sql(@"
            UPDATE Transactions
            SET PublicId = lower(hex(randomblob(16)));
        ");

        migrationBuilder.CreateIndex(
            name: "IX_Transactions_PublicId",
            table: "Transactions",
            column: "PublicId",
            unique: true);
    }
}
```

**Advantages**:
- ✅ Works identically in SQLite and PostgreSQL
- ✅ No database-specific SQL
- ✅ GUIDs generated before database sees them (useful for logging, correlation)
- ✅ Can validate/test GUID generation in unit tests
- ✅ No migration compatibility issues

**Disadvantages**:
- ❌ Must remember to set PublicId before saving (can be mitigated with base class)

---

### ✅ Solution 2: Database-Specific Default Values

Use different SQL for SQLite vs PostgreSQL.

#### Implementation

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Transaction>(entity =>
    {
        entity.HasKey(t => t.Id);

        entity.Property(t => t.Id)
            .ValueGeneratedOnAdd();

        // Database-specific default value
        if (Database.IsSqlite())
        {
            // SQLite: Use randomblob
            entity.Property(t => t.PublicId)
                .HasDefaultValueSql("lower(hex(randomblob(16)))");
        }
        else if (Database.IsNpgsql())
        {
            // PostgreSQL: Use gen_random_uuid
            entity.Property(t => t.PublicId)
                .HasDefaultValueSql("gen_random_uuid()");
        }

        entity.HasIndex(t => t.PublicId)
            .IsUnique();
    });
}
```

#### Helper Extension

```csharp
public static class DatabaseExtensions
{
    public static bool IsSqlite(this DatabaseFacade database)
        => database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";

    public static bool IsNpgsql(this DatabaseFacade database)
        => database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL";
}
```

**Advantages**:
- ✅ Database generates UUIDs automatically
- ✅ No need to set PublicId in code
- ✅ Works in both databases

**Disadvantages**:
- ❌ SQLite randomblob format differs from standard GUID format
- ❌ Database-specific code in model configuration
- ❌ Migrations can be tricky if switching databases

---

### ⚠️ Solution 3: SQLite GUID Extension

Load a SQLite extension that provides UUID functions.

#### Using `uuid` Extension

```csharp
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
{
    if (optionsBuilder.IsConfigured)
        return;

    optionsBuilder.UseSqlite("Data Source=app.db", options =>
    {
        options.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery);
    });

    // Load UUID extension after connection opens
    var connection = Database.GetDbConnection();
    connection.Open();
    using var command = connection.CreateCommand();
    command.CommandText = "SELECT load_extension('uuid');";
    command.ExecuteNonQuery();
}

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    entity.Property(t => t.PublicId)
        .HasDefaultValueSql("uuid()");  // Uses extension
}
```

**Advantages**:
- ✅ Proper UUID format
- ✅ Database-generated

**Disadvantages**:
- ❌ Requires SQLite extension installation
- ❌ May not work in all environments (Azure, containers)
- ❌ Additional deployment complexity
- ❌ Still different from PostgreSQL

---

### ❌ Solution 4: Trigger-Based Generation

Create a SQLite trigger to generate UUIDs.

```sql
CREATE TRIGGER tr_transactions_publicid
AFTER INSERT ON Transactions
WHEN NEW.PublicId IS NULL
BEGIN
    UPDATE Transactions
    SET PublicId = lower(hex(randomblob(16)))
    WHERE Id = NEW.Id;
END;
```

**Disadvantages**:
- ❌ Adds complexity
- ❌ Different behavior from PostgreSQL
- ❌ Hard to test
- ❌ Not recommended for new projects

---

## Recommended Approach for YoFi.V3

### Use Solution 1: Application-Generated UUIDs

```csharp
// Base class ensures all entities get PublicId
public abstract class BaseModel : IModel
{
    public long Id { get; set; }
    public Guid PublicId { get; private set; }

    protected BaseModel()
    {
        PublicId = Guid.NewGuid();
    }
}

// Entities inherit from BaseModel
public class Tenant : BaseModel
{
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    // ...
}

public class Transaction : BaseModel, ITenantModel
{
    public long TenantId { get; set; }
    public decimal Amount { get; set; }
    // ...
}
```

### Database Configuration (Works for Both SQLite & PostgreSQL)

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // Configure all IModel entities
    foreach (var entityType in modelBuilder.Model.GetEntityTypes())
    {
        if (typeof(IModel).IsAssignableFrom(entityType.ClrType))
        {
            modelBuilder.Entity(entityType.ClrType, entity =>
            {
                entity.Property("PublicId")
                    .IsRequired();

                entity.HasIndex("PublicId")
                    .IsUnique();
            });
        }
    }

    // Specific entity configurations
    modelBuilder.Entity<Tenant>(entity =>
    {
        entity.HasKey(t => t.Id);
        entity.Property(t => t.Id).ValueGeneratedOnAdd();
        // PublicId configured by convention above
    });
}
```

### Testing

```csharp
[Test]
public void NewEntity_ShouldHavePublicId()
{
    var tenant = new Tenant { Name = "Test" };

    Assert.That(tenant.PublicId, Is.Not.EqualTo(Guid.Empty));
    Assert.That(tenant.PublicId, Is.Not.EqualTo(default(Guid)));
}

[Test]
public void TwoNewEntities_ShouldHaveDifferentPublicIds()
{
    var tenant1 = new Tenant { Name = "Test1" };
    var tenant2 = new Tenant { Name = "Test2" };

    Assert.That(tenant1.PublicId, Is.Not.EqualTo(tenant2.PublicId));
}
```

## Benefits for Cross-Database Compatibility

1. **Identical behavior** in SQLite and PostgreSQL
2. **No migration issues** when switching databases
3. **Testable** GUID generation
4. **Predictable** - GUIDs exist before database interaction
5. **Simple** - no database-specific configuration

## When Database Generation Might Be Better

Consider database-generated UUIDs if:
- You need database-generated timestamps or other computed values
- You're importing data from external sources without GUIDs
- You have legacy data migration requirements

For YoFi.V3, application-generated UUIDs are the cleanest solution.
