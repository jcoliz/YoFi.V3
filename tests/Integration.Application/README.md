# Integration.Application Tests

Tests Application Features with real database through IDataProvider interface.

## Purpose

Test that Application Features correctly use `IDataProvider` methods to interact with the database, including:

- Navigation property loading (e.g., Splits on Transactions)
- Complex queries and filtering
- Multi-entity operations
- Business logic requiring database state

## Why This Layer Exists

**The Gap:** We need tests that verify Application Features work correctly with a real database, **without** the overhead of HTTP testing through Controllers.

**Key Insight:** "Most of the application's actual work happens between the app layer and the data context."

This layer catches bugs at the Feature level (fast, ~20-50ms per test) rather than the Controller level (slower, ~100-200ms per test).

## When to Use

✅ **DO use Integration.Application tests for:**
- Testing Feature methods that use IDataProvider
- Verifying navigation properties load correctly
- Testing complex queries and filtering through Features
- Testing multi-entity operations
- Testing business rules that require database state

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
pwsh -File ./scripts/Run-Tests.ps1

# Run only Integration.Application tests
dotnet test tests/Integration.Application

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

## Speed Expectations

- **Target:** 20-50ms per test
- **Reality:** Faster than Controller Integration tests (~100-200ms)
- **Database:** In-memory SQLite (same as Integration.Data)

## Architecture Alignment

```
┌─────────────────────────────────────────────┐
│ Integration.Controller Tests (HTTP layer)  │  ← Test Controllers + HTTP
└─────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────┐
│ Integration.Application Tests ✨ (PRIMARY)  │  ← Test Features + IDataProvider
└─────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────┐
│ Integration.Data Tests (Schema layer)      │  ← Test DbContext + Schema
└─────────────────────────────────────────────┘
```

This layer tests the **primary work location** in YoFi.V3 - where Features interact with the database through IDataProvider.
