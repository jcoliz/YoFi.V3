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

public class TenantDataImportFeatureTests : FeatureTestBase
{
    private MemoryCache _cache = null!;
    private Tenant _targetTenant = null!;
    private Tenant _otherTenant = null!;
    private TenantDataImportFeature _service = null!;
    private PayeeMatchingRuleFeature _rulesFeature = null!;
    private TestTimeProvider _timeProvider = null!;

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
        _timeProvider = new TestTimeProvider();
        var dataImport = new DataImportService(tenantProvider, _dataProvider, _cache, regex, _timeProvider);
        _service = new TenantDataImportFeature(
            [new OriginalYoFiWorkbookFormatDetector(), new NativeYoFiV3WorkbookFormatDetector()],
            [
                new OriginalYoFiPayeeWorksheetHandler(dataImport, regex),
                new OriginalYoFiTransactionWorksheetHandler(dataImport, _timeProvider),
                new OriginalYoFiSplitWorksheetHandler(),
                new NativeYoFiPayeeWorksheetHandler(dataImport, regex),
                new NativeYoFiTransactionWorksheetHandler(dataImport, _timeProvider),
                new NativeYoFiSplitWorksheetHandler(dataImport)
            ]);
        _rulesFeature = new PayeeMatchingRuleFeature(tenantProvider, _dataProvider, _cache, _timeProvider);
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
            Assert.That(imported.Select(rule => rule.CreatedAt), Is.All.EqualTo(_timeProvider.GetUtcNow()));
            Assert.That(imported.Select(rule => rule.ModifiedAt), Is.All.EqualTo(_timeProvider.GetUtcNow()));
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

    [Test]
    public async Task Import_NativeWorkbookUpdatesTenantTransactionAndReconcilesSplits()
    {
        // Given: a native transaction and its existing split graph in the target tenant
        var transactionKey = Guid.NewGuid();
        var firstSplitKey = Guid.NewGuid();
        var transaction = new Transaction
        {
            Key = transactionKey,
            TenantId = _targetTenant.Id,
            Date = new DateOnly(2026, 1, 1),
            Payee = "Old Payee",
            Amount = 25m,
            Source = "Old Source"
        };
        transaction.Splits.Add(new Split
        {
            Key = firstSplitKey,
            Amount = 25m,
            Category = "Old Category",
            Memo = null,
            Order = 0
        });
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        var secondSplitKey = Guid.NewGuid();
        var file = NamedWorkbook(
            ("Metadata", ["Name", "Value"], [["Format", "YoFi.V3"]]),
            ("Transactions", ["Key", "Date", "Payee", "Amount", "Source", "ExternalId", "Memo", "Category"],
                [[transactionKey.ToString("D"), "2026-02-03", "Updated Payee", 25m, "Checking", "EXT-1", "Updated memo", " split "]]),
            ("Splits", ["Key", "TransactionKey", "Amount", "Category", "Memo", "Order"],
                [[firstSplitKey.ToString("D"), transactionKey.ToString("D"), 10m, " shopping : food ", "Food memo", 0],
                 [secondSplitKey.ToString("D"), transactionKey.ToString("D"), 15m, "home   and garden", "Garden memo", 1]]));

        // When: previewed and imported
        var preview = _service.Preview(file);
        var result = await _service.ImportAsync(file, preview.Sha256);
        _context.ChangeTracker.Clear();

        // Then: native format and both worksheet results are applied tenant-safely
        var imported = await _context.Transactions
            .Where(item => item.TenantId == _targetTenant.Id)
            .Include(item => item.Splits)
            .SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(preview.FormatId, Is.EqualTo("yofi.v3"));
            Assert.That(preview.Worksheets.Select(item => item.WorksheetName), Is.EquivalentTo(["Transactions", "Splits"]));
            Assert.That(result.Worksheets.Single(item => item.WorksheetName == "Transactions").Updated, Is.EqualTo(1));
            Assert.That(result.Worksheets.Single(item => item.WorksheetName == "Splits").Updated, Is.EqualTo(1));
            Assert.That(imported.Payee, Is.EqualTo("Updated Payee"));
            Assert.That(imported.Source, Is.EqualTo("Checking"));
            Assert.That(imported.ExternalId, Is.EqualTo("EXT-1"));
            Assert.That(imported.Splits, Has.Count.EqualTo(2));
            Assert.That(imported.Splits.Select(split => split.Category), Is.EquivalentTo(["Shopping:Food", "Home And Garden"]));
            Assert.That(imported.Splits.Select(split => split.Key), Is.EquivalentTo([firstSplitKey, secondSplitKey]));
        }
    }


    [Test]
    public async Task Import_OriginalTransactionAndSplitWorkbookCreatesTenantGraph()
    {
        // Given: original transaction and split worksheets
        var file = NamedWorkbook(
            ("Transaction", ["ID", "Timestamp", "Payee", "Amount", "Category", "Memo", "BankReference", "ReceiptUrl"],
                [[101, "2026-02-03 10:30", "Coffee Shop", 12.50m, " food : drinks ", "Morning", "BANK-101", "ignored"]]),
            ("Split", ["ID", "Amount", "Category", "Memo", "TransactionID"],
                [[1, 7.50m, " food : dining ", "Breakfast", 101],
                 [2, 5.00m, " work   travel ", "Coffee", 101]]));

        // When
        var preview = _service.Preview(file);
        var result = await _service.ImportAsync(file, preview.Sha256);
        _context.ChangeTracker.Clear();

        // Then
        var imported = await _context.Transactions
            .Where(item => item.TenantId == _targetTenant.Id)
            .Include(item => item.Splits)
            .SingleAsync();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(preview.FormatId, Is.EqualTo("original-yofi"));
            Assert.That(result.Worksheets.Single(item => item.WorksheetName == "Transaction").Created, Is.EqualTo(1));
            Assert.That(imported.ExternalId, Is.EqualTo("BANK-101"));
            Assert.That(imported.Splits, Has.Count.EqualTo(2));
            Assert.That(imported.Splits.Select(split => split.Category), Is.EquivalentTo(["Food:Dining", "Work Travel"]));
            Assert.That(imported.Id, Is.Not.EqualTo(101));
        }
    }


    private static byte[] NamedWorkbook(params (string Name, string[] Headers, object[][] Rows)[] worksheets)
    {
        using var workbook = new XLWorkbook();
        foreach (var worksheet in worksheets)
        {
            var sheet = workbook.Worksheets.Add(worksheet.Name);
            SetRow(sheet, 1, worksheet.Headers);
            for (var index = 0; index < worksheet.Rows.Length; index++)
                SetRow(sheet, index + 2, worksheet.Rows[index].Select(value => value?.ToString() ?? string.Empty).ToArray());
        }
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
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
