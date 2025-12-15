using System.Collections.Immutable;

namespace YoFi.V3.BackEnd.Logging;

/// <summary>
/// Manages logging scope state using AsyncLocal storage.
/// </summary>
/// <remarks>
/// This implementation uses AsyncLocal to maintain scope context across async/await boundaries,
/// ensuring that scope information is correctly associated with the originating request/activity.
/// </remarks>
internal sealed class CustomConsoleLoggerScope : IDisposable
{
    private static readonly AsyncLocal<ImmutableStack<object>> _scopes = new();

    private readonly object _state;

    private CustomConsoleLoggerScope(object state)
    {
        _state = state;
    }

    /// <summary>
    /// Pushes a new scope onto the current context's scope stack.
    /// </summary>
    /// <param name="state">The scope state to push.</param>
    /// <returns>A disposable that removes the scope when disposed.</returns>
    public static IDisposable Push(object state)
    {
        var current = _scopes.Value ?? ImmutableStack<object>.Empty;
        _scopes.Value = current.Push(state);
        return new CustomConsoleLoggerScope(state);
    }

    /// <summary>
    /// Gets the current scope stack for the current async context.
    /// </summary>
    /// <returns>A list of scope objects from outermost to innermost.</returns>
    public static IReadOnlyList<object> GetScopes()
    {
        var current = _scopes.Value;
        if (current == null || current.IsEmpty)
        {
            return Array.Empty<object>();
        }

        // Reverse the stack so scopes appear in the order they were created
        return current.Reverse().ToList();
    }

    /// <summary>
    /// Removes this scope from the stack when disposed.
    /// </summary>
    public void Dispose()
    {
        var current = _scopes.Value;
        if (current != null && !current.IsEmpty)
        {
            // Pop the scope if it matches (basic sanity check)
            if (current.Peek() == _state)
            {
                _scopes.Value = current.Pop();
            }
        }
    }
}
