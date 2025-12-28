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

---

## Impact on Overall Project Test Strategy

### Current Problem: Controller Integration Tests Are Doing Too Much

**The core issue:** Controller Integration tests currently serve **two conflicting purposes**:

1. ✅ **Legitimate:** HTTP contract verification (status codes, DTO mapping, routing, authorization policies)
2. ❌ **Misplaced:** Feature + Data integration testing (navigation properties, query logic, business rules)

**Why this is problematic:**

- Controller tests bypass authentication using `TestAuthenticationHandler` (mocked auth, not real)
- When tests fail, "the bug is several layers deep" and expensive to debug
- HTTP overhead makes tests slower (~100-200ms vs ~20-50ms)
- Business logic failures are hidden behind HTTP layer abstractions

### Recommended Test Distribution Changes

**Current distribution (from [`docs/TESTING-STRATEGY.md`](../../docs/TESTING-STRATEGY.md)):**

```
Current Test Mix:
┌─────────────────────────────────────┐
│ Controller Integration: 60-70%      │ ← Too much responsibility
│ Unit: 19-25%                        │
│ Functional: 7-13%                   │
└─────────────────────────────────────┘

Total: ~259-331 tests for ~288 acceptance criteria
```

**Proposed distribution with Integration.Application:**

```
Proposed Test Mix:
┌─────────────────────────────────────┐
│ Controller Integration: 35-40%      │ ← Focus on HTTP contracts only
│ Application Integration: 30-35%     │ ← NEW - Feature + IDataProvider
│ Unit: 20-25%                        │ ← Unchanged
│ Functional: 10-15%                  │ ← Slightly increased
└─────────────────────────────────────┘

Total: ~300-380 tests (adding ~40-50 Application Integration tests)
```

### What to Move from Controller Integration to Application Integration

**❌ MOVE these concerns to Application Integration:**

1. **Navigation property loading verification**
   ```csharp
   // BEFORE: In Controller test (too deep, slow)
   [Test]
   public async Task GetTransaction_WithMultipleSplits_ReturnsSplitsInResponse()
   {
       // Testing: HTTP + Controller + Feature + IDataProvider + DbContext + Splits
       // Problem: If this fails, is it HTTP? Feature? Navigation loading?
       var response = await _client.GetAsync($"/api/tenant/{id}/transactions/{key}");
       var result = await response.Content.ReadFromJsonAsync<TransactionResultDto>();
       Assert.That(result.Splits, Has.Count.EqualTo(3)); // ← Testing Feature logic
   }

   // AFTER: In Application Integration test (right level, fast)
   [Test]
   public async Task GetTransactionByKeyAsync_WithMultipleSplits_LoadsAllSplits()
   {
       // Testing: Feature + IDataProvider + DbContext + Splits loading
       // Benefit: Failure is clearly in Feature's data access logic
       var result = await _transactionsFeature.GetTransactionByKeyAsync(transaction.Key);
       Assert.That(result.Splits, Has.Count.EqualTo(3)); // ← Directly testing Feature
   }
   ```

2. **IDataProvider query building correctness**
   - Verify Features use specialized query builders (`GetTransactionsWithSplits()`)
   - Test custom IDataProvider methods work correctly
   - Verify tenant-scoped queries apply filters

3. **Business logic validation with database**
   - Multi-step Feature operations with data persistence
   - Business rule enforcement (e.g., "splits must sum to transaction amount")
   - Data consistency across operations

4. **Feature-level error handling**
   - Exception handling within Features
   - Business rule violations detected by Features
   - Data validation at Feature layer

**✅ KEEP these concerns in Controller Integration:**

1. **HTTP status codes**
   ```csharp
   [Test]
   public async Task GetTransaction_ValidRequest_Returns200()
   {
       // Testing: HTTP contract only
       var response = await _client.GetAsync($"/api/tenant/{id}/transactions/{key}");
       Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
       // Verify DTO structure, not business logic
   }
   ```

2. **Authorization policy verification**
   ```csharp
   [Test]
   public async Task DeleteTransaction_AsViewer_Returns403()
   {
       // Testing: [Authorize(Policy = "RequireEditor")] enforcement
   }
   ```

3. **Request/Response DTO mapping**
   - Verify `TransactionEditDto` → `TransactionResultDto` serialization
   - Verify JSON format and structure
   - Verify required/optional fields in HTTP contract

4. **HTTP-specific validation**
   - Model binding errors (400 Bad Request)
   - Invalid route parameters (404 Not Found)
   - Content negotiation

