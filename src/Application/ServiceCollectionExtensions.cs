using Microsoft.Extensions.DependencyInjection;
using YoFi.V3.Application.Features;

namespace YoFi.V3.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationFeatures(this IServiceCollection services)
    {
        // Auto-register all features
        services.Scan(scan => scan
            .FromAssemblyOf<WeatherFeature>()
            .AddClasses(classes => classes.Where(type => type.Name.EndsWith("Feature")))
            .AsSelf()
            .WithScopedLifetime());

        return services;
    }
}
