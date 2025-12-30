namespace YoFi.V3.Application.Dto;

/// <summary>
/// Error encountered during OFX parsing.
/// </summary>
public class OfxParsingError
{
    /// <summary>
    /// Error message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Error code or type.
    /// </summary>
    public string? Code { get; init; }

    /// <summary>
    /// Name of the file where the error occurred.
    /// </summary>
    public string? FileName { get; init; }
}
