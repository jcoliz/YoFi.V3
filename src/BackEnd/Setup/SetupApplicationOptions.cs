using System.Reflection;
using YoFi.V3.BackEnd.Setup;
using YoFi.V3.Entities.Options;

namespace YoFi.V3.BackEnd.Startup;

public static class __SetupApplicationOptions
{
    /// <summary>
    /// Get app version, store in configuration for later use
    /// </summary>
    public static WebApplicationBuilder AddApplicationOptions(this WebApplicationBuilder builder, ILogger logger)
    {
        builder.Services.Configure<ApplicationOptions>(builder.Configuration.GetSection(ApplicationOptions.Section));

        // Get app version from assembly attribute
        var assembly = Assembly.GetEntryAssembly();
        var version = assembly?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "unknown";

        builder.Configuration["Application:Version"] = version;
        logger.LogVersion(version);

        return builder;
    }
}
