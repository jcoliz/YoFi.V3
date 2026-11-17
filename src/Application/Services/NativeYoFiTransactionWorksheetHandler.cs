using System.Collections.Immutable;
using ClosedXML.Excel;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Helpers;
using YoFi.V3.Application.Validation;

namespace YoFi.V3.Application.Services;

/// <summary>Maps native YoFi.V3 transaction worksheet rows to transaction import DTOs.</summary>
public sealed class NativeYoFiTransactionWorksheetHandler(DataImportService dataImportService, TimeProvider timeProvider) : IImportWorksheetHandler, IWorkbookImportContextHandler
{
    private XLWorkbook? _workbook;
    public string FormatId => "yofi.v3";
    public string WorksheetName => "Transactions";
    public string DataType => "Transactions";
    public int DependencyOrder => 200;
    public IReadOnlyCollection<string> RequiresWorksheetNames => [];

    void IWorkbookImportContextHandler.SetWorkbookContext(XLWorkbook workbook) => _workbook = workbook;

    public ImportWorksheetPreviewDto Preview(IXLWorksheet worksheet) => Analyze(worksheet).Preview(worksheet.Name, DataType);

    public async Task<ImportWorksheetResultDto> ImportAsync(IXLWorksheet worksheet, CancellationToken cancellationToken)
    {
        var analysis = Analyze(worksheet);
        var preview = analysis.Preview(worksheet.Name, DataType);
        if (!preview.CanImport)
            throw new InvalidOperationException("The worksheet must be validated by the import coordinator before persistence.");
        var counts = await dataImportService.UpsertNativeTransactionsAsync(analysis.Transactions, [], cancellationToken);
        return new ImportWorksheetResultDto(worksheet.Name, DataType, counts.CreatedTransactions,
            counts.SkippedTransactions, counts.UpdatedTransactions, 0);
    }

