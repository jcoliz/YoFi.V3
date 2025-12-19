# Infrastructure

This directory contains core test infrastructure classes shared across all functional tests.

## Contents

### [`FunctionalTestBase.cs`](FunctionalTestBase.cs)

Base class providing infrastructure for all functional tests.

**Provides:**
- Test lifecycle management (setup/teardown)
- Playwright browser configuration
- Test correlation headers for distributed tracing (W3C Trace Context)
- Prerequisite checking (browsers installed, backend health)
- Object store access for sharing data between test steps
- Test Control API client for test data setup/cleanup
- Screenshot capture utilities

**Usage:**
All step definition classes inherit from this base class (through the Common step hierarchy).

### [`ObjectStore.cs`](ObjectStore.cs)

In-memory store for sharing objects between test steps.

**Purpose:**
Enables test step methods to share data without requiring local variables, making tests generatable from Gherkin feature files.

**Usage:**
- `Add<T>(obj)` - Store an object by its type name
- `Add<T>(key, obj)` - Store an object with a specific key
- `Get<T>()` - Retrieve an object by type
- `Get<T>(key)` - Retrieve an object by key
- `Contains<T>()` - Check if an object of type exists
- `Contains<T>(key)` - Check if an object with key exists

## Architecture

The infrastructure layer sits at the foundation of the test hierarchy:

```
FunctionalTestBase (Infrastructure/)
    ↓
CommonGivenSteps (Steps/Common/)
    ↓
CommonWhenSteps (Steps/Common/)
    ↓
CommonThenSteps (Steps/Common/)
    ↓
AuthenticationSteps, WeatherSteps, WorkspaceTenancySteps (Steps/)
    ↓
Generated Test Classes (Tests/)
```

## Design Principles

1. **Separation of Concerns**: Infrastructure code is isolated from step definitions
2. **Single Responsibility**: Each class has a focused purpose
3. **Testability**: Clean abstractions enable easier testing
4. **Reusability**: Shared infrastructure reduces duplication

## Related Documentation

- [`../Steps/Common/README.md`](../Steps/Common/README.md) - Common step definitions
- [`../README.md`](../README.md) - Functional tests overview
- [`../../docs/wip/functional-tests/FUNCTIONAL-TESTS-ORGANIZATION-ANALYSIS.md`](../../../docs/wip/functional-tests/FUNCTIONAL-TESTS-ORGANIZATION-ANALYSIS.md) - Architecture analysis
