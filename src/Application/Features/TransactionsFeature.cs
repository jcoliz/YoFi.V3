using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Providers;
using YoFi.V3.Entities.Tenancy;

namespace YoFi.V3.Application.Features;

public class TransactionsFeature(ITenantProvider tenantProvider, IDataProvider dataProvider)
{
    private readonly Tenant _currentTenant = tenantProvider.CurrentTenant;

    /// <summary>
    /// Gets transactions for the current tenant, optionally filtered by date range.
    /// </summary>
    /// <remarks>
    /// TODO: Move to a DTO-based approach rather than returning entity models directly.
    /// </remarks>
    /// <param name="fromDate">Optional start date for filtering transactions (inclusive).</param>
    /// <param name="toDate">Optional end date for filtering transactions (inclusive).</param>
    /// <returns>Requested transactions for the current tenant.</returns>
    public async Task<ICollection<Transaction>> GetTransactionsAsync(DateOnly? fromDate = null, DateOnly? toDate = null)
    {
        var query = GetBaseTransactionQuery();

        if (fromDate.HasValue)
        {
            query = query.Where(t => t.Date >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(t => t.Date <= toDate.Value);
        }

        return await dataProvider.ToListAsync(query);
    }

    public async Task<Transaction> GetTransactionByKeyAsync(Guid key)
    {
        var query = GetBaseTransactionQuery()
            .Where(t => t.Key == key);

        var result = await dataProvider.ToListAsync(query);

        if (result.Count == 0)
        {
            // FIX: Use an application-specific exception type.
            throw new KeyNotFoundException("Transaction not found.");
        }

        return result[0];
    }

    public async Task AddTransactionAsync(Transaction transaction)
    {
        transaction.TenantId = _currentTenant.Id;
        dataProvider.Add(transaction);
        await dataProvider.SaveChangesAsync();
    }

    public async Task UpdateTransactionAsync(Transaction transaction)
    {
        var existingTransaction = await GetTransactionByKeyAsync(transaction.Key);

        // Update properties
        existingTransaction.Date = transaction.Date;
        existingTransaction.Amount = transaction.Amount;

        dataProvider.UpdateRange([existingTransaction]);
        await dataProvider.SaveChangesAsync();
    }

    public async Task DeleteTransactionAsync(Guid key)
    {
        var transaction = await GetTransactionByKeyAsync(key);
        // TODO: dataProvider.RemoveRange([transaction]);
        await dataProvider.SaveChangesAsync();
    }

    private IQueryable<Transaction> GetBaseTransactionQuery()
    {
        return dataProvider.Get<Transaction>()
            .Where(t => t.TenantId == _currentTenant.Id)
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.Id);
    }
}
