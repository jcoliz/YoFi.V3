# HOWTO: ASP.NET Core Exception Handling with IExceptionHandler

---
status: Reference Guide
created_date: 2026-03-25
source_project: YoFi.V3
---

## Overview

This guide documents a production-grade exception handling pattern for ASP.NET Core applications using the modern `IExceptionHandler` interface. It provides centralized exception-to-HTTP mapping with RFC 7807 ProblemDetails, comprehensive logging, and extensibility through custom exception hierarchies.

### Key Benefits

- ✅ **Centralized error handling** - Single point for exception-to-HTTP mapping
- ✅ **Thin controllers** - No try/catch blocks, business logic throws domain exceptions
- ✅ **Consistent API responses** - RFC 7807 ProblemDetails with trace IDs
- ✅ **Clean Architecture** - Features throw domain exceptions, middleware handles HTTP concerns
- ✅ **Type-safe** - Pattern matching maps exception types to status codes
- ✅ **Extensible** - Easy to add new exception types and handlers
- ✅ **Observable** - Automatic logging with full context and trace IDs

### When to Use This Pattern

**Use this pattern when:**
- Building REST APIs with Clean Architecture principles
- Need consistent error responses across all endpoints
- Want thin controllers that delegate business logic to features
- Require centralized logging of unhandled exceptions
- Following domain-driven design with typed exceptions

**Do NOT use when:**
- Building minimal APIs without complex exception hierarchies
- Exception handling needs to vary significantly per endpoint
- Using legacy code with existing try/catch patterns throughout

## Architecture Overview

```
┌─────────────────────────────────────────────────────────┐
│                     HTTP Request                        │
└────────────────────┬────────────────────────────────────┘
                     │
                     ▼
     ┌───────────────────────────────┐
     │   Authentication Middleware   │
     │   Authorization Middleware    │
     │   Custom Business Middleware  │
     └───────────────┬───────────────┘
                     │
                     ▼
           ┌─────────────────┐
           │   Controller    │ ◄── Thin, no try/catch
           └────────┬────────┘
                    │
                    ▼
           ┌─────────────────┐
           │ Application     │ ◄── Throws domain exceptions
           │ Feature         │
           └────────┬────────┘
                    │ throws exception
                    ▼
     ┌──────────────────────────────┐
     │   IExceptionHandler          │ ◄── Catches & maps to HTTP
     │   (CustomExceptionHandler)   │
     └────────┬─────────────────────┘
              │
              ▼
     ┌──────────────────────────┐
     │  ProblemDetails Response │
     │  + Logging               │
     └──────────────────────────┘
```

### Exception Flow

1. **Controller** receives request, calls Feature (no error handling)
2. **Feature** executes business logic, throws domain exception on error
3. **IExceptionHandler** catches exception, maps to HTTP status code
4. **ProblemDetails** response generated with trace ID and metadata
5. **Logging** occurs automatically with full exception context

## Step-by-Step Implementation

### Step 1: Create Exception Hierarchy

Create typed domain exceptions in your Entities/Domain layer. Use an inheritance hierarchy to group related exceptions.

**Directory structure:**
```
src/
├── Entities/
│   └── Exceptions/
│       ├── ResourceNotFoundException.cs      # Base for 404 errors
│       ├── ValidationException.cs            # Base for 400 errors
│       ├── TransactionNotFoundException.cs   # Specific 404
│       └── PayeeRuleNotFoundException.cs     # Specific 404
```

#### Base Exception: ResourceNotFoundException

Abstract base class for all "resource not found" scenarios (maps to 404):

```csharp
namespace YourApp.Entities.Exceptions;

/// <summary>
/// Base exception for when a requested resource cannot be found.
/// Automatically maps to HTTP 404 in the API pipeline.
/// </summary>
public abstract class ResourceNotFoundException : Exception
{
    /// <summary>
    /// Gets the type of resource that was not found (e.g., "Transaction", "Product").
    /// </summary>
    public abstract string ResourceType { get; }

    /// <summary>
    /// Gets the unique key of the resource that was not found.
    /// </summary>
    public Guid ResourceKey { get; }

    /// <summary>
    /// Initializes a new instance with the resource key.
    /// Generates a default message using the ResourceType.
    /// </summary>
    protected ResourceNotFoundException(Guid key)
        : base(string.Empty) // Will be overridden by Message property
    {
        ResourceKey = key;
    }

    /// <summary>
    /// Initializes a new instance with a custom message.
    /// </summary>
    protected ResourceNotFoundException(Guid key, string message)
        : base(message)
    {
        ResourceKey = key;
    }

    /// <summary>
    /// Gets the exception message. Generates a default message if one wasn't provided.
    /// </summary>
    public override string Message =>
        string.IsNullOrEmpty(base.Message)
            ? $"{ResourceType} with key '{ResourceKey}' was not found."
            : base.Message;
}
```

#### Specific Exception: TransactionNotFoundException

Concrete exception for a specific resource type:

```csharp
namespace YourApp.Entities.Exceptions;

/// <summary>
/// Exception thrown when a requested transaction cannot be found.
/// </summary>
public class TransactionNotFoundException : ResourceNotFoundException
{
    /// <inheritdoc/>
    public override string ResourceType => "Transaction";

    /// <summary>
    /// Gets the unique key of the transaction that was not found.
    /// </summary>
    public Guid TransactionKey => ResourceKey;

    /// <summary>
    /// Initializes a new instance with the transaction key.
    /// </summary>
    public TransactionNotFoundException(Guid key)
        : base(key)
    {
    }

    /// <summary>
    /// Initializes a new instance with a custom message.
    /// </summary>
    public TransactionNotFoundException(Guid key, string message)
        : base(key, message)
    {
    }
}
```

#### ValidationException

For input validation errors (maps to 400):

```csharp
namespace YourApp.Entities.Exceptions;

/// <summary>
/// Exception thrown when input validation fails.
/// Automatically maps to HTTP 400 Bad Request in the API pipeline.
/// </summary>
/// <remarks>
/// Use this exception for controller-level validation errors such as invalid file formats,
/// file size limits, or missing required parameters. For model validation (FluentValidation),
/// use the built-in validation pipeline instead.
/// </remarks>
public class ValidationException : Exception
{
    /// <summary>
    /// Gets the name of the parameter or field that failed validation.
    /// </summary>
    public string? ParameterName { get; }

    /// <summary>
    /// Initializes a new instance with an error message.
    /// </summary>
    public ValidationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance with a parameter name.
    /// </summary>
    public ValidationException(string parameterName, string message)
        : base(message)
    {
        ParameterName = parameterName;
    }
}
```

### Step 2: Create IExceptionHandler Implementation

Create a custom exception handler that implements `IExceptionHandler` and maps your domain exceptions to HTTP status codes.

**Location:** `src/Controllers/Middleware/CustomExceptionHandler.cs`

