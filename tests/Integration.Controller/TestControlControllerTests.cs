using System.Net;
using YoFi.V3.Application.Dto;
using YoFi.V3.Tests.Integration.Controller.TestHelpers;

namespace YoFi.V3.Tests.Integration.Controller;

[TestFixture]
public class TestControlControllerTests
{
    [TestCase("/TestControl/errors", HttpStatusCode.OK)]
    [TestCase("/TestControl/errors/400", HttpStatusCode.BadRequest)]
    [TestCase("/TestControl/errors/401", HttpStatusCode.Unauthorized)]
    [TestCase("/TestControl/errors/403", HttpStatusCode.Forbidden)]
    [TestCase("/TestControl/errors/404", HttpStatusCode.NotFound)]
    [TestCase("/TestControl/errors/409", HttpStatusCode.Conflict)]
    [TestCase("/TestControl/errors/500", HttpStatusCode.InternalServerError)]
    [TestCase("/TestControl/pagination/strings", HttpStatusCode.OK)]
    public async Task GetErrorTestingEndpoint_Anonymous_ReturnsExpectedStatus(
        string path, HttpStatusCode expectedStatus)
    {
        using var factory = new BaseTestWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path);

        Assert.That(response.StatusCode, Is.EqualTo(expectedStatus));
    }

    [TestCase(1, HttpStatusCode.OK)]
    [TestCase(0, HttpStatusCode.BadRequest)]
    public async Task ValidateTransactions_Anonymous_ValidatesRequest(
        int amount, HttpStatusCode expectedStatus)
    {
        using var factory = new BaseTestWebApplicationFactory();
        using var client = factory.CreateClient();
        var request = new CollectionRequest<TransactionEditDto>(
            [new(DateOnly.FromDateTime(DateTime.UtcNow), amount, "Test Payee", null, null, null, null)]);

        using var response = await client.PostAsJsonAsync("/TestControl/validate/transactions", request);

        Assert.That(response.StatusCode, Is.EqualTo(expectedStatus));
    }

    [Test]
    public async Task Identify_Anonymous_ReturnsUnauthorized()
    {
        using var factory = new BaseTestWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsync("/TestControl/ident", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }
}
