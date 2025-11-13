# Integrations tests

These tests focus on data logic in isolation, integrating with an actual data layer.

## Goals

1. **Catch EF Core configuration issues** before they hit production
2. **Document expected database behavior** through tests
3. **Validate SQLite compatibility** (important since you're using it everywhere)
4. **Fast feedback**: In-memory SQLite tests run quickly
5. **Accelerate refactoring**: Safe to change `ApplicationDbContext` implementation

## Running Tests

```powershell
dotnet test
```