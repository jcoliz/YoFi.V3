namespace YoFi.V3.Entities.Models;

/// <summary>
/// Identifies an object as a model stored in the database
/// </summary>
public interface IModel
{
    /// <summary>
    /// Database identity for this record
    /// </summary>
    /// <remarks>
    /// By definition, all models stored in the database use an increasing
    /// int as their primary key, and clustered index. Use a GUID public-facing
    /// key with separate non-clustered index if we don't want to expose this
    /// ID to the public.
    /// </remarks>
    long Id { get; set; }

    /// <summary>
    /// Public-facing unique identifier for this record
    /// </summary>
    /// <remarks>
    /// For compatibility with Sqlite, remember to generate a GUID for the Key
    /// when creating a new record, rather than relying on the database
    /// to generate it.
    /// </remarks>
    Guid Key { get; set; }
}
