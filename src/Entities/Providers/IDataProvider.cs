using System.Linq.Expressions;
using YoFi.V3.Entities.Models;

namespace YoFi.V3.Entities.Providers;

/// <summary>
/// Defines a service to provide data into the system
/// </summary>
public interface IDataProvider
{
    /// <summary>
    /// Retrieves a queryable set of <typeparamref name="TEntity"/> objects
    /// </summary>
    /// <typeparam name="TEntity">The type of entity being operated on by this set.</typeparam>
    /// <returns>queryable set of <typeparamref name="TEntity"/> objects</returns>
    IQueryable<TEntity> Get<TEntity>() where TEntity : class, IModel;

    /// <summary>
    /// Retrieves a queryable set of Transaction objects with Splits navigation property loaded.
    /// </summary>
    /// <returns>Queryable set of Transaction objects with Splits included.</returns>
    IQueryable<Transaction> GetTransactionsWithSplits();

    /// <summary>
    /// Add an item
    /// </summary>
    /// <param name="item">Item to add</param>
    void Add(IModel item);

    /// <summary>
    /// Add a range of items
    /// </summary>
    /// <param name="items">Items to add</param>
    void AddRange(IEnumerable<IModel> items);

    /// <summary>
    /// Update a range of items
    /// </summary>
    /// <param name="items">Items to update</param>
    void UpdateRange(IEnumerable<IModel> items);

    /// <summary>
    /// Remove an item
    /// </summary>
    /// <param name="item">Item to remove</param>
    void Remove(IModel item);

    /// <summary>
    /// Remove a range of items
    /// </summary>
    /// <param name="items">Items to remove</param>
    void RemoveRange(IEnumerable<IModel> items);

    /// <summary>
    /// Save changes previously made
    /// </summary>
    /// <remarks>
    /// This is only needed in the case where we made changes to tracked objects and
    /// did NOT call update on them. Should be rare.
    /// </remarks>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute ToList query asynchronously, with no tracking
    /// </summary>
    /// <typeparam name="T">Type of entities being queried</typeparam>
    /// <param name="query">Query to execute</param>
    /// <returns>List of items</returns>
    Task<List<T>> ToListNoTrackingAsync<T>(IQueryable<T> query) where T : class;
    Task<List<T>> ToListAsync<T>(IQueryable<T> query) where T : class;

    /// <summary>
    /// Execute SingleOrDefault query asynchronously
    /// </summary>
    /// <typeparam name="T">Type of entities being queried</typeparam>
    /// <param name="query">Query to execute</param>
    /// <returns>Single item or default</returns>
    Task<T?> SingleOrDefaultAsync<T>(IQueryable<T> query) where T : class;

    /// <summary>
    /// Execute Count query asynchronously
    /// </summary>
    /// <typeparam name="T">Type of entities being queried</typeparam>
    /// <param name="query">Query to execute</param>
    /// <returns>Count of items matching the query</returns>
    Task<int> CountAsync<T>(IQueryable<T> query) where T : class;

    /// <summary>
    /// Execute bulk delete query asynchronously without loading entities into memory
    /// </summary>
    /// <typeparam name="T">Type of entities being deleted</typeparam>
    /// <param name="query">Query defining which entities to delete</param>
    /// <returns>Number of entities deleted</returns>
    Task<int> ExecuteDeleteAsync<T>(IQueryable<T> query) where T : class;

    /// <summary>
    /// Execute bulk update query to set a single property value without loading entities into memory.
    /// </summary>
    /// <typeparam name="TEntity">Type of entities being updated</typeparam>
    /// <typeparam name="TProperty">Type of the property being updated</typeparam>
    /// <param name="query">Query defining which entities to update</param>
    /// <param name="propertySelector">Expression selecting the property to update</param>
    /// <param name="newValue">New value to set for the property</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of entities updated</returns>
    Task<int> ExecuteUpdatePropertyAsync<TEntity, TProperty>(
        IQueryable<TEntity> query,
        Expression<Func<TEntity, TProperty>> propertySelector,
        TProperty newValue,
        CancellationToken cancellationToken = default)
        where TEntity : class;
}
