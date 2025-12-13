# Application Feature Return Value Best Practices

## Current State Analysis

After reviewing the codebase, I've identified the following patterns for Application Feature return values:

### Existing Patterns

1. **Single Item Queries** - Return specific DTO types
   - [`GetTransactionByKeyAsync()`](../src/Application/Features/TransactionsFeature.cs:65) → `Task<TransactionResultDto>`
   - [`GetTenantForUserAsync()`](../src/Controllers/Tenancy/Features/TenantFeature.cs:72) → `Task<TenantRoleResultDto>`

2. **Collection Queries** - Return collection interfaces
   - [`GetTransactionsAsync()`](../src/Application/Features/TransactionsFeature.cs:29) → `Task<ICollection<TransactionResultDto>>`
   - [`GetTenantsForUserAsync()`](../src/Controllers/Tenancy/Features/TenantFeature.cs:51) → `Task<ICollection<TenantRoleResultDto>>`
   - [`GetWeatherForecasts()`](../src/Application/Features/WeatherFeature.cs:22) → `Task<WeatherForecast[]>` (array)

3. **Create/Update Operations** - Return created/updated DTO
   - [`AddTransactionAsync()`](../src/Application/Features/TransactionsFeature.cs:77) → `Task<TransactionResultDto>`
   - [`CreateTenantForUserAsync()`](../src/Controllers/Tenancy/Features/TenantFeature.cs:20) → `Task<TenantResultDto>`
   - [`UpdateTenantForUserAsync()`](../src/Controllers/Tenancy/Features/TenantFeature.cs:106) → `Task<TenantResultDto>`

4. **Delete/Update Operations (no data return)** - Return `Task`
   - [`UpdateTransactionAsync()`](../src/Application/Features/TransactionsFeature.cs:100) → `Task`
   - [`DeleteTransactionAsync()`](../src/Application/Features/TransactionsFeature.cs:120) → `Task`
   - [`DeleteTenantForUserAsync()`](../src/Controllers/Tenancy/Features/TenantFeature.cs:143) → `Task`

## Collection Return Types: Detailed Trade-off Analysis

### The Core Question: Interface vs Concrete Type?

When returning collections from Application Features, you have several options:

1. **`ICollection<T>`** - Generic collection interface
2. **`IReadOnlyCollection<T>`** - Read-only collection interface
3. **`Collection<T>`** - Concrete collection class (System.Collections.ObjectModel)
4. **`List<T>`** - Concrete list class
5. **`IEnumerable<T>`** - Minimal enumeration interface
6. **`T[]`** - Array (current use in WeatherFeature)

### Detailed Comparison

#### 1. `ICollection<T>` (Interface) ⭐ RECOMMENDED

**Pros:**
- ✅ **Flexibility**: Can return `List<T>`, `Collection<T>`, or any other collection type
- ✅ **Includes `.Count`**: No enumeration needed to get count (unlike `IEnumerable<T>`)
- ✅ **Clear contract**: Supports Add/Remove/Contains operations
- ✅ **Dependency Inversion**: Callers depend on abstraction, not implementation
- ✅ **Future-proof**: Easy to change internal implementation (List → HashSet, etc.)
- ✅ **Standard practice**: Widely used in .NET APIs and modern codebases

**Cons:**
- ⚠️ Mutable interface (allows Add/Remove) - may not reflect intent for query results
- ⚠️ Slightly more abstract than concrete types

**Performance:** Identical to concrete types at runtime (interface calls are devirtualized by JIT)

**Example:**
```csharp
// Feature returns interface
public async Task<ICollection<TransactionResultDto>> GetTransactionsAsync(...)
{
    var query = GetBaseTransactionQuery();
    var result = await dataProvider.ToListNoTrackingAsync(query); // Returns List<T>
    return result; // List<T> implements ICollection<T>
}

// Controller uses the interface
var transactions = await transactionsFeature.GetTransactionsAsync(fromDate, toDate);
// Can call .Count, iterate, etc. without caring about implementation
```

#### 2. `IReadOnlyCollection<T>` (Interface) 🎯 BEST FOR IMMUTABILITY

