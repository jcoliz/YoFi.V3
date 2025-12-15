using Microsoft.Extensions.DependencyInjection;
using YoFi.V3.Controllers.Middleware;

namespace YoFi.V3.Controllers;

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

        return services;
    }
}
