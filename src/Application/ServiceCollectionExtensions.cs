using Microsoft.Extensions.DependencyInjection;
using YoFi.V3.Application.Features;
using YoFi.V3.Application.Import.Services;

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

        // Register import services
        services.AddScoped<IOfxParsingService, OfxParsingService>();

        return services;
    }
}
