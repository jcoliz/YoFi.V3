using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Providers;

namespace YoFi.V3.Tests.Unit.TestHelpers;

/// <summary>
/// An in-memory implementation of IDataProvider for unit testing purposes.
/// </summary>
public class InMemoryDataProvider : IDataProvider
{
    private readonly List<WeatherForecast> _weatherForecasts = new();

    public IQueryable<WeatherForecast> WeatherForecasts => _weatherForecasts.AsQueryable();

    public void Add<T>(T entity) where T : class
    {
        if (entity is WeatherForecast forecast)
        {
            _weatherForecasts.Add(forecast);
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
    }

    IQueryable<TEntity> IDataProvider.Get<TEntity>()
    {
        if (typeof(TEntity) == typeof(WeatherForecast))
        {
            return (IQueryable<TEntity>)_weatherForecasts.AsQueryable();
        }

        throw new NotSupportedException($"Entity type {typeof(TEntity)} is not supported.");
    }

    public void Add(IModel item)
    {
        throw new NotImplementedException();
    }

    public void AddRange(IEnumerable<IModel> items)
    {
        foreach (var item in items)
        {
            if (item is WeatherForecast forecast)
            {
                _weatherForecasts.Add(forecast);
            }
            else
            {
                throw new NotSupportedException($"Entity type {item.GetType()} is not supported.");
            }
        }
    }

    public void UpdateRange(IEnumerable<IModel> items)
    {
        throw new NotImplementedException();
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
        throw new NotImplementedException();
    }
}
