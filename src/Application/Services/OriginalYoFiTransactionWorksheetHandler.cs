using System.Collections.Immutable;
using ClosedXML.Excel;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Helpers;
using YoFi.V3.Application.Validation;

namespace YoFi.V3.Application.Services;

/// <summary>Maps original YoFi legacy transaction worksheet rows to append-only transaction imports.</summary>
/// <param name="dataImportService">Original transaction persistence service.</param>
/// <param name="timeProvider">Provider used to validate transaction dates.</param>
public sealed class OriginalYoFiTransactionWorksheetHandler(DataImportService dataImportService, TimeProvider timeProvider) : IImportWorksheetHandler, IWorkbookImportContextHandler
{
    private XLWorkbook? _workbook;

    /// <inheritdoc />
    public string FormatId => "original-yofi";

    /// <inheritdoc />
    public string WorksheetName => "Transaction";

    /// <inheritdoc />
    public string DataType => "Transactions";

    /// <inheritdoc />
    public int DependencyOrder => 200;

    /// <inheritdoc />
    public IReadOnlyCollection<string> RequiresWorksheetNames => [];

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
        var created = await dataImportService.CreateOriginalTransactionsAsync(analysis.Transactions, cancellationToken);
        return new ImportWorksheetResultDto(worksheet.Name, DataType, created, analysis.SkippedRows.Count, 0, 0);
    }

    private Analysis Analyze(IXLWorksheet worksheet)
    {
        var errors = ImmutableArray.CreateBuilder<DataImportIssueDto>();
        var skipped = ImmutableArray.CreateBuilder<DataImportIssueDto>();
        var headers = ReadHeaders(worksheet);
        var required = new[] { "Timestamp", "Payee", "Amount" };
        var ignored = headers.Names.Where(name => !required.Concat(new[] { "ID", "Category", "Memo", "BankReference", "ReceiptUrl", "Hidden", "Imported", "Selected" }).Contains(name, StringComparer.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var name in required)
            if (!headers.Columns.ContainsKey(name)) errors.Add(new DataImportIssueDto(1, name, "missing_header", $"The required {name} column is missing."));

        var transactions = ImmutableArray.CreateBuilder<OriginalTransactionImportDto>();
        if (errors.Count == 0)
        {
            var validator = new TransactionEditDtoValidator(timeProvider);
            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
            for (var row = 2; row <= lastRow; row++)
            {
                if (worksheet.Row(row).IsEmpty()) continue;
                if (!TryDate(worksheet, headers, row, out var date) || !TryDecimal(worksheet, headers, row, out var amount) ||
                    !TryText(worksheet, headers, row, "Payee", out var payee))
                {
                    errors.Add(new DataImportIssueDto(row, "Timestamp/Payee/Amount", "invalid_value", "Timestamp, Payee, and Amount are required valid values."));
                    continue;
                }
                TryText(worksheet, headers, row, "Memo", out var memo);
                TryText(worksheet, headers, row, "BankReference", out var bankReference);
                TryText(worksheet, headers, row, "Category", out var rawCategory);
                bankReference = string.IsNullOrWhiteSpace(bankReference)
                    ? DataImportService.GenerateLegacyExternalId(payee, amount, date)
                    : bankReference.Trim();
                var category = CategoryHelper.SanitizeCategory(rawCategory);
                var dto = new TransactionEditDto(date, amount, payee.Trim(), Blank(memo), null, bankReference,
                    string.IsNullOrEmpty(category) ? null : category);
                var validation = validator.Validate(dto);
                if (!validation.IsValid)
                {
                    foreach (var failure in validation.Errors) errors.Add(new DataImportIssueDto(row, failure.PropertyName, "invalid_value", failure.ErrorMessage));
                    continue;
                }
                var splitRows = new List<OriginalSplitImportDto>();
                if (_workbook is not null
                    && _workbook.TryGetWorksheet("Split", out var splitWorksheet)
                    && headers.Columns.TryGetValue("ID", out var legacyIdColumn)
                    && long.TryParse(worksheet.Cell(row, legacyIdColumn).GetString().Trim(), out var legacyId))
                {
                    var splitHeaders = ReadHeaders(splitWorksheet);
                    if (splitHeaders.Columns.TryGetValue("TransactionID", out var splitLinkColumn)
                        && splitHeaders.Columns.TryGetValue("Amount", out var splitAmountColumn)
                        && splitHeaders.Columns.TryGetValue("Category", out var splitCategoryColumn))
                    {
                        var lastSplitRow = splitWorksheet.LastRowUsed()?.RowNumber() ?? 1;
                        for (var splitRow = 2; splitRow <= lastSplitRow; splitRow++)
                        {
                            if (!long.TryParse(splitWorksheet.Cell(splitRow, splitLinkColumn).GetString().Trim(), out var linkedId)
                                || linkedId != legacyId) continue;
                            if (!splitWorksheet.Cell(splitRow, splitAmountColumn).TryGetValue(out decimal splitAmount)) continue;
                            var splitCategory = CategoryHelper.SanitizeCategory(splitWorksheet.Cell(splitRow, splitCategoryColumn).GetString());
                            var splitMemo = splitHeaders.Columns.TryGetValue("Memo", out var splitMemoColumn)
                                ? splitWorksheet.Cell(splitRow, splitMemoColumn).GetString().Trim() : string.Empty;
                            splitRows.Add(new OriginalSplitImportDto(splitAmount, splitCategory,
                                string.IsNullOrWhiteSpace(splitMemo) ? null : splitMemo));
                        }
                    }
                }
                if (splitRows.Count == 0)
                    splitRows.Add(new OriginalSplitImportDto(amount, category, null));
                if (splitRows.Sum(splitRow => splitRow.Amount) != amount)
                {
                    errors.Add(new DataImportIssueDto(row, "Split", "split_sum_mismatch",
                        "Split amounts must sum to the transaction amount."));
                    continue;
                }
                transactions.Add(new OriginalTransactionImportDto(date, amount, payee.Trim(), Blank(memo), bankReference, splitRows));
            }
        }
        return new Analysis(transactions.ToImmutable(), ignored, errors.ToImmutable(), skipped.ToImmutable());
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool TryText(IXLWorksheet worksheet, HeaderMap headers, int row, string name, out string value)
    { value = string.Empty; return headers.Columns.TryGetValue(name, out var col) && !worksheet.Cell(row, col).HasFormula && (value = worksheet.Cell(row, col).GetString()).Length >= 0; }
    private static bool TryDecimal(IXLWorksheet worksheet, HeaderMap headers, int row, out decimal value)
    { value = 0; return headers.Columns.TryGetValue("Amount", out var col) && worksheet.Cell(row, col).TryGetValue(out value); }
    private static bool TryDate(IXLWorksheet worksheet, HeaderMap headers, int row, out DateOnly value)
    {
        value = default;
        if (!headers.Columns.TryGetValue("Timestamp", out var col)) return false;
        var cell = worksheet.Cell(row, col);
        if (cell.DataType == XLDataType.DateTime && cell.TryGetValue(out DateTime dateTime)) { value = DateOnly.FromDateTime(dateTime); return true; }
        var text = cell.GetString().Trim();
        if (DateTime.TryParse(text, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AllowWhiteSpaces, out var dateTimeValue))
        { value = DateOnly.FromDateTime(dateTimeValue); return true; }
        return DateOnly.TryParse(text, out value);
    }
    private static HeaderMap ReadHeaders(IXLWorksheet worksheet)
    { var dict = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase); var names = new List<string>(); foreach (var cell in worksheet.Row(1).CellsUsed()) { var name = cell.GetString().Trim(); if (name.Length > 0) { dict.TryAdd(name, cell.Address.ColumnNumber); names.Add(name); } } return new HeaderMap(dict,names); }
    private sealed record HeaderMap(Dictionary<string,int> Columns, IReadOnlyCollection<string> Names);
    private sealed record Analysis(IReadOnlyCollection<OriginalTransactionImportDto> Transactions, IReadOnlyCollection<string> IgnoredColumns,
        IReadOnlyCollection<DataImportIssueDto> Errors, IReadOnlyCollection<DataImportIssueDto> SkippedRows)
    { public ImportWorksheetPreviewDto Preview(string name, string dataType) => new(name, dataType, Transactions.Count, SkippedRows.Count, IgnoredColumns, Errors, SkippedRows); }
}
