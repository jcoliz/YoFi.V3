# Gherkin Test Generator (Source Generator)

This project contains a Roslyn Incremental Source Generator that automatically generates NUnit test code from Gherkin feature files at build time.

## Purpose

This source generator automates the creation of NUnit test code from Gherkin `.feature` files. At build time, it:
- Discovers step definitions from your C# source files using Roslyn analysis
- Processes `.feature` files and `.mustache` templates from AdditionalFiles
- Generates test code automatically
- Always produces compilable code (generates stubs for unimplemented steps)

## Project Structure

```
Gherkin.Generator/
├── GherkinSourceGenerator.cs       # Roslyn incremental source generator
├── StepMethodAnalyzer.cs           # Discovers step definitions via Roslyn
├── CrifModels.cs                   # Data models for intermediate representation
├── GherkinToCrifConverter.cs       # Converts Gherkin → CRIF
├── FunctionalTestGenerator.cs      # Renders CRIF → C# code via Mustache
└── FunctionalTest.mustache         # Example template (users provide their own)
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

## Usage (Consuming Projects)

### 1. Add Package Reference

```xml
<ItemGroup>
  <PackageReference Include="YoFi.V3.Tests.Gherkin.Generator" Version="1.0.0" OutputItemType="Analyzer" />
</ItemGroup>
```

### 2. Add Feature Files as AdditionalFiles

```xml
<ItemGroup>
  <AdditionalFiles Include="Features\**\*.feature" />
</ItemGroup>
```

### 3. Add Mustache Template as AdditionalFiles

```xml
<ItemGroup>
  <AdditionalFiles Include="Templates\YourTemplate.mustache" />
</ItemGroup>
```

**Note:** You must provide exactly one `.mustache` file in AdditionalFiles. The generator will use the first mustache template it finds.

### 4. Create Step Definitions

```csharp
using Reqnroll;

namespace YourProject.Steps
{
    [Binding]
    public class AuthSteps
    {
        [Given("I am logged in")]
        public void IAmLoggedIn()
        {
            // Step implementation
        }

        [When("I click {string}")]
        public void IClick(string buttonName)
        {
            // Step implementation
        }
    }
}
```

### 5. Build Your Project

The generator runs automatically during build and creates test files like `YourFeature.feature.g.cs` in the compilation.

## How It Works

1. **Step Discovery**: [`StepMethodAnalyzer`](StepMethodAnalyzer.cs) scans your compilation using Roslyn to find methods with `[Given]`, `[When]`, `[Then]` attributes
2. **Feature Processing**: Parses all `.feature` files from AdditionalFiles
3. **Step Matching**: Matches Gherkin steps to discovered step definitions (handles placeholders like `{parameter}`)
4. **Code Generation**: Renders matched steps into C# test code using your Mustache template
5. **Stub Generation**: For unimplemented steps, generates stubs with `NotImplementedException` (ensures code always compiles)

## Target Framework

This project targets **netstandard2.0** for compatibility with Roslyn source generators.

## Diagnostics

The generator reports diagnostics during build:

- **GHERKIN001** (Error): No `.mustache` template found in AdditionalFiles
- **GHERKIN002** (Error): Error generating test for a feature file
- **GHERKIN003** (Error): Error parsing a `.feature` file
- **GHERKIN004** (Warning): Feature has unimplemented steps (stubs will be generated)

## Debugging Generated Code

By default, source-generated files are only stored in memory during compilation. To inspect the generated code for debugging:

### Option 1: Save Generated Files to Disk

Add this property to your test project `.csproj`:

```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  <CompilerGeneratedFilesOutputPath>$(BaseIntermediateOutputPath)\GeneratedFiles</CompilerGeneratedFilesOutputPath>
</PropertyGroup>
```

Generated files will be saved to `obj\GeneratedFiles\` (e.g., `obj\GeneratedFiles\YoFi.V3.Tests.Generator\YoFi.V3.Tests.Generator.GherkinSourceGenerator\YourFeature.feature.g.cs`).

### Option 2: Include Generated Files in Project

To see generated files in Solution Explorer and enable IntelliSense:

```xml
<ItemGroup>
  <Compile Include="$(BaseIntermediateOutputPath)\GeneratedFiles\**\*.g.cs" Visible="true" />
</ItemGroup>
```

**Tip:** Add `obj/GeneratedFiles/` to your `.gitignore` to avoid committing generated code.

### Viewing Generated Code Without Saving

1. Build your project: `dotnet build`
2. In Visual Studio: Right-click on the project → View → View Generated Files
3. Navigate to the Gherkin.Generator node to see all generated `.feature.g.cs` files

## Related Projects

- **[Gherkin.Generator.Tests](../Gherkin.Generator.Tests/)** - Test suite for this library (80+ tests)

## Dependencies

- `Gherkin` (v30.0.2) - Parses Gherkin feature files
- `Stubble.Core` (v1.10.8) - Mustache template rendering
- `Microsoft.CodeAnalysis.CSharp` (v4.8.0) - Roslyn-based step discovery
- `Microsoft.CodeAnalysis.Analyzers` (v3.3.4) - Analyzer infrastructure

## Testing

Tests are in [`tests/Gherkin.Generator.Tests/`](../Gherkin.Generator.Tests/):

```bash
dotnet test tests/Gherkin.Generator.Tests
```

All tests should pass (80+ tests covering step matching, Gherkin parsing, CRIF conversion, and template rendering).
