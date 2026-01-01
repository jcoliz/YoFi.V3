---
status: Draft
target_release: TBD
design_document: docs/wip/import-export/EXECUTEUPDATE-ABSTRACTION-DESIGN.md
---

# ExecuteUpdateAsync Abstraction Design

## Problem Statement

The [`ImportReviewFeature`](../../../src/Application/Features/ImportReviewFeature.cs) contains three methods that update selection state using a load-update-save pattern:

- [`SetSelectionAsync()`](../../../src/Application/Features/ImportReviewFeature.cs:382) (lines 382-396)
- [`SelectAllAsync()`](../../../src/Application/Features/ImportReviewFeature.cs:401) (lines 401-413)
- [`DeselectAllAsync()`](../../../src/Application/Features/ImportReviewFeature.cs:418) (lines 418-430)

These methods load entities into memory, modify them, and save changes. For potentially large datasets (hundreds or thousands of transactions), this is inefficient compared to EF Core's `ExecuteUpdateAsync()` which performs bulk updates directly in the database.

### The Challenge

EF Core's `ExecuteUpdateAsync()` signature contains EF Core-specific types:

```csharp
// From Microsoft.EntityFrameworkCore namespace
Task<int> ExecuteUpdateAsync<TSource>(
    this IQueryable<TSource> source,
    Expression<Func<SetPropertyCalls<TSource>, SetPropertyCalls<TSource>>> setPropertyCalls,
    CancellationToken cancellationToken = default);
```

The `SetPropertyCalls<T>` type is defined in `Microsoft.EntityFrameworkCore` and cannot be referenced from the Application layer without violating Clean Architecture boundaries. The Application layer cannot depend on EF Core.

### Current Workaround

We already expose [`ExecuteDeleteAsync()`](../../../src/Entities/Providers/IDataProvider.cs:93) successfully because it only requires a query and doesn't expose EF Core-specific types:

```csharp
// IDataProvider.cs
Task<int> ExecuteDeleteAsync<T>(IQueryable<T> query) where T : class;

// ApplicationDbContext.cs
Task<int> IDataProvider.ExecuteDeleteAsync<T>(IQueryable<T> query)
    => query.ExecuteDeleteAsync();
```

## Design Constraints

1. **Clean Architecture**: Application layer (`src/Application`) cannot reference EF Core packages
2. **Type Safety**: Solution should maintain compile-time type safety
3. **Reusability**: Should support updates to any entity type, not just `ImportReviewTransaction`
4. **Simplicity**: Should not significantly complicate the codebase
5. **Performance**: Must avoid loading entities into memory for bulk updates

## Solution Options

### Option 1: Property Setter Delegate Pattern (Recommended)

Use a simple delegate-based abstraction that captures property name and value.

#### Interface Design

```csharp
// In IDataProvider.cs
public delegate void PropertySetter<T>(T entity);

Task<int> ExecuteUpdateAsync<T>(
    IQueryable<T> query,
    PropertySetter<T> propertySetter,
    CancellationToken cancellationToken = default)
    where T : class;
```

#### Implementation

```csharp
// In ApplicationDbContext.cs
Task<int> IDataProvider.ExecuteUpdateAsync<T>(
    IQueryable<T> query,
    PropertySetter<T> propertySetter,
    CancellationToken cancellationToken)
{
    // Create a temporary entity to capture property changes
    var tempEntity = Activator.CreateInstance<T>();
    var tracker = new PropertyChangeTracker<T>();

    // Apply the setter to track changes
    propertySetter(tempEntity);

    // Build SetPropertyCalls using reflection on the temp entity
    // This implementation detail is hidden in the Data layer
    return query.ExecuteUpdateAsync(
        s => ApplyPropertyChanges(s, tracker.GetChanges()),
        cancellationToken);
}
```

#### Usage

```csharp
// In ImportReviewFeature.cs
public async Task SelectAllAsync()
{
    await dataProvider.ExecuteUpdateAsync(
        GetTenantScopedQuery(),
        entity => entity.IsSelected = true
    );
}
```

**Pros:**
- Clean, simple API for Application layer
- Type-safe property access
- No EF Core dependencies in Application layer
- Reusable for any entity and property

**Cons:**
- Requires reflection or expression tree analysis in Data layer
- More complex implementation in `ApplicationDbContext`
- May not support multiple property updates in single call (could be extended)

---

### Option 2: Explicit Property Update Methods

Add specific methods for common update scenarios.

#### Interface Design

