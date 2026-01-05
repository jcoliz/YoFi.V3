# Gherkin Generator Project Plan

## Problem

Gherkin Generator must be released to Nuget before I can effectively leverage it.
It has to be released **before** I run a CI build on my code.
Ergo, it cannot be in this project. It has to be a stand-alone.

## Goal

Create a new GitHub repository: `Gherkin.Generator`, which contains everything here,
and releases the built analyzer to Nuget.

## Components

Project should contain the following:

1. src/Analyzer. The Gherkin.Generator analyzer project, as built below.
2. src/Lib. Gherkin.Generator.Lib. Contains all the business logic that isn't analyzer specific. This is everything in tests\Gherkin.Generator which doesn't take a dependency on Microsoft.CodeAnalysis namespace.
3. src/Utils. Gherkin.Generator.Utils. Test utilities which you would include in your own project.
   1. DataTable implementation. Currently in C:\Source\jcoliz\YoFi.V3\tests\Functional\Helpers\DataTable.cs. We need to move these out because the generator is tightly coupled.
   2. Given/When/Then attributes. Currently in tests\Functional\Attributes. Any user project will need these, so we should provide them.
   3. This will also be released to Nuget, using the same version, and at the same time as the analyzer.
4. src/Tool. Gherkin.Generator.Tool. See [STANDALONE-TOOL-ALTERNATIVE](STANDALONE-TOOL-ALTERNATIVE.md) design. This will also be released to Nuget, as `dotnet tool global` installable.
5. tests/Unit. Gherkin.Generator.Tests.Unit. Current unit tests, but only consume the Lib
6. tests/Example. Gherkin.Generator.Tests.Example. Test project which consumes the latest publicly release lib, and gives a complete example of how to use all the features. Would be great to use common sample reference gherkin examples (search web for Gherkin examples). Note the analyzer CANNOT BE consumed via project reference, see [DEPLOYMENT-CHALLENGES](DEPLOYMENT-CHALLENGES.md). This project cannot be created until a nuget package is created first.
7. docs/ directory containing all docs, aside from README. docs root contains public docs for user or developer consumption.
8. docs/USER-GUIDE.md comprehensive guide for user wanting to use this.
9. docs/wip directory contains works in progress. AI Assistants should place documents here by default.
10. templates/Default.mustache. Copy (don't move!) of current tests\Functional\Features\FunctionalTest.mustache
11. templates/README.md. Explain that you should start with the default, and modify it as needed to match your test infrastructure or other conventions.
12. README.md at the root, explaining the project at a high level. 200 words max. Includes badges for: Build action;  latest nuget version # and link
13. github/workflows/release.yaml: pushes updates to nuget when release is created
14. github/workflows/ci.yaml: builds everything, runs tests

## Migration Plan

Most of these components are currently in tests\Gherkin.Generator and tests\Gherkin.Generator.Tests. The existing
components will be removed. When this work is complete, the YoFi.V3 project will consume the Gherkin.Generator
packages from GitHub.

YoFi.V3 is currently using this in a branch. It cannot be checked into main branch until available in NuGet. Once
this work is complete, the current YoFi.V3 branch will be abandoned, and the main branch will consume the public version only.

Note that the Gherkin feature set is complete as far as YoFi.V3 is concerned. The first step is to re-organize and release the current functionality (analyzer and lib). Utils, Tests.Example, and Tool are all new functionality which will come after the initial release is in place.

## Release Plan

Pushed to NuGet from GitHub when new release is created. Release name will be straight semantic version, e.g. `1.2.3`

First release of ours will be 0.0.1. No beta/preview tags.
First release that works with YoFi will be upgraded to 0.1.0.
When YoFi is fully moved over, we will move to 1.0.0.

## Package Dependency Strategy

The `Gherkin.Generator` analyzer package will have a PackageReference to `Gherkin.Generator.Utils`, ensuring that users only need to install the analyzer package to get all required dependencies automatically via transitive dependency.

**User experience:**
```xml
<!-- User's test project - only needs this -->
<PackageReference Include="Gherkin.Generator" Version="1.1.0" />
<!-- Gherkin.Generator.Utils comes automatically -->
```

**Implementation:**
In `src/Analyzer/Gherkin.Generator.csproj`:
```xml
<ItemGroup>
  <!-- Include Utils as a dependency that flows to consumers -->
  <PackageReference Include="Gherkin.Generator.Utils" Version="$(Version)" />
</ItemGroup>
```

**Rationale:**
- Simplest user experience - install one package, get everything needed
- Version synchronization guaranteed - no mismatch possible
- No missing dependency errors - Utils is always present
- Standard pattern for analyzers with runtime dependencies (e.g., xunit.analyzers → xunit.core)

## Implementation ordering

1. Gherkin.Generator.Lib, including Github build/pr action
2. templates/Default.mustache
3. Gherkin.Generator.Tests.Unit, including adding tests to build/pr action.
4. Gherkin.Generator analyzer project, as exists today
5. Initial project README
6. Release tooling, including github workflow and all necessary Nuget metadata <- 0.0.1 MVP
7. Gherkin.Generator.Utils, including new unit tests for them <- Required for 1.0.0.
8. Update release tooling for utils
9. Gherkin.Generator.Tests.Example
10. Port over docs, create USER-GUIDE, templates/README
11. Gherkin.Generator.Tool
12. Update release tooling to include tool

Note that the analyzer works fine with utils still being in the target project as they are today.
Moving to utils isn't required for initial testing in YoFi.V3.
