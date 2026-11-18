using Microsoft.Extensions.DependencyInjection;
using YoFi.V3.Application.Features;
using YoFi.V3.Application.Services;

namespace YoFi.V3.Application;

/// <summary>Registers application features and application-layer services.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Adds features, persistence services, caches, and import/export handlers.</summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The configured service collection.</returns>
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
        services.AddScoped<IWorkbookImportFormatDetector, NativeYoFiV3WorkbookFormatDetector>();
        services.AddScoped<IImportWorksheetHandler, OriginalYoFiPayeeWorksheetHandler>();
        services.AddScoped<IImportWorksheetHandler, OriginalYoFiTransactionWorksheetHandler>();
        services.AddScoped<IImportWorksheetHandler, OriginalYoFiSplitWorksheetHandler>();
        services.AddScoped<IImportWorksheetHandler, NativeYoFiPayeeWorksheetHandler>();
        services.AddScoped<IImportWorksheetHandler, NativeYoFiTransactionWorksheetHandler>();
        services.AddScoped<IImportWorksheetHandler, NativeYoFiSplitWorksheetHandler>();
        services.AddScoped<IExportWorksheetWriter, TransactionExportWorksheetWriter>();
        services.AddScoped<IExportWorksheetWriter, PayeeMatchingRuleExportWorksheetWriter>();

        // Register memory cache for features that need caching (e.g., PayeeMatchingRuleFeature)
        services.AddMemoryCache();

        return services;
    }
}
