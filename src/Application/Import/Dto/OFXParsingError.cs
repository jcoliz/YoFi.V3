namespace YoFi.V3.Application.Import.Dto;

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
}
