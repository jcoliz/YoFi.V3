namespace YoFi.V3.Tests.Integration.Controller.TestHelpers;

/// <summary>
/// Custom WebApplicationFactory that injects a specific version and environment for testing
/// </summary>
public class CustomVersionWebApplicationFactory : BaseTestWebApplicationFactory
{
    // TODO: This is no longer an effective way to inject version and environment for testing.
    public CustomVersionWebApplicationFactory(string version, string environment)
        : base(
            configurationOverrides: new Dictionary<string, string?>
            {
                ["Version"] = version
            },
            dbPath: null,
            environment: environment)
    {
    }
}
