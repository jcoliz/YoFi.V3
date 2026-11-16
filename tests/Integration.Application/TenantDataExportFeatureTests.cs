using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Features;
using YoFi.V3.Application.Services;
using YoFi.V3.Entities.Exceptions;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Tenancy.Models;
using YoFi.V3.Entities.Tenancy.Providers;
using YoFi.V3.Tests.Integration.Application.TestHelpers;

namespace YoFi.V3.Tests.Integration.Application;

[TestFixture]
public class TenantDataExportFeatureTests : FeatureTestBase
{
    private Tenant _targetTenant = null!;
    private Tenant _otherTenant = null!;
    private TenantDataExportFeature _feature = null!;

    [SetUp]
    public new async Task SetUp()
    {
        await base.SetUp();
        _targetTenant = new Tenant { Name = "Target workspace" };
        _otherTenant = new Tenant { Name = "Other workspace" };
        _context.Tenants.AddRange(_targetTenant, _otherTenant);
        await _context.SaveChangesAsync();

        ITenantProvider tenantProvider = new TestTenantProvider { CurrentTenant = _targetTenant };
        _feature = new TenantDataExportFeature(
            tenantProvider,
            _dataProvider,
            [
                new TransactionExportWorksheetWriter(_dataProvider),
                new PayeeMatchingRuleExportWorksheetWriter(_dataProvider)
            ],
            new TestTimeProvider());
    }