```csharp
// In IDataProvider.cs
Task<int> ExecuteUpdatePropertyAsync<TEntity, TProperty>(
    IQueryable<TEntity> query,
    Expression<Func<TEntity, TProperty>> propertySelector,
    TProperty newValue,
    CancellationToken cancellationToken = default)
    where TEntity : class;
```

#### Implementation

```csharp
// In ApplicationDbContext.cs
Task<int> IDataProvider.ExecuteUpdatePropertyAsync<TEntity, TProperty>(
    IQueryable<TEntity> query,
    Expression<Func<TEntity, TProperty>> propertySelector,
    TProperty newValue,
    CancellationToken cancellationToken)
{
    return query.ExecuteUpdateAsync(
        s => s.SetProperty(propertySelector, newValue),
        cancellationToken);
}
```

#### Usage

```csharp
// In ImportReviewFeature.cs
public async Task SelectAllAsync()
{
    await dataProvider.ExecuteUpdatePropertyAsync(
        GetTenantScopedQuery(),
        entity => entity.IsSelected,
        true
    );
}
```

**Pros:**
- Simple, straightforward API
- Type-safe
- No EF Core dependencies in Application layer
- Easy to implement

**Cons:**
- Only supports single property updates (by design - multi-property would use collection parameter overload)
- Less flexible than native EF Core API for complex scenarios

---

### Option 3: Builder Pattern with Fluent API

Create a builder that collects property changes and executes them.

#### Interface Design

```csharp
// In IDataProvider.cs
IUpdateBuilder<T> CreateUpdateBuilder<T>(IQueryable<T> query) where T : class;

public interface IUpdateBuilder<T>
{
    IUpdateBuilder<T> Set<TProperty>(
        Expression<Func<T, TProperty>> propertySelector,
        TProperty value);

    Task<int> ExecuteAsync(CancellationToken cancellationToken = default);
}
```

#### Usage

```csharp
// In ImportReviewFeature.cs
public async Task SelectAllAsync()
{
    await dataProvider.CreateUpdateBuilder(GetTenantScopedQuery())
        .Set(e => e.IsSelected, true)
        .ExecuteAsync();
}
```

**Pros:**
- Fluent, readable API
- Supports multiple property updates
- Clean separation of concerns

**Cons:**
- More complex - requires new interface and implementation
- Adds another abstraction layer
- Builder lifetime management concerns

---

### Option 4: Pass Query to Data Layer Method

Move update logic entirely to Data layer with specific repository methods.

#### Interface Design

```csharp
// In IDataProvider.cs or new IImportReviewRepository
Task<int> SetImportReviewSelectionAsync(
    Guid tenantId,
    IReadOnlyCollection<Guid> keys,
    bool isSelected);

Task<int> SelectAllImportReviewsAsync(Guid tenantId);

Task<int> DeselectAllImportReviewsAsync(Guid tenantId);
```

#### Usage

```csharp
// In ImportReviewFeature.cs
public async Task SelectAllAsync()
{
    await dataProvider.SelectAllImportReviewsAsync(_currentTenant.Id);
}
```

**Pros:**
- Complete separation - no abstraction needed
- Simplest for Application layer
- Data layer has full control

**Cons:**
- **Violates Clean Architecture** - Data layer would depend on domain concepts
- Not reusable for other entities
- Creates coupling between layers
- Goes against IDataProvider's generic design philosophy

---

## Recommended Solution: Option 2 (Explicit Property Update)

**Rationale:**

1. **Simplicity**: Straightforward to implement and understand
2. **Type Safety**: Full compile-time checking
3. **Clean Architecture**: No EF Core types leak to Application layer
4. **Sufficient for Current Needs**: All three update methods only need single property updates
5. **Extensibility**: Can add multi-property method later if needed
6. **Consistent with ExecuteDeleteAsync**: Similar simple signature pattern

### Signature

```csharp
/// <summary>
/// Execute bulk update query to set a single property value without loading entities into memory.
/// </summary>
/// <typeparam name="TEntity">Type of entities being updated</typeparam>
/// <typeparam name="TProperty">Type of the property being updated</typeparam>
/// <param name="query">Query defining which entities to update</param>
/// <param name="propertySelector">Expression selecting the property to update</param>
/// <param name="newValue">New value to set for the property</param>
/// <param name="cancellationToken">Cancellation token</param>
/// <returns>Number of entities updated</returns>
Task<int> ExecuteUpdatePropertyAsync<TEntity, TProperty>(
    IQueryable<TEntity> query,
    Expression<Func<TEntity, TProperty>> propertySelector,
    TProperty newValue,
    CancellationToken cancellationToken = default)
    where TEntity : class;
```

