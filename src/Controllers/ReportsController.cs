using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Features;
using YoFi.V3.Controllers.Tenancy.Authorization;
using YoFi.V3.Entities.Tenancy.Models;

namespace YoFi.V3.Controllers;

/// <summary>
/// Provides built-in reports for a tenant workspace.
/// </summary>
/// <param name="reportsFeature">Feature providing report definitions and report data.</param>
/// <param name="logger">Logger for diagnostic output.</param>
[Route("api/tenant/{tenantKey:guid}/reports")]
[ApiController]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public partial class ReportsController(
    ReportsFeature reportsFeature,
    ILogger<ReportsController> logger) : ControllerBase
{
    /// <summary>
    /// Lists the built-in report definitions available to the tenant.
    /// </summary>
    [HttpGet("available")]
    [RequireTenantRole(TenantRole.Viewer)]
    [ProducesResponseType(typeof(IReadOnlyList<ReportDefinitionDto>), StatusCodes.Status200OK)]
    public IActionResult GetAvailableReports()
    {
        LogStarting();

        var reports = reportsFeature.GetAvailableReports();

        LogOkCount(reports.Count);
        return Ok(reports);
    }

    /// <summary>
    /// Gets the selected built-in report for a calendar year.
    /// </summary>
    /// <param name="reportId">Stable identifier of a built-in report.</param>
    /// <param name="year">Calendar year to report, defaulting to the current UTC year.</param>
    /// <exception cref="KeyNotFoundException">The report identifier is not in the built-in catalog.</exception>
    /// <exception cref="YoFi.V3.Entities.Exceptions.ValidationException">The year is outside the supported calendar range.</exception>
    [HttpGet("{reportId}")]
    [RequireTenantRole(TenantRole.Viewer)]
    [ProducesResponseType(typeof(ReportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReport(
        string reportId,
        [FromQuery] int? year = null)
    {
        LogStartingReport(reportId);

        var result = await reportsFeature.GetReportAsync(reportId, year);

        LogOkReport(reportId, result.Rows.Count);
        return Ok(result);
    }

    [LoggerMessage(1, LogLevel.Debug, "{Location}: Starting")]
    private partial void LogStarting([CallerMemberName] string? location = null);

    [LoggerMessage(2, LogLevel.Information, "{Location}: OK {Count} items")]
    private partial void LogOkCount(int count, [CallerMemberName] string? location = null);

    [LoggerMessage(3, LogLevel.Debug, "{Location}: Starting Report {ReportId}")]
    private partial void LogStartingReport(string reportId, [CallerMemberName] string? location = null);
    [LoggerMessage(4, LogLevel.Information, "{Location}: OK Report {ReportId} {Count} rows")]
    private partial void LogOkReport(string reportId, int count, [CallerMemberName] string? location = null);
}
