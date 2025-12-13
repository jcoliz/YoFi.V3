using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using YoFi.V3.Entities.Tenancy.Exceptions;
using TenantNotFound = YoFi.V3.Entities.Tenancy.Exceptions.TenantNotFoundException;
using TenantAccess = YoFi.V3.Entities.Tenancy.Exceptions.TenantAccessDeniedException;

namespace YoFi.V3.Controllers.Tenancy.Exceptions;

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
            // Check most specific types first
            case UserTenantRoleNotFoundException roleNotFound:
                problemDetails.Extensions["resourceType"] = roleNotFound.ResourceType;
                problemDetails.Extensions["userId"] = roleNotFound.UserId;
                problemDetails.Extensions["tenantKey"] = roleNotFound.TenantKey;
                break;

            case TenantNotFound tenantNotFound:
                // Minimal information to prevent enumeration
                problemDetails.Extensions["tenantKey"] = tenantNotFound.TenantKey;
                break;

            case TenantAccess accessDenied:
                problemDetails.Extensions["userId"] = accessDenied.UserId;
                problemDetails.Extensions["tenantKey"] = accessDenied.TenantKey;
                break;

            case DuplicateUserTenantRoleException duplicate:
                problemDetails.Extensions["userId"] = duplicate.UserId;
                problemDetails.Extensions["tenantKey"] = duplicate.TenantKey;
                break;

            case TenancyResourceNotFoundException notFound:
                // Generic resource not found (fallback)
                problemDetails.Extensions["resourceType"] = notFound.ResourceType;
                if (notFound.ResourceKey.HasValue)
                {
                    problemDetails.Extensions["resourceKey"] = notFound.ResourceKey.Value;
                }
                break;
        }
    }
}
