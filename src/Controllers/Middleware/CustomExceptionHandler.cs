using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using YoFi.V3.Controllers.Tenancy.Exceptions;
using YoFi.V3.Entities.Exceptions;
using YoFi.V3.Entities.Tenancy.Exceptions;

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
            LogHandledException(exception,exception.GetType().Name, httpContext.Response.StatusCode);
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

        // Use Activity.Current?.Id for W3C trace context format, matching built-in ASP.NET Core behavior
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        problemDetails.Extensions["traceId"] = traceId;

        return problemDetails;
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

        var problemDetails = CreateProblemDetails(
            httpContext,
            StatusCodes.Status404NotFound,
            "Resource not found",
            exception.Message);

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

        var problemDetails = CreateProblemDetails(
            httpContext,
            StatusCodes.Status400BadRequest,
            "Validation Error",
            exception.Message);

        if (!string.IsNullOrEmpty(exception.ParamName))
        {
            problemDetails.Extensions["paramName"] = exception.ParamName;
        }

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
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

    [LoggerMessage(1, LogLevel.Information, "{Location}: Handled {ExceptionType} with status code {StatusCode}")]
    private partial void LogHandledException(Exception ex, string exceptionType, int statusCode, [CallerMemberName] string? location = null);
}