```csharp
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using YourApp.Entities.Exceptions;

namespace YourApp.Controllers.Middleware;

/// <summary>
/// Configurable exception handler that maps specific exception types to HTTP status codes and problem details.
/// </summary>
/// <param name="logger">Logger for diagnostic output.</param>
/// <remarks>
/// This handler processes application-specific exceptions and converts them into appropriate
/// HTTP responses with ProblemDetails. It handles:
/// <list type="bullet">
/// <item><description>ResourceNotFoundException → 404 Not Found</description></item>
/// <item><description>KeyNotFoundException → 404 Not Found (legacy)</description></item>
/// <item><description>ValidationException → 400 Bad Request</description></item>
/// </list>
/// Unhandled exceptions are passed to the next exception handler in the pipeline.
/// </remarks>
public partial class CustomExceptionHandler(ILogger<CustomExceptionHandler> logger) : IExceptionHandler
{
    /// <summary>
    /// Attempts to handle the exception by mapping it to an appropriate HTTP response.
    /// </summary>
    /// <param name="httpContext">The HTTP context for the current request.</param>
    /// <param name="exception">The exception to handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the exception was handled; false to pass to the next handler.</returns>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Try each registered exception handler using pattern matching
        var handled = exception switch
        {
            // 404 Not Found - ResourceNotFoundException and derived types
            ResourceNotFoundException notFound => await HandleResourceNotFoundAsync(
                httpContext, notFound, cancellationToken),

            // 404 Not Found - KeyNotFoundException (legacy, deprecated)
            KeyNotFoundException keyNotFound => await HandleKeyNotFoundExceptionAsync(
                httpContext, keyNotFound, cancellationToken),

            // 400 Bad Request - ValidationException (input validation errors)
            ValidationException validationException => await HandleValidationExceptionAsync(
                httpContext, validationException, cancellationToken),

            // If no match, let other handlers process it (returns false)
            _ => false
        };

        if (handled)
        {
            LogHandledException(exception, exception.GetType().Name, httpContext.Response.StatusCode);
        }

        return handled;
    }

    /// <summary>
    /// Creates a ProblemDetails object with trace ID and common fields populated.
    /// </summary>
    private static ProblemDetails CreateProblemDetails(
        HttpContext httpContext,
        int statusCode,
        string title,
        string detail)
    {
        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        // Use Activity.Current?.Id for W3C trace context format
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        problemDetails.Extensions["traceId"] = traceId;

        return problemDetails;
    }

    /// <summary>
    /// Handles ResourceNotFoundException and its derived types.
    /// Returns HTTP 404 with problem details.
    /// </summary>
    private async ValueTask<bool> HandleResourceNotFoundAsync(
        HttpContext httpContext,
        ResourceNotFoundException exception,
        CancellationToken cancellationToken)
    {
        httpContext.Response.StatusCode = StatusCodes.Status404NotFound;

        var problemDetails = CreateProblemDetails(
            httpContext,
            StatusCodes.Status404NotFound,
            $"{exception.ResourceType} not found",
            exception.Message);

        // Add exception-specific metadata to extensions
        problemDetails.Extensions["resourceType"] = exception.ResourceType;
        problemDetails.Extensions["resourceKey"] = exception.ResourceKey;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }

    /// <summary>
    /// Handles KeyNotFoundException (legacy support).
    /// Returns HTTP 404 with problem details.
    /// </summary>
    private async ValueTask<bool> HandleKeyNotFoundExceptionAsync(
        HttpContext httpContext,
        KeyNotFoundException exception,
        CancellationToken cancellationToken)
    {
        httpContext.Response.StatusCode = StatusCodes.Status404NotFound;

        var problemDetails = CreateProblemDetails(
            httpContext,
            StatusCodes.Status404NotFound,
            "Resource not found",
            exception.Message);

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }

    /// <summary>
    /// Handles ValidationException (input validation errors).
    /// Returns HTTP 400 with ValidationProblemDetails.
    /// </summary>
    private async ValueTask<bool> HandleValidationExceptionAsync(
        HttpContext httpContext,
        ValidationException exception,
        CancellationToken cancellationToken)
    {
        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        // Create errors dictionary for ValidationProblemDetails
        var errors = new Dictionary<string, string[]>();
        if (!string.IsNullOrEmpty(exception.ParameterName))
        {
            errors[exception.ParameterName] = [exception.Message];
        }
        else
        {
            // If no parameter name provided, use a generic key
            errors["validation"] = [exception.Message];
        }

        var problemDetails = new ValidationProblemDetails(errors)
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Title = "The request failed validation.",
            Status = StatusCodes.Status400BadRequest,
            Instance = httpContext.Request.Path,
            Detail = exception.Message
        };

        // Add W3C trace context ID for diagnostics
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        problemDetails.Extensions["traceId"] = traceId;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }

    [LoggerMessage(1, LogLevel.Information, "{Location}: Handled {ExceptionType} with status code {StatusCode}")]
    private partial void LogHandledException(Exception ex, string exceptionType, int statusCode, [CallerMemberName] string? location = null);
}
```

### Step 3: Register Exception Handler

Register the custom exception handler in your dependency injection container.

**Location:** `src/Controllers/Extensions/ServiceCollectionExtensions.cs`

```csharp
using Microsoft.Extensions.DependencyInjection;
using YourApp.Controllers.Middleware;

namespace YourApp.Controllers.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddControllerServices(this IServiceCollection services)
    {
        // Register custom exception handler for application-specific exceptions
        services.AddExceptionHandler<CustomExceptionHandler>();

        // Add ProblemDetails services (required for RFC 7807 support)
        services.AddProblemDetails();

        // ... other controller services

        return services;
    }
}
```

**In Program.cs:**

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails(); // Required for ProblemDetails
builder.Services.AddControllerServices(); // Registers CustomExceptionHandler

var app = builder.Build();

// Exception handler must come BEFORE middleware that might throw exceptions
app.UseExceptionHandler();

// Other middleware...
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();
```

### Step 4: Configure Middleware Pipeline

The order of middleware registration is **critical**. Exception handler must be registered early to catch exceptions from downstream middleware.

**Correct middleware order:**

```csharp
public static WebApplication ConfigureMiddlewarePipeline(this WebApplication app)
{
    // 1. Production-specific middleware (HSTS, HTTPS redirection)
    if (app.Environment.IsProduction())
    {
        app.UseHsts();
        app.UseHttpsRedirection();
    }

    // 2. Swagger/OpenAPI (development)
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    // 3. CORS (must come before auth/routing)
    app.UseCors();

    // 4. Exception handler (BEFORE middleware that might throw)
    app.UseExceptionHandler();

    // 5. Status code pages (handles routing failures)
    app.UseStatusCodePages();

    // 6. Authentication and Authorization
    app.UseAuthentication();
    app.UseAuthorization();

    // 7. Custom business middleware (e.g., tenancy, correlation)
    app.UseCustomMiddleware();

    // 8. Endpoints
    app.MapControllers();

    return app;
}
```

**Key principle:** `UseExceptionHandler()` must come BEFORE any middleware that might throw exceptions you want to handle.

### Step 5: Throw Exceptions from Features

In your Application layer (Features), throw domain exceptions instead of returning error results. Do NOT catch exceptions - let them propagate to the exception handler.

**Example Feature:**

```csharp
namespace YourApp.Application.Features;

public class TransactionFeature(ITransactionRepository repository)
{
    public async Task<TransactionResultDto> GetTransactionByIdAsync(Guid transactionId)
    {
        // Query for the transaction
        var transaction = await repository.GetByIdAsync(transactionId);

        // Throw domain exception if not found - NO try/catch needed
        if (transaction == null)
        {
            throw new TransactionNotFoundException(transactionId);
        }

        // Map to DTO and return
        return MapToDto(transaction);
    }

    public async Task<TransactionResultDto> UpdateTransactionAsync(
        Guid transactionId,
        TransactionEditDto dto)
    {
        // Validate input - throw ValidationException for business rule violations
        if (dto.Amount == 0)
        {
            throw new ValidationException(nameof(dto.Amount), "Amount cannot be zero.");
        }

        var transaction = await repository.GetByIdAsync(transactionId);
        if (transaction == null)
        {
            throw new TransactionNotFoundException(transactionId);
        }

        // Update and save
        transaction.Payee = dto.Payee;
        transaction.Amount = dto.Amount;
        await repository.SaveChangesAsync();

        return MapToDto(transaction);
    }
}
```

### Step 6: Keep Controllers Thin

Controllers should have NO try/catch blocks. They simply call Features and return results. The exception handler middleware will catch any exceptions.

**Example Controller:**

```csharp
using Microsoft.AspNetCore.Mvc;
using YourApp.Application.Features;

