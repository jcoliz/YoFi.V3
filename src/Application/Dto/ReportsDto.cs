using System.ComponentModel.DataAnnotations;
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
/// Available calendar years for the current tenant's reports and the backend-selected default.
/// </summary>
/// <param name="Years">Distinct transaction years plus the current UTC year, sorted newest first.</param>
/// <param name="DefaultYear">The current UTC year, always included in <paramref name="Years"/>.</param>
public sealed record ReportYearsDto(
    [property: Required] IReadOnlyList<int> Years,
    [property: Required] int DefaultYear);

/// <summary>
/// Annual report data with category rows and explicit report totals.
/// </summary>
/// <param name="Id">Stable report identifier.</param>
/// <param name="Name">Display name of the report.</param>
/// <param name="CategoryRoot">Category filter root used as the category-column header, or null for reports spanning roots.</param>
/// <param name="Year">Calendar year represented by the report.</param>
/// <param name="DisplayDepth">Effective number of category levels included below the report filter root.</param>
/// <param name="HasMonthColumns">Whether monthly amount columns are included.</param>
/// <param name="Rows">Category rows included in the report.</param>
/// <param name="Totals">Overall and category totals for the report.</param>
public record ReportResultDto(
    string Id,
    string Name,
    string? CategoryRoot,
    int Year,
    int DisplayDepth,
    bool HasMonthColumns,
    IReadOnlyList<ReportRowDto> Rows,
    ReportTotalsDto Totals);

/// <summary>
/// One category's annual amount and optional January-to-December amounts.
/// </summary>
/// <param name="CategoryPath">Full category path; uncategorized splits use "Uncategorized".</param>
/// <param name="CategoryName">Local category name for display; ancestor names are provided by parent rows or the report.</param>
/// <param name="Depth">Displayed depth below the report filter root.</param>
/// <param name="IsSubtotal">Whether this row includes amounts from descendant categories.</param>
/// <param name="MonthlyAmounts">Amounts indexed January through December, or null when months are hidden.</param>
/// <param name="AnnualTotal">Signed annual row amount including descendants.</param>
/// <param name="Percentage">Signed share of the report annual total, or null when that total is zero.</param>
public record ReportRowDto(
    string CategoryPath,
    string CategoryName,
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
