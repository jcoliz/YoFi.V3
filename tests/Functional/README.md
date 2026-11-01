# Functional Tests

YoFi's functional tests drive the application through a real browser with Playwright. Scenarios are
written in Gherkin and compiled into NUnit tests by `Gherkin.Generator`.

## Run This Before You Push

For the normal container-based workflow, from the repository root run:

```powershell
.\scripts\Run-FunctionalTestsVsContainer.ps1
```

To run tests against containers that are already running:

```powershell
.\scripts\Run-Tests.ps1 -Functional
```

To run one scenario, use a test filter:

```powershell
dotnet test .\tests\Functional\YoFi.V3.Tests.Functional.csproj `
  --filter "FullyQualifiedName~UserUpdatesMemoViaQuickEditModal"
```

See [docs/environments.md](docs/environments.md) for local-development, container, and production
configuration, and [docs/debugging.md](docs/debugging.md) when a test fails.

## I Want To...

| Goal | Go to |
| --- | --- |
| Understand the test architecture | [docs/architecture.md](docs/architecture.md) |
| Write a Gherkin scenario | [docs/writing-features.md](docs/writing-features.md) |
| Add or change step definitions | [docs/writing-steps.md](docs/writing-steps.md) |
| Create or change a page object | [docs/writing-pages.md](docs/writing-pages.md) |
| Add or update front-end test hooks | [docs/frontend-contract.md](docs/frontend-contract.md) |
| Seed precise transaction data | [docs/TEST-CONTROLS.md](docs/TEST-CONTROLS.md) |
| Understand Nuxt hydration waits | [docs/NUXT-SSR-TESTING-PATTERN.md](docs/NUXT-SSR-TESTING-PATTERN.md) |
| Find existing step phrases | `bin/Debug/net10.0/YoFi.V3.Tests.Functional.StepCatalog.md` (written when the test assembly loads) |
| Review the test-layer strategy | [../../docs/TESTING-STRATEGY.md](../../docs/TESTING-STRATEGY.md) |

## Project Structure

- [`Features/`](Features/) contains the Gherkin source of truth.
- [`Steps/`](Steps/) contains the C# definitions bound to Gherkin steps.
- [`Pages/`](Pages/) and [`Components/`](Components/) contain Playwright page/component models.
- [`Infrastructure/`](Infrastructure/) contains the test base, context, and Test Control API setup.
- [`docs/`](docs/) contains the functional-test authoring and operations guides.

## House Rules

1. **Describe behavior in Gherkin.** Keep scenarios focused on user-visible behavior rather than
   implementation details.
2. **Arrange state through Test Control APIs; verify behavior in the browser.** For transactions,
   use `TestControl_SeedTransactionsPreciseAsync` with the exact values needed by the scenario.
3. **Keep Playwright locators and actions in page/component objects.** Steps should express the
   scenario and orchestrate those objects.
4. **Wait for deterministic UI state.** Do not use fixed delays to paper over navigation or
   hydration races.
5. **Keep test state isolated.** Use the test-user/workspace setup helpers and track resources for
   cleanup.

## Adding a Test

Follow [docs/WRITING-TESTS.md](docs/WRITING-TESTS.md) for the incremental workflow and the focused
guides linked there. Build the functional test project to run Gherkin source generation; generated
files are build artifacts and must not be edited.
