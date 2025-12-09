using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using YoFi.V3.Application.Dto;
using YoFi.V3.Data;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Tenancy;

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

/// <summary>
/// Custom WebApplicationFactory that sets up test data with a tenant and transactions
/// </summary>
public class CustomTenantWebApplicationFactory : WebApplicationFactory<Program>
{
    public const int ExpectedTransactionCount = 5;
    public Guid TestTenantKey { get; private set; }
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Configure app configuration with minimal required settings to ensure startup succeeds
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Application:Version"] = "test-version",
                ["Application:Environment"] = "Local",
                ["Application:AllowedCorsOrigins:0"] = "http://localhost:3000",
                // Use a temporary file-based SQLite database for testing
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={_dbPath}"
            });
        });
    }

    public async Task SeedTestDataAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Database was already created by PrepareDatabaseAsync in Program.cs
        // Just seed test data

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

        TestTenantKey = testTenant.Key;

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

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            // Clean up the temporary database file
            try
            {
                if (File.Exists(_dbPath))
                {
                    File.Delete(_dbPath);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
