using System.Globalization;
using ClosedXML.Excel;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Helpers;
using YoFi.V3.Application.Validation;
using YoFi.V3.Entities.Exceptions;

namespace YoFi.V3.Application.Services;

/// <summary>Parses split-upload workbooks and creates the downloadable XLSX template.</summary>
public sealed class TransactionSplitWorkbookService
{
    private const string InvalidWorkbookMessage =
        "The uploaded file is not a readable XLSX workbook.";

    /// <summary>Parses and validates split rows, using the first worksheet when no memo match is available.</summary>
    /// <param name="content">Exact uploaded XLSX bytes.</param>
    /// <returns>Validated, non-zero rows in source order.</returns>
    /// <exception cref="ValidationException">The workbook or one or more rows is invalid.</exception>
    public IReadOnlyCollection<SplitUploadRowDto> Parse(byte[] content)
        => Parse(content, transactionMemo: null);

    /// <summary>Parses and validates split rows, preferring a worksheet matching the transaction memo.</summary>
    /// <param name="content">Exact uploaded XLSX bytes.</param>
    /// <param name="transactionMemo">The authoritative transaction memo used to select a worksheet.</param>
    /// <returns>Validated, non-zero rows in source order.</returns>
    /// <exception cref="ValidationException">The workbook or one or more rows is invalid.</exception>
    public IReadOnlyCollection<SplitUploadRowDto> Parse(byte[] content, string? transactionMemo)
    {
        ArgumentNullException.ThrowIfNull(content);
        using var workbook = OpenWorkbook(content);

        var worksheet = SelectWorksheet(workbook, transactionMemo);
        var issues = new List<SplitUploadIssueDto>();
        var headers = ReadHeaders(worksheet, issues);
        var (rows, rowLimitExceeded) = ParseRows(worksheet, headers, issues);

        if (rowLimitExceeded)
            issues.Add(new SplitUploadIssueDto(1, "file", "row_limit_exceeded",
                "The worksheet contains more than 200 non-empty data rows."));
        if (issues.Count == 0 && rows.Count == 0)
            issues.Add(new SplitUploadIssueDto(1, "file", "empty_import",
                "The worksheet does not contain any non-zero splits to import."));

        ThrowIfInvalid(issues);
        return rows;
    }

    private static XLWorkbook OpenWorkbook(byte[] content)
    {
        if (content.Length == 0)
            throw CreateInvalidWorkbookException();

        try
        {
            var workbook = new XLWorkbook(new MemoryStream(content, writable: false));
            return workbook.Worksheets.Count == 0
                ? throw CreateInvalidWorkbookException()
                : workbook;
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
    }

    private static Dictionary<string, int> ReadHeaders(
        IXLWorksheet worksheet,
        List<SplitUploadIssueDto> issues)
    {
        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var duplicateHeaders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lastColumn = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;

        for (var column = 1; column <= lastColumn; column++)
        {
            var name = worksheet.Cell(1, column).GetString().Trim();
            if (!IsRecognizedHeader(name))
                continue;
            if (!headers.TryAdd(name, column))
                duplicateHeaders.Add(name);
        }

        foreach (var required in new[] { "Category", "Amount" })
        {
            if (!headers.ContainsKey(required))
                issues.Add(new SplitUploadIssueDto(1, "Header", "missing_header",
                    $"The required {required} header is missing."));
        }
        foreach (var duplicate in duplicateHeaders)
            issues.Add(new SplitUploadIssueDto(1, "Header", "duplicate_header",
                $"The {duplicate} header appears more than once."));

        return headers;
    }

    private static (List<SplitUploadRowDto> Rows, bool RowLimitExceeded) ParseRows(
        IXLWorksheet worksheet,
        IReadOnlyDictionary<string, int> headers,
        List<SplitUploadIssueDto> issues)
    {
        var rows = new List<SplitUploadRowDto>();
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        var nonEmptyCount = 0;
        var rowLimitExceeded = false;

        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            if (worksheet.Row(rowNumber).IsEmpty())
                continue;

            nonEmptyCount++;
            if (nonEmptyCount > 200)
            {
                rowLimitExceeded = true;
                continue;
            }

            var row = ParseRow(worksheet, headers, rowNumber, issues);
            if (row is not null)
                rows.Add(row);
        }

        return (rows, rowLimitExceeded);
    }

