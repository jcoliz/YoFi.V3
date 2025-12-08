# Tenant Context Middleware Testing - Implementation Checklist

## Quick Reference

**Main Plan**: See [TENANT-MIDDLEWARE-TESTING-PLAN.md](TENANT-MIDDLEWARE-TESTING-PLAN.md) for complete testing strategy.

**Target Files**:
- Unit Tests: `tests/Unit/Tests/TenantContextMiddlewareTests.cs`
- Integration Tests: `tests/Integration.Controllers/TenantContextMiddlewareIntegrationTests.cs`

**Estimated Timeline**: 1-2 weeks for complete implementation

---

## Phase 1: Setup & Infrastructure (Days 1-2)

### ✅ Checklist

- [ ] Create `tests/Unit/Tests/TenantContextMiddlewareTests.cs`
- [ ] Add required NuGet packages to `tests/Unit/YoFi.V3.Tests.Unit.csproj`:
  - `Moq` (already present)
  - Verify `Microsoft.AspNetCore.Http` is available
- [ ] Create new integration test project:
  - [ ] `tests/Integration.Controllers/YoFi.V3.Tests.Integration.Controllers.csproj`
  - [ ] Add package references:
    - `Microsoft.AspNetCore.Mvc.Testing`
    - `Microsoft.EntityFrameworkCore.Sqlite`
    - `NUnit`
    - `NUnit3TestAdapter`
  - [ ] Add project references:
    - `src/BackEnd/YoFi.V3.BackEnd.csproj`
    - `src/Application/YoFi.V3.Application.csproj`
- [ ] Create test fixtures directory: `tests/Integration.Controllers/Fixtures/`

### 🎯 Deliverable
Working test project structure ready for test implementation

---

## Phase 2: Unit Tests Implementation (Days 3-5)

### Test Cases to Implement (Priority Order)

#### P0 - Critical Path Tests

1. **✅ Valid Tenant Key Extraction**
   ```csharp
   [Test]
   public async Task InvokeAsync_ValidTenantKeyInRoute_SetsTenantContext()
   ```
   - Mock setup for successful tenant lookup
   - Verify `SetCurrentTenantAsync` called with correct GUID
   - Verify next delegate invoked

2. **✅ Missing Tenant Key**
   ```csharp
   [Test]
   public async Task InvokeAsync_MissingTenantKey_SkipsTenantSetup()
   ```
   - Empty route values
   - Verify `SetCurrentTenantAsync` NOT called
   - Verify next delegate still invoked

3. **✅ Invalid GUID Format**
   ```csharp
   [Test]
   public async Task InvokeAsync_InvalidTenantKeyFormat_SkipsTenantSetup()
   ```
   - Non-GUID string in route values
   - Verify graceful handling

#### P1 - Error Handling Tests

4. **✅ Tenant Not Found**
   ```csharp
   [Test]
   public async Task InvokeAsync_TenantNotFoundInDatabase_ThrowsException()
   ```
   - Mock `SetCurrentTenantAsync` to throw `InvalidOperationException`
   - Verify exception propagates

5. **✅ Null Route Value**
   ```csharp
   [Test]
   public async Task InvokeAsync_NullTenantKeyValue_SkipsTenantSetup()
   ```
   - Route values contains key but value is null

6. **✅ Next Delegate Always Invoked**
   ```csharp
   [Test]
   public async Task InvokeAsync_Always_InvokesNextDelegate()
   ```
   - Verify in both success and skip scenarios

### Test Helper Implementation

```csharp
// Helper class structure
[TestFixture]
public class TenantContextMiddlewareTests
{
    private Mock<RequestDelegate> _nextMock;
    private Mock<TenantContext> _tenantContextMock;
    private TenantContextMiddleware _middleware;
    private DefaultHttpContext _httpContext;

    [SetUp]
    public void Setup()
    {
        // Initialize mocks
        // Create middleware instance
        // Setup default HTTP context
    }

    private void SetTenantKeyInRoute(Guid? tenantKey)
    {
        // Helper to set route values
    }

    private void SetupSuccessfulTenantLookup(Guid tenantKey)
    {
        // Helper to mock successful tenant retrieval
    }

    private void SetupTenantNotFound(Guid tenantKey)
    {
        // Helper to mock tenant not found scenario
    }
}
```