### Alternative for Multiple Properties

When future requirements need multi-property updates, the single-property method will be replaced/overloaded with a collection-based signature:

```csharp
/// <summary>
/// Execute bulk update query to set multiple property values without loading entities into memory.
/// </summary>
Task<int> ExecuteUpdatePropertiesAsync<TEntity>(
    IQueryable<TEntity> query,
    IReadOnlyCollection<(Expression<Func<TEntity, object>> PropertySelector, object NewValue)> updates,
    CancellationToken cancellationToken = default)
    where TEntity : class;

// Usage
await dataProvider.ExecuteUpdatePropertiesAsync(
    query,
    new[]
    {
        (Expression<Func<ImportReviewTransaction, object>>)(e => e.IsSelected), (object)true,
        (Expression<Func<ImportReviewTransaction, object>>)(e => e.DuplicateStatus), (object)DuplicateStatus.New
    }
);
```

**Note**: We would NOT use the single-property method for multiple properties. The moment we need multiple properties, we'll update to use a collection parameter rather than calling the single-property method multiple times.

## Implementation Plan

### Phase 1: Add Method to IDataProvider

1. Add `ExecuteUpdatePropertyAsync<TEntity, TProperty>()` to [`IDataProvider`](../../../src/Entities/Providers/IDataProvider.cs)
2. Add XML documentation explaining purpose and usage
3. Keep signature simple with property selector and value

### Phase 2: Implement in ApplicationDbContext

1. Add implementation in [`ApplicationDbContext`](../../../src/Data/Sqlite/ApplicationDbContext.cs)
2. Map to EF Core's `ExecuteUpdateAsync()` with `SetProperty()`
3. Test with simple query to verify behavior

### Phase 3: Refactor ImportReviewFeature

1. Update [`SetSelectionAsync()`](../../../src/Application/Features/ImportReviewFeature.cs:382)
2. Update [`SelectAllAsync()`](../../../src/Application/Features/ImportReviewFeature.cs:401)
3. Update [`DeselectAllAsync()`](../../../src/Application/Features/ImportReviewFeature.cs:418)
4. Remove TODO comments

### Phase 4: Testing

#### Unit Tests (Application Layer)

**Current state**: [`ImportReviewFeatureTests.cs`](../../../tests/Unit/Application/Import/ImportReviewFeatureTests.cs) only tests the `DetectDuplicate()` static method. No tests exist for `SetSelectionAsync()`, `SelectAllAsync()`, or `DeselectAllAsync()`.

**Required changes**:

1. **Add `ExecuteUpdatePropertyAsync()` to InMemoryDataProvider** ([`tests/Unit/TestHelpers/InMemoryDataProvider.cs`](../../../tests/Unit/TestHelpers/InMemoryDataProvider.cs))
   - Implement in-memory version that loads entities from query, applies property update, and returns count
   - Similar pattern to existing `ExecuteDeleteAsync()` implementation (lines 155-166)

   ```csharp
   public Task<int> ExecuteUpdatePropertyAsync<TEntity, TProperty>(
       IQueryable<TEntity> query,
       Expression<Func<TEntity, TProperty>> propertySelector,
       TProperty newValue,
       CancellationToken cancellationToken = default)
       where TEntity : class
   {
       var items = query.ToList();
       var propertyInfo = GetPropertyInfo(propertySelector);

       foreach (var item in items)
       {
           propertyInfo.SetValue(item, newValue);
       }

       return Task.FromResult(items.Count);
   }

   private static PropertyInfo GetPropertyInfo<TEntity, TProperty>(
       Expression<Func<TEntity, TProperty>> propertySelector)
   {
       if (propertySelector.Body is not MemberExpression memberExpression)
       {
           throw new ArgumentException("Property selector must be a member expression");
       }

       return (PropertyInfo)memberExpression.Member;
   }
   ```

2. **Add unit tests** for the three selection methods (optional but recommended)
   - Test `SetSelectionAsync()` with specific keys
   - Test `SelectAllAsync()` updates all entities
   - Test `DeselectAllAsync()` updates all entities
   - Verify correct count returned
   - Verify InMemoryDataProvider behavior matches expected bulk update semantics

**Note**: Since these methods don't have complex business logic (just delegation to data provider), unit tests are low priority.

#### Integration Tests (Data Layer)

