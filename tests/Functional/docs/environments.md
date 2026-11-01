# Environments and Running Tests

Functional tests use the `webAppUrl`, `apiUrl`, credentials, timeout, and screenshot context supplied
by a `.runsettings` file. The functional test project defaults to
[`container-msedge.runsettings`](../runsettings/container-msedge.runsettings).

## Configure Local Secrets

Copy [`../.env.example`](../.env.example) to `.env` in `tests/Functional/` and set the password
variables needed for your target:

- `TESTPWORD_DEV` for the seeded development `Tester` account.
- `TESTPWORD` for the container `Tester` account. It must match the value used by Docker Compose.
- `TESTPWORD_PROD` for the production test account. Never commit this value.
- Production runsettings also use `PRODUCTION_WEB_APP_URL` and `PRODUCTION_API_BASE_URL`.

The `.env` file is local configuration and must remain untracked.

## Targets

| Target | Runsettings | Default URLs | Typical use |
| --- | --- | --- | --- |
| Development | `development.runsettings` | `http://localhost:5173/`, API `http://localhost:5379/` | App running locally |
| Container | `container.runsettings` or `container-msedge.runsettings` | `http://localhost:5000/`, API `http://localhost:5001/` | Local CI containers |
| Production | `production.runsettings` | Supplied through `.env` | Controlled smoke tests against a deployment |

Use the matching target file explicitly when running outside the project's default:

```powershell
dotnet test .\tests\Functional\YoFi.V3.Tests.Functional.csproj `
  --settings .\tests\Functional\runsettings\development.runsettings
```

The selected target must already be running. For local app startup see
[`scripts/Start-LocalDev.ps1`](../../../scripts/Start-LocalDev.ps1); for the container workflow see
[`scripts/Run-FunctionalTestsVsContainer.ps1`](../../../scripts/Run-FunctionalTestsVsContainer.ps1).

## Run a Filtered Test

Use a fully qualified name substring to run one scenario or a subset:

```powershell
dotnet test .\tests\Functional\YoFi.V3.Tests.Functional.csproj `
  --filter "FullyQualifiedName~UserUpdatesMemoViaQuickEditModal"
```

The generated scenario method names are visible in the test output or generated source under
`obj/GeneratedFiles/`.

## Install the Playwright Browser

Build the test project, then install Chromium for runsettings that use Playwright's default browser:

```powershell
dotnet build .\tests\Functional\YoFi.V3.Tests.Functional.csproj
.\tests\Functional\bin\Debug\net10.0\playwright.ps1 install chromium
```

The `*-msedge.runsettings` files select the installed Microsoft Edge channel instead; ensure Edge
is installed when using those files.

## Concurrency and Safety

The checked-in runsettings limit NUnit to one worker. Keep production test data isolated to test
accounts and `__TEST__` workspaces, and do not run destructive cleanup against real user data.
