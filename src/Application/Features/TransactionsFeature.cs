using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Reflection;
using YoFi.V3.Application.Dto;
using YoFi.V3.Entities.Exceptions;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Providers;
using YoFi.V3.Entities.Tenancy;
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
    public async Task<ICollection<TransactionResultDto>> GetTransactionsAsync(DateOnly? fromDate = null, DateOnly? toDate = null)
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
    /// Gets a specific transaction by its unique key.
    /// </summary>
    /// <param name="key">The unique identifier of the transaction.</param>
    /// <returns>The requested transaction.</returns>
    /// <exception cref="TransactionNotFoundException">Thrown when the transaction is not found.</exception>
    public async Task<TransactionResultDto> GetTransactionByKeyAsync(Guid key)
    {
        var transaction = await GetTransactionByKeyInternalAsync(key);

        return new TransactionResultDto(transaction.Key, transaction.Date, transaction.Amount, transaction.Payee);
    }

    /// <summary>
    /// Adds a new transaction for the current tenant.
    /// </summary>
    /// <param name="transaction">The transaction data to add.</param>
    public async Task AddTransactionAsync(TransactionEditDto transaction)
    {
        ValidateTransactionEditDto(transaction);

        var newTransaction = new Transaction
        {
            Date = transaction.Date,
            Amount = transaction.Amount,
            Payee = transaction.Payee,
            TenantId = _currentTenant.Id
        };
        dataProvider.Add(newTransaction);
        await dataProvider.SaveChangesAsync();
    }

    /// <summary>
    /// Updates an existing transaction.
    /// </summary>
    /// <param name="key">The unique identifier of the transaction to update.</param>
    /// <param name="transaction">The updated transaction data.</param>
    /// <exception cref="TransactionNotFoundException">Thrown when the transaction is not found.</exception>
    public async Task UpdateTransactionAsync(Guid key, TransactionEditDto transaction)
    {
        ValidateTransactionEditDto(transaction);

        var existingTransaction = await GetTransactionByKeyInternalAsync(key);

        // Update properties
        existingTransaction.Date = transaction.Date;
        existingTransaction.Amount = transaction.Amount;
        existingTransaction.Payee = transaction.Payee;

        dataProvider.UpdateRange([existingTransaction]);
        await dataProvider.SaveChangesAsync();
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
        // Validate Date using DateRangeAttribute
        var dateProperty = typeof(TransactionEditDto).GetProperty(nameof(TransactionEditDto.Date))!;
        var dateRangeAttr = dateProperty.GetCustomAttribute<DateRangeAttribute>();
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

        // Validate Payee max length first (before whitespace check)
        var payeeProperty = typeof(TransactionEditDto).GetProperty(nameof(TransactionEditDto.Payee))!;
        var maxLengthAttr = payeeProperty.GetCustomAttribute<MaxLengthAttribute>();
        if (maxLengthAttr != null && transaction.Payee != null && transaction.Payee.Length > maxLengthAttr.Length)
        {
            throw new ArgumentException($"Transaction payee cannot exceed {maxLengthAttr.Length} characters.");
        }

        // Validate Payee - must not be empty or whitespace
        if (string.IsNullOrWhiteSpace(transaction.Payee))
        {
            throw new ArgumentException("Transaction payee cannot be empty.");
        }
    }

    private static Expression<Func<Transaction, TransactionResultDto>> ToResultDto =>
        t => new TransactionResultDto(t.Key, t.Date, t.Amount, t.Payee);
}