**Required**: Add integration tests in `tests/Integration.Data/` to verify `ApplicationDbContext.ExecuteUpdatePropertyAsync()` implementation works correctly with actual database.

1. Create new test file: `tests/Integration.Data/ExecuteUpdatePropertyTests.cs`
2. Test scenarios:
   - Verify `ExecuteUpdatePropertyAsync()` updates entities without loading them
   - Verify tenant isolation (WHERE clause properly applied)
   - Verify method returns correct count
   - Test with different property types (bool, int, string, enum)
3. Use actual database context to test EF Core's `ExecuteUpdateAsync()` behavior

#### Integration Tests (Application Feature Layer)

**Note**: An Integration.Application test layer design exists but is not yet implemented. For this refactoring, rely on existing coverage (Unit tests + Integration.Data tests + Functional tests).

#### Functional Tests

**No changes required** - Functional tests already cover the complete workflow including selection state management. The refactoring is transparent to the API contract.

## Example Refactored Code

### Before (Current)

```csharp
public async Task SelectAllAsync()
{
    // TODO: Consider adding ExecuteUpdateAsync to IDataProvider for bulk updates without loading entities
    // For now, use load/update pattern
    var transactions = await dataProvider.ToListAsync(GetTenantScopedQuery());

    foreach (var transaction in transactions)
    {
        transaction.IsSelected = true;
    }

    await dataProvider.SaveChangesAsync();
}
```

### After (With ExecuteUpdatePropertyAsync)

```csharp
public async Task SelectAllAsync()
{
    await dataProvider.ExecuteUpdatePropertyAsync(
        GetTenantScopedQuery(),
        t => t.IsSelected,
        true
    );
}
```

**Performance Improvement:**
- Before: Load N entities into memory, modify, track changes, generate N UPDATE statements
- After: Single SQL UPDATE statement, zero memory allocation for entities

For 1000 transactions:
- Before: ~1000 entities loaded + 1000 UPDATE statements
- After: 1 UPDATE statement (e.g., `UPDATE ImportReviewTransactions SET IsSelected = 1 WHERE TenantId = @p0`)

## Future Considerations

### If Multi-Property Updates Needed

Replace/overload with collection-based signature:

```csharp
Task<int> ExecuteUpdatePropertiesAsync<TEntity>(
    IQueryable<TEntity> query,
    IReadOnlyCollection<(Expression<Func<TEntity, object>> PropertySelector, object NewValue)> updates,
    CancellationToken cancellationToken = default)
    where TEntity : class;

// Usage
await dataProvider.ExecuteUpdatePropertiesAsync(
    query,
    new[]
    {
        (Expression<Func<ImportReviewTransaction, object>>)(e => e.IsSelected), (object)true,
        (Expression<Func<ImportReviewTransaction, object>>)(e => e.DuplicateStatus), (object)DuplicateStatus.New
    }
);
```

**Important**: Do NOT call the single-property method multiple times for multiple properties. Use the collection-based overload instead to generate a single SQL UPDATE statement with multiple SET clauses.

### If Complex Update Logic Needed

For updates with computed values (e.g., increment counter), add:

```csharp
Task<int> ExecuteUpdatePropertyAsync<TEntity, TProperty>(
    IQueryable<TEntity> query,
    Expression<Func<TEntity, TProperty>> propertySelector,
    Expression<Func<TEntity, TProperty>> valueExpression,
    CancellationToken cancellationToken = default)
    where TEntity : class;

// Usage: Increment counter
await dataProvider.ExecuteUpdatePropertyAsync(
    query,
    e => e.ViewCount,
    e => e.ViewCount + 1
);
```

## Open Questions

1. **Should we add the multi-property overload immediately?**
   - Recommendation: No, YAGNI principle - add when needed

2. **Should this be in IDataProvider or a separate interface?**
   - Recommendation: IDataProvider for consistency with ExecuteDeleteAsync

3. **Should we support computed value expressions now?**
   - Recommendation: No, current use cases only need static values

4. **Naming: ExecuteUpdatePropertyAsync vs ExecuteSetPropertyAsync?**
   - Recommendation: ExecuteUpdatePropertyAsync matches EF Core naming

## References

- [EF Core ExecuteUpdate and ExecuteDelete](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-7.0/whatsnew#executeupdate-and-executedelete-bulk-updates)
- [Current ExecuteDeleteAsync in IDataProvider](../../../src/Entities/Providers/IDataProvider.cs:93)
- [Clean Architecture in YoFi.V3](../../adr/0011-clean-architecture.md)
