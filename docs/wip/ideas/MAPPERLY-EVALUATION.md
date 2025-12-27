# Object Mapping Library Evaluation for YoFi.V3

## Context

The project currently performs manual object-to-DTO mapping in several features:
- [`TransactionsFeature`](src/Application/Features/TransactionsFeature.cs) - Maps between `Transaction` and `TransactionEditDto`/`TransactionResultDto`
- [`TenantFeature`](src/Controllers/Tenancy/Features/TenantFeature.cs) - Maps between `Tenant` and `TenantEditDto`/`TenantResultDto`/`TenantRoleResultDto`

[Mapperly](https://github.com/riok/mapperly) is a source generator for object mapping that generates compile-time mapping code.

## Current Mapping Patterns

### TransactionsFeature Mapping

**Entity → DTO (Query Projection):**
```csharp
private static Expression<Func<Transaction, TransactionResultDto>> ToResultDto =>
    t => new TransactionResultDto(t.Key, t.Date, t.Amount, t.Payee);
```

**DTO → Entity (Create):**
```csharp
var newTransaction = new Transaction
{
    Date = transaction.Date,
    Amount = transaction.Amount,
    Payee = transaction.Payee,
    TenantId = _currentTenant.Id  // Context-specific
};
```

**DTO → Entity (Update):**
```csharp
existingTransaction.Date = transaction.Date;
existingTransaction.Amount = transaction.Amount;
existingTransaction.Payee = transaction.Payee;
```

### TenantFeature Mapping

**Entity → DTO:**
```csharp
return new TenantResultDto(
    Key: tenant.Key,
    Name: tenant.Name,
    Description: tenant.Description,
    CreatedAt: tenant.CreatedAt
);
```

**Entity + Role → DTO:**
```csharp
return roles.Select(utr => new TenantRoleResultDto(
    Key: utr.Tenant!.Key,
    Name: utr.Tenant.Name,
    Description: utr.Tenant.Description,
    Role: utr.Role,
    CreatedAt: utr.Tenant.CreatedAt
)).ToList();
```

**DTO → Entity:**
```csharp
var tenant = new Tenant
{
    Name = tenantDto.Name,
    Description = tenantDto.Description,
    CreatedAt = DateTimeOffset.UtcNow  // System value, not from DTO
};
```

## Mapperly Benefits

### ✅ Advantages

1. **Performance** - Generates optimized mapping code at compile time (no reflection)
2. **Type Safety** - Compile-time errors for mapping mismatches
3. **Less Boilerplate** - Reduces repetitive manual mapping code
4. **Maintainability** - Centralized mapping configuration
5. **IDE Support** - Full IntelliSense and navigation to generated code
6. **Zero Runtime Overhead** - No runtime reflection or expression compilation

### ❌ Limitations for This Project

1. **Query Projection Incompatibility**
   - Mapperly generates regular methods, not `Expression<Func<>>` for EF Core query projection
   - Current: `ToResultDto` expression compiles to SQL via EF Core
   - With Mapperly: Would require `.ToList()` then map in memory (performance hit)

2. **Context-Specific Values**
   - Current mapping often includes context values (e.g., `TenantId = _currentTenant.Id`)
   - Mapperly requires all values to come from source object or be configured
   - Would need workarounds for contextual data

3. **Simple Mappings Already**
   - Most mappings are straightforward property copies
   - Record constructors make mapping code already concise
   - Limited boilerplate to reduce

4. **Custom Logic Required**
   - Transaction validation happens during mapping
   - Update scenarios only map specific properties (not full entity replacement)
   - Mapperly adds complexity for these scenarios

## Detailed Analysis

### Transaction Mapping Scenarios

#### ✅ Could Use Mapperly
- Simple DTO → Entity mapping for creates (but with caveats for `TenantId`)

#### ❌ Should NOT Use Mapperly
- **Query projections** (`ToResultDto` expression) - Mapperly doesn't support EF Core expressions
- **Updates** - Partial property updates with validation
- **Contextual data** - `TenantId` comes from current context, not DTO

### Tenant Mapping Scenarios

#### ✅ Could Use Mapperly
- Entity → `TenantResultDto` (simple property mapping)

#### ⚠️ Challenging with Mapperly
- Entity → `TenantRoleResultDto` - Combines `Tenant` + `UserTenantRoleAssignment`
- DTO → Entity - Includes system-generated values (`CreatedAt`, `Key`)
- Collection mappings with navigation properties

## Recommendation: **DO NOT** Use Mapperly

### Primary Reasons

1. **EF Core Query Projection Incompatibility**
   - The most critical mapping (`ToResultDto` in TransactionsFeature) uses `Expression<Func<>>` for query projection
   - This allows EF Core to translate mapping to SQL
   - Mapperly cannot generate expression trees, only regular methods
   - Converting to Mapperly would require loading full entities into memory first (major performance regression)

2. **Minimal Actual Benefit**
   - Current mappings are already simple and readable
   - Record types with positional constructors make manual mapping concise
   - Lines of code saved would be minimal (maybe 3-5 lines per mapping)

3. **Adds Complexity Without Value**
   - Need to configure context injection for `TenantId`
   - Need workarounds for system-generated values
   - Need special handling for update scenarios
   - Mapperly syntax and attributes add learning curve

4. **Current Pattern Works Well**
   - Expression trees for queries (optimal SQL generation)
   - Direct property assignment for updates (clear intent)
   - Explicit mapping shows exactly what's happening (maintainability)

### When Mapperly WOULD Be Valuable

Mapperly shines when you have:
- ✅ Many complex DTOs with 10+ properties
- ✅ Deep object graphs requiring nested mapping
- ✅ No EF Core query projection requirements
- ✅ Minimal contextual/computed values
- ✅ Large volume of similar mappings

YoFi.V3 has:
- ❌ Simple DTOs (3-5 properties)
- ❌ Flat object structures
- ✅ Critical query projection needs
- ❌ Contextual values (TenantId, system timestamps)
- ❌ Relatively few unique mappings

## Alternative: Keep Current Pattern, Add Consistency

Instead of Mapperly, consider:

1. **Standardize Expression Pattern**
   ```csharp
   // Use expression trees for all query projections
   private static Expression<Func<Entity, ResultDto>> ToResultDto =>
       e => new ResultDto(...);
   ```

2. **Extract Common Update Logic**
   ```csharp
   // Helper method for common update patterns
   private static void ApplyEdits(Transaction target, TransactionEditDto source)
   {
       target.Date = source.Date;
       target.Amount = source.Amount;
       target.Payee = source.Payee;
   }
   ```

3. **Consider Record `with` Expressions**
   ```csharp
   // For immutable updates
   var updated = existing with
   {
       Date = dto.Date,
       Amount = dto.Amount,
       Payee = dto.Payee
   };
   ```

## Alternative Mapping Libraries Comparison

Beyond Mapperly, here are other open-source mapping libraries:

### 1. **AutoMapper** (NOT Recommended - Already Excluded)
- ❌ Uses reflection (performance overhead)
- ❌ Runtime configuration discovery
- ❌ Less type-safe than source generators
- ✅ Most mature and feature-rich
- **Verdict:** Excluded per requirements

### 2. **Mapster** (https://github.com/MapsterMapper/Mapster)
- ✅ High performance (faster than AutoMapper)
- ✅ Code generation via Mapster.Tool
- ✅ Can generate adapter classes
- ⚠️ **Same EF Core Query Projection Issue** - Cannot generate `Expression<Func<>>` for SQL translation
- ⚠️ Still requires configuration/setup
- ⚠️ Less type-safe than Mapperly
- **Verdict:** Better than AutoMapper but same core limitation as Mapperly

### 3. **TinyMapper** (https://github.com/TinyMapper/TinyMapper)
- ❌ Reflection-based (slow)
- ❌ No longer actively maintained
- ❌ Cannot handle query projections
- **Verdict:** Not recommended - outdated

### 4. **AgileMapper** (https://github.com/agileobjects/AgileMapper)
- ✅ Compile-time expression generation
- ❌ Complex API
- ❌ Cannot generate EF Core query projections
- ⚠️ Less active development
- **Verdict:** Overly complex for simple needs

### 5. **ExpressMapper** (https://github.com/fluentsprings/ExpressMapper)
- ❌ No longer maintained
- ❌ Reflection-based
- **Verdict:** Obsolete

### 6. **Manual Expression Trees** (Current Approach)
- ✅ Perfect for EF Core query projections
- ✅ Zero library dependencies
- ✅ Complete type safety
- ✅ Full control and transparency
- ✅ No learning curve for standard C#
- ⚠️ Requires writing mapping code manually
- **Verdict:** BEST for YoFi.V3's requirements

## Critical Limitation: EF Core Query Projections

**ALL mapping libraries share the same fundamental limitation:**

None of them can generate `Expression<Func<T, TDto>>` that EF Core can translate to SQL. They all generate regular methods that require:

```csharp
// ALL libraries force this pattern:
var entities = await query.ToListAsync();  // Load full entities from DB
var dtos = mapper.Map<Entity, Dto>(entities);  // Map in memory
```

**YoFi.V3's optimal pattern** (current approach):
```csharp
// Mapping happens in SQL - only needed columns are selected
var dtos = await query.Select(ToResultDto).ToListAsync();
```

This is **not just a performance difference** - it's architectural:
- Libraries: Load → Map (N+1 queries, full entity hydration)
- Expression trees: Project in SQL (optimal queries, minimal data transfer)

## Performance Comparison

For a query returning 1,000 transactions:

| Approach | Database Load | Memory Usage | Performance |
|----------|--------------|--------------|-------------|
| **Expression Tree** (current) | Only 4 columns per row | Minimal | **Optimal** |
| Any mapping library | Full entity hydration | High | Suboptimal |

**Real Impact:**
- Current: SELECT Key, Date, Amount, Payee (4 columns)
- With library: SELECT * (all columns + navigation properties)

For 1,000 rows:
- Current: ~100KB data transfer
- With library: ~500KB+ data transfer (5x more)

## Comprehensive Recommendation

### For YoFi.V3: **Use Manual Expression Trees** (Current Approach)

**Why?**

1. **Architecture Fit** - EF Core query projections are core to your data access pattern
2. **Simplicity** - DTOs have 3-5 properties; manual mapping is 3-4 lines
3. **Performance** - No library can match SQL-side projection
4. **Type Safety** - Compile-time verification, no configuration
5. **Zero Dependencies** - No additional NuGet packages to maintain
6. **Transparency** - Mapping logic is explicit and debuggable

### When to Reconsider

Use a mapping library (Mapster or Mapperly) ONLY if:

- ✅ Moving away from EF Core projections (use repository pattern that returns full entities)
- ✅ DTOs grow to 15+ properties each
- ✅ Complex nested object graphs become common
- ✅ Have 50+ unique mapping scenarios
- ✅ Team struggles with maintaining manual mappings

**None of these apply to YoFi.V3 currently.**

## Conclusion

After evaluating all major open-source mapping libraries, **none offer benefits** for YoFi.V3 that outweigh the critical limitation of incompatibility with EF Core query projections.

The current manual mapping approach using expression trees is:
- ✅ Architecturally optimal
- ✅ Highest performance
- ✅ Most maintainable for your DTO complexity
- ✅ Zero external dependencies

**Final Decision:** Continue with manual expression tree mappings. No mapping library (Mapperly, Mapster, or others) provides value for this project's specific requirements.

---

**Review Triggers:**
- Project grows beyond 30+ unique mapping scenarios
- Average DTO complexity exceeds 10 properties
- Team identifies mapping maintenance as a pain point
- Architecture shifts away from EF Core query projections
