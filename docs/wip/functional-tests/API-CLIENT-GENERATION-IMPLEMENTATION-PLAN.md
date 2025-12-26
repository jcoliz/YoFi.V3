---
status: Approved
related_doc: API-CLIENT-GENERATION-IMPROVEMENT.md
---

# API Client Generation Implementation Plan

## Overview

Move C# API client generation from [`WireApiHost`](../../src/WireApiHost/) project to [`Functional tests`](../../tests/Functional/) project, generating into `obj/` directory instead of checking generated code into source control.

**Scope:** C# client generation only. TypeScript generation remains in WireApiHost.

## Current State Analysis

### WireApiHost Project
**File:** [`src/WireApiHost/YoFi.V3.WireApiHost.csproj`](../../src/WireApiHost/YoFi.V3.WireApiHost.csproj)
- Has NSwag.MSBuild package reference
- PostBuildEvent runs NSwag generation
- Generates BOTH TypeScript and C# clients

**File:** [`src/WireApiHost/nswag.json`](../../src/WireApiHost/nswag.json)
- Line 22-83: `openApiToTypeScriptClient` - Generates to `../FrontEnd.Nuxt/app/utils/apiclient.ts`
- Line 84-176: `openApiToCSharpClient` - Generates to `../../tests/Functional/Api/ApiClient.cs`
- Uses `aspNetCoreToOpenApi` with `noBuild: true` (requires pre-built assembly)

### Functional Tests Project
**File:** [`tests/Functional/YoFi.V3.Tests.Functional.csproj`](../../tests/Functional/YoFi.V3.Tests.Functional.csproj)
- Does NOT have NSwag.MSBuild package reference
- Line 21-24: Commented-out WireApiHost project reference (intentionally removed)

**File:** [`tests/Functional/nswag.json`](../../tests/Functional/nswag.json)
- Line 5-19: `aspNetCoreToOpenApi` - References WireApiHost project with `noBuild: false`
- Line 22-114: `openApiToCSharpClient` - Has output variable `$(OutputFile)`
- Line 72: Namespace already set to `YoFi.V3.Tests.Functional.Generated`
- **Currently unused** - This config exists but isn't being executed

**File:** [`tests/Functional/Api/ApiClient.cs`](../../tests/Functional/Api/ApiClient.cs)
- Generated file checked into source control
- Currently hand-copied from WireApiHost generation output

### Key Insight
The Functional tests project already has a complete `nswag.json` configuration! It was likely created as an attempt to solve this problem but wasn't successfully integrated into the build process.

## Implementation Strategy

### Phase 1: Remove C# Generation from WireApiHost

#### Step 1.1: Update WireApiHost nswag.json
**File:** [`src/WireApiHost/nswag.json`](../../src/WireApiHost/nswag.json)

**Action 1:** Change TypeScript output from hardcoded path to variable (line 81)

**From:**
```json
"output": "../FrontEnd.Nuxt/app/utils/apiclient.ts"
```

**To:**
```json
"output": "$(OutputFile)"
```

**Action 2:** Remove the entire `openApiToCSharpClient` section (lines 84-176)

**Result:** Only TypeScript generation remains, using variable for output path

```json
{
  "runtime": "Net100",
  "defaultVariables": null,
  "documentGenerator": {
    "aspNetCoreToOpenApi": {
      // ... existing config ...
    }
  },
  "codeGenerators": {
    "openApiToTypeScriptClient": {
      // ... existing config ...
      "output": "$(OutputFile)"  // Now uses variable from MSBuild
    }
    // openApiToCSharpClient section REMOVED
  }
}
```

#### Step 1.2: WireApiHost MSBuild Target Already Correct
**File:** [`src/WireApiHost/YoFi.V3.WireApiHost.csproj`](../../src/WireApiHost/YoFi.V3.WireApiHost.csproj)

**No changes needed!** The existing configuration already passes `OutputFile` variable to nswag.json:

```xml
<PropertyGroup>
  <RunPostBuildEvent>OnBuildSuccess</RunPostBuildEvent>
  <ApiClientConfigFile>nswag.json</ApiClientConfigFile>
  <ApiClientOutputFile>../FrontEnd.Nuxt/app/utils/apiclient.ts</ApiClientOutputFile>
</PropertyGroup>

<Target Name="NSwag" AfterTargets="PostBuildEvent">
  <Exec WorkingDirectory="$(ProjectDir)" Command="$(NSwagExe_Net100) run $(ApiClientConfigFile) /variables:Configuration=$(Configuration),MSBuildOutputPath=$(OutputPath),MSBuildProjectFile=$(MSBuildProjectFile),OutputFile=$(ApiClientOutputFile)"/>
</Target>
```

