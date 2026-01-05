# Alternative: Standalone CLI Tool for Test Generation

## Overview

Instead of using a source generator (which has deployment challenges), we could create a standalone CLI tool that generates test code. This would be similar to how NSwag CLI works for API client generation.

## Architecture

### Standalone Tool Structure

```
tools/
└── Gherkin.Cli/
    ├── Program.cs                      # CLI entry point
    ├── Commands/
    │   └── GenerateCommand.cs          # Generate tests command
    ├── Analysis/
    │   └── StepMethodAnalyzer.cs       # Roslyn-based step discovery (reused)
    ├── Generation/
    │   ├── GherkinToCrifConverter.cs   # Gherkin → CRIF conversion (reused)
    │   └── FunctionalTestGenerator.cs  # Template rendering (reused)
    ├── Models/
    │   └── CrifModels.cs               # Data models (reused)
    └── YoFi.V3.Tools.Gherkin.Cli.csproj
```

### How It Works

1. **Tool is installed as local dotnet tool:**
   ```bash
   # In solution root
   dotnet tool install --local YoFi.V3.Tools.Gherkin.Cli
   ```

2. **Run the tool to generate tests:**
   ```bash
   cd tests/Functional
   gherkin-generate --project . --features Features/*.feature --template Features/FunctionalTest.mustache
   ```

3. **Generated files are committed to source control:**
   - Just like NSwag-generated API clients
   - Generated files have `.g.cs` extension
   - Developer runs tool manually when feature files change

### Key Components

#### CLI Entry Point
```csharp
// Program.cs
using System.CommandLine;

var rootCommand = new RootCommand("Gherkin test generator");
var generateCommand = new GenerateCommand();
rootCommand.AddCommand(generateCommand);

return await rootCommand.InvokeAsync(args);
```

#### Generate Command
```csharp
public class GenerateCommand : Command
{
    public GenerateCommand() : base("generate", "Generate test code from Gherkin feature files")
    {
        var projectOption = new Option<string>(
            "--project",
            "Path to the test project (.csproj)");

        var featuresOption = new Option<string[]>(
            "--features",
            "Glob patterns for feature files");

        var templateOption = new Option<string>(
            "--template",
            "Path to Mustache template");

        AddOption(projectOption);
        AddOption(featuresOption);
        AddOption(templateOption);

        this.SetHandler(async (project, features, template) =>
        {
            await GenerateTestsAsync(project, features, template);
        }, projectOption, featuresOption, templateOption);
    }

    private async Task GenerateTestsAsync(string projectPath, string[] featurePatterns, string templatePath)
    {
        // 1. Analyze project to find step definitions using Roslyn
        var analyzer = new StepMethodAnalyzer();
        var compilation = await LoadCompilationAsync(projectPath);
        var steps = analyzer.DiscoverStepMethods(compilation);

        // 2. Parse feature files
        foreach (var pattern in featurePatterns)
        {
            var featureFiles = Directory.GetFiles(Path.GetDirectoryName(projectPath), pattern);

            foreach (var featureFile in featureFiles)
            {
                // 3. Convert Gherkin → CRIF
                var converter = new GherkinToCrifConverter(steps);
                var crif = await converter.ConvertAsync(featureFile);

                // 4. Generate C# code from template
                var generator = new FunctionalTestGenerator(templatePath);
                var code = generator.Generate(crif);

                // 5. Write to file
                var outputPath = Path.ChangeExtension(featureFile, ".feature.g.cs");
                await File.WriteAllTextAsync(outputPath, code);

                Console.WriteLine($"Generated: {outputPath}");
            }
        }
    }

    private async Task<Compilation> LoadCompilationAsync(string projectPath)
    {
        // Use Roslyn's MSBuildWorkspace to load the project
        var workspace = MSBuildWorkspace.Create();
        var project = await workspace.OpenProjectAsync(projectPath);
        return await project.GetCompilationAsync();
    }
}
```

## Advantages Over Source Generator

### 1. No Deployment Issues
- Regular .NET tool installation
- No analyzer assembly loading problems
- Dependencies work normally

### 2. Explicit Control
- Developer knows when tests are regenerated
- Generated files are visible in source control
- Easy to review changes (git diff)

### 3. Simpler Workflow
```bash
# Make changes to feature files or step definitions
edit Features/BankImport.feature

# Regenerate tests
dotnet gherkin-generate

# Review changes
git diff Features/BankImport.feature.g.cs

# Commit both feature file and generated code
git add Features/BankImport.feature Features/BankImport.feature.g.cs
git commit -m "feat(tests): add bank import scenario"
```

### 4. Better Debugging
- Generated code is always on disk
- Can set breakpoints in generated code
- No mystery about what was generated

### 5. Works Everywhere
- Local development
- CI/CD (just call the tool)
- Any IDE (not Roslyn-dependent)

### 6. Versioning
- Tool versioned independently
- Can pin to specific version in CI
- Easy to update: `dotnet tool update`

## Disadvantages vs Source Generator

### 1. Manual Regeneration
**Source Generator:** Automatic on every build
**CLI Tool:** Must remember to run tool

**Mitigation:**
- Add pre-build check that verifies generated files are up-to-date
- Add git pre-commit hook
- Document in README

### 2. Generated Files in Source Control
**Source Generator:** Generated files in `obj/`, not committed
**CLI Tool:** Generated files committed

**Why This Is Actually Good:**
- Explicit about what code is executed
- Git history shows generated code changes
- No "invisible" code generation
- Easier to debug and understand

### 3. Requires Tool Installation
**Source Generator:** Works automatically if referenced
**CLI Tool:** Developers must install tool

