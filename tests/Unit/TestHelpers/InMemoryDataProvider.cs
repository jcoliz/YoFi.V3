using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Providers;

namespace YoFi.V3.Tests.Unit.TestHelpers;

/// <summary>
/// An in-memory implementation of IDataProvider for unit testing purposes.
/// </summary>
public class InMemoryDataProvider : IDataProvider
{
    private readonly List<WeatherForecast> _weatherForecasts = new();
    private readonly List<Transaction> _transactions = new();
    private int _nextId = 1;

    public IQueryable<WeatherForecast> WeatherForecasts => _weatherForecasts.AsQueryable();
    public IQueryable<Transaction> Transactions => _transactions.AsQueryable();

    public void Add<T>(T entity) where T : class
    {
        if (entity is WeatherForecast forecast)
        {
            _weatherForecasts.Add(forecast);
        }
        else if (entity is Transaction transaction)
        {
            _transactions.Add(transaction);
        }
        else
        {
            throw new NotSupportedException($"Entity type {typeof(T)} is not supported.");
        }
    }

    public void SaveChanges()
    {
        // No-op for in-memory implementation
    }

    public void Dispose()
    {
        _weatherForecasts.Clear();
        _transactions.Clear();
    }

    IQueryable<TEntity> IDataProvider.Get<TEntity>()
    {
        if (typeof(TEntity) == typeof(WeatherForecast))
        {
            return (IQueryable<TEntity>)_weatherForecasts.AsQueryable();
        }
        else if (typeof(TEntity) == typeof(Transaction))
        {
            return (IQueryable<TEntity>)_transactions.AsQueryable();
        }

        throw new NotSupportedException($"Entity type {typeof(TEntity)} is not supported.");
    }

    public void Add(IModel item)
    {
        if (item is WeatherForecast forecast)
        {
            _weatherForecasts.Add(forecast);
        }
        else if (item is Transaction transaction)
        {
            // Assign ID if not set
            if (transaction.Id == 0)
            {
                transaction.Id = _nextId++;
            }
            // Assign Key if not set
            if (transaction.Key == Guid.Empty)
            {
                transaction.Key = Guid.NewGuid();
            }
            _transactions.Add(transaction);
        }
        else
        {
            throw new NotSupportedException($"Entity type {item.GetType()} is not supported.");
        }
    }

    public void AddRange(IEnumerable<IModel> items)
    {
        foreach (var item in items)
        {
            Add(item);
        }
    }

    public void UpdateRange(IEnumerable<IModel> items)
    {
        // In-memory updates are already reflected since we're working with references
        // No action needed
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(0);
    }

    Task<List<T>> IDataProvider.ToListNoTrackingAsync<T>(IQueryable<T> query)
    {
        return Task.FromResult(query.ToList());
    }

    Task<List<T>> IDataProvider.ToListAsync<T>(IQueryable<T> query)
    {
        return Task.FromResult(query.ToList());
    }

    public void Remove(IModel item)
    {
        if (item is WeatherForecast forecast)
        {
            _weatherForecasts.Remove(forecast);
        }
        else if (item is Transaction transaction)
        {
            _transactions.Remove(transaction);
        }
        else
        {
            throw new NotSupportedException($"Entity type {item.GetType()} is not supported.");
        }
    }

    public Task<T?> SingleOrDefaultAsync<T>(IQueryable<T> query) where T : class
    {
        return Task.FromResult(query.SingleOrDefault());
    }
}
