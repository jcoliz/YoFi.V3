using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using YoFi.V3.Entities.Tenancy.Models;

namespace YoFi.V3.Entities.Models;

/// <summary>
/// Represents a user-defined rule for matching transaction payees and assigning categories automatically.
/// </summary>
/// <remarks>
/// <para>
/// Payee matching rules enable automatic categorization of transactions during bank import.
/// Rules match transaction payees using either substring or regex patterns (case-insensitive)
/// and assign a predefined category when matched.
/// </para>
/// <para>
/// Each rule is scoped to a specific tenant and includes usage tracking (match count and last used timestamp)
/// for analytics and cleanup operations.
/// </para>
/// <para>
/// The Category field is always stored sanitized (via CategoryHelper.SanitizeCategory) to ensure
/// consistency with transaction split categories.
/// </para>
/// </remarks>
[Table("YoFi.V3.PayeeMatchingRules")]
public record PayeeMatchingRule: BaseTenantModel
{
    /// <summary>
    /// Pattern to match against transaction payees (substring or regex).
    /// </summary>
    /// <remarks>
    /// <para>
    /// If <see cref="PayeeIsRegex"/> is false, performs case-insensitive substring matching.
    /// If <see cref="PayeeIsRegex"/> is true, compiles as .NET Regex with IgnoreCase and NonBacktracking options.
    /// </para>
    /// <para>
    /// Required field with maximum length of 200 characters.
    /// </para>
    /// </remarks>
    [Required]
    [MaxLength(200)]
    public string PayeePattern { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether <see cref="PayeePattern"/> is a regular expression.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If false: substring matching (case-insensitive).
    /// If true: regex matching with RegexOptions.IgnoreCase | RegexOptions.NonBacktracking.
    /// </para>
    /// <para>
    /// NonBacktracking engine (available in .NET 7+) provides O(n) time complexity and eliminates
    /// ReDoS vulnerabilities. Patterns using backreferences, lookahead, or lookbehind are rejected
    /// during validation.
    /// </para>
    /// <para>
    /// Defaults to false.
    /// </para>
    /// </remarks>
    public bool PayeeIsRegex { get; set; } = false;

    /// <summary>
    /// Category to assign when the rule matches a transaction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Required field with maximum length of 200 characters.
    /// This field is always stored sanitized (via CategoryHelper.SanitizeCategory) to ensure
    /// consistency with transaction split categories. Sanitization collapses whitespace,
    /// removes leading/trailing colons, and normalizes category separator format.
    /// </para>
    /// </remarks>
    [Required]
    [MaxLength(200)]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when this rule was created.
    /// </summary>
    /// <remarks>
    /// Auto-set on creation. Stored as TEXT in SQLite using ISO 8601 format ("O" format string).
    /// </remarks>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Timestamp when this rule was last modified.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Auto-updated on any rule modification. Used for conflict resolution when multiple rules
    /// match the same transaction (most recently modified rule wins for equal precedence).
    /// </para>
    /// <para>
    /// Stored as TEXT in SQLite using ISO 8601 format ("O" format string).
    /// </para>
    /// </remarks>
    public DateTimeOffset ModifiedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Timestamp when this rule last matched a transaction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Null if the rule has never matched any transaction. Updated automatically during
    /// bank import matching operations.
    /// </para>
    /// <para>
    /// Used for analytics and rule cleanup (identifying unused rules).
    /// Stored as TEXT in SQLite using ISO 8601 format ("O" format string).
    /// </para>
    /// </remarks>
    public DateTimeOffset? LastUsedAt { get; set; }

    /// <summary>
    /// Number of times this rule has matched transactions.
    /// </summary>
    /// <remarks>
    /// Defaults to 0. Incremented automatically during bank import matching operations.
    /// Used for analytics and rule cleanup (identifying low-value rules).
    /// </remarks>
    public int MatchCount { get; set; } = 0;

    // Navigation properties

    /// <summary>
    /// Navigation property to the parent tenant.
    /// </summary>
    public virtual Tenant? Tenant { get; set; }
}
