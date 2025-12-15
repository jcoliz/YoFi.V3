namespace YoFi.V3.BackEnd.Logging;

/// <summary>
/// Configuration options for the systemd-style console logger.
/// </summary>
public sealed class CustomConsoleLoggerOptions
{
    /// <summary>
    /// Gets or sets the timestamp format string.
    /// </summary>
    /// <remarks>
    /// Default is "MM-ddTHH:mm:ss " to match systemd journal format.
    /// </remarks>
    public string TimestampFormat { get; set; } = "MM-dd'T'HH:mm:ss ";

    /// <summary>
    /// Gets or sets whether to use UTC timestamps.
    /// </summary>
    /// <remarks>
    /// Default is false (use local time).
    /// </remarks>
    public bool UseUtcTimestamp { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to include scope information in log output.
    /// </summary>
    /// <remarks>
    /// Default is true to capture Activity, Request, and other scope data.
    /// </remarks>
    public bool IncludeScopes { get; set; } = true;
}
