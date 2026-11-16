using System.Globalization;
using ClosedXML.Excel;
using DotNetNuxt.Abstractions;
using YoFi.V3.Application.Dto;
using YoFi.V3.Entities.Models;

namespace YoFi.V3.Application.Services;

/// <summary>Writes native YoFi.V3 payee matching rule records.</summary>
/// <param name="dataProvider">Tenant-scoped persistence provider.</param>
public sealed class PayeeMatchingRuleExportWorksheetWriter(IDataProvider dataProvider) : IExportWorksheetWriter
{
    /// <inheritdoc />
    public string DataType => "PayeeMatchingRules";

    /// <inheritdoc />
    public string WorksheetName => "PayeeMatchingRules";

    /// <inheritdoc />
    public string FileNamePart => "payee-matching-rules";

    /// <inheritdoc />
    public bool SupportsDateFilter => false;

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<TenantDataExportCountDto>> WriteAsync(
        XLWorkbook workbook,
        long tenantId,
        DateOnly? startDate,
        DateOnly? endDate,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var rules = await dataProvider.ToListNoTrackingAsync(dataProvider.Get<PayeeMatchingRule>()
            .Where(rule => rule.TenantId == tenantId)
            .OrderBy(rule => rule.PayeePattern)
            .ThenBy(rule => rule.Key)
            .Select(rule => new RuleExportRow(
                rule.Key,
                rule.PayeePattern,
                rule.PayeeIsRegex,
                rule.Category,
                rule.CreatedAt,
                rule.ModifiedAt,
                rule.LastUsedAt,
                rule.MatchCount)));
        cancellationToken.ThrowIfCancellationRequested();

        var worksheet = workbook.Worksheets.Add(WorksheetName);
        SetHeaders(
            worksheet,
            "Key",
            "PayeePattern",
            "PayeeIsRegex",
            "Category",
            "CreatedAt",
            "ModifiedAt",
            "LastUsedAt",
            "MatchCount");
        for (var index = 0; index < rules.Count; index++)
        {
            var row = index + 2;
            var rule = rules[index];
            worksheet.Cell(row, 1).Value = rule.Key.ToString("D");
            worksheet.Cell(row, 2).Value = rule.PayeePattern;
            worksheet.Cell(row, 3).Value = rule.PayeeIsRegex;
            worksheet.Cell(row, 4).Value = rule.Category;
            worksheet.Cell(row, 5).Value = rule.CreatedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
            worksheet.Cell(row, 6).Value = rule.ModifiedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
            worksheet.Cell(row, 7).Value =
                rule.LastUsedAt?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) ?? string.Empty;
            worksheet.Cell(row, 8).Value = rule.MatchCount;
        }

        return [new TenantDataExportCountDto(DataType, rules.Count)];
    }

    private static void SetHeaders(IXLWorksheet worksheet, params string[] headers)
    {
        for (var index = 0; index < headers.Length; index++)
        {
            worksheet.Cell(1, index + 1).Value = headers[index];
        }
    }

    private sealed record RuleExportRow(
        Guid Key,
        string PayeePattern,
        bool PayeeIsRegex,
        string Category,
        DateTimeOffset CreatedAt,
        DateTimeOffset ModifiedAt,
        DateTimeOffset? LastUsedAt,
        int MatchCount);
}
