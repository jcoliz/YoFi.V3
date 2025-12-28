using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Reflection;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Helpers;
using YoFi.V3.Entities.Exceptions;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Providers;
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
    /// Gets transactions for the current tenant, optionally filtered by date range.
    /// </summary>
    /// <param name="fromDate">Optional start date for filtering transactions (inclusive).</param>
    /// <param name="toDate">Optional end date for filtering transactions (inclusive).</param>
    /// <returns>Requested transactions for the current tenant.</returns>
    public async Task<IReadOnlyCollection<TransactionResultDto>> GetTransactionsAsync(DateOnly? fromDate = null, DateOnly? toDate = null)
    {
        // Validate date range logic
        if (fromDate.HasValue && toDate.HasValue && fromDate > toDate)
        {
            throw new ArgumentException(
                "From date cannot be later than to date.",
                nameof(fromDate)
            );
        }

        var query = GetBaseTransactionQuery();

        if (fromDate.HasValue)
        {
            query = query.Where(t => t.Date >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(t => t.Date <= toDate.Value);
        }

        var dtoQuery = query.Select(ToResultDto);

        var result = await dataProvider.ToListNoTrackingAsync(dtoQuery);

        return result;
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

        // Alpha-1: Get category from single split (Order = 0)
        var category = transaction.Splits.FirstOrDefault(s => s.Order == 0)?.Category ?? string.Empty;

        return new TransactionDetailDto(
            transaction.Key,
            transaction.Date,
            transaction.Amount,
            transaction.Payee,
            transaction.Memo,
            transaction.Source,
            transaction.ExternalId,
            category
        );
    }

    /// <summary>
    /// Adds a new transaction for the current tenant.
    /// </summary>
    /// <param name="transaction">The transaction data to add. MUST be validated before calling this method.
    /// See <see cref="YoFi.V3.Application.Validation.TransactionEditDtoValidator"/> for validation rules.</param>
    /// <returns>The created transaction with all fields.</returns>
    public async Task<TransactionDetailDto> AddTransactionAsync(TransactionEditDto transaction)
    {
        // Alpha-1: Sanitize category
        var sanitizedCategory = CategoryHelper.SanitizeCategory(transaction.Category);

        var newTransaction = new Transaction
        {
            Date = transaction.Date,
            Amount = transaction.Amount,
            Payee = transaction.Payee,
            Memo = transaction.Memo,
            Source = transaction.Source,
            ExternalId = transaction.ExternalId,
            TenantId = _currentTenant.Id
        };

        // Alpha-1: Auto-create single split (Order = 0, Amount = transaction.Amount)
        newTransaction.Splits.Add(new Split
        {
            Amount = transaction.Amount,
            Category = sanitizedCategory,
            Memo = null,
            Order = 0
        });

        dataProvider.Add(newTransaction);
        await dataProvider.SaveChangesAsync();

        return new TransactionDetailDto(
            newTransaction.Key,
            newTransaction.Date,
            newTransaction.Amount,
            newTransaction.Payee,
            newTransaction.Memo,
            newTransaction.Source,
            newTransaction.ExternalId,
            sanitizedCategory
        );
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

        return new TransactionDetailDto(
            existingTransaction.Key,
            existingTransaction.Date,
            existingTransaction.Amount,
            existingTransaction.Payee,
            existingTransaction.Memo,
            existingTransaction.Source,
            existingTransaction.ExternalId,
            sanitizedCategory
        );
    }

    /// <summary>
    /// Quick edit: updates only payee and memo, preserving all other transaction fields.
    /// </summary>
    /// <param name="key">The unique identifier of the transaction to update.</param>
    /// <param name="quickEdit">The updated payee and memo values.</param>
    /// <returns>The updated transaction with all fields.</returns>
    /// <exception cref="TransactionNotFoundException">Thrown when the transaction is not found.</exception>
    public async Task<TransactionDetailDto> QuickEditTransactionAsync(Guid key, TransactionQuickEditDto quickEdit)
    {
        var existingTransaction = await GetTransactionByKeyInternalAsync(key);

        // Alpha-1: Sanitize category
        var sanitizedCategory = CategoryHelper.SanitizeCategory(quickEdit.Category);

        // Update only payee, memo, and category, preserve all other fields (Date, Amount, Source, ExternalId)
        existingTransaction.Payee = quickEdit.Payee;
        existingTransaction.Memo = quickEdit.Memo;

        // Alpha-1: Update single split's category (Order = 0)
        var split = existingTransaction.Splits.FirstOrDefault(s => s.Order == 0);
        if (split != null)
        {
            split.Category = sanitizedCategory;
        }

        dataProvider.UpdateRange([existingTransaction]);
        await dataProvider.SaveChangesAsync();

        return new TransactionDetailDto(
            existingTransaction.Key,
            existingTransaction.Date,
            existingTransaction.Amount,
            existingTransaction.Payee,
            existingTransaction.Memo,
            existingTransaction.Source,
            existingTransaction.ExternalId,
            sanitizedCategory
        );
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
        return dataProvider.GetTransactionsWithSplits()
            .Where(t => t.TenantId == _currentTenant.Id)
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.Id);
    }

    private static void ValidateTransactionQuickEditDto(TransactionQuickEditDto quickEdit)
    {
        // Get constructor parameters for record type validation attributes
        var constructor = typeof(TransactionQuickEditDto).GetConstructors()[0];
        var parameters = constructor.GetParameters();

        // Validate Payee max length first (before whitespace check)
        var payeeParameter = parameters.First(p => p.Name == nameof(TransactionQuickEditDto.Payee));
        var payeeMaxLengthAttr = payeeParameter.GetCustomAttribute<MaxLengthAttribute>();
        if (payeeMaxLengthAttr != null && quickEdit.Payee != null && quickEdit.Payee.Length > payeeMaxLengthAttr.Length)
        {
            throw new ArgumentException($"Transaction payee cannot exceed {payeeMaxLengthAttr.Length} characters.");
        }

        // Validate Payee - must not be empty or whitespace
        if (string.IsNullOrWhiteSpace(quickEdit.Payee))
        {
            throw new ArgumentException("Transaction payee cannot be empty.");
        }

        // Validate Memo max length (nullable field)
        var memoParameter = parameters.First(p => p.Name == nameof(TransactionQuickEditDto.Memo));
        var memoMaxLengthAttr = memoParameter.GetCustomAttribute<MaxLengthAttribute>();
        if (memoMaxLengthAttr != null && quickEdit.Memo != null && quickEdit.Memo.Length > memoMaxLengthAttr.Length)
        {
            throw new ArgumentException($"Transaction memo cannot exceed {memoMaxLengthAttr.Length} characters.");
        }

        // Validate Category max length (nullable field)
        var categoryParameter = parameters.First(p => p.Name == nameof(TransactionQuickEditDto.Category));
        var categoryMaxLengthAttr = categoryParameter.GetCustomAttribute<MaxLengthAttribute>();
        if (categoryMaxLengthAttr != null && quickEdit.Category != null && quickEdit.Category.Length > categoryMaxLengthAttr.Length)
        {
            throw new ArgumentException($"Transaction category cannot exceed {categoryMaxLengthAttr.Length} characters.");
        }
    }

    private static Expression<Func<Transaction, TransactionResultDto>> ToResultDto =>
        t => new TransactionResultDto(
            t.Key,
            t.Date,
            t.Amount,
            t.Payee,
            t.Memo,
            // Alpha-1: Get category from single split (Order = 0), default to empty string
            t.Splits.Where(s => s.Order == 0).Select(s => s.Category).FirstOrDefault() ?? string.Empty
        );
}
