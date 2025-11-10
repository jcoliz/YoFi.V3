# Functional (E2E) Tests

Playwright-driven browser-based tests running against the Front End.

## Principles

1. **Completeness**: All user scenarios should be covered by functional tests
2. **Multiple Targets**: Tests can be run against multiple target environments. (See "Targets", below.)
3. **Fast, simple, reliable**: In order to be effective, developers need to be able to quickly run them locally before every checkin. If they're flaky, or difficult to setup or run, or take too long, we won't actually run them locally.
4. **Pre-deployment CI**: Functional tests absolutely must run in automated tests, and server as a blocker to deployment.
5. **Non-destructive**. We must be able to run the functional tests against a deployed instance, so it must be sufficiently isolated that it doesn't interfere with real-life use.
6. **Gherkin**: Tests will be written in strict Gherkin form.
7. **Generated Future**: In the future, we'll use C# code generation to convert the Gherkin features into running C# tests. In the meantime, the hand-written tests will mimic the generated code in the future. The [FunctionalTest.mustache](./Features/FunctionalTest.mustache) file gives the template for these tests
8. **Page Object Models**: We use page and component models to encapsulate knowledge about the structure of specific pages or controls.

## Targets

1. **Docker container**. Run against a locally-built or remotely-pulled container image.
2. **Local build**. Run against the locally-built development bits running in Visual Studio or running with `dotnet watch`.
3. **Deployed instance**. Run against any deployed version in Azure App Service, including the production environment.

The choice of target is governed by a `.runsettings` file, providing details about the target.

### Local build

You can run tests against a build running locally, either in Visual Studio or with `dotnet watch`. In one window, launch the app. In another, run the functional tests with `local.runsettings`.

### Docker container

Running in a docker container is a future project.

### Deployed instance

Likewise, running against a deployed instance is for the far future. Right now, the codebase cannot even be deployed!!

## Getting Started

Before running the tests, there are two key prerequisites.

1. Install [.NET 10.0 SDK](https://dotnet.microsoft.com/en-us/download)
2. Install Chromium for Playwright 

To install Chromium for Playwright, first build the functional tests. Then run the install script which is generated in the build. This only needs to be done once on initial setup, then again 
whenever the Playwright packages are updated to a new version.

```Powershell
.\bin\Debug\net10.0\playwright.ps1 install chromium
```
