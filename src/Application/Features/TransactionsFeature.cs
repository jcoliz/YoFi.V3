using System.Linq.Expressions;
using DotNetNuxt.Abstractions;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Helpers;
using YoFi.V3.Entities.Exceptions;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Tenancy.Models;
using YoFi.V3.Entities.Tenancy.Providers;

namespace YoFi.V3.Application.Features;

/// <summary>
/// Provides transaction management functionality for the current tenant.
/// </summary>
/// <param name="tenantProvider">Provider for accessing the current tenant context.</param>
/// <param name="dataProvider">Provider for data access operations.</param>
public class TransactionsFeature(ITenantProvider tenantProvider, IDataProvider dataProvider)
{
    private readonly Tenant _currentTenant = tenantProvider.CurrentTenant;

    /// <summary>
    /// Gets paginated transactions for the current tenant, optionally filtered by date range and search term.
    /// </summary>
    /// <param name="pageNumber">Page number to retrieve (1-based). If null, defaults to first page.</param>
    /// <param name="fromDate">Optional start date for filtering transactions (inclusive).</param>
    /// <param name="toDate">Optional end date for filtering transactions (inclusive).</param>
    /// <param name="search">Optional search term for transaction payee, memos, split categories, and amount.</param>
    /// <param name="payee">Optional literal substring filter for transaction payees.</param>
    /// <param name="category">Optional sanitized substring filter for split categories.</param>
    /// <param name="memo">Optional literal substring filter for transaction and split memos.</param>
    /// <param name="amount">Optional exact transaction amount filter.</param>
    /// <param name="uncategorizedOnly">Whether to include only transactions with an uncategorized split.</param>
    /// <returns>Paginated result containing transactions and pagination metadata.</returns>
    public async Task<PaginatedResultDto<TransactionResultDto>> GetTransactionsAsync(
        int? pageNumber = null,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        string? search = null,
        string? payee = null,
        string? category = null,
        string? memo = null,
        string? amount = null,
        bool uncategorizedOnly = false)
    {
        // Apply defaults
        var actualPageNumber = pageNumber ?? 1;

        var query = GetBaseTransactionQuery();
        query = TransactionSearchHelper.Apply(query, search);
        query = TransactionFilterHelper.Apply(
            query, fromDate, toDate, payee, category, memo, amount, uncategorizedOnly);

        // Get total count before pagination
        var totalCount = await dataProvider.CountAsync(query);

        // Apply pagination
        var actualPageSize = PaginationHelper.ItemsPerPage;
        var paginatedQuery = query
            .Skip((actualPageNumber - 1) * actualPageSize)
            .Take(actualPageSize);

        var dtoQuery = paginatedQuery.Select(ToResultDto);
        var items = await dataProvider.ToListNoTrackingAsync(dtoQuery);

        // Calculate pagination metadata
        var metadata = PaginationHelper.Calculate(actualPageNumber, totalCount);

        return new PaginatedResultDto<TransactionResultDto>(items, metadata);
    }

    /// <summary>
    /// Gets a specific transaction by its unique key with all fields.
    /// </summary>
    /// <param name="key">The unique identifier of the transaction.</param>
    /// <returns>The requested transaction with all fields.</returns>
    /// <exception cref="TransactionNotFoundException">Thrown when the transaction is not found.</exception>
    public async Task<TransactionDetailDto> GetTransactionByKeyAsync(Guid key)
    {
        var transaction = await GetTransactionByKeyInternalAsync(key);

        return ToDetailDto(transaction);
    }

    /// <summary>
    /// Adds a new transaction for the current tenant.
    /// </summary>
    /// <param name="transaction">The transaction data to add. MUST be validated before calling this method.
    /// See <see cref="YoFi.V3.Application.Validation.TransactionEditDtoValidator"/> for validation rules.</param>
    /// <returns>The created transaction with all fields.</returns>
    public async Task<TransactionDetailDto> AddTransactionAsync(TransactionEditDto transaction)
    {
        var newTransaction = ImportModelFactory.Transaction(transaction, _currentTenant.Id);

        dataProvider.Add(newTransaction);
        await dataProvider.SaveChangesAsync();

        return ToDetailDto(newTransaction);
    }

