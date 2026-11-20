using System.Globalization;
using ClosedXML.Excel;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Exceptions;
using YoFi.V3.Application.Helpers;
using YoFi.V3.Application.Validation;

namespace YoFi.V3.Application.Services;

/// <summary>Parses split-upload workbooks and creates the downloadable XLSX template.</summary>
public sealed class TransactionSplitWorkbookService
{
    private const string InvalidWorkbookMessage =
        "The uploaded file is not a readable XLSX workbook.";

    /// <summary>Parses and validates split rows, using the first worksheet when no memo match is available.</summary>
    /// <param name="content">Exact uploaded XLSX bytes.</param>
    /// <returns>Validated, non-zero rows in source order.</returns>
    /// <exception cref="ValidationProblemsException">The workbook or one or more rows is invalid.</exception>
    public IReadOnlyCollection<SplitUploadRowDto> Parse(byte[] content)
        => Parse(content, transactionMemo: null);

    /// <summary>Parses and validates split rows, preferring a worksheet matching the transaction memo.</summary>
    /// <param name="content">Exact uploaded XLSX bytes.</param>
    /// <param name="transactionMemo">The authoritative transaction memo used to select a worksheet.</param>
    /// <returns>Validated, non-zero rows in source order.</returns>
    /// <exception cref="ValidationProblemsException">The workbook or one or more rows is invalid.</exception>
    public IReadOnlyCollection<SplitUploadRowDto> Parse(byte[] content, string? transactionMemo)
    {
        ArgumentNullException.ThrowIfNull(content);

        var issues = new List<SplitUploadIssueDto>();
        if (content.Length == 0)
        {
            throw CreateInvalidWorkbookException();
        }

        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(new MemoryStream(content, writable: false));
        }
        catch (Exception exception) when (exception is InvalidDataException
            or IOException
            or ArgumentException
            or FormatException
            or System.Xml.XmlException
            or InvalidOperationException
            or DocumentFormat.OpenXml.Packaging.OpenXmlPackageException
            or NullReferenceException)
        {
            throw CreateInvalidWorkbookException();
        }

        if (workbook.Worksheets.Count == 0)
        {
            throw CreateInvalidWorkbookException();
        }

        using (workbook)
        {
            var worksheet = SelectWorksheet(workbook, transactionMemo);
        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var duplicateHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lastColumn = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;

        for (var column = 1; column <= lastColumn; column++)
        {
            var name = worksheet.Cell(1, column).GetString().Trim();
            if (!IsRecognizedHeader(name))
            {
                continue;
            }

            if (!headers.TryAdd(name, column))
            {
                duplicateHeaders.Add(name);
            }
        }

        foreach (var required in new[] { "Category", "Amount" })
        {
            if (!headers.ContainsKey(required))
            {
                issues.Add(new SplitUploadIssueDto(1, "Header",
                    "missing_header", $"The required {required} header is missing."));
            }
        }

        foreach (var duplicate in duplicateHeaders)
        {
            issues.Add(new SplitUploadIssueDto(1, "Header",
                "duplicate_header", $"The {duplicate} header appears more than once."));
        }

        headers.TryGetValue("Category", out var categoryColumn);
        headers.TryGetValue("Amount", out var amountColumn);

        var rows = new List<SplitUploadRowDto>();
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        var nonEmptyCount = 0;
        var rowLimitExceeded = false;

        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            var row = worksheet.Row(rowNumber);
            if (row.IsEmpty())
            {
                continue;
            }

            nonEmptyCount++;
            if (nonEmptyCount > 200)
            {
                rowLimitExceeded = true;
                continue;
            }

            if (!headers.ContainsKey("Amount"))
            {
                continue;
            }

            var amountCell = worksheet.Cell(rowNumber, amountColumn);
            if (!TryReadAmount(amountCell, out var amount))
            {
                var code = amountCell.HasFormula
                    ? "invalid_formula_result"
                    : "invalid_amount";
                issues.Add(new SplitUploadIssueDto(rowNumber, "Amount", code,
                    amountCell.HasFormula
                        ? "The formula in Amount does not have a usable cached numeric result."
                        : "Amount must be a decimal number."));
                continue;
            }

            if (amount == 0m)
            {
                continue;
            }

            string category;
            if (categoryColumn == 0)
            {
                category = string.Empty;
            }
            else
            {
                var categoryCell = worksheet.Cell(rowNumber, categoryColumn);
                if (categoryCell.HasFormula)
                {
                    if (!TryReadCachedText(categoryCell, out category))
                    {
                        issues.Add(new SplitUploadIssueDto(rowNumber, "Category",
                            "invalid_formula_result",
                            "The formula in Category does not have a usable cached text result."));
                        category = string.Empty;
                    }
                }
                else
                {
                    category = categoryCell.GetString();
                }
            }
            category = CategoryHelper.SanitizeCategory(category);

            string? memo = null;
            if (headers.TryGetValue("Memo", out var memoColumn))
            {
                var memoCell = worksheet.Cell(rowNumber, memoColumn);
                if (memoCell.HasFormula)
                {
                    if (!TryReadCachedText(memoCell, out var memoText))
                    {
                        issues.Add(new SplitUploadIssueDto(rowNumber, "Memo",
                            "invalid_formula_result",
                            "The formula in Memo does not have a usable cached text result."));
                    }
                    else
                    {
                        memo = NormalizeMemo(memoText);
                    }
                }
                else
                {
                    memo = NormalizeMemo(memoCell.GetString());
                }
            }

            var rowValidator = new SplitEditDtoValidator();
            var validation = rowValidator.Validate(new SplitEditDto(amount, category, memo));
            foreach (var failure in validation.Errors)
            {
                var field = failure.PropertyName switch
                {
                    nameof(SplitEditDto.Amount) => "Amount",
                    nameof(SplitEditDto.Category) => "Category",
                    nameof(SplitEditDto.Memo) => "Memo",
                    _ => failure.PropertyName
                };
                issues.Add(new SplitUploadIssueDto(rowNumber, field,
                    "invalid_value", failure.ErrorMessage));
            }

            rows.Add(new SplitUploadRowDto(rowNumber, amount, category, memo));
        }

