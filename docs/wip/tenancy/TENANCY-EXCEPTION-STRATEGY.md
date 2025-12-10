# Tenancy Exception Strategy

## Overview

This document defines the exception strategy for tenancy-related code in the `src/Entities/Tenancy` namespace. The strategy mirrors the successful pattern used in the application layer with [`ResourceNotFoundException`](../../../src/Entities/Exceptions/ResourceNotFoundException.cs), providing a base exception class that enables the [`CustomExceptionHandler`](../../../src/Controllers/Middleware/CustomExceptionHandler.cs) to automatically map exceptions to appropriate HTTP status codes.

## Design Principles

1. **Base Exception Pattern**: Create a base `TenancyException` class that all tenancy-specific exceptions inherit from, enabling consistent handling
2. **Separation of Concerns**: Domain exceptions focus on business logic; HTTP status code mapping is handled by `TenancyExceptionHandler`
3. **No HTTP Dependencies**: Tenancy exceptions don't reference HTTP status codes or ASP.NET Core types
4. **No Application Layer Dependency**: Tenancy exceptions live in `src/Entities/Tenancy` and cannot depend on `src/Application` or `src/Controllers`
5. **Structured Exception Data**: Include relevant identifiers (UserId, TenantId, Key) as strongly-typed properties
6. **Clear Semantics**: Exception names clearly indicate the problem (NotFound, Duplicate, AccessDenied, etc.)

## Exception Hierarchy

```
Exception
├── TenancyException (abstract base, new)
│   ├── TenancyResourceNotFoundException (abstract base for 404s, new)
│   │   └── UserTenantRoleNotFoundException (existing, refactor to inherit)
│   ├── TenancyAccessDeniedException (abstract base for 403s, new)
│   │   ├── TenantNotFoundException (existing, refactor to inherit - returns 403 for security)
│   │   └── TenantAccessDeniedException (new, for explicit access denial)
│   ├── DuplicateUserTenantRoleException (existing, refactor to inherit - returns 409)
│   └── TenantContextNotSetException (existing, refactor to inherit - returns 500)
```

## HTTP Status Code Mappings

| Exception Type | HTTP Status | Use Case |
|---------------|-------------|----------|
| [`TenantNotFoundException`](../../../src/Controllers/Tenancy/TenantNotFoundException.cs) | 403 Forbidden | Tenant with specified key doesn't exist (returns 403 to prevent enumeration) |
| [`UserTenantRoleNotFoundException`](../../../src/Entities/Tenancy/UserTenantRoleNotFoundException.cs) | 404 Not Found | User doesn't have a role assignment for the tenant |
| [`DuplicateUserTenantRoleException`](../../../src/Entities/Tenancy/DuplicateUserTenantRoleException.cs) | 409 Conflict | Attempting to create duplicate user-tenant role assignment |
| `TenantAccessDeniedException` (new) | 403 Forbidden | User doesn't have access to the requested tenant |
| [`TenantContextNotSetException`](../../../src/Entities/Tenancy/TenantContextNotSetException.cs) | 500 Internal Server Error | Tenant middleware failed (code error) |

**Security Note**: Both `TenantNotFoundException` and `TenantAccessDeniedException` return 403 (not 404) to prevent tenant enumeration attacks. From the user's perspective, "tenant doesn't exist" and "tenant exists but you don't have access" should be indistinguishable. The [`TenancyExceptionHandler`](../../../src/Controllers/Tenancy/Exceptions/TenancyExceptionHandler.cs) ensures both exceptions return identical 403 responses.

## Exception Definitions

### Base Exception: TenancyException

```csharp
namespace YoFi.V3.Entities.Tenancy;

/// <summary>
/// Base exception for all tenancy-related errors.
/// Provides a common base for exception handling.
/// </summary>
public abstract class TenancyException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TenancyException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    protected TenancyException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenancyException"/> class with an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    protected TenancyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
```

### Abstract Base: TenancyResourceNotFoundException

