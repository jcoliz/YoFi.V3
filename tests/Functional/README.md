# Functional (E2E) Tests

Playwright-driven browser-based tests running against the Front End. Top-quality and exhaustive functional tests
are an absolute requirement to release new features quickly. Having these, we can be assured that new
changes do not break existing features.

## Principles

1. **Behavior-Driven Development**. When crafting new functionality, *start* by writing the behavior into a functional test, and then implement the feature in code.
2. **Completeness**: All user scenarios should be covered by functional tests
3. **Fast, simple, reliable**: In order to be effective, developers need to be able to quickly run them locally before every commit. If the tests are flaky, or difficult to setup or run, or take too long, we won't actually run them locally.
4. **Multiple Targets**: Tests can be run against multiple target environments. (See "Targets", below.)
5. **Pre-deployment CI**: Functional tests absolutely must run in automated tests. Failed tests must block deployment.
6. **Non-destructive**. We must be able to run the functional tests against a deployed instance, so it must be sufficiently isolated that it doesn't interfere with real-life use.
7. **Gherkin**: Tests will be written in strict Gherkin form.
8. **Generated Future**: In the future, we'll use C# code generation to convert the Gherkin features into running C# tests. In the meantime, the hand-written tests will mimic the generated code in the future. The [FunctionalTest.mustache](./Features/FunctionalTest.mustache) file gives the template for these tests
9. **Page Object Models**: We use page and component models to encapsulate knowledge about the structure of specific pages or controls.

## Test Architecture

The functional tests follow a layered architecture that separates test intent from implementation:

```mermaid
flowchart TD
    A[Gherkin Features<br/>.feature files] -->|Compiled via<br/>Mustache Template| B[Test Classes<br/>Tests/*.feature.cs]
    B -->|Inherits| C[Step Definitions<br/>Steps/*.cs]
    C -->|Inherits| D[Common Steps<br/>Steps/Common/*.cs]
    D -->|Inherits| E[Test Infrastructure<br/>Infrastructure/*.cs]
    C -->|Uses| F[Page Models<br/>Pages/*.cs]
    F -->|Contains| G[Component Models<br/>Components/*.cs]
    F -->|Uses| H[Playwright API]
    G -->|Uses| H
```

**Directory Structure:**
```
tests/Functional/
├── Infrastructure/          # Core test infrastructure
│   ├── FunctionalTestBase.cs   # Base class with setup/teardown
│   └── ObjectStore.cs          # Shared data storage
├── Steps/                   # Step definitions
│   ├── Common/              # Shared step implementations
│   │   ├── CommonGivenSteps.cs
│   │   ├── CommonWhenSteps.cs
│   │   └── CommonThenSteps.cs
│   ├── AuthenticationSteps.cs  # Feature-specific steps
│   ├── WeatherSteps.cs
│   └── WorkspaceTenancySteps.cs
├── Pages/                   # Page Object Models
├── Components/              # Reusable UI components
├── Features/                # Gherkin feature files
└── Tests/                   # Generated test classes
```

**Inheritance Hierarchy:**
```
FunctionalTestBase (Infrastructure/)
    ↓
CommonGivenSteps → CommonWhenSteps → CommonThenSteps (Steps/Common/)
    ↓
AuthenticationSteps, WeatherSteps, WorkspaceTenancySteps (Steps/)
    ↓
Generated Test Classes (Tests/)
```

**Flow:**
- **Gherkin Features** ([`Features/`](Features/)) define test scenarios in business-readable language
- **Test Classes** ([`Tests/`](Tests/)) are generated from Gherkin, containing `[Test]` methods that call step methods
- **Step Definitions** ([`Steps/`](Steps/)) orchestrate the test flow by calling page models
  - **Common Steps** ([`Steps/Common/`](Steps/Common/)) provide shared Given/When/Then implementations
  - **Feature Steps** extend common steps with feature-specific implementations
- **Infrastructure** ([`Infrastructure/`](Infrastructure/)) provides test lifecycle, correlation headers, and shared utilities
- **Page Models** ([`Pages/`](Pages/)) encapsulate page structure and provide interaction methods
- **Component Models** ([`Components/`](Components/)) are reusable UI components shared across multiple pages
- **Playwright API** provides browser automation (locators, actions, assertions)

