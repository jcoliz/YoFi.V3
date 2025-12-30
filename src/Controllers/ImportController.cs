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

/// <summary>
/// Manages bank transaction import operations within a tenant workspace.
/// </summary>
/// <param name="importReviewFeature">Feature providing import review workflow operations.</param>
/// <param name="logger">Logger for diagnostic output.</param>
/// <remarks>
/// All operations are scoped to a specific tenant identified by the tenantKey route parameter.
/// Users must have Editor or Owner roles to access import endpoints.
/// Supports OFX and QFX file formats with duplicate detection and review workflow.
/// </remarks>
[Route("api/tenant/{tenantKey:guid}/import")]
[ApiController]
[RequireTenantRole(TenantRole.Editor)]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public partial class ImportController(
    ImportReviewFeature importReviewFeature,
    ILogger<ImportController> logger) : ControllerBase
{
    private static readonly string[] AllowedExtensions = [".ofx", ".qfx"];
    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB

    /// <summary>
    /// Uploads an OFX/QFX file, parses transactions, detects duplicates, and stores them for review.
    /// </summary>
    /// <param name="file">The OFX or QFX file to upload.</param>
    /// <returns>Import result containing statistics and any parsing errors.</returns>
    [HttpPost("upload")]
    [ProducesResponseType(typeof(ImportReviewUploadDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [RequestSizeLimit(MaxFileSizeBytes)]
    public async Task<IActionResult> UploadFile(IFormFile file)
    {
        LogStartingFileName(file?.FileName ?? "none");

        // Validate file is provided
        if (file == null || file.Length == 0)
        {
            LogValidationError("File is required and cannot be empty");
            throw new ValidationException(nameof(file), "File is required and cannot be empty.");
        }

        // Validate file extension
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
        {
            LogValidationError($"Invalid file extension: {extension}");
            throw new ValidationException(nameof(file), $"Only {string.Join(", ", AllowedExtensions)} files are allowed.");
        }

        // Validate file size
        if (file.Length > MaxFileSizeBytes)
        {
            LogValidationError($"File size exceeds maximum: {file.Length} bytes");
            throw new ValidationException(nameof(file), $"File size must not exceed {MaxFileSizeBytes / (1024 * 1024)} MB.");
        }

        // Process the file
        using var stream = file.OpenReadStream();
        var result = await importReviewFeature.ImportFileAsync(stream, file.FileName);

        LogOkCount(result.ImportedCount);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves pending import review transactions for the current tenant with pagination support.
    /// </summary>
    /// <param name="pageNumber">The page number to retrieve (default: 1).</param>
    /// <param name="pageSize">The number of items per page (default: 50, max: 1000).</param>
    /// <returns>Paginated response containing transactions and pagination metadata.</returns>
    [HttpGet("review")]
    [ProducesResponseType(typeof(PaginatedResultDto<ImportReviewTransactionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingReview(
        [FromQuery] int? pageNumber = null,
        [FromQuery] int? pageSize = null)
    {
        LogStarting();

        var result = await importReviewFeature.GetPendingReviewAsync(pageNumber, pageSize);

        LogOkCount(result.Items.Count);
        return Ok(result);
    }

    /// <summary>
    /// Completes the import review by accepting selected transactions and deleting all pending review transactions.
    /// </summary>
    /// <param name="keys">The collection of transaction keys to accept (import into main transaction table).</param>
    /// <returns>Result indicating the number of transactions accepted and rejected.</returns>
    [HttpPost("review/complete")]
    [ProducesResponseType(typeof(ImportReviewCompleteDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CompleteReview([FromBody] IReadOnlyCollection<Guid> keys)
    {
        LogStartingCount(keys.Count);

        // Validate keys collection is provided
        if (keys == null || keys.Count == 0)
        {
            LogValidationError("At least one transaction key must be provided");
            throw new ValidationException(nameof(keys), "At least one transaction key must be provided.");
        }

        var result = await importReviewFeature.CompleteReviewAsync(keys);

        LogCompleteReview(result.AcceptedCount, result.RejectedCount);
        return Ok(result);
    }

    /// <summary>
    /// Deletes all pending import review transactions for the current tenant.
    /// </summary>
    [HttpDelete("review")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteAllPendingReview()
    {
        LogStarting();

        var deletedCount = await importReviewFeature.DeleteAllAsync();

        LogOkCount(deletedCount);
        return NoContent();
    }

    [LoggerMessage(1, LogLevel.Debug, "{Location}: Starting")]
    private partial void LogStarting([CallerMemberName] string? location = null);

    [LoggerMessage(2, LogLevel.Information, "{Location}: OK")]
    private partial void LogOk([CallerMemberName] string? location = null);

    [LoggerMessage(3, LogLevel.Information, "{Location}: OK {Count} items")]
    private partial void LogOkCount(int count, [CallerMemberName] string? location = null);

    [LoggerMessage(4, LogLevel.Debug, "{Location}: Starting {Count} items")]
    private partial void LogStartingCount(int count, [CallerMemberName] string? location = null);

    [LoggerMessage(5, LogLevel.Warning, "{Location}: Validation error {Message}")]
    private partial void LogValidationError(string message, [CallerMemberName] string? location = null);

    [LoggerMessage(6, LogLevel.Information, "{Location}: OK {AcceptedCount} accepted, {RejectedCount} rejected")]
    private partial void LogCompleteReview(int acceptedCount, int rejectedCount, [CallerMemberName] string? location = null);

    [LoggerMessage(7, LogLevel.Debug, "{Location}: Starting {FileName}")]
    private partial void LogStartingFileName(string fileName, [CallerMemberName] string? location = null);
}