    private static SplitUploadRowDto? ParseRow(
        IXLWorksheet worksheet,
        IReadOnlyDictionary<string, int> headers,
        int rowNumber,
        List<SplitUploadIssueDto> issues)
    {
        if (!headers.TryGetValue("Amount", out var amountColumn))
            return null;

        var amountCell = worksheet.Cell(rowNumber, amountColumn);
        if (!TryReadAmount(amountCell, out var amount))
        {
            var code = amountCell.HasFormula ? "invalid_formula_result" : "invalid_amount";
            var message = amountCell.HasFormula
                ? "The formula in Amount does not have a usable cached numeric result."
                : "Amount must be a decimal number.";
            issues.Add(new SplitUploadIssueDto(rowNumber, "Amount", code, message));
            return null;
        }
        if (amount == 0m)
            return null;

        var category = ReadCategory(worksheet, headers, rowNumber, issues);
        var memo = ReadMemo(worksheet, headers, rowNumber, issues);
        ValidateRow(rowNumber, amount, category, memo, issues);
        return new SplitUploadRowDto(rowNumber, amount, category, memo);
    }

    private static string ReadCategory(
        IXLWorksheet worksheet,
        IReadOnlyDictionary<string, int> headers,
        int rowNumber,
        List<SplitUploadIssueDto> issues)
    {
        if (!headers.TryGetValue("Category", out var categoryColumn))
            return string.Empty;

        var categoryCell = worksheet.Cell(rowNumber, categoryColumn);
        if (!categoryCell.HasFormula)
            return CategoryHelper.SanitizeCategory(categoryCell.GetString());

        if (TryReadCachedText(categoryCell, out var category))
            return CategoryHelper.SanitizeCategory(category);

        issues.Add(new SplitUploadIssueDto(rowNumber, "Category", "invalid_formula_result",
            "The formula in Category does not have a usable cached text result."));
        return string.Empty;
    }

    private static string? ReadMemo(
        IXLWorksheet worksheet,
        IReadOnlyDictionary<string, int> headers,
        int rowNumber,
        List<SplitUploadIssueDto> issues)
    {
        if (!headers.TryGetValue("Memo", out var memoColumn))
            return null;

        var memoCell = worksheet.Cell(rowNumber, memoColumn);
        if (!memoCell.HasFormula)
            return NormalizeMemo(memoCell.GetString());

        if (TryReadCachedText(memoCell, out var memoText))
            return NormalizeMemo(memoText);

        issues.Add(new SplitUploadIssueDto(rowNumber, "Memo", "invalid_formula_result",
            "The formula in Memo does not have a usable cached text result."));
        return null;
    }

    private static void ValidateRow(int rowNumber, decimal amount, string category, string? memo,
        List<SplitUploadIssueDto> issues)
    {
        var validation = new SplitEditDtoValidator().Validate(new SplitEditDto(amount, category, memo));
        foreach (var failure in validation.Errors)
        {
            var field = failure.PropertyName switch
            {
                nameof(SplitEditDto.Amount) => "Amount",
                nameof(SplitEditDto.Category) => "Category",
                nameof(SplitEditDto.Memo) => "Memo",
                _ => failure.PropertyName
            };
            issues.Add(new SplitUploadIssueDto(rowNumber, field, "invalid_value", failure.ErrorMessage));
        }
    }

    private static void ThrowIfInvalid(List<SplitUploadIssueDto> issues)
    {
        if (issues.Count == 0)
            return;

        var grouped = issues
            .GroupBy(issue => $"Row {issue.RowNumber}: {issue.Field}", StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(issue => issue.Message).ToArray(),
                StringComparer.Ordinal);

        throw new ValidationException(
            "Correct the workbook errors and upload it again. No splits were changed.")
        {
            Title = "The split workbook failed validation.",
            Errors = grouped
        };
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

    private static ValidationException CreateInvalidWorkbookException()
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Row 1: file"] = [InvalidWorkbookMessage]
        };
        return new ValidationException(
            "Correct the workbook errors and upload it again. No splits were changed.")
        {
            Title = "The split workbook failed validation.",
            Errors = errors
        };
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
