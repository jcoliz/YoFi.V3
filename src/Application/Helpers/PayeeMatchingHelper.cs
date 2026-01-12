using System.Text.RegularExpressions;
using YoFi.V3.Entities.Models;

namespace YoFi.V3.Application.Helpers;

/// <summary>
/// Helper methods for payee matching rule operations.
/// </summary>
/// <remarks>
/// Provides pure matching algorithm for finding the best matching rule for a given payee string.
/// Uses precedence rules: regex > substring, longer > shorter, newer > older.
/// </remarks>
public static class PayeeMatchingHelper
{
    /// <summary>
    /// Finds the best matching rule for a given payee string.
    /// </summary>
    /// <param name="payee">Transaction payee string to match against</param>
    /// <param name="rules">Pre-sorted rule list (sorted by ModifiedAt DESC for conflict resolution)</param>
    /// <returns>Category string from best matching rule, or null if no match</returns>
    /// <remarks>
    /// Matching algorithm:
    /// - Tracks separate best regex match and best substring match
    /// - For regex: uses RegexOptions.IgnoreCase | RegexOptions.NonBacktracking
    /// - For substring: uses OrdinalIgnoreCase comparison
    /// - Returns: regex match > substring match > null
    ///
    /// Conflict resolution precedence:
    /// 1. Regex pattern beats substring pattern (always)
    /// 2. For substring: longer pattern beats shorter
    /// 3. For equal length/both regex: most recently modified wins (first in pre-sorted list)
    ///
    /// Exception handling:
    /// - NotSupportedException or ArgumentException during matching are thrown back to caller
    /// - Callers must catch and handle these exceptions appropriately
    /// </remarks>
    /// <exception cref="NotSupportedException">Thrown when regex pattern uses unsupported features</exception>
    /// <exception cref="ArgumentException">Thrown when regex pattern has invalid syntax</exception>
    public static string? FindBestMatch(string payee, IReadOnlyCollection<PayeeMatchingRule> rules)
    {
        if (string.IsNullOrWhiteSpace(payee))
        {
            return null;
        }

        string? bestRegexMatch = null;
        string? bestSubstringMatch = null;
        int longestSubstringLength = 0;

        // Iterate through rules (already sorted by ModifiedAt DESC)
        foreach (var rule in rules)
        {
            if (rule.PayeeIsRegex)
            {
                bestRegexMatch ??= TryRegexMatch(payee, rule);
            }
            else
            {
                var substringMatch = TrySubstringMatch(payee, rule, longestSubstringLength);
                if (substringMatch != null)
                {
                    longestSubstringLength = rule.PayeePattern.Length;
                    bestSubstringMatch = substringMatch;
                }
            }
        }

        // Precedence: regex > substring
        return bestRegexMatch ?? bestSubstringMatch;
    }

    /// <summary>
    /// Attempts to match a payee against a regex rule.
    /// </summary>
    /// <param name="payee">Transaction payee string</param>
    /// <param name="rule">Rule with regex pattern</param>
    /// <returns>Category if match found, null otherwise</returns>
    private static string? TryRegexMatch(string payee, PayeeMatchingRule rule)
    {
        var regex = new Regex(rule.PayeePattern, RegexOptions.IgnoreCase | RegexOptions.NonBacktracking);
        return regex.IsMatch(payee) ? rule.Category : null;
    }

    /// <summary>
    /// Attempts to match a payee against a substring rule.
    /// </summary>
    /// <param name="payee">Transaction payee string</param>
    /// <param name="rule">Rule with substring pattern</param>
    /// <param name="currentLongestLength">Length of current longest match</param>
    /// <returns>Category if match found and longer than current best, null otherwise</returns>
    private static string? TrySubstringMatch(string payee, PayeeMatchingRule rule, int currentLongestLength)
    {
        if (payee.Contains(rule.PayeePattern, StringComparison.OrdinalIgnoreCase)
            && rule.PayeePattern.Length > currentLongestLength)
        {
            return rule.Category;
        }
        return null;
    }
}
