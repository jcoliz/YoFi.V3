---
status: Active
last_updated: 2026-11-24
applies_to: All YoFi.V3 request/input validation
---

# Validation Policy

## Overview

This document describes how input validation works across YoFi.V3. Validation is
layered: each layer has a distinct responsibility, from request-shape checks at the
API boundary down to database schema constraints at persistence time. The primary
strategy for validating request bodies is **FluentValidation**, run automatically at
the controller boundary.

## Implementation Follow-ups

- Ensure every client-originated `400 Bad Request` response uses
  `ValidationProblemDetails` (or `HttpValidationProblemDetails`) with `errors` and a
  trace ID. Plain `ProblemDetails` is not sufficient for validation failures.

## Core Principles

1. **Validate at the boundary** - Invalid requests are rejected before reaching
   controller actions or the Application layer, returning `400 Bad Request`.
2. **FluentValidation for request bodies** - Complex bound DTOs are validated by
   `AbstractValidator<T>` classes in the Application layer.
3. **DataAnnotations for scalars and schema** - Individual action parameters and EF
   Core entity constraints use DataAnnotations attributes.
4. **Consistent error format** - All `400 Bad Request` validation failures return RFC 9110
   `application/problem+json` as `ValidationProblemDetails` (or
   `HttpValidationProblemDetails`) with structured `errors`, a `traceId`, and
   a user-facing `title`. A `detail` may provide general explanatory text but is
   not required.
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

### 2. DataAnnotations — scalar parameter validation

Use built-in DataAnnotations such as `[Range]` or `[Required]` to validate scalar
action parameters where appropriate. ASP.NET Core's model-validation pipeline invokes
applied attributes derived from `ValidationAttribute`; `[ApiController]` then returns
`400` for invalid model state before the action runs, without a manual
`ModelState.IsValid` check.

For a reusable scalar constraint not covered by a built-in attribute, a custom
`ValidationAttribute` can allow `AttributeTargets.Parameter` and implement the
validation check. If null is permitted by the check, let `[Required]` handle null
validation separately when needed.

### 3. Entity-level DataAnnotations — database schema

`[Required]` / `[MaxLength]` attributes on the EF Core models in
[`src/Entities/Models`](../src/Entities/Models) (e.g. `Transaction`, `Split`,
`PayeeMatchingRule`) primarily shape the **database schema** (column nullability and
sizes). They are a last line of defense at persistence time, not the primary
request-validation mechanism.

### 4. Manual and application-level validation — via exception

For checks that don't fit model binding, throw a validation exception rather than
returning ad hoc responses:

Use [`ValidationException`](../src/Entities/Exceptions/ValidationException.cs) for
simple or grouped failures, optionally with a parameter name, title, and `Errors`
dictionary. When no title is supplied, the handler uses "The request failed validation."
If a parameter name is supplied, the handler adds the exception message under that
name, appending it to any existing messages for the same key. With no parameter name,
the handler uses the grouped errors alone, or adds the message under `validation`
when there are no grouped errors. The exception message is also returned as `detail`.

Do not return domain-specific `400` response shapes or add domain-specific handlers
for invalid input. Use these existing validation mechanisms and let
`CustomExceptionHandler` produce `HttpValidationProblemDetails`. Resource, access,
and concurrency errors use their appropriate non-`400` responses.

As an example, the existing `TenantDataImportValidationException` retains its internal preview,
but the standard `ValidationException` handler returns its title and message without
exposing the preview. The preview remains available through the preview endpoint.

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

FluentValidation participates in ASP.NET Core model binding. When model state is
invalid, `InvalidModelStateResponseFactory` returns `ValidationProblemDetails`
serialized as `application/problem+json` with structured `errors` and a W3C
`traceId`. It does not duplicate error messages in `detail`. Clients should use
`errors` as the source of field-specific messages and treat `detail`, when present,
as general explanatory text.

The frontend `ErrorDisplay` component renders `errors` inline, by field, and displays
additional diagnostic fields, including `traceId`, in an expandable support-details
section. Users can provide the trace ID when reporting an issue so support can correlate
it with server logs.

## Request Flow

```mermaid
flowchart TD
    A[HTTP request] --> B[Model binding]
    B --> C{Bound complex DTO?}
    C -- yes --> D{FluentValidation<br/>AutoValidation}
    C -- scalar params --> E{DataAnnotations<br/>via ApiController}
    D -- invalid --> F[400 problem+json<br/>title + errors + traceId]
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
    [FromQuery, Range(1, int.MaxValue)] int? pageNumber = null)
```

### ...a rule spanning multiple parameters

FluentValidation cannot compare separate scalar arguments. Bind the parameters into a
single query/request model and write an `AbstractValidator<T>` for it, so cross-field
rules (e.g. `fromDate <= toDate`) can be expressed across its properties.

### ...something that isn't model-bound

Throw `ValidationException` for an imperative failure, optionally setting `Errors`
for grouped field messages. The `CustomExceptionHandler` returns `400` with
`HttpValidationProblemDetails`.

## Guidance Summary

- Request body? → FluentValidation `AbstractValidator<T>` (auto-registered).
- Independent scalar constraint? → DataAnnotations attribute on the parameter.
- Cross-parameter rule? → wrap parameters in a model, validate with FluentValidation.
- Non-bound / imperative failure? → throw `ValidationException`, optionally with grouped errors.
- Any `400` validation response? → return `ValidationProblemDetails` with `errors` and `traceId`.
- New open-generic validator? → add an explicit `AddTransient` registration per closed type.