    [Test]
    public async Task GetOptions_ReturnsRegisteredTypesAndOnlyTargetTenantYears()
    {
        _context.Transactions.AddRange(
            Transaction(_targetTenant, new DateOnly(2024, 2, 1), "First"),
            Transaction(_targetTenant, new DateOnly(2025, 3, 1), "Second"),
            Transaction(_otherTenant, new DateOnly(2026, 4, 1), "Other"));
        await _context.SaveChangesAsync();

        var options = await _feature.GetOptionsAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(options.DataTypes.Select(type => type.DataType),
                Is.EqualTo(new[] { "PayeeMatchingRules", "Transactions" }));
            Assert.That(options.DataTypes.Single(type => type.DataType == "Transactions").SupportsDateFilter, Is.True);
            Assert.That(options.DataTypes.Single(type => type.DataType == "PayeeMatchingRules").SupportsDateFilter, Is.False);
            Assert.That(options.Years, Is.EqualTo(new[] { 2025, 2024 }));
        }
    }

    [Test]
    public async Task Export_TransactionSelectionFiltersTransactionsAndIncludesRelatedSplits()
    {
        var included = Transaction(
            _targetTenant,
            new DateOnly(2025, 1, 1),
            "Included",
            new Split { Amount = 10m, Category = "Food", Memo = "Lunch", Order = 0 },
            new Split { Amount = 20m, Category = "Home", Order = 1 });
        var excludedByDate = Transaction(
            _targetTenant,
            new DateOnly(2024, 12, 31),
            "Old",
            new Split { Amount = 5m, Category = "Old" });
        var excludedByTenant = Transaction(
            _otherTenant,
            new DateOnly(2025, 6, 1),
            "Other tenant",
            new Split { Amount = 7m, Category = "Other" });
        _context.Transactions.AddRange(included, excludedByDate, excludedByTenant);
        await _context.SaveChangesAsync();

        var result = await _feature.ExportAsync(new TenantDataExportRequest(
            ["Transactions"],
            StartDate: new DateOnly(2025, 1, 1),
            EndDate: new DateOnly(2025, 1, 1)));

        using var stream = new MemoryStream(result.Content);
        using var workbook = new XLWorkbook(stream);
        var transactions = workbook.Worksheet("Transactions");
        var splits = workbook.Worksheet("Splits");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(workbook.Worksheets.Select(sheet => sheet.Name),
                Is.EqualTo(new[] { "Metadata", "Transactions", "Splits" }));
            Assert.That(transactions.Row(1).CellsUsed().Select(cell => cell.GetString()),
                Is.EqualTo(new[] { "Key", "Date", "Payee", "Amount", "Source", "ExternalId", "Memo" }));
            Assert.That(transactions.Row(2).Cell(1).GetString(), Is.EqualTo(included.Key.ToString("D")));
            Assert.That(transactions.Row(2).Cell(2).GetDateTime(), Is.EqualTo(new DateTime(2025, 1, 1)));
            Assert.That(transactions.Row(2).Cell(3).GetString(), Is.EqualTo("Included"));
            Assert.That(transactions.Row(1).CellsUsed().Select(cell => cell.GetString()), Does.Not.Contain("Id"));
            Assert.That(splits.Row(1).CellsUsed().Select(cell => cell.GetString()),
                Is.EqualTo(new[] { "Key", "TransactionKey", "Amount", "Category", "Memo", "Order" }));
            Assert.That(splits.Row(2).Cell(2).GetString(), Is.EqualTo(included.Key.ToString("D")));
            Assert.That(splits.Row(3).Cell(2).GetString(), Is.EqualTo(included.Key.ToString("D")));
            Assert.That(result.Counts, Is.EqualTo(new[]
            {
                new TenantDataExportCountDto("Transactions", 1),
                new TenantDataExportCountDto("Splits", 2)
            }));
            Assert.That(result.FileName, Is.EqualTo("yofi-transactions-2025-01-01-to-2025-01-01-2040-07-01.xlsx"));
        }
    }

    [Test]
    public async Task Export_PayeeRulesIgnoreDateFilterAndUseNativeV3Metadata()
    {
        var rule = Rule(_targetTenant, "Coffee Shop", "Food");
        var otherRule = Rule(_otherTenant, "Other", "Other");
        _context.PayeeMatchingRules.AddRange(rule, otherRule);
        await _context.SaveChangesAsync();

        var result = await _feature.ExportAsync(new TenantDataExportRequest(
            ["PayeeMatchingRules"],
            Year: 2020));

        using var stream = new MemoryStream(result.Content);
        using var workbook = new XLWorkbook(stream);
        var metadata = workbook.Worksheet("Metadata");
        var rules = workbook.Worksheet("PayeeMatchingRules");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(metadata.Cell(1, 1).GetString(), Is.EqualTo("Name"));
            Assert.That(metadata.Cell(1, 2).GetString(), Is.EqualTo("Value"));
            Assert.That(metadata.Cell(2, 1).GetString(), Is.EqualTo("Format"));
            Assert.That(metadata.Cell(2, 2).GetString(), Is.EqualTo("YoFi.V3"));
            Assert.That(workbook.Worksheets.Select(sheet => sheet.Name),
                Is.EqualTo(new[] { "Metadata", "PayeeMatchingRules" }));
            Assert.That(rules.Row(1).CellsUsed().Select(cell => cell.GetString()), Is.EqualTo(new[]
            {
                "Key", "PayeePattern", "PayeeIsRegex", "Category", "CreatedAt", "ModifiedAt", "LastUsedAt", "MatchCount"
            }));
            Assert.That(rules.Row(2).Cell(1).GetString(), Is.EqualTo(rule.Key.ToString("D")));
            Assert.That(rules.Row(1).CellsUsed().Select(cell => cell.GetString()), Does.Not.Contain("Id"));
            Assert.That(rules.Row(1).CellsUsed().Select(cell => cell.GetString()), Does.Not.Contain("TenantId"));
            Assert.That(result.Counts, Is.EqualTo(new[] { new TenantDataExportCountDto("PayeeMatchingRules", 1) }));
            Assert.That(result.FileName, Is.EqualTo("yofi-payee-matching-rules-2040-07-01.xlsx"));
        }
    }

    [Test]
    public async Task ExportAll_IncludesEveryTypeAndIgnoresSelectionAndDateFilter()
    {
        _context.Transactions.AddRange(
            Transaction(_targetTenant, new DateOnly(2024, 1, 1), "Old"),
            Transaction(_targetTenant, new DateOnly(2025, 1, 1), "New"));
        _context.PayeeMatchingRules.Add(Rule(_targetTenant, "Coffee Shop", "Food"));
        await _context.SaveChangesAsync();

        var result = await _feature.ExportAsync(new TenantDataExportRequest(
            ["Transactions"],
            Year: 2025,
            ExportAllData: true));

        using var stream = new MemoryStream(result.Content);
        using var workbook = new XLWorkbook(stream);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(workbook.Worksheets.Select(sheet => sheet.Name),
                Is.EqualTo(new[] { "Metadata", "PayeeMatchingRules", "Transactions", "Splits" }));
            Assert.That(workbook.Worksheet("Transactions").LastRowUsed()!.RowNumber(), Is.EqualTo(3));
            Assert.That(result.Counts.Select(count => count.DataType),
                Is.EqualTo(new[] { "PayeeMatchingRules", "Transactions", "Splits" }));
            Assert.That(result.FileName, Is.EqualTo("yofi-complete-export-2040-07-01.xlsx"));
        }
    }

    [Test]
    public async Task Export_RejectsEmptyUnknownDuplicateAndInvalidDateSelections()
    {
        var empty = await Assert.ThrowsAsync<ValidationException>(() =>
            _feature.ExportAsync(new TenantDataExportRequest([])));
        var unknown = await Assert.ThrowsAsync<ValidationException>(() =>
            _feature.ExportAsync(new TenantDataExportRequest(["Unknown"])));
        var duplicate = await Assert.ThrowsAsync<ValidationException>(() =>
            _feature.ExportAsync(new TenantDataExportRequest(["Transactions", "transactions"])));
        var partialRange = await Assert.ThrowsAsync<ValidationException>(() =>
            _feature.ExportAsync(new TenantDataExportRequest(["Transactions"], StartDate: new DateOnly(2025, 1, 1))));
        var reversedRange = await Assert.ThrowsAsync<ValidationException>(() =>
            _feature.ExportAsync(new TenantDataExportRequest(
                ["Transactions"],
                StartDate: new DateOnly(2025, 2, 1),
                EndDate: new DateOnly(2025, 1, 1))));
        var mixedFilters = await Assert.ThrowsAsync<ValidationException>(() =>
            _feature.ExportAsync(new TenantDataExportRequest(
                ["Transactions"],
                Year: 2025,
                StartDate: new DateOnly(2025, 1, 1),
                EndDate: new DateOnly(2025, 12, 31))));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(empty!.ParameterName, Is.EqualTo(nameof(TenantDataExportRequest.DataTypes)));
            Assert.That(unknown!.ParameterName, Is.EqualTo(nameof(TenantDataExportRequest.DataTypes)));
            Assert.That(duplicate!.ParameterName, Is.EqualTo(nameof(TenantDataExportRequest.DataTypes)));
            Assert.That(partialRange!.ParameterName, Is.EqualTo(nameof(TenantDataExportRequest.StartDate)));
            Assert.That(reversedRange!.ParameterName, Is.EqualTo(nameof(TenantDataExportRequest.StartDate)));
            Assert.That(mixedFilters!.ParameterName, Is.EqualTo(nameof(TenantDataExportRequest.Year)));
        }
    }

    private static Transaction Transaction(Tenant tenant, DateOnly date, string payee, params Split[] splits) =>
        new()
        {
            TenantId = tenant.Id,
            Date = date,
            Payee = payee,
            Amount = splits.Sum(split => split.Amount),
            Splits = splits
        };

    private static PayeeMatchingRule Rule(Tenant tenant, string pattern, string category) =>
        new()
        {
            TenantId = tenant.Id,
            PayeePattern = pattern,
            Category = category,
            CreatedAt = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            ModifiedAt = new DateTimeOffset(2025, 1, 2, 0, 0, 0, TimeSpan.Zero),
            LastUsedAt = null,
            MatchCount = 3
        };

    private sealed class TestTenantProvider : ITenantProvider
    {
        public required Tenant CurrentTenant { get; init; }
    }
}
