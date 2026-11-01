# Debugging a Failing Test

Debug failures in order, starting with the evidence the test run already provides.

## 1. Run Only the Failing Scenario

Use the scenario method name as a filter so the failure is quick to reproduce:

```powershell
dotnet test .\tests\Functional\YoFi.V3.Tests.Functional.csproj `
  --filter "FullyQualifiedName~ScenarioMethodName"
```

Confirm the intended application and API are running with the same target as the runsettings file.
Check the test output for the failing Gherkin step, assertion message, browser console messages, and
server response details.

## 2. Use Playwright Inspector

Set `PWDEBUG` to `1` in the selected `.runsettings` file's
`RunConfiguration/EnvironmentVariables` section, then rerun the filtered test. The Inspector pauses
browser automation and lets you inspect the current page and locators. Restore `PWDEBUG` to `0`
after debugging.

The repository also has [`Edit-FunctionalTests.ps1`](../../../scripts/Edit-FunctionalTests.ps1),
which opens the functional-test folder in VS Code with the requested debug environment.

## 3. Inspect Generated Test Source

Generated NUnit source under `tests/Functional/obj/GeneratedFiles/` shows which step method the
generator bound to each feature phrase. Use it to diagnose bindings or scenario naming; never edit
generated files. Fix the `.feature` file or the step definition instead.

## 4. Correlate Backend Logs

The test base initializes the Test Control client and calls the identify endpoint at test start.
Use the test name and correlation information in the application logs to find backend failures.
See [Test correlation](TEST-CORRELATION.md) for the logging and tracing details.

For front-end timing failures, check that the page object waits for a deterministic ready signal and
that Nuxt client hydration has completed. See [the hydration pattern](NUXT-SSR-TESTING-PATTERN.md).
