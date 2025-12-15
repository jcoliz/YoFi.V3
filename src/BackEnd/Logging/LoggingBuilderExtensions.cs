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

    /// <summary>
    /// Creates a startup logger with custom console configuration.
    /// </summary>
    /// <returns>A logger instance configured for startup logging.</returns>
    /// <remarks>
    /// This logger is used during application startup before the full logging infrastructure is available.
    /// </remarks>
    public static ILogger CreateStartupLogger()
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddCustomConsole(ConfigureConsoleOptions);
            builder.AddEventSourceLogger();
        });

        return loggerFactory.CreateLogger("Startup");
    }

    /// <summary>
    /// Configures application logging with custom console provider.
    /// </summary>
    /// <param name="logging">The logging builder to configure.</param>
    /// <returns>The logging builder for chaining.</returns>
    /// <remarks>
    /// Clears default providers and adds only the custom console logger.
    /// </remarks>
    public static ILoggingBuilder AddApplicationLogging(this ILoggingBuilder logging)
    {
        logging.ClearProviders();
        logging.AddCustomConsole(ConfigureConsoleOptions);
        return logging;
    }

    private static void ConfigureConsoleOptions(CustomConsoleLoggerOptions options)
    {
        options.IncludeScopes = true;
#if DEBUG
        options.TimestampFormat = "MM-dd'T'HH:mm:ss ";
        options.UseUtcTimestamp = false;
#endif
    }
}
