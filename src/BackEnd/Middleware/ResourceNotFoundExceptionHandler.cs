using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using YoFi.V3.Entities.Exceptions;

namespace YoFi.V3.BackEnd.Middleware;

/// <summary>
/// Exception handler that maps ResourceNotFoundException (and derived types) to HTTP 404 responses.
/// </summary>
public class ResourceNotFoundExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Only handle ResourceNotFoundException and its derived types
        if (exception is not ResourceNotFoundException resourceNotFound)
        {
            // Let other handlers handle this exception
            return false;
        }

        // Set the response status code to 404
        httpContext.Response.StatusCode = StatusCodes.Status404NotFound;

        // Create problem details
        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status404NotFound,
            Title = $"{resourceNotFound.ResourceType} Not Found",
            Detail = resourceNotFound.Message,
            Instance = httpContext.Request.Path
        };

        // Add additional context
        problemDetails.Extensions["resourceType"] = resourceNotFound.ResourceType;
        problemDetails.Extensions["resourceKey"] = resourceNotFound.ResourceKey;

        // Write the problem details to the response
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        // Return true to indicate we handled the exception
        return true;
    }
}
