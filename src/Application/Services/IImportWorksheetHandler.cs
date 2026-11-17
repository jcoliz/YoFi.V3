using ClosedXML.Excel;
using YoFi.V3.Application.Dto;

namespace YoFi.V3.Application.Services;

/// <summary>Maps and imports one worksheet for one detected source format and V3 data type.</summary>
public interface IImportWorksheetHandler
{
    /// <summary>Source format identifier this handler supports.</summary>
    string FormatId { get; }

    /// <summary>Source worksheet name this handler supports.</summary>
    string WorksheetName { get; }

    /// <summary>V3 data type receiving the imported records.</summary>
    string DataType { get; }

    /// <summary>Explicit worksheet dependency order; lower values execute first.</summary>
    int DependencyOrder { get; }

    /// <summary>Worksheet names that must be present and successfully committed first.</summary>
    IReadOnlyCollection<string> RequiresWorksheetNames { get; }

    /// <summary>Validates worksheet data without writing.</summary>
    ImportWorksheetPreviewDto Preview(IXLWorksheet worksheet);

    /// <summary>Revalidates and commits this worksheet as one persistence unit.</summary>
    Task<ImportWorksheetResultDto> ImportAsync(IXLWorksheet worksheet, CancellationToken cancellationToken);

}

/// <summary>
/// Provides workbook-level context to worksheet handlers that validate relationships across multiple sheets.
/// </summary>
/// <remarks>
/// This is an internal coordinator capability, not part of the public worksheet-handler contract. The
/// <see cref="YoFi.V3.Application.Features.TenantDataImportFeature"/> sets the current workbook before preview and import operations so
/// handlers can validate references between dependency-related worksheets, such as native splits against
/// transactions or original splits against transaction IDs.
/// </remarks>
internal interface IWorkbookImportContextHandler
{
    /// <summary>
    /// Sets the workbook containing the worksheet currently being previewed or imported.
    /// </summary>
    /// <param name="workbook">The open workbook, valid for the duration of the preview or import call.</param>
    void SetWorkbookContext(XLWorkbook workbook);
}
