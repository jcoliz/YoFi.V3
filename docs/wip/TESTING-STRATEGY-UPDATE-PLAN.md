---
status: Draft
---

# Testing Strategy Update Plan

**Moving testing primacy from Controller Integration to Application Integration**

## Executive Summary

**Problem:** Current testing strategy over-emphasizes Controller Integration tests (60%) and under-tests the Application layer where most business logic lives.

**User Insight:** "Most of the application's actual work happens between the app layer and the data context."

**Solution:** Shift testing primacy to Application Integration tests (45%), making them the primary test surface while reducing Controller Integration to HTTP-specific concerns (20%).

**Impact:** Faster feedback, clearer failure messages, better architecture alignment, catches bugs earlier at the Feature layer.

## Current State vs. Proposed State

### Current Test Distribution (TESTING-STRATEGY.md)

```
                    ▲
                   ╱ ╲
                  ╱   ╲
                 ╱ E2E  ╲           15% - Functional
                ╱  15%   ╲
               ╱___________╲
              ╱             ╲
             ╱  Controller   ╲      60% - Controller Integration ← TOO HIGH
            ╱   Integration   ╲
           ╱       60%         ╲
          ╱_____________________╲
         ╱                       ╱
        ╱        Unit            ╱   25% - Unit Tests
       ╱         25%            ╱
      ╱_________________________╱
```

**Problems:**
- Controller Integration tests verify business logic indirectly through HTTP (~200ms per test)
- Hard to debug failures (is it HTTP? Auth? Feature? Query? Navigation loading?)
- Doesn't align with Clean Architecture (tests wrong layer)
- Recent Splits bug wasn't caught because tests didn't verify navigation property loading

### Proposed Test Distribution

```
┌─────────────────────────────────────────────────────────┐
│ Functional Tests (10%)                                  │
│ Browser → Frontend → Backend → Database                 │
│ Purpose: E2E user workflows, critical paths             │
└─────────────────────────────────────────────────────────┘
                         ↓
┌─────────────────────────────────────────────────────────┐
│ Controller Integration Tests (20%)                      │
│ HTTP → Controllers → (rest of stack)                    │
│ Purpose: API contracts, auth, HTTP-specific concerns    │
└─────────────────────────────────────────────────────────┘
                         ↓
┌─────────────────────────────────────────────────────────┐
│ Application Integration Tests (45%) ✨ PRIMARY          │
│ Application Features → IDataContext → Database          │
│ Purpose: Business logic + database integration          │
└─────────────────────────────────────────────────────────┘
                         ↓
┌─────────────────────────────────────────────────────────┐
│ Data Integration Tests (10%)                            │
│ Direct DbContext → Database (in-memory)                 │
│ Purpose: EF Core configurations, schema validation      │
└─────────────────────────────────────────────────────────┘
                         ↓
┌─────────────────────────────────────────────────────────┐
│ Unit Tests (15%)                                        │
│ Pure logic, no dependencies                             │
│ Purpose: Algorithms, validation, entities logic         │
└─────────────────────────────────────────────────────────┘
```

**Distribution:** 10% Functional / 20% Controller / 45% Application / 10% Data / 15% Unit

## Updated Test Layer Guidelines

### 1. Application Integration Tests (PRIMARY - 45%)

**Location:** `tests/Integration.Application/` (NEW)

**Purpose:** Test Application Features with real IDataContext to verify business logic + database integration.

**When to Use:**
- ✅ Any Feature method that uses IDataContext
- ✅ Navigation property loading verification
- ✅ Complex queries and filtering through Features
- ✅ Multi-entity operations
- ✅ Business rules requiring database state
- ✅ Feature query builders (e.g., `GetTransactionsWithSplits()`)

**Speed:** ~20-50ms per test

**Example Test:**
```csharp
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

    // Clear tracking to simulate fresh query
    _context.ChangeTracker.Clear();

    // When: Getting transaction through TransactionsFeature
    var result = await _transactionsFeature.GetTransactionByKeyAsync(transaction.Key);

    // Then: Splits collection should be loaded
    Assert.That(result, Is.Not.Null);
    Assert.That(result.Splits, Is.Not.Empty);
    Assert.That(result.Splits.First().Category, Is.EqualTo("Groceries"));
}
```

**Key Characteristics:**
- Tests Features directly (not through HTTP)
- Uses real ApplicationDbContext (in-memory SQLite)
- Verifies navigation properties load correctly
- Exercises IDataProvider interface like production
- Fast enough for frequent execution