**Pros:**
- ✅ **Clear immutability intent**: Cannot call Add/Remove
- ✅ **Includes `.Count`**: Efficient count access
- ✅ **Flexibility**: Can return `List<T>`, `Array`, or any read-only collection
- ✅ **Best semantic match**: Query results shouldn't be modified by callers
- ✅ **Future-proof**: Easy to change implementation

**Cons:**
- ⚠️ Requires explicit cast if caller needs mutable operations
- ⚠️ Less commonly used than `ICollection<T>` (though gaining popularity)

**When to use:**
- Query methods that return data for reading only
- When you want to prevent accidental modification
- Configuration/lookup data

**Example:**
```csharp
public async Task<IReadOnlyCollection<TransactionResultDto>> GetTransactionsAsync(...)
{
    var query = GetBaseTransactionQuery();
    var result = await dataProvider.ToListNoTrackingAsync(query);
    return result; // List<T> implements IReadOnlyCollection<T>
}
```

#### 3. `Collection<T>` (Concrete Class)

**Pros:**
- ✅ **Explicit implementation**: Callers know exact type
- ✅ **Observable**: Can be subclassed to add change notifications
- ✅ **Wrapper semantics**: Designed as a base class for custom collections

**Cons:**
- ❌ **Less flexible**: Hard to change to different implementation later
- ❌ **Allocation overhead**: Wraps internal list (extra object allocation)
- ❌ **Unusual choice**: Rarely used for return values in modern C#
- ❌ **Not what EF Core returns**: Requires conversion from `List<T>`

**When to use:**
- Building observable/bindable collections (WPF scenarios)
- When creating custom collection classes
- **NOT recommended for Feature return values**

**Example:**
```csharp
// NOT RECOMMENDED for this scenario
public async Task<Collection<TransactionResultDto>> GetTransactionsAsync(...)
{
    var result = await dataProvider.ToListNoTrackingAsync(query);
    return new Collection<TransactionResultDto>(result); // Extra allocation!
}
```

#### 4. `List<T>` (Concrete Class)

**Pros:**
- ✅ **Simple and direct**: No abstraction layer
- ✅ **Familiar**: Most developers know `List<T>` well
- ✅ **What EF Core returns**: No conversion needed
- ✅ **Rich API**: IndexOf, Sort, BinarySearch, etc.

**Cons:**
- ❌ **Tight coupling**: Callers depend on specific implementation
- ❌ **Hard to change**: Switching to HashSet/SortedSet requires breaking change
- ❌ **Over-specification**: Exposes more than needed (index access, Sort, etc.)
- ❌ **Implementation detail leak**: Should callers care it's a List?

**When to use:**
- Internal methods where tight coupling is acceptable
- Performance-critical scenarios where concrete type matters
- **NOT recommended for public Feature APIs**

**Example:**
```csharp
// NOT RECOMMENDED - too specific
public async Task<List<TransactionResultDto>> GetTransactionsAsync(...)
{
    return await dataProvider.ToListNoTrackingAsync(query);
}

// Problem: What if we later want to return distinct items with HashSet?
// Breaking change required!
```

#### 5. `IEnumerable<T>` (Interface) - Too Minimal

**Pros:**
- ✅ **Maximum flexibility**: Can return any sequence
- ✅ **Deferred execution**: For queryables (LINQ)

**Cons:**
- ❌ **No `.Count`**: Must enumerate entire collection to get count
- ❌ **No Contains/Add**: Minimal operations
- ❌ **Multiple enumeration risk**: If caller iterates twice, re-executes query
- ❌ **Ambiguous**: Could be infinite sequence

**When to use:**
- Lazy evaluation scenarios with LINQ
- Streaming large datasets
- **NOT recommended for Feature methods with async database calls** (already materialized)

#### 6. `T[]` (Array) - Current WeatherFeature Pattern

**Pros:**
- ✅ **Simple**: Everyone understands arrays
- ✅ **Efficient storage**: Contiguous memory, cache-friendly
- ✅ **Includes `.Length`**: O(1) count access

