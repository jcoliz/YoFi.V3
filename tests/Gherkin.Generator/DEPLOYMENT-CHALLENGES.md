# Why Source Generation Is Not Currently Used for Test Generation

## Question

Why aren't we using the custom Gherkin source generator to create C# test implementations from feature files?

## Short Answer

**Source generators with external dependencies cannot be reliably consumed as project references** due to Roslyn's assembly loading architecture. Using them requires packaging infrastructure (local NuGet feeds) that adds complexity to both local development and CI/CD workflows.

## The Problem

### Source Generator Dependencies

The Gherkin.Generator has two critical runtime dependencies:
- `Gherkin` (v30.0.2) - For parsing .feature files
- `Stubble.Core` (v1.10.8) - For Mustache template rendering

These are NOT compile-time dependencies - the generator needs them at runtime when Roslyn executes the source generator during compilation.

### Roslyn's Assembly Loading Model

When the C# compiler (Roslyn) loads a source generator:

1. The generator DLL is loaded into an isolated `AssemblyLoadContext` or `AppDomain`
2. Roslyn looks for the generator's dependencies in specific locations:
   - The analyzer/generator's original directory (for package references)
   - NuGet package cache directories
3. Roslyn **does NOT** automatically probe:
   - The generator's build output directory (for project references)
   - The consuming project's dependency directories
   - Transitive dependency paths

This means when you use a project reference to a source generator, even though Gherkin.dll and Stubble.Core.dll exist in `tests/Gherkin.Generator/bin/Debug/netstandard2.0/`, the compiler cannot find them.

### Error Manifestation

```
CSC : error GHERKIN002: Error generating test for BankImport:
Could not load file or assembly 'Gherkin, Version=30.0.2.0, Culture=neutral,
PublicKeyToken=86496cfa5b4a5851'. The system cannot find the file specified.
```

## Attempted Solutions

### 1. Add Dependencies to Consuming Project
**Tried:** Adding `<PackageReference Include="Gherkin" />` and `<PackageReference Include="Stubble.Core" />` to the Functional tests project.

**Result:** Failed. Dependencies in the consuming project's output directory don't help because Roslyn doesn't look there when loading the generator.

### 2. CopyLocalLockFileAssemblies
**Tried:** Setting `<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>` in the generator project to force dependency copying.

**Result:** Dependencies are copied to the generator's output directory, but Roslyn still can't find them because it doesn't probe project reference output paths.

### 3. ReferenceOutputAssembly Configuration
**Tried:** Various combinations of `ReferenceOutputAssembly="true"` and `ReferenceOutputAssembly="false"`.

**Result:**
- `false` - Generator works as analyzer but dependencies aren't copied
- `true` - Dependencies might be copied but causes other assembly reference issues

### 4. GetDependencyTargetPaths MSBuild Target
**Tried:** Custom MSBuild target to include resolved dependencies:
```xml
<Target Name="GetDependencyTargetPaths" AfterTargets="ResolvePackageAssets">
  <ItemGroup>
    <TargetPathWithTargetPlatformMoniker Include="@(ResolvedCompileFileDefinitions)" />
  </ItemGroup>
</Target>
```

**Result:** Caused 199+ assembly version conflict errors by pulling in too many transitive dependencies (Microsoft.CSharp, System.Buffers, System.Memory, etc.).

## Working Solution: Package References

Source generators **do** work reliably when consumed as NuGet packages because:

1. NuGet packages can declare dependencies in their `.nuspec` file
2. NuGet restores all dependencies to the package cache
3. Roslyn knows to look in the package cache for analyzer dependencies
4. Dependencies are properly isolated per package

### What This Requires

**Local Development:**
1. Pack the generator after changes:
   ```powershell
   dotnet pack tests/Gherkin.Generator -o ./local-packages
   ```

2. Create `nuget.config` pointing to local feed:
   ```xml
   <configuration>
     <packageSources>
       <add key="local" value="./local-packages" />
     </packageSources>
   </configuration>
   ```

3. Reference as package:
   ```xml
   <PackageReference Include="YoFi.V3.Tests.Gherkin.Generator"
                     Version="1.0.0"
                     OutputItemType="Analyzer" />
   ```

**CI/CD:**
1. Add build step to pack generator before building tests
2. Configure NuGet sources in pipeline
3. Restore and build functional tests

### Workflow Impact

- **Every generator change** requires repacking before testing
- **Version management** required even for local development
- **CI pipeline complexity** increases with multi-step build process
- **Developer onboarding** must explain local NuGet feed setup

## Alternative: ILRepack/ILMerge

Theoretically, you could merge all dependencies into a single generator DLL using ILRepack or ILMerge. This would eliminate external dependencies.

**Challenges:**
- Complex build process
- Potential assembly conflicts
- Licensing concerns (merging third-party code)
- Debugging becomes harder
- Not a standard .NET practice

## Microsoft's Approach

Microsoft's own source generators (Regex, JSON serialization, etc.) are **always distributed as NuGet packages**, never as project references, for exactly these reasons.

Examples:
- `System.Text.Json.SourceGeneration` - Package only
- `System.Text.RegularExpressions` generator - Package only
- All Roslyn analyzers - Package only

## Current Status

### What Works
- The generator itself is functional and well-tested (80+ unit tests)
- Parsing Gherkin feature files works correctly
- Matching steps to methods works reliably
- Code generation via Mustache templates works as designed

### What Doesn't Work
- **Consuming the generator as a project reference**
- Local development without packaging infrastructure
- Simple "just reference and use" experience

## Recommendation

### Short Term: Don't Use Source Generation Yet

Continue writing functional tests manually or using established tooling until we have proper infrastructure for:

1. Automated packaging in CI
2. Local NuGet feed management
3. Developer workflow documentation
4. Version management strategy

### Medium Term: Set Up Packaging Infrastructure

When the project reaches a maturity level where:
- Functional tests are a larger percentage of the codebase
- Test generation provides clear productivity wins
- Team size justifies infrastructure investment

Then implement:
1. CI pipeline to pack generator automatically
2. Local NuGet feed setup in developer onboarding
3. Versioning strategy (SemVer with automatic bumping)
4. Documentation for the full workflow

### Long Term: Publish to NuGet.org

If the generator proves valuable and reusable:
- Clean up and document for public use
- Publish to NuGet.org as a proper package
- Share with the .NET community

## Lessons Learned

1. **Source generators are not normal libraries** - They have special deployment requirements
2. **Project references don't work well** - Despite being the standard .NET development pattern
3. **Packaging is required** - Even for internal tools within a solution
4. **Microsoft's patterns exist for a reason** - Their "package-only" distribution model solves real problems

## References

- [Roslyn Source Generators Cookbook](https://github.com/dotnet/roslyn/blob/main/docs/features/source-generators.cookbook.md)
- [Deploying Source Generators](https://github.com/dotnet/roslyn/blob/main/docs/features/source-generators.cookbook.md#distributing-source-generators-as-nuget-packages)
- [MSBuild Analyzer References](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/source-generators-overview#how-do-i-use-a-source-generator)

## See Also

- [`README.md`](README.md) - Generator documentation and usage
- [`../Gherkin.Generator.Tests/`](../Gherkin.Generator.Tests/) - Generator test suite
