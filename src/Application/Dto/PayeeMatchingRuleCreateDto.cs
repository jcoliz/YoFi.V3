namespace YoFi.V3.Application.Dto;

/// <summary>
/// Input for creating a rule, optionally categorizing existing uncategorized transactions.
/// </summary>
/// <param name="PayeePattern">Pattern to match against transaction payees.</param>
/// <param name="PayeeIsRegex">Whether the pattern is a regular expression.</param>
/// <param name="Category">Category to assign when the rule matches.</param>
/// <param name="ApplyToUncategorizedTransactions">Apply only this rule to eligible transactions in the workspace.
/// Defaults to false to preserve create-only behavior for existing clients.</param>
public sealed record PayeeMatchingRuleCreateDto(
    string PayeePattern,
    bool PayeeIsRegex,
    string Category,
    bool ApplyToUncategorizedTransactions = false
) : PayeeMatchingRuleEditDto(PayeePattern, PayeeIsRegex, Category);
