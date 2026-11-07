# Testing Strategy

How to test Application Features effectively in YoFi.V3

## The Testing Layers

YoFi.V3 uses a **six-layer testing strategy** optimized for testing where the actual work happens: the Application layer interacting with the database.

```text
┌─────────────────────────────────────────────────────────────┐
│ Functional Tests (10%)                                      │
│ Browser → Frontend → Backend → Database                     │
│ Purpose: E2E user workflows, critical paths                 │
└─────────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────────┐
│ Frontend Component Tests (5%)                               │
│ Vitest + @vue/test-utils, happy-dom                         │
│ Purpose: Component logic, pure utility functions            │
└─────────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────────┐
│ Controller Integration Tests (20%)                          │
│ HTTP → Controllers → (rest of stack)                        │
│ Purpose: API contracts, auth, HTTP-specific concerns        │
└─────────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────────┐
│ Application Integration Tests (40%) ✨ PRIMARY              │
│ Application Features → IDataContext → Database              │
│ Purpose: Business logic + database integration              │
└─────────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────────┐
│ Data Integration Tests (10%)                                │
│ Direct DbContext → PostgreSQL (Testcontainers)              │
│ Purpose: EF Core configurations, schema validation          │
└─────────────────────────────────────────────────────────────┘
                          ↓
┌─────────────────────────────────────────────────────────────┐
│ Unit Tests (15%)                                            │
│ Pure logic, no dependencies                                 │
│ Purpose: Algorithms, validation, entities logic             │
└─────────────────────────────────────────────────────────────┘
```

**Target Distribution:** 10% Functional / 5% Frontend Component / 20% Controller / 40% Application / 10% Data / 15% Unit

**Key Insight:** "Most of the application's actual work happens between the app layer and the data context" - so test that boundary directly.

### Integration Database Lifecycle

All three integration projects require Docker and use PostgreSQL Testcontainers
with real EF migrations. Data and Application tests get a fresh database per
test; Controller tests get an isolated database per WebApplicationFactory.
Test databases and their disposable test containers are cleaned up automatically.
Application containers are separate and must not be stopped by test runs.

Run `.\scripts\Run-Tests.ps1 -Integration` from the repository root. The script
checks Docker availability before starting any selected tests.

## 1. Frontend Component Tests (LOGIC ONLY - 5%)

**Location:** [`src/FrontEnd.Nuxt/tests/`](../src/FrontEnd.Nuxt/tests/)

**Runner:** Vitest + @vue/test-utils + happy-dom

**Purpose:** Test component logic and pure utility functions that are not covered by functional tests.

### When to Use

✅ **DO use Frontend Component tests for:**

- Pure utility functions (date formatting, calculations, transformations)
- Component behavioral logic (conditional rendering based on props/state, event emission)
- Store interactions (does clicking X call the right store method?)
- Role-based visibility (does the manage link appear only for owners?)

❌ **DON'T use Frontend Component tests for:**

- Page-level navigation tests — pages use `useAuth`, `useAuthenticationGuard`, `definePageMeta`, `onBeforeRouteLeave`, `onBeforeRouteUpdate` which cannot be meaningfully mocked. Write functional tests instead.
- HTML structure / CSS class assertions — these break with every layout change and provide no value
- Visual appearance — use visual review and functional tests
- Anything already covered by functional tests

### Speed

~1-5ms per test (very fast, no browser)

### Mocking Patterns

Two distinct patterns depending on how the component imports its dependencies:

**Explicitly imported modules** (e.g., Pinia stores) — use `vi.mock()`:

```typescript
vi.mock('~/stores/userPreferences', () => ({
  useUserPreferencesStore: vi.fn(),
}))
```

**Nuxt auto-imported composables** (e.g., `useApiClient`, `navigateTo`, `handleApiError`) — use `vi.stubGlobal()`:

```typescript
const useApiClientMock = vi.fn()
vi.stubGlobal('useApiClient', useApiClientMock)
vi.stubGlobal('navigateTo', vi.fn())
vi.stubGlobal('handleApiError', (err: unknown) => err)
```

**Components using `<Teleport>`** (e.g., `ModalDialog`) — stub with an inline version that renders slots directly:

```typescript
const ModalDialogStub = {
  props: ['show', 'testId', 'primaryButtonTestId', 'secondaryButtonTestId', 'loading'],
  emits: ['primary', 'update:show'],
  setup(props: any, { slots, emit }: any) {
    return () => props.show
      ? h('div', { 'data-test-id': props.testId }, [
          slots.default?.(),
          h('button', { 'data-test-id': props.primaryButtonTestId, onClick: () => emit('primary') }),
          h('button', { 'data-test-id': props.secondaryButtonTestId, onClick: () => emit('update:show', false) }),
        ])
      : null
  },
}
```

**`NuxtLink` stub** — must use `inheritAttrs: false` to pass `data-test-id` through:

```typescript
const NuxtLink = {
  inheritAttrs: false,
  props: ['to'],
  template: '<a v-bind="$attrs" :href="to"><slot /></a>',
}
```

### Example

