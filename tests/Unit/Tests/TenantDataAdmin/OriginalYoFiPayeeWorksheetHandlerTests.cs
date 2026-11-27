using ClosedXML.Excel;
using DotNetNuxt.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Features;
using YoFi.V3.Application.Services;
using YoFi.V3.Entities.Models;
using YoFi.V3.Tests.Unit.TestHelpers;

namespace YoFi.V3.Tests.Unit.Application.TenantDataAdmin;

[TestFixture]
public class OriginalYoFiPayeeWorksheetHandlerTests
{
    private Mock<IDataProvider> _data = null!;
    private MemoryCache _cache = null!;
    private OriginalYoFiPayeeWorksheetHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _data = new Mock<IDataProvider>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        var tenant = new TestTenantProvider();
        tenant.CurrentTenant.Id = 42;
        var regex = new RegexValidationService();
        var time = new Mock<TimeProvider>();
        time.Setup(item => item.GetUtcNow()).Returns(new DateTimeOffset(2040, 7, 1, 12, 0, 0, TimeSpan.Zero));
        var service = new DataImportService(tenant, _data.Object, _cache, regex, time.Object);
        _handler = new OriginalYoFiPayeeWorksheetHandler(service, regex);
    }

    [TearDown]
    public void TearDown() => _cache.Dispose();

    [Test]
    public void MetadataIdentifiesOriginalWorksheet()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_handler.FormatId, Is.EqualTo("original-yofi"));
            Assert.That(_handler.WorksheetName, Is.EqualTo("Payee"));
            Assert.That(_handler.DataType, Is.EqualTo("PayeeMatchingRules"));
            Assert.That(_handler.DependencyOrder, Is.EqualTo(100));
            Assert.That(_handler.RequiresWorksheetNames, Is.Empty);
        }
    }

    [Test]
    public void PreviewMapsRegexPlainAndIgnoredHeaders()
    {
        using var workbook = Workbook(["ID", "Name", "Category", "Selected", "VendorNote"],
        [
            [1, " /AMAZON.*/ ", " shopping : food ", true, "ignored"],
            [2, "Coffee Shop", " restaurants ", false, "ignored"]
        ]);

        var preview = _handler.Preview(workbook.Worksheet("Payee"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(preview.ValidRecordCount, Is.EqualTo(2));
            Assert.That(preview.IgnoredColumns, Is.EqualTo(["VendorNote"]));
            Assert.That(preview.Errors, Is.Empty);
            Assert.That(preview.CanImport, Is.True);
        }
    }

    [TestCase("Name")]
    [TestCase("Category")]
    public void PreviewMissingRequiredHeaderReportsError(string missing)
    {
        var headers = missing == "Name" ? new object[] { "Category" } : new object[] { "Name" };
        using var workbook = Workbook(headers, [["Food"]]);

        var preview = _handler.Preview(workbook.Worksheet("Payee"));

        Assert.That(preview.Errors.Any(error => error.Field == missing && error.Code == "missing_header"), Is.True);
        Assert.That(preview.CanImport, Is.False);
    }

    [TestCase("Name")]
    [TestCase("Category")]
    public void PreviewDuplicateRequiredHeaderReportsError(string duplicate)
    {
        using var workbook = Workbook([duplicate, duplicate], [["A", "B"]]);

        var preview = _handler.Preview(workbook.Worksheet("Payee"));

        Assert.That(preview.Errors.Any(error => error.Code == "duplicate_header" && error.Field == duplicate), Is.True);
        Assert.That(preview.CanImport, Is.False);
    }

    [Test]
    public void PreviewFormulaNameAndCategoryReportFormulaErrors()
    {
        using var workbook = Workbook(["Name", "Category"], []);
        var worksheet = workbook.Worksheet("Payee");
        worksheet.Cell(2, 1).FormulaA1 = "\"Coffee\"";
        worksheet.Cell(2, 2).Value = "Food";
        worksheet.Cell(3, 1).Value = "Tea";
        worksheet.Cell(3, 2).FormulaA1 = "\"Food\"";

        var preview = _handler.Preview(worksheet);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(preview.Errors.Any(error => error.Field == "Name" && error.Code == "formula_not_allowed"), Is.True);
            Assert.That(preview.Errors.Any(error => error.Field == "Category" && error.Code == "formula_not_allowed"), Is.True);
        }
    }

    [Test]
    public void PreviewInvalidCellTypesBlankNameAndBlankCategoryProduceSpecificResults()
    {
        using var workbook = Workbook(["Name", "Category"],
        [
            [123, "Food"],
            ["", "Food"],
            ["Coffee", "   "]
        ]);

        var preview = _handler.Preview(workbook.Worksheet("Payee"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(preview.Errors.Any(error => error.Code == "invalid_cell_type" && error.Field == "Name"), Is.True);
            Assert.That(preview.Errors.Any(error => error.Code == "required" && error.Field == "Name"), Is.True);
            Assert.That(preview.SkippedRows.Any(row => row.Code == "blank_category"), Is.True);
            Assert.That(preview.ValidRecordCount, Is.Zero);
        }
    }

    [Test]
    public void PreviewInvalidCategoryAndRegexReportInvalidValues()
    {
        using var workbook = Workbook(["Name", "Category"],
        [
            ["(?=coffee)", "Food"],
            ["Coffee", "::"],
            [new string('n', 201), "Food"]
        ]);

        var preview = _handler.Preview(workbook.Worksheet("Payee"));

        Assert.That(preview.Errors.Count, Is.GreaterThanOrEqualTo(3));
        Assert.That(preview.Errors.All(error => error.Code == "invalid_value"), Is.True);
    }

    [Test]
    public void PreviewBlankRowsAndEmptyWorksheetReturnNoRecords()
    {
        using var workbook = Workbook(["Name", "Category"], []);
        var worksheet = workbook.Worksheet("Payee");
        worksheet.Row(2).Clear(XLClearOptions.All);

        var preview = _handler.Preview(worksheet);

        Assert.That(preview.ValidRecordCount, Is.Zero);
        Assert.That(preview.CanImport, Is.True);
    }

    [Test]
    public async Task ImportInvalidWorksheetThrowsBeforePersistence()
    {
        using var workbook = Workbook(["Name"], [["Coffee"]]);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.ImportAsync(workbook.Worksheet("Payee"), CancellationToken.None));

        _data.Verify(data => data.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ImportValidRowsStagesRulesAndSavesOnce()
    {
        using var workbook = Workbook(["Name", "Category"],
        [
            ["/AMAZON.*/", " shopping : food "],
            ["Coffee Shop", " restaurants "]
        ]);
        var staged = new List<IModel>();
        _data.Setup(data => data.AddRange(It.IsAny<IEnumerable<IModel>>()))
            .Callback((IEnumerable<IModel> models) => staged.AddRange(models));
        _data.Setup(data => data.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var result = await _handler.ImportAsync(workbook.Worksheet("Payee"), CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Created, Is.EqualTo(2));
            Assert.That(result.Skipped, Is.Zero);
            Assert.That(staged.OfType<PayeeMatchingRule>().Count(), Is.EqualTo(2));
            Assert.That(staged.OfType<PayeeMatchingRule>().Any(rule => rule.PayeeIsRegex && rule.PayeePattern == "AMAZON.*"), Is.True);
        }
        _data.Verify(data => data.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ImportCancelledTokenThrowsBeforeStaging()
    {
        using var workbook = Workbook(["Name", "Category"], [["Coffee", "Food"]]);
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _handler.ImportAsync(workbook.Worksheet("Payee"), source.Token));

        _data.Verify(data => data.AddRange(It.IsAny<IEnumerable<IModel>>()), Times.Never);
    }

    private static XLWorkbook Workbook(object[] headers, object[][] rows)
    {
        var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Payee");
        for (var column = 0; column < headers.Length; column++)
            worksheet.Cell(1, column + 1).Value = headers[column].ToString();
        for (var row = 0; row < rows.Length; row++)
            for (var column = 0; column < rows[row].Length; column++)
            {
                var value = rows[row][column];
                var cell = worksheet.Cell(row + 2, column + 1);
                switch (value)
                {
                    case int integer:
                        cell.Value = integer;
                        break;
                    case bool boolean:
                        cell.Value = boolean;
                        break;
                    default:
                        cell.Value = value?.ToString() ?? string.Empty;
                        break;
                }
            }
        return workbook;
    }
}
