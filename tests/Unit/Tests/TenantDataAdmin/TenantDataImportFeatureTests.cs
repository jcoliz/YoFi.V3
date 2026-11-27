using System.IO.Compression;
using System.Security.Cryptography;
using ClosedXML.Excel;
using DotNetNuxt.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using YoFi.V3.Application;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Exceptions;
using YoFi.V3.Application.Features;
using YoFi.V3.Application.Services;
using YoFi.V3.Entities.Exceptions;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Tenancy.Providers;
using YoFi.V3.Tests.Unit.TestHelpers;

namespace YoFi.V3.Tests.Unit.Application.DataImport;

[TestFixture]
public class TenantDataImportFeatureTests
{
    private Mock<IDataProvider> _data = null!;
    private TestTenantProvider _tenant = null!;
    private MemoryCache _cache = null!;
    private TenantDataImportFeature _service = null!;
    private Mock<TimeProvider> _timeProvider = null!;
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
        _timeProvider = new Mock<TimeProvider>();
        _timeProvider.Setup(time => time.GetUtcNow())
            .Returns(new DateTimeOffset(2040, 7, 1, 12, 0, 0, TimeSpan.Zero));
        var dataImport = new DataImportService(_tenant, _data.Object, _cache, regex, _timeProvider.Object);
        _service = new TenantDataImportFeature(
            [new OriginalYoFiWorkbookFormatDetector()],
            [new OriginalYoFiPayeeWorksheetHandler(dataImport, regex)]);
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
            Assert.That(result.FormatId, Is.EqualTo("original-yofi"));
            Assert.That(result.Worksheets, Has.Count.EqualTo(1));
            Assert.That(result.Worksheets.Single().ValidRecordCount, Is.EqualTo(1));
            Assert.That(result.Worksheets.Single().SkippedRecordCount, Is.EqualTo(1));
            Assert.That(result.Worksheets.Single().SkippedRows.Single().RowNumber, Is.EqualTo(3));
            Assert.That(result.Worksheets.Single().SkippedRows.Single().Code, Is.EqualTo("blank_category"));
            Assert.That(result.IgnoredWorksheets, Is.EqualTo(["Transactions"]));
            Assert.That(result.Worksheets.Single().IgnoredColumns, Is.EqualTo(["Vendor note"]));
            Assert.That(result.Worksheets.Single().Errors, Is.Empty);
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

        var result = await _service.ImportAsync(file, preview.Sha256);

