using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YoFi.V3.Entities.Options;

namespace YoFi.V3.BackEnd.Setup;

/// <summary>
/// Extension methods for configuring CORS services.
/// </summary>
public static class SetupCors
{
    /// <summary>
    /// Adds CORS services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="applicationOptions">The application options containing CORS configuration.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCorsServices(
        this IServiceCollection services,
        ApplicationOptions applicationOptions,
        ILogger logger)
    {
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                if (applicationOptions.AllowedCorsOrigins.Length == 0)
                {
                    logger.LogCorsNotConfigured();
                }
                else
                {
                    policy.WithOrigins(applicationOptions.AllowedCorsOrigins)
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials();
                    logger.LogCorsConfigured(string.Join(", ", applicationOptions.AllowedCorsOrigins));
                }
            });
        });

        return services;
    }
}
