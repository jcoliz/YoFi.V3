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

    /// <summary>Validates worksheet data without writing.</summary>
    ImportWorksheetPreviewDto Preview(IXLWorksheet worksheet);

    /// <summary>Revalidates and commits this worksheet as one persistence unit.</summary>
    Task<ImportWorksheetResultDto> ImportAsync(IXLWorksheet worksheet, CancellationToken cancellationToken);
}
