namespace YoFi.V3.Application.Dto;

/// <summary>
/// Created rule with the number of existing transactions categorized during creation.
/// Retains all existing top-level rule fields for compatibility with existing clients.
/// </summary>
/// <param name="Key">Unique identifier for the rule.</param>
/// <param name="PayeePattern">Pattern to match against transaction payees.</param>
/// <param name="PayeeIsRegex">Whether the pattern is a regular expression.</param>
/// <param name="Category">Sanitized category assigned by the rule.</param>
/// <param name="CreatedAt">When the rule was created.</param>
/// <param name="ModifiedAt">When the rule was last modified.</param>
/// <param name="LastUsedAt">When the rule last matched, or null if unused.</param>
/// <param name="MatchCount">Number of matching operations in which the rule was used.</param>
/// <param name="TransactionsCategorized">Number of transactions categorized by this create operation.</param>
public sealed record PayeeMatchingRuleCreateResultDto(
    Guid Key,
    string PayeePattern,
    bool PayeeIsRegex,
    string Category,
    DateTimeOffset CreatedAt,
    DateTimeOffset ModifiedAt,
    DateTimeOffset? LastUsedAt,
    int MatchCount,
    int TransactionsCategorized
) : PayeeMatchingRuleResultDto(
    Key, PayeePattern, PayeeIsRegex, Category, CreatedAt, ModifiedAt, LastUsedAt, MatchCount);