```typescript
import { describe, it, expect, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { h } from 'vue'
import MyComponent from '~/components/MyComponent.vue'

vi.mock('~/stores/userPreferences', () => ({
  useUserPreferencesStore: vi.fn(),
}))
vi.stubGlobal('useApiClient', vi.fn())

describe('MyComponent', () => {
  it('shows delete button only for owners', async () => {
    // Given: a component mounted with owner role
    const wrapper = mount(MyComponent, {
      props: { role: 'Owner' },
    })

    // Then: delete button is visible
    expect(wrapper.find('[data-test-id="delete-button"]').exists()).toBe(true)
  })
})
```

**See:** [`src/FrontEnd.Nuxt/tests/README.md`](../src/FrontEnd.Nuxt/tests/README.md) for complete guidelines and patterns.

## 2. Application Integration Tests (PRIMARY - 40%)

**Location:** [`tests/Integration.Application/`](../tests/Integration.Application/)

**Purpose:** Test Application Features with real IDataContext to verify business logic + database integration.

### When to Use Application Integration Tests

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

### Speed of Application Integration Tests

~20-50ms per test (faster than Controller Integration ~100-200ms)

### Example Application Integration Tests

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

## 3. Controller Integration Tests (HTTP-SPECIFIC - 20%)

**Location:** [`tests/Integration.Controller/`](../tests/Integration.Controller/)

**Purpose:** Test HTTP boundary concerns ONLY - don't duplicate business logic testing.

### When to Use Controller Integration Tests

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

### Speed of Controller Integration Tests

~100-200ms per test

### Example of Controller Integration Tests

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

## 4. Data Integration Tests (SCHEMA VALIDATION - 10%)

**Location:** [`tests/Integration.Data/`](../tests/Integration.Data/)

**Purpose:** Test database layer directly - EF Core configurations, schema, relationships.

### When to Use Data Integration Tests

✅ **DO use Data Integration tests for:**

- EF Core entity configurations
- Database schema validation (indexes, constraints)
- Relationship configurations (one-to-many, many-to-many)
- Direct DbContext query behavior
- Database-specific features (SQL functions, stored procedures)

❌ **DON'T use Data Integration tests for:**

- Testing Feature business logic (use Application Integration instead)
- Testing through Features (use Application Integration)

### Speed of Data Integration Tests

~10-20ms per test

## 5. Unit Tests (PURE LOGIC - 15%)

**Location:** [`tests/Unit/`](../tests/Unit/)

**Purpose:** Test pure business logic without any dependencies.

### When to Use Unit Tests

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

### Speed of Unit Tests

~1-10ms per test

## 6. Functional Tests (E2E - 10%)

**Location:** [`tests/Functional/`](../tests/Functional/)

**Purpose:** Test complete user workflows through the browser.

### When to Use Functional Tests

✅ **DO use Functional tests for:**

- Critical user workflows (login, registration, core features)
- End-to-end acceptance tests
- UI-dependent functionality
- Cross-layer integration requiring browser

### Tagging Strategy

Some functional feature sets use tags for planning and traceability:

- `@pri:<n>` - Priority level defined by the feature's test plan (1=highest; use that plan's range)
- `@id:1` through `@id:N` - Sequential scenario identifiers when used by that feature set
- Apply these tags consistently within the feature set; they are not required on every feature.
- Document acceptance criteria as Gherkin scenarios and implement according to the agreed test plan.

### Speed of Functional Tests

~2-5 seconds per test

## Decision Flowchart: Which Test Layer?

```text
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
| ------- | ---------- | ------- | ------------- |
| **Functional** | `tests/Functional/` | 2-5s | E2E Workflows |
| **Frontend Component** | `src/FrontEnd.Nuxt/tests/` | 1-5ms | Component logic, utilities |
| **Controller Integration** | `tests/Integration.Controller/` | 100-200ms | HTTP + Auth |
| **Application Integration** ✨ | `tests/Integration.Application/` | 20-50ms | Feature + IDataProvider |
| **Data Integration** | `tests/Integration.Data/` | 10-20ms | EF Core + Schema |
| **Unit** | `tests/Unit/` | 1-10ms | Pure Logic |

## When In Doubt

**Default to Application Integration tests** - they test where the work happens (Features + Database) without HTTP overhead.

Only use Controller Integration when you specifically need to test HTTP concerns (auth, status codes, serialization).

## Related Documentation

- [`src/FrontEnd.Nuxt/tests/README.md`](../src/FrontEnd.Nuxt/tests/README.md) - Frontend component test guidelines
- [`tests/Integration.Application/README.md`](../tests/Integration.Application/README.md) - Application Integration test patterns
- [`tests/Integration.Controller/README.md`](../tests/Integration.Controller/README.md) - Controller Integration test patterns
- [`tests/Integration.Data/README.md`](../tests/Integration.Data/README.md) - Data Integration test patterns
- [`tests/Unit/README.md`](../tests/Unit/README.md) - Unit test patterns
- [`tests/Functional/README.md`](../tests/Functional/README.md) - Functional test patterns
- [`docs/wip/TESTING-STRATEGY-UPDATE-PLAN.md`](wip/TESTING-STRATEGY-UPDATE-PLAN.md) - Complete strategy update plan
- [`.roorules`](../.roorules) - Project testing standards and patterns
