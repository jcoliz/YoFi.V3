# Data integration tests

These tests focus on data logic in isolation, integrating with an actual data layer.

## Goals

1. **Catch EF Core configuration issues** before they hit production
2. **Document expected database behavior** through tests
3. **Validate PostgreSQL behavior**, including enforced lengths and numeric precision
4. **Isolate tests**: Migrated tests get a fresh database with the EF migrations applied
5. **Accelerate refactoring**: Safe to change `ApplicationDbContext` implementation

## Running Tests

Run all data integration tests from the repository root with Docker running:

```powershell
dotnet test .\tests\Integration.Data\YoFi.V3.Tests.Integration.Data.csproj
```

`PostgreSqlTestDatabase` starts a `postgres:17-alpine` Testcontainers container once
for the test assembly. `PostgreSqlTestBase` creates and migrates a unique database
before each test and drops that database after disposing the context. No backend
host, environment-variable overrides, or Aspire environment is required.

The test container is disposed after the assembly finishes. This does not stop
or modify any application containers. Its name is printed in test output.

All fixtures use the shared PostgreSQL base. Schema checks query PostgreSQL catalogs,
and timestamp and currency assertions verify native PostgreSQL storage behavior.