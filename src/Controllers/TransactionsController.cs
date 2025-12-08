using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Features;
using YoFi.V3.Entities.Exceptions;

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
        LogFetchingAllTransactions();

        var transactions = await transactionsFeature.GetTransactionsAsync();

        LogSuccessfullyFetchedAllTransactions();
        return Ok(transactions);
    }

    [HttpGet("{key:guid}")]
    [ProducesResponseType(typeof(TransactionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTransactionById(Guid key)
    {
        LogFetchingTransactionById(key);

        try
        {
            var transaction = await transactionsFeature.GetTransactionByKeyAsync(key);

            LogSuccessfullyFetchedTransactionById(key);
            return Ok(transaction);
        }
        catch (TransactionNotFoundException ex)
        {
            LogTransactionNotFound(key);
            return NotFound(new ProblemDetails
            {
                Title = "Transaction Not Found",
                Detail = ex.Message,
                Status = StatusCodes.Status404NotFound
            });
        }
        catch (Exception ex)
        {
            LogErrorFetchingTransactionById(ex, key);
            throw;
        }
    }

    [LoggerMessage(0, LogLevel.Debug, "GetTransactions: Fetching all transactions")]
    private partial void LogFetchingAllTransactions();

    [LoggerMessage(1, LogLevel.Information, "GetTransactions: Successfully fetched all transactions")]
    private partial void LogSuccessfullyFetchedAllTransactions();

    [LoggerMessage(2, LogLevel.Debug, "GetTransactionById: Fetching transaction {TransactionId}")]
    private partial void LogFetchingTransactionById(Guid transactionId);

    [LoggerMessage(3, LogLevel.Information, "GetTransactionById: Successfully fetched transaction {TransactionId}")]
    private partial void LogSuccessfullyFetchedTransactionById(Guid transactionId);

    [LoggerMessage(4, LogLevel.Warning, "GetTransactionById: Transaction not found {TransactionId}")]
    private partial void LogTransactionNotFound(Guid transactionId);

    [LoggerMessage(5, LogLevel.Error, "GetTransactionById: Failed fetching transaction {TransactionId}")]
    private partial void LogErrorFetchingTransactionById(Exception ex, Guid transactionId);
}
