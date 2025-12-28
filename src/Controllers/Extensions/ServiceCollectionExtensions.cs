using System.Diagnostics;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using YoFi.V3.Application.Validation;
using YoFi.V3.Controllers.Middleware;

namespace YoFi.V3.Controllers.Extensions;

/// <summary>
/// Extension methods for configuring Controllers-specific services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Controllers-specific services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddControllerServices(this IServiceCollection services)
    {
        // Register custom exception handler for application-specific exceptions
        services.AddExceptionHandler<CustomExceptionHandler>();

        // Register FluentValidation validators from Application assembly
        services.AddValidatorsFromAssemblyContaining<TransactionEditDtoValidator>();

        // Add FluentValidation to ASP.NET Core model binding pipeline.
        // This causes automatic validation at the controller boundary BEFORE controller actions execute.
        // Invalid DTOs will return 400 Bad Request with validation error details, preventing
        // invalid data from reaching controller methods or the Application layer.
        services.AddFluentValidationAutoValidation();

        // Customize validation error response format to include a "detail" field
        // that combines all validation errors into a single string for frontend compatibility.
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

                // Add a "detail" field that combines all error messages for frontend compatibility
                var errorMessages = problemDetails.Errors
                    .SelectMany(kvp => kvp.Value.Select(msg => $"{kvp.Key}: {msg}"))
                    .ToList();

                if (errorMessages.Count > 0)
                {
                    problemDetails.Detail = string.Join("; ", errorMessages);
                }

                // Add W3C trace context ID for diagnostics (format: 00-{trace-id}-{span-id}-{flags})
                problemDetails.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;

                return new BadRequestObjectResult(problemDetails)
                {
                    ContentTypes = { "application/problem+json" }
                };
            };
        });

        return services;
    }
}
