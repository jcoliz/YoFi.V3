using Microsoft.EntityFrameworkCore;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Providers;

namespace YoFi.V3.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options), IDataProvider
{
    #region Data

    public DbSet<WeatherForecast> WeatherForecasts
    {
        get; set;
    }

    #endregion

    #region Model Building

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }

    #endregion

    #region Query Builders

    IQueryable<T> IDataProvider.Get<T>()
        => base.Set<T>();

    #endregion

    #region Modifiers

    void IDataProvider.Add(IModel item)
        => base.Add(item);

    public void AddRange(IEnumerable<IModel> items)
        => base.AddRange(items);

    public void UpdateRange(IEnumerable<IModel> items)
        => base.UpdateRange(items);

    #endregion


    #region Query Runners

#pragma warning disable S2325 // These methods can't be static, as they are accessed via interface

    Task<List<T>> IDataProvider.ToListNoTrackingAsync<T>(IQueryable<T> query)
        => query.AsNoTracking().ToListAsync();

    Task<List<T>> IDataProvider.ToListAsync<T>(IQueryable<T> query)
        => query.ToListAsync();

#pragma warning restore S2325

    #endregion

}
