using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace YoFi.V3.Tests.Integration.Controller.TestHelpers;

/// <summary>
/// A single captured log entry
/// </summary>
public record CapturedLogEntry(string Category, LogLevel Level, EventId EventId, string Message);

/// <summary>
/// Logger provider which records all log entries in memory for test verification
/// </summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<CapturedLogEntry> _entries = new();

    /// <summary>
    /// All entries captured so far
    /// </summary>
    public IReadOnlyCollection<CapturedLogEntry> Entries => _entries.ToArray();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _entries);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(string category, ConcurrentQueue<CapturedLogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            entries.Enqueue(new CapturedLogEntry(category, logLevel, eventId, formatter(state, exception)));
        }
    }
}
