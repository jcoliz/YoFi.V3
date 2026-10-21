using DotNetNuxt.AspNetCore.Options;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace YoFi.V3.Tests.Integration.Controller.TestHelpers;

/// <summary>
/// WebApplicationFactory that overrides the application version reported by the version endpoint,
/// and captures log output for verification.
/// </summary>
/// <param name="version">Version to report, or null to simulate an unknown version</param>
public class CustomVersionWebApplicationFactory(string? version) : BaseTestWebApplicationFactory
{
    /// <summary>
    /// Log entries emitted by the application under test
    /// </summary>
    public CapturingLoggerProvider Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            // PostConfigure runs after the application's Configure<VersionOptions>, so the test value wins
            services.PostConfigure<VersionOptions>(options => options.Version = version);
            services.AddSingleton<ILoggerProvider>(Logs);
        });
    }
}