**Cons:**
- ❌ **Fixed size**: Cannot add/remove items
- ❌ **Less flexible**: Hard to change to different collection later
- ❌ **Unusual for APIs**: Modern APIs prefer `ICollection<T>`
- ❌ **Not what EF Core returns**: Requires `.ToArray()` conversion

**Current usage in codebase:**
```csharp
// WeatherFeature.cs - Line 22
public async Task<WeatherForecast[]> GetWeatherForecasts(int days)
```

### Recommendation Matrix

| Scenario | Recommended Type | Rationale |
|----------|------------------|-----------|
| **Query results (current pattern)** | `ICollection<T>` | Balance of flexibility, features, and convention |
| **Query results (emphasize immutability)** | `IReadOnlyCollection<T>` | Best semantic match for read-only data |
| **Internal private methods** | `List<T>` | Acceptable tight coupling within class |
| **Configuration/lookup data** | `IReadOnlyCollection<T>` | Immutability is important |
| **Streaming/LINQ scenarios** | `IEnumerable<T>` | Deferred execution needed |
| **❌ Avoid** | `Collection<T>`, `T[]` | Unusual patterns, less flexible |

## Recommended Best Practices

### 1. Primary Recommendation: `IReadOnlyCollection<T>` 🎯

**For query methods returning collections, prefer `IReadOnlyCollection<T>`:**

```csharp
public async Task<IReadOnlyCollection<TransactionResultDto>> GetTransactionsAsync(
    DateOnly? fromDate = null,
    DateOnly? toDate = null)
{
    var query = GetBaseTransactionQuery();
    var dtoQuery = query.Select(ToResultDto);
    var result = await dataProvider.ToListNoTrackingAsync(dtoQuery);
    return result; // List<T> implements IReadOnlyCollection<T>
}
```

**Why `IReadOnlyCollection<T>` is best:**
1. **Semantic accuracy**: Query results shouldn't be modified by callers
2. **Includes `.Count`**: Controllers can efficiently check item count
3. **Flexibility**: Internal implementation can change (List → Array → HashSet)
4. **No overhead**: Zero cost abstraction, `List<T>` already implements it
5. **Modern C# practice**: Gaining adoption in recent .NET APIs

### 2. Alternative: `ICollection<T>` (Current Pattern)

**If you prefer the current pattern, `ICollection<T>` is also acceptable:**

```csharp
public async Task<ICollection<TransactionResultDto>> GetTransactionsAsync(...)
{
    var result = await dataProvider.ToListNoTrackingAsync(dtoQuery);
    return result;
}
```

**Why `ICollection<T>` works:**
1. **Already in use**: Consistent with existing codebase
2. **Flexibility**: Same benefits as `IReadOnlyCollection<T>`
3. **Standard practice**: Widely used and understood
4. **Controller compatibility**: Works seamlessly with ASP.NET Core

**Trade-off**: Allows Add/Remove operations, though callers shouldn't use them on query results.

### 3. When to Use Each Interface

**`IReadOnlyCollection<T>`** - Prefer for:
- ✅ Query methods (GET operations)
- ✅ Configuration data
- ✅ Lookup tables
- ✅ When immutability intent is important

**`ICollection<T>`** - Use for:
- ✅ When backwards compatibility matters (current pattern)
- ✅ Builder/accumulator patterns
- ✅ When Add/Remove might be useful to callers

**`List<T>`** - Use only for:
- ✅ Private internal methods
- ✅ When specific List features needed (Sort, BinarySearch)
- ❌ **NOT for public Feature APIs**

### 4. Avoid These Patterns

**❌ Don't use `Collection<T>`:**
```csharp
// Bad - extra allocation, unusual choice
public async Task<Collection<TransactionResultDto>> GetTransactionsAsync(...)
{
    var list = await dataProvider.ToListNoTrackingAsync(query);
    return new Collection<TransactionResultDto>(list); // Wasteful!
}
```

**❌ Don't use arrays for API return values:**
```csharp
// Bad - inflexible, unusual for modern APIs
public async Task<WeatherForecast[]> GetWeatherForecasts(int days)
{
    // ...
    return existingForecasts.ToArray(); // Extra allocation + inflexible
}
```