### Concrete Refactoring Examples

#### Example 1: Transaction Splits Loading

**BEFORE (Controller test doing too much):**

```csharp
// tests/Integration.Controller/TransactionsControllerTests.cs
[Test]
public async Task GetTransaction_WithMultipleSplits_ReturnsAllSplitsInResponse()
{
    // Given: Transaction with 3 splits in database
    var transaction = await SeedTransactionWithSplits(count: 3);

    // When: GET /api/tenant/{id}/transactions/{key}
    var response = await _client.GetAsync($"/api/tenant/{_tenantId}/transactions/{transaction.Key}");

    // Then: 200 OK
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    // And: Response contains all 3 splits (← Testing Feature logic, not HTTP)
    var result = await response.Content.ReadFromJsonAsync<TransactionResultDto>();
    Assert.That(result.Splits, Has.Count.EqualTo(3));
    Assert.That(result.Splits.Sum(s => s.Amount), Is.EqualTo(transaction.Amount));
    Assert.That(result.Splits, Is.Ordered.By("Order"));

    // Problem: Testing business logic through HTTP layer
    // Slow: ~150ms due to HTTP overhead
    // Unclear: If this fails, is it HTTP, Feature, or Data?
}
```

**AFTER (Separated concerns):**

```csharp
// tests/Integration.Controller/TransactionsControllerTests.cs
[Test]
public async Task GetTransaction_ValidRequest_Returns200WithTransactionDto()
{
    // Given: Transaction exists
    var transaction = await SeedTransactionWithSplits(count: 3);

    // When: GET /api/tenant/{id}/transactions/{key}
    var response = await _client.GetAsync($"/api/tenant/{_tenantId}/transactions/{transaction.Key}");

    // Then: 200 OK with valid TransactionResultDto structure
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    var result = await response.Content.ReadFromJsonAsync<TransactionResultDto>();
    Assert.That(result, Is.Not.Null);
    Assert.That(result.Key, Is.EqualTo(transaction.Key));
    Assert.That(result.Amount, Is.EqualTo(transaction.Amount));

    // HTTP contract verified ✓
    // Don't verify business logic details here
}

// tests/Integration.Application/TransactionsFeaturesTests.cs
[Test]
public async Task GetTransactionByKeyAsync_WithMultipleSplits_LoadsAllSplitsCorrectly()
{
    // Given: Transaction with 3 splits
    var transaction = new Transaction
    {
        Date = DateOnly.FromDateTime(DateTime.Now),
        Payee = "Grocery Store",
        Amount = 100.00m,
        TenantId = _testTenant.Id
    };
    _context.Transactions.Add(transaction);
    await _context.SaveChangesAsync();

    var splits = new[]
    {
        new Split { TransactionId = transaction.Id, Amount = 60m, Category = "Food", Order = 0 },
        new Split { TransactionId = transaction.Id, Amount = 30m, Category = "Household", Order = 1 },
        new Split { TransactionId = transaction.Id, Amount = 10m, Category = "Other", Order = 2 }
    };
    _context.Splits.AddRange(splits);
    await _context.SaveChangesAsync();

    _context.ChangeTracker.Clear(); // Simulate fresh query

    // When: Getting through Feature (no HTTP)
    var result = await _transactionsFeature.GetTransactionByKeyAsync(transaction.Key);

    // Then: All splits loaded correctly with proper ordering
    Assert.That(result.Splits, Has.Count.EqualTo(3));
    Assert.That(result.Splits.Sum(s => s.Amount), Is.EqualTo(100.00m));
    Assert.That(result.Splits, Is.Ordered.By("Order"));
    Assert.That(result.Splits.First().Category, Is.EqualTo("Food"));

    // Feature logic verified ✓
    // Fast: ~30ms (no HTTP overhead)
    // Clear: Failure is in Feature's IDataProvider usage
}
```

#### Example 2: Authorization Testing

**BEFORE (Testing authorization at wrong level):**

```csharp
// tests/Integration.Controller/TransactionsControllerTests.cs
[Test]
public async Task DeleteTransaction_AsViewer_Returns403()
{
    // Given: User has Viewer role (read-only)
    // And: Transaction exists

    // When: Viewer attempts to delete transaction
    var response = await _client.DeleteAsync($"/api/tenant/{id}/transactions/{key}");

    // Then: 403 Forbidden
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

    // And: Transaction still exists in database (← Testing Feature behavior)
    var transaction = await _context.Transactions.FindAsync(transactionId);
    Assert.That(transaction, Is.Not.Null); // ← Should this be here?
}
```

