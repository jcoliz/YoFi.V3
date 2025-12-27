using System.Text.RegularExpressions;

namespace YoFi.V3.Application.Helpers;

/// <summary>
/// Helper methods for category processing and sanitization.
/// </summary>
/// <remarks>
/// Categories are free-text strings that can optionally use ':' separators to define hierarchies.
/// All categories are sanitized before saving to ensure consistent formatting and valid structure.
/// See PRD-TRANSACTION-SPLITS.md (lines 155-200) for complete sanitization rules.
/// </remarks>
public static partial class CategoryHelper
{
    /// <summary>
    /// Sanitizes a category string according to YoFi category rules.
    /// </summary>
    /// <param name="category">The raw category input (can be null or whitespace).</param>
    /// <returns>A sanitized category string, or empty string if input is null/whitespace.</returns>
    /// <remarks>
    /// Sanitization rules:
    /// - Trims leading/trailing whitespace
    /// - Consolidates multiple spaces to single space
    /// - Capitalizes all words (first letter of each word)
    /// - Removes whitespace around ':' separator
    /// - Removes empty terms after splitting by ':'
    /// - Returns empty string for null/whitespace input
    ///
    /// Examples:
    /// - "homeAndGarden" → "HomeAndGarden"
    /// - "Home    and Garden" → "Home And Garden"
    /// - "Home :Garden" → "Home:Garden"
    /// - "Home: " → "Home"
    /// - "  " → ""
    /// </remarks>
    public static string SanitizeCategory(string? category)
    {
        // Handle null or whitespace input
        if (string.IsNullOrWhiteSpace(category))
        {
            return string.Empty;
        }

        // Split by ':' separator to process hierarchy terms individually
        var terms = category.Split(':', StringSplitOptions.None);

        // Process each term
        var sanitizedTerms = new List<string>();
        foreach (var term in terms)
        {
            // Trim whitespace from term
            var trimmed = term.Trim();

            // Skip empty terms (e.g., from "::" or "Home:" or ":Home")
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            // Consolidate multiple spaces to single space
            var consolidated = MultipleSpacesRegex().Replace(trimmed, " ");

            // Capitalize all words (including "small words" for consistency)
            var capitalized = CapitalizeAllWords(consolidated);

            sanitizedTerms.Add(capitalized);
        }

        // Join terms back together with ':' separator
        return string.Join(":", sanitizedTerms);
    }

    /// <summary>
    /// Capitalizes the first letter of each word in a string.
    /// </summary>
    /// <param name="input">The string to capitalize.</param>
    /// <returns>String with all words capitalized.</returns>
    private static string CapitalizeAllWords(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var words = input.Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0)
            {
                // Capitalize first character, preserve rest of word casing
                words[i] = char.ToUpper(words[i][0]) + words[i].Substring(1);
            }
        }

        return string.Join(" ", words);
    }

    /// <summary>
    /// Regex pattern to match multiple consecutive spaces.
    /// </summary>
    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex MultipleSpacesRegex();
}
