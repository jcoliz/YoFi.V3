using System.Reflection;
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

        // Get app version, store in configuration for later use
        var assembly = Assembly.GetEntryAssembly();
        var resource = assembly!.GetManifestResourceNames().Where(x => x.EndsWith(".version.txt")).SingleOrDefault();
        if (resource is not null)
        {
            using var stream = assembly.GetManifestResourceStream(resource);
            using var streamreader = new StreamReader(stream!);
            var version = streamreader.ReadLine();
            builder.Configuration["Application:Version"] = version;
            logger.LogInformation(21,"Version: {version}", version);
        }

        return builder;
    }
}