namespace YourApp.Controllers;

[ApiController]
[Route("api/transactions")]
public class TransactionsController(TransactionFeature feature) : ControllerBase
{
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TransactionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        // No try/catch - exceptions propagate to CustomExceptionHandler
        var result = await feature.GetTransactionByIdAsync(id);
        return Ok(result);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(TransactionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] TransactionEditDto dto)
    {
        // No try/catch - exceptions propagate to CustomExceptionHandler
        var result = await feature.UpdateTransactionAsync(id, dto);
        return Ok(result);
    }
}
```

**Note:** Always add `[ProducesResponseType]` attributes for error responses to ensure proper OpenAPI/Swagger documentation and TypeScript client generation.

## Advanced Patterns

### Pattern 1: Domain-Specific Exception Handlers

For complex domains (like multi-tenancy), create separate exception hierarchies and handlers.

#### Separate Exception Hierarchy

```csharp
namespace YourApp.Entities.Tenancy.Exceptions;

/// <summary>
/// Base exception for all tenancy-related errors.
/// </summary>
public abstract class TenancyException : Exception
{
    protected TenancyException(string message) : base(message) { }
}

/// <summary>
/// Base exception for tenancy access denied scenarios.
/// Maps to HTTP 403 Forbidden.
/// </summary>
public abstract class TenancyAccessDeniedException : TenancyException
{
    protected TenancyAccessDeniedException(string message) : base(message) { }
}

/// <summary>
/// Exception thrown when a tenant cannot be found.
/// Returns 403 (not 404) to prevent tenant enumeration attacks.
/// </summary>
public class TenantNotFoundException : TenancyAccessDeniedException
{
    public Guid TenantKey { get; }

    public TenantNotFoundException(Guid key)
        : base($"Access to tenant '{key}' is denied.")
    {
        TenantKey = key;
    }
}

/// <summary>
/// Exception thrown when a duplicate tenant role assignment is attempted.
/// Maps to HTTP 409 Conflict.
/// </summary>
public class DuplicateUserTenantRoleException : TenancyException
{
    public string UserId { get; }
    public Guid TenantKey { get; }

    public DuplicateUserTenantRoleException(string userId, Guid tenantKey)
        : base($"User '{userId}' already has a role for tenant '{tenantKey}'.")
    {
        UserId = userId;
        TenantKey = tenantKey;
    }
}
```

#### Dedicated Tenancy Handler

Create a static helper class for domain-specific exception mapping:

```csharp
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using YourApp.Entities.Tenancy.Exceptions;

namespace YourApp.Controllers.Tenancy.Exceptions;

/// <summary>
/// Handles TenancyException and its derived types, mapping them to appropriate HTTP responses.
/// </summary>
public static class TenancyExceptionHandler
{
    /// <summary>
    /// Handles a TenancyException by writing an appropriate ProblemDetails response.
    /// </summary>
    public static async Task HandleAsync(
        HttpContext httpContext,
        TenancyException exception,
        CancellationToken cancellationToken = default)
    {
        // Map exception type to HTTP status code and title
        var (statusCode, title) = MapExceptionToResponse(exception);

        httpContext.Response.StatusCode = statusCode;

        var problemDetails = CreateProblemDetails(httpContext, statusCode, title, exception.Message);

        // Add type-specific extensions
        AddExceptionExtensions(problemDetails, exception);

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
    }

    private static ProblemDetails CreateProblemDetails(
        HttpContext httpContext,
        int statusCode,
        string title,
        string detail)
    {
        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        problemDetails.Extensions["traceId"] = traceId;

        return problemDetails;
    }

    /// <summary>
    /// Maps exception types to HTTP status codes and titles.
    /// </summary>
    private static (int StatusCode, string Title) MapExceptionToResponse(TenancyException exception)
    {
        return exception switch
        {
            // 403 Forbidden - Access denied exceptions
            TenancyAccessDeniedException => (StatusCodes.Status403Forbidden, "Access denied"),

            // 409 Conflict - Duplicate resource exceptions
            DuplicateUserTenantRoleException => (StatusCodes.Status409Conflict, "Duplicate user tenant role"),

            // Default - treat unknown tenancy exceptions as 500
            _ => (StatusCodes.Status500InternalServerError, "Tenancy error")
        };
    }

    /// <summary>
    /// Adds exception-specific data to the problem details extensions.
    /// </summary>
    private static void AddExceptionExtensions(ProblemDetails problemDetails, TenancyException exception)
    {
        switch (exception)
        {
            case TenantNotFoundException tenantNotFound:
                problemDetails.Extensions["tenantKey"] = tenantNotFound.TenantKey;
                break;

            case DuplicateUserTenantRoleException duplicate:
                problemDetails.Extensions["userId"] = duplicate.UserId;
                problemDetails.Extensions["tenantKey"] = duplicate.TenantKey;
                break;
        }
    }
}
```

#### Delegate to Domain Handler

In your main `CustomExceptionHandler`, delegate to the domain-specific handler:

```csharp
public async ValueTask<bool> TryHandleAsync(
    HttpContext httpContext,
    Exception exception,
    CancellationToken cancellationToken)
{
    var handled = exception switch
    {
        // Tenancy exceptions - delegate to tenancy handler
        TenancyException tenancyException => await HandleTenancyExceptionAsync(
            httpContext, tenancyException, cancellationToken),

        // Other exceptions...
        ResourceNotFoundException notFound => await HandleResourceNotFoundAsync(
            httpContext, notFound, cancellationToken),

        _ => false
    };

    if (handled)
    {
        LogHandledException(exception, exception.GetType().Name, httpContext.Response.StatusCode);
    }

    return handled;
}

private static async ValueTask<bool> HandleTenancyExceptionAsync(
    HttpContext httpContext,
    TenancyException exception,
    CancellationToken cancellationToken)
{
    await TenancyExceptionHandler.HandleAsync(httpContext, exception, cancellationToken);
    return true;
}
```

### Pattern 2: FluentValidation Integration

For DTO validation at the controller boundary, integrate FluentValidation with automatic error responses.

#### Register FluentValidation

```csharp
using FluentValidation;
using FluentValidation.AspNetCore;

public static IServiceCollection AddControllerServices(this IServiceCollection services)
{
    services.AddExceptionHandler<CustomExceptionHandler>();
    services.AddProblemDetails();

    // Register FluentValidation validators from Application assembly
    services.AddValidatorsFromAssemblyContaining<TransactionEditDtoValidator>();

    // Add FluentValidation to ASP.NET Core model binding pipeline
    // Invalid DTOs return 400 Bad Request BEFORE controller actions execute
    services.AddFluentValidationAutoValidation();

    // Customize validation error response format
    services.Configure<ApiBehaviorOptions>(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var problemDetails = new ValidationProblemDetails(context.ModelState)
            {
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                Title = "One or more validation errors occurred.",
                Status = StatusCodes.Status400BadRequest,
                Instance = context.HttpContext.Request.Path
            };

            // Combine all error messages into a single "detail" field
            var errorMessages = problemDetails.Errors
                .SelectMany(kvp => kvp.Value.Select(msg => $"{kvp.Key}: {msg}"))
                .ToList();

            if (errorMessages.Count > 0)
            {
                problemDetails.Detail = string.Join("; ", errorMessages);
            }

            // Add trace ID for diagnostics
            problemDetails.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;

            return new BadRequestObjectResult(problemDetails)
            {
                ContentTypes = { "application/problem+json" }
            };
        };
    });

    return services;
}
```

#### Example FluentValidation Validator

```csharp
using FluentValidation;
using YourApp.Application.Dto;

