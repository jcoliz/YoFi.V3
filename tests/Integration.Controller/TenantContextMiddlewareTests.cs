using System.Net;
using System.Net.Http.Json;
using YoFi.V3.Application.Dto;
using YoFi.V3.Tests.Integration.Controller.TestHelpers;

namespace YoFi.V3.Tests.Integration.Controller;

[TestFixture]
public class TenantContextMiddlewareTests
{
    private CustomTenantWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _testTenantKey;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new CustomTenantWebApplicationFactory();

        // Setup test data BEFORE creating the client
        await _factory.SeedTestDataAsync();
        _testTenantKey = _factory.TestTenantKey;

        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task GetTransactions_OneTenantWithMultipleTransactions_ReturnsAllExpectedTransactions()
    {
        // Given: One tenant in the database
        // And: Multiple transactions in the database which are in that tenant
        // (Setup already done in OneTimeSetUp)

        // When: API Client requests transactions for that tenant
        var response = await _client.GetAsync($"/api/tenant/{_testTenantKey}/transactions");

        // Then: All expected transactions returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var transactions = await response.Content.ReadFromJsonAsync<List<TransactionResultDto>>();
        Assert.That(transactions, Is.Not.Null);
        Assert.That(transactions, Has.Count.EqualTo(CustomTenantWebApplicationFactory.ExpectedTransactionCount));

        // Verify all transactions have expected data
        Assert.That(transactions.All(t => t.Payee.StartsWith("Test Payee")), Is.True);
        Assert.That(transactions.All(t => t.Amount > 0), Is.True);
    }
}