    private Analysis Analyze(IXLWorksheet worksheet)
    {
        var errors = ImmutableArray.CreateBuilder<DataImportIssueDto>();
        var headers = ReadHeaders(worksheet);
        var required = new[] { "Key", "Date", "Payee", "Amount", "Source", "ExternalId", "Memo", "Category" };
        var ignored = headers.Names.Where(name => !required.Contains(name, StringComparer.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var name in new[] { "Date", "Payee", "Amount" })
        {
            if (!headers.Columns.ContainsKey(name)) errors.Add(new DataImportIssueDto(1, name, "missing_header", $"The required {name} column is missing."));
        }
        if (headers.Duplicate("Key")) errors.Add(new DataImportIssueDto(1, "Key", "duplicate_header", "The Key column must appear at most once."));

        var seenKeys = new HashSet<Guid>();
        var transactions = ImmutableArray.CreateBuilder<NativeTransactionImportDto>();
        if (errors.Count == 0)
        {
            var validator = new TransactionEditDtoValidator(timeProvider);
            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
            for (var row = 2; row <= lastRow; row++)
            {
                if (worksheet.Row(row).IsEmpty()) continue;
                Guid? key = null;
                if (headers.Columns.TryGetValue("Key", out var keyColumn))
                {
                    var keyText = worksheet.Cell(row, keyColumn).GetString().Trim();
                    if (keyText.Length > 0)
                    {
                        if (!Guid.TryParse(keyText, out var parsed) || parsed == Guid.Empty)
                        { errors.Add(new DataImportIssueDto(row, "Key", "invalid_guid", "Key must be a valid GUID.")); continue; }
                        if (!seenKeys.Add(parsed))
                        { errors.Add(new DataImportIssueDto(row, "Key", "duplicate_key", "Duplicate nonblank Keys are not allowed.")); continue; }
                        key = parsed;
                    }
                }
                if (!TryDate(worksheet, headers, row, out var date)) { errors.Add(new DataImportIssueDto(row, "Date", "invalid_value", "Date must be a valid date.")); continue; }
                if (!TryText(worksheet, headers, row, "Payee", out var payee)) { errors.Add(new DataImportIssueDto(row, "Payee", "invalid_value", "Payee must be text.")); continue; }
                if (!TryDecimal(worksheet, headers, row, "Amount", out var amount)) { errors.Add(new DataImportIssueDto(row, "Amount", "invalid_value", "Amount must be decimal.")); continue; }

                TryText(worksheet, headers, row, "Source", out var source);
                TryText(worksheet, headers, row, "ExternalId", out var externalId);
                TryText(worksheet, headers, row, "Memo", out var memo);
                TryText(worksheet, headers, row, "Category", out var rawCategory);
                var category = string.IsNullOrWhiteSpace(rawCategory) ? null
                    : (string.Equals(rawCategory.Trim(), "SPLIT", StringComparison.OrdinalIgnoreCase) ? "SPLIT"
                        : CategoryHelper.SanitizeCategory(rawCategory));
                var dto = new TransactionEditDto(date, amount, payee.Trim(), Blank(memo), Blank(source), Blank(externalId), category);
                var validation = validator.Validate(dto);
                if (!validation.IsValid)
                {
                    foreach (var failure in validation.Errors)
                        errors.Add(new DataImportIssueDto(row, failure.PropertyName, "invalid_value", failure.ErrorMessage));
                    continue;
                }
                transactions.Add(new NativeTransactionImportDto(key, date, amount, payee.Trim(), Blank(memo), Blank(source), Blank(externalId), category));
            }
        }
        return new Analysis(transactions.ToImmutable(), ignored, errors.ToImmutable(), []);
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool TryText(IXLWorksheet worksheet, HeaderMap headers, int row, string name, out string value)
    { value = string.Empty; return headers.Columns.TryGetValue(name, out var col) && !worksheet.Cell(row, col).HasFormula && (value = worksheet.Cell(row, col).GetString()).Length >= 0; }
    private static bool TryDecimal(IXLWorksheet worksheet, HeaderMap headers, int row, string name, out decimal value)
    { value = 0; return headers.Columns.TryGetValue(name, out var col) && worksheet.Cell(row, col).TryGetValue(out value); }
    private static bool TryDate(IXLWorksheet worksheet, HeaderMap headers, int row, out DateOnly value)
    {
        value = default;
        if (!headers.Columns.TryGetValue("Date", out var col)) return false;
        var cell = worksheet.Cell(row, col);
        if (cell.HasFormula) return false;
        if (cell.DataType == XLDataType.DateTime && cell.TryGetValue(out DateTime dateTime)) { value = DateOnly.FromDateTime(dateTime); return true; }
        return DateOnly.TryParse(cell.GetString().Trim(), out value);
    }
    private static HeaderMap ReadHeaders(IXLWorksheet worksheet)
    {
        var dict = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase); var names = new List<string>();
        foreach (var cell in worksheet.Row(1).CellsUsed()) { var name = cell.GetString().Trim(); if (name.Length > 0) { dict.TryAdd(name, cell.Address.ColumnNumber); names.Add(name); } }
        return new HeaderMap(dict, names);
    }
    private sealed record HeaderMap(Dictionary<string,int> Columns, IReadOnlyCollection<string> Names)
    { public bool Duplicate(string name) => Names.Count(value => string.Equals(value, name, StringComparison.OrdinalIgnoreCase)) > 1; }
    private sealed record Analysis(IReadOnlyCollection<NativeTransactionImportDto> Transactions, IReadOnlyCollection<string> IgnoredColumns,
        IReadOnlyCollection<DataImportIssueDto> Errors, IReadOnlyCollection<DataImportIssueDto> SkippedRows)
    { public ImportWorksheetPreviewDto Preview(string name, string dataType) => new(name, dataType, Transactions.Count, SkippedRows.Count, IgnoredColumns, Errors, SkippedRows); }
}
