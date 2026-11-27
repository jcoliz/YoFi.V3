using ClosedXML.Excel;
using DotNetNuxt.Abstractions;
using Moq;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Features;
using YoFi.V3.Application.Services;
using YoFi.V3.Entities.Exceptions;
using YoFi.V3.Entities.Models;
using YoFi.V3.Tests.Unit.TestHelpers;

namespace YoFi.V3.Tests.Unit.Application;

[TestFixture]
public class TenantDataExportFeatureTests
{
    private Mock<IDataProvider> _data = null!;
    private TestTenantProvider _tenant = null!;
    private Mock<TimeProvider> _time = null!;

    [SetUp]
    public void SetUp()
    {
        _data = new Mock<IDataProvider>();
        _tenant = new TestTenantProvider();
        _tenant.CurrentTenant.Id = 42;
        _time = new Mock<TimeProvider>();
        _time.Setup(time => time.GetUtcNow())
            .Returns(new DateTimeOffset(2040, 7, 1, 12, 0, 0, TimeSpan.Zero));
    }

    [Test]
    public async Task GetOptions_ReturnsSortedWritersAndDistinctTenantYears()
    {
        var transactions = new[]
        {
            new Transaction { TenantId = 42, Date = new DateOnly(2025, 1, 1) },
            new Transaction { TenantId = 42, Date = new DateOnly(2023, 1, 1) },
            new Transaction { TenantId = 42, Date = new DateOnly(2025, 6, 1) },
            new Transaction { TenantId = 99, Date = new DateOnly(2030, 1, 1) }
        };
        _data.Setup(data => data.Get<Transaction>()).Returns(transactions.AsQueryable());
        _data.Setup(data => data.ToListAsync(It.IsAny<IQueryable<int>>()))
            .Returns((IQueryable<int> query) => Task.FromResult(query.ToList()));
        var zeta = Writer("Zeta", "ZetaSheet", "zeta", false);
        var alpha = Writer("Alpha", "AlphaSheet", "alpha", true);
        var feature = Feature(alpha.Object, zeta.Object);

        var result = await feature.GetOptionsAsync();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.DataTypes.Select(type => type.DataType), Is.EqualTo(new[] { "Alpha", "Zeta" }));
            Assert.That(result.DataTypes.Select(type => type.WorksheetName), Is.EqualTo(new[] { "AlphaSheet", "ZetaSheet" }));
            Assert.That(result.DataTypes.Select(type => type.SupportsDateFilter), Is.EqualTo(new[] { true, false }));
            Assert.That(result.Years, Is.EqualTo(new[] { 2025, 2023 }));
        }
    }

    [Test]
    public async Task Export_AllDataWritesEveryWriterAndCreatesCompleteExportName()
    {
        var writer = Writer("Transactions", "Transactions", "transactions", true);
        writer.Setup(item => item.WriteAsync(It.IsAny<XLWorkbook>(), 42, null, null, It.IsAny<CancellationToken>()))
            .Callback<XLWorkbook, long, DateOnly?, DateOnly?, CancellationToken>((workbook, _, _, _, _) =>
                workbook.Worksheets.Add("Transactions"))
            .ReturnsAsync([new TenantDataExportCountDto("Transactions", 3)]);
        var feature = Feature(writer.Object);

        var result = await feature.ExportAsync(new TenantDataExportRequest([], ExportAllData: true));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.FileName, Is.EqualTo("yofi-complete-export-2040-07-01.xlsx"));
            Assert.That(result.Counts, Is.EqualTo(new[] { new TenantDataExportCountDto("Transactions", 3) }));
            Assert.That(result.Content, Is.Not.Empty);
        }
        using var workbook = new XLWorkbook(new MemoryStream(result.Content));
        Assert.That(workbook.Worksheets.Select(sheet => sheet.Name), Is.EqualTo(new[] { "Metadata", "Transactions" }));
    }

    [Test]
    public async Task Export_SelectedDataIsCaseInsensitiveSortedAndUsesInclusiveDateRange()
    {
        var zeta = Writer("Zeta", "ZetaSheet", "zeta", false);
        zeta.Setup(item => item.WriteAsync(It.IsAny<XLWorkbook>(), 42, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TenantDataExportCountDto("Zeta", 2)]);
        var alpha = Writer("Alpha", "AlphaSheet", "alpha", true);
        alpha.Setup(item => item.WriteAsync(It.IsAny<XLWorkbook>(), 42,
                new DateOnly(2025, 1, 2), new DateOnly(2025, 2, 3), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TenantDataExportCountDto("Alpha", 4)]);
        var feature = Feature(zeta.Object, alpha.Object);

        var result = await feature.ExportAsync(new TenantDataExportRequest(
            ["alpha", "ZETA"], new DateOnly(2025, 1, 2), new DateOnly(2025, 2, 3)));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.FileName, Is.EqualTo("yofi-alpha-zeta-2025-01-02-to-2025-02-03-2040-07-01.xlsx"));
            Assert.That(result.Counts.Select(count => count.DataType), Is.EqualTo(new[] { "Alpha", "Zeta" }));
        }
        alpha.Verify(item => item.WriteAsync(It.IsAny<XLWorkbook>(), 42,
            new DateOnly(2025, 1, 2), new DateOnly(2025, 2, 3), It.IsAny<CancellationToken>()), Times.Once);
        zeta.Verify(item => item.WriteAsync(It.IsAny<XLWorkbook>(), 42, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestCase(true, false, "-from-2025-01-02")]
    [TestCase(false, true, "-through-2025-02-03")]
    public async Task Export_DateBasedFilenameParts(bool startOnly, bool endOnly, string expectedSuffix)
    {
        var start = startOnly ? new DateOnly(2025, 1, 2) : (DateOnly?)null;
        var end = endOnly ? new DateOnly(2025, 2, 3) : (DateOnly?)null;
        var writer = Writer("Transactions", "Transactions", "transactions", true);
        writer.Setup(item => item.WriteAsync(It.IsAny<XLWorkbook>(), 42, start, end, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var feature = Feature(writer.Object);

        var result = await feature.ExportAsync(new TenantDataExportRequest(["Transactions"], start, end));

        Assert.That(result.FileName, Does.EndWith($"{expectedSuffix}-2040-07-01.xlsx"));
    }

    [Test]
    public async Task Export_SelectedDateBasedWriterWithoutDatesUsesAllYearsFilename()
    {
        var transactions = Writer("Transactions", "Transactions", "transactions", true);
        transactions.Setup(item => item.WriteAsync(It.IsAny<XLWorkbook>(), 42, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var rules = Writer("Rules", "Rules", "rules", false);
        var feature = Feature(transactions.Object, rules.Object);

        var result = await feature.ExportAsync(new TenantDataExportRequest(["Transactions"]));

        Assert.That(result.FileName, Is.EqualTo("yofi-transactions-all-years-2040-07-01.xlsx"));
    }

    [Test]
    public async Task Export_NonDateBasedSelectionDoesNotIncludeDates()
    {
        var writer = Writer("Rules", "Rules", "rules", false);
        writer.Setup(item => item.WriteAsync(It.IsAny<XLWorkbook>(), 42, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var feature = Feature(writer.Object);

        var result = await feature.ExportAsync(new TenantDataExportRequest(["Rules"],
            new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31)));

        Assert.That(result.FileName, Is.EqualTo("yofi-rules-2040-07-01.xlsx"));
        writer.Verify(item => item.WriteAsync(It.IsAny<XLWorkbook>(), 42, null, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Export_NullRequestThrowsArgumentNullException()
    {
        var feature = Feature(Writer("Alpha", "Alpha", "alpha", false).Object);

        await Assert.ThrowsAsync<ArgumentNullException>(() => feature.ExportAsync(null!));
    }

    [Test]
    public async Task Export_CancelledTokenThrowsBeforeWriting()
    {
        var feature = Feature(Writer("Alpha", "Alpha", "alpha", false).Object);
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => feature.ExportAsync(
            new TenantDataExportRequest(["Alpha"]), source.Token));
    }

    [Test]
    public async Task GetOptions_CancelledTokenThrows()
    {
        var feature = Feature(Writer("Alpha", "Alpha", "alpha", false).Object);
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => feature.GetOptionsAsync(source.Token));
    }

    [Test]
    public async Task Export_NoDataTypesThrowsValidationException()
    {
        var feature = Feature(Writer("Alpha", "Alpha", "alpha", false).Object);

        await Assert.ThrowsAsync<ValidationException>(() => feature.ExportAsync(new TenantDataExportRequest([])));
    }

    [Test]
    public async Task Export_UnknownDataTypeThrowsValidationException()
    {
        var feature = Feature(Writer("Alpha", "Alpha", "alpha", false).Object);

        await Assert.ThrowsAsync<ValidationException>(() => feature.ExportAsync(new TenantDataExportRequest(["Missing"])));
    }

    [Test]
    public async Task Export_DuplicateDataTypeThrowsValidationException()
    {
        var feature = Feature(Writer("Alpha", "Alpha", "alpha", false).Object);

        await Assert.ThrowsAsync<ValidationException>(() => feature.ExportAsync(new TenantDataExportRequest(["Alpha", "alpha"])));
    }

    [Test]
    public async Task Export_ReversedDateRangeThrowsValidationException()
    {
        var feature = Feature(Writer("Alpha", "Alpha", "alpha", true).Object);

        await Assert.ThrowsAsync<ValidationException>(() => feature.ExportAsync(new TenantDataExportRequest(
            ["Alpha"], new DateOnly(2025, 2, 1), new DateOnly(2025, 1, 1))));
    }

    [TestCase("", "AlphaSheet", "alpha")]
    [TestCase("Alpha", "", "alpha")]
    [TestCase("Alpha", "AlphaSheet", "")]
    [TestCase("Alpha", "Metadata", "alpha")]
    public async Task Export_InvalidWriterMetadataThrowsInvalidOperationException(string dataType, string worksheetName, string fileNamePart)
    {
        var writer = Writer(dataType, worksheetName, fileNamePart, false);
        var feature = Feature(writer.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => feature.ExportAsync(new TenantDataExportRequest(["Alpha"])));
    }

    [Test]
    public async Task Export_DuplicateWriterNamesThrowInvalidOperationException()
    {
        var first = Writer("Alpha", "SameSheet", "alpha", false);
        var second = Writer("Beta", "SameSheet", "beta", false);
        var feature = Feature(first.Object, second.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => feature.ExportAsync(new TenantDataExportRequest(["Alpha", "Beta"])));
    }

    [Test]
    public async Task Export_DuplicateDataTypesFromWritersThrowInvalidOperationException()
    {
        var first = Writer("Alpha", "AlphaSheet", "alpha", false);
        var second = Writer("alpha", "BetaSheet", "beta", false);
        var feature = Feature(first.Object, second.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => feature.ExportAsync(new TenantDataExportRequest(["Alpha"])));
    }

    [Test]
    public async Task GetOptions_QueryFailurePropagates()
    {
        _data.Setup(data => data.Get<Transaction>()).Returns(Array.Empty<Transaction>().AsQueryable());
        _data.Setup(data => data.ToListAsync(It.IsAny<IQueryable<int>>()))
            .ThrowsAsync(new InvalidOperationException("Query failed."));
        var feature = Feature(Writer("Alpha", "Alpha", "alpha", false).Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => feature.GetOptionsAsync());
    }

    [Test]
    public async Task Export_CancellationDuringWriterWritePropagates()
    {
        using var source = new CancellationTokenSource();
        var writer = Writer("Alpha", "Alpha", "alpha", false);
        writer.Setup(item => item.WriteAsync(It.IsAny<XLWorkbook>(), 42, null, null, source.Token))
            .Returns(async () =>
            {
                await Task.Yield();
                source.Cancel();
                source.Token.ThrowIfCancellationRequested();
                return Array.Empty<TenantDataExportCountDto>();
            });
        var feature = Feature(writer.Object);

        await Assert.ThrowsAsync<OperationCanceledException>(() => feature.ExportAsync(
            new TenantDataExportRequest(["Alpha"]), source.Token));
    }

    private TenantDataExportFeature Feature(params IExportWorksheetWriter[] writers) =>
        new(_tenant, _data.Object, writers, _time.Object);

    private static Mock<IExportWorksheetWriter> Writer(
        string dataType,
        string worksheetName,
        string fileNamePart,
        bool supportsDateFilter)
    {
        var writer = new Mock<IExportWorksheetWriter>();
        writer.SetupGet(item => item.DataType).Returns(dataType);
        writer.SetupGet(item => item.WorksheetName).Returns(worksheetName);
        writer.SetupGet(item => item.FileNamePart).Returns(fileNamePart);
        writer.SetupGet(item => item.SupportsDateFilter).Returns(supportsDateFilter);
        return writer;
    }
}
