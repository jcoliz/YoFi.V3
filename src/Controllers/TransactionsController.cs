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

/// <summary>
/// Manages financial transactions within a tenant workspace.
/// </summary>
/// <param name="transactionsFeature">Feature providing transaction operations.</param>
/// <param name="logger">Logger for diagnostic output.</param>
/// <remarks>
/// All operations are scoped to a specific tenant identified by the tenantKey route parameter.
/// Users must have appropriate tenant roles (Viewer for reads, Editor for writes) to access endpoints.
/// </remarks>
[Route("api/tenant/{tenantKey:guid}/[controller]")]
[ApiController]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public partial class TransactionsController(TransactionsFeature transactionsFeature, ILogger<TransactionsController> logger) : ControllerBase
{
    /// <summary>
    /// Retrieves all transactions for the tenant, optionally filtered by date range.
    /// </summary>
    /// <param name="fromDate">The starting date for the date range filter (inclusive). If null, no lower bound is applied.</param>
    /// <param name="toDate">The ending date for the date range filter (inclusive). If null, no upper bound is applied.</param>
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

    /// <summary>
    /// Retrieves a specific transaction by its unique key.
    /// </summary>
    /// <param name="key">The unique identifier of the transaction.</param>
    /// <exception cref="TransactionNotFoundException">Thrown when the transaction is not found in the tenant.</exception>
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

    /// <summary>
    /// Creates a new transaction in the tenant workspace.
    /// </summary>
    /// <param name="tenantKey">The unique identifier of the tenant (from route).</param>
    /// <param name="transaction">The transaction data including date, amount, and payee.</param>
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

    /// <summary>
    /// Updates an existing transaction in the tenant workspace.
    /// </summary>
    /// <param name="key">The unique identifier of the transaction to update.</param>
    /// <param name="transaction">The updated transaction data including date, amount, and payee.</param>
    /// <exception cref="TransactionNotFoundException">Thrown when the transaction is not found in the tenant.</exception>
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

    /// <summary>
    /// Deletes a transaction from the tenant workspace.
    /// </summary>
    /// <param name="key">The unique identifier of the transaction to delete.</param>
    /// <exception cref="TransactionNotFoundException">Thrown when the transaction is not found in the tenant.</exception>
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