namespace YourApp.Application.Validation;

public class TransactionEditDtoValidator : AbstractValidator<TransactionEditDto>
{
    public TransactionEditDtoValidator()
    {
        RuleFor(x => x.Payee)
            .NotEmpty()
            .WithMessage("Payee is required.")
            .MaximumLength(200)
            .WithMessage("Payee must not exceed 200 characters.");

        RuleFor(x => x.Amount)
            .NotEqual(0)
            .WithMessage("Amount cannot be zero.");

        RuleFor(x => x.Date)
            .NotEmpty()
            .WithMessage("Date is required.")
            .LessThanOrEqualTo(DateTime.UtcNow.Date)
            .WithMessage("Date cannot be in the future.");
    }
}
```

**Result:** Invalid DTOs are automatically rejected at the controller boundary with ValidationProblemDetails before the controller action executes.

### Pattern 3: Security-Conscious Exception Responses

For security-sensitive scenarios like tenant access, carefully control what information is exposed.

#### Anti-Pattern: Exposing Tenant Existence

```csharp
// ❌ BAD: 404 for "not found" and 403 for "access denied" allows enumeration
if (tenant == null)
{
    throw new TenantNotFoundException(tenantId); // Maps to 404
}

if (!user.HasAccessToTenant(tenantId))
{
    throw new TenantAccessDeniedException(tenantId); // Maps to 403
}
```

**Problem:** An attacker can enumerate valid tenant IDs by observing different status codes:
- 404 → Tenant doesn't exist
- 403 → Tenant exists, but I don't have access

#### Correct Pattern: Indistinguishable Responses

```csharp
// ✅ GOOD: Both scenarios return 403 with identical message
if (tenant == null)
{
    // Returns 403, NOT 404 - prevents enumeration
    throw new TenantNotFoundException(tenantId);
}

if (!user.HasAccessToTenant(tenantId))
{
    throw new TenantAccessDeniedException(tenantId); // Also returns 403
}
```

**Map both to 403 in handler:**

```csharp
private static (int StatusCode, string Title) MapExceptionToResponse(TenancyException exception)
{
    return exception switch
    {
        // Both return 403 Forbidden - indistinguishable to attackers
        TenantNotFoundException => (StatusCodes.Status403Forbidden, "Access denied"),
        TenantAccessDeniedException => (StatusCodes.Status403Forbidden, "Access denied"),

        _ => (StatusCodes.Status500InternalServerError, "Tenancy error")
    };
}
```

**Security benefit:** Attackers cannot determine if a tenant ID is valid by observing status codes.

### Pattern 4: ProblemDetails Extensions for Debugging

Add exception-specific metadata to ProblemDetails extensions for debugging, while keeping the main error message generic.

```csharp
private async ValueTask<bool> HandleResourceNotFoundAsync(
    HttpContext httpContext,
    ResourceNotFoundException exception,
    CancellationToken cancellationToken)
{
    httpContext.Response.StatusCode = StatusCodes.Status404NotFound;

    var problemDetails = CreateProblemDetails(
        httpContext,
        StatusCodes.Status404NotFound,
        $"{exception.ResourceType} not found",
        exception.Message);

    // Add structured metadata for debugging and monitoring
    problemDetails.Extensions["resourceType"] = exception.ResourceType;
    problemDetails.Extensions["resourceKey"] = exception.ResourceKey;

    // If the exception has additional context, include it
    if (exception.Data.Count > 0)
    {
        problemDetails.Extensions["exceptionData"] = exception.Data;
    }

    await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
    return true;
}
```

**Benefits:**
- Generic error message for end users
- Structured metadata for support engineers
- Trace IDs for log correlation
- Type-safe exception properties

## Testing Exception Handling

### Unit Test: Verify Exceptions Are Thrown

Test that your Features throw the correct exceptions:

```csharp
using NUnit.Framework;
using YourApp.Application.Features;
using YourApp.Entities.Exceptions;

[TestFixture]
public class TransactionFeatureTests
{
    [Test]
    public void GetTransactionByIdAsync_NotFound_ThrowsTransactionNotFoundException()
    {
        // Given: A feature with an empty repository
        var repository = new MockTransactionRepository();
        var feature = new TransactionFeature(repository);
        var nonExistentId = Guid.NewGuid();

        // When/Then: Feature should throw TransactionNotFoundException
        var exception = Assert.ThrowsAsync<TransactionNotFoundException>(
            async () => await feature.GetTransactionByIdAsync(nonExistentId));

        // And: Exception should contain the requested key
        Assert.That(exception!.TransactionKey, Is.EqualTo(nonExistentId));
    }

    [Test]
    public void UpdateTransactionAsync_InvalidAmount_ThrowsValidationException()
    {
        // Given: A feature and a DTO with invalid amount
        var feature = new TransactionFeature(new MockTransactionRepository());
        var dto = new TransactionEditDto { Amount = 0, Payee = "Test" };

        // When/Then: Feature should throw ValidationException
        var exception = Assert.ThrowsAsync<ValidationException>(
            async () => await feature.UpdateTransactionAsync(Guid.NewGuid(), dto));

        // And: Exception should specify the parameter name
        Assert.That(exception!.ParameterName, Is.EqualTo("Amount"));
    }
}
```

### Integration Test: Verify HTTP Status Codes

Test that exceptions are correctly mapped to HTTP responses:

```csharp
using Microsoft.AspNetCore.Mvc.Testing;
using NUnit.Framework;
using System.Net;

