using DotNetNuxt.Abstractions;
using YoFi.V3.Application.Dto;
using YoFi.V3.Entities.Exceptions;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Tenancy.Providers;

namespace YoFi.V3.Application.Features;

/// <summary>
/// A split amount grouped by exact category and calendar month after the tenant/year query.
/// </summary>
internal sealed record ReportSplitAmount(string Category, int Month, decimal Amount);

/// <summary>
/// Provides the built-in report catalog and tenant-scoped yearly report data.
/// </summary>
public class ReportsFeature(
    ITenantProvider tenantProvider,
    IDataProvider dataProvider,
    TimeProvider timeProvider)
{
    private static readonly IReadOnlyList<ReportDefinition> Definitions =
    [
        new("income", "Income", "Income", 1, false, false),
        new("taxes", "Taxes", "Taxes", 1, false, false),
        new("expenses", "Expenses", null, 1, false, true, ReportCategoryFilter.Expenses),
        new("savings", "Savings", "Savings", 1, false, false),
        new("transfers", "Transfers", "Transfer", 1, false, false),
        new("income-detail", "Income Detail", "Income", 2, true, false),
        new("taxes-detail", "Taxes Detail", "Taxes", 2, true, false),
        new("expenses-detail", "Expenses Detail", null, 2, true, true, ReportCategoryFilter.Expenses),
        new("savings-detail", "Savings Detail", "Savings", 2, false, false),
        new("all", "All", null, 1, false, true, ReportCategoryFilter.All),
        new("all-detail", "All Detail", null, 2, true, true, ReportCategoryFilter.All)
    ];

    /// <summary>
    /// Gets the definitions of all built-in reports.
    /// </summary>
    public IReadOnlyList<ReportDefinitionDto> GetAvailableReports()
        => Definitions
            .Select(definition => new ReportDefinitionDto(
                definition.Id,
                definition.Name,
                definition.DefaultDepth,
                definition.HasMonthColumns,
                definition.IncludesUncategorized))
            .ToArray();

    /// <summary>
    /// Gets the current tenant's report data for a calendar year.
    /// </summary>
    /// <param name="reportId">Stable ID of a built-in report.</param>
    /// <param name="year">Calendar year, or the current UTC year when omitted.</param>
    /// <exception cref="KeyNotFoundException">The report ID is not in the built-in catalog.</exception>
    /// <exception cref="ValidationException">The year is outside the supported calendar range.</exception>
    public async Task<ReportResultDto> GetReportAsync(string reportId, int? year = null)
    {
        var definition = Definitions.FirstOrDefault(item =>
            string.Equals(item.Id, reportId, StringComparison.Ordinal));
        if (definition is null)
        {
            throw new KeyNotFoundException($"Report with ID '{reportId}' was not found.");
        }

        var reportYear = year ?? timeProvider.GetUtcNow().Year;
        if (reportYear < DateOnly.MinValue.Year || reportYear > DateOnly.MaxValue.Year)
        {
            throw new ValidationException(nameof(year), "Year must be between 1 and 9999.");
        }

        var startDate = new DateOnly(reportYear, 1, 1);
        var endDate = new DateOnly(reportYear, 12, 31);
        var splitQuery = dataProvider.Get<Transaction>()
            .Where(transaction =>
                transaction.TenantId == tenantProvider.CurrentTenant.Id
                && transaction.Date >= startDate
                && transaction.Date <= endDate)
            .SelectMany(transaction => transaction.Splits.Select(split =>
                new { split.Category, Month = transaction.Date.Month, split.Amount }))
            .GroupBy(split => new { split.Category, split.Month })
            .Select(group => new ReportSplitAmount(
                group.Key.Category,
                group.Key.Month,
                group.Sum(split => split.Amount)));

        var splits = await dataProvider.ToListNoTrackingAsync(splitQuery);
        return BuildReport(definition, reportYear, splits);
    }

    /// <summary>
    /// Filters queried split totals, builds the visible category tree, and creates the response.
    /// The report grand total is calculated from exact categories before roll-ups so parent and
    /// child rows can never cause the same amount to be counted more than once.
    /// </summary>
    /// <param name="definition">Catalog settings that determine filtering and display depth.</param>
    /// <param name="year">Calendar year represented by the result.</param>
    /// <param name="splits">Database-aggregated split amounts grouped by category and month.</param>
    /// <returns>The report rows and independently calculated month and annual totals.</returns>
    private static ReportResultDto BuildReport(
        ReportDefinition definition,
        int year,
        IEnumerable<ReportSplitAmount> splits)
    {
        var matchingSplits = splits
            .Where(split => IsIncluded(definition, split.Category))
            .ToArray();

        var exactCategoryAmounts = matchingSplits
            .GroupBy(split => split.Category, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, SumMonths, StringComparer.Ordinal);

        var grandTotals = new decimal[12];
        foreach (var amounts in exactCategoryAmounts.Values)
        {
            AddAmounts(grandTotals, amounts);
        }

        var grandTotal = grandTotals.Sum();
        var roots = new List<CategoryNode>();
        foreach (var (category, amounts) in exactCategoryAmounts)
        {
            if (category.Length == 0)
            {
                var uncategorized = new CategoryNode("Uncategorized", "Uncategorized");
                AddAmounts(uncategorized.DirectAmounts, amounts);
                roots.Add(uncategorized);
                continue;
            }

            var segments = category.Split(':');
            var firstDisplayedSegment = definition.CategoryRoot is null ? 0 : 1;
            if (firstDisplayedSegment == segments.Length)
            {
                continue;
            }

            var nodes = roots;
            CategoryNode? node = null;
            for (var index = firstDisplayedSegment; index < segments.Length; index++)
            {
                var segment = segments[index];
                var path = string.Join(":", segments.Take(index + 1));
                node = nodes.FirstOrDefault(candidate =>
                    string.Equals(candidate.Name, segment, StringComparison.Ordinal));
                if (node is null)
                {
                    node = new CategoryNode(segment, path);
                    nodes.Add(node);
                }

                nodes = node.Children;
            }

            AddAmounts(node!.DirectAmounts, amounts);
        }

        var rows = new List<ReportRowDto>();
        foreach (var root in OrderSiblings(roots))
        {
            AppendRows(root, 1, definition, grandTotal, rows);
        }

        return new ReportResultDto(
            definition.Id,
            definition.Name,
            year,
            definition.HasMonthColumns,
            rows,
            new ReportTotalsDto(
                definition.HasMonthColumns ? grandTotals : null,
                grandTotal));
    }

    /// <summary>
    /// Applies a report's category-family rule to an exact category path.
    /// Only the first segment determines reserved-family membership; matching a family name
    /// does not match longer names such as <c>IncomeTax</c>.
    /// </summary>
    /// <param name="definition">The report filter and uncategorized setting.</param>
    /// <param name="category">The exact category path, or an empty string for uncategorized.</param>
    /// <returns><see langword="true"/> when the split belongs in the report.</returns>
    private static bool IsIncluded(ReportDefinition definition, string category)
    {
        if (category.Length == 0)
        {
            return definition.IncludesUncategorized;
        }

        if (definition.CategoryFilter == ReportCategoryFilter.All)
        {
            return true;
        }

        var separatorIndex = category.IndexOf(':');
        var topLevel = separatorIndex < 0 ? category : category[..separatorIndex];
        if (definition.CategoryFilter == ReportCategoryFilter.Expenses)
        {
            return !ReservedTopLevelCategories.Contains(topLevel);
        }

        return string.Equals(topLevel, definition.CategoryRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static readonly HashSet<string> ReservedTopLevelCategories =
        new(StringComparer.OrdinalIgnoreCase) { "Income", "Taxes", "Savings", "Transfer" };

    /// <summary>
    /// Creates a January-to-December amount vector from grouped split amounts.
    /// </summary>
    /// <param name="splits">Amounts for one exact category, potentially across all twelve months.</param>
    /// <returns>A twelve-element array whose index zero represents January.</returns>
    private static decimal[] SumMonths(IEnumerable<ReportSplitAmount> splits)
    {
        var amounts = new decimal[12];
        foreach (var split in splits)
        {
            amounts[split.Month - 1] += split.Amount;
        }

        return amounts;
    }

    /// <summary>
    /// Adds one January-to-December amount vector into another vector in place.
    /// </summary>
    /// <param name="target">The vector to update.</param>
    /// <param name="amounts">The vector to add, aligned by calendar month.</param>
    private static void AddAmounts(decimal[] target, decimal[] amounts)
    {
        for (var month = 0; month < target.Length; month++)
        {
            target[month] += amounts[month];
        }
    }

    /// <summary>
    /// Emits a category subtotal before its descendants in depth-first order.
    /// The row amount includes direct amounts and every descendant, while the separate report
    /// total remains based on exact categories. Nodes below the display depth are traversed but
    /// not emitted, because their values have already rolled into the deepest visible ancestor.
    /// </summary>
    /// <param name="node">Category node to emit and traverse.</param>
    /// <param name="depth">Displayed depth, with report-level categories at depth one.</param>
    /// <param name="definition">Report settings that control depth and month columns.</param>
    /// <param name="grandTotal">Exact-category report total used as the percentage denominator.</param>
    /// <param name="rows">Output collection receiving rows in depth-first order.</param>
    private static void AppendRows(
        CategoryNode node,
        int depth,
        ReportDefinition definition,
        decimal grandTotal,
        ICollection<ReportRowDto> rows)
    {
        var amounts = node.DirectAmounts.ToArray();
        foreach (var child in node.Children)
        {
            AddAmounts(amounts, child.DirectAmounts);
            AddDescendantAmounts(amounts, child);
        }

        if (depth <= definition.DefaultDepth)
        {
            var annualTotal = amounts.Sum();
            rows.Add(new ReportRowDto(
                node.Path,
                node.Name,
                depth,
                depth < definition.DefaultDepth && node.Children.Count > 0,
                definition.HasMonthColumns ? amounts : null,
                annualTotal,
                grandTotal == 0 ? null : annualTotal / grandTotal));
        }

        foreach (var child in OrderSiblings(node.Children))
        {
            AppendRows(child, depth + 1, definition, grandTotal, rows);
        }
    }

    /// <summary>
    /// Adds every descendant's direct amounts into an ancestor's amount vector.
    /// This is used to calculate subtotals without treating already-rolled-up node totals as
    /// new source amounts.
    /// </summary>
    /// <param name="amounts">Ancestor vector to update.</param>
    /// <param name="node">Node whose descendants are included.</param>
    private static void AddDescendantAmounts(decimal[] amounts, CategoryNode node)
    {
        foreach (var child in node.Children)
        {
            AddAmounts(amounts, child.DirectAmounts);
            AddDescendantAmounts(amounts, child);
        }
    }

    /// <summary>
    /// Orders sibling categories case-insensitively, with ordinal ordering as a stable tie-breaker.
    /// </summary>
    /// <param name="nodes">Sibling nodes to sort.</param>
    /// <returns>Nodes in deterministic display order.</returns>
    private static IOrderedEnumerable<CategoryNode> OrderSiblings(IEnumerable<CategoryNode> nodes)
        => nodes.OrderBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(node => node.Name, StringComparer.Ordinal);

    /// <summary>
    /// In-memory category tree node storing only direct amounts; ancestors calculate subtotals
    /// by summing these direct values recursively.
    /// </summary>
    private sealed class CategoryNode(string name, string path)
    {
        public string Name { get; } = name;
        public string Path { get; } = path;
        public decimal[] DirectAmounts { get; } = new decimal[12];
        public List<CategoryNode> Children { get; } = [];
    }

    /// <summary>
    /// Internal catalog configuration separating category selection from table presentation.
    /// </summary>
    private sealed record ReportDefinition(
        string Id,
        string Name,
        string? CategoryRoot,
        int DefaultDepth,
        bool HasMonthColumns,
        bool IncludesUncategorized,
        ReportCategoryFilter CategoryFilter = ReportCategoryFilter.Root);

    /// <summary>
    /// Describes the built-in category-selection rule independently of display settings.
    /// </summary>
    private enum ReportCategoryFilter
    {
        Root,
        Expenses,
        All
    }
}
