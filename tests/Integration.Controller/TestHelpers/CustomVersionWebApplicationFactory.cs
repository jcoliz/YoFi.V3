using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using YoFi.V3.Entities.Options;

namespace YoFi.V3.Tests.Integration.Controller.TestHelpers;

/// <summary>
/// Custom WebApplicationFactory that injects a specific version for testing
/// </summary>
public class CustomVersionWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _version;
    private readonly EnvironmentType _environment;

    public CustomVersionWebApplicationFactory(string version, EnvironmentType environment)
    {
        _version = version;
        _environment = environment;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Add in-memory configuration to override the application settings
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Application:Version"] = _version,
                ["Application:Environment"] = _environment.ToString()
            });
        });
    }
}
