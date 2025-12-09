using System.Net;
using Microsoft.AspNetCore.Mvc;
using YoFi.V3.Tests.Integration.Controller.TestHelpers;

namespace YoFi.V3.Tests.Integration.Controller;

[TestFixture]
public class TransactionsControllerTests
{
    private BaseTestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new BaseTestWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task GetTransactions_InvalidTenantIdFormat_Returns404WithProblemDetails()
    {
        // Given: A request with an invalid tenant ID format (not a valid GUID)

        // When: API Client requests transactions with invalid tenant ID format
        var response = await _client.GetAsync("/api/tenant/1/transactions");

        // Then: 404 Not Found should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        // And: Response should contain problem details (not empty body)
        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Is.Not.Empty, "Response body should not be empty");

        // And: Response should be valid problem details JSON
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problemDetails, Is.Not.Null, "Response should be deserializable as ProblemDetails");
        Assert.That(problemDetails!.Status, Is.EqualTo(404));
    }

    [Test]
    public async Task GetTransactionById_InvalidTenantIdFormat_Returns404WithProblemDetails()
    {
        // Given: A request with an invalid tenant ID format (not a valid GUID)
        var validTransactionKey = Guid.NewGuid();

        // When: API Client requests a transaction with invalid tenant ID format
        var response = await _client.GetAsync($"/api/tenant/invalid-guid/transactions/{validTransactionKey}");

        // Then: 404 Not Found should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        // And: Response should contain problem details (not empty body)
        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Is.Not.Empty, "Response body should not be empty");

        // And: Response should be valid problem details JSON
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problemDetails, Is.Not.Null, "Response should be deserializable as ProblemDetails");
        Assert.That(problemDetails!.Status, Is.EqualTo(404));
    }

    [Test]
    public async Task GetTransactionById_InvalidTransactionIdFormat_Returns404WithProblemDetails()
    {
        // Given: A request with an invalid transaction ID format (not a valid GUID)
        var validTenantKey = Guid.NewGuid();

        // When: API Client requests a transaction with invalid transaction ID format
        var response = await _client.GetAsync($"/api/tenant/{validTenantKey}/transactions/not-a-guid");

        // Then: 404 Not Found should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        // And: Response should contain problem details (not empty body)
        var content = await response.Content.ReadAsStringAsync();
        Assert.That(content, Is.Not.Empty, "Response body should not be empty");

        // And: Response should be valid problem details JSON
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problemDetails, Is.Not.Null, "Response should be deserializable as ProblemDetails");
        Assert.That(problemDetails!.Status, Is.EqualTo(404));
    }
}
