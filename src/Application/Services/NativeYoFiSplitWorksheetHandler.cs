using System.Collections.Immutable;
using ClosedXML.Excel;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Helpers;
using YoFi.V3.Application.Validation;

namespace YoFi.V3.Application.Services;

/// <summary>Maps native YoFi.V3 split worksheet rows to native split import DTOs.</summary>
public sealed class NativeYoFiSplitWorksheetHandler(DataImportService dataImportService) : IImportWorksheetHandler
{
    private XLWorkbook? _workbook;
    public string FormatId => "yofi.v3";
    public string WorksheetName => "Splits";
    public string DataType => "Splits";
    public int DependencyOrder => 300;
    public IReadOnlyCollection<string> RequiresWorksheetNames => ["Transactions"];

    public void SetWorkbookContext(XLWorkbook workbook) => _workbook = workbook;

    public ImportWorksheetPreviewDto Preview(IXLWorksheet worksheet) => Analyze(worksheet).Preview(worksheet.Name, DataType);

    public async Task<ImportWorksheetResultDto> ImportAsync(IXLWorksheet worksheet, CancellationToken cancellationToken)
    {
        var analysis = Analyze(worksheet);
        var preview = analysis.Preview(worksheet.Name, DataType);
        if (!preview.CanImport)
            throw new InvalidOperationException("The worksheet must be validated by the import coordinator before persistence.");
        var counts = await dataImportService.UpsertNativeSplitsAsync(analysis.Splits, cancellationToken);
        return new ImportWorksheetResultDto(worksheet.Name, DataType, counts.CreatedSplits, 0, counts.UpdatedSplits, 0);
    }

