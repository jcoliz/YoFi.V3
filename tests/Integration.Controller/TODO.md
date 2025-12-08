# Next Steps: Testing TenantContextMiddleware

This document outlines how to extend the current WebApplicationFactory setup to test the [`TenantContextMiddleware`](../../src/Controllers/Tenancy/TenantContextMiddleware.cs).

## Current Setup ✅

The integration test infrastructure is now working:
- ✅ `WebApplicationFactory<Program>` configured
- ✅ Basic controller tests passing ([`VersionControllerTests.cs`](VersionControllerTests.cs))
- ✅ Full HTTP pipeline being tested
- ✅ 3 tests passing in <2 seconds

## Adding TenantContextMiddleware Tests

### Step 1: Create Test Database Fixture

Create `Fixtures/TenantTestFixture.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using YoFi.V3.Data;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Tenancy;

namespace YoFi.V3.Tests.Integration.Controller.Fixtures;

public class TenantTestFixture : IDisposable
{
    public ApplicationDbContext DbContext { get; }
    public Guid TenantAKey { get; }
    public Guid TenantBKey { get; }

    public TenantTestFixture()
    {
        // Setup SQLite in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        DbContext = new ApplicationDbContext(options);
        DbContext.Database.OpenConnection();
        DbContext.Database.EnsureCreated();

        // Seed test tenants
        var tenantA = new Tenant { Key = Guid.NewGuid(), Name = "Tenant A" };
        var tenantB = new Tenant { Key = Guid.NewGuid(), Name = "Tenant B" };

        DbContext.Tenants.AddRange(tenantA, tenantB);
        DbContext.SaveChanges();

        TenantAKey = tenantA.Key;
        TenantBKey = tenantB.Key;

        // Seed transactions for each tenant
        SeedTransactionsForTenant(tenantA.Id, 3);
        SeedTransactionsForTenant(tenantB.Id, 5);
    }

    private void SeedTransactionsForTenant(long tenantId, int count)
    {
        for (int i = 0; i < count; i++)
        {
            DbContext.Transactions.Add(new Transaction
            {
                TenantId = tenantId,
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(-i)),
                Amount = 100m * (i + 1),
                Payee = $"Payee {i + 1}",
                Key = Guid.NewGuid()
            });
        }
        DbContext.SaveChanges();
    }

    public void Dispose()
    {
        DbContext.Database.CloseConnection();
        DbContext.Dispose();
    }
}
```

### Step 2: Create Custom WebApplicationFactory

Create `Fixtures/CustomWebApplicationFactory.cs`:

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using YoFi.V3.Data;

namespace YoFi.V3.Tests.Integration.Controller.Fixtures;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public TenantTestFixture TenantFixture { get; } = new TenantTestFixture();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the existing DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));

            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Add test database using the shared fixture
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlite(TenantFixture.DbContext.Database.GetConnectionString());
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            TenantFixture.Dispose();
        }
        base.Dispose(disposing);
    }
}
```

### Step 3: Create TenantContextMiddleware Tests

Create `TenantContextMiddlewareTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using YoFi.V3.Application.Dto;
using YoFi.V3.Tests.Integration.Controller.Fixtures;

namespace YoFi.V3.Tests.Integration.Controller;

[TestFixture]
public class TenantContextMiddlewareTests
{
    private CustomWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _tenantAKey;
    private Guid _tenantBKey;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new CustomWebApplicationFactory();
        _client = _factory.CreateClient();
        _tenantAKey = _factory.TenantFixture.TenantAKey;
        _tenantBKey = _factory.TenantFixture.TenantBKey;
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task GetTransactions_ValidTenantKey_ReturnsTenantData()
    {
        // Act
        var response = await _client.GetAsync($"/api/tenant/{_tenantAKey}/transactions");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var transactions = await response.Content.ReadFromJsonAsync<List<TransactionResultDto>>();
        Assert.That(transactions, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task GetTransactions_DifferentTenants_ReturnsIsolatedData()
    {
        // Act - Request Tenant A data
        var responseA = await _client.GetAsync($"/api/tenant/{_tenantAKey}/transactions");
        var transactionsA = await responseA.Content.ReadFromJsonAsync<List<TransactionResultDto>>();

        // Act - Request Tenant B data
        var responseB = await _client.GetAsync($"/api/tenant/{_tenantBKey}/transactions");
        var transactionsB = await responseB.Content.ReadFromJsonAsync<List<TransactionResultDto>>();

        // Assert - Each tenant gets only their data
        Assert.That(transactionsA, Has.Count.EqualTo(3));
        Assert.That(transactionsB, Has.Count.EqualTo(5));
    }

    [Test]
    public async Task GetTransactions_NonExistentTenant_Returns500()
    {
        // Arrange
        var nonExistentTenantKey = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/tenant/{nonExistentTenantKey}/transactions");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
    }

    [Test]
    public async Task GetTransactions_InvalidTenantKeyFormat_Returns404()
    {
        // Act
        var response = await _client.GetAsync("/api/tenant/not-a-guid/transactions");

        // Assert
        // Route constraint should fail, resulting in 404
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
}
```

### Step 4: Run the Tests

```bash
# Run all integration tests
dotnet test tests/Integration.Controller

# Run only middleware tests
dotnet test --filter "FullyQualifiedName~TenantContextMiddlewareTests"

# Run with detailed output
dotnet test tests/Integration.Controller --logger "console;verbosity=detailed"
```

## Test Coverage Goals

These integration tests will verify:

✅ **Middleware extracts tenant key from route** - Verified by successful requests
✅ **TenantContext is populated** - Verified by controller returning correct data
✅ **TransactionsFeature uses tenant context** - Verified by data isolation
✅ **Tenant isolation is enforced** - Verified by different tenants returning different data
✅ **Error handling for non-existent tenants** - Verified by 500 response
✅ **Route constraint validation** - Verified by 404 for invalid GUIDs

## Advantages of This Approach

1. **Real pipeline testing** - Tests the actual middleware execution
2. **No mocking required** - Uses real components
3. **High confidence** - Tests the full integration
4. **Easy to understand** - HTTP requests in, assertions on responses
5. **Fast enough** - In-memory database makes tests quick

## Complementary Unit Tests

While integration tests provide end-to-end confidence, consider adding unit tests for:

- Middleware logic in isolation (using mocks)
- Edge cases that are hard to trigger via HTTP
- Performance-sensitive code paths
- Complex conditional logic

See [`../../docs/wip/TENANT-MIDDLEWARE-TESTING-PLAN.md`](../../docs/wip/TENANT-MIDDLEWARE-TESTING-PLAN.md) for the complete hybrid testing strategy.

## Resources

- [Current VersionController Tests](VersionControllerTests.cs) - Working example
- [Testing Plan](../../docs/wip/TENANT-MIDDLEWARE-TESTING-PLAN.md) - Complete strategy
- [Implementation Guide](../../docs/wip/TENANT-MIDDLEWARE-TESTING-IMPLEMENTATION.md) - Step-by-step checklist