This separation ensures maintainability: UI changes only affect Page/Component Models, infrastructure changes are isolated, and common test logic is reused.

## Targets

1. **Development**. Run against the locally-built development bits running in Visual Studio or running with `dotnet watch`.
2. **Container**. Run against a locally-built or remotely-pulled container image.
3. **Production**. Run against any deployed version in Azure App Service, including the production environment.

The choice of target is governed by a `.runsettings` file, providing details about the target.

## Configuration

Test configuration uses `.runsettings` files combined with environment variables from `.env` files.

### Environment Variables (.env)

Environment variables provide a flexible way to configure test targets without modifying version-controlled files.

**Setup:**
1. Copy [`.env.example`](./.env.example) to `.env`
2. Set your environment URLs in `.env`:
   ```bash
   # Production
   PRODUCTION_WEB_APP_URL=https://your-app.azurestaticapps.net
   PRODUCTION_API_BASE_URL=https://your-api.azurewebsites.net

   # Additional environments
   STAGING_WEB_APP_URL=https://your-app-staging.azurestaticapps.net
   STAGING_API_BASE_URL=https://your-api-staging.azurewebsites.net
   ```
3. The `.env` file is automatically loaded during test initialization

**Note:** The `.env` file is ignored by Git to keep environment URLs out of version control.

### RunSettings Files

- **[`runsettings/development.runsettings`](./runsettings/development.runsettings)** - Local development (hardcoded localhost URLs)
- **[`runsettings/container.runsettings`](./runsettings/container.runsettings)** - Docker container testing
- **[`runsettings/production.runsettings`](./runsettings/production.runsettings)** - Production environment (references `PRODUCTION_*` variables from `.env`)

To run tests against production:
```powershell
dotnet test --settings runsettings/production.runsettings
```

You can create additional `.runsettings` files for other environments (staging, dev) following the same pattern.

### Development build

Run tests against a build running locally, either in Visual Studio or with `dotnet watch`. In one window, launch the app. In another, run the functional tests:

```powershell
dotnet test --settings runsettings/development.runsettings
```

### Container

Run tests against a locally-built or remotely-pulled container image:

```powershell
dotnet test --settings runsettings/container.runsettings
```

### Production

Run tests against a deployed instance in Azure App Service. First configure your `.env` file with production URLs, then run:

```powershell
dotnet test --settings runsettings/production.runsettings
```

## Getting Started

Before running the tests, there are two key prerequisites.

1. Install [.NET 10.0 SDK](https://dotnet.microsoft.com/en-us/download)
2. Install Chromium for Playwright

To install Chromium for Playwright, first build the functional tests. Then run the install script which is generated in the build. This only needs to be done once on initial setup, then again
whenever the Playwright packages are updated to a new version.

```Powershell
.\bin\Debug\net10.0\playwright.ps1 install chromium
```

## Adding new tests

1. Write new Gherkin scenarios in a [`Features/`](./Features/) file. Either create a new feature, or add new scenarios to an existing feature.
2. Ensure the steps are available:
   - Add to [`Steps/Common/`](./Steps/Common/) if shared across features
   - Add to feature-specific step class in [`Steps/`](./Steps/) if unique to that feature
3. Write additional [Page](./Pages/) or [Component](./Components/) models if page functionality is new or changed.
4. Write or update new C# test files in the [`Tests/`](./Tests/) directory, following the [Instructions](./INSTRUCTIONS.md) provided. GitHub Copilot is excellent at this!

See [`Steps/README.md`](./Steps/README.md) for detailed guidance on writing step definitions.

## Known issues

Unfortunately, the [Playwright Test for VSCode](https://marketplace.visualstudio.com/items?itemName=ms-playwright.playwright) does not support C# test runners. This request was made and rejected. See [Feature: Playwright Test for VSCode Support for C# Projects ](https://github.com/microsoft/playwright/issues/38045).

To bring up the Playwright debugger, change the `PWDEBUG` environment variable using the `.runsettings` file.

```xml
  <RunConfiguration>
    <EnvironmentVariables>
      <PWDEBUG>0</PWDEBUG>
    </EnvironmentVariables>
  </RunConfiguration>
```