**Why this is better:**
- Output path defined once in `.csproj` (clear, easy to find)
- `nswag.json` remains configuration-focused, not path-focused
- Consistent with how Functional tests will work
- Easy to see where TypeScript client is generated

### Phase 2: Enable C# Generation in Functional Tests

#### Step 2.1: Move nswag.json to Api Directory
**Current location:** [`tests/Functional/nswag.json`](../../tests/Functional/nswag.json)
**New location:** `tests/Functional/Api/nswag.json`

**Action:** Move the file to co-locate API client configuration with its usage

**Command:**
```bash
git mv tests/Functional/nswag.json tests/Functional/Api/nswag.json
```

**Why:** Keeps API client generation configuration in the Api folder alongside README and (previously) the generated client. Makes it easier to find and understand.

**File path update needed in Step 2.3:** MSBuild target will reference `Api/nswag.json` instead of `nswag.json`

#### Step 2.2: Verify nswag.json Configuration
**File:** `tests/Functional/Api/nswag.json` (after move)

**No changes needed!** The configuration is already correct:
- Line 6: `"project": "../../src/WireApiHost/YoFi.V3.WireApiHost.csproj"` ✅ (path still correct after move)
- Line 12: `"noBuild": false` ✅ (Will build WireApiHost if needed)
- Line 72: `"namespace": "YoFi.V3.Tests.Functional.Generated"` ✅
- Line 112: `"output": "$(OutputFile)"` ✅ (Uses MSBuild variable)

#### Step 2.3: Add NSwag Package to Functional Tests
**File:** [`tests/Functional/YoFi.V3.Tests.Functional.csproj`](../../tests/Functional/YoFi.V3.Tests.Functional.csproj)

**Add after line 15 (after other PackageReferences):**
```xml
<PackageReference Include="NSwag.MSBuild" Version="14.6.3">
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  <PrivateAssets>all</PrivateAssets>
</PackageReference>
```

#### Step 2.4: Add MSBuild Target to Functional Tests
**File:** [`tests/Functional/YoFi.V3.Tests.Functional.csproj`](../../tests/Functional/YoFi.V3.Tests.Functional.csproj)

**Add before closing `</Project>` tag:**
```xml
<PropertyGroup>
  <ApiClientGeneratedDir>$(IntermediateOutputPath)Generated</ApiClientGeneratedDir>
  <ApiClientOutputFile>$(ApiClientGeneratedDir)\ApiClient.cs</ApiClientOutputFile>
  <ApiClientConfigFile>Api\nswag.json</ApiClientConfigFile>
</PropertyGroup>

<Target Name="GenerateApiClient" BeforeTargets="CoreCompile">
  <!-- Ensure output directory exists -->
  <MakeDir Directories="$(ApiClientGeneratedDir)" />

  <!-- Generate C# client from WireApiHost OpenAPI spec -->
  <Exec WorkingDirectory="$(ProjectDir)"
        Command="$(NSwagExe_Net100) run $(ApiClientConfigFile) /variables:OutputFile=$(ApiClientOutputFile)" />

  <!-- Include generated file in compilation -->
  <ItemGroup>
    <Compile Include="$(ApiClientOutputFile)" />
  </ItemGroup>
</Target>
```

**Note:** Added `<ApiClientConfigFile>` property to make the nswag.json path clear and easy to change.

**How it works:**
1. `BeforeTargets="CoreCompile"` - Runs before C# compilation
2. `$(IntermediateOutputPath)` - Resolves to `obj/Debug/net10.0/` (or Release)
3. `MakeDir` - Creates `obj/Debug/net10.0/Generated/` directory
4. `Exec` - Runs NSwag with output to `obj/Debug/net10.0/Generated/ApiClient.cs`
5. `<Compile Include>` - Adds generated file to compilation (dynamically)
6. `noBuild: false` in nswag.json - NSwag will build WireApiHost if necessary

### Phase 3: Clean Up Generated File from Source Control

#### Step 3.1: Delete Checked-in Generated File
**File:** [`tests/Functional/Api/ApiClient.cs`](../../tests/Functional/Api/ApiClient.cs)

