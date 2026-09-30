# Validation Policy

**Status:** Active
**Last Updated:** 2026-09-29
**Applies To:** All YoFi.V3 request/input validation

## Overview

This document describes how input validation works across YoFi.V3. Validation is
layered: each layer has a distinct responsibility, from request-shape checks at the
API boundary down to database schema constraints at persistence time. The primary
strategy for validating request bodies is **FluentValidation**, run automatically at
the controller boundary.

## Core Principles

1. **Validate at the boundary** - Invalid requests are rejected before reaching
   controller actions or the Application layer, returning `400 Bad Request`.
2. **FluentValidation for request bodies** - Complex bound DTOs are validated by
   `AbstractValidator<T>` classes in the Application layer.
3. **DataAnnotations for scalars and schema** - Individual action parameters and EF
   Core entity constraints use DataAnnotations attributes.
4. **Consistent error format** - All validation failures return RFC 9110
   `application/problem+json` with a combined `detail` string and a `traceId`.
5. **Business rules live in validators** - Validators may inject services (e.g. regex
   safety) so validation enforces business rules, not just shape.

## The Four Validation Layers

### 1. FluentValidation — primary request-body validation

Located in [`src/Application/Validation`](../src/Application/Validation). These are
`AbstractValidator<T>` classes that validate complex DTOs bound from the request body:

| Validator | Validates |
| --- | --- |
| `TransactionEditDtoValidator` | Full transaction create/update (date range, non-zero amount, payee, length limits) |
| `TransactionQuickEditDtoValidator` | Lighter subset for inline list edits (payee, memo, category) |
| `PayeeMatchingRuleEditDtoValidator` | Payee rule rules, including ReDoS-safe regex checking via `IRegexValidationService` |
| `CollectionRequestValidator<T>` | Generic wrapper that applies an item validator to each element of a bulk `CollectionRequest<T>` |

**Key behavior:** FluentValidation auto-validation only runs on **complex (bound)
model types**. It deliberately skips primitive/scalar types, so a `[FromQuery] int` or
`[FromRoute] Guid` will never trigger a FluentValidation validator.

### 2. DataAnnotations custom attributes — scalar parameter validation

Located alongside the validators:

- [`DateRangeAttribute`](../src/Application/Validation/DateRangeAttribute.cs) - validates a
  `DateOnly` is within a relative range of today.
- [`NotWhiteSpaceAttribute`](../src/Application/Validation/NotWhiteSpaceAttribute.cs) - validates
  a string is not null/empty/whitespace.

Both declare `AttributeTargets.Parameter`, so they can annotate individual action
parameters. Because every controller is marked `[ApiController]`, ASP.NET Core
automatically enforces DataAnnotations on action parameters and returns `400` before
the action runs — no manual `ModelState.IsValid` check needed. Both attributes return
`Success` on null so `[Required]` owns null-checking.

### 3. Entity-level DataAnnotations — database schema

`[Required]` / `[MaxLength]` attributes on the EF Core models in
[`src/Entities/Models`](../src/Entities/Models) (e.g. `Transaction`, `Split`,
`PayeeMatchingRule`) primarily shape the **database schema** (column nullability and
sizes). They are a last line of defense at persistence time, not the primary
request-validation mechanism.

### 4. Manual controller-level validation — via exception

For checks that don't fit model binding (file uploads, format, size limits), throw
[`ValidationException`](../src/Entities/Exceptions/ValidationException.cs) from the
controller. The registered `CustomExceptionHandler` maps it to `400 Bad Request`.

## How It's Wired In

All registration happens in
[`src/Controllers/Extensions/ServiceCollectionExtensions.cs`](../src/Controllers/Extensions/ServiceCollectionExtensions.cs),
in `AddControllerServices()`, which is called from
[`src/BackEnd/Program.cs`](../src/BackEnd/Program.cs):

```csharp
// 1. Assembly scan — auto-registers every AbstractValidator<T> in the Application assembly
services.AddValidatorsFromAssemblyContaining<TransactionEditDtoValidator>();

// 2. Explicit registration for the open generic (not caught by the scan)
services.AddTransient<IValidator<CollectionRequest<TransactionEditDto>>,
                      CollectionRequestValidator<TransactionEditDto>>();

// 3. Hook FluentValidation into the model-binding pipeline
services.AddFluentValidationAutoValidation();
```

- **`AddValidatorsFromAssemblyContaining`** scans the whole Application assembly and
  registers every concrete `AbstractValidator<T>` automatically. New non-generic
  validators need no manual registration.
- **The explicit `AddTransient`** exists because `CollectionRequestValidator<T>` is an
  *open generic*, which the assembly scan does not materialize. Each closed generic
  type you use must be registered by hand.
- **`AddFluentValidationAutoValidation`** connects validators into ASP.NET Core's
  model-binding pipeline, so they run at the controller boundary automatically.

The error-response shape is customized via `InvalidModelStateResponseFactory` in the
same file: it emits `application/problem+json`, combines all messages into a `detail`
string for frontend convenience, and attaches a W3C `traceId` for diagnostics.

## Request Flow

```mermaid
flowchart TD
    A[HTTP request] --> B[Model binding]
    B --> C{Bound complex DTO?}
    C -- yes --> D{FluentValidation<br/>AutoValidation}
    C -- scalar params --> E{DataAnnotations<br/>via ApiController}
    D -- invalid --> F[400 problem+json<br/>detail + traceId]
    E -- invalid --> F
    D -- valid --> G[Controller action]
    E -- valid --> G
    G -- manual check fails --> H[throw ValidationException]
    H --> I[CustomExceptionHandler → 400]
    G --> J[Application layer / Features]
    J --> K[EF Core save]
    K -. schema constraints .-> L[(Database)]
```

## How To Validate...

### ...a request body (complex DTO)

Write an `AbstractValidator<TDto>` in `src/Application/Validation`. The assembly scan
registers it and auto-validation invokes it whenever an action binds that DTO from the
body. Nothing else required.

### ...an individual scalar parameter

Annotate the parameter with a DataAnnotations attribute. `[ApiController]` enforces it
automatically:

```csharp
public async Task<IActionResult> GetTransactions(
    [FromRoute] Guid tenantKey,
    [FromQuery, Range(1, int.MaxValue)] int? pageNumber = null,
    [FromQuery, DateRange(50, 5)] DateOnly? fromDate = null,
    [FromQuery, DateRange(50, 5)] DateOnly? toDate = null)
```

### ...a rule spanning multiple parameters

FluentValidation cannot compare separate scalar arguments. Bind the parameters into a
single query/request model and write an `AbstractValidator<T>` for it, so cross-field
rules (e.g. `fromDate <= toDate`) can be expressed across its properties.

### ...something that isn't model-bound

Throw `ValidationException` from the controller; the `CustomExceptionHandler` returns
`400`.

## Guidance Summary

- Request body? → FluentValidation `AbstractValidator<T>` (auto-registered).
- Independent scalar constraint? → DataAnnotations attribute on the parameter.
- Cross-parameter rule? → wrap parameters in a model, validate with FluentValidation.
- Non-bound / imperative check? → throw `ValidationException`.
- New open-generic validator? → add an explicit `AddTransient` registration per closed type.
