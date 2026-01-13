# Testing Strategy

**How to test Application Features effectively in YoFi.V3**

## The Testing Layers

YoFi.V3 uses a **five-layer testing strategy** optimized for testing where the actual work happens: the Application layer interacting with the database.

```
┌─────────────────────────────────────────────────────────────┐
│ Functional Tests (10%)                                      │
│ Browser → Frontend → Backend → Database                     │
│ Purpose: E2E user workflows, critical paths                 │
└─────────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────────┐
│ Controller Integration Tests (20%)                          │
│ HTTP → Controllers → (rest of stack)                        │
│ Purpose: API contracts, auth, HTTP-specific concerns        │
└─────────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────────┐
│ Application Integration Tests (45%) ✨ PRIMARY              │
│ Application Features → IDataContext → Database              │
│ Purpose: Business logic + database integration              │
└─────────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────────┐
│ Data Integration Tests (10%)                                │
│ Direct DbContext → Database (in-memory)                     │
│ Purpose: EF Core configurations, schema validation          │
└─────────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────────┐
│ Unit Tests (15%)                                            │
│ Pure logic, no dependencies                                 │
│ Purpose: Algorithms, validation, entities logic             │
└─────────────────────────────────────────────────────────────┘
```

**Target Distribution:** 10% Functional / 20% Controller / 45% Application / 10% Data / 15% Unit

**Key Insight:** "Most of the application's actual work happens between the app layer and the data context" - so test that boundary directly.

## 1. Application Integration Tests (PRIMARY - 45%)

**Location:** [`tests/Integration.Application/`](../tests/Integration.Application/)

**Purpose:** Test Application Features with real IDataContext to verify business logic + database integration.

### When to Use

✅ **DO use Application Integration tests for:**
- Any Feature method that uses IDataContext
- Navigation property loading verification
- Complex queries and filtering through Features
- Multi-entity operations
- Business rules requiring database state
- Feature query builders (e.g., `GetTransactionsWithSplits()`)

❌ **DON'T use Application Integration tests for:**
- HTTP contracts, status codes, auth → Use Controller Integration instead
- Database schema, EF Core configs → Use Data Integration instead
- Pure business logic without database → Use Unit tests instead

### Speed

~20-50ms per test (faster than Controller Integration ~100-200ms)

### Example

```csharp
[TestFixture]
public class WeatherFeatureTests : FeatureTestBase
{
    private WeatherFeature _weatherFeature;

    [SetUp]
    public new async Task SetUp()
    {
        await base.SetUp();
        _weatherFeature = new WeatherFeature(_dataProvider);
    }

    [Test]
    public async Task GetWeatherForecasts_ExistingData_ReturnsFromDatabase()
    {
        // Given: Existing weather forecast data in the database
        var forecast = new WeatherForecast
        {
            Id = Guid.NewGuid(),
            Date = new DateOnly(2025, 6, 15),
            TemperatureC = 25,
            Summary = "Warm"
        };
        _context.WeatherForecasts.Add(forecast);
        await _context.SaveChangesAsync();

        // When: Weather forecasts are requested
        var result = await _weatherFeature.GetWeatherForecasts(5);

        // Then: Should return forecasts from database (not generate new ones)
        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result.First().Summary, Is.EqualTo("Warm"));
    }
}
```

**See:** [`tests/Integration.Application/WeatherFeatureTests.cs`](../tests/Integration.Application/WeatherFeatureTests.cs) for complete reference implementation.

## 2. Controller Integration Tests (HTTP-SPECIFIC - 20%)

**Location:** [`tests/Integration.Controller/`](../tests/Integration.Controller/)

**Purpose:** Test HTTP boundary concerns ONLY - don't duplicate business logic testing.

### When to Use

✅ **DO use Controller Integration tests for:**
- Authentication/authorization middleware
- HTTP status codes (401, 403, 404, etc.)
- Request/response serialization
- Content negotiation and headers
- Error handling middleware
- API versioning, CORS, rate limiting

❌ **DON'T use Controller Integration tests for:**
- Testing business logic (use Application Integration instead)
- Testing navigation property loading (use Application Integration)
- Testing complex queries (use Application Integration)

### Speed

~100-200ms per test

### Example

```csharp
[Test]
public async Task GetTransactions_Unauthenticated_Returns401()
{
    // Given: No authentication token

    // When: Request is made without authentication
    var response = await _client.GetAsync("/api/tenant/123/transactions");

    // Then: 401 Unauthorized should be returned
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
}
```

## 3. Data Integration Tests (SCHEMA VALIDATION - 10%)

**Location:** [`tests/Integration.Data/`](../tests/Integration.Data/)

**Purpose:** Test database layer directly - EF Core configurations, schema, relationships.

### When to Use

✅ **DO use Data Integration tests for:**
- EF Core entity configurations
- Database schema validation (indexes, constraints)
- Relationship configurations (one-to-many, many-to-many)
- Direct DbContext query behavior
- Database-specific features (SQL functions, stored procedures)

❌ **DON'T use Data Integration tests for:**
- Testing Feature business logic (use Application Integration instead)
- Testing through Features (use Application Integration)

### Speed

~10-20ms per test

## 4. Unit Tests (PURE LOGIC - 15%)

**Location:** [`tests/Unit/`](../tests/Unit/)

**Purpose:** Test pure business logic without any dependencies.

### When to Use

✅ **DO use Unit tests for:**
- Entities layer validation and logic
- Application layer logic testable WITHOUT IDataContext
- Algorithms, calculations, transformations
- DTO mapping with complex rules
- Validation rules with edge cases
- Pure domain logic

❌ **DON'T use Unit tests for:**
- Anything requiring IDataContext (use Application Integration)
- Anything requiring HTTP context (use Controller Integration)