**AFTER (Clear separation):**

```csharp
// tests/Integration.Controller/TransactionsControllerTests.cs
[Test]
public async Task DeleteTransaction_AsViewer_Returns403()
{
    // Given: User has Viewer role (read-only)
    // And: Transaction exists

    // When: Viewer attempts to delete transaction
    var response = await _client.DeleteAsync($"/api/tenant/{id}/transactions/{key}");

    // Then: 403 Forbidden
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

    // HTTP authorization policy verified ✓
    // Don't verify database state here - that's Feature concern
}

// tests/Integration.Application/TransactionsFeaturesTests.cs
[Test]
public async Task DeleteTransactionAsync_ExistingTransaction_RemovesFromDatabase()
{
    // Given: Transaction exists
    var transaction = await SeedTransaction();

    // When: Deleting through Feature
    await _transactionsFeature.DeleteTransactionAsync(transaction.Key);

    // Then: Transaction should no longer exist
    _context.ChangeTracker.Clear();
    var result = await _transactionsFeature.GetTransactionByKeyAsync(transaction.Key);
    Assert.That(result, Is.Null);

    // Feature deletion logic verified ✓
    // No authorization testing here - that's Controller concern
}
```

### Updated Decision Flowchart for Test Layer Selection

Add this to [`docs/TESTING-STRATEGY.md`](../../docs/TESTING-STRATEGY.md):

```
┌─────────────────────────────────────────────────────────────┐
│  START: Analyzing Acceptance Criterion                      │
└───────────────────────┬─────────────────────────────────────┘
                        │
                        ▼
        ┌───────────────────────────────────┐
        │ Does it involve browser/UI        │
        │ interaction or visual behavior?   │
        └───────────┬───────────────────────┘
                    │
        ┌───────────┴───────────┐
        │ YES                   │ NO
        ▼                       ▼
┌───────────────────┐   ┌───────────────────────────────────┐
│ ✅ FUNCTIONAL     │   │ Does it test HTTP status codes,   │
│                   │   │ request/response format, or       │
│                   │   │ authorization policy?             │
└───────────────────┘   └───────────┬───────────────────────┘
                                    │
                        ┌───────────┴───────────┐
                        │ YES                   │ NO
                        ▼                       ▼
                ┌───────────────────┐   ┌───────────────────────────────┐
                │ ✅ CONTROLLER     │   │ Does it test Feature + Data   │
                │ INTEGRATION       │   │ interaction, navigation       │
                │                   │   │ properties, or query logic?   │
                │ Examples:         │   └───────────┬───────────────────┘
                │ • Status codes    │               │
                │ • Auth policies   │   ┌───────────┴───────────┐
                │ • DTO mapping     │   │ YES                   │ NO
                │ • HTTP contracts  │   ▼                       ▼
                └───────────────────┘   ┌───────────────────┐   ┌───────────────────┐
                                        │ ✅ APPLICATION    │   │ Is it pure logic  │
                                        │ INTEGRATION       │   │ with no database? │
                                        │ 🎯 NEW LAYER!     │   └───────────┬───────┘
                                        │                   │               │
                                        │ Examples:         │   ┌───────────┴────┐
                                        │ • Nav properties  │   │ YES            │ NO
                                        │ • IDataProvider   │   ▼                ▼
                                        │ • Query building  │   ┌──────────┐  ┌──────────┐
                                        │ • Feature logic   │   │ ✅ UNIT  │  │ ✅ DATA  │
                                        └───────────────────┘   └──────────┘  │INTEGRATION│
                                                                              └──────────┘
```

### Acceptance Criteria Mapping Examples

**How to map common acceptance criteria with the new layer:**

| Acceptance Criterion | Current Mapping | New Mapping | Rationale |
|---------------------|----------------|-------------|-----------|
| "API returns 404 when resource not found" | Controller Integration | ✅ Controller Integration | HTTP contract - keep in Controller |
| "Transaction includes all splits when retrieved" | Controller Integration | ✅ **Application Integration** | Feature + Data concern - move to Application |
| "Split amounts must sum to transaction amount" | Unit or Controller | ✅ Unit | Pure business logic - keep in Unit |
| "User with Viewer role cannot delete transactions" | Controller Integration | ✅ Controller Integration | Authorization policy - keep in Controller |
| "GetTransactionByKeyAsync loads Splits collection" | ❌ Not tested | ✅ **Application Integration** | Navigation loading - NEW test |
| "Query filters transactions by date range" | Controller Integration | ✅ **Application Integration** | Feature query logic - move to Application |
| "API accepts TransactionEditDto and returns TransactionResultDto" | Controller Integration | ✅ Controller Integration | DTO mapping - keep in Controller |

