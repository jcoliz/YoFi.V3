using DotNetNuxt.Abstractions;
using Moq;
using YoFi.V3.Application.Features;
using YoFi.V3.Entities.Models;
using YoFi.V3.Tests.Unit.TestHelpers;

namespace YoFi.V3.Tests.Unit.Application;

[TestFixture]
public class ReportsFeatureTests
{
    private Mock<IDataProvider> _dataProvider = null!;
    private Mock<TimeProvider> _timeProvider = null!;
    private TestTenantProvider _tenantProvider = null!;
    private List<Transaction> _transactions = null!;
    private ReportsFeature _feature = null!;

    [SetUp]
    public void SetUp()
    {
        _transactions = [];
        _dataProvider = new Mock<IDataProvider>();
        _timeProvider = new Mock<TimeProvider>();
        _timeProvider.Setup(time => time.GetUtcNow())
            .Returns(new DateTimeOffset(2040, 7, 1, 0, 0, 0, TimeSpan.Zero));
        _tenantProvider = new TestTenantProvider();
        _tenantProvider.CurrentTenant.Id = 42;
        _dataProvider.Setup(data => data.Get<Transaction>())
            .Returns(() => _transactions.AsQueryable());
        _dataProvider.Setup(data =>
                data.ToListNoTrackingAsync(It.IsAny<IQueryable<ReportSplitAmount>>()))
            .Returns((IQueryable<ReportSplitAmount> query) => Task.FromResult(query.ToList()));
        _feature = new ReportsFeature(_tenantProvider, _dataProvider.Object, _timeProvider.Object);
    }

    [Test]
    public void GetAvailableReports_ReturnsTheBuiltInCatalogAndDisplayDefaults()
    {
        var reports = _feature.GetAvailableReports();

        Assert.That(reports.Select(report => report.Id), Is.EqualTo(new[]
        {
            "income", "taxes", "expenses", "savings", "transfers",
            "income-detail", "taxes-detail", "expenses-detail", "savings-detail",
            "all", "all-detail"
        }));
        Assert.That(reports.Select(report => report.DefaultDepth),
            Is.EqualTo(new[] { 1, 1, 1, 1, 1, 2, 2, 2, 2, 1, 2 }));
        Assert.That(reports.Select(report => report.HasMonthColumns),
            Is.EqualTo(new[] { false, false, false, false, false, true, true, true, false, false, true }));
        Assert.That(reports.Select(report => report.IncludesUncategorized),
            Is.EqualTo(new[] { false, false, true, false, false, false, false, true, false, true, true }));
    }