### Speed

~1-10ms per test

## 5. Functional Tests (E2E - 10%)

**Location:** [`tests/Functional/`](../tests/Functional/)

**Purpose:** Test complete user workflows through the browser.

### When to Use

✅ **DO use Functional tests for:**
- Critical user workflows (login, registration, core features)
- End-to-end acceptance tests
- UI-dependent functionality
- Cross-layer integration requiring browser

### Speed

~2-5 seconds per test

## Decision Flowchart: Which Test Layer?

```
┌─────────────────────────────────────────────────────────┐
│  START: Analyzing What to Test                          │
└───────────────────────┬─────────────────────────────────┘
                        │
                        ▼
        ┌───────────────────────────────────┐
        │ Does it require browser/UI?       │
        └───────────┬───────────────────────┘
                    │
        ┌───────────┴───────────┐
        │ YES                   │ NO
        ▼                       ▼
┌───────────────────┐   ┌───────────────────────────────────┐
│ ✅ FUNCTIONAL     │   │ Does it require HTTP context      │
│                   │   │ (auth, headers, status codes)?    │
└───────────────────┘   └───────────┬───────────────────────┘
                                    │
                        ┌───────────┴───────────┐
                        │ YES                   │ NO
                        ▼                       ▼
                ┌───────────────────┐   ┌───────────────────────────────┐
                │ ✅ CONTROLLER     │   │ Does it require IDataContext? │
                │ INTEGRATION       │   └───────────┬───────────────────┘
                │                   │               │
                │ Examples:         │   ┌───────────┴───────────┐
                │ • Auth middleware │   │ YES                   │ NO
                │ • 401/403 status  │   ▼                       ▼
                │ • Serialization   │   ┌───────────────────┐   ┌───────────────────┐
                └───────────────────┘   │ Is it testing     │   │ ✅ UNIT TEST      │
                                        │ EF Core directly? │   │                   │
                                        └───────┬───────────┘   │ Examples:         │
                                                │               │ • Validation      │
                                    ┌───────────┴───────────┐   │ • Algorithms      │
                                    │ YES               NO  │   │ • Calculations    │
                                    ▼                   ▼   │   └───────────────────┘
                            ┌───────────────┐   ┌───────────────────┐
                            │ ✅ DATA       │   │ ✅ APPLICATION    │
                            │ INTEGRATION   │   │ INTEGRATION       │
                            │               │   │                   │
                            │ Examples:     │   │ 🎯 PRIMARY LAYER  │
                            │ • EF configs  │   │                   │
                            │ • Schema      │   │ Examples:         │
                            │ • Indexes     │   │ • Feature queries │
                            └───────────────┘   │ • Nav properties  │
                                                │ • Business logic  │
                                                └───────────────────┘
```

## Test Documentation Standards

### Gherkin-Style Comments (Required)

All tests MUST use Gherkin-style comments (Given/When/Then/And) to document test scenarios.

```csharp
[Test]
public async Task GetTransactions_InvalidTenantIdFormat_Returns404()
{
    // Given: A request with an invalid tenant ID format (not a valid GUID)

    // When: API Client requests transactions with invalid tenant ID format
    var response = await _client.GetAsync("/api/tenant/1/transactions");

    // Then: 404 Not Found should be returned
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

    // And: Response should contain problem details
    var content = await response.Content.ReadAsStringAsync();
    Assert.That(content, Is.Not.Empty);
}
```

### NUnit Testing Framework

YoFi.V3 uses **NUnit** as the standard testing framework.

**Required attributes:**
- `[Test]` - Test methods
- `[TestFixture]` - Test classes
- `[SetUp]` / `[TearDown]` - Test lifecycle
- `[TestCase]` - Parameterized tests
- `[Explicit("reason")]` - Intentionally skipped tests

**Constraint-based assertions:**
```csharp
Assert.That(actual, Is.EqualTo(expected));
Assert.That(collection, Is.Not.Empty);
Assert.That(value, Is.GreaterThan(0));
```

## Quick Reference

| Layer | Location | Speed | Primary Use |
|-------|----------|-------|-------------|
| **Application Integration** ✨ | `tests/Integration.Application/` | 20-50ms | Feature + IDataProvider |
| **Controller Integration** | `tests/Integration.Controller/` | 100-200ms | HTTP + Auth |
| **Data Integration** | `tests/Integration.Data/` | 10-20ms | EF Core + Schema |
| **Unit** | `tests/Unit/` | 1-10ms | Pure Logic |
| **Functional** | `tests/Functional/` | 2-5s | E2E Workflows |

## When In Doubt

**Default to Application Integration tests** - they test where the work happens (Features + Database) without HTTP overhead.

Only use Controller Integration when you specifically need to test HTTP concerns (auth, status codes, serialization).

## Related Documentation

- [`tests/Integration.Application/README.md`](../tests/Integration.Application/README.md) - Application Integration test patterns
- [`tests/Integration.Controller/README.md`](../tests/Integration.Controller/README.md) - Controller Integration test patterns
- [`tests/Integration.Data/README.md`](../tests/Integration.Data/README.md) - Data Integration test patterns
- [`tests/Unit/README.md`](../tests/Unit/README.md) - Unit test patterns
- [`tests/Functional/README.md`](../tests/Functional/README.md) - Functional test patterns
- [`docs/wip/TESTING-STRATEGY-UPDATE-PLAN.md`](wip/TESTING-STRATEGY-UPDATE-PLAN.md) - Complete strategy update plan
- [`docs/wip/INTEGRATION-APPLICATION-TEST-LAYER-DESIGN.md`](wip/INTEGRATION-APPLICATION-TEST-LAYER-DESIGN.md) - Application Integration layer design
- [`.roorules`](../.roorules) - Project testing standards and patterns