    /// <summary>
    /// Adds multiple transactions for the current tenant in a single batch operation.
    /// </summary>
    /// <param name="transactions">The collection of transaction data to add. Each MUST be validated before calling this method.
    /// See <see cref="YoFi.V3.Application.Validation.TransactionEditDtoValidator"/> for validation rules.</param>
    /// <returns>Collection of created transactions with all fields.</returns>
    public async Task<IReadOnlyCollection<TransactionDetailDto>> AddTransactionsAsync(IReadOnlyCollection<TransactionEditDto> transactions)
    {
        var newTransactions = new List<Transaction>();

        foreach (var transaction in transactions)
        {
            newTransactions.Add(ImportModelFactory.Transaction(transaction, _currentTenant.Id));
        }

        // Save to database to generate Keys and IDs
        dataProvider.AddRange(newTransactions);
        await dataProvider.SaveChangesAsync();

        // Now build result DTOs with generated Keys
        var results = newTransactions.Select(ToDetailDto).ToList();

        return results;
    }

    /// <summary>
    /// Adds one split and adjusts the first existing split so the transaction remains balanced.
    /// </summary>
    /// <param name="transactionKey">The unique identifier of the transaction.</param>
    /// <param name="split">The split data to add. MUST be validated before calling this method.</param>
    /// <returns>The updated transaction with all splits in display order.</returns>
    /// <exception cref="TransactionNotFoundException">Thrown when the transaction is not found.</exception>
    /// <exception cref="ValidationException">Thrown when the transaction has no existing split.</exception>
    public async Task<TransactionDetailDto> AddSplitAsync(Guid transactionKey, SplitEditDto split)
    {
        var transaction = await GetTransactionByKeyInternalAsync(transactionKey);
        var existingSplits = transaction.Splits.OrderBy(s => s.Order).ToList();

        if (existingSplits.Count == 0)
        {
            throw new ValidationException(
                nameof(transactionKey),
                "A split cannot be added to a transaction without an existing split.");
        }

        var firstSplit = existingSplits[0];
        var otherSplitTotal = existingSplits.Skip(1).Sum(s => s.Amount);
        firstSplit.Amount = transaction.Amount - otherSplitTotal - split.Amount;

        var newSplit = new Split
        {
            TransactionId = transaction.Id,
            Amount = split.Amount,
            Category = CategoryHelper.SanitizeCategory(split.Category),
            Memo = split.Memo,
            Order = checked(existingSplits[^1].Order + 1)
        };

        transaction.Splits.Add(newSplit);
        dataProvider.Add(newSplit);
        await dataProvider.SaveChangesAsync();

        return ToDetailDto(transaction);
    }

    /// <summary>
    /// Updates an existing transaction.
    /// </summary>
    /// <param name="key">The unique identifier of the transaction to update.</param>
    /// <param name="transaction">The updated transaction data. MUST be validated before calling this method.
    /// See <see cref="YoFi.V3.Application.Validation.TransactionEditDtoValidator"/> for validation rules.</param>
    /// <returns>The updated transaction with all fields.</returns>
    /// <exception cref="TransactionNotFoundException">Thrown when the transaction is not found.</exception>
    public async Task<TransactionDetailDto> UpdateTransactionAsync(Guid key, TransactionEditDto transaction)
    {
        var existingTransaction = await GetTransactionByKeyInternalAsync(key);

        // Alpha-1: Sanitize category
        var sanitizedCategory = CategoryHelper.SanitizeCategory(transaction.Category);

        // Update all properties (all fields are editable per Story 3)
        existingTransaction.Date = transaction.Date;
        existingTransaction.Amount = transaction.Amount;
        existingTransaction.Payee = transaction.Payee;
        existingTransaction.Memo = transaction.Memo;
        existingTransaction.Source = transaction.Source;
        existingTransaction.ExternalId = transaction.ExternalId;

        // Alpha-1: Update single split (Order = 0) - sync amount and category
        var split = existingTransaction.Splits.FirstOrDefault(s => s.Order == 0);
        if (split != null)
        {
            split.Amount = transaction.Amount;
            split.Category = sanitizedCategory;
        }

        dataProvider.UpdateRange([existingTransaction]);
        await dataProvider.SaveChangesAsync();

        return ToDetailDto(existingTransaction);
    }

