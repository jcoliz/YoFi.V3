using FluentValidation;
using FluentValidation.AspNetCore;
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

        return services;
    }
}