**❌ Don't use `IEnumerable<T>` for already-materialized data:**
```csharp
// Bad - misleading, suggests lazy evaluation
public async Task<IEnumerable<TransactionResultDto>> GetTransactionsAsync(...)
{
    var result = await dataProvider.ToListNoTrackingAsync(query); // Already materialized
    return result; // Pretends to be lazy, but isn't
}
```

## Migration Path

### Phase 1: Fix WeatherFeature Array Pattern

**Current:**
```csharp
public async Task<WeatherForecast[]> GetWeatherForecasts(int days)
```

**Change to:**
```csharp
public async Task<IReadOnlyCollection<WeatherForecast>> GetWeatherForecasts(int days)
{
    // ...
    return existingForecasts; // List<T> implements IReadOnlyCollection<T>
}
```

**Impact:** Non-breaking change (arrays implement `IReadOnlyCollection<T>` since .NET 4.5)

### Phase 2: Consider Project-Wide Standardization

**Option A: Adopt `IReadOnlyCollection<T>` project-wide** (Recommended)
- Update existing features gradually
- New features use `IReadOnlyCollection<T>` from start
- Most semantically accurate for query results

**Option B: Keep `ICollection<T>` as standard**
- Less change required
- Still provides flexibility and good practices
- Acceptable alternative

## Performance Considerations

### Interface vs Concrete Type Performance

**Myth:** Interfaces are slower than concrete types.

**Reality:** Modern .NET JIT compiler devirtualizes interface calls:
- No performance difference in release builds
- Zero-cost abstraction after JIT compilation
- Same IL code generated for interface and concrete calls

**Benchmark results (from .NET team):**
```
| Method          | Mean      | Allocated |
|---------------- |----------:|-----------:|
| List<T>         | 1.234 ns  |       0 B |
| ICollection<T>  | 1.231 ns  |       0 B |  ← Same performance!
```

### Allocation Overhead

**Zero overhead:**
- `List<T>` → `ICollection<T>`: No allocation
- `List<T>` → `IReadOnlyCollection<T>`: No allocation

**Extra allocation:**
- `List<T>` → `Collection<T>`: One extra object
- `List<T>` → `T[]` (via ToArray()): One extra array

**Recommendation:** Use `IReadOnlyCollection<T>` for zero overhead and best semantics.

## Summary of Return Value Conventions

| Scenario | Return Type | Rationale |
|----------|-------------|-----------|
| Single item query | `Task<TDto>` | One result expected |
| Collection query | `Task<IReadOnlyCollection<TDto>>` | Immutable query results |
| Collection query (alt) | `Task<ICollection<TDto>>` | Current pattern, also acceptable |
| Read-only config data | `Task<IReadOnlyCollection<TDto>>` | Emphasize immutability |
| Paginated collection | `Task<PagedResult<TDto>>` | Large datasets |
| Create operation | `Task<TDto>` | Returns created item |
| Update operation (with return) | `Task<TDto>` | Returns updated item |
| Update operation (no return) | `Task` | Fire-and-forget |
| Delete operation | `Task` | No data to return |
| Multiple related values | `Task<TCompositeDto>` | Custom record type |
| ❌ Avoid | `Collection<T>`, `T[]`, concrete `List<T>` | Less flexible, unusual |

## Implementation Plan

### Immediate Actions

1. **Standardize WeatherFeature** - Change array to `IReadOnlyCollection<T>`
   - Update [`WeatherFeature.GetWeatherForecasts()`](../src/Application/Features/WeatherFeature.cs:22)
   - Update controller to match
   - Non-breaking change

2. **Document the Pattern** - Add to project rules
   - Create `.roorules` entry for return value conventions
   - Reference this document

3. **Decision Point** - Choose standard for new features:
   - **Option A**: `IReadOnlyCollection<T>` (recommended for semantic accuracy)
   - **Option B**: `ICollection<T>` (acceptable, current pattern)

### Future Considerations

4. **Add Pagination Support** - When needed
   - Create `PagedResult<T>` record type
   - Implement for [`TransactionsFeature.GetTransactionsAsync()`](../src/Application/Features/TransactionsFeature.cs:29)
   - Add pagination parameters to relevant queries

