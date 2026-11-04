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

/// <summary>Deletes existing tenant data for a workspace Owner.</summary>
/// <param name="tenantDataAdminFeature">Feature coordinating workspace-data deletion.</param>
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
    ILogger<TenantDataAdminController> logger) : ControllerBase
{
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
        var actor = User.FindFirstValue(ClaimTypes.NameIdentifier);
        LogStarting(tenantKey, actor);
        var succeeded = false;
        try
        {
            var result = await tenantDataAdminFeature.DeleteAllAsync(request, cancellationToken);
            LogOk(tenantKey, actor, result.Deleted.Transactions, result.Deleted.Splits,
                result.Deleted.PayeeMatchingRules, result.Deleted.ImportReviewTransactions);
            succeeded = true;
            return Ok(result);
        }
        finally
        {
            if (!succeeded)
            {
                LogFailed(tenantKey, actor);
            }
        }
    }

    [LoggerMessage(1, LogLevel.Debug, "{Location}: Starting Tenant={TenantKey} Actor={Actor}")]
    private partial void LogStarting(Guid tenantKey, string? actor, [CallerMemberName] string? location = null);

    [LoggerMessage(2, LogLevel.Information, "{Location}: OK Tenant={TenantKey} Actor={Actor} Transactions={Transactions} Splits={Splits} Rules={Rules} ImportReviews={ImportReviews}")]
    private partial void LogOk(Guid tenantKey, string? actor, int transactions, int splits, int rules,
        int importReviews, [CallerMemberName] string? location = null);

    [LoggerMessage(3, LogLevel.Warning, "{Location}: Failed Tenant={TenantKey} Actor={Actor}")]
    private partial void LogFailed(Guid tenantKey, string? actor, [CallerMemberName] string? location = null);
}
