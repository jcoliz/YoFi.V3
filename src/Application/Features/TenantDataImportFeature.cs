using System.Security.Cryptography;
using System.Xml;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Exceptions;
using YoFi.V3.Application.Services;
using YoFi.V3.Entities.Exceptions;

namespace YoFi.V3.Application.Features;

/// <summary>Coordinates source-format detection, worksheet preview, and confirmed workbook imports.</summary>
/// <param name="formatDetectors">Registered source-format detectors.</param>
/// <param name="worksheetHandlers">Registered format-specific worksheet/data-type handlers.</param>
public sealed class TenantDataImportFeature(
    IEnumerable<IWorkbookImportFormatDetector> formatDetectors,
    IEnumerable<IImportWorksheetHandler> worksheetHandlers)
{
    /// <summary>Parses and previews all recognized worksheet data without persistence.</summary>
    /// <param name="file">Exact uploaded XLSX bytes.</param>
    /// <returns>Format-neutral worksheet previews, ignored worksheets, and a digest for confirmation.</returns>
    public TenantDataImportPreviewDto Preview(byte[] file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var sha256 = Convert.ToHexString(SHA256.HashData(file));
        using var workbook = OpenWorkbook(file);
        var detected = DetectFormat(workbook);
        if (detected is null)
        {
            return new TenantDataImportPreviewDto(
                null,
                [],
                workbook.Worksheets.Select(worksheet => worksheet.Name).ToArray(),
                sha256);
        }

        var previews = new List<ImportWorksheetPreviewDto>();
        var ignored = new List<string>();
        foreach (var worksheet in workbook.Worksheets)
        {
            var handler = FindHandler(detected, worksheet.Name);
            if (handler is null)
            {
                ignored.Add(worksheet.Name);
            }
            else
            {
                if (handler is IWorkbookImportContextHandler contextHandler)
                    contextHandler.SetWorkbookContext(workbook);
                previews.Add(handler.Preview(worksheet));
            }
        }

        return new TenantDataImportPreviewDto(detected, previews, ignored, sha256);
    }

    /// <summary>Revalidates the confirmed workbook and commits each recognized worksheet atomically.</summary>
    /// <param name="file">Exact uploaded XLSX bytes.</param>
    /// <param name="expectedSha256">Digest returned by the preview operation.</param>
    /// <param name="cancellationToken">Token passed to each worksheet persistence operation.</param>
    /// <returns>Per-worksheet committed outcomes.</returns>
    /// <exception cref="ValidationException">The file differs from the preview or is not a readable XLSX.</exception>
    /// <exception cref="TenantDataImportValidationException">Format detection or worksheet validation failed.</exception>
    public async Task<TenantDataImportResultDto> ImportAsync(
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

        using var workbook = OpenWorkbook(file);
        var detected = DetectFormat(workbook);
        var previews = new List<ImportWorksheetPreviewDto>();
        var handlers = new List<IImportWorksheetHandler>();
        var ignored = new List<string>();

        if (detected is not null)
        {
            foreach (var worksheet in workbook.Worksheets)
            {
                var handler = FindHandler(detected, worksheet.Name);
                if (handler is null)
                {
                    ignored.Add(worksheet.Name);
                    continue;
                }

                handlers.Add(handler);
                if (handler is IWorkbookImportContextHandler contextHandler)
                    contextHandler.SetWorkbookContext(workbook);
                previews.Add(handler.Preview(worksheet));
            }
        }

        var preview = new TenantDataImportPreviewDto(detected, previews, ignored, sha256);
        if (!preview.CanImport)
        {
            throw new TenantDataImportValidationException(preview);
        }

        var ordered = handlers
            .Select((handler, index) => new { Handler = handler, Preview = previews[index] })
            .OrderBy(item => item.Handler.DependencyOrder)
            .ThenBy(item => item.Preview.WorksheetName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var completedWorksheetNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var workbookWorksheetNames = workbook.Worksheets
            .Select(worksheet => worksheet.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var results = new List<ImportWorksheetResultDto>(ordered.Length);
        foreach (var item in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var requiredName in item.Handler.RequiresWorksheetNames)
            {
                if (!workbookWorksheetNames.Contains(requiredName))
                {
                    throw new TenantDataImportValidationException(new TenantDataImportPreviewDto(
                        detected,
                        previews,
                        ignored,
                        sha256));
                }
                if (!completedWorksheetNames.Contains(requiredName))
                {
                    throw new TenantDataImportValidationException(new TenantDataImportPreviewDto(
                        detected,
                        previews,
                        ignored,
                        sha256));
                }
            }

            if (item.Handler is IWorkbookImportContextHandler contextHandler)
                contextHandler.SetWorkbookContext(workbook);
            var worksheet = workbook.Worksheet(item.Preview.WorksheetName);
            results.Add(await item.Handler.ImportAsync(worksheet, cancellationToken));
            completedWorksheetNames.Add(item.Preview.WorksheetName);
        }

        return new TenantDataImportResultDto(results);
    }

    private XLWorkbook OpenWorkbook(byte[] file)
    {
        var stream = new MemoryStream(file, writable: false);
        try
        {
            return new XLWorkbook(stream);
        }
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
        finally
        {
            stream.Dispose();
        }
    }

    private string? DetectFormat(XLWorkbook workbook)
    {
        var detected = formatDetectors.Where(detector => detector.CanDetect(workbook)).ToArray();
        return detected.Length switch
        {
            0 => null,
            1 => detected[0].FormatId,
            _ => throw new ValidationException("file", "The workbook matches multiple supported source formats.")
        };
    }

    private IImportWorksheetHandler? FindHandler(string formatId, string worksheetName)
    {
        var handlers = worksheetHandlers.Where(handler =>
            string.Equals(handler.FormatId, formatId, StringComparison.Ordinal)
            && string.Equals(handler.WorksheetName, worksheetName, StringComparison.OrdinalIgnoreCase)).ToArray();
        return handlers.Length switch
        {
            0 => null,
            1 => handlers[0],
            _ => throw new InvalidOperationException(
                $"Multiple import handlers are registered for format '{formatId}' worksheet '{worksheetName}'.")
        };
    }
}
