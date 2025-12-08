using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace YoFi.V3.Tests.Integration.Controller;

[TestFixture]
public class VersionControllerTests
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new WebApplicationFactory<Program>();
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
    public async Task GetVersion_ReturnsVersionWithEnvironment()
    {
        // Act
        var response = await _client.GetAsync("/version");
        var version = await response.Content.ReadAsStringAsync();

        // Assert
        // Version should contain either (Local), (Container), or just the version number
        Assert.That(response.IsSuccessStatusCode, Is.True);
        Assert.That(version, Does.Contain(".").Or.Contain("Local").Or.Contain("Container"));
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
}