### 🎯 Deliverable
6 passing unit tests with >90% code coverage for [`TenantContextMiddleware`](../../src/Controllers/Tenancy/TenantContextMiddleware.cs)

---

## Phase 3: Integration Tests Setup (Days 6-7)

### Infrastructure Files

1. **✅ Create `TenantTestFixture.cs`**
   ```csharp
   public class TenantTestFixture : IDisposable
   {
       public ApplicationDbContext DbContext { get; }
       public Guid TenantAKey { get; private set; }
       public Guid TenantBKey { get; private set; }

       // Setup in-memory database
       // Seed test tenants
       // Seed test transactions
   }
   ```

2. **✅ Create `CustomWebApplicationFactory.cs`**
   ```csharp
   public class CustomWebApplicationFactory : WebApplicationFactory<Program>
   {
       protected override void ConfigureWebHost(IWebHostBuilder builder)
       {
           // Override database to use SQLite in-memory
           // Configure test services
       }
   }
   ```

3. **✅ Create Base Test Class**
   ```csharp
   public class IntegrationTestBase : IClassFixture<CustomWebApplicationFactory>
   {
       protected readonly HttpClient Client;
       protected readonly ApplicationDbContext DbContext;

       // Common setup
       // Helper methods for seeding data
       // Helper methods for assertions
   }
   ```

### 🎯 Deliverable
Working integration test infrastructure with test database

---

## Phase 4: Integration Tests Implementation (Days 8-10)

### Test Cases to Implement (Priority Order)

#### P0 - Core Functionality

1. **✅ End-to-End Valid Tenant Request**
   ```csharp
   [Test]
   public async Task GetTransactions_ValidTenantKey_ReturnsTenantData()
   ```
   - Seed tenant with transactions
   - Make HTTP request to transactions endpoint
   - Verify correct data returned

2. **✅ Tenant Isolation**
   ```csharp
   [Test]
   public async Task GetTransactions_DifferentTenants_ReturnsIsolatedData()
   ```
   - Seed two tenants with different data
   - Verify no cross-tenant data leakage

3. **✅ Non-Existent Tenant**
   ```csharp
   [Test]
   public async Task GetTransactions_NonExistentTenant_Returns500()
   ```
   - Valid GUID but no tenant in database
   - Verify appropriate error response

#### P1 - Edge Cases

4. **✅ Invalid GUID in Route**
   ```csharp
   [Test]
   public async Task GetTransactions_InvalidTenantKeyFormat_Returns404()
   ```
   - Send request with invalid GUID
   - Verify route constraint rejects it

5. **✅ Concurrent Requests**
   ```csharp
   [Test]
   public async Task ConcurrentRequests_DifferentTenants_MaintainsIsolation()
   ```
   - Parallel requests to different tenants
   - Verify each gets correct data

6. **✅ Controller Integration**
   ```csharp
   [Test]
   public async Task TransactionController_UsesTenantFromMiddleware_Successfully()
   ```
   - Verify full pipeline integration
   - No manual tenant passing required

### Data Seeding Helpers

```csharp
protected async Task<Tenant> SeedTenantWithTransactions(string name, int transactionCount)
{
    var tenant = new Tenant { Key = Guid.NewGuid(), Name = name };
    DbContext.Tenants.Add(tenant);
    await DbContext.SaveChangesAsync();

    var transactions = TenantTestData.CreateTransactionsForTenant(tenant.Id, transactionCount);
    DbContext.Transactions.AddRange(transactions);
    await DbContext.SaveChangesAsync();

    return tenant;
}
```

### 🎯 Deliverable
6 passing integration tests verifying end-to-end tenant isolation

---

## Phase 5: Quality Assurance (Days 11-12)

### Testing Quality Checks

- [ ] **Code Coverage Analysis**
  - Run: `dotnet test --collect:"XPlat Code Coverage"`
  - Target: >85% coverage for `TenantContextMiddleware`
  - Review coverage report

- [ ] **Test Reliability**
  - Run all tests 10 times: `for i in {1..10}; do dotnet test; done`
  - Verify 100% pass rate (no flaky tests)

