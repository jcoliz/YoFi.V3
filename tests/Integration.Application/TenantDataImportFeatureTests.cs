using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Features;
using YoFi.V3.Application.Services;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Tenancy.Models;
using YoFi.V3.Entities.Tenancy.Providers;
using YoFi.V3.Tests.Integration.Application.TestHelpers;

namespace YoFi.V3.Tests.Integration.Application;

[TestFixture]
public class TenantDataImportFeatureTests : FeatureTestBase
{
    private MemoryCache _cache = null!;
    private Tenant _targetTenant = null!;
    private Tenant _otherTenant = null!;
    private TenantDataImportFeature _service = null!;
    private PayeeMatchingRuleFeature _rulesFeature = null!;

    [SetUp]
    public new async Task SetUp()
    {
        await base.SetUp();
        _targetTenant = new Tenant { Name = "Target workspace" };
        _otherTenant = new Tenant { Name = "Other workspace" };
        _context.Tenants.AddRange(_targetTenant, _otherTenant);
        await _context.SaveChangesAsync();

        ITenantProvider tenantProvider = new TestTenantProvider { CurrentTenant = _targetTenant };
        _cache = new MemoryCache(new MemoryCacheOptions());
        var regex = new RegexValidationService();
        var dataImport = new DataImportService(tenantProvider, _dataProvider, _cache, regex);
        _service = new TenantDataImportFeature(
            [new OriginalYoFiWorkbookFormatDetector()],
            [new OriginalYoFiPayeeWorksheetHandler(dataImport, regex)]);
        _rulesFeature = new PayeeMatchingRuleFeature(tenantProvider, _dataProvider, _cache, TimeProvider.System);
    }

    [TearDown]
    public void DisposeCache() => _cache.Dispose();

    [Test]
    public async Task Import_PersistsRulesForTargetOnlyAndInvalidatesRuleCache()
    {
        var existing = new PayeeMatchingRule
        {
            TenantId = _targetTenant.Id,
            PayeePattern = "Existing",
            Category = "Existing"
        };
        var other = new PayeeMatchingRule
        {
            TenantId = _otherTenant.Id,
            PayeePattern = "Other",
            Category = "Unchanged"
        };
        _context.PayeeMatchingRules.AddRange(existing, other);
        await _context.SaveChangesAsync();

        Assert.That((await _rulesFeature.GetRulesAsync()).Items, Has.Count.EqualTo(1));
        var file = Workbook(
            ["ID", "Name", "Category", "Selected"],
            [["12", "/AMAZON.*/", "shopping: food", "TRUE"], ["13", "Coffee Shop", "Restaurants", "FALSE"]]);
        var preview = _service.Preview(file);

        var result = await _service.ImportAsync(file, preview.Sha256);
        _context.ChangeTracker.Clear();
        var imported = await _context.PayeeMatchingRules
            .Where(rule => rule.TenantId == _targetTenant.Id && rule.Key != existing.Key)
            .OrderBy(rule => rule.PayeePattern)
            .ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Worksheets.Single().Created, Is.EqualTo(2));
            Assert.That(result.Worksheets.Single().Updated, Is.Zero);
            Assert.That(result.Worksheets.Single().Failed, Is.Zero);
            Assert.That(imported, Has.Count.EqualTo(2));
            Assert.That(imported[0].PayeePattern, Is.EqualTo("AMAZON.*"));
            Assert.That(imported[0].PayeeIsRegex, Is.True);
            Assert.That(imported[0].Category, Is.EqualTo("Shopping:Food"));
            Assert.That(imported[0].TenantId, Is.EqualTo(_targetTenant.Id));
            Assert.That(imported[1].PayeePattern, Is.EqualTo("Coffee Shop"));
            Assert.That(imported[1].PayeeIsRegex, Is.False);
            Assert.That(imported[1].Category, Is.EqualTo("Restaurants"));
            Assert.That(imported.Select(rule => rule.Key), Is.Unique.And.All.Not.EqualTo(Guid.Empty));
            Assert.That(await _context.PayeeMatchingRules.CountAsync(rule => rule.TenantId == _targetTenant.Id), Is.EqualTo(3));
            Assert.That(await _context.PayeeMatchingRules.CountAsync(rule => rule.TenantId == _otherTenant.Id), Is.EqualTo(1));
            Assert.That(await _context.PayeeMatchingRules.Where(rule => rule.Key == other.Key)
                .Select(rule => rule.Category).SingleAsync(), Is.EqualTo("Unchanged"));
        }

        var refreshedRules = (await _rulesFeature.GetRulesAsync()).Items;
        Assert.That(refreshedRules, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task Import_SkipsBlankCategoriesAndRepeatedImportCreatesFreshRules()
    {
        var file = Workbook(
            ["Name", "Category"],
            [["Coffee Shop", "Food"], ["No category", ""]]);
        var preview = _service.Preview(file);

        var first = await _service.ImportAsync(file, preview.Sha256);
        var second = await _service.ImportAsync(file, preview.Sha256);
        _context.ChangeTracker.Clear();
        var rules = await _context.PayeeMatchingRules
            .Where(rule => rule.TenantId == _targetTenant.Id)
            .ToListAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(preview.Worksheets.Single().ValidRecordCount, Is.EqualTo(1));
            Assert.That(preview.Worksheets.Single().SkippedRecordCount, Is.EqualTo(1));
            Assert.That(first.Worksheets.Single().Created, Is.EqualTo(1));
            Assert.That(first.Worksheets.Single().Skipped, Is.EqualTo(1));
            Assert.That(second.Worksheets.Single().Created, Is.EqualTo(1));
            Assert.That(second.Worksheets.Single().Skipped, Is.EqualTo(1));
            Assert.That(rules, Has.Count.EqualTo(2));
            Assert.That(rules.Select(rule => rule.PayeePattern), Is.All.EqualTo("Coffee Shop"));
            Assert.That(rules.Select(rule => rule.Key), Is.Unique);
        }
    }

    [Test]
    public async Task Import_PersistenceFailureRollsBackWorksheetAndDoesNotReturnSuccess()
    {
        await _context.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION reject_payee_rules() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'Injected payee-rule persistence failure';
            END;
            $$;
            CREATE TRIGGER reject_payee_rules BEFORE INSERT ON "PayeeMatchingRules"
            FOR EACH ROW EXECUTE FUNCTION reject_payee_rules();
            """);
        var file = Workbook(["Name", "Category"], [["Coffee Shop", "Food"]]);
        var preview = _service.Preview(file);

        await Assert.ThrowsAsync<DbUpdateException>(() => _service.ImportAsync(file, preview.Sha256));
        _context.ChangeTracker.Clear();

        Assert.That(await _context.PayeeMatchingRules.CountAsync(), Is.Zero);
    }

    private static byte[] Workbook(string[] headers, string[][] rows)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Payee");
        SetRow(worksheet, 1, headers);
        for (var index = 0; index < rows.Length; index++)
        {
            SetRow(worksheet, index + 2, rows[index]);
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void SetRow(IXLWorksheet worksheet, int rowNumber, string[] values)
    {
        for (var index = 0; index < values.Length; index++)
        {
            worksheet.Cell(rowNumber, index + 1).Value = values[index];
        }
    }

    private sealed class TestTenantProvider : ITenantProvider
    {
        public required Tenant CurrentTenant { get; init; }
    }
}
