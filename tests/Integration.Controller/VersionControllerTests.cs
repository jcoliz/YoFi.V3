using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using YoFi.V3.Entities.Options;

namespace YoFi.V3.Tests.Integration.Controller;

[TestFixture]
public class VersionControllerTests
{
    private CustomVersionWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private const string TestVersion = "1.2.3-test";
    private const EnvironmentType TestEnvironment = EnvironmentType.Local;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new CustomVersionWebApplicationFactory(TestVersion, TestEnvironment);
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task GetVersion_ReturnsSuccessAndVersion()
    {
        // Act
        var response = await _client.GetAsync("/version");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var version = await response.Content.ReadAsStringAsync();
        Assert.That(version, Is.Not.Null);
        Assert.That(version, Is.Not.Empty);
    }

    [Test]
    public async Task GetVersion_ReturnsConfiguredVersion()
    {
        // Act
        var response = await _client.GetAsync("/version");
        var version = await response.Content.ReadFromJsonAsync<string>();

        // Assert
        Assert.That(response.IsSuccessStatusCode, Is.True);
        Assert.That(version, Does.Contain(TestVersion));
    }

    [Test]
    public async Task GetVersion_ReturnsVersionWithLocalEnvironment()
    {
        // Act
        var response = await _client.GetAsync("/version");
        var version = await response.Content.ReadFromJsonAsync<string>();

        // Assert
        Assert.That(response.IsSuccessStatusCode, Is.True);

        // Should include the test version and environment suffix
        Assert.That(version, Is.EqualTo($"{TestVersion} (Local)"));
    }

    [Test]
    public async Task GetVersion_ReturnsJsonContentType()
    {
        // Act
        var response = await _client.GetAsync("/version");

        // Assert
        Assert.That(response.Content.Headers.ContentType?.MediaType,
            Is.EqualTo("application/json"));
    }

    [TestCase(EnvironmentType.Production, "1.2.3-test", ExpectedResult = "1.2.3-test")]
    [TestCase(EnvironmentType.Local, "1.2.3-test", ExpectedResult = "1.2.3-test (Local)")]
    [TestCase(EnvironmentType.Container, "1.2.3-test", ExpectedResult = "1.2.3-test (Container)")]
    [TestCase(EnvironmentType.Production, "2.0.0", ExpectedResult = "2.0.0")]
    [TestCase(EnvironmentType.Local, "2.0.0", ExpectedResult = "2.0.0 (Local)")]
    [TestCase(EnvironmentType.Container, "2.0.0", ExpectedResult = "2.0.0 (Container)")]
    public async Task<string> GetVersion_AllEnvironmentTypes_ReturnsCorrectFormat(
        EnvironmentType environment,
        string version)
    {
        // Arrange
        using var factory = new CustomVersionWebApplicationFactory(version, environment);
        using var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/version");
        var result = await response.Content.ReadFromJsonAsync<string>();

        // Assert
        Assert.That(response.IsSuccessStatusCode, Is.True);
        return result ?? string.Empty;
    }
}

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