[TestFixture]
public class TransactionsControllerTests
{
    private HttpClient _client = null!;
    private WebApplicationFactory<Program> _factory = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();
    }

    [Test]
    public async Task GetById_NotFound_Returns404WithProblemDetails()
    {
        // Given: A non-existent transaction ID
        var nonExistentId = Guid.NewGuid();

        // When: Request is made for non-existent transaction
        var response = await _client.GetAsync($"/api/transactions/{nonExistentId}");

        // Then: 404 Not Found should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        // And: Response should contain problem details
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problemDetails, Is.Not.Null);
        Assert.That(problemDetails!.Status, Is.EqualTo(404));
        Assert.That(problemDetails.Title, Does.Contain("Transaction not found"));

        // And: Should include trace ID for correlation
        Assert.That(problemDetails.Extensions.ContainsKey("traceId"), Is.True);

        // And: Should include resource metadata
        Assert.That(problemDetails.Extensions["resourceType"], Is.EqualTo("Transaction"));
        Assert.That(problemDetails.Extensions["resourceKey"], Is.EqualTo(nonExistentId));
    }

    [Test]
    public async Task Update_InvalidAmount_Returns400WithValidationProblemDetails()
    {
        // Given: A transaction update with invalid amount
        var transactionId = Guid.NewGuid();
        var invalidDto = new { Amount = 0, Payee = "Test" };

        // When: Update request is made with invalid data
        var response = await _client.PutAsJsonAsync(
            $"/api/transactions/{transactionId}",
            invalidDto);

        // Then: 400 Bad Request should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

        // And: Response should contain validation problem details
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problemDetails, Is.Not.Null);
        Assert.That(problemDetails!.Status, Is.EqualTo(400));
        Assert.That(problemDetails.Detail, Does.Contain("Amount"));
    }

    [TearDown]
    public void TearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }
}
```

## Exception-to-HTTP Mapping Guide

| Exception Type | HTTP Status | ProblemDetails Type | Use Case |
|---------------|-------------|---------------------|----------|
| `ResourceNotFoundException` | 404 Not Found | `ProblemDetails` | Resource doesn't exist |
| `ValidationException` | 400 Bad Request | `ValidationProblemDetails` | Business rule violation |
| `InvalidOperationException` | 400 Bad Request | `ProblemDetails` | Invalid state transition |
| `UnauthorizedAccessException` | 403 Forbidden | `ProblemDetails` | Permission denied |
| `ArgumentException` | 400 Bad Request | `ValidationProblemDetails` | Invalid argument |
| `TenantNotFoundException` | 403 Forbidden | `ProblemDetails` | Security: prevent enumeration |
| `DuplicateResourceException` | 409 Conflict | `ProblemDetails` | Resource already exists |
| `KeyNotFoundException` | 404 Not Found | `ProblemDetails` | Legacy .NET exception |

### When to Use Each Status Code

**400 Bad Request:**
- Client sent invalid data (validation failed)
- Required parameters missing
- Data format incorrect
- Business rule violation

**404 Not Found:**
- Requested resource doesn't exist
- Route exists but resource ID is invalid

**403 Forbidden:**
- User authenticated but lacks permission
- Security-sensitive: resource existence should not be revealed
- Access denied scenarios

**409 Conflict:**
- Resource already exists (duplicate)
- Concurrent modification conflict
- State conflict (e.g., can't delete resource with dependencies)

**500 Internal Server Error:**
- Unhandled exceptions
- Configuration errors (e.g., TenantContextNotSetException)
- Database connection failures
- Unexpected application errors

## Best Practices

### 1. Exception Handler Must Be Partial Class

To use `[LoggerMessage]` attribute, the class must be partial:

```csharp
public partial class CustomExceptionHandler(ILogger<CustomExceptionHandler> logger) : IExceptionHandler
{
    [LoggerMessage(1, LogLevel.Information, "{Location}: Handled {ExceptionType} with status code {StatusCode}")]
    private partial void LogHandledException(Exception ex, string exceptionType, int statusCode, [CallerMemberName] string? location = null);
}
```

### 2. Always Include Trace IDs

Include W3C trace context ID in all ProblemDetails responses for log correlation:

```csharp
var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
problemDetails.Extensions["traceId"] = traceId;
```

**Why:** Allows users to provide trace ID to support team, who can then query logs for full exception context.

### 3. Use Pattern Matching for Clarity

Pattern matching makes exception handling logic clear and maintainable:

```csharp
var handled = exception switch
{
    ResourceNotFoundException notFound => await HandleResourceNotFoundAsync(...),
    ValidationException validation => await HandleValidationExceptionAsync(...),
    _ => false // Pass to next handler
};
```

### 4. Return False for Unhandled Exceptions

If your handler doesn't recognize an exception, return `false` to pass it to the next handler:

```csharp
_ => false // Let other handlers or default handler process it
```

Built-in ASP.NET Core exception handler will log unhandled exceptions and return a generic 500 error.

### 5. Add ProducesResponseType Attributes

Always add `[ProducesResponseType]` attributes for error responses:

```csharp
[HttpGet("{id}")]
[ProducesResponseType(typeof(TransactionResultDto), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public async Task<IActionResult> GetById(Guid id)
{
    var result = await feature.GetTransactionByIdAsync(id);
    return Ok(result);
}
```

**Why:**
- Generates correct OpenAPI/Swagger documentation
- TypeScript client generators produce typed error handling
- Documents expected error responses for API consumers

### 6. Log at Information Level

Log handled exceptions at Information level (not Warning or Error):

```csharp
[LoggerMessage(1, LogLevel.Information, "{Location}: Handled {ExceptionType} with status code {StatusCode}")]
private partial void LogHandledException(Exception ex, string exceptionType, int statusCode, [CallerMemberName] string? location = null);
```

**Why:** These are expected application exceptions (404, 400) that represent normal error cases, not system failures. Warning/Error levels should be reserved for truly unexpected issues.

### 7. Never Swallow Exceptions in Features

Features should throw exceptions and let them propagate - do NOT catch and return null/default:

```csharp
// ❌ BAD: Swallows exception, returns null, forces controller to check for null
public async Task<TransactionResultDto?> GetTransactionByIdAsync(Guid id)
{
    try
    {
        var transaction = await repository.GetByIdAsync(id);
        return transaction != null ? MapToDto(transaction) : null;
    }
    catch (Exception)
    {
        return null; // ❌ Lost exception context
    }
}

// ✅ GOOD: Throws typed exception, exception handler converts to HTTP response
public async Task<TransactionResultDto> GetTransactionByIdAsync(Guid id)
{
    var transaction = await repository.GetByIdAsync(id);
    if (transaction == null)
    {
        throw new TransactionNotFoundException(id); // ✅ Typed exception
    }
    return MapToDto(transaction);
}
```

### 8. One Exception Handler Per Layer/Domain

For large applications, create separate exception handlers for different domains:

- `CustomExceptionHandler` - Core application exceptions (404, 400, validation)
- `TenancyExceptionHandler` - Tenancy-specific exceptions (403, 409)
- `AuthenticationExceptionHandler` - Auth-specific exceptions (401)

Main handler delegates to domain handlers:

```csharp
var handled = exception switch
{
    TenancyException => await HandleTenancyExceptionAsync(...),
    AuthException => await HandleAuthExceptionAsync(...),
    ResourceNotFoundException => await HandleResourceNotFoundAsync(...),
    _ => false
};
```

### 9. Validate Early, Fail Fast

Perform validation as early as possible in the pipeline:

1. **Model binding validation** - FluentValidation at controller boundary (automatic)
2. **Business validation** - Throw ValidationException in Features
3. **Data validation** - Database constraints as last resort

```csharp
// Controller boundary: FluentValidation (automatic)
[HttpPost]
public async Task<IActionResult> Create([FromBody] TransactionEditDto dto)
{
    // dto is already validated by FluentValidation before method executes

    // Business validation: Feature throws ValidationException
    var result = await feature.CreateTransactionAsync(dto);
    return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
}

// Feature layer: Business rule validation
public async Task<TransactionResultDto> CreateTransactionAsync(TransactionEditDto dto)
{
    // Additional business rules not expressed in FluentValidation
    if (await IsDuplicateTransaction(dto))
    {
        throw new ValidationException("Duplicate transaction detected.");
    }

    // Proceed with creation...
}
```

### 10. XML Documentation for Exception Handlers

Document which exceptions are handled and what HTTP status they map to:

```csharp
/// <summary>
/// Configurable exception handler that maps specific exception types to HTTP status codes and problem details.
/// </summary>
/// <param name="logger">Logger for diagnostic output.</param>
/// <remarks>
/// This handler processes application-specific exceptions and converts them into appropriate
/// HTTP responses with ProblemDetails. It handles:
/// <list type="bullet">
/// <item><description>ResourceNotFoundException → 404 Not Found</description></item>
/// <item><description>ValidationException → 400 Bad Request</description></item>
/// <item><description>TenancyException (delegated to TenancyExceptionHandler)</description></item>
/// </list>
/// Unhandled exceptions are passed to the next exception handler in the pipeline.
/// </remarks>
public partial class CustomExceptionHandler(ILogger<CustomExceptionHandler> logger) : IExceptionHandler
```

## Complete Working Example

### Directory Structure

```
YourApp/
├── src/
│   ├── Entities/
│   │   └── Exceptions/
│   │       ├── ResourceNotFoundException.cs
│   │       ├── ValidationException.cs
│   │       ├── TransactionNotFoundException.cs
│   │       └── ProductNotFoundException.cs
│   │
│   ├── Application/
│   │   ├── Features/
│   │   │   └── TransactionFeature.cs
│   │   └── Validation/
│   │       └── TransactionEditDtoValidator.cs
│   │
│   └── Controllers/
│       ├── Middleware/
│       │   └── CustomExceptionHandler.cs
│       ├── Extensions/
│       │   └── ServiceCollectionExtensions.cs
│       └── TransactionsController.cs
```

### 1. Exception Classes

**ResourceNotFoundException.cs:**

```csharp
namespace YourApp.Entities.Exceptions;

public abstract class ResourceNotFoundException : Exception
{
    public abstract string ResourceType { get; }
    public Guid ResourceKey { get; }

    protected ResourceNotFoundException(Guid key) : base(string.Empty)
    {
        ResourceKey = key;
    }

    public override string Message =>
        string.IsNullOrEmpty(base.Message)
            ? $"{ResourceType} with key '{ResourceKey}' was not found."
            : base.Message;
}
```

**TransactionNotFoundException.cs:**

```csharp
namespace YourApp.Entities.Exceptions;

public class TransactionNotFoundException : ResourceNotFoundException
{
    public override string ResourceType => "Transaction";
    public Guid TransactionKey => ResourceKey;

    public TransactionNotFoundException(Guid key) : base(key) { }
}
```

**ValidationException.cs:**

```csharp
namespace YourApp.Entities.Exceptions;

public class ValidationException : Exception
{
    public string? ParameterName { get; }

    public ValidationException(string message) : base(message) { }

    public ValidationException(string parameterName, string message) : base(message)
    {
        ParameterName = parameterName;
    }
}
```

### 2. CustomExceptionHandler

**CustomExceptionHandler.cs:**

```csharp
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using YourApp.Entities.Exceptions;

namespace YourApp.Controllers.Middleware;

public partial class CustomExceptionHandler(ILogger<CustomExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var handled = exception switch
        {
            ResourceNotFoundException notFound => await HandleResourceNotFoundAsync(
                httpContext, notFound, cancellationToken),

            ValidationException validation => await HandleValidationExceptionAsync(
                httpContext, validation, cancellationToken),

            KeyNotFoundException keyNotFound => await HandleKeyNotFoundExceptionAsync(
                httpContext, keyNotFound, cancellationToken),

            _ => false
        };

        if (handled)
        {
            LogHandledException(exception, exception.GetType().Name, httpContext.Response.StatusCode);
        }

        return handled;
    }

    private static ProblemDetails CreateProblemDetails(
        HttpContext httpContext,
        int statusCode,
        string title,
        string detail)
    {
        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };

        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        problemDetails.Extensions["traceId"] = traceId;

        return problemDetails;
    }

    private async ValueTask<bool> HandleResourceNotFoundAsync(
        HttpContext httpContext,
        ResourceNotFoundException exception,
        CancellationToken cancellationToken)
    {
        httpContext.Response.StatusCode = StatusCodes.Status404NotFound;

        var problemDetails = CreateProblemDetails(
            httpContext,
            StatusCodes.Status404NotFound,
            $"{exception.ResourceType} not found",
            exception.Message);

        problemDetails.Extensions["resourceType"] = exception.ResourceType;
        problemDetails.Extensions["resourceKey"] = exception.ResourceKey;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }

    private async ValueTask<bool> HandleValidationExceptionAsync(
        HttpContext httpContext,
        ValidationException exception,
        CancellationToken cancellationToken)
    {
        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        var errors = new Dictionary<string, string[]>();
        if (!string.IsNullOrEmpty(exception.ParameterName))
        {
            errors[exception.ParameterName] = [exception.Message];
        }
        else
        {
            errors["validation"] = [exception.Message];
        }

        var problemDetails = new ValidationProblemDetails(errors)
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Title = "The request failed validation.",
            Status = StatusCodes.Status400BadRequest,
            Instance = httpContext.Request.Path,
            Detail = exception.Message
        };

        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        problemDetails.Extensions["traceId"] = traceId;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }

    private async ValueTask<bool> HandleKeyNotFoundExceptionAsync(
        HttpContext httpContext,
        KeyNotFoundException exception,
        CancellationToken cancellationToken)
    {
        httpContext.Response.StatusCode = StatusCodes.Status404NotFound;

        var problemDetails = CreateProblemDetails(
            httpContext,
            StatusCodes.Status404NotFound,
            "Resource not found",
            exception.Message);

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }

    [LoggerMessage(1, LogLevel.Information, "{Location}: Handled {ExceptionType} with status code {StatusCode}")]
    private partial void LogHandledException(Exception ex, string exceptionType, int statusCode, [CallerMemberName] string? location = null);
}
```

### 3. Service Registration

**ServiceCollectionExtensions.cs:**

```csharp
using Microsoft.Extensions.DependencyInjection;
using YourApp.Controllers.Middleware;

