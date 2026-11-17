using System.Globalization;
using ClosedXML.Excel;
using DotNetNuxt.Abstractions;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Services;
using YoFi.V3.Entities.Exceptions;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Tenancy.Providers;

namespace YoFi.V3.Application.Features;

/// <summary>Provides tenant-scoped XLSX export for registered workspace data types.</summary>
/// <param name="tenantProvider">Current tenant established by tenant middleware.</param>
/// <param name="dataProvider">Persistence provider used to discover transaction years.</param>
/// <param name="worksheetWriters">Registered exporters for selectable data types.</param>
/// <param name="timeProvider">UTC clock used for complete-export filenames.</param>
public sealed class TenantDataExportFeature(
    ITenantProvider tenantProvider,
    IDataProvider dataProvider,
    IEnumerable<IExportWorksheetWriter> worksheetWriters,
    TimeProvider timeProvider)
{
    /// <summary>Gets the available selectable data types and years containing transaction data.</summary>
    public async Task<TenantDataExportOptionsDto> GetOptionsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var writers = GetWriters();
        var years = await dataProvider.ToListAsync(dataProvider.Get<Transaction>()
            .Where(transaction => transaction.TenantId == tenantProvider.CurrentTenant.Id)
            .Select(transaction => transaction.Date.Year)
            .Distinct()
            .OrderByDescending(year => year));
        cancellationToken.ThrowIfCancellationRequested();
        return new TenantDataExportOptionsDto(
            writers.Select(writer =>
                new TenantDataExportTypeDto(writer.DataType, writer.WorksheetName, writer.SupportsDateFilter)).ToArray(),
            years);
    }

    /// <summary>Creates a native YoFi.V3 workbook for the requested tenant data types.</summary>
    /// <param name="request">Types and optional inclusive transaction date filter.</param>
    /// <param name="cancellationToken">Token checked during workbook generation.</param>
    /// <returns>Workbook bytes, a descriptive filename, and per-worksheet counts.</returns>
    /// <exception cref="ValidationException">The selection or date filter is invalid.</exception>
    public async Task<TenantDataExportResultDto> ExportAsync(
        TenantDataExportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var writers = GetWriters();
        var selectedWriters = request.ExportAllData
            ? writers
            : ResolveSelection(request.DataTypes, writers);
        var (startDate, endDate) = request.ExportAllData
            ? (null, null)
            : ResolveDateRange(request);

        using var workbook = new XLWorkbook();
        WriteMetadata(workbook);

        var counts = new List<TenantDataExportCountDto>();
        foreach (var writer in selectedWriters)
        {
            cancellationToken.ThrowIfCancellationRequested();
            counts.AddRange(await writer.WriteAsync(
                workbook,
                tenantProvider.CurrentTenant.Id,
                writer.SupportsDateFilter ? startDate : null,
                writer.SupportsDateFilter ? endDate : null,
                cancellationToken));
        }

        cancellationToken.ThrowIfCancellationRequested();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return new TenantDataExportResultDto(
            stream.ToArray(),
            CreateFileName(selectedWriters, request, startDate, endDate),
            counts);
    }

    private IExportWorksheetWriter[] GetWriters()
    {
        var writers = worksheetWriters.OrderBy(writer => writer.DataType, StringComparer.Ordinal).ToArray();
        if (writers.Any(writer => string.IsNullOrWhiteSpace(writer.DataType)
            || string.IsNullOrWhiteSpace(writer.WorksheetName)
            || string.IsNullOrWhiteSpace(writer.FileNamePart))
            || writers.Select(writer => writer.DataType).Distinct(StringComparer.OrdinalIgnoreCase).Count() != writers.Length
            || writers.Select(writer => writer.WorksheetName).Append("Metadata")
                .Distinct(StringComparer.OrdinalIgnoreCase).Count() != writers.Length + 1)
        {
            throw new InvalidOperationException("Export worksheet writers must have unique, non-empty type and worksheet names.");
        }

        return writers;
    }

    private static IExportWorksheetWriter[] ResolveSelection(
        IReadOnlyCollection<string> dataTypes,
        IExportWorksheetWriter[] writers)
    {
        if (dataTypes is null || dataTypes.Count == 0)
        {
            throw new ValidationException(nameof(TenantDataExportRequest.DataTypes),
                "Select at least one data type, or set allData=true to request a complete export.");
        }

        var selected = new List<IExportWorksheetWriter>(dataTypes.Count);
        foreach (var dataType in dataTypes)
        {
            var writer = writers.SingleOrDefault(candidate =>
                string.Equals(candidate.DataType, dataType, StringComparison.OrdinalIgnoreCase));
            if (writer is null)
            {
                throw new ValidationException(nameof(TenantDataExportRequest.DataTypes),
                    $"The data type '{dataType}' is not available for export.");
            }

            if (selected.Contains(writer))
            {
                throw new ValidationException(nameof(TenantDataExportRequest.DataTypes),
                    $"The data type '{dataType}' was selected more than once.");
            }

            selected.Add(writer);
        }

        return selected.OrderBy(writer => writer.DataType, StringComparer.Ordinal).ToArray();
    }

    private static (DateOnly? StartDate, DateOnly? EndDate) ResolveDateRange(TenantDataExportRequest request)
    {
        if (request.Year is { } year)
        {
            if (year < 1 || year > 9999)
            {
                throw new ValidationException(nameof(request.Year), "Year must be between 1 and 9999.");
            }

            if (request.StartDate is not null || request.EndDate is not null)
            {
                throw new ValidationException(nameof(request.Year),
                    "Choose a year or a custom date range, not both.");
            }

            return (new DateOnly(year, 1, 1), new DateOnly(year, 12, 31));
        }

        if (request.StartDate is { } start && request.EndDate is { } end)
        {
            if (start > end)
            {
                throw new ValidationException(nameof(request.StartDate),
                    "The start date must be on or before the end date.");
            }

            return (start, end);
        }

        return (request.StartDate, request.EndDate);
    }

    private string CreateFileName(
        IReadOnlyCollection<IExportWorksheetWriter> selectedWriters,
        TenantDataExportRequest request,
        DateOnly? startDate,
        DateOnly? endDate)
    {
        var date = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (request.ExportAllData)
        {
            return $"yofi-complete-export-{date}.xlsx";
        }

        var typePart = string.Join("-", selectedWriters.Select(writer => writer.FileNamePart));
        var isDateBasedSelection = selectedWriters.Any(writer => writer.SupportsDateFilter);
        var datePart = !isDateBasedSelection
            ? string.Empty
            : startDate is { } start && endDate is { } end
                ? $"-{start:yyyy-MM-dd}-to-{end:yyyy-MM-dd}"
                : startDate is { } startOnly
                    ? $"-from-{startOnly:yyyy-MM-dd}"
                    : endDate is { } endOnly
                        ? $"-through-{endOnly:yyyy-MM-dd}"
                        : "-all-years";
        return $"yofi-{typePart}{datePart}-{date}.xlsx";
    }

    private static void WriteMetadata(XLWorkbook workbook)
    {
        var worksheet = workbook.Worksheets.Add("Metadata");
        worksheet.Cell(1, 1).Value = "Name";
        worksheet.Cell(1, 2).Value = "Value";
        worksheet.Cell(2, 1).Value = "Format";
        worksheet.Cell(2, 2).Value = "YoFi.V3";
    }
}
