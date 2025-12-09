using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace YoFi.V3.Tests.Integration.Controller.TestHelpers;

/// <summary>
/// Base WebApplicationFactory with common test configuration
/// </summary>
public class BaseTestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath;
    private readonly Dictionary<string, string?> _configurationOverrides;

    public BaseTestWebApplicationFactory(
        Dictionary<string, string?>? configurationOverrides = null,
        string? dbPath = null)
    {
        _configurationOverrides = configurationOverrides ?? new Dictionary<string, string?>();
        _dbPath = dbPath ?? Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");

        // Set default configuration if not provided
        if (!_configurationOverrides.ContainsKey("Application:Version"))
            _configurationOverrides["Application:Version"] = "test-version";

        if (!_configurationOverrides.ContainsKey("Application:Environment"))
            _configurationOverrides["Application:Environment"] = "Local";

        if (!_configurationOverrides.ContainsKey("Application:AllowedCorsOrigins:0"))
            _configurationOverrides["Application:AllowedCorsOrigins:0"] = "http://localhost:3000";

        if (!_configurationOverrides.ContainsKey("ConnectionStrings:DefaultConnection"))
            _configurationOverrides["ConnectionStrings:DefaultConnection"] = $"Data Source={_dbPath}";
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(_configurationOverrides);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            // Clean up the temporary database file
            try
            {
                if (File.Exists(_dbPath))
                {
                    File.Delete(_dbPath);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
