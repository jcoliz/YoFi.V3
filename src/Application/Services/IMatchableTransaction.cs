namespace YoFi.V3.Application.Services;

/// <summary>
/// Represents a transaction that can be matched against payee matching rules.
/// </summary>
/// <remarks>
/// This interface allows payee matching to work with any transaction-like object
/// without coupling to specific DTO types. Currently only requires Payee, but
/// may be extended in the future with additional properties for more complex matching.
/// </remarks>
public interface IMatchableTransaction
{
    /// <summary>
    /// The payee string to match against rules.
    /// </summary>
    string Payee { get; }
}
