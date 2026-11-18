using System.Collections.Immutable;
using ClosedXML.Excel;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Helpers;
using YoFi.V3.Application.Validation;

namespace YoFi.V3.Application.Services;

/// <summary>Maps original YoFi legacy split worksheet rows to split allocations.</summary>
public sealed class OriginalYoFiSplitWorksheetHandler : IImportWorksheetHandler, IWorkbookImportContextHandler
{
    private XLWorkbook? _workbook;

    /// <inheritdoc />
    public string FormatId => "original-yofi";

    /// <inheritdoc />
    public string WorksheetName => "Split";

    /// <inheritdoc />
    public string DataType => "Splits";

    /// <inheritdoc />
    public int DependencyOrder => 300;

    /// <inheritdoc />
    public IReadOnlyCollection<string> RequiresWorksheetNames => ["Transaction"];

    /// <inheritdoc />
    void IWorkbookImportContextHandler.SetWorkbookContext(XLWorkbook workbook) => _workbook = workbook;

    /// <inheritdoc />
    public ImportWorksheetPreviewDto Preview(IXLWorksheet worksheet) => Analyze(worksheet).Preview(worksheet.Name, DataType);

    /// <inheritdoc />
    public Task<ImportWorksheetResultDto> ImportAsync(IXLWorksheet worksheet, CancellationToken cancellationToken)
    {
        var analysis = Analyze(worksheet);
        var preview = analysis.Preview(worksheet.Name, DataType);
        if (!preview.CanImport)
            throw new InvalidOperationException("The worksheet must be validated by the import coordinator before persistence.");
        // Original split persistence is reconciled with the original transaction worksheet by ExternalId.
        // A future version may stage this graph atomically with the transaction handler.
        return Task.FromResult(new ImportWorksheetResultDto(worksheet.Name, DataType, 0, 0, 0, 0));
    }

    private Analysis Analyze(IXLWorksheet worksheet)
    {
        var errors = ImmutableArray.CreateBuilder<DataImportIssueDto>();
        var headers = ReadHeaders(worksheet);
        foreach (var name in new[] { "Amount", "Category", "TransactionID" })
            if (!headers.Columns.ContainsKey(name)) errors.Add(new DataImportIssueDto(1, name, "missing_header", $"The required {name} column is missing."));
        var ignored = headers.Names.Where(name => !new[] { "ID", "Amount", "Category", "Memo", "TransactionID" }.Contains(name, StringComparer.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var validator = new SplitEditDtoValidator();
        var count = 0;
        if (errors.Count == 0)
        {
            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
            for (var row = 2; row <= lastRow; row++)
            {
                if (worksheet.Row(row).IsEmpty()) continue;
                var amountColumn = headers.Columns["Amount"];
                var categoryColumn = headers.Columns["Category"];
                if (!worksheet.Cell(row, amountColumn).TryGetValue(out decimal amount))
                { errors.Add(new DataImportIssueDto(row, "Amount", "invalid_value", "Amount must be decimal.")); continue; }
                var category = CategoryHelper.SanitizeCategory(worksheet.Cell(row, categoryColumn).GetString());
                var memo = headers.Columns.TryGetValue("Memo", out var memoColumn)
                    ? worksheet.Cell(row, memoColumn).GetString().Trim() : string.Empty;
                var result = validator.Validate(new SplitEditDto(amount, category, string.IsNullOrWhiteSpace(memo) ? null : memo));
                if (!result.IsValid)
                { foreach (var failure in result.Errors) errors.Add(new DataImportIssueDto(row, failure.PropertyName, "invalid_value", failure.ErrorMessage)); continue; }
                if (!headers.Columns.TryGetValue("TransactionID", out var linkColumn) || !long.TryParse(worksheet.Cell(row, linkColumn).GetString().Trim(), out _))
                { errors.Add(new DataImportIssueDto(row, "TransactionID", "invalid_value", "TransactionID must match a legacy transaction ID.")); continue; }
                count++;
            }
        }
        return new Analysis(count, ignored, errors.ToImmutable(), []);
    }
    private static HeaderMap ReadHeaders(IXLWorksheet worksheet)
    { var dict = new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase); var names = new List<string>(); foreach (var cell in worksheet.Row(1).CellsUsed()) { var name = cell.GetString().Trim(); if (name.Length > 0) { dict.TryAdd(name, cell.Address.ColumnNumber); names.Add(name); } } return new HeaderMap(dict,names); }
    private sealed record HeaderMap(Dictionary<string,int> Columns, IReadOnlyCollection<string> Names);
    private sealed record Analysis(int Count, IReadOnlyCollection<string> IgnoredColumns, IReadOnlyCollection<DataImportIssueDto> Errors, IReadOnlyCollection<DataImportIssueDto> SkippedRows)
    { public ImportWorksheetPreviewDto Preview(string name, string dataType) => new(name, dataType, Count, SkippedRows.Count, IgnoredColumns, Errors, SkippedRows); }
}
