using ClosedXML.Excel;
using DotNetNuxt.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using YoFi.V3.Application.Services;
using YoFi.V3.Entities.Models;
using YoFi.V3.Tests.Unit.TestHelpers;

namespace YoFi.V3.Tests.Unit.Application.TenantDataAdmin;

[TestFixture]
public class NativeYoFiPayeeWorksheetHandlerTests
{
    private static readonly Guid ExistingKey = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private Mock<IDataProvider> _data = null!;
    private MemoryCache _cache = null!;
    private NativeYoFiPayeeWorksheetHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _data = new Mock<IDataProvider>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        var tenant = new TestTenantProvider();
        tenant.CurrentTenant.Id = 42;
        var regex = new RegexValidationService();
        var timeProvider = new FixedTimeProvider();
        var service = new DataImportService(tenant, _data.Object, _cache, regex, timeProvider);
        _handler = new NativeYoFiPayeeWorksheetHandler(service, regex);
    }

    [TearDown]
    public void TearDown() => _cache.Dispose();

    [Test]
    public void Metadata_IdentifiesNativePayeeWorksheet()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_handler.FormatId, Is.EqualTo("yofi.v3"));
            Assert.That(_handler.WorksheetName, Is.EqualTo("PayeeMatchingRules"));
            Assert.That(_handler.DataType, Is.EqualTo("PayeeMatchingRules"));
            Assert.That(_handler.DependencyOrder, Is.EqualTo(100));
            Assert.That(_handler.RequiresWorksheetNames, Is.Empty);
        }
    }

    [Test]
    public void Preview_ValidRowsMapsTextBooleanAndSanitizesCategory()
    {
        using var workbook = Workbook(
            ["Key", "PayeePattern", "PayeeIsRegex", "Category", "Extra", "extra"],
            [
                [ExistingKey.ToString(), " /AMAZON.*/ ", true, " shopping : food ", "a", "b"],
                ["", "Coffee Shop", "FALSE", "Restaurants", "c", "d"]
            ]);

        var preview = _handler.Preview(workbook.Worksheet("PayeeMatchingRules"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(preview.ValidRecordCount, Is.EqualTo(2));
            Assert.That(preview.Errors, Is.Empty);
            Assert.That(preview.IgnoredColumns, Is.EqualTo(["Extra"]));
            Assert.That(preview.CanImport, Is.True);
        }
    }

    [Test]
    public void Preview_MissingRequiredHeadersReportsExactOnceRules()
    {
        using var workbook = Workbook(["Key", "PayeePattern"], []);

        var preview = _handler.Preview(workbook.Worksheet("PayeeMatchingRules"));

        Assert.That(preview.Errors.Select(error => (error.Field, error.Code)), Is.EquivalentTo(new[]
        {
            ("PayeeIsRegex", "missing_or_duplicate_header"),
            ("Category", "missing_or_duplicate_header")
        }));
    }

    [Test]
    public void Preview_DuplicateRequiredHeaderReportsHeaderErrors()
    {
        using var workbook = Workbook(["PayeePattern", "PayeePattern", "PayeeIsRegex", "Category"], []);

        var errors = _handler.Preview(workbook.Worksheet("PayeeMatchingRules")).Errors;

        Assert.That(errors.Select(error => (error.Field, error.Code)), Is.EquivalentTo(new[]
        {
            ("PayeePattern", "missing_or_duplicate_header")
        }));
    }

    [Test]
    public void Preview_DuplicateKeyHeaderReportsError()
    {
        using var workbook = Workbook(["Key", "Key", "PayeePattern", "PayeeIsRegex", "Category"], []);

        var error = _handler.Preview(workbook.Worksheet("PayeeMatchingRules")).Errors.Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(error.Field, Is.EqualTo("Key"));
            Assert.That(error.Code, Is.EqualTo("duplicate_header"));
        }
    }

    [Test]
    public void Preview_FormulasInvalidGuidAndDuplicateKeysReportRowErrors()
    {
        using var workbook = Workbook(["Key", "PayeePattern", "PayeeIsRegex", "Category"],
        [
            ["not-a-guid", "One", true, "Food"],
            [Guid.Empty.ToString(), "Two", true, "Food"],
            [ExistingKey.ToString(), "Three", true, "Food"],
            [ExistingKey.ToString(), "Four", true, "Food"]
        ]);
        var worksheet = workbook.Worksheet("PayeeMatchingRules");
        worksheet.Cell(2, 1).FormulaA1 = "\"not-a-guid\"";

        var errors = _handler.Preview(worksheet).Errors;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(errors.Any(error => error.RowNumber == 2 && error.Code == "formula_not_allowed"), Is.True);
            Assert.That(errors.Any(error => error.RowNumber == 3 && error.Code == "invalid_guid"), Is.True);
            Assert.That(errors.Any(error => error.RowNumber == 5 && error.Code == "duplicate_key"), Is.True);
        }
    }

    [Test]
    public void Preview_InvalidTextBooleanAndRegexReportFieldErrors()
    {
        using var workbook = Workbook(["PayeePattern", "PayeeIsRegex", "Category"],
        [
            [123, true, "Food"],
            ["Coffee", "maybe", "Food"],
            ["(?=coffee)", true, "Food"],
            ["", false, "   "],
            [new string('p', 201), false, new string('c', 201)]
        ]);

        var preview = _handler.Preview(workbook.Worksheet("PayeeMatchingRules"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(preview.Errors.Any(error => error.Field == "PayeePattern" && error.Code == "invalid_value"), Is.True);
            Assert.That(preview.Errors.Any(error => error.Field == "PayeeIsRegex" && error.Code == "invalid_value"), Is.True);
            Assert.That(preview.Errors.Any(error => error.Field == "PayeePattern" && error.Message.Contains("regex", StringComparison.OrdinalIgnoreCase)), Is.True);
            Assert.That(preview.Errors.Any(error => error.Field == "PayeePattern" && error.Message.Contains("required", StringComparison.OrdinalIgnoreCase)), Is.True);
            Assert.That(preview.Errors.Any(error => error.Field == "Category" && error.Message.Contains("whitespace", StringComparison.OrdinalIgnoreCase)), Is.True);
        }
    }

    [Test]
    public void Preview_BlankRowsAndFormulaTextAreIgnoredOrRejected()
    {
        using var workbook = Workbook(["PayeePattern", "PayeeIsRegex", "Category"], []);
        var worksheet = workbook.Worksheet("PayeeMatchingRules");
        worksheet.Cell(2, 1).FormulaA1 = "\"Coffee\"";
        worksheet.Cell(2, 2).Value = false;
        worksheet.Cell(2, 3).Value = "Food";
        worksheet.Row(3).Clear(XLClearOptions.All);

        var preview = _handler.Preview(worksheet);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(preview.ValidRecordCount, Is.Zero);
            Assert.That(preview.Errors.Any(error => error.RowNumber == 2
                && error.Field == "PayeePattern"), Is.True);
        }
    }

    [Test]
    public async Task Import_InvalidWorksheetThrowsBeforePersistence()
    {
        using var workbook = Workbook(["PayeePattern"], []);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.ImportAsync(workbook.Worksheet("PayeeMatchingRules"), CancellationToken.None));

        Assert.That(exception!.Message, Does.Contain("validated by the import coordinator"));
        _data.Verify(data => data.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task Import_ValidRowsCreatesAndUpdatesRules()
    {
        var existing = new PayeeMatchingRule
        {
            Key = ExistingKey,
            TenantId = 42,
            PayeePattern = "Old",
            PayeeIsRegex = false,
            Category = "Old",
            CreatedAt = DateTimeOffset.UtcNow,
            ModifiedAt = DateTimeOffset.UtcNow
        };
        var existingRules = new List<PayeeMatchingRule> { existing };
        _data.Setup(data => data.Get<PayeeMatchingRule>()).Returns(existingRules.AsQueryable());
        _data.Setup(data => data.ToListAsync(It.IsAny<IQueryable<PayeeMatchingRule>>()))
            .Returns((IQueryable<PayeeMatchingRule> query) => Task.FromResult(query.ToList()));
        _data.Setup(data => data.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        using var workbook = Workbook(["Key", "PayeePattern", "PayeeIsRegex", "Category"],
        [
            [ExistingKey.ToString(), "/AMAZON.*/", true, " shopping:food "],
            ["", "Coffee Shop", false, "restaurants"]
        ]);

        var result = await _handler.ImportAsync(workbook.Worksheet("PayeeMatchingRules"), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Created, Is.EqualTo(1));
            Assert.That(result.Updated, Is.EqualTo(1));
            Assert.That(result.Skipped, Is.Zero);
            Assert.That(existing.PayeePattern, Is.EqualTo("/AMAZON.*/"));
            Assert.That(existing.PayeeIsRegex, Is.True);
            Assert.That(existing.Category, Is.EqualTo("Shopping:Food"));
        }
        _data.Verify(data => data.Add(It.IsAny<PayeeMatchingRule>()), Times.Once);
        _data.Verify(data => data.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Import_CanceledWorksheetObservesCancellation()
    {
        using var workbook = Workbook(["PayeePattern", "PayeeIsRegex", "Category"],
            [["Coffee", false, "Food"]]);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _handler.ImportAsync(workbook.Worksheet("PayeeMatchingRules"), cancellation.Token));
    }

    private static XLWorkbook Workbook(IEnumerable<object?> headers, IEnumerable<object?[]> rows)
    {
        var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("PayeeMatchingRules");
        SetRow(worksheet, 1, headers.ToArray());
        var rowNumber = 2;
        foreach (var row in rows)
        {
            SetRow(worksheet, rowNumber++, row);
        }
        return workbook;
    }

    private static void SetRow(IXLWorksheet worksheet, int rowNumber, IReadOnlyList<object?> values)
    {
        for (var index = 0; index < values.Count; index++)
        {
            worksheet.Cell(rowNumber, index + 1).Value = XLCellValue.FromObject(values[index]);
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
    }
}
