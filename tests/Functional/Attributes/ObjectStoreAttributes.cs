namespace YoFi.V3.Tests.Functional.Attributes;

/// <summary>
/// Indicates that a step method requires specific ObjectStore keys to be present before execution.
/// </summary>
/// <remarks>
/// Used for documentation and potential future validation. Lists keys that must exist
/// in the ObjectStore before the method runs. Can be used multiple times on a single method.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class RequiresObjectsAttribute : Attribute
{
    /// <summary>
    /// Gets the ObjectStore keys required by this method.
    /// </summary>
    public string[] Keys { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RequiresObjectsAttribute"/> class.
    /// </summary>
    /// <param name="keys">The ObjectStore keys required by the method.</param>
    public RequiresObjectsAttribute(params string[] keys)
    {
        Keys = keys ?? throw new ArgumentNullException(nameof(keys));
    }
}

/// <summary>
/// Indicates that a step method provides (creates/updates) specific ObjectStore keys during execution.
/// </summary>
/// <remarks>
/// Used for documentation and potential future validation. Lists keys that will be
/// available in the ObjectStore after the method completes. Can be used multiple times on a single method.
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class ProvidesObjectsAttribute : Attribute
{
    /// <summary>
    /// Gets the ObjectStore keys provided by this method.
    /// </summary>
    public string[] Keys { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProvidesObjectsAttribute"/> class.
    /// </summary>
    /// <param name="keys">The ObjectStore keys provided by the method.</param>
    public ProvidesObjectsAttribute(params string[] keys)
    {
        Keys = keys ?? throw new ArgumentNullException(nameof(keys));
    }
}
