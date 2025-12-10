using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using YoFi.V3.Entities.Exceptions;
using YoFi.V3.Entities.Tenancy;

namespace YoFi.V3.Controllers.Middleware;

/// <summary>
/// Configurable exception handler that maps specific exception types to HTTP status codes and problem details.
/// </summary>
public partial class CustomExceptionHandler(ILogger<CustomExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Try each registered exception handler
        var handled = exception switch
        {
            // 404 Not Found - ResourceNotFoundException and derived types
            ResourceNotFoundException notFound => await HandleResourceNotFoundAsync(
                httpContext, notFound, cancellationToken),

            // 404 Not Found - KeyNotFoundException
            KeyNotFoundException keyNotFound => await HandleKeyNotFoundExceptionAsync(
                httpContext, keyNotFound, cancellationToken),

            // 400 Bad Request - ArgumentException (validation errors)
            ArgumentException argumentException => await HandleArgumentExceptionAsync(
                httpContext, argumentException, cancellationToken),

            // 500 Internal Server Error - TenantContextNotSetException
            TenantContextNotSetException tenantContextError => await HandleTenantContextNotSetAsync(
                httpContext, tenantContextError, cancellationToken),

            // Add more exception mappings here as needed, for example:
            // ValidationException validation => await HandleValidationExceptionAsync(
            //     httpContext, validation, cancellationToken),
            // UnauthorizedAccessException unauthorized => await HandleUnauthorizedAsync(
            //     httpContext, unauthorized, cancellationToken),

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
    /// Handles ResourceNotFoundException and its derived types (e.g., TenantNotFoundException, TransactionNotFoundException).
    /// Returns HTTP 404 with problem details.
    /// </summary>
    private async ValueTask<bool> HandleResourceNotFoundAsync(
        HttpContext httpContext,
        ResourceNotFoundException exception,
        CancellationToken cancellationToken)
    {
        httpContext.Response.StatusCode = StatusCodes.Status404NotFound;

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = $"{exception.ResourceType} not found",
            Detail = exception.Message,
            Instance = httpContext.Request.Path
        };

        problemDetails.Extensions["resourceType"] = exception.ResourceType;
        problemDetails.Extensions["resourceKey"] = exception.ResourceKey;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }

    /// <summary>
    /// Handles KeyNotFoundException.
    /// Returns HTTP 404 with problem details.
    /// </summary>
    private async ValueTask<bool> HandleKeyNotFoundExceptionAsync(
        HttpContext httpContext,
        KeyNotFoundException exception,
        CancellationToken cancellationToken)
    {
        httpContext.Response.StatusCode = StatusCodes.Status404NotFound;

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Resource not found",
            Detail = exception.Message,
            Instance = httpContext.Request.Path
        };

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }

    /// <summary>
    /// Handles ArgumentException (validation errors).
    /// Returns HTTP 400 with problem details.
    /// </summary>
    private async ValueTask<bool> HandleArgumentExceptionAsync(
        HttpContext httpContext,
        ArgumentException exception,
        CancellationToken cancellationToken)
    {
        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation Error",
            Detail = exception.Message,
            Instance = httpContext.Request.Path
        };

        if (!string.IsNullOrEmpty(exception.ParamName))
        {
            problemDetails.Extensions["paramName"] = exception.ParamName;
        }

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }

    /// <summary>
    /// Handles TenantContextNotSetException when tenant context is not properly initialized.
    /// Returns HTTP 500 with problem details.
    /// </summary>
    private async ValueTask<bool> HandleTenantContextNotSetAsync(
        HttpContext httpContext,
        TenantContextNotSetException exception,
        CancellationToken cancellationToken)
    {
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Tenant Context Error",
            Detail = "The tenant context was not properly initialized. This is likely a configuration issue with the tenant middleware.",
            Instance = httpContext.Request.Path
        };

        problemDetails.Extensions["error"] = exception.Message;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }

    // Example: Add more specific exception handlers as needed
    // private static async ValueTask<bool> HandleValidationExceptionAsync(
    //     HttpContext httpContext,
    //     ValidationException exception,
    //     CancellationToken cancellationToken)
    // {
    //     httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
    //
    //     var problemDetails = new ProblemDetails
    //     {
    //         Status = StatusCodes.Status400BadRequest,
    //         Title = "Validation Error",
    //         Detail = exception.Message,
    //         Instance = httpContext.Request.Path
    //     };
    //
    //     problemDetails.Extensions["errors"] = exception.Errors;
    //
    //     await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
    //     return true;
    // }

    [LoggerMessage(0, LogLevel.Information, "{Location}: Handled {ExceptionType} with status code {StatusCode}")]
    private partial void LogHandledException(string exceptionType, int statusCode, [CallerMemberName] string? location = null);
}