### Benefits of This Refactoring

**1. Faster Feedback Loop**
```
BEFORE: Find bug at Controller layer (~150ms per test)
├── HTTP overhead: ~100ms
├── Feature execution: ~30ms
└── Database: ~20ms

AFTER: Find bug at Application layer (~30ms per test)
├── Feature execution: ~30ms
└── Database: ~20ms
└── No HTTP overhead: ~0ms
```

**2. Clearer Failure Messages**
```
BEFORE (Controller test):
"Expected HTTP 200 OK but got 500 Internal Server Error"
↓ Now dig through logs to find the actual problem...
↓ "NullReferenceException in TransactionsFeature.GetTransactionByKeyAsync"
↓ "Splits collection was null"

AFTER (Application test):
"Splits collection should not be empty but was empty"
↓ Immediately know: Feature didn't load navigation property
```

**3. Reduced Test Duplication**
```
BEFORE:
├── Unit test: Mock IDataProvider, test business logic
├── Controller test: Test HTTP + Feature + IDataProvider + Data
└── Duplicated effort: Testing Feature logic in both layers

AFTER:
├── Unit test: Mock IDataProvider, test business logic
├── Application test: Real IDataProvider, test Feature + Data
├── Controller test: Test HTTP contracts only
└── Clear separation: Each layer tests one thing
```

**4. Better Architecture Alignment**
```
Clean Architecture Layers:
├── Controllers (HTTP)          → Controller Integration Tests
├── Features (Business Logic)   → Application Integration Tests
├── Data (Repository)           → Data Integration Tests
└── Domain (Entities)           → Unit Tests

Each test layer maps to ONE architecture layer ✓
```

### Migration Strategy for Existing Tests

**Phase 1: Add Application Integration Infrastructure** (Week 1)
- [ ] Create `tests/Integration.Application/` project
- [ ] Add `FeatureTestBase` and `TestCurrentTenant` helpers
- [ ] Document patterns in README
- [ ] Add to [`scripts/Run-Tests.ps1`](../../scripts/Run-Tests.ps1)

**Phase 2: Retrofit High-Risk Features** (Week 2-3)
- [ ] Add `TransactionsFeaturesTests` (Splits bug area - HIGH PRIORITY)
- [ ] Test navigation property loading for all Transaction methods
- [ ] Add `TenantFeatureTests` (multi-tenancy isolation - HIGH PRIORITY)
- [ ] Verify tenant-scoped queries work correctly

**Phase 3: Refactor Existing Controller Tests** (Week 4-6)
- [ ] Identify Controller tests that verify business logic
- [ ] Move business logic assertions to Application Integration tests
- [ ] Keep only HTTP contract verification in Controller tests
- [ ] Target: Reduce Controller Integration from ~200 tests → ~120 tests
- [ ] Add: ~40-50 new Application Integration tests

**Phase 4: Update Documentation** (Week 6)
- [ ] Update [`docs/TESTING-STRATEGY.md`](../../docs/TESTING-STRATEGY.md) with new distribution
- [ ] Update decision flowchart in TESTING-STRATEGY.md
- [ ] Update [`.roorules`](../../.roorules) with Application Integration patterns
- [ ] Add examples to [`docs/wip/IMPLEMENTATION-WORKFLOW.md`](IMPLEMENTATION-WORKFLOW.md)

**Phase 5: Establish as Standard Practice** (Ongoing)
- [ ] Use Test-First approach for new Features
- [ ] Write Application Integration tests BEFORE implementing Feature methods
- [ ] Review PRs to ensure tests are in correct layer

### Expected Outcomes

**Test Count Changes:**
```
BEFORE:
├── Controller Integration: ~200 tests (60%)
├── Unit: ~70 tests (20%)
├── Data Integration: ~30 tests (10%)
└── Functional: ~35 tests (10%)
Total: ~335 tests

AFTER:
├── Controller Integration: ~120 tests (35%) ← Reduced by 80 tests
├── Application Integration: ~115 tests (33%) ← NEW
├── Unit: ~70 tests (20%) ← Unchanged
├── Data Integration: ~30 tests (9%) ← Unchanged
└── Functional: ~40 tests (12%) ← Slightly increased
Total: ~375 tests (+40 net new tests)
```

