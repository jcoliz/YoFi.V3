using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Reflection;
using YoFi.V3.Application.Dto;
using YoFi.V3.Entities.Exceptions;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Providers;
using YoFi.V3.Entities.Tenancy.Models;
using YoFi.V3.Entities.Tenancy.Providers;
using YoFi.V3.Application.Validation;

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

        return new TransactionDetailDto(
            transaction.Key,
            transaction.Date,
            transaction.Amount,
            transaction.Payee,
            transaction.Memo,
            transaction.Source,
            transaction.ExternalId
        );
    }

    /// <summary>
    /// Adds a new transaction for the current tenant.
    /// </summary>
    /// <param name="transaction">The transaction data to add.</param>
    /// <returns>The created transaction with all fields.</returns>
    public async Task<TransactionDetailDto> AddTransactionAsync(TransactionEditDto transaction)
    {
        ValidateTransactionEditDto(transaction);

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
        dataProvider.Add(newTransaction);
        await dataProvider.SaveChangesAsync();

        return new TransactionDetailDto(
            newTransaction.Key,
            newTransaction.Date,
            newTransaction.Amount,
            newTransaction.Payee,
            newTransaction.Memo,
            newTransaction.Source,
            newTransaction.ExternalId
        );
    }

    /// <summary>
    /// Updates an existing transaction.
    /// </summary>
    /// <param name="key">The unique identifier of the transaction to update.</param>
    /// <param name="transaction">The updated transaction data.</param>
    /// <returns>The updated transaction with all fields.</returns>
    /// <exception cref="TransactionNotFoundException">Thrown when the transaction is not found.</exception>
    public async Task<TransactionDetailDto> UpdateTransactionAsync(Guid key, TransactionEditDto transaction)
    {
        ValidateTransactionEditDto(transaction);

        var existingTransaction = await GetTransactionByKeyInternalAsync(key);

        // Update all properties (all fields are editable per Story 3)
        existingTransaction.Date = transaction.Date;
        existingTransaction.Amount = transaction.Amount;
        existingTransaction.Payee = transaction.Payee;
        existingTransaction.Memo = transaction.Memo;
        existingTransaction.Source = transaction.Source;
        existingTransaction.ExternalId = transaction.ExternalId;

        dataProvider.UpdateRange([existingTransaction]);
        await dataProvider.SaveChangesAsync();

        return new TransactionDetailDto(
            existingTransaction.Key,
            existingTransaction.Date,
            existingTransaction.Amount,
            existingTransaction.Payee,
            existingTransaction.Memo,
            existingTransaction.Source,
            existingTransaction.ExternalId
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
    /// <returns>A queryable of transactions for the current tenant, ordered by date descending.</returns>
    private IQueryable<Transaction> GetBaseTransactionQuery()
    {
        return dataProvider.Get<Transaction>()
            .Where(t => t.TenantId == _currentTenant.Id)
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.Id);
    }

    private static void ValidateTransactionEditDto(TransactionEditDto transaction)
    {
        // Get constructor parameters for record type validation attributes
        var constructor = typeof(TransactionEditDto).GetConstructors()[0];
        var parameters = constructor.GetParameters();

        // Validate Date using DateRangeAttribute (from constructor parameter)
        var dateParameter = parameters.First(p => p.Name == nameof(TransactionEditDto.Date));
        var dateRangeAttr = dateParameter.GetCustomAttribute<DateRangeAttribute>();
        if (dateRangeAttr != null)
        {
            var dateValidationContext = new ValidationContext(transaction) { MemberName = nameof(TransactionEditDto.Date) };
            var dateResult = dateRangeAttr.GetValidationResult(transaction.Date, dateValidationContext);
            if (dateResult != ValidationResult.Success)
            {
                throw new ArgumentException(dateResult!.ErrorMessage!);
            }
        }

        // Validate Amount - must be non-zero
        if (transaction.Amount == 0)
        {
            throw new ArgumentException("Transaction amount cannot be zero.");
        }

        // Validate Payee max length first (before whitespace check) - from constructor parameter
        var payeeParameter = parameters.First(p => p.Name == nameof(TransactionEditDto.Payee));
        var payeeMaxLengthAttr = payeeParameter.GetCustomAttribute<MaxLengthAttribute>();
        if (payeeMaxLengthAttr != null && transaction.Payee != null && transaction.Payee.Length > payeeMaxLengthAttr.Length)
        {
            throw new ArgumentException($"Transaction payee cannot exceed {payeeMaxLengthAttr.Length} characters.");
        }

        // Validate Payee - must not be empty or whitespace
        if (string.IsNullOrWhiteSpace(transaction.Payee))
        {
            throw new ArgumentException("Transaction payee cannot be empty.");
        }

        // Validate Memo max length (nullable field)
        var memoParameter = parameters.First(p => p.Name == nameof(TransactionEditDto.Memo));
        var memoMaxLengthAttr = memoParameter.GetCustomAttribute<MaxLengthAttribute>();
        if (memoMaxLengthAttr != null && transaction.Memo != null && transaction.Memo.Length > memoMaxLengthAttr.Length)
        {
            throw new ArgumentException($"Transaction memo cannot exceed {memoMaxLengthAttr.Length} characters.");
        }

        // Validate Source max length (nullable field)
        var sourceParameter = parameters.First(p => p.Name == nameof(TransactionEditDto.Source));
        var sourceMaxLengthAttr = sourceParameter.GetCustomAttribute<MaxLengthAttribute>();
        if (sourceMaxLengthAttr != null && transaction.Source != null && transaction.Source.Length > sourceMaxLengthAttr.Length)
        {
            throw new ArgumentException($"Transaction source cannot exceed {sourceMaxLengthAttr.Length} characters.");
        }

        // Validate ExternalId max length (nullable field)
        var externalIdParameter = parameters.First(p => p.Name == nameof(TransactionEditDto.ExternalId));
        var externalIdMaxLengthAttr = externalIdParameter.GetCustomAttribute<MaxLengthAttribute>();
        if (externalIdMaxLengthAttr != null && transaction.ExternalId != null && transaction.ExternalId.Length > externalIdMaxLengthAttr.Length)
        {
            throw new ArgumentException($"Transaction externalId cannot exceed {externalIdMaxLengthAttr.Length} characters.");
        }
    }

    private static Expression<Func<Transaction, TransactionResultDto>> ToResultDto =>
        t => new TransactionResultDto(t.Key, t.Date, t.Amount, t.Payee, t.Memo);
}
