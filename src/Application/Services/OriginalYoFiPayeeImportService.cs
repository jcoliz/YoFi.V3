using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Xml;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Exceptions;
using YoFi.V3.Application.Helpers;
using YoFi.V3.Application.Validation;
using YoFi.V3.Entities.Exceptions;

namespace YoFi.V3.Application.Services;

/// <summary>Previews and imports the original YoFi Payee worksheet as V3 payee matching rules.</summary>
/// <param name="dataImportService">Shared create-only persistence path for rules.</param>
/// <param name="regexValidationService">Existing safe-regex and payee-rule validator dependency.</param>
public sealed class OriginalYoFiPayeeImportService(
    DataImportService dataImportService,
    IRegexValidationService regexValidationService)
{
    /// <summary>Parses and validates the Payee worksheet without persisting records.</summary>
    /// <param name="file">Exact uploaded XLSX bytes.</param>
    /// <returns>Detection summary, row counts, issues, and digest used to bind confirmation.</returns>
    public PayeeImportPreviewDto Preview(byte[] file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var sha256 = Convert.ToHexString(SHA256.HashData(file));
        var analysis = Analyze(file, sha256);
        return analysis.Preview;
    }

    /// <summary>Revalidates the confirmed file and atomically creates all valid worksheet rows.</summary>
    /// <param name="file">Exact uploaded XLSX bytes.</param>
    /// <param name="expectedSha256">Digest returned by the preview operation.</param>
    /// <param name="cancellationToken">Token passed to persistence after parsing and validation.</param>
    /// <returns>Committed created and skipped counts.</returns>
    /// <exception cref="ValidationException">The file differs from the previewed file or is not a valid XLSX.</exception>
    /// <exception cref="PayeeImportValidationException">The worksheet is missing or contains blocking row errors.</exception>
    public async Task<PayeeImportResultDto> ImportAsync(
        byte[] file,
        string expectedSha256,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedSha256);

        var sha256 = Convert.ToHexString(SHA256.HashData(file));
        if (!string.Equals(sha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException(nameof(expectedSha256),
                "The uploaded file differs from the file shown in the preview. Preview it again before importing.");
        }

        var analysis = Analyze(file, sha256);
        if (!analysis.Preview.CanImport)
        {
            throw new PayeeImportValidationException(analysis.Preview);
        }

        var created = await dataImportService.CreateRulesAsync(analysis.Rules, cancellationToken);
        return new PayeeImportResultDto(created, analysis.Preview.SkippedRecordCount, 0, 0);
    }

    private Analysis Analyze(byte[] file, string sha256)
    {
        using var stream = new MemoryStream(file, writable: false);
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(stream);
        }
        // ClosedXML can throw NullReferenceException while opening incomplete OpenXML packages.
        catch (Exception exception) when (exception is InvalidDataException
            or IOException
            or ArgumentException
            or FormatException
            or XmlException
            or InvalidOperationException
            or OpenXmlPackageException
            or NullReferenceException)
        {
            throw new ValidationException("file", "The uploaded file is not a readable XLSX workbook.", exception);
        }

        using (workbook)
        {
            var worksheet = workbook.Worksheets.FirstOrDefault(sheet =>
                string.Equals(sheet.Name, "Payee", StringComparison.OrdinalIgnoreCase));
            var ignoredWorksheets = workbook.Worksheets
                .Where(sheet => !ReferenceEquals(sheet, worksheet))
                .Select(sheet => sheet.Name)
                .ToImmutableArray();

            if (worksheet is null)
            {
                return new Analysis(
                    new PayeeImportPreviewDto(
                        false, 0, 0, ignoredWorksheets, [], [], [], sha256),
                    []);
            }

            var errors = ImmutableArray.CreateBuilder<PayeeImportIssueDto>();
            var skippedRows = ImmutableArray.CreateBuilder<PayeeImportIssueDto>();
            var headerColumns = ReadHeaders(worksheet);
            var ignoredColumns = headerColumns.Names
                .Where(name => !IsSupportedHeader(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToImmutableArray();

            if (!headerColumns.HasName)
            {
                errors.Add(new PayeeImportIssueDto(1, "Name", "missing_header", "The required Name column is missing."));
            }
            if (!headerColumns.HasCategory)
            {
                errors.Add(new PayeeImportIssueDto(1, "Category", "missing_header", "The required Category column is missing."));
            }
            if (headerColumns.DuplicateName)
            {
                errors.Add(new PayeeImportIssueDto(1, "Name", "duplicate_header", "The Name column must appear exactly once."));
            }
            if (headerColumns.DuplicateCategory)
            {
                errors.Add(new PayeeImportIssueDto(1, "Category", "duplicate_header", "The Category column must appear exactly once."));
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
                        errors.Add(new PayeeImportIssueDto(rowNumber, "Name", "formula_not_allowed",
                            "Formula cells are not supported for Name."));
                        continue;
                    }
                    if (categoryCell.HasFormula)
                    {
                        errors.Add(new PayeeImportIssueDto(rowNumber, "Category", "formula_not_allowed",
                            "Formula cells are not supported for Category."));
                        continue;
                    }

                    if (!TryReadText(nameCell, out var name))
                    {
                        errors.Add(new PayeeImportIssueDto(rowNumber, "Name", "invalid_cell_type",
                            "Name must be a text cell."));
                        continue;
                    }
                    if (!TryReadText(categoryCell, out var category))
                    {
                        errors.Add(new PayeeImportIssueDto(rowNumber, "Category", "invalid_cell_type",
                            "Category must be a text cell."));
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(category))
                    {
                        skippedRows.Add(new PayeeImportIssueDto(rowNumber, "Category", "blank_category",
                            "The row was skipped because Category is blank."));
                        continue;
                    }
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        errors.Add(new PayeeImportIssueDto(rowNumber, "Name", "required",
                            "Name is required."));
                        continue;
                    }

                    var trimmedName = name.Trim();
                    var isRegex = trimmedName.Length >= 2
                        && trimmedName[0] == '/'
                        && trimmedName[^1] == '/';
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
                            errors.Add(new PayeeImportIssueDto(rowNumber, field, "invalid_value", failure.ErrorMessage));
                        }
                        continue;
                    }

                    rules.Add(dto);
                }
            }

            var preview = new PayeeImportPreviewDto(
                true,
                rules.Count,
                skippedRows.Count,
                ignoredWorksheets,
                ignoredColumns,
                errors.ToImmutable(),
                skippedRows.ToImmutable(),
                sha256);
            return new Analysis(preview, rules.ToImmutable());
        }
    }

    private static HeaderColumns ReadHeaders(IXLWorksheet worksheet)
    {
        var headers = worksheet.Row(1).CellsUsed()
            .Select(cell => (Name: cell.HasFormula ? string.Empty : cell.GetString().Trim(), Column: cell.Address.ColumnNumber))
            .ToArray();
        var names = headers.Select(header => header.Name).Where(name => name.Length > 0).ToImmutableArray();
        var nameColumns = headers.Where(header => string.Equals(header.Name, "Name", StringComparison.OrdinalIgnoreCase))
            .Select(header => header.Column).ToArray();
        var categoryColumns = headers.Where(header => string.Equals(header.Name, "Category", StringComparison.OrdinalIgnoreCase))
            .Select(header => header.Column).ToArray();
        return new HeaderColumns(
            names,
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

    private sealed record Analysis(PayeeImportPreviewDto Preview, ImmutableArray<PayeeMatchingRuleEditDto> Rules);
}
