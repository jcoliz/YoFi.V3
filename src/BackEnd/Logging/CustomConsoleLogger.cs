using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;

namespace YoFi.V3.BackEnd.Logging;

/// <summary>
/// Console logger that formats output to mimic systemd scoped logs.
/// </summary>
/// <remarks>
/// Outputs logs in the format:
/// &lt;priority&gt;MM-ddTHH:mm:ss CategoryName[EventId] => Scope1 => Scope2 Message
///
/// Priority codes follow syslog RFC 5424:
/// 0=Emergency, 1=Alert, 2=Critical, 3=Error, 4=Warning, 5=Notice, 6=Information, 7=Debug
/// </remarks>
public sealed class CustomConsoleLogger : ILogger
{
    private readonly string _categoryName;
    private readonly CustomConsoleLoggerOptions _options;

    /// <summary>
    /// Creates a new systemd-style console logger.
    /// </summary>
    /// <param name="categoryName">The category name for the logger.</param>
    /// <param name="options">Configuration options for the logger.</param>
    public CustomConsoleLogger(string categoryName, CustomConsoleLoggerOptions options)
    {
        _categoryName = categoryName;
        _options = options;
    }

    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return CustomConsoleLoggerScope.Push(state);
    }

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel != LogLevel.None;
    }

    /// <inheritdoc/>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);
        if (string.IsNullOrEmpty(message) && exception == null)
        {
            return;
        }

        WriteLogEntry(logLevel, eventId, message, exception);
    }

    private void WriteLogEntry(LogLevel logLevel, EventId eventId, string message, Exception? exception)
    {
        var builder = new StringBuilder();

        // Priority code (syslog style)
        builder.Append('<');
        builder.Append(GetSyslogPriority(logLevel));
        builder.Append('>');

        // Timestamp
        var timestamp = _options.UseUtcTimestamp ? DateTime.UtcNow : DateTime.Now;
        builder.Append(timestamp.ToString(_options.TimestampFormat));

        // Category name
        builder.Append(_categoryName);

        // Event ID (if present)
        if (eventId.Id != 0)
        {
            builder.Append('[');
            builder.Append(eventId.Id);
            builder.Append(']');
        }

        // Scopes
        if (_options.IncludeScopes)
        {
            var scopes = CustomConsoleLoggerScope.GetScopes();
            if (scopes.Count > 0)
            {
                foreach (var scope in scopes)
                {
                    builder.Append(" => ");
                    builder.Append(scope);
                }
            }
        }

        // Message
        builder.Append(' ');
        builder.Append(message);

        // Exception (if present)
        if (exception != null)
        {
            builder.AppendLine();
            builder.Append(exception);
        }

        // Write to console
        Console.WriteLine(builder.ToString());
    }

    /// <summary>
    /// Maps .NET LogLevel to syslog priority codes (RFC 5424).
    /// </summary>
    /// <param name="logLevel">The log level to map.</param>
    /// <returns>Syslog priority code (0-7).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetSyslogPriority(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Trace => 7,        // Debug
            LogLevel.Debug => 7,        // Debug
            LogLevel.Information => 6,  // Informational
            LogLevel.Warning => 4,      // Warning
            LogLevel.Error => 3,        // Error
            LogLevel.Critical => 2,     // Critical
            _ => 6                      // Default to Informational
        };
    }
}
