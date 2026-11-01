# Test Control APIs

The Test Control API gives functional tests a fast way to set up isolated users, workspaces, and
domain data without driving setup screens through the browser. Use Playwright for the behavior and
visible results the test is meant to verify.

The generated client is available from `ITestContext.TestControlClient`. It is initialized by
`FunctionalTestBase` using the `userName` and `userPassword` runsettings parameters. The configured
test account must have the `Functional Tests` role.

## Test Resource Setup

Use the established helpers in `ITestContext` and the workspace step classes to create test users
and workspaces. Usernames and workspace names are prefixed with `__TEST__`; the Test Control API
validates test-resource names and workspace access.

The test context tracks users and workspaces created for a test. `FunctionalTestBase` removes tracked
workspaces and users during teardown. Prefer per-test tracked resources to broad cleanup operations,
especially when running tests against a shared or deployed environment.

**Shared-environment warning:** `WorkspaceDataSteps.GivenIAmLoggedInAsAUserWithEditorRole` currently
calls `TestControl_DeleteAllTestDataAsync` before setting up its user and workspace. That operation
clears test-prefixed data beyond the current test, so do not use this setup step against a shared
environment or alongside other test runs. Prefer steps that create and track only the resources
needed by the current test.

## Seed Precise Transactions

When a scenario depends on particular transaction values, seed those values with
`TestControlClient.TestControl_SeedTransactionsPreciseAsync`. The API accepts a collection of
`TransactionEditDto` values and returns the transactions that were created. It validates the
requested transactions and checks the test user/workspace association.

The endpoint is:

```text
POST /testcontrol/users/{username}/workspaces/{tenantKey}/transactions/seed-precise
```

### One transaction

`TransactionDataSteps.GivenIHaveAWorkspaceWithATransaction` accepts a Gherkin `Field`/`Value` table
and uses the precise seed endpoint. It stores the returned transaction values and key for later
steps. `Payee` is required; the step defaults `Amount` to `100.00`, and sets `Date` to the current
UTC time.

```gherkin
Given I have a workspace with a transaction:
    | Field    | Value          |
    | Payee    | Coffee Shop    |
    | Amount   | -5.50          |
    | Category | Beverages      |
    | Memo     | Morning coffee |
    | Source   | Checking       |
    | ExternalId | TX-1001      |
```

### Multiple transactions with exact dates and external IDs

For duplicate-detection scenarios, use `Given I have existing transactions with external IDs:` or
`Given I have these exact transactions already:`. The table contains `ExternalId`, `Date`, `Payee`,
and `Amount`; the step sends each row through the same precise seed endpoint.

```gherkin
Given I have these exact transactions already:
    | ExternalId | Date       | Payee       | Amount |
    | FITID-001  | 2024-01-05 | Coffee Shop | -5.50  |
```

These are API-backed setup steps: they do not navigate to the transactions page. Add a separate
navigation step before checking the UI.

## Client and API Source

The C# client is generated from the checked-in OpenAPI description during the functional test
project build. Do not edit the generated client under `obj/GeneratedFiles/`. The source endpoint is
[`TestControlController`](../../../src/Controllers/TestControlController.cs); the client setup and
authentication logic are in [`TestControlClientBase.cs`](../Infrastructure/TestControl/TestControlClientBase.cs).
