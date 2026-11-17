using System.Collections.Immutable;
using ClosedXML.Excel;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Helpers;
using YoFi.V3.Application.Validation;

namespace YoFi.V3.Application.Services;

/// <summary>Maps and upserts native YoFi.V3 payee matching-rule worksheet rows.</summary>
public sealed class NativeYoFiPayeeWorksheetHandler(
    DataImportService dataImportService,
    IRegexValidationService regexValidationService) : IImportWorksheetHandler
{
    /// <inheritdoc />
    public string FormatId => "yofi.v3";
    /// <inheritdoc />
    public string WorksheetName => "PayeeMatchingRules";
    /// <inheritdoc />
    public string DataType => "PayeeMatchingRules";
    /// <inheritdoc />
    public int DependencyOrder => 100;
    /// <inheritdoc />
    public IReadOnlyCollection<string> RequiresWorksheetNames => [];

    /// <inheritdoc />
    public ImportWorksheetPreviewDto Preview(IXLWorksheet worksheet) => Analyze(worksheet).Preview(worksheet.Name, DataType);

    /// <inheritdoc />
    public async Task<ImportWorksheetResultDto> ImportAsync(IXLWorksheet worksheet, CancellationToken cancellationToken)
    {
        var analysis = Analyze(worksheet);
        var preview = analysis.Preview(worksheet.Name, DataType);
        if (!preview.CanImport)
            throw new InvalidOperationException("The worksheet must be validated by the import coordinator before persistence.");
        var counts = await dataImportService.UpsertNativeRulesAsync(analysis.Rules, cancellationToken);
        return new ImportWorksheetResultDto(worksheet.Name, DataType, counts.Created, analysis.SkippedRows.Count,
            counts.Updated, 0);
    }

    private Analysis Analyze(IXLWorksheet worksheet)
    {
        var errors = ImmutableArray.CreateBuilder<DataImportIssueDto>();
        var headers = ReadHeaders(worksheet);
        var required = new[] { "Key", "PayeePattern", "PayeeIsRegex", "Category" };
        var ignored = headers.Cells
            .Where(name => !required.Contains(name, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var name in new[] { "PayeePattern", "PayeeIsRegex", "Category" })
        {
            if (headers.Duplicate(name) || !headers.Columns.TryGetValue(name, out _))
                errors.Add(new DataImportIssueDto(1, name, "missing_or_duplicate_header", $"The {name} column must appear exactly once."));
        }
        if (headers.Duplicate("Key")) errors.Add(new DataImportIssueDto(1, "Key", "duplicate_header", "The Key column must appear at most once."));

        var rules = ImmutableArray.CreateBuilder<NativePayeeMatchingRuleImportDto>();
        var seenKeys = new HashSet<Guid>();
        if (errors.Count == 0)
        {
            var validator = new PayeeMatchingRuleEditDtoValidator(regexValidationService);
            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
            for (var row = 2; row <= lastRow; row++)
            {
                if (worksheet.Row(row).IsEmpty()) continue;
                Guid? key = null;
                if (headers.Columns.TryGetValue("Key", out var keyColumn))
                {
                    var keyCell = worksheet.Cell(row, keyColumn);
                    if (keyCell.HasFormula) { errors.Add(new DataImportIssueDto(row, "Key", "formula_not_allowed", "Formula cells are not supported for Key.")); continue; }
                    var keyText = keyCell.GetString().Trim();
                    if (keyText.Length > 0)
                    {
                        if (!Guid.TryParse(keyText, out var parsed) || parsed == Guid.Empty) { errors.Add(new DataImportIssueDto(row, "Key", "invalid_guid", "Key must be a valid GUID.")); continue; }
                        if (!seenKeys.Add(parsed)) { errors.Add(new DataImportIssueDto(row, "Key", "duplicate_key", "Duplicate nonblank Keys are not allowed.")); continue; }
                        key = parsed;
                    }
                }

                if (!headers.TryGetText(worksheet, row, "PayeePattern", out var pattern) ||
                    !headers.TryGetText(worksheet, row, "Category", out var category) ||
                    !headers.TryGetBool(worksheet, row, "PayeeIsRegex", out var isRegex))
                {
                    if (!headers.TryGetText(worksheet, row, "PayeePattern", out _)) errors.Add(new DataImportIssueDto(row, "PayeePattern", "invalid_value", "PayeePattern must be text."));
                    if (!headers.TryGetText(worksheet, row, "Category", out _)) errors.Add(new DataImportIssueDto(row, "Category", "invalid_value", "Category must be text."));
                    if (!headers.TryGetBool(worksheet, row, "PayeeIsRegex", out _)) errors.Add(new DataImportIssueDto(row, "PayeeIsRegex", "invalid_value", "PayeeIsRegex must be a boolean."));
                    continue;
                }
                pattern = pattern.Trim();
                var sanitized = CategoryHelper.SanitizeCategory(category);
                var dto = new PayeeMatchingRuleEditDto(pattern, isRegex, sanitized);
                var validation = validator.Validate(dto);
                if (!validation.IsValid)
                {
                    foreach (var failure in validation.Errors)
                        errors.Add(new DataImportIssueDto(row, failure.PropertyName, "invalid_value", failure.ErrorMessage));
                    continue;
                }
                rules.Add(new NativePayeeMatchingRuleImportDto(key, pattern, isRegex, sanitized));
            }
        }
        return new Analysis(rules.ToImmutable(), ignored, errors.ToImmutable(), []);
    }

    private static HeaderMap ReadHeaders(IXLWorksheet worksheet)
    {
        var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var names = new List<string>();
        foreach (var cell in worksheet.Row(1).CellsUsed())
        {
            var name = cell.GetString().Trim();
            if (name.Length == 0) continue;
            names.Add(name);
            dict.TryAdd(name, cell.Address.ColumnNumber);
        }
        return new HeaderMap(dict, names);
    }

    private sealed record Analysis(IReadOnlyCollection<NativePayeeMatchingRuleImportDto> Rules,
        IReadOnlyCollection<string> IgnoredColumns, IReadOnlyCollection<DataImportIssueDto> Errors,
        IReadOnlyCollection<DataImportIssueDto> SkippedRows)
    {
        public ImportWorksheetPreviewDto Preview(string name, string dataType) => new(name, dataType, Rules.Count,
            SkippedRows.Count, IgnoredColumns, Errors, SkippedRows);
    }

    private sealed record HeaderMap(Dictionary<string, int> Columns, IReadOnlyCollection<string> Cells)
    {
        public bool Duplicate(string name) => Cells.Count(value => string.Equals(value, name, StringComparison.OrdinalIgnoreCase)) > 1;
        public bool TryGetText(IXLWorksheet worksheet, int row, string name, out string value)
        {
            value = string.Empty;
            if (!Columns.TryGetValue(name, out var column)) return false;
            var cell = worksheet.Cell(row, column);
            if (cell.HasFormula || cell.DataType != XLDataType.Text) return false;
            value = cell.GetString(); return true;
        }
        public bool TryGetBool(IXLWorksheet worksheet, int row, string name, out bool value)
        {
            value = false;
            if (!Columns.TryGetValue(name, out var column)) return false;
            var cell = worksheet.Cell(row, column);
            if (cell.HasFormula) return false;
            if (cell.DataType == XLDataType.Boolean) { value = cell.GetBoolean(); return true; }
            return bool.TryParse(cell.GetString().Trim(), out value);
        }
    }
}
