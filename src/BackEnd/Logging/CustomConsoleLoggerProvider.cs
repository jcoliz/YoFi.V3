using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace YoFi.V3.BackEnd.Logging;

/// <summary>
/// Provider for systemd-style console loggers.
/// </summary>
/// <remarks>
/// This provider creates and manages logger instances, maintaining a cache of loggers
/// by category name to avoid creating duplicate instances.
/// </remarks>
[ProviderAlias("CustomConsole")]
public sealed class CustomConsoleLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, CustomConsoleLogger> _loggers = new();
    private readonly CustomConsoleLoggerOptions _options;

    /// <summary>
    /// Creates a new systemd-style console logger provider.
    /// </summary>
    /// <param name="options">Configuration options for the loggers.</param>
    public CustomConsoleLoggerProvider(IOptions<CustomConsoleLoggerOptions> options)
    {
        _options = options.Value;
    }

    /// <inheritdoc/>
    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name => new CustomConsoleLogger(name, _options));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _loggers.Clear();
    }
}
