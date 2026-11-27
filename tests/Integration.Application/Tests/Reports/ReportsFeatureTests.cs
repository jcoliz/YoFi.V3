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
    public async Task GetAvailableYearsAsync_QueriesDistinctTenantDateYearsIncludingCalendarBoundaries()
    {
        var currentTenant = await CreateTenantAsync("Report years tenant");
        var otherTenant = await CreateTenantAsync("Other years tenant");
        var feature = new ReportsFeature(
            new FixedTenantProvider(currentTenant),
            _dataProvider,
            new FixedTimeProvider());
        _context.Transactions.AddRange(
            CreateTransaction(currentTenant.Id, new DateOnly(2035, 1, 1), ("Income:Salary", 10m)),
            CreateTransaction(currentTenant.Id, new DateOnly(2035, 12, 31), ("Home:Food", -5m)),
            CreateTransaction(currentTenant.Id, new DateOnly(2038, 12, 31), ("", 0m)),
            CreateTransaction(currentTenant.Id, new DateOnly(2042, 1, 1), ("Transfer:Internal", 20m)),
            CreateTransaction(otherTenant.Id, new DateOnly(2039, 6, 15), ("Home:OtherTenant", 100m)));
        await _context.SaveChangesAsync();

        var result = await feature.GetAvailableYearsAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Years, Is.EqualTo(new[] { 2042, 2040, 2038, 2035 }));
            Assert.That(result.DefaultYear, Is.EqualTo(2040));
        }
    }

    [Test]
    public async Task GetAvailableYearsAsync_EmptyTenantReturnsCurrentYearEvenWhenOtherTenantHasData()
    {
        var currentTenant = await CreateTenantAsync("Empty report years tenant");
        var otherTenant = await CreateTenantAsync("Populated years tenant");
        var feature = new ReportsFeature(
            new FixedTenantProvider(currentTenant),
            _dataProvider,
            new FixedTimeProvider());
        _context.Transactions.Add(
            CreateTransaction(otherTenant.Id, new DateOnly(2035, 1, 1), ("Income:Salary", 10m)));
        await _context.SaveChangesAsync();

        var result = await feature.GetAvailableYearsAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Years, Is.EqualTo(new[] { 2040 }));
            Assert.That(result.DefaultYear, Is.EqualTo(2040));
        }
    }

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

    [Test]
    public async Task GetReportAsync_AppliesDisplayOverridesWithoutChangingTenantScopedAnnualTotals()
    {
        var currentTenant = await CreateTenantAsync("Display override tenant");
        var otherTenant = await CreateTenantAsync("Other display override tenant");
        var feature = new ReportsFeature(
            new FixedTenantProvider(currentTenant),
            _dataProvider,
            TimeProvider.System);

        // Given: Three category levels in the current tenant and matching data in another tenant
        _context.Transactions.AddRange(
            CreateTransaction(currentTenant.Id, new DateOnly(2024, 1, 15),
                ("Home:Utilities:Electric", 10m)),
            CreateTransaction(currentTenant.Id, new DateOnly(2024, 2, 15),
                ("Home:Utilities:Gas", 15m)),
            CreateTransaction(currentTenant.Id, new DateOnly(2024, 3, 15),
                ("Home:Repairs", 5m)),
            CreateTransaction(otherTenant.Id, new DateOnly(2024, 6, 15),
                ("Home:Utilities:Electric", 1000m)));
        await _context.SaveChangesAsync();

        // When: Requesting depth 3 with months hidden, then depth 1 with months shown
        var depthThree = await feature.GetReportAsync("all-detail", 2024, 3, false);
        var depthOne = await feature.GetReportAsync("all-detail", 2024, 1, true);

        // Then: Effective row shape and monthly settings are applied without cross-tenant roll-ups
        using (Assert.EnterMultipleScope())
        {
            Assert.That(depthThree.DisplayDepth, Is.EqualTo(3));
            Assert.That(depthThree.HasMonthColumns, Is.False);
            Assert.That(depthThree.Rows.Select(row => row.CategoryPath), Is.EqualTo(new[]
            {
                "Home", "Home:Repairs", "Home:Utilities",
                "Home:Utilities:Electric", "Home:Utilities:Gas"
            }));
            Assert.That(depthThree.Rows.Single(row => row.CategoryPath == "Home").IsSubtotal, Is.True);
            Assert.That(depthThree.Rows.Single(row => row.CategoryPath == "Home:Utilities").IsSubtotal, Is.True);
            Assert.That(depthThree.Rows.Where(row => row.CategoryName is "Electric" or "Gas" or "Repairs")
                .All(row => row.MonthlyAmounts is null), Is.True);
            Assert.That(depthThree.Totals.AnnualTotal, Is.EqualTo(30m));
            Assert.That(depthThree.Totals.MonthlyAmounts, Is.Null);

            Assert.That(depthOne.DisplayDepth, Is.EqualTo(1));
            Assert.That(depthOne.HasMonthColumns, Is.True);
            Assert.That(depthOne.Rows.Select(row => row.CategoryPath), Is.EqualTo(new[] { "Home" }));
            Assert.That(depthOne.Rows[0].IsSubtotal, Is.False);
            Assert.That(depthOne.Rows[0].AnnualTotal, Is.EqualTo(30m));
            Assert.That(depthOne.Rows[0].MonthlyAmounts, Is.Not.Null);
            Assert.That(depthOne.Rows[0].MonthlyAmounts![0], Is.EqualTo(10m));
            Assert.That(depthOne.Rows[0].MonthlyAmounts![1], Is.EqualTo(15m));
            Assert.That(depthOne.Rows[0].MonthlyAmounts![2], Is.EqualTo(5m));
            Assert.That(depthOne.Totals.AnnualTotal, Is.EqualTo(30m));
            Assert.That(depthOne.Totals.MonthlyAmounts![0], Is.EqualTo(10m));
            Assert.That(depthOne.Totals.MonthlyAmounts![1], Is.EqualTo(15m));
            Assert.That(depthOne.Totals.MonthlyAmounts![2], Is.EqualTo(5m));
        }
    }

    [Test]
    public async Task GetReportAsync_DefaultsDisplaySettingsFromDefinition()
    {
        var tenant = await CreateTenantAsync("Report display default tenant");
        var feature = new ReportsFeature(
            new FixedTenantProvider(tenant),
            _dataProvider,
            TimeProvider.System);

        // Given: A tenant with data for a report that defaults to category depth 2 and monthly columns
        _context.Transactions.Add(
            CreateTransaction(tenant.Id, new DateOnly(2024, 4, 15),
                ("Income:Salary:Primary", 100m)));
        await _context.SaveChangesAsync();

        // When: Omitting display query parameters
        var monthlyDetail = await feature.GetReportAsync("income-detail", 2024);
        var annualSummary = await feature.GetReportAsync("income", 2024);

        // Then: Each result uses its definition's depth and month defaults
        using (Assert.EnterMultipleScope())
        {
            Assert.That(monthlyDetail.DisplayDepth, Is.EqualTo(2));
            Assert.That(monthlyDetail.HasMonthColumns, Is.True);
            Assert.That(monthlyDetail.Rows.Select(row => row.CategoryPath), Is.EqualTo(new[]
            {
                "Income:Salary", "Income:Salary:Primary"
            }));
            Assert.That(monthlyDetail.Rows.All(row => row.MonthlyAmounts is not null), Is.True);

            Assert.That(annualSummary.DisplayDepth, Is.EqualTo(1));
            Assert.That(annualSummary.HasMonthColumns, Is.False);
            Assert.That(annualSummary.Rows.Select(row => row.CategoryPath), Is.EqualTo(new[] { "Income:Salary" }));
            Assert.That(annualSummary.Rows.All(row => row.MonthlyAmounts is null), Is.True);
            Assert.That(annualSummary.Totals.MonthlyAmounts, Is.Null);
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

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
            => new(2040, 7, 1, 0, 0, 0, TimeSpan.Zero);
    }
}