```csharp
namespace YoFi.V3.Entities.Tenancy;

/// <summary>
/// Base exception for when a tenancy-related resource cannot be found.
/// </summary>
public abstract class TenancyResourceNotFoundException : TenancyException
{
    /// <summary>
    /// Gets the type of resource that was not found (e.g., "Tenant", "UserTenantRole").
    /// </summary>
    public abstract string ResourceType { get; }

    /// <summary>
    /// Gets the unique key of the resource that was not found (if applicable).
    /// </summary>
    public Guid? ResourceKey { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenancyResourceNotFoundException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="resourceKey">The unique identifier of the resource that was not found.</param>
    protected TenancyResourceNotFoundException(string message, Guid? resourceKey = null)
        : base(message)
    {
        ResourceKey = resourceKey;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenancyResourceNotFoundException"/> class with an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="resourceKey">The unique identifier of the resource that was not found.</param>
    /// <param name="innerException">The inner exception.</param>
    protected TenancyResourceNotFoundException(string message, Guid? resourceKey, Exception innerException)
        : base(message, innerException)
    {
        ResourceKey = resourceKey;
    }
}
```

### Abstract Base: TenancyAccessDeniedException

```csharp
namespace YoFi.V3.Entities.Tenancy;

/// <summary>
/// Base exception for when access to a tenancy-related resource is denied.
/// </summary>
public abstract class TenancyAccessDeniedException : TenancyException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TenancyAccessDeniedException"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    protected TenancyAccessDeniedException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenancyAccessDeniedException"/> class with an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    protected TenancyAccessDeniedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
```

### Concrete Exception: TenantNotFoundException

```csharp
namespace YoFi.V3.Entities.Tenancy;

/// <summary>
/// Exception thrown when a requested tenant cannot be found.
/// Returns HTTP 403 (not 404) to prevent tenant enumeration attacks.
/// </summary>
/// <remarks>
/// For security reasons, this exception returns 403 Forbidden instead of 404 Not Found.
/// This makes it indistinguishable from explicit access denial, preventing
/// attackers from enumerating valid tenant IDs by observing different status codes.
/// </remarks>
public class TenantNotFoundException : TenancyAccessDeniedException
{

    /// <summary>
    /// Gets the unique key of the tenant that was not found.
    /// </summary>
    public Guid TenantKey { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantNotFoundException"/> class.
    /// </summary>
    /// <param name="key">The unique identifier of the tenant that was not found.</param>
    public TenantNotFoundException(Guid key)
        : base($"Access to tenant '{key}' is denied.")
    {
        TenantKey = key;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantNotFoundException"/> class with a custom message.
    /// </summary>
    /// <param name="key">The unique identifier of the tenant that was not found.</param>
    /// <param name="message">The custom error message.</param>
    public TenantNotFoundException(Guid key, string message)
        : base(message)
    {
        TenantKey = key;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantNotFoundException"/> class with an inner exception.
    /// </summary>
    /// <param name="key">The unique identifier of the tenant that was not found.</param>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public TenantNotFoundException(Guid key, string message, Exception innerException)
        : base(message, innerException)
    {
        TenantKey = key;
    }
}
```

### Concrete Exception: UserTenantRoleNotFoundException (Refactored)

```csharp
namespace YoFi.V3.Entities.Tenancy;

/// <summary>
/// Exception thrown when attempting to access a user-tenant role assignment that doesn't exist.
/// </summary>
public class UserTenantRoleNotFoundException : TenancyResourceNotFoundException
{
    /// <inheritdoc/>
    public override string ResourceType => "UserTenantRole";

    /// <inheritdoc/>
    public override string Title => "User tenant role not found";

    /// <summary>
    /// Gets the user identifier.
    /// </summary>
    public string UserId { get; }

    /// <summary>
    /// Gets the tenant identifier.
    /// </summary>
    public long TenantId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UserTenantRoleNotFoundException"/> class.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    public UserTenantRoleNotFoundException(string userId, long tenantId)
        : base($"User '{userId}' does not have a role assignment for tenant '{tenantId}'.")
    {
        UserId = userId;
        TenantId = tenantId;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UserTenantRoleNotFoundException"/> class with an inner exception.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="innerException">The inner exception.</param>
    public UserTenantRoleNotFoundException(string userId, long tenantId, Exception innerException)
        : base($"User '{userId}' does not have a role assignment for tenant '{tenantId}'.", null, innerException)
    {
        UserId = userId;
        TenantId = tenantId;
    }
}
```

