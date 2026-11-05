using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Features;
using YoFi.V3.Controllers.Tenancy.Authorization;
using YoFi.V3.Entities.Exceptions;
using YoFi.V3.Entities.Tenancy.Models;

namespace YoFi.V3.Controllers;

/// <summary>Imports and deletes tenant data for a workspace Owner.</summary>
/// <param name="tenantDataAdminFeature">Feature coordinating workspace-data deletion.</param>
/// <param name="tenantDataImportFeature">Feature coordinating workbook import preview and execution.</param>
/// <param name="logger">Logger for operation outcomes and committed counts.</param>
[Route("api/tenant/{tenantKey:guid}/admin")]
[ApiController]
[RequireTenantRole(TenantRole.Owner)]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public partial class TenantDataAdminController(
    TenantDataAdminFeature tenantDataAdminFeature,
    TenantDataImportFeature tenantDataImportFeature,
    ILogger<TenantDataAdminController> logger) : ControllerBase
{
    private const long MaxWorkbookSizeBytes = 50 * 1024 * 1024;

    /// <summary>Previews the supported data and validation issues in an uploaded XLSX workbook.</summary>
    /// <param name="tenantKey">Route key identifying the authorized target workspace.</param>
    /// <param name="file">XLSX workbook to analyze.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>Detected format, per-worksheet preview, and digest for import confirmation.</returns>
    [HttpPost("import/preview")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxWorkbookSizeBytes)]
    [ProducesResponseType(typeof(TenantDataImportPreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PreviewImport(
        [FromRoute] Guid tenantKey,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        var workbook = await ReadWorkbookAsync(file, cancellationToken);
        var preview = tenantDataImportFeature.Preview(workbook);
        LogPreviewOk(
            preview.FormatId,
            preview.Worksheets.Count,
            preview.Worksheets.Sum(worksheet => worksheet.ValidRecordCount),
            preview.Worksheets.Sum(worksheet => worksheet.SkippedRecordCount),
            preview.Worksheets.Sum(worksheet => worksheet.Errors.Count),
            preview.IgnoredWorksheets.Count);
        return Ok(preview);
    }

    /// <summary>Imports a previously previewed XLSX workbook into the authorized workspace.</summary>
    /// <param name="tenantKey">Route key identifying the authorized target workspace.</param>
    /// <param name="file">The exact XLSX workbook bytes returned by the preview operation.</param>
    /// <param name="expectedSha256">Digest returned by the preview operation.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>Committed per-worksheet import counts.</returns>
    [HttpPost("import")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxWorkbookSizeBytes)]
    [ProducesResponseType(typeof(TenantDataImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ImportData(
        [FromRoute] Guid tenantKey,
        IFormFile? file,
        [FromForm] string? expectedSha256,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(expectedSha256))
        {
            throw new ValidationException(nameof(expectedSha256), "The preview digest is required.");
        }

        LogImportStarting();
        var succeeded = false;
        try
        {
            var workbook = await ReadWorkbookAsync(file, cancellationToken);
            var result = await tenantDataImportFeature.ImportAsync(workbook, expectedSha256, cancellationToken);
            LogImportOk(
                result.Worksheets.Count,
                result.Worksheets.Sum(worksheet => worksheet.Created),
                result.Worksheets.Sum(worksheet => worksheet.Skipped),
                result.Worksheets.Sum(worksheet => worksheet.Updated));
            succeeded = true;
            return Ok(result);
        }
        finally
        {
            if (!succeeded)
            {
                LogImportFailed();
            }
        }
    }

    /// <summary>Deletes all supported workspace data, preserving the workspace and its memberships.</summary>
    /// <param name="tenantKey">Route key identifying the authorized target workspace.</param>
    /// <param name="request">Exact current workspace name required for confirmation.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>Committed per-type deletion counts.</returns>
    [HttpDelete("data")]
    [ProducesResponseType(typeof(TenantDataDeletionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteAllData(
        [FromRoute] Guid tenantKey,
        [FromBody] TenantDataDeletionRequest request,
        CancellationToken cancellationToken)
    {
        LogStarting();
        var succeeded = false;
        try
        {
            var result = await tenantDataAdminFeature.DeleteAllAsync(request, cancellationToken);
            LogOk(result.Deleted.Transactions, result.Deleted.Splits,
                result.Deleted.PayeeMatchingRules, result.Deleted.ImportReviewTransactions);
            succeeded = true;
            return Ok(result);
        }
        finally
        {
            if (!succeeded)
            {
                LogFailed();
            }
        }
    }

    private static async Task<byte[]> ReadWorkbookAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            throw new ValidationException(nameof(file), "An XLSX workbook is required and cannot be empty.");
        }

        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException(nameof(file), "Only .xlsx workbooks are supported.");
        }

        if (file.Length > MaxWorkbookSizeBytes)
        {
            throw new ValidationException(nameof(file),
                $"The workbook must not exceed {MaxWorkbookSizeBytes / (1024 * 1024)} MB.");
        }

        await using var stream = new MemoryStream((int)file.Length);
        await file.CopyToAsync(stream, cancellationToken);
        return stream.ToArray();
    }

    [LoggerMessage(1, LogLevel.Debug, "{Location}: Starting")]
    private partial void LogStarting([CallerMemberName] string? location = null);

    [LoggerMessage(2, LogLevel.Information, "{Location}: OK Transactions={Transactions} Splits={Splits} Rules={Rules} ImportReviews={ImportReviews}")]
    private partial void LogOk(int transactions, int splits, int rules,
        int importReviews, [CallerMemberName] string? location = null);

    [LoggerMessage(3, LogLevel.Warning, "{Location}: Failed")]
    private partial void LogFailed([CallerMemberName] string? location = null);

    [LoggerMessage(4, LogLevel.Information, "{Location}: Previewed import Format={FormatId} Worksheets={Worksheets} Valid={Valid} Skipped={Skipped} Errors={Errors} Ignored={Ignored}")]
    private partial void LogPreviewOk(
        string? formatId,
        int worksheets,
        int valid,
        int skipped,
        int errors,
        int ignored,
        [CallerMemberName] string? location = null);

    [LoggerMessage(5, LogLevel.Debug, "{Location}: Starting import")]
    private partial void LogImportStarting([CallerMemberName] string? location = null);

    [LoggerMessage(6, LogLevel.Information, "{Location}: Imported Worksheets={Worksheets} Created={Created} Skipped={Skipped} Updated={Updated}")]
    private partial void LogImportOk(
        int worksheets,
        int created,
        int skipped,
        int updated,
        [CallerMemberName] string? location = null);

    [LoggerMessage(7, LogLevel.Warning, "{Location}: Import failed")]
    private partial void LogImportFailed([CallerMemberName] string? location = null);
}
