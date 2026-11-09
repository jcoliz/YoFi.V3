using YoFi.V3.Application.Features;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Tenancy.Models;
using YoFi.V3.Entities.Tenancy.Providers;
using YoFi.V3.Tests.Integration.Application.TestHelpers;

namespace YoFi.V3.Tests.Integration.Application;

[TestFixture]
public class ReportsFeatureTests : FeatureTestBase
{
    [Test]
    public async Task GetReportAsync_FiltersByTenantAndIncludesBothCalendarYearBoundariesAndAllSplits()
    {
        var currentTenant = await CreateTenantAsync("Report tenant");
        var otherTenant = await CreateTenantAsync("Other tenant");
        var feature = new ReportsFeature(
            new FixedTenantProvider(currentTenant),
            _dataProvider,
            TimeProvider.System);

        _context.Transactions.AddRange(
            CreateTransaction(currentTenant.Id, new DateOnly(2024, 1, 1),
                ("Home:Food", 10m), ("Home:Supplies", 5m)),
            CreateTransaction(currentTenant.Id, new DateOnly(2024, 12, 31),
                ("Income:Salary", 20m)),
            CreateTransaction(currentTenant.Id, new DateOnly(2023, 12, 31),
                ("Home:OutsideYear", 100m)),
            CreateTransaction(currentTenant.Id, new DateOnly(2025, 1, 1),
                ("Home:OutsideYear", 100m)),
            CreateTransaction(otherTenant.Id, new DateOnly(2024, 6, 15),
                ("Home:OtherTenant", 1000m)));
        await _context.SaveChangesAsync();

        var result = await feature.GetReportAsync("all-detail", 2024);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Totals.AnnualTotal, Is.EqualTo(35m));
            Assert.That(result.Totals.MonthlyAmounts![0], Is.EqualTo(15m));
            Assert.That(result.Totals.MonthlyAmounts[11], Is.EqualTo(20m));
            Assert.That(result.Rows.Select(row => row.CategoryPath),
                Is.EqualTo(new[] { "Home", "Home:Supplies", "Home:Food", "Income", "Income:Salary" }));
            Assert.That(result.Rows.Single(row => row.CategoryPath == "Home").AnnualTotal, Is.EqualTo(15m));
        }
    }

    private async Task<Tenant> CreateTenantAsync(string name)
    {
        var tenant = new Tenant { Name = name, Description = name };
        _context.Tenants.Add(tenant);
        await _context.SaveChangesAsync();
        return tenant;
    }

    private static Transaction CreateTransaction(
        long tenantId,
        DateOnly date,
        params (string Category, decimal Amount)[] splits)
        => new()
        {
            TenantId = tenantId,
            Date = date,
            Payee = "Report test transaction",
            Amount = splits.Sum(split => split.Amount),
            Splits = splits.Select((split, index) => new Split
            {
                Amount = split.Amount,
                Category = split.Category,
                Order = index
            }).ToList()
        };

    private sealed class FixedTenantProvider(Tenant currentTenant) : ITenantProvider
    {
        public Tenant CurrentTenant { get; } = currentTenant;
    }
}
