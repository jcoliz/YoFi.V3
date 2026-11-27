using System.Collections.Immutable;
using ClosedXML.Excel;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Helpers;
using YoFi.V3.Application.Validation;

namespace YoFi.V3.Application.Services;

/// <summary>Maps native YoFi.V3 split worksheet rows to native split import DTOs.</summary>
/// <param name="dataImportService">Native split persistence service.</param>
public sealed class NativeYoFiSplitWorksheetHandler(DataImportService dataImportService) : IImportWorksheetHandler, IWorkbookImportContextHandler
{
    private XLWorkbook? _workbook;

    /// <inheritdoc />
    public string FormatId => "yofi.v3";

    /// <inheritdoc />
    public string WorksheetName => "Splits";

    /// <inheritdoc />
    public string DataType => "Splits";

    /// <inheritdoc />
    public int DependencyOrder => 300;

    /// <inheritdoc />
    public IReadOnlyCollection<string> RequiresWorksheetNames => ["Transactions"];

    /// <inheritdoc />
    void IWorkbookImportContextHandler.SetWorkbookContext(XLWorkbook workbook) => _workbook = workbook;

    /// <inheritdoc />
    public ImportWorksheetPreviewDto Preview(IXLWorksheet worksheet) => Analyze(worksheet).Preview(worksheet.Name, DataType);

    /// <inheritdoc />
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
        var ignored = headers.Names
            .Where(name => !required.Contains(name, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        ValidateHeaders(headers, errors);
        var splits = ParseSplits(worksheet, headers, errors);
        ValidateSplitTotals(splits, errors);
        ValidateTransactionReferences(splits, errors);

        return new Analysis(splits, ignored, errors.ToImmutable(), []);
    }

    private static void ValidateHeaders(HeaderMap headers, ImmutableArray<DataImportIssueDto>.Builder errors)
    {
        foreach (var name in new[] { "TransactionKey", "Amount", "Category", "Memo", "Order" })
        {
            if (!headers.Columns.ContainsKey(name))
                errors.Add(new DataImportIssueDto(1, name, "missing_header", $"The required {name} column is missing."));
        }

        if (headers.Duplicate("Key") || headers.Duplicate("TransactionKey"))
            errors.Add(new DataImportIssueDto(1, "Key", "duplicate_header", "Key and TransactionKey columns must appear at most once."));
    }

    private static IReadOnlyCollection<NativeSplitImportDto> ParseSplits(
        IXLWorksheet worksheet,
        HeaderMap headers,
        ImmutableArray<DataImportIssueDto>.Builder errors)
    {
        if (errors.Count != 0)
            return [];

        var validator = new SplitEditDtoValidator();
        var seenKeys = new HashSet<Guid>();
        var seenOrders = new Dictionary<Guid, HashSet<int>>();
        var splits = ImmutableArray.CreateBuilder<NativeSplitImportDto>();
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;

        for (var row = 2; row <= lastRow; row++)
        {
            if (worksheet.Row(row).IsEmpty())
                continue;

            var split = ParseRow(worksheet, headers, row, seenKeys, seenOrders, validator, errors);
            if (split is not null)
                splits.Add(split);
        }

        return splits.ToImmutable();
    }

    private static NativeSplitImportDto? ParseRow(
        IXLWorksheet worksheet,
        HeaderMap headers,
        int row,
        HashSet<Guid> seenKeys,
        Dictionary<Guid, HashSet<int>> seenOrders,
        SplitEditDtoValidator validator,
        ImmutableArray<DataImportIssueDto>.Builder errors)
    {
        if (!TryReadKey(worksheet, headers, row, seenKeys, out var key, errors))
            return null;
        if (!TryReadTransactionKey(worksheet, headers, row, out var transactionKey, errors))
            return null;
        if (!TryReadAmount(worksheet, headers, row, out var amount, errors))
            return null;
        if (!TryReadOrder(worksheet, headers, row, out var order, errors))
            return null;

        var orders = seenOrders.GetOrAdd(transactionKey);
        if (!orders.Add(order))
        {
            errors.Add(new DataImportIssueDto(row, "Order", "duplicate_order", "Order must be unique within a transaction."));
            return null;
        }

        var category = ReadCategory(worksheet, headers, row);
        var memo = ReadMemo(worksheet, headers, row);
        var split = new SplitEditDto(amount, category, string.IsNullOrWhiteSpace(memo) ? null : memo);
        var result = validator.Validate(split);
        if (!result.IsValid)
        {
            foreach (var failure in result.Errors)
                errors.Add(new DataImportIssueDto(row, failure.PropertyName, "invalid_value", failure.ErrorMessage));
            return null;
        }

        return new NativeSplitImportDto(key, transactionKey, amount, category,
            string.IsNullOrWhiteSpace(memo) ? null : memo, order);
    }

    private static bool TryReadKey(
        IXLWorksheet worksheet,
        HeaderMap headers,
        int row,
        HashSet<Guid> seenKeys,
        out Guid? key,
        ImmutableArray<DataImportIssueDto>.Builder errors)
    {
        key = null;
        if (!headers.Columns.TryGetValue("Key", out var keyColumn))
            return true;

        var text = worksheet.Cell(row, keyColumn).GetString().Trim();
        if (text.Length == 0)
            return true;
        if (!Guid.TryParse(text, out var parsed) || parsed == Guid.Empty || !seenKeys.Add(parsed))
        {
            errors.Add(new DataImportIssueDto(row, "Key", "invalid_or_duplicate_key", "Key must be a unique valid GUID."));
            return false;
        }
        key = parsed;
        return true;
    }

    private static bool TryReadTransactionKey(IXLWorksheet worksheet, HeaderMap headers, int row,
        out Guid transactionKey, ImmutableArray<DataImportIssueDto>.Builder errors)
    {
        transactionKey = Guid.Empty;
        if (!headers.Columns.TryGetValue("TransactionKey", out var column)
            || !Guid.TryParse(worksheet.Cell(row, column).GetString().Trim(), out transactionKey))
        {
            errors.Add(new DataImportIssueDto(row, "TransactionKey", "invalid_guid", "TransactionKey must be a valid GUID."));
            return false;
        }
        return true;
    }

    private static bool TryReadAmount(IXLWorksheet worksheet, HeaderMap headers, int row,
        out decimal amount, ImmutableArray<DataImportIssueDto>.Builder errors)
    {
        amount = 0;
        if (!headers.Columns.TryGetValue("Amount", out var column)
            || !worksheet.Cell(row, column).TryGetValue(out amount))
        {
            errors.Add(new DataImportIssueDto(row, "Amount", "invalid_value", "Amount must be decimal."));
            return false;
        }
        return true;
    }

    private static bool TryReadOrder(IXLWorksheet worksheet, HeaderMap headers, int row,
        out int order, ImmutableArray<DataImportIssueDto>.Builder errors)
    {
        order = 0;
        if (!headers.Columns.TryGetValue("Order", out var column)
            || !int.TryParse(worksheet.Cell(row, column).GetString().Trim(), out order)
            || order < 0)
        {
            errors.Add(new DataImportIssueDto(row, "Order", "invalid_value", "Order must be a nonnegative integer."));
            return false;
        }
        return true;
    }

    private static string ReadCategory(IXLWorksheet worksheet, HeaderMap headers, int row) =>
        CategoryHelper.SanitizeCategory(headers.Columns.TryGetValue("Category", out var column)
            ? worksheet.Cell(row, column).GetString()
            : string.Empty);

    private static string ReadMemo(IXLWorksheet worksheet, HeaderMap headers, int row) =>
        headers.Columns.TryGetValue("Memo", out var column)
            ? worksheet.Cell(row, column).GetString().Trim()
            : string.Empty;

    private void ValidateSplitTotals(IReadOnlyCollection<NativeSplitImportDto> splits,
        ImmutableArray<DataImportIssueDto>.Builder errors)
    {
        if (_workbook is null || !_workbook.TryGetWorksheet("Transactions", out var transactionsWorksheet))
            return;

        var headers = ReadHeaders(transactionsWorksheet);
        if (!headers.Columns.TryGetValue("Key", out var keyColumn)
            || !headers.Columns.TryGetValue("Amount", out var amountColumn)
            || !headers.Columns.TryGetValue("Category", out var categoryColumn))
            return;

        var lastRow = transactionsWorksheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var row = 2; row <= lastRow; row++)
        {
            if (!Guid.TryParse(transactionsWorksheet.Cell(row, keyColumn).GetString().Trim(), out var transactionKey)
                || !transactionsWorksheet.Cell(row, amountColumn).TryGetValue(out decimal transactionAmount))
                continue;

            var category = transactionsWorksheet.Cell(row, categoryColumn).GetString().Trim();
            if (!string.Equals(category, "SPLIT", StringComparison.OrdinalIgnoreCase))
                continue;

            var splitTotal = splits.Where(split => split.TransactionKey == transactionKey).Sum(split => split.Amount);
            if (splitTotal != transactionAmount)
                errors.Add(new DataImportIssueDto(row, "Amount", "split_sum_mismatch",
                    "Split amounts must sum to the transaction amount."));
        }
    }

