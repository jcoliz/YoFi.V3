using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Features;

namespace YoFi.V3.Controllers;

[Route("api/tenant/{tenantKey:guid}/[controller]")]
[ApiController]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public partial class TransactionsController(TransactionsFeature transactionsFeature, ILogger<TransactionsController> logger) : ControllerBase
{
    [HttpGet()]
    [ProducesResponseType(typeof(ICollection<TransactionResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTransactions()
    {
        LogStarting();

        var transactions = await transactionsFeature.GetTransactionsAsync();

        LogOkCount(transactions.Count);
        return Ok(transactions);
    }

    [HttpGet("{key:guid}")]
    [ProducesResponseType(typeof(TransactionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTransactionById(Guid key)
    {
        LogStartingKey(key);

        var transaction = await transactionsFeature.GetTransactionByKeyAsync(key);

        LogOkKey(key);
        return Ok(transaction);
    }

    [LoggerMessage(0, LogLevel.Debug, "{Location}: Starting")]
    private partial void LogStarting([CallerMemberName] string? location = null);

    [LoggerMessage(1, LogLevel.Information, "{Location}: OK")]
    private partial void LogOk([CallerMemberName] string? location = null);

    [LoggerMessage(2, LogLevel.Debug, "{Location}: Starting {Key}")]
    private partial void LogStartingKey(Guid key, [CallerMemberName] string? location = null);

    [LoggerMessage(3, LogLevel.Information, "{Location}: OK {Key}")]
    private partial void LogOkKey(Guid key, [CallerMemberName] string? location = null);

    [LoggerMessage(4, LogLevel.Information, "{Location}: OK {Count} items")]
    private partial void LogOkCount(int count, [CallerMemberName] string? location = null);
}
