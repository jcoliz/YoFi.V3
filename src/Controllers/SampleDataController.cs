using System.Runtime.CompilerServices;
using System.Security.Claims;
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
    /// <param name="tenantKey">Workspace key authorized by the tenant Owner policy.</param>
    /// <param name="cancellationToken">Request cancellation token checked before the operation starts.</param>
    /// <returns>Scenario display metadata without server-calculated date defaults.</returns>
    [HttpGet("scenarios")]
    [ProducesResponseType(typeof(IReadOnlyCollection<SampleScenarioDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetScenarios([FromRoute] Guid tenantKey, CancellationToken cancellationToken)
        => ExecuteAsync(tenantKey, null, cancellationToken,
            () => Task.FromResult<IActionResult>(Ok(sampleDataFeature.GetScenarios())));

    /// <summary>Previews exact sample counts and the workspace's current transaction count without writing.</summary>
    /// <param name="tenantKey">Workspace key authorized by the tenant Owner policy.</param>
    /// <param name="request">Scenario ID, current revision, and explicit inclusive calendar dates.</param>
    /// <param name="cancellationToken">Request cancellation token checked before the operation starts.</param>
    /// <returns>Validated inputs, exact dataset counts, and the workspace's unfiltered transaction count.</returns>
    [HttpPost("preview")]
    [ProducesResponseType(typeof(SampleDataPreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public Task<IActionResult> Preview(
        [FromRoute] Guid tenantKey,
        [FromBody] SampleDataRequest request,
        CancellationToken cancellationToken)
        => ExecuteAsync(tenantKey, request, cancellationToken,
            async () => Ok(await sampleDataFeature.PreviewAsync(request)));

    /// <summary>Adds the whole sample dataset without clearing or updating existing workspace data.</summary>
    /// <param name="tenantKey">Workspace key authorized by the tenant Owner policy.</param>
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
    public Task<IActionResult> Load(
        [FromRoute] Guid tenantKey,
        [FromBody] SampleDataRequest request,
        CancellationToken cancellationToken)
        => ExecuteAsync(tenantKey, request, cancellationToken,
            async () => Ok(await sampleDataFeature.LoadAsync(request, cancellationToken)));

    /// <summary>Downloads matching sample OFX for upload through the normal bank-import workflow.</summary>
    /// <param name="tenantKey">Workspace key authorized by the tenant Owner policy.</param>
    /// <param name="request">Query-bound scenario ID, current revision, and explicit inclusive YYYY-MM-DD dates.</param>
    /// <param name="cancellationToken">Request cancellation token checked before generation starts.</param>
    /// <returns>An OFX attachment named for the scenario and requested dates, without category assignments.</returns>
    [HttpGet("ofx")]
    [Produces("application/x-ofx")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK, "application/x-ofx")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public Task<IActionResult> DownloadOfx(
        [FromRoute] Guid tenantKey,
        [FromQuery] SampleDataRequest request,
        CancellationToken cancellationToken)
        => ExecuteAsync(tenantKey, request, cancellationToken, () =>
        {
            var download = sampleDataFeature.DownloadOfx(request);
            return Task.FromResult<IActionResult>(File(download.Stream, download.ContentType, download.FileName));
        });

    private async Task<IActionResult> ExecuteAsync(
        Guid tenantKey,
        SampleDataRequest? request,
        CancellationToken cancellationToken,
        Func<Task<IActionResult>> action,
        [CallerMemberName] string operation = "")
    {
        var actor = User.FindFirstValue(ClaimTypes.NameIdentifier);
        LogStarting(operation, actor, tenantKey, request?.ScenarioId, request?.Revision, request?.StartDate, request?.EndDate);
        IActionResult? result = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            result = await action();
            return result;
        }
        finally
        {
            if (result == null)
            {
                LogFailed(operation, actor, tenantKey, request?.ScenarioId, request?.Revision, request?.StartDate, request?.EndDate);
            }
            else
            {
                var counts = result is ObjectResult { Value: SampleDataPreviewDto preview } ? preview.Counts
                    : result is ObjectResult { Value: SampleDataLoadResultDto loaded } ? loaded.Created : null;
                LogOk(operation, actor, tenantKey, request?.ScenarioId, request?.Revision, request?.StartDate, request?.EndDate,
                    counts?.Transactions, counts?.PayeeMatchingRules);
            }
        }
    }

    [LoggerMessage(1, LogLevel.Debug, "{Operation}: Starting Actor={Actor} Tenant={TenantKey} Scenario={ScenarioId} Revision={Revision} Start={StartDate} End={EndDate}")]
    private partial void LogStarting(string operation, string? actor, Guid tenantKey, string? scenarioId, int? revision, DateOnly? startDate, DateOnly? endDate);

    [LoggerMessage(2, LogLevel.Information, "{Operation}: OK Actor={Actor} Tenant={TenantKey} Scenario={ScenarioId} Revision={Revision} Start={StartDate} End={EndDate} Transactions={Transactions} Rules={Rules}")]
    private partial void LogOk(string operation, string? actor, Guid tenantKey, string? scenarioId, int? revision, DateOnly? startDate, DateOnly? endDate, int? transactions, int? rules);

    [LoggerMessage(3, LogLevel.Warning, "{Operation}: Failed Actor={Actor} Tenant={TenantKey} Scenario={ScenarioId} Revision={Revision} Start={StartDate} End={EndDate}")]
    private partial void LogFailed(string operation, string? actor, Guid tenantKey, string? scenarioId, int? revision, DateOnly? startDate, DateOnly? endDate);
}
