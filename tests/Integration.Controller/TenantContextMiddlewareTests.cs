using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
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
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        // And: Response should be a problem details with title "Tenant not found"
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problemDetails, Is.Not.Null);
        Assert.That(problemDetails!.Title, Is.EqualTo("Tenant not found"));
    }

    [Test]
    public async Task GetTransactions_MultipleTenantsInDatabase_ReturnsOnlyRequestedTenantTransactions()
    {
        // Given: Multiple tenants in the database, each with their own transactions
        Guid tenant1Key, tenant2Key;
        int tenant1TransactionCount = 3;
        int tenant2TransactionCount = 4;

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Create first tenant with transactions
            var tenant1 = new Tenant
            {
                Key = Guid.NewGuid(),
                Name = "Tenant 1",
                Description = "First test tenant",
                CreatedAt = DateTimeOffset.UtcNow
            };
            dbContext.Set<Tenant>().Add(tenant1);
            await dbContext.SaveChangesAsync();
            tenant1Key = tenant1.Key;

            for (int i = 1; i <= tenant1TransactionCount; i++)
            {
                dbContext.Set<Transaction>().Add(new Transaction
                {
                    Key = Guid.NewGuid(),
                    TenantId = tenant1.Id,
                    Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-i)),
                    Payee = $"Tenant1 Payee {i}",
                    Amount = 50.00m * i
                });
            }

            // Create second tenant with transactions
            var tenant2 = new Tenant
            {
                Key = Guid.NewGuid(),
                Name = "Tenant 2",
                Description = "Second test tenant",
                CreatedAt = DateTimeOffset.UtcNow
            };
            dbContext.Set<Tenant>().Add(tenant2);
            await dbContext.SaveChangesAsync();
            tenant2Key = tenant2.Key;

            for (int i = 1; i <= tenant2TransactionCount; i++)
            {
                dbContext.Set<Transaction>().Add(new Transaction
                {
                    Key = Guid.NewGuid(),
                    TenantId = tenant2.Id,
                    Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-i)),
                    Payee = $"Tenant2 Payee {i}",
                    Amount = 75.00m * i
                });
            }

            await dbContext.SaveChangesAsync();
        }

        // When: API Client requests transactions for tenant 1
        var response1 = await _client.GetAsync($"/api/tenant/{tenant1Key}/transactions");
        Assert.That(response1.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var transactions1 = await response1.Content.ReadFromJsonAsync<List<TransactionResultDto>>();
        Assert.That(transactions1, Is.Not.Null);

        // Then: Only tenant 1's transactions are returned
        Assert.That(transactions1, Has.Count.EqualTo(tenant1TransactionCount));
        Assert.That(transactions1.All(t => t.Payee.StartsWith("Tenant1 Payee")), Is.True);

        // When: API Client requests transactions for tenant 2
        var response2 = await _client.GetAsync($"/api/tenant/{tenant2Key}/transactions");
        Assert.That(response2.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var transactions2 = await response2.Content.ReadFromJsonAsync<List<TransactionResultDto>>();
        Assert.That(transactions2, Is.Not.Null);

        // Then: Only tenant 2's transactions are returned
        Assert.That(transactions2, Has.Count.EqualTo(tenant2TransactionCount));
        Assert.That(transactions2.All(t => t.Payee.StartsWith("Tenant2 Payee")), Is.True);
    }
}
