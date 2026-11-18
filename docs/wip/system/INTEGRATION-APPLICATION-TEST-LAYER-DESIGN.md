---
status: Draft
---

# Integration.Application Test Layer Design

**Filling the gap between unit tests and controller integration tests**

## Executive Summary

**Problem:** Current test architecture has a gap - no tests verify that Application Features correctly use `IDataProvider` interface methods (like `GetTransactionsWithSplits()`) to load EF Core navigation properties.

**Impact:** The recent bug where `GetTransactionByKeyAsync()` failed to load `Splits` was not caught by any existing tests:
- Integration.Data tests use `.Include()` explicitly in test code (never exercise Feature query builders)
- Unit tests use mock `InMemoryDataProvider` (all data already in memory, no lazy loading)
- Controller Integration tests verify HTTP responses (don't verify navigation property loading)

**Solution:** Create new `tests/Integration.Application/` project to test Application Features with real database through `IDataProvider` interface.

**Key Insight from User:** "Controller tests WOULD HAVE found the problem, we just haven't written the controllers yet. However, it's problematic because by that time the bug will be several layers deep."

## Problem Statement

### The Bug That Exposed the Gap

In [`TransactionsFeature.GetTransactionByKeyInternalAsync()`](../../src/Application/Features/TransactionsFeature.cs:239-252), the code called:

```csharp
return dataProvider.Get<Transaction>()
    .Where(t => t.TenantId == _currentTenant.Id)
    .Where(t => t.Key == key)
    .SingleOrDefaultAsync();
```

Later, the code accessed `transaction.Splits.FirstOrDefault()`, expecting the Splits collection to be loaded. **But it wasn't loaded** because the query didn't include `.Include(t => t.Splits)`.

### Why No Existing Test Caught This

**1. Integration.Data Tests** ([`tests/Integration.Data/SplitTests.cs`](../../tests/Integration.Data/SplitTests.cs))
- ✅ Test `ApplicationDbContext` directly with real SQLite
- ✅ Verify EF Core configurations, indexes, relationships
- ❌ Use `.Include()` explicitly in test code (line 410)
- ❌ Never exercise Feature's query builder methods
- ❌ Don't test through `IDataProvider` interface

**2. Unit Tests** ([`tests/Unit/`](../../tests/Unit/))
- ✅ Test Application Features with mock `InMemoryDataProvider`
- ❌ Mock has all data in memory (no lazy loading behavior)
- ❌ Cannot simulate EF Core navigation property loading issues
- ❌ `Splits` collection always populated in mock (line 59-64 of `InMemoryDataProvider.cs`)

**3. Controller Integration Tests** ([`tests/Integration.Controller/TransactionsControllerTests.cs`](../../tests/Integration.Controller/TransactionsControllerTests.cs))
- ✅ Test full HTTP pipeline with real database via WebApplicationFactory
- ✅ Actually DO test through `IDataProvider` interface (indirectly)
- ❌ But don't verify navigation property loading (test HTTP responses, not object state)
- ⚠️ **Would have caught the bug** IF we had written split-related controller tests
- **Problem:** Finding bugs at the controller layer means the bug is "several layers deep" and expensive to debug

## The Missing Test Layer

We need tests that:
1. ✅ Test **Application Features** directly (not Controllers)
2. ✅ Use **real `ApplicationDbContext`** (not mocks)
3. ✅ Exercise **`IDataProvider` interface** (like production)
4. ✅ Verify **navigation properties load correctly** (like Splits)
5. ✅ Catch bugs **at the Feature layer** (before Controllers)

**Name:** Integration.Application Tests (tests Features + DbContext through IDataProvider)

## Proposed Architecture

### Project Structure

```
tests/
├── Integration.Application/           # NEW - Feature integration tests
│   ├── YoFi.V3.Tests.Integration.Application.csproj
│   ├── README.md                      # Purpose, patterns, examples
│   ├── TestHelpers/
│   │   ├── FeatureTestBase.cs        # Base class for feature tests
│   │   ├── TestCurrentTenant.cs      # Mock current tenant
│   │   └── README.md
│   ├── TransactionsFeaturesTests.cs  # Test TransactionsFeature
│   ├── SplitsFeaturesTests.cs        # Test splits-related features
│   └── TenantFeatureTests.cs         # Test TenantFeature
│
├── Integration.Data/                  # EXISTING - DbContext tests
├── Integration.Controller/            # EXISTING - Controller tests
└── Unit/                              # EXISTING - Unit tests
```

### Test Base Class Pattern

```csharp
/// <summary>
/// Base class for Integration.Application tests.
/// </summary>
/// <remarks>
/// Provides real ApplicationDbContext with in-memory SQLite database
/// and real IDataProvider interface for testing Application Features.
/// </remarks>
public abstract class FeatureTestBase
{
    protected ApplicationDbContext _context;
    protected IDataProvider _dataProvider;
    protected ICurrentTenant _currentTenant;
    protected Tenant _testTenant;
    private DbContextOptions<ApplicationDbContext> _options;

    [SetUp]
    public async Task SetUp()
    {
        // Use in-memory SQLite database (same as Integration.Data)
        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _context = new ApplicationDbContext(_options);
        _context.Database.OpenConnection(); // Keep in-memory DB alive
        _context.Database.EnsureCreated();

        // IDataProvider is the DbContext itself (explicit interface implementation)
        _dataProvider = _context;

        // Create test tenant
        _testTenant = new Tenant
        {
            Name = "Test Tenant",
            Description = "Test tenant for feature tests"
        };
        _context.Tenants.Add(_testTenant);
        await _context.SaveChangesAsync();

        // Mock current tenant (for tenant-scoped features)
        _currentTenant = new TestCurrentTenant(_testTenant.Id, _testTenant.Key);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }
}
```

### Mock Current Tenant

```csharp
/// <summary>
/// Test implementation of ICurrentTenant for feature tests.
/// </summary>
internal class TestCurrentTenant : ICurrentTenant
{
    public int Id { get; }
    public Guid Key { get; }

    public TestCurrentTenant(int id, Guid key)
    {
        Id = id;
        Key = key;
    }
}
```

## Example Tests

### Test That Would Have Caught the Splits Bug

```csharp
/// <summary>
/// Integration tests for TransactionsFeature.
/// </summary>
/// <remarks>
/// Tests TransactionsFeature methods with real ApplicationDbContext
/// to verify IDataProvider usage and navigation property loading.
/// </remarks>
[TestFixture]
public class TransactionsFeaturesTests : FeatureTestBase
{
    private TransactionsFeature _transactionsFeature;

    [SetUp]
    public new async Task SetUp()
    {
        await base.SetUp();

        // Create the feature with real dependencies
        var logger = new NullLogger<TransactionsFeature>();
        _transactionsFeature = new TransactionsFeature(
            _dataProvider,
            _currentTenant,
            logger
        );
    }

    [Test]
    public async Task GetTransactionByKeyAsync_WithSplits_LoadsSplitsCollection()
    {
        // Given: Transaction with split in database
        var transaction = new Transaction
        {
            Date = DateOnly.FromDateTime(DateTime.Now),
            Payee = "Test Payee",
            Amount = 100.00m,
            TenantId = _testTenant.Id
        };
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        var split = new Split
        {
            TransactionId = transaction.Id,
            Amount = 100.00m,
            Category = "Groceries",
            Order = 0
        };
        _context.Splits.Add(split);
        await _context.SaveChangesAsync();

        // Clear tracking to simulate fresh query (like production)
        _context.ChangeTracker.Clear();

        // When: Getting transaction through TransactionsFeature
        var result = await _transactionsFeature.GetTransactionByKeyAsync(transaction.Key);

        // Then: Transaction should be found
        Assert.That(result, Is.Not.Null);

        // And: Splits SHOULD be loaded (NOT empty collection)
        Assert.That(result.Splits, Is.Not.Empty,
            "Splits collection should be loaded when GetTransactionByKeyAsync is called");
        Assert.That(result.Splits.First().Category, Is.EqualTo("Groceries"));
    }

    [Test]
    public async Task GetTransactionByKeyAsync_WithMultipleSplits_LoadsAllSplits()
    {
        // Given: Transaction with multiple splits
        var transaction = new Transaction
        {
            Date = DateOnly.FromDateTime(DateTime.Now),
            Payee = "Grocery Store",
            Amount = 100.00m,
            TenantId = _testTenant.Id
        };
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        var split1 = new Split
        {
            TransactionId = transaction.Id,
            Amount = 60.00m,
            Category = "Groceries",
            Order = 0
        };
        var split2 = new Split
        {
            TransactionId = transaction.Id,
            Amount = 40.00m,
            Category = "Household",
            Order = 1
        };
        _context.Splits.AddRange(split1, split2);
        await _context.SaveChangesAsync();

        _context.ChangeTracker.Clear();

        // When: Getting transaction through Feature
        var result = await _transactionsFeature.GetTransactionByKeyAsync(transaction.Key);

        // Then: All splits should be loaded
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Splits, Has.Count.EqualTo(2));
        Assert.That(result.Splits.Sum(s => s.Amount), Is.EqualTo(100.00m));
    }

    [Test]
    public async Task UpdateTransactionAsync_WithSplits_LoadsSplitsForUpdate()
    {
        // Given: Transaction with split exists
        var transaction = new Transaction
        {
            Date = DateOnly.FromDateTime(DateTime.Now),
            Payee = "Original Payee",
            Amount = 100.00m,
            TenantId = _testTenant.Id
        };
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        var split = new Split
        {
            TransactionId = transaction.Id,
            Amount = 100.00m,
            Category = "Original Category",
            Order = 0
        };
        _context.Splits.Add(split);
        await _context.SaveChangesAsync();

        _context.ChangeTracker.Clear();

        // And: Update DTO
        var updateDto = new TransactionEditDto(
            Date: DateOnly.FromDateTime(DateTime.Now),
            Amount: 150.00m,
            Payee: "Updated Payee",
            Memo: null,
            Source: null,
            ExternalId: null,
            Category: "Updated Category"
        );

        // When: Updating transaction through Feature
        var result = await _transactionsFeature.UpdateTransactionAsync(
            transaction.Key,
            updateDto
        );

        // Then: Update should succeed
        Assert.That(result, Is.Not.Null);

        // And: Splits should be accessible during update logic
        // (This would have failed before the fix because Splits wasn't loaded)
        Assert.That(result.Splits, Is.Not.Empty);

        // And: Single split should be updated to match new category
        Assert.That(result.Splits.First().Category, Is.EqualTo("Updated Category"));
    }
}
```

### Additional Test Examples

```csharp
[Test]
public async Task GetTransactionsAsync_WithSplits_LoadsSplitsForAllTransactions()
{
    // Given: Multiple transactions with splits
    var tx1 = CreateTransactionWithSplit("Payee 1", 100m, "Category 1");
    var tx2 = CreateTransactionWithSplit("Payee 2", 200m, "Category 2");
    await SaveToDatabase(tx1, tx2);

    // When: Getting all transactions through Feature
    var results = await _transactionsFeature.GetTransactionsAsync();

    // Then: All transactions should have splits loaded
    Assert.That(results, Has.Count.EqualTo(2));
    Assert.That(results.All(t => t.Splits.Any()), Is.True,
        "All transactions should have splits loaded");
}

[Test]
public async Task QuickEditTransactionAsync_PreservesExistingSplits()
{
    // Given: Transaction with multiple splits
    var transaction = CreateTransactionWithMultipleSplits();
    await SaveToDatabase(transaction);

    // When: Quick editing transaction (payee only)
    await _transactionsFeature.QuickEditTransactionAsync(
        transaction.Key,
        new QuickEditDto { Payee = "New Payee" }
    );

    // Then: Splits should still be loaded and preserved
    var updated = await _transactionsFeature.GetTransactionByKeyAsync(transaction.Key);
    Assert.That(updated.Splits, Has.Count.EqualTo(2));
}
```

## Comparison: Three Integration Test Layers

| Aspect | Integration.Data | **Integration.Application** | Integration.Controller |
|--------|-----------------|---------------------------|----------------------|
| **Tests** | DbContext directly | **Features via IDataProvider** | Controllers via HTTP |
| **Database** | Real SQLite (in-memory) | **Real SQLite (in-memory)** | Real SQLite (in-memory) |
| **HTTP** | No | **No** | Yes (WebApplicationFactory) |
| **Purpose** | EF Core config | **Feature + IDataProvider** | API contracts + auth |
| **Catches** | Schema issues | **Navigation loading bugs** | HTTP contract issues |
| **Speed** | Fast (~10ms) | **Fast (~20-50ms)** | Medium (~100-200ms) |
| **When to Use** | DB schema, indexes, relationships | **Feature queries, navigation properties** | API endpoints, auth, HTTP |

## Benefits of Integration.Application Layer

### 1. **Catches Bugs Early** (Before Controllers)
- Finds navigation property loading issues at Feature layer
- Cheaper to debug than "several layers deep" Controller failures
- Tests run faster than Controller tests (no HTTP overhead)

### 2. **Tests Real IDataProvider Usage**
- Verifies Features use correct `IDataProvider` methods (e.g., `GetTransactionsWithSplits()`)
- Exercises actual EF Core query building
- Catches lazy loading issues that mocks hide

### 3. **Complements Existing Test Layers**
```
Unit Tests (mock) → Integration.Application (real DB) → Controller Tests (HTTP + DB)
     ↓                           ↓                              ↓
Business logic          Feature + IDataProvider          API contracts + auth
```

### 4. **Fast Enough for Frequent Execution**
- No HTTP overhead (unlike Controller tests)
- No mock setup complexity (unlike Unit tests)
- In-memory SQLite is fast (~20-50ms per test)

### 5. **Clear Test Responsibility**
- **Integration.Data:** "Does the database schema work?"
- **Integration.Application:** "Do Features use IDataProvider correctly?"
- **Integration.Controller:** "Does the HTTP API work correctly?"

## When to Use Integration.Application Tests

### ✅ DO Use Integration.Application When:

1. **Testing navigation property loading**
   - Verify `Include()` statements are present
   - Test lazy loading scenarios
   - Verify related entities are loaded correctly

2. **Testing IDataProvider usage**
   - Verify Features use specialized query builders (e.g., `GetTransactionsWithSplits()`)
   - Test custom IDataProvider methods
   - Verify tenant-scoped queries work correctly

3. **Testing Feature query building**
   - Complex LINQ queries in Features
   - Query optimization verification
   - Filter/sort/pagination logic

4. **Testing multi-step Feature operations**
   - Operations that query, modify, and save data
   - Verify transaction boundaries
   - Test data consistency across operations

### ❌ DON'T Use Integration.Application When:

1. **Testing HTTP contracts** → Use Integration.Controller instead
   - Status codes, response formats, headers
   - Authentication/authorization
   - Request validation

2. **Testing database schema** → Use Integration.Data instead
   - EF Core configurations
   - Indexes, constraints, relationships
   - Table structures

3. **Testing pure business logic** → Use Unit tests instead
   - Calculations without database
   - Validation rules
   - Algorithm correctness

4. **Testing user workflows** → Use Functional tests instead
   - End-to-end scenarios
   - UI interactions
   - Cross-layer workflows

## Implementation Roadmap

### Phase 1: Foundation (Immediate)
- [ ] Create `tests/Integration.Application/` project
- [ ] Add `FeatureTestBase` helper class
- [ ] Add `TestCurrentTenant` mock
- [ ] Create README with patterns and examples
- [ ] Add package references (EF Core, NUnit, etc.)

### Phase 2: Core Feature Tests (High Priority)
- [ ] `TransactionsFeaturesTests.cs` - Test splits loading bug fix
- [ ] Verify `GetTransactionByKeyAsync()` loads splits
- [ ] Verify `UpdateTransactionAsync()` loads splits
- [ ] Verify `QuickEditTransactionAsync()` loads splits
- [ ] Verify `GetTransactionsAsync()` loads splits for all transactions

### Phase 3: Expand Coverage (As Needed)
- [ ] `TenantFeatureTests.cs` - Test tenant-related features
- [ ] `SplitsFeaturesTests.cs` - Test split-specific features (when implemented)
- [ ] Other Feature tests as needed

### Phase 4: Documentation & Process
- [ ] Update [`docs/TESTING-STRATEGY.md`](../../docs/TESTING-STRATEGY.md) with new layer
- [ ] Add to [`docs/wip/IMPLEMENTATION-WORKFLOW.md`](IMPLEMENTATION-WORKFLOW.md)
- [ ] Create test decision flowchart including new layer

## Test Execution Strategy

### Run All Tests Hierarchy

```powershell
# Run all backend tests (Unit + Integration.Data + Integration.Application + Integration.Controller)
pwsh -File ./scripts/Run-Tests.ps1

# Run specific integration test project
dotnet test tests/Integration.Application
dotnet test tests/Integration.Data
dotnet test tests/Integration.Controller

# Run specific test class
dotnet test --filter "FullyQualifiedName~TransactionsFeaturesTests"

# Run specific test method
dotnet test --filter "FullyQualifiedName~TransactionsFeaturesTests.GetTransactionByKeyAsync_WithSplits_LoadsSplitsCollection"
```

### Test Execution Order (Fastest to Slowest)

1. **Unit Tests** (~10ms each) - No external dependencies
2. **Integration.Data** (~10-20ms each) - DbContext only
3. **Integration.Application** (~20-50ms each) - Features + DbContext
4. **Integration.Controller** (~100-200ms each) - HTTP + Features + DbContext
5. **Functional Tests** (~2-5s each) - Browser + Full stack

## Migration Strategy for Existing Code

### Option A: Retrofit Existing Features
**Priority:** TransactionsFeature (high risk, splits bug just fixed)

```markdown
1. Add `TransactionsFeaturesTests.cs` immediately
2. Test splits loading in all methods that use transactions
3. Add tests as bugs are discovered in other features
```

### Option B: Test-First for New Features
**Approach:** Write Integration.Application tests BEFORE implementing new Features

```markdown
1. When adding new Feature methods, write Integration.Application tests first
2. Verify navigation properties load correctly
3. Ensure IDataProvider methods are used correctly
```

### Recommendation: Hybrid Approach
- **Retrofit:** Add tests for TransactionsFeature NOW (high risk area)
- **Test-First:** Use for all new Feature development going forward

## Success Metrics

### How We'll Know This Is Working

1. **Bugs Caught Early**
   - Navigation property loading issues caught at Feature layer
   - Reduced debugging time (find bugs before Controller layer)

2. **Coverage Improvement**
   - All Features have Integration.Application tests
   - All IDataProvider methods have usage tests
   - All navigation properties have loading verification

3. **Test Execution Speed**
   - Integration.Application tests run in ~20-50ms each
   - Faster than Controller tests but comprehensive coverage

4. **Developer Confidence**
   - Developers trust Feature methods load data correctly
   - Less "it works in development but fails in production"

## Open Questions

1. **Should Integration.Application replace some Controller tests?**
   - No - They complement each other. Controller tests verify HTTP contracts, Integration.Application tests verify Feature logic.

2. **How much coverage should this layer have?**
   - Start with high-risk areas (navigation properties, complex queries)
   - Expand as bugs are discovered or new features added
   - Target: All Features have at least smoke tests

3. **Should we test DTOs at this layer?**
   - No - DTO mapping is tested by Controller tests (request → DTO → response)
   - This layer tests Feature → IDataProvider → Entity behavior

## Related Documentation

- [`docs/TESTING-STRATEGY.md`](../../docs/TESTING-STRATEGY.md) - Overall testing strategy
- [`tests/Integration.Data/README.md`](../../tests/Integration.Data/README.md) - Data layer testing
- [`tests/Integration.Controller/README.md`](../../tests/Integration.Controller/README.md) - Controller testing
- [`tests/Unit/README.md`](../../tests/Unit/README.md) - Unit testing patterns
- [`.roorules`](../../.roorules) - Project testing standards

## Conclusion

The Integration.Application test layer fills a critical gap in our test architecture by verifying that Application Features correctly use the `IDataProvider` interface with real EF Core behavior. This catches bugs like the Splits loading issue **before they reach the Controller layer**, where they're "several layers deep" and expensive to debug.

**Key Insight:** While Controller Integration tests would eventually catch these bugs, finding them at the Feature layer is faster to debug and provides better developer experience.

**Recommendation:** Implement Phase 1 immediately and add tests for TransactionsFeature to verify the splits loading fix. Use Integration.Application tests going forward for all Features that work with navigation properties or complex queries.
