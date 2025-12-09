using Microsoft.Extensions.DependencyInjection;
using YoFi.V3.Data;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Tenancy;

namespace YoFi.V3.Tests.Integration.Controller.TestHelpers;

/// <summary>
/// Custom WebApplicationFactory that sets up test data with a tenant and transactions
/// </summary>
public class CustomTenantWebApplicationFactory : BaseTestWebApplicationFactory
{
    public const int ExpectedTransactionCount = 5;
    public Guid TestTenantKey { get; private set; }

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
}
