using System.Text.Json.Serialization;

namespace YoFi.V3.Application.Dto;

/// <summary>
/// Built-in report metadata used to populate the report selector.
/// </summary>
public record ReportDefinitionDto(
    string Id,
    string Name,
    int DefaultDepth,
    bool HasMonthColumns,
    bool IncludesUncategorized);

/// <summary>
/// Annual report data with category rows and explicit report totals.
/// </summary>
public record ReportResultDto(
    string Id,
    string Name,
    int Year,
    bool HasMonthColumns,
    IReadOnlyList<ReportRowDto> Rows,
    ReportTotalsDto Totals);

/// <summary>
/// One category's annual amount and optional January-to-December amounts.
/// </summary>
/// <param name="CategoryPath">Full category path; uncategorized splits use "Uncategorized".</param>
/// <param name="Depth">Displayed depth below the report filter root.</param>
/// <param name="IsSubtotal">Whether this row includes amounts from descendant categories.</param>
/// <param name="MonthlyAmounts">Amounts indexed January through December, or null when months are hidden.</param>
/// <param name="AnnualTotal">Signed annual row amount including descendants.</param>
/// <param name="Percentage">Signed share of the report annual total, or null when that total is zero.</param>
public record ReportRowDto(
    string CategoryPath,
    int Depth,
    bool IsSubtotal,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal[]? MonthlyAmounts,
    decimal AnnualTotal,
    decimal? Percentage);

/// <summary>
/// Grand totals computed from included split amounts, without summing overlapping category rows.
/// </summary>
/// <param name="MonthlyAmounts">Grand totals indexed January through December, or null when months are hidden.</param>
/// <param name="AnnualTotal">Signed annual report total.</param>
public record ReportTotalsDto(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal[]? MonthlyAmounts,
    decimal AnnualTotal);
