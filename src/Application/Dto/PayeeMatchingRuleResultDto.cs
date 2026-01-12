namespace YoFi.V3.Application.Dto;

/// <summary>
/// Payee matching rule data returned from queries (output DTO).
/// </summary>
/// <param name="Key">Unique identifier for the rule</param>
/// <param name="PayeePattern">Pattern to match against transaction payee (substring or regex)</param>
/// <param name="PayeeIsRegex">If true, PayeePattern is treated as regex; if false, as substring</param>
/// <param name="Category">Category to assign when rule matches (already sanitized)</param>
/// <param name="CreatedAt">When the rule was created</param>
/// <param name="ModifiedAt">When the rule was last modified (used for conflict resolution)</param>
/// <param name="LastUsedAt">When the rule last matched a transaction (null if never used)</param>
/// <param name="MatchCount">Number of times this rule has matched transactions</param>
/// <remarks>
/// This is an output DTO - data is already validated when read from the database.
/// Category is stored sanitized in the database and returned as-is.
///
/// For creating or updating rules, see <see cref="PayeeMatchingRuleEditDto"/>.
/// </remarks>
public record PayeeMatchingRuleResultDto(
    Guid Key,
    string PayeePattern,
    bool PayeeIsRegex,
    string Category,
    DateTimeOffset CreatedAt,
    DateTimeOffset ModifiedAt,
    DateTimeOffset? LastUsedAt,
    int MatchCount
);
