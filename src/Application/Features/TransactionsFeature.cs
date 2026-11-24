using System.Data;
using System.Linq.Expressions;
using DotNetNuxt.Abstractions;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Helpers;
using YoFi.V3.Application.Services;
using YoFi.V3.Application.Validation;
using YoFi.V3.Entities.Exceptions;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Tenancy.Models;
using YoFi.V3.Entities.Tenancy.Providers;

namespace YoFi.V3.Application.Features;

/// <summary>
/// Provides transaction management functionality for the current tenant.
/// </summary>
public class TransactionsFeature
{
    private readonly Tenant _currentTenant;
    private readonly IDataProvider dataProvider;
    private readonly TransactionSplitWorkbookService _workbookService;

    /// <summary>Initializes a transactions feature with the default workbook service.</summary>
    /// <param name="tenantProvider">Provider for accessing the current tenant context.</param>
    /// <param name="dataProvider">Provider for data access operations.</param>
    public TransactionsFeature(
        ITenantProvider tenantProvider,
        IDataProvider dataProvider)
        : this(tenantProvider, dataProvider, new TransactionSplitWorkbookService())
    {
    }

    /// <summary>Initializes a transactions feature with a workbook service.</summary>
    /// <param name="tenantProvider">Provider for accessing the current tenant context.</param>
    /// <param name="dataProvider">Provider for data access operations.</param>
    /// <param name="workbookService">Workbook parser and template service.</param>
    public TransactionsFeature(
        ITenantProvider tenantProvider,
        IDataProvider dataProvider,
        TransactionSplitWorkbookService workbookService)
    {
        ArgumentNullException.ThrowIfNull(workbookService);
        _currentTenant = tenantProvider.CurrentTenant;
        this.dataProvider = dataProvider;
        _workbookService = workbookService;
    }

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
    /// Gets case-insensitive prefix suggestions for categories persisted in the current tenant.
    /// </summary>
    /// <param name="term">The category text entered by the user.</param>
    /// <param name="cancellationToken">Token to cancel the lookup.</param>
    /// <returns>
    /// Up to 20 distinct existing category values matching the sanitized term, ordered case-insensitively.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the term is null, whitespace, sanitizes to an empty value, or exceeds 200 characters.
    /// </exception>
    /// <exception cref="OperationCanceledException">Thrown when cancellation is requested.</exception>
    public async Task<IReadOnlyCollection<string>> GetCategorySuggestionsAsync(
        string term,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(term);

        var sanitizedTerm = CategoryHelper.SanitizeCategory(term);
        if (sanitizedTerm.Length == 0)
        {
            throw new ArgumentException(
                "Category suggestion term cannot be empty after sanitization.",
                nameof(term));
        }
        if (sanitizedTerm.Length > 200)
        {
            throw new ArgumentException(
                "Category suggestion term cannot exceed 200 characters.",
                nameof(term));
        }

        var normalizedTerm = sanitizedTerm.ToLowerInvariant();
        var tenantId = _currentTenant.Id;
        var query = dataProvider.Get<Split>()
            .Where(split => split.Category != string.Empty
                && split.Category.ToLower().Contains(normalizedTerm)
                && dataProvider.Get<Transaction>().Any(transaction =>
                    transaction.Id == split.TransactionId
                    && transaction.TenantId == tenantId))
            .Select(split => split.Category)
            .Distinct();

        var matches = await dataProvider.ToListNoTrackingAsync(query);
        cancellationToken.ThrowIfCancellationRequested();

        return matches
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(category => category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(category => category, StringComparer.Ordinal)
            .Take(20)
            .ToArray();
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

    /// <summary>Validates, appends, and balances transaction splits from an uploaded XLSX workbook.</summary>
    /// <param name="transactionKey">Tenant-scoped transaction key.</param>
    /// <param name="workbook">Exact uploaded XLSX bytes.</param>
    /// <param name="cancellationToken">Cancellation token for parsing and persistence.</param>
    /// <returns>The committed, ordered transaction detail.</returns>
    /// <exception cref="ValidationException">The workbook, splits, or calculated balance are invalid.</exception>
    /// <exception cref="TransactionNotFoundException">The transaction is not in the current tenant.</exception>
    /// <exception cref="DataConflictException">A concurrent change prevented the serializable update.</exception>
    public Task<TransactionDetailDto> UploadSplitsAsync(
        Guid transactionKey,
        byte[] workbook,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workbook);

        return dataProvider.ExecuteInTransactionAsync(async token =>
        {
            token.ThrowIfCancellationRequested();
            var transaction = await GetTransactionByKeyInternalAsync(transactionKey);
            var uploadedRows = _workbookService.Parse(workbook, transaction.Memo);
            var existingSplits = transaction.Splits.OrderBy(split => split.Order).ToList();

            if (existingSplits.Count == 0)
            {
                throw new ValidationException(
                    nameof(transactionKey),
                    "A split upload cannot be applied to a transaction without an existing split.");
            }

            var firstSplit = existingSplits[0];
            var importedTotal = uploadedRows.Sum(row => row.Amount);
            var balancingAmount = transaction.Amount
                - existingSplits.Skip(1).Sum(split => split.Amount)
                - importedTotal;

            var balancingValidation = new SplitEditDtoValidator().Validate(
                new SplitEditDto(balancingAmount, firstSplit.Category, firstSplit.Memo));
            if (!balancingValidation.IsValid)
            {
                var failure = balancingValidation.Errors.First();
                throw new ValidationException(failure.PropertyName, failure.ErrorMessage);
            }

            var maximumOrder = existingSplits.Count == 0
                ? -1
                : existingSplits.Max(split => split.Order);
            var importedOrder = maximumOrder + 1;

            foreach (var row in uploadedRows)
            {
                var newSplit = new Split
                {
                    TransactionId = transaction.Id,
                    Amount = row.Amount,
                    Category = row.Category,
                    Memo = row.Memo,
                    Order = importedOrder++
                };
                transaction.Splits.Add(newSplit);
                dataProvider.Add(newSplit);
            }

            if (balancingAmount == 0m)
            {
                transaction.Splits.Remove(firstSplit);
                dataProvider.Remove(firstSplit);

                var survivingSplits = transaction.Splits
                    .OrderBy(split => split.Order)
                    .ToList();
                for (var order = 0; order < survivingSplits.Count; order++)
                {
                    survivingSplits[order].Order = order;
                }
            }
            else
            {
                firstSplit.Amount = balancingAmount;
            }

            await dataProvider.SaveChangesAsync(token);
            return ToDetailDto(transaction);
        }, cancellationToken, isolationLevel: IsolationLevel.Serializable);
    }

    /// <summary>
    /// Adds one split and adjusts the first existing split so the transaction remains balanced.
    /// </summary>
    /// <param name="transactionKey">The unique identifier of the transaction.</param>
    /// <param name="split">The split data to add. MUST be validated before calling this method.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The updated transaction with all splits in display order.</returns>
    /// <exception cref="TransactionNotFoundException">Thrown when the transaction is not found.</exception>
    /// <exception cref="ValidationException">Thrown when the transaction has no existing split.</exception>
    /// <exception cref="DataConflictException">A concurrent change prevented the addition.</exception>
    public Task<TransactionDetailDto> AddSplitAsync(
        Guid transactionKey, SplitEditDto split, CancellationToken cancellationToken = default)
        => dataProvider.ExecuteInTransactionAsync(async token =>
        {
            token.ThrowIfCancellationRequested();
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
            await dataProvider.SaveChangesAsync(token);

            return ToDetailDto(transaction);
        }, cancellationToken, isolationLevel: IsolationLevel.Serializable);

    /// <summary>
    /// Updates a split and adjusts the first other split to balance the transaction.
    /// </summary>
    /// <param name="transactionKey">The unique identifier of the transaction.</param>
    /// <param name="splitKey">The unique identifier of the split to update.</param>
    /// <param name="split">The split data. MUST be validated before calling this method.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The updated transaction with all splits in display order.</returns>
    /// <exception cref="TransactionNotFoundException">The transaction is not found in the tenant.</exception>
    /// <exception cref="SplitNotFoundException">The split is not found in the transaction.</exception>
    /// <exception cref="ValidationException">The transaction has no distinct balancing split.</exception>
    /// <exception cref="DataConflictException">A concurrent change prevented the update.</exception>
    public Task<TransactionDetailDto> UpdateSplitAsync(
        Guid transactionKey,
        Guid splitKey,
        SplitEditDto split,
        CancellationToken cancellationToken = default)
        => dataProvider.ExecuteInTransactionAsync(async token =>
        {
            token.ThrowIfCancellationRequested();
            var transaction = await GetTransactionByKeyInternalAsync(transactionKey);
            var existingSplits = transaction.Splits.OrderBy(s => s.Order).ToList();
            var editedSplit = existingSplits.SingleOrDefault(s => s.Key == splitKey)
                ?? throw new SplitNotFoundException(splitKey);
            var balancingSplit = existingSplits.FirstOrDefault(s => s.Key != splitKey)
                ?? throw new ValidationException(nameof(splitKey),
                    "A split can only be edited on a transaction with multiple splits.");

            var otherSplitTotal = existingSplits
                .Where(s => s.Key != splitKey && s.Key != balancingSplit.Key)
                .Sum(s => s.Amount);
            balancingSplit.Amount = transaction.Amount - otherSplitTotal - split.Amount;
            editedSplit.Amount = split.Amount;
            editedSplit.Category = CategoryHelper.SanitizeCategory(split.Category);
            editedSplit.Memo = split.Memo;

            await dataProvider.SaveChangesAsync(token);
            return ToDetailDto(transaction);
        }, cancellationToken, isolationLevel: IsolationLevel.Serializable);

    /// <summary>
    /// Deletes a split and balances the first surviving split, preserving categories and memos.
    /// </summary>
    /// <param name="transactionKey">The unique identifier of the transaction.</param>
    /// <param name="splitKey">The unique identifier of the split to delete.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>The updated transaction with surviving splits in normalized display order.</returns>
    /// <exception cref="TransactionNotFoundException">The transaction is not found in the tenant.</exception>
    /// <exception cref="SplitNotFoundException">The split is not found in the transaction.</exception>
    /// <exception cref="ValidationException">The requested split is the last allocation.</exception>
    /// <exception cref="DataConflictException">A concurrent change prevented the deletion.</exception>
    public Task<TransactionDetailDto> DeleteSplitAsync(
        Guid transactionKey, Guid splitKey, CancellationToken cancellationToken = default)
        => dataProvider.ExecuteInTransactionAsync(async token =>
        {
            token.ThrowIfCancellationRequested();
            var transaction = await GetTransactionByKeyInternalAsync(transactionKey);
            var splits = transaction.Splits.OrderBy(s => s.Order).ToList();
            var deletedSplit = splits.SingleOrDefault(s => s.Key == splitKey)
                ?? throw new SplitNotFoundException(splitKey);
            if (splits.Count == 1)
            {
                throw new ValidationException(nameof(splitKey),
                    "The last split cannot be deleted. A transaction must have at least one split.");
            }

            splits.Remove(deletedSplit);
            dataProvider.Remove(deletedSplit);
            transaction.Splits.Remove(deletedSplit);
            splits[0].Amount = transaction.Amount - splits.Skip(1).Sum(s => s.Amount);
            for (var order = 0; order < splits.Count; order++)
            {
                splits[order].Order = order;
            }

            await dataProvider.SaveChangesAsync(token);
            return ToDetailDto(transaction);
        }, cancellationToken, isolationLevel: IsolationLevel.Serializable);

    /// <summary>
    /// Updates an existing transaction.
    /// </summary>
    /// <param name="key">The unique identifier of the transaction to update.</param>
    /// <param name="transaction">The updated transaction data. MUST be validated before calling this method.
    /// See <see cref="YoFi.V3.Application.Validation.TransactionEditDtoValidator"/> for validation rules.</param>
    /// <returns>The updated transaction with all fields.</returns>
    /// <exception cref="TransactionNotFoundException">Thrown when the transaction is not found.</exception>
    /// <exception cref="ValidationException">A category was supplied for a multi-split transaction.</exception>
    /// <exception cref="DataConflictException">A concurrent change prevented the update.</exception>
    /// <remarks>
    /// Balances the transaction by adjusting the first split by Order, preserving the other splits.
    /// Category is updated only for a single-split transaction.
    /// </remarks>
    public Task<TransactionDetailDto> UpdateTransactionAsync(Guid key, TransactionEditDto transaction)
        => dataProvider.ExecuteInTransactionAsync(async token =>
        {
            var existingTransaction = await GetTransactionByKeyInternalAsync(key);

            if (existingTransaction.Splits.Count > 1 && transaction.Category != null)
            {
                throw new ValidationException(nameof(transaction.Category),
                    "Category cannot be changed for a transaction with multiple splits.");
            }

            existingTransaction.Date = transaction.Date;
            existingTransaction.Amount = transaction.Amount;
            existingTransaction.Payee = transaction.Payee;
            existingTransaction.Memo = transaction.Memo;
            existingTransaction.Source = transaction.Source;
            existingTransaction.ExternalId = transaction.ExternalId;

            var splits = existingTransaction.Splits.OrderBy(s => s.Order).ToList();
            if (splits.Count > 0)
            {
                splits[0].Amount = transaction.Amount - splits.Skip(1).Sum(s => s.Amount);
                if (splits.Count == 1)
                {
                    splits[0].Category = CategoryHelper.SanitizeCategory(transaction.Category);
                }
            }

            dataProvider.UpdateRange([existingTransaction]);
            await dataProvider.SaveChangesAsync(token);

            return ToDetailDto(existingTransaction);
        }, isolationLevel: IsolationLevel.Serializable);

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
    {
        var splitsTotal = transaction.Splits.Sum(split => split.Amount);

        return new TransactionDetailDto(
            transaction.Key,
            transaction.Date,
            transaction.Amount,
            transaction.Payee,
            transaction.Memo,
            transaction.Source,
            transaction.ExternalId,
            transaction.Splits.Count > 1 ? string.Empty
                : transaction.Splits.FirstOrDefault(s => s.Order == 0)?.Category ?? string.Empty,
            splitsTotal,
            transaction.Amount == splitsTotal)
        {
            Splits = transaction.Splits.Count > 1 ? transaction.Splits.OrderBy(s => s.Order)
                .Select(s => new TransactionSplitDto(s.Key, s.Amount, s.Category, s.Memo, s.Order)).ToArray()
                : []
        };
    }

    private static Expression<Func<Transaction, TransactionResultDto>> ToResultDto =>
        t => new TransactionResultDto(
            t.Key,
            t.Date,
            t.Amount,
            t.Payee,
            t.Memo,
            t.Splits.Count > 1 ? string.Empty
                : t.Splits.Where(s => s.Order == 0).Select(s => s.Category).FirstOrDefault() ?? string.Empty,
            t.Splits.Count > 1,
            t.Amount == t.Splits.Sum(split => split.Amount)
        );
}
