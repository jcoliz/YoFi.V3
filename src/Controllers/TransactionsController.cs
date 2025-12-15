using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Features;
using YoFi.V3.Controllers.Tenancy.Authorization;
using YoFi.V3.Entities.Tenancy.Models;

namespace YoFi.V3.Controllers;

[Route("api/tenant/{tenantKey:guid}/[controller]")]
[ApiController]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public partial class TransactionsController(TransactionsFeature transactionsFeature, ILogger<TransactionsController> logger) : ControllerBase
{
    [HttpGet()]
    [RequireTenantRole(TenantRole.Viewer)]
    [ProducesResponseType(typeof(IReadOnlyCollection<TransactionResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTransactions([FromQuery] DateOnly? fromDate = null, [FromQuery] DateOnly? toDate = null)
    {
        LogStarting();

        var transactions = await transactionsFeature.GetTransactionsAsync(fromDate, toDate);

        LogOkCount(transactions.Count);
        return Ok(transactions);
    }

    [HttpGet("{key:guid}")]
    [RequireTenantRole(TenantRole.Viewer)]
    [ProducesResponseType(typeof(TransactionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTransactionById(Guid key)
    {
        LogStartingKey(key);

        var transaction = await transactionsFeature.GetTransactionByKeyAsync(key);

        LogOkKey(key);
        return Ok(transaction);
    }

    [HttpPost()]
    [RequireTenantRole(TenantRole.Editor)]
    [ProducesResponseType(typeof(TransactionResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateTransaction([FromRoute] Guid tenantKey, [FromBody] TransactionEditDto transaction)
    {
        LogStarting();

        var created = await transactionsFeature.AddTransactionAsync(transaction);

        LogOkKey(created.Key);
        return CreatedAtAction(nameof(GetTransactionById), new { tenantKey, key = created.Key }, created);
    }

    [HttpPut("{key:guid}")]
    [RequireTenantRole(TenantRole.Editor)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTransaction(Guid key, [FromBody] TransactionEditDto transaction)
    {
        LogStartingKey(key);

        await transactionsFeature.UpdateTransactionAsync(key, transaction);

        LogOkKey(key);
        return NoContent();
    }

    [HttpDelete("{key:guid}")]
    [RequireTenantRole(TenantRole.Editor)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTransaction(Guid key)
    {
        LogStartingKey(key);

        await transactionsFeature.DeleteTransactionAsync(key);

        LogOkKey(key);
        return NoContent();
    }

    [LoggerMessage(1, LogLevel.Debug, "{Location}: Starting")]
    private partial void LogStarting([CallerMemberName] string? location = null);

    [LoggerMessage(2, LogLevel.Information, "{Location}: OK")]
    private partial void LogOk([CallerMemberName] string? location = null);

    [LoggerMessage(3, LogLevel.Debug, "{Location}: Starting {Key}")]
    private partial void LogStartingKey(Guid key, [CallerMemberName] string? location = null);

    [LoggerMessage(4, LogLevel.Information, "{Location}: OK {Key}")]
    private partial void LogOkKey(Guid key, [CallerMemberName] string? location = null);

    [LoggerMessage(5, LogLevel.Information, "{Location}: OK {Count} items")]
    private partial void LogOkCount(int count, [CallerMemberName] string? location = null);
}