**Mitigation:**
- Add to project README
- Use local tool manifest (`dotnet tool restore`)
- Add to developer onboarding checklist

## Implementation Plan

### Phase 1: Extract Core Logic (Current)
- ✅ `StepMethodAnalyzer.cs` - Already isolated
- ✅ `GherkinToCrifConverter.cs` - Already isolated
- ✅ `FunctionalTestGenerator.cs` - Already isolated
- ✅ `CrifModels.cs` - Already isolated

### Phase 2: Create CLI Project
```bash
cd tools
dotnet new tool -n Gherkin.Cli
cd Gherkin.Cli
dotnet add package System.CommandLine --prerelease
dotnet add package Microsoft.CodeAnalysis.CSharp
dotnet add package Microsoft.Build.Locator
dotnet add package Microsoft.CodeAnalysis.Workspaces.MSBuild
dotnet add package Gherkin
dotnet add package Stubble.Core
```

### Phase 3: Move Logic to CLI
- Copy analyzer, converter, generator to CLI project
- Remove source generator infrastructure
- Add CLI command structure

### Phase 4: Add Tool Manifest
```bash
# In solution root
dotnet new tool-manifest
dotnet tool install --local YoFi.V3.Tests.Gherkin.Cli
```

This creates `.config/dotnet-tools.json`:
```json
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "yofi.v3.tests.gherkin.cli": {
      "version": "1.0.0",
      "commands": ["gherkin-generate"]
    }
  }
}
```

### Phase 5: Update Functional Tests Project
Remove source generator references:
```xml
<!-- OLD: Source generator reference -->
<!-- <ProjectReference Include="..\Gherkin.Generator\..." OutputItemType="Analyzer" /> -->

<!-- NEW: Just include generated files -->
<ItemGroup>
  <Compile Include="Features\*.feature.g.cs" />
</ItemGroup>
```

### Phase 6: Add PowerShell Script
```powershell
# scripts/Generate-FunctionalTests.ps1
<#
.SYNOPSIS
Generates C# test implementations from Gherkin feature files.

.DESCRIPTION
Runs the Gherkin test generator CLI tool to create test code from feature files.
Generated files are written to the same directory as the feature files with
a .feature.g.cs extension.

.EXAMPLE
.\scripts\Generate-FunctionalTests.ps1
Generates tests for all feature files in tests/Functional/Features/
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

try {
    $repoRoot = Split-Path $PSScriptRoot -Parent
    Push-Location "$repoRoot/tests/Functional"

    Write-Host "Generating functional tests from Gherkin feature files..." -ForegroundColor Cyan

    dotnet gherkin-generate `
        --project YoFi.V3.Tests.Functional.csproj `
        --features "Features/*.feature" `
        --template "Features/FunctionalTest.mustache"

    if ($LASTEXITCODE -ne 0) {
        throw "Test generation failed with exit code $LASTEXITCODE"
    }

    Write-Host "OK Tests generated successfully" -ForegroundColor Green
}
catch {
    Write-Error "Failed to generate tests: $_"
    Write-Error $_.ScriptStackTrace
    exit 1
}
finally {
    Pop-Location
}
```

### Phase 7: Update CI Pipeline
```yaml
# .github/workflows/test.yml
steps:
  - name: Restore .NET tools
    run: dotnet tool restore

  - name: Generate functional tests
    run: dotnet gherkin-generate --project tests/Functional/YoFi.V3.Tests.Functional.csproj --features "Features/*.feature" --template "Features/FunctionalTest.mustache"
    working-directory: tests/Functional

  - name: Verify generated files are up-to-date
    run: |
      git diff --exit-code tests/Functional/Features/*.feature.g.cs || \
        (echo "Generated test files are out of date. Run 'dotnet gherkin-generate' and commit changes." && exit 1)

  - name: Build and test
    run: dotnet test
```

## Comparison: Source Generator vs CLI Tool

| Aspect | Source Generator | CLI Tool |
|--------|-----------------|----------|
| **Deployment** | Complex (packaging required) | Simple (dotnet tool) |
| **Dependencies** | Assembly loading issues | Works normally |
| **Workflow** | Automatic on build | Manual regeneration |
| **Generated Code** | In obj/, not visible | In source control, explicit |
| **Debugging** | Requires special config | Always available |
| **CI/CD** | Complex integration | Simple tool call |
| **Developer Setup** | NuGet feed configuration | `dotnet tool restore` |
| **Updates** | Repack after every change | Standard tool update |
| **IDE Support** | Requires Roslyn support | Works everywhere |

## Recommendation

**Convert the source generator to a standalone CLI tool** because:

1. **Eliminates all deployment issues** - No more dependency loading problems
2. **Simpler mental model** - Feature files → Run tool → Generated C# files
3. **Better fits the workflow** - Tests don't change that often, manual regeneration is fine
4. **Matches existing patterns** - Same model as NSwag API client generation
5. **More maintainable** - Standard .NET tool packaging and distribution
6. **Easier to debug** - Generated code always visible

The source generator approach is technically elegant but practically problematic. A CLI tool trades automatic regeneration for reliability and simplicity - a good tradeoff for test generation.

## Next Steps

1. Create new CLI tool project
2. Move/refactor existing code
3. Create PowerShell wrapper script
4. Update CI pipeline
5. Update documentation
6. Archive the source generator project for reference

## See Also

- [`DEPLOYMENT-CHALLENGES.md`](DEPLOYMENT-CHALLENGES.md) - Why source generators are difficult
- [`README.md`](README.md) - Current source generator documentation
- [System.CommandLine](https://github.com/dotnet/command-line-api) - CLI framework
- [Microsoft.Build.Locator](https://github.com/microsoft/MSBuildLocator) - For loading MSBuild in CLI tools