**Action:** Delete this file (it will be regenerated in `obj/` directory on next build)

**Command:**
```bash
git rm tests/Functional/Api/ApiClient.cs
```

**Note:** No `.gitignore` changes needed. The new location (`obj/Debug/net10.0/Generated/ApiClient.cs`) is already ignored by standard `.gitignore` patterns that exclude `obj/` directories.

#### Step 3.2: Update README
**File:** [`tests/Functional/Api/README.md`](../../tests/Functional/Api/README.md)

**Replace current content with:**
```markdown
# Backend API Client

This folder contains the configuration for C# API client generation. The generated client is **automatically created during the Functional tests build** into the `obj/` directory.

## Generation Process

The C# client is generated by the Functional tests project itself during build:

1. **Build triggers generation** - MSBuild target `GenerateApiClient` runs before C# compilation
2. **NSwag introspects WireApiHost** - Analyzes the backend API to generate OpenAPI spec
3. **Client generated to obj/** - Output: `obj/Debug/net10.0/Generated/ApiClient.cs`
4. **Included in compilation** - Generated file is automatically compiled

## Configuration Files

- [`nswag.json`](nswag.json) - NSwag configuration for C# client generation (in this directory)
- [`tests/Functional/YoFi.V3.Tests.Functional.csproj`](../YoFi.V3.Tests.Functional.csproj) - MSBuild target for generation

## Generated Types

The client is generated in the `YoFi.V3.Tests.Functional.Generated` namespace with:

- `TransactionsClient` - Transactions API endpoints
- `TestControlClient` - Test control endpoints (seeding, cleanup)
- `VersionClient` - Version information endpoint
- `WeatherClient` - Weather forecast endpoint
- DTOs: `TransactionEditDto`, `TransactionResultDto`, etc.

## Manual Regeneration

The client is automatically regenerated on every build. To manually regenerate:

```bash
cd tests/Functional
dotnet build
```

Or run NSwag directly:

```bash
cd tests/Functional
nswag run Api/nswag.json /variables:OutputFile=obj/Generated/ApiClient.cs
```

## Why This Approach?

**Advantages:**
- ✅ Generated code NOT in source control (standard practice)
- ✅ No cross-project file dependencies (each project owns its generation)
- ✅ Automatic regeneration on every build (always in sync)
- ✅ Standard MSBuild integration (works in IDE and CI/CD)

**Previous Approach:**
The C# client was previously generated by WireApiHost and checked into source control at `tests/Functional/Api/ApiClient.cs`. This worked but violated best practices (generated code in source control, cross-project dependencies).
```

### Phase 4: Testing & Validation

#### Test 4.1: Clean Build
```bash
# Clean all build outputs
dotnet clean

# Build Functional tests (should trigger generation)
cd tests/Functional
dotnet build
```

**Expected result:**
- Build succeeds
- `obj/Debug/net10.0/Generated/ApiClient.cs` is created
- No build errors about missing types

#### Test 4.2: Verify Generated File Location
```bash
# Check generated file exists
ls tests/Functional/obj/Debug/net10.0/Generated/ApiClient.cs

# Check it's NOT in Api/ directory anymore
ls tests/Functional/Api/  # Should not contain ApiClient.cs
```

#### Test 4.3: Run Functional Tests
```bash
# Run functional tests (requires container)
pwsh -File ./scripts/Run-FunctionalTestsVsContainer.ps1
```

**Expected result:** All tests pass

#### Test 4.4: CI/CD Verification
**Check:** Ensure CI/CD builds work with new approach
- GitHub Actions should restore NuGet packages (including NSwag.MSBuild)
- Build process should automatically generate client
- Tests should pass

### Phase 5: Documentation Updates

#### Doc 5.1: Update Implementation Document
**File:** [`docs/wip/functional-tests/API-CLIENT-GENERATION-IMPROVEMENT.md`](../../docs/wip/functional-tests/API-CLIENT-GENERATION-IMPROVEMENT.md)

