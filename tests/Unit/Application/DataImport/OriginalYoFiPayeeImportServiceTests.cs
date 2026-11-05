using System.IO.Compression;
using System.Security.Cryptography;
using ClosedXML.Excel;
using DotNetNuxt.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using YoFi.V3.Application;
using YoFi.V3.Application.Exceptions;
using YoFi.V3.Application.Features;
using YoFi.V3.Application.Services;
using YoFi.V3.Entities.Exceptions;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Tenancy.Providers;
using YoFi.V3.Tests.Unit.TestHelpers;

namespace YoFi.V3.Tests.Unit.Application.DataImport;

[TestFixture]
public class OriginalYoFiPayeeImportServiceTests
{
    private Mock<IDataProvider> _data = null!;
    private TestTenantProvider _tenant = null!;
    private MemoryCache _cache = null!;
    private OriginalYoFiPayeeImportService _service = null!;
    private readonly List<IModel> _staged = [];

    [SetUp]
    public void SetUp()
    {
        _staged.Clear();
        _data = new Mock<IDataProvider>();
        _data.Setup(data => data.AddRange(It.IsAny<IEnumerable<IModel>>()))
            .Callback((IEnumerable<IModel> models) => _staged.AddRange(models));
        _data.Setup(data => data.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _tenant = new TestTenantProvider();
        _tenant.CurrentTenant.Id = 42;
        _cache = new MemoryCache(new MemoryCacheOptions());

        var regex = new RegexValidationService();
        var dataImport = new DataImportService(_tenant, _data.Object, _cache, regex);
        _service = new OriginalYoFiPayeeImportService(dataImport, regex);
    }

    [TearDown]
    public void TearDown() => _cache.Dispose();

    [Test]
    public void Preview_MapsLegacyHeadersReportsIgnoredDataAndSkipsBlankCategory()
    {
        var file = Workbook(
            [" ID ", " name ", "CATEGORY", "Selected", "Vendor note"],
            [
                [12, " /AMAZON.*/ ", " shopping : food  ", true, "ignored"],
                [13, "Coffee Shop", "", false, "ignored"]
            ],
            otherWorksheet: true);

        var result = _service.Preview(file);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.WorksheetFound, Is.True);
            Assert.That(result.ValidRecordCount, Is.EqualTo(1));
            Assert.That(result.SkippedRecordCount, Is.EqualTo(1));
            Assert.That(result.SkippedRows.Single().RowNumber, Is.EqualTo(3));
            Assert.That(result.SkippedRows.Single().Code, Is.EqualTo("blank_category"));
            Assert.That(result.IgnoredWorksheets, Is.EqualTo(["Transactions"]));
            Assert.That(result.IgnoredColumns, Is.EqualTo(["Vendor note"]));
            Assert.That(result.Errors, Is.Empty);
            Assert.That(result.CanImport, Is.True);
            Assert.That(result.Sha256, Is.EqualTo(Convert.ToHexString(SHA256.HashData(file))));
            Assert.That(_staged, Is.Empty);
        }
        _data.Verify(data => data.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Import_CreatesFreshTenantRulesAndInvalidatesOnlyTargetCache()
    {
        var file = Workbook(
            ["ID", "Name", "Category", "Selected"],
            [
                [44, "/AMAZON.*/", "shopping: food", true],
                [45, "Coffee Shop", "restaurants", false]
            ]);
        var preview = _service.Preview(file);
        _cache.Set(PayeeMatchingRuleFeature.CacheKey(42), "stale");
        _cache.Set(PayeeMatchingRuleFeature.CacheKey(99), "other tenant");
        var before = DateTimeOffset.UtcNow;

        var result = await _service.ImportAsync(file, preview.Sha256);

        var rules = _staged.OfType<PayeeMatchingRule>().ToArray();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Created, Is.EqualTo(2));
            Assert.That(result.Skipped, Is.Zero);
            Assert.That(result.Updated, Is.Zero);
            Assert.That(result.Failed, Is.Zero);
            Assert.That(rules, Has.Length.EqualTo(2));
            Assert.That(rules.Select(rule => rule.Key), Is.Unique.And.All.Not.EqualTo(Guid.Empty));
            Assert.That(rules.Select(rule => rule.TenantId), Is.All.EqualTo(42));
            Assert.That(rules[0].PayeePattern, Is.EqualTo("AMAZON.*"));
            Assert.That(rules[0].PayeeIsRegex, Is.True);
            Assert.That(rules[0].Category, Is.EqualTo("Shopping:Food"));
            Assert.That(rules[1].PayeePattern, Is.EqualTo("Coffee Shop"));
            Assert.That(rules[1].PayeeIsRegex, Is.False);
            Assert.That(rules.All(rule => rule.CreatedAt >= before), Is.True);
            Assert.That(rules.All(rule => rule.MatchCount == 0 && rule.LastUsedAt is null), Is.True);
            Assert.That(_cache.TryGetValue(PayeeMatchingRuleFeature.CacheKey(42), out _), Is.False);
            Assert.That(_cache.Get<string>(PayeeMatchingRuleFeature.CacheKey(99)), Is.EqualTo("other tenant"));
        }
        _data.Verify(data => data.AddRange(It.IsAny<IEnumerable<IModel>>()), Times.Once);
        _data.Verify(data => data.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Import_RepeatedFileCreatesAdditionalRulesWithoutDeduplication()
    {
        var file = Workbook(["Name", "Category"], [["Coffee Shop", "Food"]]);
        var preview = _service.Preview(file);

        await _service.ImportAsync(file, preview.Sha256);
        await _service.ImportAsync(file, preview.Sha256);

        var rules = _staged.OfType<PayeeMatchingRule>().ToArray();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(rules, Has.Length.EqualTo(2));
            Assert.That(rules.Select(rule => rule.PayeePattern), Is.All.EqualTo("Coffee Shop"));
            Assert.That(rules.Select(rule => rule.Key), Is.Unique);
        }
        _data.Verify(data => data.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Test]
    public async Task Import_PersistenceFailureDoesNotInvalidateCacheOrReturnSuccess()
    {
        var file = Workbook(["Name", "Category"], [["Coffee Shop", "Food"]]);
        var preview = _service.Preview(file);
        _cache.Set(PayeeMatchingRuleFeature.CacheKey(42), "stale");
        _data.Setup(data => data.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("commit failed"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.ImportAsync(file, preview.Sha256));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception!.Message, Is.EqualTo("commit failed"));
            Assert.That(_cache.Get<string>(PayeeMatchingRuleFeature.CacheKey(42)), Is.EqualTo("stale"));
        }
    }

