using System.Reflection;

namespace YoFi.V3.BackEnd.Startup;

public static class __SetupVersion
{
    /// <summary>
    /// Get app version, store in configuration for later use
    /// </summary>
    public static WebApplicationBuilder AddVersion(this WebApplicationBuilder builder /*TODO:, ILogger logger*/)
    {
        // Get app version, store in configuration for later use
        var assembly = Assembly.GetEntryAssembly();
        var resource = assembly!.GetManifestResourceNames().Where(x => x.EndsWith(".version.txt")).SingleOrDefault();
        if (resource is not null)
        {
            using var stream = assembly.GetManifestResourceStream(resource);
            using var streamreader = new StreamReader(stream!);
            var version = streamreader.ReadLine();
            builder.Configuration["Startup:Version"] = version;
            //TODO: logger.LogInformation("Version: {version}", version);
        }

        return builder;
    }
}
