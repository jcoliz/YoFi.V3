# Gherkin Generator Tests

Comprehensive test suite for the Gherkin test generator library.

## Purpose

This project contains 80+ tests that verify the correctness of the Gherkin-to-C# test generation pipeline implemented in [`Gherkin.Generator`](../Gherkin.Generator/).

## Test Structure

### Test Categories

| Test File | Tests | Purpose |
|-----------|-------|---------|
| [`StepMatchingTests.cs`](StepMatchingTests.cs) | 15 | Step pattern matching with placeholders |
| [`GherkinToCrifConverterTests.cs`](GherkinToCrifConverterTests.cs) | 40+ | Gherkin parsing to CRIF conversion |
| [`GherkinToCrifIntegrationTests.cs`](GherkinToCrifIntegrationTests.cs) | 20+ | End-to-end with step matching |
| [`FunctionalTestGeneratorTests.cs`](FunctionalTestGeneratorTests.cs) | 20+ | Mustache template rendering |

### Test Coverage

**StepMatchingTests** - Verifies step matching logic:
- Exact matches (case-insensitive)
- Placeholder matching (`{parameter}`)
- Quoted multi-word arguments (`"Ski Village"`)
- Adjacent placeholders (`{firstName} {lastName}`)
- Multiple parameters
- Keyword filtering (Given/When/Then)

**GherkinToCrifConverterTests** - Verifies Gherkin parsing:
- Feature metadata extraction
- Tag parsing (`@namespace:`, `@baseclass:`, `@using:`, `@explicit`)
- Background steps
- Rules and scenarios
- Data tables (headers, rows, cells)
- Scenario outlines with examples
- Unimplemented step tracking

**GherkinToCrifIntegrationTests** - Verifies end-to-end conversion:
- Step matching integration
- Class and namespace collection
- Argument extraction
- Keyword normalization (And/But → Given/When/Then)
- Mixed implemented/unimplemented steps

**FunctionalTestGeneratorTests** - Verifies code generation:
- Basic template rendering
- Background → `[SetUp]` method
- Data tables → `DataTable` instantiation
- Scenario outlines → `[TestCase]` attributes
- Multiple parameters
- Explicit tag → `[Explicit]` attribute
- Unimplemented steps → stub methods with `NotImplementedException`
- XML documentation (remarks)

## Running Tests

```bash
# Run all tests
dotnet test tests/Gherkin.Generator.Tests

# Run specific test file
dotnet test tests/Gherkin.Generator.Tests --filter "FullyQualifiedName~StepMatchingTests"

# Run with detailed output
dotnet test tests/Gherkin.Generator.Tests --logger "console;verbosity=detailed"
```

All 80+ tests should pass.

## Test Data

The project includes sample test data:

- **[`sample-crif.yaml`](sample-crif.yaml)** - Example CRIF object in YAML format
- **[`sample-output.cs`](sample-output.cs)** - Example generated C# test code
- **[`FunctionalTest.mustache`](FunctionalTest.mustache)** - Template used for code generation (linked from generator project)

## Generation Flow (Documented)

The tests verify this flow:

### 1. Inputs
- **Gherkin feature file** - Human-readable BDD specification
- **Step definitions** - C# classes with `[Given]`, `[When]`, `[Then]` attributes
- **Mustache template** - Defines output C# code structure

### 2. Process
1. Parse Gherkin feature using Gherkin library → `GherkinDocument`
2. Extract step metadata from step definition classes → `StepMetadataCollection`
3. Match Gherkin steps to definitions → `FunctionalTestCrif` (CRIF)
4. Render CRIF with Mustache template → C# test code

### 3. Outputs
- **Compiler-ready C# test file** with NUnit attributes
- **Stub methods** for unimplemented steps

## Step Metadata Structure

Step metadata captures:
- **Normalized keyword** - Given, When, Then (no And/But)
- **Text** - Step pattern with placeholders (e.g., `"I have {quantity} cars"`)
- **Method** - Exact method name
- **Class** - Class containing the step
- **Namespace** - Full namespace path
- **Parameters** - Type and name of each parameter

### Example

```csharp
// Step definition in CarSteps.cs
[Given("I have {quantity} cars in my {place}")]
public async Task IHaveThemCars(int quantity, string place) { }

// Produces metadata:
new StepMetadata
{
    NormalizedKeyword = "Given",
    Text = "I have {quantity} cars in my {place}",
    Method = "IHaveThemCars",
    Class = "CarSteps",
    Namespace = "YoFi.V3.Tests.Functional.Steps",
    Parameters = [
        new StepParameter { Type = "int", Name = "quantity" },
        new StepParameter { Type = "string", Name = "place" }
    ]
};
```

## Step Matching Rules

Tested and verified matching behavior:

1. **Keyword normalization** - And/But normalized to context keyword (Given/When/Then)
2. **Case insensitive** - Text matching ignores case
3. **Exact match first** - Steps without parameters matched before parameterized ones
4. **Placeholder patterns**:
   - `{parameter}` matches single words or quoted phrases
   - Single word: `Ski-Village` ✓
   - Quoted phrase: `"Ski Village"` ✓
   - Unquoted phrase: `Ski Village` ✗ (doesn't match)
5. **Argument extraction** - Values captured from matched placeholders

## Test Philosophy

This project follows **Test-Driven Development (TDD)**:
- Tests written before implementation
- Comprehensive coverage of edge cases
- Integration tests verify end-to-end flow
- Tests document expected behavior

## Related Projects

- **[`Gherkin.Generator`](../Gherkin.Generator/)** - The library being tested
- **[`Functional`](../Functional/)** - Uses generated tests (future consumer of source generator)

## Future: Analyzer Tests

When Phase 4 (Source Generator Integration) is implemented, this project will also test:
- Roslyn syntax analysis for step discovery
- Incremental generation pipeline
- Build-time diagnostics
- `.feature` file handling as `AdditionalFiles`