        if (rowLimitExceeded)
        {
            issues.Add(new SplitUploadIssueDto(1, "file",
                "row_limit_exceeded",
                "The worksheet contains more than 200 non-empty data rows."));
        }

            if (issues.Count == 0 && rows.Count == 0)
            {
                issues.Add(new SplitUploadIssueDto(1, "file", "empty_import",
                    "The worksheet does not contain any non-zero splits to import."));
            }

            if (issues.Count > 0)
            {
                var grouped = issues
                .GroupBy(issue => $"Row {issue.RowNumber}: {issue.Field}",
                    StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(issue => issue.Message).ToArray(),
                    StringComparer.Ordinal);

                throw new ValidationProblemsException(
                    "The split workbook failed validation.",
                    "Correct the workbook errors and upload it again. No splits were changed.",
                    grouped);
            }

            return rows;
        }
    }

    /// <summary>Creates the supported transaction-split XLSX template.</summary>
    /// <returns>The generated workbook bytes and download filename.</returns>
    public SplitUploadTemplateResultDto CreateTemplate()
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Splits");

        worksheet.Cell(1, 1).Value = "Category";
        worksheet.Cell(1, 2).Value = "Amount";
        worksheet.Cell(1, 3).Value = "Memo";
        worksheet.Cell(1, 1).CreateComment().AddText("Category is optional.");
        worksheet.Cell(1, 2).CreateComment().AddText("Amount is required and must be a signed currency value.");
        worksheet.Cell(1, 3).CreateComment().AddText("Memo is optional.");

        worksheet.Column(2).Style.NumberFormat.Format = "0.00";
        worksheet.SheetView.FreezeRows(1);
        worksheet.Columns(1, 3).AdjustToContents();
        worksheet.Column(1).Width = Math.Max(worksheet.Column(1).Width, 18);
        worksheet.Column(2).Width = Math.Max(worksheet.Column(2).Width, 12);
        worksheet.Column(3).Width = Math.Max(worksheet.Column(3).Width, 24);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return new SplitUploadTemplateResultDto(
            stream.ToArray(),
            "yofi-transaction-splits-template.xlsx");
    }

    private static IXLWorksheet SelectWorksheet(XLWorkbook workbook, string? transactionMemo)
    {
        var fallback = workbook.Worksheets.First();
        var normalizedMemo = transactionMemo?.Trim();
        if (string.IsNullOrEmpty(normalizedMemo))
        {
            return fallback;
        }

        return workbook.Worksheets
            .FirstOrDefault(worksheet =>
                worksheet.Name.Trim().Equals(normalizedMemo, StringComparison.OrdinalIgnoreCase))
            ?? fallback;
    }

    private static ValidationProblemsException CreateInvalidWorkbookException()
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Row 1: file"] = [InvalidWorkbookMessage]
        };
        return new ValidationProblemsException(
            "The split workbook failed validation.",
            "Correct the workbook errors and upload it again. No splits were changed.",
            errors);
    }

    private static bool IsRecognizedHeader(string name)
        => name.Equals("Category", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Amount", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Memo", StringComparison.OrdinalIgnoreCase);

    private static bool TryReadAmount(IXLCell cell, out decimal amount)
    {
        amount = 0m;
        if (cell.HasFormula)
        {
            if (!cell.CachedValue.IsNumber)
            {
                return false;
            }

            amount = Convert.ToDecimal(cell.CachedValue.GetNumber(), CultureInfo.InvariantCulture);
            return true;
        }

        if (cell.DataType == XLDataType.Number)
        {
            amount = Convert.ToDecimal(cell.GetDouble(), CultureInfo.InvariantCulture);
            return true;
        }

        if (cell.DataType == XLDataType.Text)
        {
            return decimal.TryParse(
                cell.GetString().Trim(),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out amount);
        }

        return false;
    }

    private static bool TryReadCachedText(IXLCell cell, out string text)
    {
        text = string.Empty;
        if (!cell.CachedValue.IsText)
        {
            return false;
        }

        text = cell.CachedValue.GetText();
        return true;
    }

    private static string? NormalizeMemo(string? memo)
        => string.IsNullOrWhiteSpace(memo) ? null : memo.Trim();
}