**Add section at end:**
```markdown
## Implementation Results (2025-12-26)

### Implemented Solution: Generate in Functional Tests Project

Successfully implemented **Solution 1** from investigation plan.

**Changes Made:**
1. Removed C# client generation from WireApiHost (kept TypeScript only)
2. Added NSwag.MSBuild package to Functional tests project
3. Added MSBuild target to generate C# client before compilation
4. Generated client outputs to `obj/Debug/net10.0/Generated/ApiClient.cs`
5. Removed `tests/Functional/Api/ApiClient.cs` from source control

**Results:**
- ✅ C# client generation working in Functional tests project
- ✅ Generated code NOT in source control
- ✅ Automatic regeneration on every build
- ✅ All functional tests passing
- ✅ CI/CD builds working

**Files Modified:**
- `src/WireApiHost/nswag.json` - Removed C# generation config
- `tests/Functional/YoFi.V3.Tests.Functional.csproj` - Added NSwag package and generation target
- `.gitignore` - Added old ApiClient.cs location (obj/ already ignored)
- `tests/Functional/Api/README.md` - Updated documentation
- Deleted: `tests/Functional/Api/ApiClient.cs` (now auto-generated)

**Lessons Learned:**
1. The `tests/Functional/nswag.json` file already existed and was correctly configured
2. The blocker was simply missing MSBuild integration
3. Setting `noBuild: false` in nswag.json allows NSwag to build WireApiHost if needed
4. Using `$(IntermediateOutputPath)` ensures generation works for all configurations (Debug/Release)

**Technical Debt Resolved:** ✅ Complete
```

#### Doc 5.2: Update Project Rules
**File:** `.roorules` (project root)

**Update the "API Client Generation Pattern" section:**
```markdown
## API Client Generation Pattern

**Two API clients are generated from the backend OpenAPI specification:**

1. **TypeScript client** - For the Nuxt frontend
   - Generated by: `src/WireApiHost` project (post-build)
   - Configuration: `src/WireApiHost/nswag.json`
   - Output: `src/FrontEnd.Nuxt/app/utils/apiclient.ts`
   - Checked into source control (required for frontend builds)

2. **C# client** - For functional tests
   - Generated by: `tests/Functional` project (pre-compile)
   - Configuration: `tests/Functional/Api/nswag.json`
   - Output: `tests/Functional/obj/[Configuration]/net10.0/Generated/ApiClient.cs`
   - NOT checked into source control (auto-generated at build time)

**NEVER edit generated clients manually:**
- Frontend: `src/FrontEnd.Nuxt/app/utils/apiclient.ts`
- Tests: `tests/Functional/obj/.../Generated/ApiClient.cs`

**When to regenerate:**
- TypeScript: Automatically regenerated when building WireApiHost
- C# client: Automatically regenerated when building Functional tests
- Both: When API endpoints or DTOs change in Controllers

**Manual regeneration:**
```bash
# TypeScript client
cd src/WireApiHost
dotnet build

# C# client
cd tests/Functional
dotnet build
```

**Script for regenerating both:**
```bash
pwsh -File ./scripts/Generate-ApiClient.ps1
```
```

## Potential Issues & Solutions

### Issue 1: Build Order Dependencies
**Problem:** Functional tests build might fail if WireApiHost isn't built first.

**Solution:** The `noBuild: false` setting in `tests/Functional/nswag.json` tells NSwag to build WireApiHost if necessary. This ensures correct build order.

**Fallback:** If issues persist, restore the explicit project reference (currently commented out):
```xml
<ItemGroup>
  <ProjectReference Include="..\..\src\WireApiHost\YoFi.V3.WireApiHost.csproj" ReferenceOutputAssembly="false" SkipGetTargetFrameworkProperties="true" />
</ItemGroup>
```

### Issue 2: NSwag Executable Not Found
**Problem:** `$(NSwagExe_Net100)` variable might not be defined.

**Solution:** The NSwag.MSBuild package automatically sets this variable. Ensure package restore completed successfully.

**Verify:**
```bash
dotnet restore tests/Functional
```

### Issue 3: Generated File Not Included in Compilation
**Problem:** Build errors about missing types from generated client.

**Solution:** The `<Compile Include>` in the MSBuild target dynamically adds the file. Check that:
1. Target runs `BeforeTargets="CoreCompile"`
2. File is generated before compilation starts
3. Path in `<Compile Include>` matches `$(ApiClientOutputFile)`

**Debug:**
```bash
dotnet build tests/Functional -v:detailed  # Verbose output shows target execution
```

### Issue 4: WireApiHost Build Failing During Generation
**Problem:** NSwag fails because WireApiHost has build errors.

**Solution:** Fix WireApiHost build first. The generation depends on a successful build to introspect the API.

**Workaround:** Temporarily set `noBuild: true` in `tests/Functional/nswag.json` and ensure WireApiHost was built previously.

