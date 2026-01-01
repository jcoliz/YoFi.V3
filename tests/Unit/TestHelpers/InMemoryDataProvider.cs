using System.Linq.Expressions;
using System.Reflection;
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

    IQueryable<TEntity> IDataProvider.GetWithIncludes<TEntity>(params Expression<Func<TEntity, object>>[] includes)
        // In-memory provider: Navigation properties are already in memory with the entity objects
        // No need for explicit Include() since we're not using a real database, just delegate to Get<TEntity>()
        => ((IDataProvider)this).Get<TEntity>();

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

    public void RemoveRange(IEnumerable<IModel> items)
    {
        foreach (var item in items)
        {
            Remove(item);
        }
    }

    public Task<T?> SingleOrDefaultAsync<T>(IQueryable<T> query) where T : class
    {
        return Task.FromResult(query.SingleOrDefault());
    }

    public Task<int> CountAsync<T>(IQueryable<T> query) where T : class
    {
        return Task.FromResult(query.Count());
    }

    public Task<int> ExecuteDeleteAsync<T>(IQueryable<T> query) where T : class
    {
        var itemsToDelete = query.ToList();
        foreach (var item in itemsToDelete)
        {
            if (item is IModel model)
            {
                Remove(model);
            }
        }
        return Task.FromResult(itemsToDelete.Count);
    }

    public Task<int> ExecuteUpdatePropertyAsync<TEntity, TProperty>(
        IQueryable<TEntity> query,
        Expression<Func<TEntity, TProperty>> propertySelector,
        TProperty newValue,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        var items = query.ToList();
        var propertyInfo = GetPropertyInfo(propertySelector);

        foreach (var item in items)
        {
            propertyInfo.SetValue(item, newValue);
        }

        return Task.FromResult(items.Count);
    }

    private static PropertyInfo GetPropertyInfo<TEntity, TProperty>(
        Expression<Func<TEntity, TProperty>> propertySelector)
    {
        if (propertySelector.Body is not MemberExpression memberExpression)
        {
            throw new ArgumentException("Property selector must be a member expression", nameof(propertySelector));
        }

        return (PropertyInfo)memberExpression.Member;
    }
}