    private Analysis Analyze(IXLWorksheet worksheet)
    {
        var errors = ImmutableArray.CreateBuilder<DataImportIssueDto>();
        var headers = ReadHeaders(worksheet);
        var required = new[] { "Key", "TransactionKey", "Amount", "Category", "Memo", "Order" };
        var ignored = headers.Names.Where(name => !required.Contains(name, StringComparer.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var name in required.Where(name => name != "Key"))
            if (!headers.Columns.ContainsKey(name)) errors.Add(new DataImportIssueDto(1, name, "missing_header", $"The required {name} column is missing."));
        if (headers.Duplicate("Key") || headers.Duplicate("TransactionKey")) errors.Add(new DataImportIssueDto(1, "Key", "duplicate_header", "Key and TransactionKey columns must appear at most once."));

        var validator = new SplitEditDtoValidator();
        var seenKeys = new HashSet<Guid>();
        var seenOrders = new Dictionary<Guid, HashSet<int>>();
        var splits = ImmutableArray.CreateBuilder<NativeSplitImportDto>();
        if (errors.Count == 0)
        {
            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
            for (var row = 2; row <= lastRow; row++)
            {
                if (worksheet.Row(row).IsEmpty()) continue;
                Guid? key = null;
                if (headers.Columns.TryGetValue("Key", out var keyColumn))
                {
                    var text = worksheet.Cell(row, keyColumn).GetString().Trim();
                    if (text.Length > 0)
                    {
                        if (!Guid.TryParse(text, out var parsed) || parsed == Guid.Empty || !seenKeys.Add(parsed))
                        { errors.Add(new DataImportIssueDto(row, "Key", "invalid_or_duplicate_key", "Key must be a unique valid GUID.")); continue; }
                        key = parsed;
                    }
                }
                if (!headers.Columns.TryGetValue("TransactionKey", out var transactionColumn) ||
                    !Guid.TryParse(worksheet.Cell(row, transactionColumn).GetString().Trim(), out var transactionKey))
                { errors.Add(new DataImportIssueDto(row, "TransactionKey", "invalid_guid", "TransactionKey must be a valid GUID.")); continue; }
                if (!headers.Columns.TryGetValue("Amount", out var amountColumn) ||
                    !worksheet.Cell(row, amountColumn).TryGetValue(out decimal amount))
                { errors.Add(new DataImportIssueDto(row, "Amount", "invalid_value", "Amount must be decimal.")); continue; }
                if (!headers.Columns.TryGetValue("Order", out var orderColumn) ||
                    !int.TryParse(worksheet.Cell(row, orderColumn).GetString().Trim(), out var order) || order < 0)
                { errors.Add(new DataImportIssueDto(row, "Order", "invalid_value", "Order must be a nonnegative integer.")); continue; }
                var orders = seenOrders.GetOrAdd(transactionKey);
                if (!orders.Add(order)) { errors.Add(new DataImportIssueDto(row, "Order", "duplicate_order", "Order must be unique within a transaction.")); continue; }

                var category = CategoryHelper.SanitizeCategory(headers.Columns.TryGetValue("Category", out var categoryColumn)
                    ? worksheet.Cell(row, categoryColumn).GetString() : string.Empty);
                var memo = headers.Columns.TryGetValue("Memo", out var memoColumn)
                    ? worksheet.Cell(row, memoColumn).GetString().Trim() : string.Empty;
                var split = new SplitEditDto(amount, category, string.IsNullOrWhiteSpace(memo) ? null : memo);
                var result = validator.Validate(split);
                if (!result.IsValid)
                {
                    foreach (var failure in result.Errors) errors.Add(new DataImportIssueDto(row, failure.PropertyName, "invalid_value", failure.ErrorMessage));
                    continue;
                }
                splits.Add(new NativeSplitImportDto(key, transactionKey, amount, category,
                    string.IsNullOrWhiteSpace(memo) ? null : memo, order));
            }
        }

        if (_workbook is not null && _workbook.TryGetWorksheet("Transactions", out var transactionsWorksheet))
        {
            var transactionHeaders = ReadHeaders(transactionsWorksheet);
            if (transactionHeaders.Columns.TryGetValue("Key", out var keyColumn)
                && transactionHeaders.Columns.TryGetValue("Amount", out var amountColumn)
                && transactionHeaders.Columns.TryGetValue("Category", out var categoryColumn))
            {
                var lastTransactionRow = transactionsWorksheet.LastRowUsed()?.RowNumber() ?? 1;
                for (var row = 2; row <= lastTransactionRow; row++)
                {
                    if (!Guid.TryParse(transactionsWorksheet.Cell(row, keyColumn).GetString().Trim(), out var transactionKey)) continue;
                    if (!transactionsWorksheet.Cell(row, amountColumn).TryGetValue(out decimal transactionAmount)) continue;
                    var category = transactionsWorksheet.Cell(row, categoryColumn).GetString().Trim();
                    if (!string.Equals(category, "SPLIT", StringComparison.OrdinalIgnoreCase)) continue;
                    var splitTotal = splits.Where(split => split.TransactionKey == transactionKey).Sum(split => split.Amount);
                    if (splitTotal != transactionAmount)
                    {
                        errors.Add(new DataImportIssueDto(row, "Amount", "split_sum_mismatch",
                            "Split amounts must sum to the transaction amount."));
                    }
                }
            }
        }

        return new Analysis(splits.ToImmutable(), ignored, errors.ToImmutable(), []);
    }

    private static HeaderMap ReadHeaders(IXLWorksheet worksheet)
    {
        var dict = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase); var names = new List<string>();
        foreach (var cell in worksheet.Row(1).CellsUsed()) { var name = cell.GetString().Trim(); if (name.Length > 0) { dict.TryAdd(name, cell.Address.ColumnNumber); names.Add(name); } }
        return new HeaderMap(dict, names);
    }
    private sealed record HeaderMap(Dictionary<string,int> Columns, IReadOnlyCollection<string> Names)
    { public bool Duplicate(string name) => Names.Count(value => string.Equals(value, name, StringComparison.OrdinalIgnoreCase)) > 1; }
    private sealed record Analysis(IReadOnlyCollection<NativeSplitImportDto> Splits, IReadOnlyCollection<string> IgnoredColumns,
        IReadOnlyCollection<DataImportIssueDto> Errors, IReadOnlyCollection<DataImportIssueDto> SkippedRows)
    { public ImportWorksheetPreviewDto Preview(string name, string dataType) => new(name, dataType, Splits.Count, SkippedRows.Count, IgnoredColumns, Errors, SkippedRows); }
}

internal static class SplitDictionaryExtensions
{
    public static HashSet<TValue> GetOrAdd<TKey, TValue>(this Dictionary<TKey, HashSet<TValue>> dictionary, TKey key)
        where TKey : notnull
    { if (!dictionary.TryGetValue(key, out var value)) { value = []; dictionary.Add(key, value); } return value; }
}