    [Test]
    public void Preview_InvalidRegexBlocksImportAndRetainsRowErrorDetails()
    {
        var file = Workbook(["Name", "Category"], [["/(?=coffee)/", "Food"]]);

        var preview = _service.Preview(file);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(preview.CanImport, Is.False);
            Assert.That(preview.ValidRecordCount, Is.Zero);
            Assert.That(preview.Errors, Has.Length.EqualTo(1));
            Assert.That(preview.Errors[0].RowNumber, Is.EqualTo(2));
            Assert.That(preview.Errors[0].Field, Is.EqualTo("Name"));
            Assert.That(preview.Errors[0].Code, Is.EqualTo("invalid_value"));
            Assert.That(preview.Errors[0].Message, Does.Contain("not supported"));
            Assert.That(_staged, Is.Empty);
        }
    }

    [Test]
    public async Task Import_InvalidRowsThrowsStructuredValidationAndWritesNothing()
    {
        var file = Workbook(["Name", "Category"], [["/(?=coffee)/", "Food"]]);
        var preview = _service.Preview(file);

        var exception = await Assert.ThrowsAsync<PayeeImportValidationException>(
            () => _service.ImportAsync(file, preview.Sha256));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception!.Preview.Errors.Single().RowNumber, Is.EqualTo(2));
            Assert.That(_staged, Is.Empty);
        }
        _data.Verify(data => data.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public void Preview_DuplicateRequiredHeadersAreReported()
    {
        var file = Workbook(["Name", " name ", "Category"], [["Coffee", "Coffee", "Food"]]);

        var preview = _service.Preview(file);

        Assert.That(preview.Errors.Select(error => error.Code), Does.Contain("duplicate_header"));
        Assert.That(preview.CanImport, Is.False);
    }

    [Test]
    public void Preview_MissingPayeeWorksheetIsNotImportable()
    {
        var file = Workbook(["Name", "Category"], [["Coffee", "Food"]], worksheetName: "Transactions");

        var preview = _service.Preview(file);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(preview.WorksheetFound, Is.False);
            Assert.That(preview.CanImport, Is.False);
            Assert.That(preview.IgnoredWorksheets, Is.EqualTo(["Transactions"]));
        }
    }

    [Test]
    public async Task Import_ChangedFileIsRejectedBeforePersistence()
    {
        var previewFile = Workbook(["Name", "Category"], [["Coffee", "Food"]]);
        var changedFile = Workbook(["Name", "Category"], [["Tea", "Food"]]);
        var preview = _service.Preview(previewFile);

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => _service.ImportAsync(changedFile, preview.Sha256));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception!.Message, Does.Contain("differs"));
            Assert.That(_staged, Is.Empty);
        }
        _data.Verify(data => data.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public void Preview_FormulaInRequiredCellIsNotEvaluated()
    {
        var file = WorkbookWithFormulaName();

        var preview = _service.Preview(file);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(preview.Errors.Single().Code, Is.EqualTo("formula_not_allowed"));
            Assert.That(preview.Errors.Single().Field, Is.EqualTo("Name"));
            Assert.That(preview.CanImport, Is.False);
        }
    }

    [Test]
    public void Preview_UnreadableFileThrowsValidationException()
    {
        var exception = Assert.Throws<ValidationException>(() => _service.Preview([1, 2, 3, 4]));

        Assert.That(exception!.Message, Does.Contain("readable XLSX"));
    }

    [Test]
    public void Preview_InvalidOpenXmlPackageThrowsValidationException()
    {
        var exception = Assert.Throws<ValidationException>(() => _service.Preview(InvalidOpenXmlPackage()));

        Assert.That(exception!.Message, Does.Contain("readable XLSX"));
    }

    [Test]
    public void DependencyRegistration_ResolvesPayeeImportService()
    {
        var services = new ServiceCollection().AddApplicationFeatures();
        services.AddSingleton(_data.Object);
        services.AddSingleton<ITenantProvider>(_tenant);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.That(scope.ServiceProvider.GetRequiredService<OriginalYoFiPayeeImportService>(), Is.Not.Null);
    }

    private static byte[] Workbook(
        object?[] headers,
        object?[][] rows,
        bool otherWorksheet = false,
        string worksheetName = "Payee")
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(worksheetName);
        SetRow(worksheet, 1, headers);
        for (var index = 0; index < rows.Length; index++)
        {
            SetRow(worksheet, index + 2, rows[index]);
        }
        if (otherWorksheet)
        {
            workbook.Worksheets.Add("Transactions");
        }
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] WorkbookWithFormulaName()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Payee");
        SetRow(worksheet, 1, ["Name", "Category"]);
        worksheet.Cell(2, 1).FormulaA1 = "\"Formula Name\"";
        worksheet.Cell(2, 2).Value = "Food";
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] InvalidOpenXmlPackage()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            archive.CreateEntry("not-an-xlsx-entry");
        }
        return stream.ToArray();
    }

    private static void SetRow(IXLWorksheet worksheet, int rowNumber, object?[] values)
    {
        for (var index = 0; index < values.Length; index++)
        {
            worksheet.Cell(rowNumber, index + 1).Value = XLCellValue.FromObject(values[index]);
        }
    }
}