    [Test]
    public async Task GetReportAsync_FiltersReservedFamilyCaseInsensitivelyAtSegmentBoundary()
    {
        AddTransaction(2024, 1, ("Income:Salary", 100m), ("income:Bonus", -25m),
            ("IncomeTax:Refund", 200m), ("Taxes:Federal", 5m));

        var result = await _feature.GetReportAsync("income", 2024);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Totals.AnnualTotal, Is.EqualTo(75m));
            Assert.That(result.Rows.Select(row => row.CategoryPath),
                Is.EqualTo(new[] { "Income:Salary", "income:Bonus" }));
            Assert.That(result.Rows.Select(row => row.CategoryName),
                Is.EqualTo(new[] { "Salary", "Bonus" }));
            Assert.That(result.Rows.Select(row => row.AnnualTotal), Is.EqualTo(new[] { 100m, -25m }));
            Assert.That(result.Rows.Select(row => row.Percentage), Is.EqualTo(new[] { 4m / 3m, -1m / 3m }));
            Assert.That(result.Totals.MonthlyAmounts, Is.Null);
        }
    }

    [Test]
    public async Task GetReportAsync_ExpensesIncludesNonReservedCategoriesAndUncategorizedSplits()
    {
        AddTransaction(2024, 5, ("Food:Dining", 20m), ("Income:Salary", 100m),
            ("Taxes:Federal", 10m), ("Savings:Emergency", 30m), ("Transfer:Internal", 40m),
            ("", -5m), ("IncomeTax:Refund", 7m));

        var result = await _feature.GetReportAsync("expenses", 2024);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Totals.AnnualTotal, Is.EqualTo(22m));
            Assert.That(result.Rows.Select(row => row.CategoryPath),
                Is.EqualTo(new[] { "Uncategorized", "IncomeTax", "Food" }));
            Assert.That(result.Rows.Select(row => row.CategoryName),
                Is.EqualTo(new[] { "Uncategorized", "IncomeTax", "Food" }));
            Assert.That(result.Rows.Select(row => row.AnnualTotal), Is.EqualTo(new[] { -5m, 7m, 20m }));
            Assert.That(result.Rows.Select(row => row.IsSubtotal), Is.All.False);
        }
    }

    [Test]
    public async Task GetReportAsync_RollsDirectAndDescendantAmountsUpWithoutDoubleCountingTotals()
    {
        AddTransaction(2024, 1, ("Home", 10m), ("Food", 75m));
        AddTransaction(2024, 2, ("Home:Utilities", 20m));
        AddTransaction(2024, 3, ("Home:Utilities:Gas", -5m));

        var result = await _feature.GetReportAsync("all-detail", 2024);
        var home = result.Rows.Single(row => row.CategoryPath == "Home");
        var utilities = result.Rows.Single(row => row.CategoryPath == "Home:Utilities");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Rows.Select(row => (row.CategoryPath, row.Depth, row.IsSubtotal)),
                Is.EqualTo(new[]
                {
                    ("Home", 1, true),
                    ("Home:Utilities", 2, false),
                    ("Food", 1, false)
                }));
            Assert.That(home.AnnualTotal, Is.EqualTo(25m));
            Assert.That(utilities.AnnualTotal, Is.EqualTo(15m));
            Assert.That(home.CategoryName, Is.EqualTo("Home"));
            Assert.That(utilities.CategoryName, Is.EqualTo("Utilities"));
            Assert.That(home.MonthlyAmounts, Is.EqualTo(new[] { 10m, 20m, -5m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m }));
            Assert.That(result.Totals.MonthlyAmounts,
                Is.EqualTo(new[] { 85m, 20m, -5m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m }));
            Assert.That(result.Totals.AnnualTotal, Is.EqualTo(100m));
            Assert.That(result.Rows.Sum(row => row.AnnualTotal), Is.Not.EqualTo(result.Totals.AnnualTotal));
        }
    }

    [Test]
    public async Task GetReportAsync_DepthOneUsesTheParentSubtotalAndOmitsMonthAmounts()
    {
        AddTransaction(2024, 4, ("Home:Utilities", 10m), ("Home:Repairs", 15m));

        var result = await _feature.GetReportAsync("all", 2024);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Rows, Has.Count.EqualTo(1));
            Assert.That(result.Rows[0].CategoryPath, Is.EqualTo("Home"));
            Assert.That(result.Rows[0].Depth, Is.EqualTo(1));
            Assert.That(result.Rows[0].IsSubtotal, Is.False);
            Assert.That(result.Rows[0].AnnualTotal, Is.EqualTo(25m));
            Assert.That(result.Rows[0].MonthlyAmounts, Is.Null);
            Assert.That(result.Totals.MonthlyAmounts, Is.Null);
        }
    }

    [Test]
    public async Task GetReportAsync_ZeroGrandTotalReturnsNullPercentagesAndKeepsSignedValues()
    {
        AddTransaction(2024, 1, ("Income:Salary", 10m), ("Income:Refund", -10m));

        var result = await _feature.GetReportAsync("income-detail", 2024);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Totals.AnnualTotal, Is.Zero);
            Assert.That(result.Rows.Select(row => row.AnnualTotal), Is.EqualTo(new[] { 10m, -10m }));
            Assert.That(result.Rows.Select(row => row.Percentage), Is.All.Null);
        }
    }

    [Test]
    public async Task GetReportAsync_EachDefinitionUsesItsConfiguredAmountSortDirection()
    {
        AddTransaction(2024, 1,
            ("Income:Alpha", -20m), ("Income:Zulu", 10m),
            ("Taxes:Alpha", 10m), ("Taxes:Zulu", -20m),
            ("Savings:Alpha", 10m), ("Savings:Zulu", -20m),
            ("Transfer:Alpha", 10m), ("Transfer:Zulu", -20m),
            ("Alpha", 10m), ("Zulu", -20m));

        foreach (var definition in _feature.GetAvailableReports())
        {
            var result = await _feature.GetReportAsync(definition.Id, 2024);
            var rootTotals = result.Rows.Where(row => row.Depth == 1)
                .Select(row => row.AnnualTotal).ToArray();
            var expected = definition.Id is "income" or "income-detail"
                ? rootTotals.OrderByDescending(amount => amount)
                : rootTotals.OrderBy(amount => amount);

            Assert.That(rootTotals, Is.EqualTo(expected), definition.Id);
        }
    }

    [TestCase("expenses-detail", "", -1)]
    [TestCase("income-detail", "Income:", 1)]
    public async Task GetReportAsync_SortsWithinParentsUsingFullSignedAnnualRollups(
        string reportId, string prefix, int sign)
    {
        AddTransaction(2024, 1,
            ($"{prefix}Alpha", sign * 5m),
            ($"{prefix}Alpha:First", sign * 10m),
            ($"{prefix}Alpha:Second", sign * 20m),
            ($"{prefix}Zulu:Small", sign * 2m),
            ($"{prefix}Zulu:Large:Hidden", sign * 40m),
            ($"{prefix}Refund", sign * -100m));
        AddTransaction(2024, 12, ($"{prefix}Zulu:Large:Hidden", sign * 10m));

        var result = await _feature.GetReportAsync(reportId, 2024);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Rows.Select(row => row.CategoryPath), Is.EqualTo(new[]
            {
                $"{prefix}Zulu", $"{prefix}Zulu:Large", $"{prefix}Zulu:Small",
                $"{prefix}Alpha", $"{prefix}Alpha:Second", $"{prefix}Alpha:First",
                $"{prefix}Refund"
            }));
            Assert.That(result.Rows.Select(row => row.AnnualTotal),
                Is.EqualTo(new[] { 52m, 50m, 2m, 35m, 20m, 10m, -100m }
                    .Select(amount => sign * amount)));
            Assert.That(result.Totals.AnnualTotal, Is.EqualTo(sign * -13m));
        }
    }

    [TestCase("expenses", "")]
    [TestCase("income", "Income:")]
    public async Task GetReportAsync_EqualAnnualTotalsUseStableCategoryNameTieBreakers(
        string reportId, string prefix)
    {
        AddTransaction(2024, 1,
            ($"{prefix}beta", 10m), ($"{prefix}alpha", 10m));
        AddTransaction(2024, 12,
            ($"{prefix}Alpha", 10m), ($"{prefix}Zero", 0m));

        var result = await _feature.GetReportAsync(reportId, 2024);
        var tiedRows = result.Rows.Where(row => row.AnnualTotal == 10m);

        Assert.That(tiedRows.Select(row => row.CategoryName),
            Is.EqualTo(new[] { "Alpha", "alpha", "beta" }));
        Assert.That(result.Rows.Single(row => row.CategoryName == "Zero").AnnualTotal, Is.Zero);
    }

    [Test]
    public async Task GetReportAsync_EmptyYearReturnsNoRowsWithZeroTotals()
    {
        var result = await _feature.GetReportAsync("all-detail", 2024);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Rows, Is.Empty);
            Assert.That(result.Totals.AnnualTotal, Is.Zero);
            Assert.That(result.Totals.MonthlyAmounts, Is.EqualTo(new decimal[12]));
        }
    }

    [Test]
    public async Task GetReportAsync_YearOmittedUsesCurrentUtcYearFromTimeProvider()
    {
        AddTransaction(2040, 1, ("Income:Salary", 80m));
        AddTransaction(2039, 12, ("Income:Salary", 100m));

        var result = await _feature.GetReportAsync("income");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Year, Is.EqualTo(2040));
            Assert.That(result.Totals.AnnualTotal, Is.EqualTo(80m));
        }
    }

    [Test]
    public async Task GetReportAsync_UnknownReportIdThrowsNotFound()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _feature.GetReportAsync("custom-report", 2024));
    }

    [TestCase(0)]
    [TestCase(10000)]
    public async Task GetReportAsync_YearOutsideCalendarRangeThrowsValidationException(int year)
    {
        await Assert.ThrowsAsync<YoFi.V3.Entities.Exceptions.ValidationException>(
            () => _feature.GetReportAsync("all", year));
    }

    private void AddTransaction(int year, int month, params (string Category, decimal Amount)[] splits)
    {
        _transactions.Add(new Transaction
        {
            TenantId = _tenantProvider.CurrentTenant.Id,
            Date = new DateOnly(year, month, 15),
            Splits = splits.Select((split, index) => new Split
            {
                Amount = split.Amount,
                Category = split.Category,
                Order = index
            }).ToList()
        });
    }
}