### Concrete Exception: DuplicateUserTenantRoleException (Refactored)

```csharp
namespace YoFi.V3.Entities.Tenancy;

/// <summary>
/// Exception thrown when attempting to add a user-tenant role assignment that already exists.
/// A user can only have one role per tenant due to unique constraint.
/// </summary>
public class DuplicateUserTenantRoleException : TenancyException
{
    /// <summary>
    /// Gets the user identifier.
    /// </summary>
    public string UserId { get; }

    /// <summary>
    /// Gets the tenant identifier.
    /// </summary>
    public long TenantId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DuplicateUserTenantRoleException"/> class.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    public DuplicateUserTenantRoleException(string userId, long tenantId)
        : base($"User '{userId}' already has a role assignment for tenant '{tenantId}'.")
    {
        UserId = userId;
        TenantId = tenantId;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DuplicateUserTenantRoleException"/> class with an inner exception.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="innerException">The inner exception.</param>
    public DuplicateUserTenantRoleException(string userId, long tenantId, Exception innerException)
        : base($"User '{userId}' already has a role assignment for tenant '{tenantId}'.", innerException)
    {
        UserId = userId;
        TenantId = tenantId;
    }
}
```

### Concrete Exception: TenantAccessDeniedException (New)

```csharp
namespace YoFi.V3.Entities.Tenancy;

/// <summary>
/// Exception thrown when a user attempts to access a tenant they don't have permissions for.
/// This is distinct from TenantNotFoundException - the tenant exists, but the user lacks access.
/// However, both return the same HTTP 403 status code for security.
/// </summary>
public class TenantAccessDeniedException : TenancyAccessDeniedException
{

    /// <summary>
    /// Gets the user identifier.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// Gets the tenant key that access was denied for.
    /// </summary>
    public Guid TenantKey { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantAccessDeniedException"/> class.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="tenantKey">The tenant key that access was denied for.</param>
    public TenantAccessDeniedException(Guid userId, Guid tenantKey)
        : base($"User '{userId}' does not have access to tenant '{tenantKey}'.")
    {
        UserId = userId;
        TenantKey = tenantKey;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantAccessDeniedException"/> class with a custom message.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="tenantKey">The tenant key that access was denied for.</param>
    /// <param name="message">The custom error message.</param>
    public TenantAccessDeniedException(Guid userId, Guid tenantKey, string message)
        : base(message)
    {
        UserId = userId;
        TenantKey = tenantKey;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantAccessDeniedException"/> class with an inner exception.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="tenantKey">The tenant key that access was denied for.</param>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public TenantAccessDeniedException(Guid userId, Guid tenantKey, string message, Exception innerException)
        : base(message, innerException)
    {
        UserId = userId;
        TenantKey = tenantKey;
    }
}
```

### Concrete Exception: TenantContextNotSetException (Refactored)

```csharp
namespace YoFi.V3.Entities.Tenancy;

/// <summary>
/// Exception thrown when attempting to access the current tenant but the tenant context has not been set.
/// This typically indicates that the tenant middleware has not run or has failed to resolve a tenant.
/// This is a code error (500) rather than a client error.
/// </summary>
public class TenantContextNotSetException : TenancyException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TenantContextNotSetException"/> class.
    /// </summary>
    public TenantContextNotSetException()
        : base("Current tenant is not set. The tenant middleware may not have run or failed to resolve a tenant.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantContextNotSetException"/> class with a custom message.
    /// </summary>
    /// <param name="message">The custom error message.</param>
    public TenantContextNotSetException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TenantContextNotSetException"/> class with an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public TenantContextNotSetException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
```

