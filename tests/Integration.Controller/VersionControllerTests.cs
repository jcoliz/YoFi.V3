using System.Net;
using DotNetNuxt.AspNetCore.Controllers;
using Microsoft.Extensions.Logging;
using YoFi.V3.Tests.Integration.Controller.TestHelpers;

namespace YoFi.V3.Tests.Integration.Controller;

[TestFixture]
public class VersionControllerTests
{
    [TestCase("1.2.3-test")]
    [TestCase("2.0.0")]
    [TestCase("1.0.0-beta.4+abc1234")]
    [TestCase("development")]
    public async Task GetVersion_VersionConfigured_ReturnsVersion(string expectedVersion)
    {
        // Given: Application configured with a specific version
        using var factory = new CustomVersionWebApplicationFactory(expectedVersion);
        using var client = factory.CreateClient();

        // When: Requesting the version
        var response = await client.GetAsync("/version");

        // Then: Response is OK and contains exactly the configured version
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var actualVersion = await response.Content.ReadAsStringAsync();
        Assert.That(actualVersion, Is.EqualTo(expectedVersion));
    }

    [Test]
    public async Task GetVersion_NoVersion_ReturnsNoContentAndLogsWarning()
    {
        // Given: Application with no known version
        using var factory = new CustomVersionWebApplicationFactory(version: null);
        using var client = factory.CreateClient();

        // When: Requesting the version
        var response = await client.GetAsync("/version");

        // Then: Response is No Content
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        // And: A warning was logged by the version controller
        var warnings = factory.Logs.Entries
            .Where(e => e.Category == typeof(VersionController).FullName && e.Level == LogLevel.Warning)
            .ToList();
        Assert.That(warnings, Has.Count.EqualTo(1));
    }
}
