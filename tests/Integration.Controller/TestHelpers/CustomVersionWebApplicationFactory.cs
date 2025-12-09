using YoFi.V3.Entities.Options;

namespace YoFi.V3.Tests.Integration.Controller.TestHelpers;

/// <summary>
/// Custom WebApplicationFactory that injects a specific version for testing
/// </summary>
public class CustomVersionWebApplicationFactory : BaseTestWebApplicationFactory
{
    public CustomVersionWebApplicationFactory(string version, EnvironmentType environment)
        : base(new Dictionary<string, string?>
        {
            ["Application:Version"] = version,
            ["Application:Environment"] = environment.ToString()
        })
    {
    }
}