    private void ValidateTransactionReferences(IReadOnlyCollection<NativeSplitImportDto> splits,
        ImmutableArray<DataImportIssueDto>.Builder errors)
    {
        if (_workbook is null || !_workbook.TryGetWorksheet("Transactions", out var transactionsWorksheet))
            return;

        var headers = ReadHeaders(transactionsWorksheet);
        var validTransactionKeys = GetValidTransactionKeys(transactionsWorksheet, headers);
        foreach (var split in splits)
        {
            if (!validTransactionKeys.Contains(split.TransactionKey))
                errors.Add(new DataImportIssueDto(2, "TransactionKey", "transaction_key_mismatch",
                    "TransactionKey must reference a row in Transactions."));

            if (IsReferencedTransactionNonSplit(transactionsWorksheet, headers, split.TransactionKey))
                errors.Add(new DataImportIssueDto(2, "TransactionKey", "non_split_transaction",
                    "Split rows may only reference transactions marked SPLIT."));
        }
    }

    private static HashSet<Guid> GetValidTransactionKeys(IXLWorksheet worksheet, HeaderMap headers)
    {
        var keys = new HashSet<Guid>();
        if (!headers.Columns.TryGetValue("Key", out var keyColumn))
            return keys;

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var row = 2; row <= lastRow; row++)
        {
            if (Guid.TryParse(worksheet.Cell(row, keyColumn).GetString().Trim(), out var key))
                keys.Add(key);
        }
        return keys;
    }

    private static bool IsReferencedTransactionNonSplit(IXLWorksheet worksheet, HeaderMap headers, Guid transactionKey)
    {
        if (!headers.Columns.TryGetValue("Key", out var keyColumn)
            || !headers.Columns.TryGetValue("Category", out var categoryColumn))
            return false;

        foreach (var row in worksheet.RowsUsed().Skip(1))
        {
            if (Guid.TryParse(row.Cell(keyColumn).GetString().Trim(), out var key)
                && key == transactionKey)
                return !string.Equals(row.Cell(categoryColumn).GetString().Trim(), "SPLIT", StringComparison.OrdinalIgnoreCase);
        }
        return false;
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
