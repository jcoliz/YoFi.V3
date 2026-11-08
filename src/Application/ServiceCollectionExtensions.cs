using Microsoft.Extensions.DependencyInjection;
using YoFi.V3.Application.Features;
using YoFi.V3.Application.Services;

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

        // Register PayeeMatchingRuleFeature as IPayeeMatchingService for bank import integration
        services.AddScoped<IPayeeMatchingService, PayeeMatchingRuleFeature>();

        // Register application services
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IRegexValidationService, RegexValidationService>();
        services.AddSingleton(new SampleScenarioLoader());
        services.AddSingleton<SampleDataGenerator>();
        services.AddScoped<DataImportService>();
        services.AddScoped<IWorkbookImportFormatDetector, OriginalYoFiWorkbookFormatDetector>();
        services.AddScoped<IImportWorksheetHandler, OriginalYoFiPayeeWorksheetHandler>();

        // Register memory cache for features that need caching (e.g., PayeeMatchingRuleFeature)
        services.AddMemoryCache();

        return services;
    }
}