namespace YourApp.Controllers.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddControllerServices(this IServiceCollection services)
    {
        // Register custom exception handler
        services.AddExceptionHandler<CustomExceptionHandler>();

        // Register ProblemDetails services (required for RFC 7807)
        services.AddProblemDetails();

        return services;
    }
}
```

### 4. Program.cs

**Program.cs:**

```csharp
using YourApp.Controllers.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddControllerServices(); // Registers exception handler

var app = builder.Build();

// Exception handler must come BEFORE middleware that throws exceptions
app.UseExceptionHandler();

// Other middleware...
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();
```

### 5. Feature Implementation

**TransactionFeature.cs:**

```csharp
using YourApp.Entities.Exceptions;

namespace YourApp.Application.Features;

public class TransactionFeature(ITransactionRepository repository)
{
    public async Task<TransactionResultDto> GetByIdAsync(Guid id)
    {
        var transaction = await repository.GetByIdAsync(id);

        // Throw domain exception - NO try/catch
        if (transaction == null)
        {
            throw new TransactionNotFoundException(id);
        }

        return MapToDto(transaction);
    }

    public async Task<TransactionResultDto> UpdateAsync(Guid id, TransactionEditDto dto)
    {
        // Business validation
        if (dto.Amount == 0)
        {
            throw new ValidationException(nameof(dto.Amount), "Amount cannot be zero.");
        }

        var transaction = await repository.GetByIdAsync(id);
        if (transaction == null)
        {
            throw new TransactionNotFoundException(id);
        }

        // Update logic...
        transaction.Payee = dto.Payee;
        transaction.Amount = dto.Amount;

        await repository.SaveChangesAsync();
        return MapToDto(transaction);
    }
}
```

### 6. Controller Implementation

**TransactionsController.cs:**

```csharp
using Microsoft.AspNetCore.Mvc;
using YourApp.Application.Features;

namespace YourApp.Controllers;

[ApiController]
[Route("api/transactions")]
public class TransactionsController(TransactionFeature feature) : ControllerBase
{
    // No try/catch - exceptions propagate to CustomExceptionHandler
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(TransactionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await feature.GetByIdAsync(id);
        return Ok(result);
    }

