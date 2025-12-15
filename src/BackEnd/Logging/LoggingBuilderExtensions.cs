using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Configuration;

namespace YoFi.V3.BackEnd.Logging;

/// <summary>
/// Extension methods for configuring systemd-style console logging.
/// </summary>
public static class LoggingBuilderExtensions
{
    /// <summary>
    /// Adds a systemd-style console logger to the logging builder.
    /// </summary>
    /// <param name="builder">The logging builder to configure.</param>
    /// <returns>The logging builder for chaining.</returns>
    public static ILoggingBuilder AddCustomConsole(this ILoggingBuilder builder)
    {
        builder.AddConfiguration();

        builder.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<ILoggerProvider, CustomConsoleLoggerProvider>());

        LoggerProviderOptions.RegisterProviderOptions
            <CustomConsoleLoggerOptions, CustomConsoleLoggerProvider>(builder.Services);

        return builder;
    }

    /// <summary>
    /// Adds a systemd-style console logger to the logging builder with configuration action.
    /// </summary>
    /// <param name="builder">The logging builder to configure.</param>
    /// <param name="configure">Action to configure the logger options.</param>
    /// <returns>The logging builder for chaining.</returns>
    public static ILoggingBuilder AddCustomConsole(
        this ILoggingBuilder builder,
        Action<CustomConsoleLoggerOptions> configure)
    {
        builder.AddCustomConsole();
        builder.Services.Configure(configure);
        return builder;
    }
}
