using System.Text.RegularExpressions;

namespace YoFi.V3.Application.Services;

/// <summary>
/// Service for validating regex patterns for correctness and ReDoS vulnerabilities.
/// </summary>
/// <remarks>
/// Uses NonBacktracking regex engine (available in .NET 7+) to completely eliminate ReDoS vulnerabilities.
/// NonBacktracking provides guaranteed linear time complexity O(n), but does not support advanced features
/// like backreferences, lookahead, or lookbehind.
/// </remarks>
public class RegexValidationService : IRegexValidationService
{
    /// <summary>
    /// Validates a regex pattern for correctness and ReDoS vulnerabilities.
    /// </summary>
    /// <param name="pattern">The regex pattern to validate</param>
    /// <returns>Validation result indicating whether the pattern is valid and any error message</returns>
    public RegexValidationResult ValidateRegex(string pattern)
    {
        // Check pattern is not null/whitespace
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return new RegexValidationResult(false, "Pattern cannot be empty or whitespace.");
        }

        try
        {
            // Attempt to compile with NonBacktracking to eliminate ReDoS vulnerabilities
            _ = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.NonBacktracking);
            return new RegexValidationResult(true, null);
        }
        catch (NotSupportedException ex)
        {
            // Pattern uses unsupported features (backreferences, lookahead/lookbehind)
            return new RegexValidationResult(false,
                $"Pattern uses features not supported by the ReDoS-safe regex engine (backreferences, lookahead, or lookbehind are not allowed). {ex.Message}");
        }
        catch (ArgumentException ex)
        {
            // Invalid syntax
            return new RegexValidationResult(false,
                $"Invalid regex pattern: {ex.Message}");
        }
    }
}