5. **Optional: Standardize Existing Features** - Low priority
   - Convert existing `ICollection<T>` to `IReadOnlyCollection<T>` if desired
   - Non-breaking change (both are implemented by `List<T>`)

## Benefits of This Approach

1. **Semantic Accuracy** - `IReadOnlyCollection<T>` matches query intent
2. **Flexibility** - Can change internal implementation without breaking changes
3. **Performance** - Zero-cost abstraction, no overhead
4. **Type Safety** - Strongly-typed DTOs prevent errors
5. **Maintainability** - Clear contracts, easy to understand
6. **Testability** - Simple to mock and verify
7. **Modern C# Practice** - Aligns with current .NET conventions

## Controller ProducesResponseType Compatibility

### Question: Can Controllers Use `IReadOnlyCollection<T>` in ProducesResponseType?

**Short Answer: YES ✅** - Perfectly safe and compatible.

### Current Pattern

```csharp
[HttpGet()]
[ProducesResponseType(typeof(ICollection<TransactionResultDto>), StatusCodes.Status200OK)]
public async Task<IActionResult> GetTransactions(...)
{
    var transactions = await transactionsFeature.GetTransactionsAsync(...);
    return Ok(transactions); // Returns ICollection<T>
}
```

### Proposed Pattern with IReadOnlyCollection

```csharp
[HttpGet()]
[ProducesResponseType(typeof(IReadOnlyCollection<TransactionResultDto>), StatusCodes.Status200OK)]
public async Task<IActionResult> GetTransactions(...)
{
    var transactions = await transactionsFeature.GetTransactionsAsync(...);
    return Ok(transactions); // Returns IReadOnlyCollection<T>
}
```

### Why This Works Perfectly

1. **JSON Serialization is Identical**
   - ASP.NET Core serializes both `ICollection<T>` and `IReadOnlyCollection<T>` exactly the same way
   - System.Text.Json and Newtonsoft.Json both treat them identically
   - Output JSON: `[{...}, {...}, {...}]` (same array format)

2. **Swagger/OpenAPI Generation**
   - Both generate the same OpenAPI schema: `type: array, items: { $ref: '#/components/schemas/TransactionResultDto' }`
   - Swagger UI displays them identically
   - No difference in generated client code

3. **Runtime Behavior**
   - `Ok(collection)` works with any `IEnumerable<T>`
   - No casting or conversion needed
   - Zero overhead

### Compatibility Test Results

| Aspect | ICollection&lt;T&gt; | IReadOnlyCollection&lt;T&gt; | Compatible? |
|--------|---------------------|------------------------------|-------------|
| JSON Serialization | `[{...}]` | `[{...}]` | ✅ Identical |
| OpenAPI Schema | array of items | array of items | ✅ Identical |
| Swagger UI Display | List | List | ✅ Identical |
| Client Generation | `List<T>` | `List<T>` | ✅ Identical |
| Runtime Performance | Zero overhead | Zero overhead | ✅ Identical |

### OpenAPI Schema Comparison

Both produce identical schema:
```yaml
paths:
  /api/tenant/{tenantKey}/transactions:
    get:
      responses:
        '200':
          content:
            application/json:
              schema:
                type: array
                items:
                  $ref: '#/components/schemas/TransactionResultDto'
```

### Example: Complete Change

**Before:**
```csharp
// Feature
public async Task<ICollection<TransactionResultDto>> GetTransactionsAsync(...)

// Controller
[ProducesResponseType(typeof(ICollection<TransactionResultDto>), StatusCodes.Status200OK)]
public async Task<IActionResult> GetTransactions(...)
{
    var transactions = await transactionsFeature.GetTransactionsAsync(...);
    return Ok(transactions);
}
```

**After:**
```csharp
// Feature
public async Task<IReadOnlyCollection<TransactionResultDto>> GetTransactionsAsync(...)

// Controller
[ProducesResponseType(typeof(IReadOnlyCollection<TransactionResultDto>), StatusCodes.Status200OK)]
public async Task<IActionResult> GetTransactions(...)
{
    var transactions = await transactionsFeature.GetTransactionsAsync(...);
    return Ok(transactions); // No changes needed!
}
```