    /// <summary>
    /// Quick edit: updates only payee, memo, and category, preserving all other transaction fields.
    /// </summary>
    /// <param name="key">The unique identifier of the transaction to update.</param>
    /// <param name="quickEdit">The updated payee, memo, and category values. MUST be validated before calling this method.
    /// See <see cref="YoFi.V3.Application.Validation.TransactionQuickEditDtoValidator"/> for validation rules.</param>
    /// <returns>The updated transaction with all fields.</returns>
    /// <exception cref="TransactionNotFoundException">Thrown when the transaction is not found.</exception>
    public async Task<TransactionDetailDto> QuickEditTransactionAsync(Guid key, TransactionQuickEditDto quickEdit)
    {
        var existingTransaction = await GetTransactionByKeyInternalAsync(key);

        if (existingTransaction.Splits.Count > 1 && quickEdit.Category != null)
        {
            throw new ValidationException(nameof(quickEdit.Category),
                "Category cannot be changed in Quick Edit for a transaction with multiple splits.");
        }

        var sanitizedCategory = CategoryHelper.SanitizeCategory(quickEdit.Category);

        // Update only payee, memo, and category, preserve all other fields (Date, Amount, Source, ExternalId)
        existingTransaction.Payee = quickEdit.Payee;
        existingTransaction.Memo = quickEdit.Memo;

        // Alpha-1: Update single split's category (Order = 0)
        var split = existingTransaction.Splits.FirstOrDefault(s => s.Order == 0);
        if (split != null && existingTransaction.Splits.Count <= 1)
        {
            split.Category = sanitizedCategory;
        }

        dataProvider.UpdateRange([existingTransaction]);
        await dataProvider.SaveChangesAsync();

        return ToDetailDto(existingTransaction);
    }

    /// <summary>
    /// Deletes a transaction.
    /// </summary>
    /// <param name="key">The unique identifier of the transaction to delete.</param>
    /// <exception cref="TransactionNotFoundException">Thrown when the transaction is not found.</exception>
    public async Task DeleteTransactionAsync(Guid key)
    {
        var transaction = await GetTransactionByKeyInternalAsync(key);
        dataProvider.Remove(transaction);
        await dataProvider.SaveChangesAsync();
    }

    /// <summary>
    /// Internal method to retrieve a transaction entity by its key.
    /// </summary>
    /// <param name="key">The unique identifier of the transaction.</param>
    /// <returns>The transaction entity.</returns>
    /// <exception cref="TransactionNotFoundException">Thrown when the transaction is not found.</exception>
    private async Task<Transaction> GetTransactionByKeyInternalAsync(Guid key)
    {
        var query = GetBaseTransactionQuery()
            .Where(t => t.Key == key);

        var result = await dataProvider.SingleOrDefaultAsync(query);

        if (result == null)
        {
            throw new TransactionNotFoundException(key);
        }

        return result;
    }

    /// <summary>
    /// Creates a base query for transactions filtered by the current tenant and ordered by date.
    /// </summary>
    /// <returns>A queryable of transactions for the current tenant, ordered by date descending, with splits loaded.</returns>
    private IQueryable<Transaction> GetBaseTransactionQuery()
    {
        return dataProvider.GetIncluding((Transaction t) => t.Splits)
            .Where(t => t.TenantId == _currentTenant.Id)
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.Id);
    }

    private static TransactionDetailDto ToDetailDto(Transaction transaction)
        => new(
            transaction.Key,
            transaction.Date,
            transaction.Amount,
            transaction.Payee,
            transaction.Memo,
            transaction.Source,
            transaction.ExternalId,
            transaction.Splits.Count > 1 ? string.Empty
                : transaction.Splits.FirstOrDefault(s => s.Order == 0)?.Category ?? string.Empty)
        {
            Splits = transaction.Splits.Count > 1 ? transaction.Splits.OrderBy(s => s.Order)
                .Select(s => new TransactionSplitDto(s.Key, s.Amount, s.Category, s.Memo, s.Order)).ToArray()
                : []
        };

    private static Expression<Func<Transaction, TransactionResultDto>> ToResultDto =>
        t => new TransactionResultDto(
            t.Key,
            t.Date,
            t.Amount,
            t.Payee,
            t.Memo,
            t.Splits.Count > 1 ? string.Empty
                : t.Splits.Where(s => s.Order == 0).Select(s => s.Category).FirstOrDefault() ?? string.Empty,
            t.Splits.Count > 1
        );
}