- [ ] **Performance Validation**
  - Unit tests: Should complete in <5 seconds
  - Integration tests: Should complete in <30 seconds
  - Profile slow tests if needed

- [ ] **Test Independence**
  - Run tests in random order
  - Run individual test files
  - Verify no test interdependencies

### Code Quality Checks

- [ ] All tests follow NUnit conventions
- [ ] Test names clearly describe what they verify (Given-When-Then)
- [ ] Proper use of `[Test]`, `[SetUp]`, `[TearDown]`
- [ ] Assertions use NUnit constraint model
- [ ] Mocks properly verified and reset
- [ ] No hardcoded values (use constants or test data helpers)

### 🎯 Deliverable
High-quality, reliable test suite

---

## Phase 6: Documentation & Handoff (Days 13-14)

### Documentation Tasks

- [ ] Update `tests/Unit/README.md`
  - Explain middleware testing pattern
  - Document mocking strategy

- [ ] Update `tests/Integration.Controllers/README.md`
  - Explain WebApplicationFactory usage
  - Document test database setup

- [ ] Create examples in main plan document
  - Add actual test code snippets
  - Document any deviations from plan

- [ ] Update project README
  - Add testing section
  - Link to test plan

### CI/CD Integration

- [ ] Verify tests run in CI pipeline
- [ ] Check test execution time in CI
- [ ] Ensure code coverage reports generated
- [ ] Add test status badge if applicable

### Knowledge Transfer

- [ ] Code review with team
- [ ] Demonstrate test execution
- [ ] Explain testing patterns for future middleware
- [ ] Document lessons learned

### 🎯 Deliverable
Complete, documented testing solution ready for production use

---

## Success Metrics

### Must Have (P0)
- ✅ All 12 tests passing (6 unit + 6 integration)
- ✅ Code coverage >85% for middleware
- ✅ Tests run reliably (100% pass rate)
- ✅ Documentation complete

### Should Have (P1)
- ✅ Performance targets met (<5s unit, <30s integration)
- ✅ CI/CD integration complete
- ✅ Test patterns documented for reuse

### Nice to Have (P2)
- ✅ Load/performance tests
- ✅ Security tests (tenant isolation)
- ✅ Logging verification tests

---

## Troubleshooting Guide

### Common Issues

**Issue**: Moq setup not working for `TenantContext`
- **Solution**: `TenantContext` may need interface for proper mocking
- **Alternative**: Use a test implementation instead of mocking

**Issue**: WebApplicationFactory not finding `Program` class
- **Solution**: Ensure `Program.cs` has `public partial class Program { }`

**Issue**: In-memory database not persisting between tests
- **Solution**: Use `DbContext.Database.OpenConnection()` in setup
- **Alternative**: Use shared database fixture

**Issue**: Integration tests failing on tenant not found
- **Solution**: Verify database seeding occurs before test execution
- **Check**: Database connection string in test configuration

**Issue**: Concurrent test failures
- **Solution**: Ensure proper scoped service lifetime
- **Check**: Each test gets isolated service scope

---

## Quick Command Reference

```bash
# Run all tests
dotnet test

# Run only unit tests
dotnet test --filter FullyQualifiedName~Unit

# Run only integration tests
dotnet test --filter FullyQualifiedName~Integration

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test
dotnet test --filter "FullyQualifiedName~TenantContextMiddlewareTests.InvokeAsync_ValidTenantKeyInRoute_SetsTenantContext"

# Watch mode (re-run on changes)
dotnet watch test
```

---

## Next Steps After Implementation

1. **Switch to Code Mode** to implement the tests
2. Review test results and coverage
3. Address any gaps or issues
4. Update documentation with actual examples
5. Share with team for review
6. Integrate into CI/CD pipeline

---

## Resources

- [Main Testing Plan](TENANT-MIDDLEWARE-TESTING-PLAN.md)
- [NUnit Documentation](https://docs.nunit.org/)
- [Moq Documentation](https://github.com/moq/moq4)
- [WebApplicationFactory Guide](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests)
- [EF Core Testing](https://learn.microsoft.com/en-us/ef/core/testing/)
