using System.Collections.Immutable;
using ClosedXML.Excel;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Helpers;
using YoFi.V3.Application.Validation;

namespace YoFi.V3.Application.Services;

/// <summary>Maps original YoFi legacy transaction worksheet rows to append-only transaction imports.</summary>
public sealed class OriginalYoFiTransactionWorksheetHandler(DataImportService dataImportService, TimeProvider timeProvider) : IImportWorksheetHandler
{
    public string FormatId => "original-yofi";
    public string WorksheetName => "Transaction";
    public string DataType => "Transactions";
    public int DependencyOrder => 200;
    public IReadOnlyCollection<string> RequiresWorksheetNames => [];

    public ImportWorksheetPreviewDto Preview(IXLWorksheet worksheet) => Analyze(worksheet).Preview(worksheet.Name, DataType);

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
                var split = new OriginalSplitImportDto(amount, category, null);
                transactions.Add(new OriginalTransactionImportDto(date, amount, payee.Trim(), Blank(memo), bankReference, [split]));
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
        return DateOnly.TryParse(cell.GetString().Trim(), out value);
    }
    private static HeaderMap ReadHeaders(IXLWorksheet worksheet)
    { var dict = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase); var names = new List<string>(); foreach (var cell in worksheet.Row(1).CellsUsed()) { var name = cell.GetString().Trim(); if (name.Length > 0) { dict.TryAdd(name, cell.Address.ColumnNumber); names.Add(name); } } return new HeaderMap(dict,names); }
    private sealed record HeaderMap(Dictionary<string,int> Columns, IReadOnlyCollection<string> Names);
    private sealed record Analysis(IReadOnlyCollection<OriginalTransactionImportDto> Transactions, IReadOnlyCollection<string> IgnoredColumns,
        IReadOnlyCollection<DataImportIssueDto> Errors, IReadOnlyCollection<DataImportIssueDto> SkippedRows)
    { public ImportWorksheetPreviewDto Preview(string name, string dataType) => new(name, dataType, Transactions.Count, SkippedRows.Count, IgnoredColumns, Errors, SkippedRows); }
}
