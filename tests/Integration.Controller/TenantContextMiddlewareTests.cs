using System.Net;
using Microsoft.AspNetCore.Mvc;
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
    private Guid _firstTransactionKey;
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
                var transaction = new Transaction
                {
                    Key = Guid.NewGuid(),
                    TenantId = testTenant.Id,
                    Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-i)),
                    Payee = $"Test Payee {i}",
                    Amount = 100.00m * i
                };
                transactions.Add(transaction);

                // Store the first transaction key for single transaction tests
                if (i == 1)
                {
                    _firstTransactionKey = transaction.Key;
                }
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

    #region Helper Methods

    private async Task<(Guid tenantKey, long tenantId)> CreateTenantAsync(string name, string description)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var tenant = new Tenant
        {
            Key = Guid.NewGuid(),
            Name = name,
            Description = description,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Set<Tenant>().Add(tenant);
        await dbContext.SaveChangesAsync();

        return (tenant.Key, tenant.Id);
    }

    private async Task<Guid> CreateTransactionAsync(long tenantId, string payee, decimal amount, int daysAgo = 0)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var transaction = new Transaction
        {
            Key = Guid.NewGuid(),
            TenantId = tenantId,
            Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-daysAgo)),
            Payee = payee,
            Amount = amount
        };

        dbContext.Set<Transaction>().Add(transaction);
        await dbContext.SaveChangesAsync();

        return transaction.Key;
    }

    private async Task CreateTransactionsAsync(long tenantId, string payeePrefix, int count, decimal baseAmount = 100.00m)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var transactions = new List<Transaction>();
        for (int i = 1; i <= count; i++)
        {
            transactions.Add(new Transaction
            {
                Key = Guid.NewGuid(),
                TenantId = tenantId,
                Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-i)),
                Payee = $"{payeePrefix} {i}",
                Amount = baseAmount * i
            });
        }

        dbContext.Set<Transaction>().AddRange(transactions);
        await dbContext.SaveChangesAsync();
    }

    #endregion

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
        int tenant1TransactionCount = 3;
        int tenant2TransactionCount = 4;

        var (tenant1Key, tenant1Id) = await CreateTenantAsync("Tenant 1", "First test tenant");
        await CreateTransactionsAsync(tenant1Id, "Tenant1 Payee", tenant1TransactionCount, 50.00m);

        var (tenant2Key, tenant2Id) = await CreateTenantAsync("Tenant 2", "Second test tenant");
        await CreateTransactionsAsync(tenant2Id, "Tenant2 Payee", tenant2TransactionCount, 75.00m);

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

    [Test]
    public async Task GetTransactionById_ValidTenantAndTransaction_ReturnsTransaction()
    {
        // Given: One tenant with multiple transactions in the database
        // (Setup already done in OneTimeSetUp)

        // When: API Client requests a specific transaction by key
        var response = await _client.GetAsync($"/api/tenant/{_testTenantKey}/transactions/{_firstTransactionKey}");

        // Then: Transaction is returned successfully
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var transaction = await response.Content.ReadFromJsonAsync<TransactionResultDto>();
        Assert.That(transaction, Is.Not.Null);
        Assert.That(transaction!.Payee, Is.EqualTo("Test Payee 1"));
        Assert.That(transaction.Amount, Is.EqualTo(100.00m));
    }

    [Test]
    public async Task GetTransactionById_NonExistentTransaction_Returns404()
    {
        // Given: One tenant in the database
        // (Setup already done in OneTimeSetUp)

        // When: API Client requests a transaction that does not exist
        var nonExistentTransactionKey = Guid.NewGuid();
        var response = await _client.GetAsync($"/api/tenant/{_testTenantKey}/transactions/{nonExistentTransactionKey}");

        // Then: 404 Not Found should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        // And: Response should be a problem details with title "Transaction Not Found"
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problemDetails, Is.Not.Null);
        Assert.That(problemDetails!.Title, Is.EqualTo("Transaction Not Found"));
    }

    [Test]
    public async Task GetTransactionById_TransactionExistsInDifferentTenant_Returns404()
    {
        // Given: Two tenants, each with their own transactions
        var (tenant1Key, tenant1Id) = await CreateTenantAsync("Cross Tenant Test - Tenant 1", "First tenant for cross-tenant access test");
        await CreateTransactionAsync(tenant1Id, "Tenant1 Transaction", 100.00m);

        var (_, tenant2Id) = await CreateTenantAsync("Cross Tenant Test - Tenant 2", "Second tenant for cross-tenant access test");
        var tenant2TransactionKey = await CreateTransactionAsync(tenant2Id, "Tenant2 Transaction", 200.00m);

        // When: API Client attempts to access Tenant 2's transaction using Tenant 1's context
        var response = await _client.GetAsync($"/api/tenant/{tenant1Key}/transactions/{tenant2TransactionKey}");

        // Then: 404 Not Found should be returned (transaction should not be accessible from wrong tenant)
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        // And: Response should be a problem details with title "Transaction Not Found"
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problemDetails, Is.Not.Null);
        Assert.That(problemDetails!.Title, Is.EqualTo("Transaction Not Found"));
    }
}
