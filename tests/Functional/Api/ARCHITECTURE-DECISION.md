# Architecture Decision: Manual API Client Generation

## Status
Implemented - December 31, 2025

## Context

The functional test project requires a C# API client to interact with the backend API. NSwag generates this client from the OpenAPI specification. We had two competing approaches:

### Previous Approach (Automatic Generation)
- NSwag ran automatically during every build via MSBuild targets
- Generated file was placed in `obj/Generated/` (not source-controlled)
- Triggered by PostBuildEvent in WireApiHost and BeforeCompile in functional tests

### Problem Identified
Every time a developer ran tests (even without code changes), the build system:
1. Built the entire WireApiHost project
2. Ran NSwag to extract OpenAPI from the compiled DLL (~8 seconds)
3. Generated TypeScript client for frontend (~8 seconds)
4. Generated C# client for tests (~6 seconds)
5. **Total overhead: ~15-20 seconds per test run**

This happened even when:
- No API changes had been made
- Developer was only modifying test code
- Only running a single test to debug an issue

## Decision

**We moved to manual API client generation with source-controlled output.**

## Implementation

1. **Removed automatic generation** from both projects:
   - `YoFi.V3.WireApiHost.csproj`: Removed PostBuildEvent NSwag target
   - `YoFi.V3.Tests.Functional.csproj`: Removed GenerateApiClient target

2. **Changed output location**:
   - From: `obj/Generated/ApiClient.cs` (gitignored, ephemeral)
   - To: `Api/Generated/ApiClient.cs` (source-controlled, permanent)

3. **Enhanced generation script**:
   - Updated `scripts/Generate-ApiClient.ps1` to generate both TypeScript and C# clients
   - Added verification and error handling
   - Documented when and why to regenerate

## Rationale

### Why We Initially Used Automatic Generation
The original architect mode recommendation was based on:
- Ensuring API client always matches backend (correctness)
- Avoiding manual steps that developers might forget
- Following the "fail fast" principle

This made sense when API changes were frequent and the team was small.

### Why We Reverted to Manual Generation
After using the system, we discovered:

1. **Build Performance**: The ~20 second overhead affected developer productivity significantly
   - Tests run frequently during development
   - Most test runs don't involve API changes
   - Local development became frustratingly slow

2. **Incremental Build Complexity**: Attempts to add proper incremental build tracking failed because:
   - MSBuild's timestamp-based system doesn't handle transitive dependencies well
   - NSwag loads DLLs at runtime, making dependency tracking unreliable
   - File touching by MSBuild internals caused false "out of date" triggers
   - The WireApiHost project always rebuilt some targets, updating timestamps

3. **Source Control Benefits**: Checking in generated code actually has advantages:
   - Code reviews show API contract changes explicitly
   - Git history shows when API changed vs when tests changed
   - Build reproducibility across environments (no NSwag version mismatches)
   - CI/CD can verify committed file matches generated output

4. **API Stability**: The API is now relatively stable, with changes occurring infrequently

## Consequences

### Positive
- ✅ Test runs are now ~20 seconds faster (build time: 2s vs 22s)
- ✅ Developer experience improved significantly
- ✅ API changes are explicit in code reviews
- ✅ Build is more predictable and reproducible

### Negative
- ⚠️ Developers must remember to run script after API changes
- ⚠️ Risk of forgetting to regenerate (though tests will fail if mismatched)

### Mitigation Strategies
1. **Documentation**: Clear README explaining when/how to regenerate
2. **Script Enhancement**: Generation script validates both clients were created
3. **CI Validation**: CI pipeline can detect if committed file is out of date
4. **Test Failures**: Tests will fail quickly if API client is stale (compilation errors or runtime failures)

## Alternative Considered: Conditional Generation

We considered adding a condition like:
```xml
<Target Name="NSwag" Condition="'$(GenerateApiClient)' == 'true'">
```

Then developers would opt-in: `dotnet build /p:GenerateApiClient=true`

**Why we didn't choose this:**
- Still requires remembering to pass parameter
- Not discoverable (hidden behind build flag)
- Adds cognitive load (when should I set this?)
- Doesn't provide source control benefits

## Lessons Learned

1. **Premature Optimization**: Automatic generation optimized for "correctness at all costs" before we knew the actual usage patterns

2. **Build Performance Matters**: Even 20 seconds adds up quickly when running tests dozens of times per day

3. **Source Control Isn't Evil**: Generated code in source control can be a feature, not a bug

4. **Context Changes**: What's right for early development (frequent API changes) may not be right for maintenance (stable API)

5. **Developer Experience**: Tools should enhance productivity, not hinder it

## When to Revisit

This decision should be reconsidered if:
- API changes become frequent again (multiple times per day)
- Team grows significantly (coordination overhead increases)
- We find a reliable incremental build solution
- MSBuild/NSwag tooling improves to handle this better

## References

- Original issue: "Every test run rebuilds API, wastes ~20 seconds"
- Related files:
  - `tests/Functional/YoFi.V3.Tests.Functional.csproj`
  - `src/WireApiHost/YoFi.V3.WireApiHost.csproj`
  - `scripts/Generate-ApiClient.ps1`
  - `tests/Functional/Api/README.md`