**Test Execution Time:**
```
BEFORE:
├── Controller Integration: ~200 tests × 150ms = ~30 seconds
├── Other tests: ~10 seconds
Total: ~40 seconds

AFTER:
├── Controller Integration: ~120 tests × 100ms = ~12 seconds
├── Application Integration: ~115 tests × 30ms = ~3.5 seconds
├── Other tests: ~10 seconds
Total: ~25.5 seconds (37% faster!)
```

**Developer Experience:**
```
BEFORE:
"Test failed at Controller layer - now I need to debug HTTP + Feature + Data"
Time to identify root cause: ~15-30 minutes

AFTER:
"Test failed at Application layer - Feature's IDataProvider usage is wrong"
Time to identify root cause: ~2-5 minutes (5-10x faster)
```

### Answering the Key Question

**"Are Controller Integration tests really adding large value that Feature Integration tests wouldn't?"**

**Answer:** Controller Integration tests add value **ONLY for HTTP-specific concerns**:

✅ **High value in Controller tests:**
- HTTP status code contracts (401, 403, 404, 400, 200)
- Authorization policy enforcement (`[Authorize]` attributes)
- Request/Response DTO serialization
- Model binding and validation at HTTP layer
- Content negotiation and headers

❌ **Low/negative value in Controller tests** (move to Application):
- Navigation property loading verification
- Business logic correctness with database
- Query building and filtering logic
- Multi-step Feature operations
- Data consistency verification

**The fundamental issue:** Controller Integration tests bypass real authentication (`TestAuthenticationHandler`) and add HTTP overhead, making them a poor choice for testing Feature + Data logic. Application Integration tests provide the same database coverage with:
- ⚡ **3-5x faster** execution (~30ms vs ~150ms)
- 🎯 **Clearer failures** (Feature layer, not "somewhere in HTTP stack")
- 🏗️ **Better architecture** alignment (each test layer → one architecture layer)

### Action Items for docs/TESTING-STRATEGY.md

When implementing this change, update [`docs/TESTING-STRATEGY.md`](../../docs/TESTING-STRATEGY.md) with:

1. **Add new section:** "Integration.Application Test Layer"
   - Purpose, characteristics, when to use
   - Add to test pyramid diagram (new middle layer)

2. **Update target distribution:**
   - Change from "60% Controller, 25% Unit, 15% Functional"
   - To: "35-40% Controller, 30-35% Application, 20-25% Unit, 10-15% Functional"

3. **Update decision flowchart:**
   - Add "Application Integration" decision branch
   - Clarify when to use Application vs Controller tests

4. **Add refactoring examples:**
   - Show before/after test separation
   - Document migration strategy

5. **Update "Overview of Test Layers" section:**
   - Add Integration.Application as primary test layer
   - Update comparison table

---

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
   - **Answer:** Not replace, but **refactor**. Move business logic assertions from Controller tests to Application tests. Keep HTTP contract verification in Controller tests.
   - **Strategy:** Reduce Controller Integration from ~200 tests (60%) → ~120 tests (35%)
   - **Add:** ~115 new Application Integration tests (33%)

2. **How much coverage should this layer have?**
   - **Answer:** Target 30-35% of total tests (~115 tests out of ~375 total)
   - Start with high-risk areas (navigation properties, complex queries)
   - Expand as bugs are discovered or new features added
   - **Goal:** All Features have comprehensive Application Integration tests

3. **Should we test DTOs at this layer?**
   - **Answer:** No - DTO mapping is tested by Controller tests (HTTP request → DTO → response)
   - Application Integration tests work with Domain Entities, not DTOs
   - This layer tests Feature → IDataProvider → Entity behavior

4. **What about authentication testing?**
   - **Answer:** Real authentication testing stays in Controller tests (HTTP layer concern)
   - Application Integration tests use `TestCurrentTenant` mock (sufficient for Feature logic)
   - This is appropriate because Features receive `ICurrentTenant` from Controllers

5. **Will this slow down the overall test suite?**
   - **Answer:** No - actually **37% faster**!
   - Moving 80 slow Controller tests (~150ms each) to Application tests (~30ms each)
   - **Net result:** ~25.5 seconds vs ~40 seconds total test time
   - See "Expected Outcomes" in "Impact on Overall Project Test Strategy" section above

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
