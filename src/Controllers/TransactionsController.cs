using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Features;
using YoFi.V3.Application.Helpers;
using YoFi.V3.Controllers.Tenancy.Authorization;
using YoFi.V3.Entities.Exceptions;
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
public partial class TransactionsController(
    TransactionsFeature transactionsFeature,
    ILogger<TransactionsController> logger) : ControllerBase
{
    private const long MaxSplitWorkbookSizeBytes = 1 * 1024 * 1024;
    private const long SplitUploadRequestSizeBytes = 1 * 1024 * 1024 + 64 * 1024;

    /// <summary>
    /// Retrieves paginated transactions for the tenant, optionally filtered by search, dates, and advanced criteria.
    /// </summary>
    /// <param name="pageNumber">Page number to retrieve (1-based). If not specified, defaults to first page.</param>
    /// <param name="fromDate">The starting date for the date range filter (inclusive). If null, no lower bound is applied.</param>
    /// <param name="toDate">The ending date for the date range filter (inclusive). If null, no upper bound is applied.</param>
    /// <param name="search">Optional search term to filter transactions by text or amount.</param>
    /// <param name="payee">Optional literal substring filter for transaction payees.</param>
    /// <param name="category">Optional sanitized substring filter for any split category.</param>
    /// <param name="memo">Optional literal substring filter for transaction and split memos.</param>
    /// <param name="amount">Optional exact transaction amount filter, preserving explicit sign semantics.</param>
    /// <param name="uncategorizedOnly">Whether to include only transactions with an uncategorized split.</param>
    [HttpGet()]
    [RequireTenantRole(TenantRole.Viewer)]
    [ProducesResponseType(typeof(PaginatedResultDto<TransactionResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] int? pageNumber = null,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        [FromQuery] string? search = null,
        [FromQuery] string? payee = null,
        [FromQuery] string? category = null,
        [FromQuery] string? memo = null,
        [FromQuery] string? amount = null,
        [FromQuery] bool uncategorizedOnly = false)
    {
        LogStarting();

        var result = await transactionsFeature.GetTransactionsAsync(
            pageNumber, fromDate, toDate, search, payee, category, memo, amount, uncategorizedOnly);

        LogOkCount(result.Items.Count);
        return Ok(result);
    }

    /// <summary>
    /// Gets existing transaction category suggestions for the current tenant.
    /// </summary>
    /// <param name="term">The category prefix to match.</param>
    /// <param name="cancellationToken">Token to cancel the lookup.</param>
    /// <returns>Up to 20 matching category values.</returns>
    [HttpGet("categories")]
    [RequireTenantRole(TenantRole.Viewer)]
    [ProducesResponseType(typeof(IReadOnlyCollection<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetCategorySuggestions(
        [FromQuery] string? term,
        CancellationToken cancellationToken)
    {
        LogStarting();

        if (string.IsNullOrWhiteSpace(term))
        {
            throw new ValidationException(nameof(term), "A category search term is required.");
        }

        var sanitizedTerm = CategoryHelper.SanitizeCategory(term);
        if (sanitizedTerm.Length == 0)
        {
            throw new ValidationException(nameof(term), "A category search term is required.");
        }
        if (sanitizedTerm.Length > 200)
        {
            throw new ValidationException(nameof(term), "The category search term cannot exceed 200 characters.");
        }

        var suggestions = await transactionsFeature.GetCategorySuggestionsAsync(term, cancellationToken);

        LogOkCount(suggestions.Count);
        return Ok(suggestions);
    }

    /// <summary>
    /// Retrieves a specific transaction by its unique key.
    /// </summary>
    /// <param name="key">The unique identifier of the transaction.</param>
    /// <exception cref="YoFi.V3.Entities.Exceptions.TransactionNotFoundException">Thrown when the transaction is not found in the tenant.</exception>
    [HttpGet("{key:guid}")]
    [RequireTenantRole(TenantRole.Viewer)]
    [ProducesResponseType(typeof(TransactionDetailDto), StatusCodes.Status200OK)]
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
    [ProducesResponseType(typeof(TransactionDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateTransaction([FromRoute] Guid tenantKey, [FromBody] TransactionEditDto transaction)
    {
        LogStarting();

        var created = await transactionsFeature.AddTransactionAsync(transaction);

        LogOkKey(created.Key);
        return CreatedAtAction(nameof(GetTransactionById), new { tenantKey, key = created.Key }, created);
    }

    /// <summary>
    /// Adds a split to an existing transaction and adjusts its first split to keep the transaction balanced.
    /// </summary>
    /// <param name="key">The unique identifier of the transaction.</param>
    /// <param name="split">The split data to add.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <exception cref="YoFi.V3.Entities.Exceptions.TransactionNotFoundException">Thrown when the transaction is not found in the tenant.</exception>
    [HttpPost("{key:guid}/splits")]
    [RequireTenantRole(TenantRole.Editor)]
    [ProducesResponseType(typeof(TransactionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddSplit(Guid key, [FromBody] SplitEditDto split, CancellationToken cancellationToken)
    {
        LogStartingKey(key);

        var updated = await transactionsFeature.AddSplitAsync(key, split, cancellationToken);

        LogOkKey(key);
        return Ok(updated);
    }

    /// <summary>
    /// Updates a split and balances the transaction by adjusting the first other split.
    /// </summary>
    /// <param name="key">The unique identifier of the transaction.</param>
    /// <param name="splitKey">The unique identifier of the split within the transaction.</param>
    /// <param name="split">The updated amount, category, and memo.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    [HttpPut("{key:guid}/splits/{splitKey:guid}")]
    [RequireTenantRole(TenantRole.Editor)]
    [ProducesResponseType(typeof(TransactionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateSplit(
        Guid key, Guid splitKey, [FromBody] SplitEditDto split, CancellationToken cancellationToken)
    {
        LogStartingKeys(key, splitKey);

        var updated = await transactionsFeature.UpdateSplitAsync(key, splitKey, split, cancellationToken);

        LogOkKeys(key, splitKey);
        return Ok(updated);
    }

    /// <summary>
    /// Deletes a split and balances the transaction by adjusting the first surviving split.
    /// </summary>
    /// <param name="key">The unique identifier of the transaction.</param>
    /// <param name="splitKey">The unique identifier of the split within the transaction.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    [HttpDelete("{key:guid}/splits/{splitKey:guid}")]
    [RequireTenantRole(TenantRole.Editor)]
    [ProducesResponseType(typeof(TransactionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteSplit(Guid key, Guid splitKey, CancellationToken cancellationToken)
    {
        LogStartingKeys(key, splitKey);

        var updated = await transactionsFeature.DeleteSplitAsync(key, splitKey, cancellationToken);

        LogOkKeys(key, splitKey);
        return Ok(updated);
    }

    /// <summary>
    /// Updates an existing transaction and adjusts its first split to keep it balanced.
    /// </summary>
    /// <param name="key">The unique identifier of the transaction to update.</param>
    /// <param name="transaction">The updated transaction data including date, amount, and payee.</param>
    /// <exception cref="YoFi.V3.Entities.Exceptions.TransactionNotFoundException">Thrown when the transaction is not found in the tenant.</exception>
    [HttpPut("{key:guid}")]
    [RequireTenantRole(TenantRole.Editor)]
    [ProducesResponseType(typeof(TransactionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateTransaction(Guid key, [FromBody] TransactionEditDto transaction)
    {
        LogStartingKey(key);

        var updated = await transactionsFeature.UpdateTransactionAsync(key, transaction);

        LogOkKey(key);
        return Ok(updated);
    }

    /// <summary>
    /// Quick edit: updates only payee and memo, preserving all other transaction fields.
    /// </summary>
    /// <param name="key">The unique identifier of the transaction to update.</param>
    /// <param name="quickEdit">The updated payee and memo values.</param>
    /// <exception cref="YoFi.V3.Entities.Exceptions.TransactionNotFoundException">Thrown when the transaction is not found in the tenant.</exception>
    [HttpPatch("{key:guid}")]
    [RequireTenantRole(TenantRole.Editor)]
    [ProducesResponseType(typeof(TransactionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> QuickEditTransaction(Guid key, [FromBody] TransactionQuickEditDto quickEdit)
    {
        LogStartingKey(key);

        var updated = await transactionsFeature.QuickEditTransactionAsync(key, quickEdit);

        LogOkKey(key);
        return Ok(updated);
    }

    /// <summary>
    /// Deletes a transaction from the tenant workspace.
    /// </summary>
    /// <param name="key">The unique identifier of the transaction to delete.</param>
    /// <exception cref="YoFi.V3.Entities.Exceptions.TransactionNotFoundException">Thrown when the transaction is not found in the tenant.</exception>
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

    /// <summary>Uploads an XLSX workbook of splits and atomically appends them to a transaction.</summary>
    /// <param name="key">The tenant-scoped transaction identifier.</param>
    /// <param name="file">The uploaded XLSX workbook.</param>
    /// <param name="cancellationToken">Cancellation token for the request.</param>
    /// <returns>The committed transaction with ordered splits.</returns>
    /// <exception cref="ValidationException">The multipart file, workbook, or its rows are invalid.</exception>
    /// <exception cref="TransactionNotFoundException">The transaction is not found in the tenant.</exception>
    [HttpPost("{key:guid}/splits/upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(SplitUploadRequestSizeBytes)]
    [RequireTenantRole(TenantRole.Editor)]
    [ProducesResponseType(typeof(TransactionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HttpValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UploadSplits(
        Guid key,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        LogStartingKey(key);

        if (file is null || file.Length == 0)
        {
            throw new ValidationException(
                "file",
                "An XLSX workbook is required and cannot be empty.");
        }

        if (!string.Equals(
                Path.GetExtension(file.FileName),
                ".xlsx",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException(
                "file",
                "Only .xlsx workbooks are supported.");
        }

        if (file.Length > MaxSplitWorkbookSizeBytes)
        {
            throw new ValidationException(
                "file",
                "The workbook must not exceed 1 MB.");
        }

        await using var stream = new MemoryStream((int)file.Length);
        await file.CopyToAsync(stream, cancellationToken);
        var updated = await transactionsFeature.UploadSplitsAsync(
            key,
            stream.ToArray(),
            cancellationToken);
        LogOkKey(key);
        return Ok(updated);
    }

    /// <summary>
    /// Applies the current tenant's payee matching rules to one transaction.
    /// </summary>
    /// <param name="key">The tenant-scoped transaction identifier.</param>
    /// <param name="payeeMatchingRuleFeature">Feature providing payee matching rule operations.</param>
    /// <param name="cancellationToken">Token that cancels the operation.</param>
    /// <returns>The typed payee-rule application result.</returns>
    [HttpPost("{key:guid}/apply-payee-rules")]
    [RequireTenantRole(TenantRole.Editor)]
    [ProducesResponseType(typeof(PayeeRuleApplicationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApplyPayeeRules(
        Guid key,
        [FromServices] PayeeMatchingRuleFeature payeeMatchingRuleFeature,
        CancellationToken cancellationToken)
    {
        LogStartingKey(key);

        var result = await payeeMatchingRuleFeature
            .ApplyRulesToTransactionAsync(key, cancellationToken);

        LogOkKey(key);
        return Ok(result);
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

    [LoggerMessage(6, LogLevel.Debug, "{Location}: Starting {TransactionKey} {SplitKey}")]
    private partial void LogStartingKeys(Guid transactionKey, Guid splitKey, [CallerMemberName] string? location = null);

    [LoggerMessage(7, LogLevel.Information, "{Location}: OK {TransactionKey} {SplitKey}")]
    private partial void LogOkKeys(Guid transactionKey, Guid splitKey, [CallerMemberName] string? location = null);

}
