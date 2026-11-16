using ClosedXML.Excel;
using YoFi.V3.Application.Dto;

namespace YoFi.V3.Application.Services;

/// <summary>Writes one selectable tenant data type to an export workbook.</summary>
public interface IExportWorksheetWriter
{
    /// <summary>Stable data type identifier accepted by export requests.</summary>
    string DataType { get; }

    /// <summary>Primary worksheet name written by this exporter.</summary>
    string WorksheetName { get; }

    /// <summary>Filename fragment used to describe this data type.</summary>
    string FileNamePart { get; }

    /// <summary>Whether this data type responds to transaction-date filtering.</summary>
    bool SupportsDateFilter { get; }

    /// <summary>Writes the selected tenant's records into the workbook.</summary>
    Task<IReadOnlyCollection<TenantDataExportCountDto>> WriteAsync(
        XLWorkbook workbook,
        long tenantId,
        DateOnly? startDate,
        DateOnly? endDate,
        CancellationToken cancellationToken);
}
