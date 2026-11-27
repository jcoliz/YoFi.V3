# Integration.Application Tests

Verifies distinct database contracts between Application Features and `IDataProvider` using a real PostgreSQL database.

## Purpose

Application Integration tests are a selective harness for database-boundary behavior that unit tests cannot prove, including:

- EF Core navigation property loading (for example, Splits on Transactions)
- Provider-specific LINQ translation and filtering
- Persistence of entity graphs
- Transaction and rollback behavior
- Bulk operations
- Tenant-scoped query behavior that must be verified through the real provider

This is no longer the primary harness for all Application Feature behavior.

## Why This Layer Exists

Unit tests provide fast, high-coverage verification of Application logic. Integration.Application tests supply the surgical database evidence needed when LINQ-to-Objects, in-memory collections, or mocks cannot establish provider behavior.

For example, a unit test verifies branch logic and orchestration. A small Integration.Application test verifies that the resulting real query translates and that navigation properties or persistence behave correctly.

## When to Use

✅ **DO use Integration.Application tests for:**

- A distinct query shape whose EF/PostgreSQL translation needs verification
- Navigation property loading or tracking behavior
- Persistence, bulk operations, or transaction semantics
- Database-enforced or provider-specific tenant isolation
- One representative happy path and, only where needed, a focused failure or rollback path

❌ **DON'T use Integration.Application tests for:**

- HTTP contracts, status codes, auth → Use [`Integration.Controller`](../Integration.Controller/) tests
- Database schema, EF Core configs → Use [`Integration.Data`](../Integration.Data/) tests
- Pure business logic without database → Use [`Unit`](../Unit/) tests
- End-to-end user workflows → Use [`Functional`](../Functional/) tests

## Test Structure

### Basic Pattern

```csharp
using YoFi.V3.Application.Features;
using YoFi.V3.Entities.Models;
using YoFi.V3.Tests.Integration.Application.TestHelpers;

namespace YoFi.V3.Tests.Integration.Application;

[TestFixture]
public class MyFeatureTests : FeatureTestBase
{
    private MyFeature _feature;

    [SetUp]
    public new async Task SetUp()
    {
        await base.SetUp();
        _feature = new MyFeature(_dataProvider);
    }

    [Test]
    public async Task FeatureMethod_Scenario_ExpectedResult()
    {
        // Given: Test data in database
        var entity = new MyEntity { Name = "Test" };
        _context.MyEntities.Add(entity);
        await _context.SaveChangesAsync();

        // When: Calling feature method
        var result = await _feature.GetById(entity.Id);

        // Then: Result should be correct
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Name, Is.EqualTo("Test"));
    }
}
```

### Clear Tracking Between Tests

If you need to simulate a fresh query (like a new HTTP request would), clear the change tracker:

```csharp
_context.ChangeTracker.Clear();
```

This ensures EF Core re-queries the database instead of returning cached entities.

## Example Tests

See [`WeatherFeatureTests.cs`](WeatherFeatureTests.cs) for a complete working example demonstrating:

- Testing Feature queries with real database
- Verifying data persistence through IDataProvider
- Testing business logic that generates and stores data

## Test Execution

```powershell
# Run all backend tests (includes Integration.Application)
pwsh -File .\scripts\Run-Tests.ps1 -Unit -Integration

# Run only Integration.Application tests
pwsh -File .\scripts\Run-Tests.ps1 -Application

# Run specific test class
dotnet test --filter "FullyQualifiedName~WeatherFeatureTests"

# Run specific test method
dotnet test --filter "FullyQualifiedName~WeatherFeatureTests.GetWeatherForecasts_NewRequest_GeneratesForecasts"
```

## Related Documentation

- [`docs/TESTING-STRATEGY.md`](../../docs/TESTING-STRATEGY.md) - Overall testing strategy
- [`docs/wip/TESTING-STRATEGY-UPDATE-PLAN.md`](../../docs/wip/TESTING-STRATEGY-UPDATE-PLAN.md) - Update plan for this layer
- [`docs/wip/INTEGRATION-APPLICATION-TEST-LAYER-DESIGN.md`](../../docs/wip/INTEGRATION-APPLICATION-TEST-LAYER-DESIGN.md) - Design document
- [`TestHelpers/README.md`](TestHelpers/README.md) - Test helper classes

## Database Lifecycle

Docker must be running. The test assembly starts a shared `postgres:17-alpine`
Testcontainers container. `FeatureTestBase` creates one unique database per derived
fixture and applies EF migrations once for that fixture. Before every test, the
fixture database's application tables are truncated and identities are reset;
the database and test context are recreated so contexts cannot leak state.

The database is dropped after the fixture finishes, and the test container is
stopped after the assembly finishes. Application containers are untouched.

Feature tests initialize their dependencies after the inherited setup.
Failure-injection tests use PostgreSQL trigger functions to verify transaction
rollback against the actual provider.

## Architecture Alignment

```
┌─────────────────────────────────────────────┐
│ Unit Tests                                  │  ← Primary Application Feature harness
│ Application logic + IDataProvider mocks     │
└─────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────┐
│ Integration.Application Tests (boundary)    │  ← Distinct real-provider contracts
└─────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────┐
│ Integration.Data Tests (schema)             │  ← EF configuration + PostgreSQL schema
└─────────────────────────────────────────────┘
```

This layer should contain focused evidence about the Application-to-data boundary, not a duplicate copy of every unit scenario.