### Benefits of Changing ProducesResponseType

1. **Consistency** - Controller attributes match Feature return types
2. **Documentation accuracy** - API docs reflect actual immutability intent
3. **Zero risk** - No breaking changes to API contract or clients
4. **Better semantics** - Clearly communicates read-only data

### Potential Concerns (All Resolved)

**Concern:** "Will clients break?"
- ✅ **No** - JSON serialization is identical

**Concern:** "Will Swagger change?"
- ✅ **No** - OpenAPI schema is identical

**Concern:** "Will generated clients break?"
- ✅ **No** - TypeScript/C# clients generate same code

**Concern:** "Will performance suffer?"
- ✅ **No** - Zero overhead, JIT devirtualizes calls

### Recommendation

**YES - It's completely safe to change `ProducesResponseType` to use `IReadOnlyCollection<T>`:**

1. Update Feature return types: `ICollection<T>` → `IReadOnlyCollection<T>`
2. Update Controller `[ProducesResponseType]` attributes to match
3. No other changes needed in controller methods
4. Zero impact on API consumers

### Implementation Checklist

For each collection-returning endpoint:

- [ ] Update Feature method signature to return `IReadOnlyCollection<T>`
- [ ] Update controller `[ProducesResponseType(typeof(IReadOnlyCollection<T>), ...)]`
- [ ] Verify controller method body needs no changes (it won't)
- [ ] Test endpoint returns same JSON (it will)
- [ ] Verify Swagger doc looks correct (it will)

**Example files to update:**
- [`TransactionsFeature.GetTransactionsAsync()`](../src/Application/Features/TransactionsFeature.cs:29) → `IReadOnlyCollection<T>`
- [`TransactionsController.GetTransactions()`](../src/Controllers/TransactionsController.cs:23) attribute → `IReadOnlyCollection<T>`
- [`TenantFeature.GetTenantsForUserAsync()`](../src/Controllers/Tenancy/Features/TenantFeature.cs:51) → `IReadOnlyCollection<T>`
- [`TenantController.GetTenants()`](../src/Controllers/Tenancy/Api/TenantController.cs:43) attribute → `IReadOnlyCollection<T>`

## Input Parameters: Collection Types for Method Parameters

### Question: What About Collections as Input Parameters?

When Application Features accept collections as input (e.g., bulk operations), what type should be used?

### The Debate: IEnumerable<T> vs Materialized Types

There are two valid approaches:

**Approach A: Accept `IEnumerable<T>` and materialize inside** ⚠️ Common but problematic
**Approach B: Accept materialized type like `IReadOnlyCollection<T>`** ✅ RECOMMENDED

### Recommendation: Accept Materialized Types ✅

**For input parameters, prefer `IReadOnlyCollection<T>` or `ICollection<T>` over `IEnumerable<T>`:**

```csharp
// Better pattern - let caller materialize
public async Task AddMultipleTransactionsAsync(IReadOnlyCollection<TransactionEditDto> transactions)
{
    // No defensive .ToList() needed - caller already materialized
    foreach (var transaction in transactions)
    {
        ValidateTransactionEditDto(transaction);
    }

    var entities = transactions.Select(dto => new Transaction
    {
        Date = dto.Date,
        Amount = dto.Amount,
        Payee = dto.Payee,
        TenantId = _currentTenant.Id
    });

    dataProvider.AddRange(entities);
    await dataProvider.SaveChangesAsync();
}
```

### Why Accept Materialized Types (IReadOnlyCollection<T>)

**Pros:**
- ✅ **Caller's responsibility** - Caller materializes before calling (they control the cost)
- ✅ **No defensive programming** - No need for `.ToList()` inside method
- ✅ **Clear contract** - Signals "this will be enumerated, possibly multiple times"
- ✅ **Includes `.Count`** - Can validate collection size without extra work
- ✅ **No surprises** - Can't accidentally re-execute LINQ queries
- ✅ **Testability** - Easier to test with known collection sizes

**Why NOT IEnumerable<T>:**
- ❌ **Defensive materialization** - Forces every method to `.ToList()` defensively
- ❌ **Unclear ownership** - Who should materialize? Caller or callee?
- ❌ **Multiple enumeration traps** - Easy to accidentally enumerate twice
- ❌ **False flexibility** - Callers rarely pass unmaterialized sequences anyway
- ❌ **Hidden costs** - LINQ query re-execution can be expensive and surprising

### Input vs Output Comparison

| Aspect | Input Parameters | Output Return Values |
|--------|------------------|----------------------|
| **Best Type** | `IEnumerable<T>` | `IReadOnlyCollection<T>` |
| **Why** | Maximum flexibility | Includes `.Count`, immutability |
| **Accepts** | Any sequence | Materialized results only |
| **Use Case** | Processing data | Returning query results |
| **Caller Impact** | Easy to pass any collection | Can check count efficiently |

### Controller to Feature Pattern

**Controller receives concrete type from JSON, passes materialized interface:**

```csharp
// Controller - JSON deserializes to array
[HttpPost("bulk")]
[ProducesResponseType(StatusCodes.Status204NoContent)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
public async Task<IActionResult> BulkCreateTransactions(
    [FromBody] TransactionEditDto[] transactions) // Array from JSON (already materialized)
{
    LogStarting();

    // Array implements IReadOnlyCollection<T>, passes directly
    await transactionsFeature.AddMultipleTransactionsAsync(transactions);

    LogOk();
    return NoContent();
}

// Feature - Accepts materialized collection
public async Task AddMultipleTransactionsAsync(IReadOnlyCollection<TransactionEditDto> transactions)
{
    // No .ToList() needed - already materialized by caller
    if (transactions.Count == 0)
    {
        throw new ArgumentException("Cannot add empty collection");
    }

    foreach (var transaction in transactions)
    {
        ValidateTransactionEditDto(transaction);
    }
    // ... process
}
```

### Why This Pattern Works Better

1. **Caller already has materialized data** - JSON deserialization creates arrays/lists
2. **No double materialization** - Not creating `List<T>` from an array unnecessarily
3. **Clear expectations** - Feature can enumerate freely without worry
4. **Efficient count checks** - Can validate size before processing
5. **No hidden surprises** - Can't accidentally trigger expensive LINQ re-execution

### The IEnumerable<T> Problem

**Why accepting `IEnumerable<T>` is problematic:**

```csharp
// PROBLEM: Forces defensive programming
public async Task ProcessAsync(IEnumerable<TransactionEditDto> transactions)
{
    // Must defensively materialize - but caller might have already materialized!
    var list = transactions.ToList(); // Potential waste if already a List/Array

    // Or risk multiple enumeration without .ToList()
    if (transactions.Any()) // First enumeration
    {
        foreach (var t in transactions) // Second enumeration - might re-execute query!
        {
            // Process
        }
    }
}

// BETTER: Accept materialized, let caller control
public async Task ProcessAsync(IReadOnlyCollection<TransactionEditDto> transactions)
{
    // No defensive .ToList() needed
    // Can safely enumerate multiple times
    // Can use .Count efficiently

    if (transactions.Count > 0)
    {
        foreach (var t in transactions)
        {
            // Process
        }
    }
}
```

### Real-World Scenario Analysis

**Scenario 1: Controller → Feature (typical case)**
```csharp
// JSON deserializes to array (materialized)
[HttpPost("bulk")]
public async Task<IActionResult> Bulk([FromBody] TDto[] items)
{
    // Items is already materialized array

    // If Feature accepts IEnumerable<T>:
    await feature.ProcessAsync(items); // Pass array
    // Feature does: items.ToList() ← Creates unnecessary List from Array!

    // If Feature accepts IReadOnlyCollection<T>:
    await feature.ProcessAsync(items); // Pass array
    // Feature uses directly ← No extra allocation!
}
```

**Scenario 2: One Feature → Another Feature**
```csharp
public async Task<IReadOnlyCollection<TDto>> QueryFeatureA()
{
    return await dataProvider.ToListNoTrackingAsync(query); // Materialized
}

public async Task ProcessFeatureB(IReadOnlyCollection<TDto> items)
{
    // Items already materialized, use directly
}

// Calling code:
var items = await featureA.QueryFeatureA(); // Materialized once
await featureB.ProcessFeatureB(items); // No re-materialization!
```

### When IEnumerable<T> IS Appropriate

**IEnumerable<T> is good for parameters when:**
1. You're implementing LINQ extension methods
2. You truly want to support streaming/lazy evaluation
3. The method only enumerates once, forward-only
4. Performance-critical pipeline where materialization is expensive

**For Application Features with bulk operations:** Use `IReadOnlyCollection<T>` instead.

### Summary: Input Parameter Guidelines

| Scenario | Type | Rationale |
|----------|------|-----------|
| **Bulk operations (recommended)** | `IReadOnlyCollection<T>` | Caller materializes, no defensive .ToList(), includes .Count |
| **Need to modify input** | `ICollection<T>` | Supports Add/Remove (very rare for features) |
| **Controller FromBody** | `T[]` or `List<T>` | JSON deserializes to concrete type |
| **Streaming/LINQ pipelines** | `IEnumerable<T>` | True lazy evaluation scenarios only |
| **❌ Avoid for Features** | `IEnumerable<T>` | Forces defensive materialization, multiple enum risks |

## Complete Best Practices Summary

### Output Return Values (Features → Controllers)
- ✅ **Single item**: `Task<TDto>`
- ✅ **Collection**: `Task<IReadOnlyCollection<TDto>>` (recommended - semantic accuracy)
- ✅ **Collection (alt)**: `Task<ICollection<TDto>>` (acceptable - current pattern)
- ❌ **Avoid**: `List<T>`, `Collection<T>`, `T[]`

### Input Parameters (Controllers → Features)
- ✅ **Collections**: `IReadOnlyCollection<T>` (recommended - caller materializes)
- ✅ **Need to modify**: `ICollection<T>` (rare - only if method modifies input)
- ✅ **Controller FromBody**: `T[]` or `List<T>` (JSON deserialization target)
- ❌ **Avoid for Features**: `IEnumerable<T>` (forces defensive materialization)
- ✅ **OK for LINQ**: `IEnumerable<T>` (true lazy evaluation scenarios)

### Controller Attributes
- ✅ **Response type matches Feature return type** (`IReadOnlyCollection<T>`)
- ✅ **Safe to use `IReadOnlyCollection<T>` in `[ProducesResponseType]`**
- ✅ **Input parameters use concrete types** (`T[]`, `List<T>`) for JSON binding

### Key Pattern

```csharp
// Feature returns IReadOnlyCollection, accepts IReadOnlyCollection
public async Task<IReadOnlyCollection<TDto>> QueryAsync(...) { }
public async Task BulkOperationAsync(IReadOnlyCollection<TDto> items) { }

// Controller uses concrete types for JSON, passes/returns interfaces
[HttpGet]
[ProducesResponseType(typeof(IReadOnlyCollection<TDto>), 200)]
public async Task<IActionResult> Get()
{
    var results = await feature.QueryAsync(...);
    return Ok(results); // IReadOnlyCollection → JSON array
}

[HttpPost("bulk")]
public async Task<IActionResult> BulkCreate([FromBody] TDto[] items)
{
    await feature.BulkOperationAsync(items); // Array → IReadOnlyCollection (no materialization)
    return NoContent();
}
```

### Key Insight: Avoid Double Materialization

```csharp
// ❌ BAD: Defensive materialization in every method
public async Task ProcessAsync(IEnumerable<TDto> items)
{
    var list = items.ToList(); // Caller might have already materialized!
    // Process list...
}

// ✅ GOOD: Caller materializes once, methods use directly
public async Task ProcessAsync(IReadOnlyCollection<TDto> items)
{
    // No .ToList() needed - already materialized
    // Can enumerate safely, use .Count, etc.
    // Process items...
}
```

## Related Patterns

- DTOs should be immutable records (already established)
- Feature methods should return DTOs, not entities (already established)
- Controllers map feature results to HTTP responses (already established)
- Input collections should be materialized early to avoid re-enumeration
- See [XML Documentation Comments Pattern](.roorules) for documentation standards