## Exception Handler Implementation

### TenancyExceptionHandler (New)

Create a new handler in `src/Entities/Tenancy/TenancyExceptionHandler.cs`:

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace YoFi.V3.Entities.Tenancy;

/// <summary>
/// Handles TenancyException and its derived types, mapping them to appropriate HTTP responses.
/// </summary>
public static class TenancyExceptionHandler
{
    /// <summary>
    /// Handles a TenancyException by writing an appropriate ProblemDetails response.
    /// </summary>
    /// <param name="httpContext">The HTTP context.</param>
    /// <param name="exception">The tenancy exception to handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task HandleAsync(
        HttpContext httpContext,
        TenancyException exception,
        CancellationToken cancellationToken = default)
    {
        // Map exception type to HTTP status code and title
        var (statusCode, title) = MapExceptionToResponse(exception);

        httpContext.Response.StatusCode = statusCode;

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = exception.Message,
            Instance = httpContext.Request.Path
        };

        // Add type-specific extensions
        AddExceptionExtensions(problemDetails, exception);

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
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

            // 404 Not Found - Resource not found exceptions
            TenancyResourceNotFoundException notFound => (StatusCodes.Status404NotFound, $"{notFound.ResourceType} not found"),

            // 409 Conflict - Duplicate resource exceptions
            DuplicateUserTenantRoleException => (StatusCodes.Status409Conflict, "Duplicate user tenant role"),

            // 500 Internal Server Error - Configuration/code errors
            TenantContextNotSetException => (StatusCodes.Status500InternalServerError, "Tenant context error"),

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
            case TenancyResourceNotFoundException notFound:
                problemDetails.Extensions["resourceType"] = notFound.ResourceType;
                if (notFound.ResourceKey.HasValue)
                {
                    problemDetails.Extensions["resourceKey"] = notFound.ResourceKey.Value;
                }
                if (exception is UserTenantRoleNotFoundException roleNotFound)
                {
                    problemDetails.Extensions["userId"] = roleNotFound.UserId;
                    problemDetails.Extensions["tenantId"] = roleNotFound.TenantId;
                }
                break;

            case TenantNotFoundException tenantNotFound:
                // Minimal information to prevent enumeration
                problemDetails.Extensions["tenantKey"] = tenantNotFound.TenantKey;
                break;

            case TenantAccessDeniedException accessDenied:
                problemDetails.Extensions["userId"] = accessDenied.UserId;
                problemDetails.Extensions["tenantKey"] = accessDenied.TenantKey;
                break;

            case DuplicateUserTenantRoleException duplicate:
                problemDetails.Extensions["userId"] = duplicate.UserId;
                problemDetails.Extensions["tenantId"] = duplicate.TenantId;
                break;
        }
    }
}
```

### CustomExceptionHandler Updates

Update [`CustomExceptionHandler`](../../../src/Controllers/Middleware/CustomExceptionHandler.cs) to call into the tenancy handler:

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

        // 404 Not Found - ResourceNotFoundException and derived types
        ResourceNotFoundException notFound => await HandleResourceNotFoundAsync(
            httpContext, notFound, cancellationToken),

        // 404 Not Found - KeyNotFoundException (legacy, deprecated)
        KeyNotFoundException keyNotFound => await HandleKeyNotFoundExceptionAsync(
            httpContext, keyNotFound, cancellationToken),

        // 400 Bad Request - ArgumentException (validation errors)
        ArgumentException argumentException => await HandleArgumentExceptionAsync(
            httpContext, argumentException, cancellationToken),

        // If no match, let other handlers process it
        _ => false
    };

    if (handled)
    {
        LogHandledException(exception.GetType().Name, httpContext.Response.StatusCode);
    }

    return handled;
}

/// <summary>
/// Handles TenancyException and its derived types by delegating to the tenancy-specific handler.
/// </summary>
private static async ValueTask<bool> HandleTenancyExceptionAsync(
    HttpContext httpContext,
    TenancyException exception,
    CancellationToken cancellationToken)
{
    await TenancyExceptionHandler.HandleAsync(httpContext, exception, cancellationToken);
    return true;
}
```