### 2. Controller Integration Tests (HTTP-SPECIFIC - 20%)

**Location:** `tests/Integration.Controller/` (EXISTING)

**Purpose:** Test HTTP boundary concerns ONLY - don't duplicate business logic testing.

**When to Use:**
- ✅ Authentication/authorization middleware
- ✅ HTTP status codes (401, 403, 404, etc.)
- ✅ Request/response serialization
- ✅ Content negotiation and headers
- ✅ Error handling middleware
- ✅ API versioning, CORS, rate limiting

**When NOT to Use:**
- ❌ Testing business logic (use Application Integration instead)
- ❌ Testing navigation property loading (use Application Integration)
- ❌ Testing complex queries (use Application Integration)

**Speed:** ~100-200ms per test

**Example Test:**
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

**Key Characteristics:**
- Tests complete HTTP pipeline
- Verifies API contracts
- Focuses on HTTP-specific concerns
- Does NOT verify business logic (that's tested at Application layer)

### 3. Data Integration Tests (SCHEMA VALIDATION - 10%)

**Location:** `tests/Integration.Data/` (EXISTING)

**Purpose:** Test database layer directly - EF Core configurations, schema, relationships.

**When to Use:**
- ✅ EF Core entity configurations
- ✅ Database schema validation (indexes, constraints)
- ✅ Relationship configurations (one-to-many, many-to-many)
- ✅ Direct DbContext query behavior
- ✅ Database-specific features (SQL functions, stored procedures)

**When NOT to Use:**
- ❌ Testing Feature business logic (use Application Integration instead)
- ❌ Testing through Features (use Application Integration)

**Speed:** ~10-20ms per test

**Key Characteristics:**
- Tests DbContext directly (not through Features)
- Verifies EF Core configurations are correct
- Catches schema and relationship misconfigurations
- Complements Application Integration tests

### 4. Unit Tests (PURE LOGIC - 15%)

**Location:** `tests/Unit/` (EXISTING)

**Purpose:** Test pure business logic without any dependencies.

**When to Use:**
- ✅ Entities layer validation and logic
- ✅ Application layer logic testable WITHOUT IDataContext
- ✅ Algorithms, calculations, transformations
- ✅ DTO mapping with complex rules
- ✅ Validation rules with edge cases
- ✅ Pure domain logic

**When NOT to Use:**
- ❌ Anything requiring IDataContext (use Application Integration)
- ❌ Anything requiring HTTP context (use Controller Integration)

**Speed:** ~1-10ms per test

**Key Characteristics:**
- Fastest tests in the suite
- No external dependencies (no DB, no HTTP)
- Tests pure logic and algorithms
- Complements integration tests

### 5. Functional Tests (E2E - 10% implemented, 25-50% documented)

**Location:** `tests/Functional/` (EXISTING)

**Purpose:** Test complete user workflows through the browser.

**Strategy:** Create Gherkin scenarios for ALL acceptance criteria (comprehensive documentation), then implement only the top 25-50% most critical scenarios.

**When to Use:**
- ✅ Critical user workflows (login, registration, core features)
- ✅ End-to-end acceptance tests
- ✅ UI-dependent functionality
- ✅ Cross-layer integration requiring browser

**Speed:** ~2-5 seconds per test

**Documentation-First Approach:**
1. Create complete Gherkin feature file for PRD (e.g., `tests/Functional/Features/future/PayeeRules-Prd.feature`)
2. Document ALL acceptance criteria as Gherkin scenarios (comprehensive coverage)
3. Tag scenarios by priority: `@implemented`, `@future`, `@critical`, `@medium`, `@low`
4. Implement top 25-50% most critical scenarios
5. Keep unimplemented scenarios documented for future reference

**Example Structure:**
```gherkin
# tests/Functional/Features/future/PayeeRules-Prd.feature

Feature: Payee Rules Management
  # All acceptance criteria documented here

  @implemented @critical
  Scenario: User creates basic payee rule
    Given user is logged in as an editor
    When user creates a payee rule matching "Safeway" with category "Groceries"
    Then rule should appear in payee rules list
    And rule should be marked as active

  @implemented @critical
  Scenario: Rule automatically categorizes matching transaction
    Given user has a payee rule for "Safeway" → "Groceries"
    When user imports a transaction with payee "SAFEWAY #1234"
    Then transaction should be automatically categorized as "Groceries"

  @future @medium
  Scenario: User edits existing rule priority
    Given user has multiple rules that could match same payee
    When user changes rule priority order
    Then transactions should be categorized by highest priority rule

  @future @low
  Scenario: User views rule match statistics
    Given user has multiple active rules
    When user views rule statistics page
    Then user should see how many transactions each rule has matched
```

**Target Coverage:**
- **100% of acceptance criteria documented** as Gherkin scenarios
- **25-50% of scenarios actually implemented** (highest priority only)
- Unimplemented scenarios serve as documentation and future test backlog

**Key Characteristics:**
- Slowest tests, implement selectively based on risk
- Tests user-visible behavior
- Verifies frontend + backend + database integration
- Complete Gherkin documentation for all acceptance criteria
- Only 25-50% of documented scenarios implemented
- Clear priority-based implementation strategy (`@implemented`, `@future`, `@critical`, `@medium`, `@low`)

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

## Implementation Roadmap

### Phase 1: Foundation with Sample Migration (Immediate)

**Goal:** Create Application Integration test infrastructure, update documentation, and migrate WeatherForecast as a working example.

**Tasks:**
1. ✅ Review existing [`docs/wip/INTEGRATION-APPLICATION-TEST-LAYER-DESIGN.md`](INTEGRATION-APPLICATION-TEST-LAYER-DESIGN.md)
2. Create `tests/Integration.Application/` project
   - Add project file with NUnit, EF Core, SQLite dependencies
   - Create `FeatureTestBase` helper class
   - Create `TestCurrentTenant` mock
   - Add README.md with patterns and examples
3. **Migrate WeatherForecast tests as sample**
   - Move tests from [`tests/Integration.Controller/`](../../tests/Integration.Controller/) to new Application Integration layer
   - Demonstrate testing Feature directly without HTTP overhead
   - Serves as reference implementation for developers
4. Update [`docs/TESTING-STRATEGY.md`](../TESTING-STRATEGY.md)
   - Add Application Integration layer section
   - Update test distribution pyramid (10/15/10/45/20)
   - Update decision flowchart
   - Add Application Integration examples (including WeatherForecast)
   - Update "when to use each layer" guidance
5. Update [`.roorules`](../../.roorules)
   - Add Application Integration test patterns
   - Update test execution pattern to include new layer
   - Add guidance on test layer selection

**Success Criteria:**
- `tests/Integration.Application/` project created and builds successfully
- WeatherForecast tests successfully migrated and passing
- Documentation clearly describes all five test layers with working example
- Developers have reference implementation to follow

### Phase 2: Update Test Execution Scripts (High Priority)

**Goal:** Ensure all test layers are executed correctly in local dev and CI, and use code coverage as migration guide.

**Tasks:**
1. Update [`scripts/Run-Tests.ps1`](../../scripts/Run-Tests.ps1)
   - Include `tests/Integration.Application/` in test run
   - Update output to show all five test layers clearly
   - Display test count and execution time per layer
2. Update [`scripts/Collect-CodeCoverage.ps1`](../../scripts/Collect-CodeCoverage.ps1)
   - **Change coverage focus from Controller Integration to Application Integration + Unit**
   - Track coverage by test layer (Unit, Application Integration, Controller Integration)
   - Use coverage metrics as migration guide: identify Features needing Application Integration tests
   - Goal: Increase coverage via Application Integration (not Controller Integration)
3. Update CI/CD pipelines
   - Ensure Application Integration tests run in CI
   - Verify test reporting includes all layers
   - Track test execution time per layer
   - Report code coverage by test layer

**Code Coverage as Migration Guide:**
- Current coverage is primarily from Controller Integration tests
- As we add Application Integration tests, coverage should increase at Application layer
- Use coverage gaps to identify Features needing retrofit
- Track coverage by layer: "Unit + Application Integration" vs. "Controller Integration"
- Goal: Shift coverage source from Controller → Application Integration

**Success Criteria:**
- `./scripts/Run-Tests.ps1` runs all test layers including Application Integration
- `./scripts/Collect-CodeCoverage.ps1` tracks coverage by test layer
- Coverage metrics show Application Integration contribution
- CI pipeline shows clear breakdown by test layer and coverage source
- Test execution time tracked per layer
- Developers see Application Integration tests in normal workflow
- Coverage tool identifies Features needing Application Integration tests

### Phase 3: Gradual Adoption (Ongoing)

**Goal:** Shift testing culture toward Application Integration as primary layer for new development.

**Guidelines:**
- **New Features:** Write Application Integration tests FIRST
- **Bug Fixes:** Add Application Integration test to prevent regression
- **Refactoring:** Update tests to use Application Integration where appropriate
- **Code Review:** Verify new Features have Application Integration coverage

**Documentation Updates:**
- Reference WeatherForecast tests as pattern to follow
- Emphasize Application Integration in implementation workflow
- Update PRD test mapping examples to show Application Integration first

**Success Criteria:**
- New Features have 40-50% Application Integration coverage
- Controller Integration tests focus on HTTP concerns only
- Developers default to Application Integration for business logic
- WeatherForecast example referenced in code reviews

### Phase 4: Comprehensive Retrofit (Medium Priority)

**Goal:** Systematically add Application Integration tests for existing Features with Controller Integration tests.

**Features IN SCOPE for Retrofit:**
1. **TransactionsFeature** (Immediate - Recent Splits bug)
   - `GetTransactionByKeyAsync()` - Verify Splits collection loads
   - `UpdateTransactionAsync()` - Verify Splits loads for update logic
   - `QuickEditTransactionAsync()` - Verify Splits preserved
   - `GetTransactionsAsync()` - Verify Splits loads for all transactions

2. **TenantFeature** (High - Multi-tenancy isolation critical)
   - Verify tenant-scoped queries work correctly
   - Test ICurrentTenant integration with Features
   - Verify tenant isolation across all operations

3. **Import/Export Features** (Medium - Existing tests need migration)
   - Bank import workflow
   - Data export features
   - Tenant data administration

**Features NOT IN SCOPE for Retrofit** (will natively use Application Integration):
- ❌ **Payee Rules Features** - No existing tests, will start with Application Integration from day one
- ❌ **Budget Features** - No existing tests, will start with Application Integration from day one
- ❌ **Report Features** - No existing tests, will start with Application Integration from day one

**Migration Approach:**
1. Add Application Integration tests for Feature business logic
2. Keep Controller Integration tests ONLY for HTTP-specific concerns (auth, status codes, serialization)
3. **Remove duplicate Controller Integration tests** that only verify business logic
4. Focus Application Integration tests on: navigation property loading, complex queries, business logic

**Example Migration:**
```
Before:
- Controller test: POST /api/transactions → verifies business logic + HTTP contract

After:
- Application Integration test: TransactionsFeature.CreateAsync() → verifies business logic
- Controller test: POST /api/transactions → verifies 201 status code + auth only
- Remove: Controller test that duplicates business logic verification
```

**Success Criteria:**
- All in-scope Features have Application Integration test coverage
- Navigation property loading verified for all Features
- Complex business logic tested at Application layer
- Duplicate Controller tests removed (keep only HTTP-specific tests)
- Confidence in Feature + IDataContext integration across codebase

### Phase 5: Metrics and Refinement (Future)

**Goal:** Track test distribution and adjust strategy based on data.

**Metrics to Track:**
1. Test count by layer (actual vs. target percentages)
2. Test execution time by layer
3. Bugs caught by each test layer
4. Test maintenance burden (flakiness, update frequency)

**Success Criteria:**
- Test distribution approaches target (10/15/10/45/20)
- Application Integration tests catch bugs early
- Clear ROI from testing investment

## Migration Strategy for Existing Tests

### Tests to Keep As-Is

**Controller Integration Tests (tests/Integration.Controller/):**
- Keep all authentication/authorization tests
- Keep HTTP status code tests (401, 403, 404)
- Keep serialization/deserialization tests
- Keep middleware tests

**Data Integration Tests (tests/Integration.Data/):**
- Keep all EF Core configuration tests
- Keep schema validation tests
- Keep relationship tests
- Keep database-specific tests

**Unit Tests (tests/Unit/):**
- Keep all pure logic tests
- Keep algorithm tests
- Keep validation tests

**Functional Tests (tests/Functional/):**
- Keep all E2E workflow tests
- Keep critical path tests

### Tests to Consider Migrating

**Controller Integration Tests that Test Business Logic:**
- Identify tests that verify Feature behavior through HTTP
- Consider adding Application Integration test for the Feature
- Keep Controller Integration test if it verifies HTTP-specific behavior

**Example:**
- Current: `POST /api/transactions returns 201 with correct response body`
- Keep: Controller Integration test for 201 status code
- Add: Application Integration test for `CreateTransactionAsync()` logic

### No Forced Migration

**Principle:** Don't force-migrate existing tests unless there's a clear benefit.

**Exceptions:**
1. Bug found in production → Add Application Integration test
2. Test is flaky due to HTTP overhead → Convert to Application Integration
3. Test is slow → Consider moving to faster layer
4. Feature refactoring → Update tests to match new architecture

## Success Metrics

### How We'll Know This Is Working

**1. Faster Feedback Loop**
- Application Integration tests run in ~20-50ms (vs. 100-200ms for Controller)
- Developers get faster feedback on Feature changes
- CI pipeline completes faster

**2. Bugs Caught Earlier**
- Navigation property loading issues caught at Application layer
- Feature logic bugs found before Controller layer
- Reduced debugging time (know which layer failed)

**3. Better Test Distribution**
- Approaching target: 10% Functional / 20% Controller / 45% Application / 10% Data / 15% Unit
- Clear separation of concerns across test layers
- Less test duplication

**4. Developer Confidence**
- Developers trust Feature methods load data correctly
- Less "it works in dev but fails in production"
- Clear guidance on which test layer to use

**5. Maintainability**
- Tests easier to debug (clear layer boundaries)
- Less brittle tests (not testing through HTTP when unnecessary)
- Clearer test intent and documentation

## Decisions Made

### 1. Test Distribution Targets: Conceptual Guidance Only ✅

**Decision:** Use conceptual guidance rather than exact percentage targets.

**Rationale:** Features vary significantly (algorithm-heavy vs. CRUD-heavy vs. workflow-heavy). Exact percentages would be misleading and create artificial constraints.

**Approach:** Describe test layer priorities ("Application Integration is primary") with general guidance (40-50% range) but don't enforce strict targets.

### 2. Update PRD Examples: Yes, Critical ✅

**Decision:** Update all PRD examples in TESTING-STRATEGY.md to reflect Application Integration as primary layer.

**Rationale:** "We definitely don't want wrong information in testing strategy. I'd rather remove and have nothing than have something wrong."

**Approach:**
- Rework PRD examples (Splits, Attachments, Bank Import) to show Application Integration tests first
- Show Controller Integration tests only for HTTP-specific concerns
- Remove examples entirely if they can't be updated accurately

### 3. Retrofitting Strategy: Comprehensive (Option B) ✅

**Decision:** Retrofit all existing Features gradually - comprehensive coverage is the goal.

**Rationale:** "We will actually do option B. Not option C, we do want it all done. Not Option A, because it's not urgent, just needs to get done."

**Approach:**
- Phase 4 focuses on systematic retrofit of all Features
- Priority order: TransactionsFeature → TenantFeature → All Other Features
- Not urgent, but comprehensive coverage is expected
- Add Application Integration tests alongside existing Controller tests (don't remove)

## Related Documentation

- [`docs/wip/INTEGRATION-APPLICATION-TEST-LAYER-DESIGN.md`](INTEGRATION-APPLICATION-TEST-LAYER-DESIGN.md) - Original design document
- [`docs/TESTING-STRATEGY.md`](../TESTING-STRATEGY.md) - Current testing strategy (to be updated)
- [`.roorules`](../../.roorules) - Project rules including test patterns
- [`tests/Integration.Controller/TESTING-GUIDE.md`](../../tests/Integration.Controller/TESTING-GUIDE.md) - Controller testing guide
- [`tests/Integration.Data/README.md`](../../tests/Integration.Data/README.md) - Data layer testing guide

## Conclusion

The shift from Controller Integration as primary (60%) to Application Integration as primary (45%) better aligns testing with where the actual work happens in YoFi.V3.

**Key Insight:** "Most of the application's actual work happens between the app layer and the data context" - so test that boundary directly.

**Benefits:**
- Faster feedback (50ms vs 200ms)
- Clearer failures (know which layer broke)
- Better architecture alignment (test each layer properly)
- Catches bugs earlier (at Feature layer, not Controller layer)
- Maintains all existing test layers (nothing deprecated)

**Next Steps:**
1. Get user approval on this plan
2. Create `tests/Integration.Application/` project
3. Update documentation
4. Retrofit TransactionsFeature tests
5. Adopt for new Features going forward
