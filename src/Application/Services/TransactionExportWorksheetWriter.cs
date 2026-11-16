using ClosedXML.Excel;
using DotNetNuxt.Abstractions;
using YoFi.V3.Application.Dto;
using YoFi.V3.Entities.Models;

namespace YoFi.V3.Application.Services;

/// <summary>Writes tenant transactions and their splits to related workbook worksheets.</summary>
/// <param name="dataProvider">Tenant-scoped persistence provider.</param>
public sealed class TransactionExportWorksheetWriter(IDataProvider dataProvider) : IExportWorksheetWriter
{
    /// <inheritdoc />
    public string DataType => "Transactions";

    /// <inheritdoc />
    public string WorksheetName => "Transactions";

    /// <inheritdoc />
    public string FileNamePart => "transactions";

    /// <inheritdoc />
    public bool SupportsDateFilter => true;

    /// <inheritdoc />
    public async Task<IReadOnlyCollection<TenantDataExportCountDto>> WriteAsync(
        XLWorkbook workbook,
        long tenantId,
        DateOnly? startDate,
        DateOnly? endDate,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var transactionQuery = dataProvider.Get<Transaction>()
            .Where(transaction => transaction.TenantId == tenantId);
        if (startDate is { } from)
        {
            transactionQuery = transactionQuery.Where(transaction => transaction.Date >= from);
        }

        if (endDate is { } to)
        {
            transactionQuery = transactionQuery.Where(transaction => transaction.Date <= to);
        }

        var transactions = await dataProvider.ToListNoTrackingAsync(transactionQuery
            .OrderBy(transaction => transaction.Date)
            .ThenBy(transaction => transaction.Key)
            .Select(transaction => new TransactionExportRow(
                transaction.Id,
                transaction.Key,
                transaction.Date,
                transaction.Payee,
                transaction.Amount,
                transaction.Source,
                transaction.ExternalId,
                transaction.Memo)));
        cancellationToken.ThrowIfCancellationRequested();

        var transactionWorksheet = workbook.Worksheets.Add(WorksheetName);
        SetHeaders(transactionWorksheet, "Key", "Date", "Payee", "Amount", "Source", "ExternalId", "Memo");
        for (var index = 0; index < transactions.Count; index++)
        {
            var row = index + 2;
            var transaction = transactions[index];
            transactionWorksheet.Cell(row, 1).Value = transaction.Key.ToString("D");
            transactionWorksheet.Cell(row, 2).Value = transaction.Date.ToDateTime(TimeOnly.MinValue);
            transactionWorksheet.Cell(row, 3).Value = transaction.Payee;
            transactionWorksheet.Cell(row, 4).Value = transaction.Amount;
            transactionWorksheet.Cell(row, 5).Value = transaction.Source ?? string.Empty;
            transactionWorksheet.Cell(row, 6).Value = transaction.ExternalId ?? string.Empty;
            transactionWorksheet.Cell(row, 7).Value = transaction.Memo ?? string.Empty;
        }

        var transactionKeysById = transactions.ToDictionary(transaction => transaction.Id, transaction => transaction.Key);
        var splitsWorksheet = workbook.Worksheets.Add("Splits");
        SetHeaders(splitsWorksheet, "Key", "TransactionKey", "Amount", "Category", "Memo", "Order");
        if (transactionKeysById.Count == 0)
        {
            return
            [
                new TenantDataExportCountDto(DataType, 0),
                new TenantDataExportCountDto("Splits", 0)
            ];
        }

        cancellationToken.ThrowIfCancellationRequested();
        var transactionIds = transactionKeysById.Keys.ToArray();
        var splits = await dataProvider.ToListNoTrackingAsync(dataProvider.Get<Split>()
            .Where(split => transactionIds.Contains(split.TransactionId))
            .OrderBy(split => split.TransactionId)
            .ThenBy(split => split.Order)
            .ThenBy(split => split.Key)
            .Select(split => new SplitExportRow(
                split.Key,
                split.TransactionId,
                split.Amount,
                split.Category,
                split.Memo,
                split.Order)));
        cancellationToken.ThrowIfCancellationRequested();

        for (var index = 0; index < splits.Count; index++)
        {
            var row = index + 2;
            var split = splits[index];
            splitsWorksheet.Cell(row, 1).Value = split.Key.ToString("D");
            splitsWorksheet.Cell(row, 2).Value = transactionKeysById[split.TransactionId].ToString("D");
            splitsWorksheet.Cell(row, 3).Value = split.Amount;
            splitsWorksheet.Cell(row, 4).Value = split.Category;
            splitsWorksheet.Cell(row, 5).Value = split.Memo ?? string.Empty;
            splitsWorksheet.Cell(row, 6).Value = split.Order;
        }

        return
        [
            new TenantDataExportCountDto(DataType, transactions.Count),
            new TenantDataExportCountDto("Splits", splits.Count)
        ];
    }

    private static void SetHeaders(IXLWorksheet worksheet, params string[] headers)
    {
        for (var index = 0; index < headers.Length; index++)
        {
            worksheet.Cell(1, index + 1).Value = headers[index];
        }
    }

    private sealed record TransactionExportRow(
        int Id,
        Guid Key,
        DateOnly Date,
        string Payee,
        decimal Amount,
        string? Source,
        string? ExternalId,
        string? Memo);

    private sealed record SplitExportRow(
        Guid Key,
        int TransactionId,
        decimal Amount,
        string Category,
        string? Memo,
        int Order);
}