    [HttpPut("{id}")]
    [ProducesResponseType(typeof(TransactionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] TransactionEditDto dto)
    {
        var result = await feature.UpdateAsync(id, dto);
        return Ok(result);
    }
}
```

## Example ProblemDetails Responses

### 404 Not Found - TransactionNotFoundException

**Request:**
```http
GET /api/transactions/12345678-1234-1234-1234-123456789abc
```

**Response:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Transaction not found",
  "status": 404,
  "detail": "Transaction with key '12345678-1234-1234-1234-123456789abc' was not found.",
  "instance": "/api/transactions/12345678-1234-1234-1234-123456789abc",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00",
  "resourceType": "Transaction",
  "resourceKey": "12345678-1234-1234-1234-123456789abc"
}
```

### 400 Bad Request - ValidationException

**Request:**
```http
PUT /api/transactions/12345678-1234-1234-1234-123456789abc
Content-Type: application/json

{
  "payee": "Test Store",
  "amount": 0,
  "date": "2026-03-25"
}
```

**Response:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "The request failed validation.",
  "status": 400,
  "detail": "Amount: Amount cannot be zero.",
  "instance": "/api/transactions/12345678-1234-1234-1234-123456789abc",
  "errors": {
    "Amount": ["Amount cannot be zero."]
  },
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00"
}
```

### 403 Forbidden - TenantNotFoundException

**Request:**
```http
GET /api/tenant/99999999-9999-9999-9999-999999999999/transactions
```

**Response:**
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.4",
  "title": "Access denied",
  "status": 403,
  "detail": "Access to tenant '99999999-9999-9999-9999-999999999999' is denied.",
  "instance": "/api/tenant/99999999-9999-9999-9999-999999999999/transactions",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00",
  "tenantKey": "99999999-9999-9999-9999-999999999999"
}
```

**Note:** Returns 403 (not 404) to prevent tenant enumeration attacks.

## Common Scenarios

### Scenario 1: Adding a New Exception Type

**Step 1:** Create the exception class:

```csharp
namespace YourApp.Entities.Exceptions;

public class DuplicateResourceException : Exception
{
    public string ResourceType { get; }
    public Guid ResourceKey { get; }

    public DuplicateResourceException(string resourceType, Guid key)
        : base($"{resourceType} with key '{key}' already exists.")
    {
        ResourceType = resourceType;
        ResourceKey = key;
    }
}
```

**Step 2:** Add handler method in `CustomExceptionHandler`:

```csharp
private async ValueTask<bool> HandleDuplicateResourceAsync(
    HttpContext httpContext,
    DuplicateResourceException exception,
    CancellationToken cancellationToken)
{
    httpContext.Response.StatusCode = StatusCodes.Status409Conflict;

    var problemDetails = CreateProblemDetails(
        httpContext,
        StatusCodes.Status409Conflict,
        "Resource already exists",
        exception.Message);

    problemDetails.Extensions["resourceType"] = exception.ResourceType;
    problemDetails.Extensions["resourceKey"] = exception.ResourceKey;

    await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
    return true;
}
```

**Step 3:** Add to pattern matching in `TryHandleAsync`:

```csharp
var handled = exception switch
{
    ResourceNotFoundException notFound => await HandleResourceNotFoundAsync(...),
    ValidationException validation => await HandleValidationExceptionAsync(...),
    DuplicateResourceException duplicate => await HandleDuplicateResourceAsync(...), // NEW
    _ => false
};
```

**Step 4:** Add `[ProducesResponseType]` to controllers:

```csharp
[HttpPost]
[ProducesResponseType(typeof(TransactionResultDto), StatusCodes.Status201Created)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
public async Task<IActionResult> Create([FromBody] TransactionEditDto dto)
{
    var result = await feature.CreateAsync(dto);
    return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
}
```

### Scenario 2: Handling External Library Exceptions

Wrap external library exceptions in your domain exceptions:

```csharp
public async Task<OFXParsingResult> ParseOFXFileAsync(Stream fileStream)
{
    try
    {
        // External library call
        var result = await externalLibrary.ParseAsync(fileStream);
        return MapToResult(result);
    }
    catch (ExternalLibraryException ex)
    {
        // Wrap in domain exception
        throw new ValidationException("file", "Invalid OFX file format.", ex);
    }
}
```

### Scenario 3: Multiple Exception Handlers

For large applications, chain multiple exception handlers:

```csharp
// Register multiple handlers - processed in order
services.AddExceptionHandler<TenancyExceptionHandler>(); // First
services.AddExceptionHandler<CustomExceptionHandler>(); // Second
services.AddExceptionHandler<DefaultExceptionHandler>(); // Last (catch-all)
```

Each handler returns `false` if it doesn't handle the exception, passing it to the next handler.

### Scenario 4: Logging Unhandled Exceptions

Create a catch-all exception handler for unhandled exceptions:

```csharp
public class DefaultExceptionHandler(ILogger<DefaultExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Log all unhandled exceptions with full context
        LogUnhandledException(
            exception,
            httpContext.Request.Method,
            httpContext.Request.Path,
            httpContext.TraceIdentifier);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An error occurred while processing your request.",
            Detail = "An unexpected error occurred. Please contact support with the trace ID.",
            Instance = httpContext.Request.Path
        };

        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        problemDetails.Extensions["traceId"] = traceId;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true; // Always handles
    }

    [LoggerMessage(1, LogLevel.Error, "{Location}: Unhandled exception {ExceptionType} for {Method} {Path} (TraceId: {TraceId})")]
    private partial void LogUnhandledException(
        Exception ex,
        string method,
        string path,
        string traceId,
        [CallerMemberName] string? location = null);
}
```

## Migration from Try/Catch Pattern

### Before: Controller with Try/Catch

```csharp
// ❌ OLD PATTERN: Try/catch everywhere
[HttpGet("{id}")]
public async Task<IActionResult> GetById(Guid id)
{
    try
    {
        var transaction = await repository.GetByIdAsync(id);
        if (transaction == null)
        {
            return NotFound(new { message = "Transaction not found" });
        }
        return Ok(transaction);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to get transaction");
        return StatusCode(500, new { message = "Internal server error" });
    }
}
```

### After: Controller with Exception Handler

```csharp
// ✅ NEW PATTERN: No try/catch, throw domain exceptions
[HttpGet("{id}")]
[ProducesResponseType(typeof(TransactionResultDto), StatusCodes.Status200OK)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
public async Task<IActionResult> GetById(Guid id)
{
    // Feature throws TransactionNotFoundException if not found
    // CustomExceptionHandler converts to 404 with ProblemDetails
    var result = await feature.GetByIdAsync(id);
    return Ok(result);
}
```

### Migration Steps

1. Create exception hierarchy (Step 1)
2. Create and register `CustomExceptionHandler` (Steps 2-3)
3. Update Features to throw domain exceptions instead of returning nulls
4. Remove try/catch blocks from Controllers
5. Add `[ProducesResponseType]` attributes
6. Run integration tests to verify HTTP status codes

## Troubleshooting

### Problem: Exceptions Not Being Caught

**Symptom:** Exceptions bypass your custom handler and return generic 500 errors.

**Causes:**
1. `UseExceptionHandler()` is called AFTER the middleware that throws the exception
2. Exception handler not registered in DI container
3. Exception type not matched in pattern matching

**Solution:**
- Move `UseExceptionHandler()` earlier in the pipeline
- Verify `services.AddExceptionHandler<CustomExceptionHandler>()` is called
- Add the exception type to the pattern matching switch statement

### Problem: Multiple Handlers Conflict

**Symptom:** First handler always wins, other handlers never execute.

**Cause:** First handler returns `true` for all exceptions.

**Solution:** Return `false` from handlers that don't recognize the exception:

```csharp
var handled = exception switch
{
    MyException => await HandleMyExceptionAsync(...),
    _ => false // IMPORTANT: Return false for unrecognized exceptions
};
return handled;
```

### Problem: ProblemDetails Not Serialized

**Symptom:** Response body is empty or plain text instead of JSON.

**Cause:** `AddProblemDetails()` not called in service registration.

**Solution:**
```csharp
builder.Services.AddProblemDetails(); // Required for RFC 7807 support
```

### Problem: Trace IDs Not in Logs

**Symptom:** Cannot correlate ProblemDetails trace IDs with log entries.

**Cause:** Logging not configured to capture trace context.

**Solution:** Add OpenTelemetry logging or ensure ASP.NET Core logging includes trace IDs:

