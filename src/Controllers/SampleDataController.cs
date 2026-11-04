using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Features;
using YoFi.V3.Controllers.Tenancy.Authorization;
using YoFi.V3.Entities.Tenancy.Models;

namespace YoFi.V3.Controllers;

/// <summary>Previews, loads, and downloads bundled sample data for a workspace Owner.</summary>
/// <param name="sampleDataFeature">Feature coordinating the sample-data use cases.</param>
/// <param name="logger">Logger for operation inputs, outcomes, and counts, never financial records.</param>
/// <remarks>
/// Every endpoint requires Owner access to the tenant identified by the route.
/// Tenant middleware establishes the context; application features resolve the current tenant.
/// Generation uses explicit date-only inputs and the current scenario revision.
/// Loading preserves existing data and intentionally adds another complete copy on repeat.
/// Workspace reset is a separate operation and is not exposed here.
/// </remarks>
[Route("api/tenant/{tenantKey:guid}/admin/sample-data")]
[ApiController]
[RequireTenantRole(TenantRole.Owner)]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public partial class SampleDataController(
    SampleDataFeature sampleDataFeature,
    ILogger<SampleDataController> logger) : ControllerBase
{
    /// <summary>Lists bundled scenarios, their revisions, and supported entity types.</summary>
    /// <param name="cancellationToken">Request cancellation token checked before the operation starts.</param>
    /// <returns>Scenario display metadata without server-calculated date defaults.</returns>
    [HttpGet("scenarios")]
    [ProducesResponseType(typeof(IReadOnlyCollection<SampleScenarioDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetScenarios(CancellationToken cancellationToken)
    {
        LogStarting();
        var succeeded = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = sampleDataFeature.GetScenarios();
            LogOk();
            succeeded = true;
            return Task.FromResult<IActionResult>(Ok(result));
        }
        finally
        {
            if (!succeeded)
            {
                LogFailed();
            }
        }
    }

    /// <summary>Previews exact sample counts and the workspace's current transaction count without writing.</summary>
    /// <param name="request">Scenario ID, current revision, and explicit inclusive calendar dates.</param>
    /// <param name="cancellationToken">Request cancellation token checked before the operation starts.</param>
    /// <returns>Validated inputs, exact dataset counts, and the workspace's unfiltered transaction count.</returns>
    [HttpPost("preview")]
    [ProducesResponseType(typeof(SampleDataPreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Preview(
        [FromBody] SampleDataRequest request,
        CancellationToken cancellationToken)
    {
        LogStarting(request.ScenarioId, request.Revision, request.StartDate, request.EndDate);
        var succeeded = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await sampleDataFeature.PreviewAsync(request);
            LogOk(request.ScenarioId, request.Revision, request.StartDate, request.EndDate,
                result.Counts.Transactions, result.Counts.PayeeMatchingRules);
            succeeded = true;
            return Ok(result);
        }
        finally
        {
            if (!succeeded)
            {
                LogFailed(request.ScenarioId, request.Revision, request.StartDate, request.EndDate);
            }
        }
    }

    /// <summary>Adds the whole sample dataset without clearing or updating existing workspace data.</summary>
    /// <param name="request">Scenario ID, current revision, and explicit inclusive calendar dates.</param>
    /// <param name="cancellationToken">Request cancellation token forwarded to the atomic persistence operation.</param>
    /// <returns>Validated inputs and committed per-type created counts; updated counts are always zero.</returns>
    /// <remarks>
    /// This command returns a summary, not a separately addressable resource.
    /// Repeating it creates duplicates intentionally. A disconnected request does not
    /// establish whether the write committed; clients must check before retrying.
    /// </remarks>
    [HttpPost("load")]
    [ProducesResponseType(typeof(SampleDataLoadResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Load(
        [FromBody] SampleDataRequest request,
        CancellationToken cancellationToken)
    {
        LogStarting(request.ScenarioId, request.Revision, request.StartDate, request.EndDate);
        var succeeded = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await sampleDataFeature.LoadAsync(request, cancellationToken);
            LogOk(request.ScenarioId, request.Revision, request.StartDate, request.EndDate,
                result.Created.Transactions, result.Created.PayeeMatchingRules);
            succeeded = true;
            return Ok(result);
        }
        finally
        {
            if (!succeeded)
            {
                LogFailed(request.ScenarioId, request.Revision, request.StartDate, request.EndDate);
            }
        }
    }

    /// <summary>Downloads matching sample OFX for upload through the normal bank-import workflow.</summary>
    /// <param name="request">Query-bound scenario ID, current revision, and explicit inclusive YYYY-MM-DD dates.</param>
    /// <param name="cancellationToken">Request cancellation token checked before generation starts.</param>
    /// <returns>An OFX attachment named for the scenario and requested dates, without category assignments.</returns>
    [HttpGet("ofx")]
    [Produces("application/x-ofx")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK, "application/x-ofx")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public Task<IActionResult> DownloadOfx(
        [FromQuery] SampleDataRequest request,
        CancellationToken cancellationToken)
    {
        LogStarting(request.ScenarioId, request.Revision, request.StartDate, request.EndDate);
        var succeeded = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var download = sampleDataFeature.DownloadOfx(request);
            LogOkDownload(request.ScenarioId, request.Revision, request.StartDate, request.EndDate);
            succeeded = true;
            return Task.FromResult<IActionResult>(File(download.Stream, download.ContentType, download.FileName));
        }
        finally
        {
            if (!succeeded)
            {
                LogFailed(request.ScenarioId, request.Revision, request.StartDate, request.EndDate);
            }
        }
    }

    [LoggerMessage(1, LogLevel.Debug, "{Location}: Starting Scenario={ScenarioId} Revision={Revision} Start={StartDate} End={EndDate}")]
    private partial void LogStarting(string? scenarioId, int revision, DateOnly? startDate, DateOnly? endDate,
        [CallerMemberName] string? location = null);

    [LoggerMessage(2, LogLevel.Information, "{Location}: OK Scenario={ScenarioId} Revision={Revision} Start={StartDate} End={EndDate} Transactions={Transactions} Rules={Rules}")]
    private partial void LogOk(string? scenarioId, int revision, DateOnly? startDate, DateOnly? endDate, int transactions, int rules,
        [CallerMemberName] string? location = null);

    [LoggerMessage(3, LogLevel.Warning, "{Location}: Failed Scenario={ScenarioId} Revision={Revision} Start={StartDate} End={EndDate}")]
    private partial void LogFailed(string? scenarioId, int revision, DateOnly? startDate, DateOnly? endDate,
        [CallerMemberName] string? location = null);

    [LoggerMessage(4, LogLevel.Debug, "{Location}: Starting")]
    private partial void LogStarting([CallerMemberName] string? location = null);

    [LoggerMessage(5, LogLevel.Information, "{Location}: OK")]
    private partial void LogOk([CallerMemberName] string? location = null);

    [LoggerMessage(6, LogLevel.Warning, "{Location}: Failed")]
    private partial void LogFailed([CallerMemberName] string? location = null);

    [LoggerMessage(7, LogLevel.Information, "{Location}: OK Scenario={ScenarioId} Revision={Revision} Start={StartDate} End={EndDate}")]
    private partial void LogOkDownload(string? scenarioId, int revision, DateOnly? startDate, DateOnly? endDate,
        [CallerMemberName] string? location = null);
}
