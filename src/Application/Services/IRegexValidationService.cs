namespace YoFi.V3.Application.Services;

/// <summary>
/// Service for validating regex patterns for correctness and ReDoS vulnerabilities.
/// </summary>
public interface IRegexValidationService
{
    /// <summary>
    /// Validates a regex pattern for correctness and ReDoS vulnerabilities.
    /// </summary>
    /// <param name="pattern">The regex pattern to validate</param>
    /// <returns>Validation result indicating whether the pattern is valid and any error message</returns>
    RegexValidationResult ValidateRegex(string pattern);
}

/// <summary>
/// Result of regex pattern validation.
/// </summary>
/// <param name="IsValid">True if the pattern is valid and safe to use; false otherwise</param>
/// <param name="ErrorMessage">User-friendly error message if validation failed; null if valid</param>
public record RegexValidationResult(bool IsValid, string? ErrorMessage);
