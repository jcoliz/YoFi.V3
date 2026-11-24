using System.Diagnostics;
using DotNetNuxt.AspNetCore.Options;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NuxtIdentity.AspNetCore.Extensions;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Validation;
using YoFi.V3.Controllers.Middleware;
using YoFi.V3.Controllers.Tenancy.Seeding;

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
    /// <param name="version">Application version reported by the version endpoint.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddControllerServices(this IServiceCollection services, string version)
    {
        // Register custom exception handler for application-specific exceptions
        services.AddExceptionHandler<CustomExceptionHandler>();

        // Register FluentValidation validators from Application assembly
        services.AddValidatorsFromAssemblyContaining<TransactionEditDtoValidator>();
        services.AddTransient<IValidator<TransactionSplitsSeedRequest>, TransactionSplitsSeedRequestValidator>();

        // Explicitly register generic CollectionRequestValidator for commonly used types
        // (Generic validators may not be auto-discovered by AddValidatorsFromAssemblyContaining)
        services.AddTransient<IValidator<CollectionRequest<TransactionEditDto>>, CollectionRequestValidator<TransactionEditDto>>();
        services.AddTransient<IValidator<CollectionRequest<PayeeMatchingRuleEditDto>>, CollectionRequestValidator<PayeeMatchingRuleEditDto>>();

        // Add FluentValidation to ASP.NET Core model binding pipeline.
        // This causes automatic validation at the controller boundary BEFORE controller actions execute.
        // Invalid DTOs will return 400 Bad Request with validation error details, preventing
        // invalid data from reaching controller methods or the Application layer.
        services.AddFluentValidationAutoValidation();

        // Add version information to the service collection for later use in controllers or middleware.
        services.Configure<VersionOptions>(options => options.Version = version);

        // Add tenancy seeders
        services.AddSeedStage<TenantSeedStage>();
        services.AddSeedStage<TenantRoleSeedStage>();

        // Customize validation responses with a trace ID for diagnostics.
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