### Issue 5: Path Issues on Windows vs Linux
**Problem:** Backslash vs forward slash in paths.

**Solution:** The implementation uses backslash `\` which works on Windows and is auto-converted by MSBuild on Linux. If issues occur, can use `$(PathSeparator)` or `/`.

## Rollback Plan

If implementation fails, revert by:

1. **Restore deleted file:**
   ```bash
   git restore tests/Functional/Api/ApiClient.cs
   ```

2. **Revert WireApiHost nswag.json:**
   ```bash
   git restore src/WireApiHost/nswag.json
   ```

3. **Remove changes from Functional tests:**
   - Remove NSwag.MSBuild package reference
   - Remove GenerateApiClient MSBuild target
   - Remove added properties

4. **Build to verify:**
   ```bash
   dotnet build tests/Functional
   ```

## Success Criteria

- ✅ C# client generated to `obj/` directory (not source directory)
- ✅ Generated file NOT in source control
- ✅ Functional tests build successfully
- ✅ All functional tests pass
- ✅ CI/CD builds work without changes
- ✅ No `tests/Functional/Api/ApiClient.cs` file in Git
- ✅ TypeScript generation still works in WireApiHost
- ✅ Clean build works (generation from scratch)

## Implementation Checklist

Use this checklist during implementation:

- [ ] Change TypeScript output to use variable in [`src/WireApiHost/nswag.json`](../../src/WireApiHost/nswag.json) line 81
- [ ] Remove `openApiToCSharpClient` section from [`src/WireApiHost/nswag.json`](../../src/WireApiHost/nswag.json) lines 84-176
- [ ] Move [`tests/Functional/nswag.json`](../../tests/Functional/nswag.json) to `tests/Functional/Api/nswag.json`
- [ ] Add NSwag.MSBuild package to [`tests/Functional/YoFi.V3.Tests.Functional.csproj`](../../tests/Functional/YoFi.V3.Tests.Functional.csproj)
- [ ] Add `<PropertyGroup>` with generation paths and config file to Functional csproj
- [ ] Add `GenerateApiClient` MSBuild target to Functional csproj (references `Api/nswag.json`)
- [ ] Delete [`tests/Functional/Api/ApiClient.cs`](../../tests/Functional/Api/ApiClient.cs) with `git rm`
- [ ] Update [`tests/Functional/Api/README.md`](../../tests/Functional/Api/README.md)
- [ ] Test: Build WireApiHost to verify TypeScript generation still works
- [ ] Test: Clean build of Functional tests (`dotnet clean && dotnet build`)
- [ ] Test: Verify `obj/Debug/net10.0/Generated/ApiClient.cs` exists
- [ ] Test: Verify TypeScript client still generated at correct location
- [ ] Test: Run functional tests (`Run-FunctionalTestsVsContainer.ps1`)
- [ ] Update [`docs/wip/functional-tests/API-CLIENT-GENERATION-IMPROVEMENT.md`](../../docs/wip/functional-tests/API-CLIENT-GENERATION-IMPROVEMENT.md)
- [ ] Update `.roorules` API Client Generation Pattern section
- [ ] Commit changes with appropriate commit message

## Recommended Commit Message

```
refactor(tests): move C# API client generation to functional tests

Move C# API client generation from WireApiHost to Functional tests project,
generating into obj/ directory instead of checking into source control.

- Remove C# generation from WireApiHost nswag.json (keep TypeScript only)
- Add NSwag.MSBuild package to Functional tests project
- Add MSBuild target to generate C# client before compilation
- Output to obj/Debug/net10.0/Generated/ApiClient.cs (not source controlled)
- Delete tests/Functional/Api/ApiClient.cs from source control
- Update documentation and .gitignore

Benefits:
- Generated code no longer in source control (standard practice)
- No cross-project file dependencies
- Automatic regeneration on every build
- Each project owns its own API client generation

Related: API-CLIENT-GENERATION-IMPROVEMENT.md
```

## Next Steps After Implementation

1. **Monitor first CI/CD build** - Ensure automated builds work correctly
2. **Update team** - Notify team about new generation approach
3. **Close related items** - Mark any GitHub issues or ADO work items as complete
4. **Document lessons learned** - Update implementation document with actual results
5. **Consider future improvements:**
   - Apply same pattern to other test projects if needed
   - Investigate OpenAPI spec file approach (decouple from WireApiHost build)
   - Create shared MSBuild targets for reuse across projects
