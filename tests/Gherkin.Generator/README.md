# Gherkin Test Generator

This project contains the core logic for generating NUnit test code from Gherkin feature files.

## Purpose

This library provides the foundation for automatic test generation from Gherkin `.feature` files. It converts Gherkin features into a Code-Ready Intermediate Form (CRIF) and then renders them into C# test code using Mustache templates.

## Project Structure

```
Gherkin.Generator/
├── CrifModels.cs                   # Data models for intermediate representation
├── GherkinToCrifConverter.cs       # Converts Gherkin → CRIF
├── FunctionalTestGenerator.cs      # Renders CRIF → C# code via Mustache
└── FunctionalTest.mustache         # Template for generated test code
```

## Key Components

### CrifModels.cs
Defines the Code-Ready Intermediate Form (CRIF) data structures that represent:
- Feature metadata (namespace, usings, base class)
- Rules and scenarios
- Steps with owners, methods, and arguments
- Data tables
- Unimplemented steps

### GherkinToCrifConverter
Converts parsed Gherkin documents into CRIF objects:
- Parses feature tags (`@namespace:`, `@baseclass:`, `@using:`, `@explicit`)
- Matches Gherkin steps to step definition methods
- Handles placeholders (`{parameter}`) in step text
- Extracts arguments from matched steps
- Tracks unimplemented steps for stub generation
- Normalizes And/But keywords while preserving display keywords

### StepMetadataCollection
Stores and matches step definitions:
- Finds exact matches (no parameters)
- Finds pattern matches with placeholders
- Supports quoted multi-word arguments
- Case-insensitive matching

### FunctionalTestGenerator
Renders CRIF objects into C# test code:
- Uses Stubble.Core for Mustache template rendering
- Generates NUnit test classes
- Supports multiple output formats (string, stream, file)

## Usage

```csharp
// 1. Create step metadata collection
var stepMetadata = new StepMetadataCollection();
stepMetadata.Add(new StepMetadata
{
    NormalizedKeyword = "Given",
    Text = "I am logged in",
    Method = "IAmLoggedIn",
    Class = "AuthSteps",
    Namespace = "YoFi.V3.Tests.Functional.Steps",
    Parameters = []
});

// 2. Parse Gherkin feature file
var parser = new Gherkin.Parser();
var gherkinDocument = parser.Parse(featureFileContent);

// 3. Convert to CRIF
var converter = new GherkinToCrifConverter(stepMetadata);
var crif = converter.Convert(gherkinDocument);

// 4. Generate C# test code
var generator = new FunctionalTestGenerator();
var testCode = generator.GenerateStringFromFile("FunctionalTest.mustache", crif);
```

## Target Framework

This project targets **netstandard2.0** to support future use as a Roslyn source generator (Phase 4 of the implementation plan).

## Future: Source Generator Integration

This library is designed to be wrapped by a Roslyn Incremental Source Generator (`IIncrementalGenerator`) that will:
- Automatically discover step definitions using Roslyn syntax analysis
- Process `.feature` files and `.mustache` template as `AdditionalFiles` at build time
- Generate test code automatically during compilation
- Generate stub methods with `NotImplementedException` for unimplemented steps (always compiles)
- Optionally provide build warnings for missing step implementations

**Required AdditionalFiles in consuming project:**
- `*.feature` - Gherkin feature files
- `FunctionalTest.mustache` - Template file for code generation

See [`docs/wip/ideas/GHERKIN-SOURCE-GENERATOR-PLAN.md`](../../docs/wip/ideas/GHERKIN-SOURCE-GENERATOR-PLAN.md) for complete implementation plan.

## Related Projects

- **Gherkin.Generator.Tests** - Test suite for this library (80+ tests)
- **Functional** - Uses generated tests (will reference this as analyzer in Phase 4)

## Dependencies

- `Gherkin` (v30.0.2) - Parses Gherkin feature files
- `Stubble.Core` (v1.10.8) - Mustache template rendering
- Future: `Microsoft.CodeAnalysis.CSharp` - For Roslyn-based step discovery

## Testing

Tests are in [`tests/Gherkin.Generator.Tests/`](../Gherkin.Generator.Tests/):

```bash
dotnet test tests/Gherkin.Generator.Tests
```

All tests should pass (80+ tests covering step matching, Gherkin parsing, CRIF conversion, and template rendering).