## Usage Examples

### Replace KeyNotFoundException in TenantFeature

**Current code (lines 74-76 and 82-84):**
```csharp
// TODO: Domain-specific exception
throw new KeyNotFoundException($"Tenant with key {tenantKey} not found.");

// TODO: Domain-specific exception
throw new KeyNotFoundException($"User does not have access to tenant {tenantKey}.");
```

**New code:**
```csharp
throw new TenantNotFoundException(tenantKey);

throw new TenantAccessDeniedException(userId, tenantKey);
```

### Exception Handling in Repository Operations

```csharp
try
{
    await tenantRepository.AddUserTenantRoleAsync(assignment);
}
catch (DbUpdateException ex) when (IsDuplicateKeyViolation(ex))
{
    throw new DuplicateUserTenantRoleException(assignment.UserId, assignment.TenantId, ex);
}
```

## Migration Plan

1. **Create base exceptions** - Add [`TenancyException`](../../../src/Entities/Tenancy/TenancyException.cs), [`TenancyResourceNotFoundException`](../../../src/Entities/Tenancy/TenancyResourceNotFoundException.cs), and [`TenancyAccessDeniedException`](../../../src/Entities/Tenancy/TenancyAccessDeniedException.cs) to `src/Entities/Tenancy/`

2. **Move TenantNotFoundException** - Move from `src/Controllers/Tenancy/` to `src/Entities/Tenancy/` and refactor to inherit from [`TenancyAccessDeniedException`](../../../src/Entities/Tenancy/TenancyAccessDeniedException.cs)

3. **Refactor existing exceptions** - Update [`UserTenantRoleNotFoundException`](../../../src/Entities/Tenancy/UserTenantRoleNotFoundException.cs), [`DuplicateUserTenantRoleException`](../../../src/Entities/Tenancy/DuplicateUserTenantRoleException.cs), and [`TenantContextNotSetException`](../../../src/Entities/Tenancy/TenantContextNotSetException.cs) to inherit from appropriate base classes

4. **Add new exception** - Create concrete [`TenantAccessDeniedException`](../../../src/Entities/Tenancy/TenantAccessDeniedException.cs)

5. **Create TenancyExceptionHandler** - Add static handler class in `src/Entities/Tenancy/` to process tenancy exceptions

6. **Update CustomExceptionHandler** - Add delegation to [`TenancyExceptionHandler`](../../../src/Entities/Tenancy/TenancyExceptionHandler.cs) and update switch statement

6. **Replace KeyNotFoundException usage** - Update [`TenantFeature`](../../../src/Controllers/Tenancy/TenantFeature.cs) to use domain-specific exceptions

7. **Update tests** - Modify integration and unit tests to expect new exception types

8. **Remove legacy handler** - Once all `KeyNotFoundException` usage is replaced, consider removing or deprecating that handler

## Benefits

1. **Consistency**: Matches the successful pattern from [`ResourceNotFoundException`](../../../src/Entities/Exceptions/ResourceNotFoundException.cs)
2. **No Dependencies**: Tenancy exceptions remain in Entities layer with no application dependencies
3. **Automatic Mapping**: HTTP status codes are defined on exceptions, simplifying handler logic
4. **Type Safety**: Strongly-typed properties for user IDs, tenant IDs, and keys
5. **Extensibility**: Easy to add new tenancy exception types in the future
6. **Clear Semantics**: Exception names clearly communicate the problem
7. **Structured Data**: Problem details responses include relevant context in extensions

## Future Considerations

- **TenantQuotaExceededException** (429 Too Many Requests) - For rate limiting tenant operations
- **TenantDisabledException** (403 Forbidden) - For suspended or disabled tenants
- **TenantOperationNotPermittedException** (403 Forbidden) - For role-based access control violations
- **InvalidTenantOperationException** (400 Bad Request) - For business rule violations
