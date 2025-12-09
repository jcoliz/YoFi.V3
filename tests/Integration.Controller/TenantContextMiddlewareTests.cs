using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using YoFi.V3.Application.Dto;
using YoFi.V3.Data;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Tenancy;
using YoFi.V3.Tests.Integration.Controller.TestHelpers;

namespace YoFi.V3.Tests.Integration.Controller;

[TestFixture]
public class TenantContextMiddlewareTests
{
    private BaseTestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;
    private Guid _testTenantKey;
    private const int ExpectedTransactionCount = 5;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _factory = new BaseTestWebApplicationFactory();

        // Given: One tenant in the database
        // And: Multiple transactions in the database which are in that tenant
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Given: One tenant in the database
            var testTenant = new Tenant
            {
                Key = Guid.NewGuid(),
                Name = "Test Tenant",
                Description = "Test tenant for middleware testing",
                CreatedAt = DateTimeOffset.UtcNow
            };

            dbContext.Set<Tenant>().Add(testTenant);
            await dbContext.SaveChangesAsync();

            _testTenantKey = testTenant.Key;

            // And: Multiple transactions in the database which are in that tenant
            var transactions = new List<Transaction>();
            for (int i = 1; i <= ExpectedTransactionCount; i++)
            {
                transactions.Add(new Transaction
                {
                    Key = Guid.NewGuid(),
                    TenantId = testTenant.Id,
                    Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-i)),
                    Payee = $"Test Payee {i}",
                    Amount = 100.00m * i
                });
            }

            dbContext.Set<Transaction>().AddRange(transactions);
            await dbContext.SaveChangesAsync();
        }

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
        Assert.That(transactions, Has.Count.EqualTo(ExpectedTransactionCount));

        // Verify all transactions have expected data
        Assert.That(transactions.All(t => t.Payee.StartsWith("Test Payee")), Is.True);
        Assert.That(transactions.All(t => t.Amount > 0), Is.True);
    }

    [Test]
    public async Task GetTransactions_NonExistentTenant_Returns404()
    {
        // Given: One tenant in the database
        // (Setup already done in OneTimeSetUp)

        // When: API Client requests transactions for a tenant that does not exist
        var nonExistentTenantKey = Guid.NewGuid();
        var response = await _client.GetAsync($"/api/tenant/{nonExistentTenantKey}/transactions");

        // Then: 404 Not Found should be returned
        // NOTE: Currently fails - returns 500 Internal Server Error because
        // TenantContext.SetCurrentTenantAsync() throws InvalidOperationException
        // when the tenant is not found.
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
}