```csharp
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true; // Includes trace context
});
```

### Problem: Validation Happens Too Late

**Symptom:** Invalid data reaches Feature layer before validation fails.

**Cause:** FluentValidation not registered or auto-validation not enabled.

**Solution:**

```csharp
services.AddValidatorsFromAssemblyContaining<TransactionEditDtoValidator>();
services.AddFluentValidationAutoValidation(); // Enables automatic validation
```

## Comparison to Other Patterns

### vs. Try/Catch in Controllers

| Aspect | Try/Catch | IExceptionHandler |
|--------|-----------|-------------------|
| **Code duplication** | High (every controller) | None (centralized) |
| **Controller complexity** | High | Low (thin controllers) |
| **Consistency** | Hard to maintain | Guaranteed |
| **Testing** | Must test each controller | Test handler once |
| **Logging** | Scattered | Centralized |
| **Clean Architecture** | Violates (HTTP in controller) | Follows (HTTP in middleware) |

### vs. Exception Filters

| Aspect | Exception Filters | IExceptionHandler |
|--------|------------------|-------------------|
| **Pipeline location** | MVC pipeline | HTTP pipeline |
| **Scope** | Controller/Action | Global |
| **Order control** | Complex | Simple (registration order) |
| **Non-MVC exceptions** | Not caught | Caught |
| **Middleware exceptions** | Not caught | Caught |
| **Recommended** | Legacy approach | Modern approach (.NET 8+) |

### vs. Problem Details Middleware (Legacy)

| Aspect | Problem Details Middleware | IExceptionHandler |
|--------|---------------------------|-------------------|
| **Configuration** | Action delegate | Typed class |
| **Testability** | Difficult | Easy (unit testable) |
| **Extensibility** | Limited | High (pattern matching) |
| **Recommended** | Legacy (.NET 6) | Modern (.NET 7+) |

## References

### YoFi.V3 Implementation Files

- [`CustomExceptionHandler.cs`](../src/Controllers/Middleware/CustomExceptionHandler.cs) - Main exception handler
- [`TenancyExceptionHandler.cs`](../src/Controllers/Tenancy/Exceptions/TenancyExceptionHandler.cs) - Domain-specific handler
- [`ResourceNotFoundException.cs`](../src/Entities/Exceptions/ResourceNotFoundException.cs) - Base 404 exception
- [`ValidationException.cs`](../src/Entities/Exceptions/ValidationException.cs) - Base 400 exception
- [`TenancyException.cs`](../src/Entities/Tenancy/Exceptions/TenancyException.cs) - Tenancy exception hierarchy
- [`ServiceCollectionExtensions.cs`](../src/Controllers/Extensions/ServiceCollectionExtensions.cs) - DI registration
- [`SetupMiddleware.cs`](../src/BackEnd/Setup/SetupMiddleware.cs) - Middleware pipeline configuration
- [`Program.cs`](../src/BackEnd/Program.cs) - Application startup

### Related Documentation

- [`docs/LOGGING-POLICY.md`](../LOGGING-POLICY.md) - Logging conventions and patterns
- [`docs/ARCHITECTURE.md`](../ARCHITECTURE.md) - Clean Architecture implementation
- [`docs/TENANCY.md`](../TENANCY.md) - Multi-tenancy implementation (includes exception handling)
- [RFC 7807 - Problem Details for HTTP APIs](https://tools.ietf.org/html/rfc7807)
- [ASP.NET Core IExceptionHandler Documentation](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling)

## FAQ

### Q: Should I catch exceptions in my Features?

**A:** No. Let exceptions propagate to the exception handler middleware. Only catch exceptions if you can handle them meaningfully (e.g., wrapping external library exceptions in domain exceptions).

### Q: What about async exceptions?

**A:** The pattern works identically for async methods. `IExceptionHandler.TryHandleAsync` is async-aware.

### Q: Can I use this with minimal APIs?

**A:** Yes, but minimal APIs don't benefit as much since they don't have the controller/feature separation that makes this pattern shine.

### Q: How do I handle exceptions in middleware?

**A:** Middleware exceptions are caught by `UseExceptionHandler()` if it's registered before the middleware. Ensure proper pipeline ordering.

### Q: What about database exceptions?

**A:** Catch database exceptions in your repository layer and wrap them in domain exceptions:

```csharp
try
{
    await dbContext.SaveChangesAsync();
}
catch (DbUpdateException ex) when (ex.InnerException is SqliteException sqlEx && sqlEx.SqliteErrorCode == 19)
{
    // Unique constraint violation
    throw new DuplicateResourceException("Transaction", transactionId);
}
```

### Q: Should I log in the exception handler?

**A:** Yes, log all handled exceptions with at least Information level. Log unhandled exceptions at Error level with full stack traces.

### Q: How do I test the exception handler?

**A:** Use integration tests with `WebApplicationFactory` to verify end-to-end behavior. Use unit tests for individual handler methods if they're complex.

### Q: Can I have multiple IExceptionHandler implementations?

**A:** Yes! Register multiple handlers and they'll be invoked in registration order. Each handler can return `false` to pass the exception to the next handler.

### Q: What about validation vs. domain exceptions?

**A:** Use FluentValidation for DTO/input validation at the controller boundary (automatic 400). Use domain exceptions for business rule violations in Features.

## Checklist for New Projects

- [ ] Create exception hierarchy in Entities/Domain layer
  - [ ] `ResourceNotFoundException` base class
  - [ ] Specific exceptions (e.g., `ProductNotFoundException`)
  - [ ] `ValidationException` for business rules
- [ ] Create `CustomExceptionHandler` implementing `IExceptionHandler`
  - [ ] Pattern matching for exception types
  - [ ] Handler methods for each exception type
  - [ ] `CreateProblemDetails` helper method
  - [ ] Logging with `[LoggerMessage]`
- [ ] Register exception handler in DI
  - [ ] `services.AddExceptionHandler<CustomExceptionHandler>()`
  - [ ] `services.AddProblemDetails()`
- [ ] Configure middleware pipeline
  - [ ] `app.UseExceptionHandler()` before business middleware
  - [ ] Correct ordering (see Step 4)
- [ ] Update Features to throw domain exceptions
  - [ ] Remove null returns
  - [ ] Throw typed exceptions
  - [ ] No try/catch (let exceptions propagate)
- [ ] Update Controllers
  - [ ] Remove try/catch blocks
  - [ ] Add `[ProducesResponseType]` for error responses
  - [ ] Keep controllers thin
- [ ] Add FluentValidation (optional but recommended)
  - [ ] Create validators for DTOs
  - [ ] Register validators in DI
  - [ ] Enable auto-validation
  - [ ] Customize ValidationProblemDetails format
- [ ] Write integration tests
  - [ ] Test each HTTP status code
  - [ ] Verify ProblemDetails structure
  - [ ] Verify trace IDs included
- [ ] Document exception-to-HTTP mappings
  - [ ] Update API documentation
  - [ ] Add to README or architecture docs

## Summary

This exception handling pattern provides:

1. **Centralized exception-to-HTTP mapping** - Single point of control via `IExceptionHandler`
2. **Clean separation of concerns** - Controllers handle HTTP, Features handle business logic, Middleware handles errors
3. **Consistent error responses** - RFC 7807 ProblemDetails with trace IDs
4. **Type safety** - Pattern matching maps exceptions to status codes
5. **Extensibility** - Easy to add new exception types and handlers
6. **Observability** - Automatic logging with full context
7. **Testability** - Unit test Features for exceptions, integration test Controllers for HTTP status codes

**Key principle:** Features throw domain exceptions, middleware converts to HTTP responses. Controllers stay thin with no error handling logic.