        var rules = _staged.OfType<PayeeMatchingRule>().ToArray();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Worksheets.Single().Created, Is.EqualTo(2));
            Assert.That(result.Worksheets.Single().Skipped, Is.Zero);
            Assert.That(result.Worksheets.Single().Updated, Is.Zero);
            Assert.That(result.Worksheets.Single().Failed, Is.Zero);
            Assert.That(rules, Has.Length.EqualTo(2));
            Assert.That(rules.Select(rule => rule.Key), Is.Unique.And.All.Not.EqualTo(Guid.Empty));
            Assert.That(rules.Select(rule => rule.TenantId), Is.All.EqualTo(42));
            Assert.That(rules[0].PayeePattern, Is.EqualTo("AMAZON.*"));
            Assert.That(rules[0].PayeeIsRegex, Is.True);
            Assert.That(rules[0].Category, Is.EqualTo("Shopping:Food"));
            Assert.That(rules[1].PayeePattern, Is.EqualTo("Coffee Shop"));
            Assert.That(rules[1].PayeeIsRegex, Is.False);
            Assert.That(rules.Select(rule => rule.CreatedAt), Is.All.EqualTo(_timeProvider.Object.GetUtcNow()));
            Assert.That(rules.Select(rule => rule.ModifiedAt), Is.All.EqualTo(_timeProvider.Object.GetUtcNow()));
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
        var worksheet = preview.Worksheets.Single();
        var error = worksheet.Errors.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(preview.CanImport, Is.False);
            Assert.That(worksheet.ValidRecordCount, Is.Zero);
            Assert.That(worksheet.Errors, Has.Count.EqualTo(1));
            Assert.That(error.RowNumber, Is.EqualTo(2));
            Assert.That(error.Field, Is.EqualTo("Name"));
            Assert.That(error.Code, Is.EqualTo("invalid_value"));
            Assert.That(error.Message, Does.Contain("not supported"));
            Assert.That(_staged, Is.Empty);
        }
    }

    [Test]
    public async Task Import_InvalidRowsThrowsStructuredValidationAndWritesNothing()
    {
        var file = Workbook(["Name", "Category"], [["/(?=coffee)/", "Food"]]);
        var preview = _service.Preview(file);

        var exception = await Assert.ThrowsAsync<TenantDataImportValidationException>(
            () => _service.ImportAsync(file, preview.Sha256));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exception!.Preview.Worksheets.Single().Errors.Single().RowNumber, Is.EqualTo(2));
            Assert.That(exception.Title, Is.EqualTo("Import could not be completed."));
            Assert.That(exception.Message, Does.Contain("Review the workbook preview"));
            Assert.That(_staged, Is.Empty);
        }
        _data.Verify(data => data.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public void Preview_DuplicateRequiredHeadersAreReported()
    {
        var file = Workbook(["Name", " name ", "Category"], [["Coffee", "Coffee", "Food"]]);

        var preview = _service.Preview(file);

        Assert.That(preview.Worksheets.Single().Errors.Select(error => error.Code), Does.Contain("duplicate_header"));
        Assert.That(preview.CanImport, Is.False);
    }

    [Test]
    public void Preview_MissingPayeeWorksheetIsNotImportable()
    {
        var file = Workbook(["Name", "Category"], [["Coffee", "Food"]], worksheetName: "Transactions");

        var preview = _service.Preview(file);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(preview.FormatId, Is.Null);
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
            Assert.That(preview.Worksheets.Single().Errors.Single().Code, Is.EqualTo("formula_not_allowed"));
            Assert.That(preview.Worksheets.Single().Errors.Single().Field, Is.EqualTo("Name"));
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
    public async Task Preview_NullFileAndImportNullArgumentsThrow()
    {
        Assert.Throws<ArgumentNullException>(() => _service.Preview(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _service.ImportAsync(null!, "SHA"));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.ImportAsync([1, 2, 3], ""));
    }

    [Test]
    public void Preview_MultipleDetectorsThrowsValidationException()
    {
        var file = Workbook(["Name", "Category"], [["Coffee", "Food"]]);
        var first = new Mock<IWorkbookImportFormatDetector>();
        var second = new Mock<IWorkbookImportFormatDetector>();
        first.Setup(detector => detector.FormatId).Returns("first");
        second.Setup(detector => detector.FormatId).Returns("second");
        first.Setup(detector => detector.CanDetect(It.IsAny<XLWorkbook>())).Returns(true);
        second.Setup(detector => detector.CanDetect(It.IsAny<XLWorkbook>())).Returns(true);
        var feature = new TenantDataImportFeature([first.Object, second.Object], []);

        var exception = Assert.Throws<ValidationException>(() => feature.Preview(file));

        Assert.That(exception!.Message, Does.Contain("multiple supported source formats"));
    }

    [Test]
    public void Preview_MultipleHandlersForWorksheetThrowsInvalidOperationException()
    {
        var file = Workbook(["Name", "Category"], [["Coffee", "Food"]]);
        var detector = new Mock<IWorkbookImportFormatDetector>();
        detector.Setup(detector => detector.FormatId).Returns("test");
        detector.Setup(detector => detector.CanDetect(It.IsAny<XLWorkbook>())).Returns(true);
        var handlers = Enumerable.Range(0, 2).Select(index =>
        {
            var handler = new Mock<IImportWorksheetHandler>();
            handler.Setup(item => item.FormatId).Returns("test");
            handler.Setup(item => item.WorksheetName).Returns("Payee");
            return handler.Object;
        }).ToArray();
        var feature = new TenantDataImportFeature([detector.Object], handlers);

        var exception = Assert.Throws<InvalidOperationException>(() => feature.Preview(file));

        Assert.That(exception!.Message, Does.Contain("Multiple import handlers"));
    }

    [Test]
    public async Task Import_CancelledTokenThrowsBeforeHandlerImport()
    {
        var file = Workbook(["Name", "Category"], [["Coffee", "Food"]]);
        var preview = _service.Preview(file);
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _service.ImportAsync(file, preview.Sha256, source.Token));

        _data.Verify(data => data.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Import_MissingRequiredWorksheetThrowsStructuredValidation()
    {
        var file = Workbook(["Name", "Category"], [["Coffee", "Food"]]);
        var detector = new Mock<IWorkbookImportFormatDetector>();
        detector.Setup(detector => detector.FormatId).Returns("test");
        detector.Setup(detector => detector.CanDetect(It.IsAny<XLWorkbook>())).Returns(true);
        var handler = new Mock<IImportWorksheetHandler>();
        handler.Setup(item => item.FormatId).Returns("test");
        handler.Setup(item => item.WorksheetName).Returns("Payee");
        handler.Setup(item => item.DependencyOrder).Returns(1);
        handler.Setup(item => item.RequiresWorksheetNames).Returns(["Missing"]);
        handler.Setup(item => item.Preview(It.IsAny<IXLWorksheet>())).Returns(new ImportWorksheetPreviewDto(
            "Payee", "Test", 1, 0, [], [], []));
        var feature = new TenantDataImportFeature([detector.Object], [handler.Object]);

        var sha = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(file));
        await Assert.ThrowsAsync<TenantDataImportValidationException>(() => feature.ImportAsync(file, sha));
    }

    [Test]
    public async Task Import_ContextHandlerReceivesWorkbookAndCompletesSuccessfully()
    {
        var file = Workbook(["Name", "Category"], [["Coffee", "Food"]]);
        var detector = new Mock<IWorkbookImportFormatDetector>();
        detector.Setup(detector => detector.FormatId).Returns("test");
        detector.Setup(detector => detector.CanDetect(It.IsAny<XLWorkbook>())).Returns(true);
        var handler = new ContextImportHandler();
        var feature = new TenantDataImportFeature([detector.Object], [handler]);
        var sha = Convert.ToHexString(SHA256.HashData(file));

        var result = await feature.ImportAsync(file, sha);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(handler.ContextSet, Is.True);
            Assert.That(handler.ImportCalled, Is.True);
            Assert.That(result.Worksheets, Has.Count.EqualTo(1));
        }
    }

    [Test]
    public async Task Import_DependencyNotYetCompletedThrowsStructuredValidation()
    {
        var file = Workbook(["Name", "Category"], [["Coffee", "Food"]]);
        using var workbook = new XLWorkbook();
        workbook.Worksheets.Add("Payee");
        workbook.Worksheets.Add("Second");
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        file = stream.ToArray();

        var detector = new Mock<IWorkbookImportFormatDetector>();
        detector.Setup(detector => detector.FormatId).Returns("test");
        detector.Setup(detector => detector.CanDetect(It.IsAny<XLWorkbook>())).Returns(true);
        var handler = new Mock<IImportWorksheetHandler>();
        handler.Setup(item => item.FormatId).Returns("test");
        handler.Setup(item => item.WorksheetName).Returns("Payee");
        handler.Setup(item => item.DependencyOrder).Returns(0);
        handler.Setup(item => item.RequiresWorksheetNames).Returns(["Second"]);
        handler.Setup(item => item.Preview(It.IsAny<IXLWorksheet>())).Returns(new ImportWorksheetPreviewDto(
            "Payee", "Test", 1, 0, [], [], []));
        var feature = new TenantDataImportFeature([detector.Object], [handler.Object]);
        var sha = Convert.ToHexString(SHA256.HashData(file));

        await Assert.ThrowsAsync<TenantDataImportValidationException>(() => feature.ImportAsync(file, sha));
    }

    [Test]
    public void Import_IgnoredWorksheetInPreviewReportsIgnoredNames()
    {
        var file = Workbook(["Name", "Category"], [["Coffee", "Food"]], worksheetName: "Other");
        var detector = new Mock<IWorkbookImportFormatDetector>();
        detector.Setup(detector => detector.FormatId).Returns("test");
        detector.Setup(detector => detector.CanDetect(It.IsAny<XLWorkbook>())).Returns(true);
        var feature = new TenantDataImportFeature([detector.Object], []);

        var preview = feature.Preview(file);

        Assert.That(preview.IgnoredWorksheets, Is.EqualTo(["Other"]));
    }

    [Test]
    public void DependencyRegistration_ResolvesImportCoordinatorAndHandlers()
    {
        var services = new ServiceCollection().AddApplicationFeatures();
        services.AddSingleton(_data.Object);
        services.AddSingleton<ITenantProvider>(_tenant);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.That(scope.ServiceProvider.GetRequiredService<TenantDataImportFeature>(), Is.Not.Null);
        var handlers = scope.ServiceProvider.GetRequiredService<IEnumerable<IImportWorksheetHandler>>().ToArray();
        Assert.That(handlers, Has.Some.TypeOf<OriginalYoFiPayeeWorksheetHandler>());
        Assert.That(handlers, Has.Some.TypeOf<NativeYoFiPayeeWorksheetHandler>());
        Assert.That(handlers, Has.Some.TypeOf<NativeYoFiTransactionWorksheetHandler>());
        Assert.That(handlers, Has.Some.TypeOf<NativeYoFiSplitWorksheetHandler>());
    }

    private sealed class ContextImportHandler : IImportWorksheetHandler, IWorkbookImportContextHandler
    {
        public string FormatId => "test";
        public string WorksheetName => "Payee";
        public string DataType => "Test";
        public int DependencyOrder => 1;
        public IReadOnlyCollection<string> RequiresWorksheetNames => [];
        public bool ContextSet { get; private set; }
        public bool ImportCalled { get; private set; }

        public void SetWorkbookContext(XLWorkbook workbook) => ContextSet = workbook.Worksheets.Count > 0;

        public ImportWorksheetPreviewDto Preview(IXLWorksheet worksheet) =>
            new("Payee", "Test", 1, 0, [], [], []);

        public Task<ImportWorksheetResultDto> ImportAsync(IXLWorksheet worksheet, CancellationToken cancellationToken)
        {
            ImportCalled = true;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new ImportWorksheetResultDto("Payee", "Test", 1, 0, 0, 0));
        }
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
