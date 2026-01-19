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
/// Manages payee matching rules within a tenant workspace.
/// </summary>
/// <param name="payeeMatchingRuleFeature">Feature providing payee matching rule operations.</param>
/// <param name="logger">Logger for diagnostic output.</param>
/// <remarks>
/// All operations are scoped to a specific tenant identified by the tenantKey route parameter.
/// Users must have Viewer role for read operations and Editor role for write operations.
/// </remarks>
[Route("api/tenant/{tenantKey:guid}/payee-rules")]
[ApiController]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public partial class PayeeMatchingRulesController(
    PayeeMatchingRuleFeature payeeMatchingRuleFeature,
    ILogger<PayeeMatchingRulesController> logger) : ControllerBase
{
    /// <summary>
    /// Retrieves all payee matching rules for the tenant with optional pagination, sorting, and search.
    /// </summary>
    /// <param name="pageNumber">Page number to retrieve (default: 1).</param>
    /// <param name="sortBy">Sort order (default: PayeePattern). Valid values: PayeePattern, Category, LastUsedAt.</param>
    /// <param name="searchText">Optional plain text search across PayeePattern and Category (case-insensitive).</param>
    [HttpGet()]
    [RequireTenantRole(TenantRole.Viewer)]
    [ProducesResponseType(typeof(PaginatedResultDto<PayeeMatchingRuleResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRules(
        [FromQuery] int? pageNumber = null,
        [FromQuery] PayeeRuleSortBy? sortBy = null,
        [FromQuery] string? searchText = null)
    {
        LogStarting();

        var rules = await payeeMatchingRuleFeature.GetRulesAsync(
            pageNumber ?? 1,
            sortBy ?? PayeeRuleSortBy.PayeePattern,
            searchText);

        LogOkCount(rules.Items.Count);
        return Ok(rules);
    }

    /// <summary>
    /// Retrieves a specific payee matching rule by its unique key.
    /// </summary>
    /// <param name="key">The unique identifier of the rule.</param>
    /// <exception cref="YoFi.V3.Entities.Exceptions.PayeeMatchingRuleNotFoundException">Thrown when the rule is not found in the tenant.</exception>
    [HttpGet("{key:guid}")]
    [RequireTenantRole(TenantRole.Viewer)]
    [ProducesResponseType(typeof(PayeeMatchingRuleResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRuleById(Guid key)
    {
        LogStartingKey(key);

        var rule = await payeeMatchingRuleFeature.GetRuleByKeyAsync(key);

        LogOkKey(key);
        return Ok(rule);
    }

    /// <summary>
    /// Creates a new payee matching rule in the tenant workspace.
    /// </summary>
    /// <param name="tenantKey">The unique identifier of the tenant (from route).</param>
    /// <param name="rule">The rule data including payee pattern, regex flag, and category.</param>
    [HttpPost()]
    [RequireTenantRole(TenantRole.Editor)]
    [ProducesResponseType(typeof(PayeeMatchingRuleResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateRule([FromRoute] Guid tenantKey, [FromBody] PayeeMatchingRuleEditDto rule)
    {
        LogStarting();

        var created = await payeeMatchingRuleFeature.CreateRuleAsync(rule);

        LogOkKey(created.Key);
        return CreatedAtAction(nameof(GetRuleById), new { tenantKey, key = created.Key }, created);
    }

    /// <summary>
    /// Updates an existing payee matching rule in the tenant workspace.
    /// </summary>
    /// <param name="key">The unique identifier of the rule to update.</param>
    /// <param name="rule">The updated rule data including payee pattern, regex flag, and category.</param>
    /// <exception cref="YoFi.V3.Entities.Exceptions.PayeeMatchingRuleNotFoundException">Thrown when the rule is not found in the tenant.</exception>
    [HttpPut("{key:guid}")]
    [RequireTenantRole(TenantRole.Editor)]
    [ProducesResponseType(typeof(PayeeMatchingRuleResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRule(Guid key, [FromBody] PayeeMatchingRuleEditDto rule)
    {
        LogStartingKey(key);

        var updated = await payeeMatchingRuleFeature.UpdateRuleAsync(key, rule);

        LogOkKey(key);
        return Ok(updated);
    }

    /// <summary>
    /// Deletes a payee matching rule from the tenant workspace.
    /// </summary>
    /// <param name="key">The unique identifier of the rule to delete.</param>
    /// <exception cref="YoFi.V3.Entities.Exceptions.PayeeMatchingRuleNotFoundException">Thrown when the rule is not found in the tenant.</exception>
    [HttpDelete("{key:guid}")]
    [RequireTenantRole(TenantRole.Editor)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteRule(Guid key)
    {
        LogStartingKey(key);

        await payeeMatchingRuleFeature.DeleteRuleAsync(key);

        LogOkKey(key);
        return NoContent();
    }

    [LoggerMessage(1, LogLevel.Debug, "{Location}: Starting")]
    private partial void LogStarting([CallerMemberName] string? location = null);

    [LoggerMessage(2, LogLevel.Debug, "{Location}: Starting {Key}")]
    private partial void LogStartingKey(Guid key, [CallerMemberName] string? location = null);

    [LoggerMessage(3, LogLevel.Information, "{Location}: OK {Key}")]
    private partial void LogOkKey(Guid key, [CallerMemberName] string? location = null);

    [LoggerMessage(4, LogLevel.Information, "{Location}: OK {Count} items")]
    private partial void LogOkCount(int count, [CallerMemberName] string? location = null);
}
