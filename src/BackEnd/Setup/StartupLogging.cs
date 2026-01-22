using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using YoFi.V3.Entities.Options;

namespace YoFi.V3.BackEnd.Setup;

/// <summary>
/// Provides LoggerMessage methods for startup and setup logging.
/// </summary>
public static partial class StartupLogging
{
    [LoggerMessage(1, LogLevel.Information, "{Location}: Starting {App} Process ID: {ProcessId}, Thread ID: {ThreadId}")]
    public static partial void LogStarting(
        this ILogger logger,
        string app,
        int processId,
        int threadId,
        [CallerMemberName] string? location = null);

    [LoggerMessage(4, LogLevel.Information, "{Location}: CORS configured with allowed origins: {Origins}")]
    public static partial void LogCorsConfigured(
        this ILogger logger,
        string origins,
        [CallerMemberName] string? location = null);

    [LoggerMessage(5, LogLevel.Error, "{Location}: No allowed CORS origins configured. Please set Application:AllowedCorsOrigins in configuration.")]
    public static partial void LogCorsNotConfigured(
        this ILogger logger,
        [CallerMemberName] string? location = null);

    [LoggerMessage(8, LogLevel.Information, "{Location}: Enabling Swagger UI")]
    public static partial void LogEnablingSwagger(
        this ILogger logger,
        [CallerMemberName] string? location = null);

    [LoggerMessage(10, LogLevel.Information, "{Location}: OK. Environment: {Environment}")]
    public static partial void LogOkEnvironment(
        this ILogger logger,
        string environment,
        [CallerMemberName] string? location = null);

    [LoggerMessage(11, LogLevel.Information, "{Location}: Application Stopped Normally")]
    public static partial void LogApplicationStopped(
        this ILogger logger,
        [CallerMemberName] string? location = null);

    [LoggerMessage(12, LogLevel.Critical, "{Location}: Failed to start")]
    public static partial void LogStartupFailed(
        this ILogger logger,
        Exception exception,
        [CallerMemberName] string? location = null);

    [LoggerMessage(13, LogLevel.Debug, "{Location}: Checkpoint {Checkpoint} reached")]
    public static partial void LogCheckpointReached(
        this ILogger logger,
        string checkpoint,
        [CallerMemberName] string? location = null);

    [LoggerMessage(21, LogLevel.Information, "{Location}: Version: {Version}")]
    public static partial void LogVersion(
        this ILogger logger,
        string version,
        [CallerMemberName] string? location = null);
}
