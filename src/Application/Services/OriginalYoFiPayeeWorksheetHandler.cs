using System.Collections.Immutable;
using ClosedXML.Excel;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Helpers;
using YoFi.V3.Application.Validation;

namespace YoFi.V3.Application.Services;

/// <summary>Maps original YoFi Payee worksheet rows to V3 payee matching rules.</summary>
/// <param name="dataImportService">Shared typed create-only persistence path.</param>
/// <param name="regexValidationService">Existing safe-regex and payee-rule validator dependency.</param>
public sealed class OriginalYoFiPayeeWorksheetHandler(
    DataImportService dataImportService,
    IRegexValidationService regexValidationService) : IImportWorksheetHandler
{
    /// <inheritdoc />
    public string FormatId => "original-yofi";

    /// <inheritdoc />
    public string WorksheetName => "Payee";

    /// <inheritdoc />
    public string DataType => "PayeeMatchingRules";

    /// <inheritdoc />
    public ImportWorksheetPreviewDto Preview(IXLWorksheet worksheet)
    {
        var analysis = Analyze(worksheet);
        return new ImportWorksheetPreviewDto(
            worksheet.Name,
            DataType,
            analysis.Rules.Length,
            analysis.SkippedRows.Length,
            analysis.IgnoredColumns,
            analysis.Errors,
            analysis.SkippedRows);
    }

    /// <inheritdoc />
    public async Task<ImportWorksheetResultDto> ImportAsync(
        IXLWorksheet worksheet,
        CancellationToken cancellationToken)
    {
        var analysis = Analyze(worksheet);
        var preview = new ImportWorksheetPreviewDto(
            worksheet.Name,
            DataType,
            analysis.Rules.Length,
            analysis.SkippedRows.Length,
            analysis.IgnoredColumns,
            analysis.Errors,
            analysis.SkippedRows);
        if (!preview.CanImport)
        {
            throw new InvalidOperationException("The worksheet must be validated by the import coordinator before persistence.");
        }

        var created = await dataImportService.CreateRulesAsync(analysis.Rules, cancellationToken);
        return new ImportWorksheetResultDto(worksheet.Name, DataType, created, analysis.SkippedRows.Length, 0, 0);
    }

    private Analysis Analyze(IXLWorksheet worksheet)
    {
        var errors = ImmutableArray.CreateBuilder<DataImportIssueDto>();
        var skippedRows = ImmutableArray.CreateBuilder<DataImportIssueDto>();
        var headerColumns = ReadHeaders(worksheet);
        var ignoredColumns = headerColumns.Names
            .Where(name => !IsSupportedHeader(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (!headerColumns.HasName)
        {
            errors.Add(new DataImportIssueDto(1, "Name", "missing_header", "The required Name column is missing."));
        }
        if (!headerColumns.HasCategory)
        {
            errors.Add(new DataImportIssueDto(1, "Category", "missing_header", "The required Category column is missing."));
        }
        if (headerColumns.DuplicateName)
        {
            errors.Add(new DataImportIssueDto(1, "Name", "duplicate_header", "The Name column must appear exactly once."));
        }
        if (headerColumns.DuplicateCategory)
        {
            errors.Add(new DataImportIssueDto(1, "Category", "duplicate_header", "The Category column must appear exactly once."));
        }

        var rules = ImmutableArray.CreateBuilder<PayeeMatchingRuleEditDto>();
        if (headerColumns.NameColumn is not null
            && headerColumns.CategoryColumn is not null
            && !headerColumns.DuplicateName
            && !headerColumns.DuplicateCategory
            && errors.Count == 0)
        {
            var validator = new PayeeMatchingRuleEditDtoValidator(regexValidationService);
            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
            for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
            {
                var row = worksheet.Row(rowNumber);
                if (row.IsEmpty())
                {
                    continue;
                }

                var nameCell = row.Cell(headerColumns.NameColumn.Value);
                var categoryCell = row.Cell(headerColumns.CategoryColumn.Value);
                if (nameCell.HasFormula)
                {
                    errors.Add(new DataImportIssueDto(rowNumber, "Name", "formula_not_allowed",
                        "Formula cells are not supported for Name."));
                    continue;
                }
                if (categoryCell.HasFormula)
                {
                    errors.Add(new DataImportIssueDto(rowNumber, "Category", "formula_not_allowed",
                        "Formula cells are not supported for Category."));
                    continue;
                }

                if (!TryReadText(nameCell, out var name))
                {
                    errors.Add(new DataImportIssueDto(rowNumber, "Name", "invalid_cell_type",
                        "Name must be a text cell."));
                    continue;
                }
                if (!TryReadText(categoryCell, out var category))
                {
                    errors.Add(new DataImportIssueDto(rowNumber, "Category", "invalid_cell_type",
                        "Category must be a text cell."));
                    continue;
                }
                if (string.IsNullOrWhiteSpace(category))
                {
                    skippedRows.Add(new DataImportIssueDto(rowNumber, "Category", "blank_category",
                        "The row was skipped because Category is blank."));
                    continue;
                }
                if (string.IsNullOrWhiteSpace(name))
                {
                    errors.Add(new DataImportIssueDto(rowNumber, "Name", "required", "Name is required."));
                    continue;
                }

                var trimmedName = name.Trim();
                var isRegex = trimmedName.Length >= 2 && trimmedName[0] == '/' && trimmedName[^1] == '/';
                var pattern = isRegex ? trimmedName[1..^1] : trimmedName;
                var dto = new PayeeMatchingRuleEditDto(pattern, isRegex, CategoryHelper.SanitizeCategory(category));
                var validation = validator.Validate(dto);
                if (!validation.IsValid)
                {
                    foreach (var failure in validation.Errors)
                    {
                        var field = failure.PropertyName == nameof(PayeeMatchingRuleEditDto.PayeePattern)
                            ? "Name"
                            : "Category";
                        errors.Add(new DataImportIssueDto(rowNumber, field, "invalid_value", failure.ErrorMessage));
                    }
                    continue;
                }

                rules.Add(dto);
            }
        }

        return new Analysis(rules.ToImmutable(), ignoredColumns, errors.ToImmutable(), skippedRows.ToImmutable());
    }

    private static HeaderColumns ReadHeaders(IXLWorksheet worksheet)
    {
        var headers = worksheet.Row(1).CellsUsed()
            .Select(cell => (Name: cell.HasFormula ? string.Empty : cell.GetString().Trim(), Column: cell.Address.ColumnNumber))
            .ToArray();
        var nameColumns = headers.Where(header => string.Equals(header.Name, "Name", StringComparison.OrdinalIgnoreCase))
            .Select(header => header.Column).ToArray();
        var categoryColumns = headers.Where(header => string.Equals(header.Name, "Category", StringComparison.OrdinalIgnoreCase))
            .Select(header => header.Column).ToArray();
        return new HeaderColumns(
            headers.Select(header => header.Name).Where(name => name.Length > 0).ToImmutableArray(),
            nameColumns.Length == 1 ? nameColumns[0] : null,
            categoryColumns.Length == 1 ? categoryColumns[0] : null,
            nameColumns.Length > 0,
            categoryColumns.Length > 0,
            nameColumns.Length > 1,
            categoryColumns.Length > 1);
    }

    private static bool TryReadText(IXLCell cell, out string value)
    {
        if (cell.IsEmpty())
        {
            value = string.Empty;
            return true;
        }

        if (cell.DataType != XLDataType.Text)
        {
            value = string.Empty;
            return false;
        }

        value = cell.GetString();
        return true;
    }

    private static bool IsSupportedHeader(string name) =>
        string.Equals(name, "Name", StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, "Category", StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, "ID", StringComparison.OrdinalIgnoreCase)
        || string.Equals(name, "Selected", StringComparison.OrdinalIgnoreCase);

    private sealed record HeaderColumns(
        ImmutableArray<string> Names,
        int? NameColumn,
        int? CategoryColumn,
        bool HasName,
        bool HasCategory,
        bool DuplicateName,
        bool DuplicateCategory);

    private sealed record Analysis(
        ImmutableArray<PayeeMatchingRuleEditDto> Rules,
        IReadOnlyCollection<string> IgnoredColumns,
        ImmutableArray<DataImportIssueDto> Errors,
        ImmutableArray<DataImportIssueDto> SkippedRows);
}
