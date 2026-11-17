namespace YoFi.V3.Application.Dto;

/// <summary>One selectable tenant data type for export.</summary>
/// <param name="DataType">Stable data type identifier accepted by an export request.</param>
/// <param name="WorksheetName">Worksheet written for the selected type.</param>
/// <param name="SupportsDateFilter">Whether the export supports transaction-date filtering.</param>
public record TenantDataExportTypeDto(string DataType, string WorksheetName, bool SupportsDateFilter);

/// <summary>Selectable data types and transaction years available to export.</summary>
/// <param name="DataTypes">Registered data types that can be exported.</param>
/// <param name="Years">Years with transaction data, sorted newest first.</param>
public record TenantDataExportOptionsDto(
    IReadOnlyCollection<TenantDataExportTypeDto> DataTypes,
    IReadOnlyCollection<int> Years);

/// <summary>Selection and optional transaction-date filtering for an export.</summary>
/// <param name="DataTypes">Stable identifiers for the selected types.</param>
/// <param name="StartDate">Optional inclusive custom range start; omit for an open start.</param>
/// <param name="EndDate">Optional inclusive custom range end; omit for an open end.</param>
/// <param name="ExportAllData">Exports every registered type without date filtering.</param>
public record TenantDataExportRequest(
    IReadOnlyCollection<string> DataTypes,
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    bool ExportAllData = false);

/// <summary>Exported record count for one worksheet data type.</summary>
/// <param name="DataType">Stable data type identifier.</param>
/// <param name="RecordCount">Number of records included in the worksheet.</param>
public record TenantDataExportCountDto(string DataType, int RecordCount);

/// <summary>Generated XLSX content, attachment filename, and exported record counts.</summary>
/// <param name="Content">Complete XLSX workbook bytes.</param>
/// <param name="FileName">Descriptive attachment filename.</param>
/// <param name="Counts">Per-worksheet data type record counts.</param>
public record TenantDataExportResultDto(
    byte[] Content,
    string FileName,
    IReadOnlyCollection<TenantDataExportCountDto> Counts);
