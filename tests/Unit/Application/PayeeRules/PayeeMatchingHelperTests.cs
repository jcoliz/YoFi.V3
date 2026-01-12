using System.Text.RegularExpressions;
using NUnit.Framework;
using YoFi.V3.Application.Helpers;
using YoFi.V3.Entities.Models;

namespace YoFi.V3.Tests.Unit.Application.PayeeRules;

/// <summary>
/// Unit tests for PayeeMatchingHelper matching algorithm.
/// </summary>
/// <remarks>
/// Tests verify matching logic per DESIGN-PAYEE-RULES-STORIES-1-2.md:
/// - Substring matching (case-insensitive)
/// - Regex matching (case-insensitive, NonBacktracking)
/// - Conflict resolution: regex > substring, longer > shorter, newer > older
/// </remarks>
[TestFixture]
public class PayeeMatchingHelperTests
{
    #region Basic Substring Matching Tests

    [Test]
    public void FindBestMatch_SubstringExactMatch_ReturnsCategory()
    {
        // Given: Rule with substring pattern "Amazon"
        var rules = new List<PayeeMatchingRule>
        {
            CreateRule("Amazon", false, "Shopping", modifiedAt: DateTimeOffset.UtcNow)
        };

        // When: Payee matches exactly
        var result = PayeeMatchingHelper.FindBestMatch("Amazon", rules);

        // Then: Category should be returned
        Assert.That(result, Is.EqualTo("Shopping"));
    }

    [Test]
    public void FindBestMatch_SubstringCaseInsensitive_ReturnsCategory()
    {
        // Given: Rule with substring pattern "amazon"
        var rules = new List<PayeeMatchingRule>
        {
            CreateRule("amazon", false, "Shopping", modifiedAt: DateTimeOffset.UtcNow)
        };

        // When: Payee matches with different casing "AMAZON.COM"
        var result = PayeeMatchingHelper.FindBestMatch("AMAZON.COM", rules);

        // Then: Category should be returned (case-insensitive)
        Assert.That(result, Is.EqualTo("Shopping"));
    }

    [Test]
    public void FindBestMatch_SubstringPartialMatch_ReturnsCategory()
    {
        // Given: Rule with substring pattern "Amazon"
        var rules = new List<PayeeMatchingRule>
        {
            CreateRule("Amazon", false, "Shopping", modifiedAt: DateTimeOffset.UtcNow)
        };

        // When: Payee contains pattern as substring
        var result = PayeeMatchingHelper.FindBestMatch("Amazon.com Marketplace", rules);

        // Then: Category should be returned
        Assert.That(result, Is.EqualTo("Shopping"));
    }

    [Test]
    public void FindBestMatch_SubstringNoMatch_ReturnsNull()
    {
        // Given: Rule with substring pattern "Amazon"
        var rules = new List<PayeeMatchingRule>
        {
            CreateRule("Amazon", false, "Shopping", modifiedAt: DateTimeOffset.UtcNow)
        };

        // When: Payee does not contain pattern
        var result = PayeeMatchingHelper.FindBestMatch("Walmart", rules);

        // Then: Null should be returned
        Assert.That(result, Is.Null);
    }

    #endregion

    #region Basic Regex Matching Tests

    [Test]
    public void FindBestMatch_RegexExactMatch_ReturnsCategory()
    {
        // Given: Rule with regex pattern "^AMZN.*"
        var rules = new List<PayeeMatchingRule>
        {
            CreateRule("^AMZN.*", true, "Shopping", modifiedAt: DateTimeOffset.UtcNow)
        };

        // When: Payee matches regex
        var result = PayeeMatchingHelper.FindBestMatch("AMZN Marketplace", rules);

        // Then: Category should be returned
        Assert.That(result, Is.EqualTo("Shopping"));
    }

    [Test]
    public void FindBestMatch_RegexCaseInsensitive_ReturnsCategory()
    {
        // Given: Rule with regex pattern "^amzn.*"
        var rules = new List<PayeeMatchingRule>
        {
            CreateRule("^amzn.*", true, "Shopping", modifiedAt: DateTimeOffset.UtcNow)
        };

        // When: Payee matches with different casing
        var result = PayeeMatchingHelper.FindBestMatch("AMZN Marketplace", rules);

        // Then: Category should be returned (case-insensitive)
        Assert.That(result, Is.EqualTo("Shopping"));
    }

    [Test]
    public void FindBestMatch_RegexComplexPattern_ReturnsCategory()
    {
        // Given: Rule with complex regex pattern matching multiple variations
        var rules = new List<PayeeMatchingRule>
        {
            CreateRule("(amazon|amzn|aws)", true, "Shopping", modifiedAt: DateTimeOffset.UtcNow)
        };

        // When: Payee matches one variation
        var result = PayeeMatchingHelper.FindBestMatch("AWS Services", rules);

        // Then: Category should be returned
        Assert.That(result, Is.EqualTo("Shopping"));
    }

    [Test]
    public void FindBestMatch_RegexNoMatch_ReturnsNull()
    {
        // Given: Rule with regex pattern "^AMZN.*"
        var rules = new List<PayeeMatchingRule>
        {
            CreateRule("^AMZN.*", true, "Shopping", modifiedAt: DateTimeOffset.UtcNow)
        };

        // When: Payee does not match regex
        var result = PayeeMatchingHelper.FindBestMatch("Walmart", rules);

        // Then: Null should be returned
        Assert.That(result, Is.Null);
    }

    #endregion

    #region Conflict Resolution: Regex > Substring

    [Test]
    public void FindBestMatch_RegexBeatsSubstring_ReturnsRegexCategory()
    {
        // Given: Two rules - one substring, one regex (both match)
        var rules = new List<PayeeMatchingRule>
        {
            CreateRule("Amazon", false, "Online", modifiedAt: DateTimeOffset.UtcNow),
            CreateRule("^AMZN.*", true, "Shopping", modifiedAt: DateTimeOffset.UtcNow)
        };

        // When: Payee matches both rules
        var result = PayeeMatchingHelper.FindBestMatch("AMZN Marketplace", rules);

        // Then: Regex category should win
        Assert.That(result, Is.EqualTo("Shopping"));
    }

    [Test]
    public void FindBestMatch_RegexBeatsSubstring_DifferentOrder_ReturnsRegexCategory()
    {
        // Given: Two rules - regex first, then substring
        var rules = new List<PayeeMatchingRule>
        {
            CreateRule("^AMZN.*", true, "Shopping", modifiedAt: DateTimeOffset.UtcNow),
            CreateRule("Amazon", false, "Online", modifiedAt: DateTimeOffset.UtcNow)
        };

        // When: Payee matches both rules
        var result = PayeeMatchingHelper.FindBestMatch("AMZN Marketplace", rules);

        // Then: Regex category should win regardless of order
        Assert.That(result, Is.EqualTo("Shopping"));
    }

    #endregion

    #region Conflict Resolution: Longer Substring Wins

    [Test]
    public void FindBestMatch_LongerSubstringWins_ReturnsLongerCategory()
    {
        // Given: Two substring rules with different lengths
        var rules = new List<PayeeMatchingRule>
        {
            CreateRule("Amazon", false, "Online", modifiedAt: DateTimeOffset.UtcNow),
            CreateRule("Amazon Prime", false, "Subscription", modifiedAt: DateTimeOffset.UtcNow)
        };

        // When: Payee matches both patterns
        var result = PayeeMatchingHelper.FindBestMatch("Amazon Prime Video", rules);

        // Then: Longer pattern should win
        Assert.That(result, Is.EqualTo("Subscription"));
    }

    [Test]
    public void FindBestMatch_LongerSubstringWins_DifferentOrder_ReturnsLongerCategory()
    {
        // Given: Two substring rules - longer first
        var rules = new List<PayeeMatchingRule>
        {
            CreateRule("Amazon Prime", false, "Subscription", modifiedAt: DateTimeOffset.UtcNow),
            CreateRule("Amazon", false, "Online", modifiedAt: DateTimeOffset.UtcNow)
        };

        // When: Payee matches both patterns
        var result = PayeeMatchingHelper.FindBestMatch("Amazon Prime Video", rules);

        // Then: Longer pattern should win regardless of order
        Assert.That(result, Is.EqualTo("Subscription"));
    }

    [Test]
    public void FindBestMatch_OnlyShorterMatches_ReturnsShortCategory()
    {
        // Given: Two substring rules
        var rules = new List<PayeeMatchingRule>
        {
            CreateRule("Amazon", false, "Online", modifiedAt: DateTimeOffset.UtcNow),
            CreateRule("Amazon Prime", false, "Subscription", modifiedAt: DateTimeOffset.UtcNow)
        };

        // When: Payee only matches shorter pattern
        var result = PayeeMatchingHelper.FindBestMatch("Amazon.com", rules);

        // Then: Shorter pattern should be returned (only match)
        Assert.That(result, Is.EqualTo("Online"));
    }

    #endregion

    #region Conflict Resolution: Newer (ModifiedAt) Wins

    [Test]
    public void FindBestMatch_NewerRuleWins_EqualLengthSubstrings_ReturnsNewerCategory()
    {
        // Given: Two substring rules with equal length (pre-sorted by ModifiedAt DESC)
        var olderDate = DateTimeOffset.UtcNow.AddDays(-10);
        var newerDate = DateTimeOffset.UtcNow;
        var rules = new List<PayeeMatchingRule>
        {
            CreateRule("Amazon", false, "E-Commerce", modifiedAt: newerDate),  // First = newer
            CreateRule("Amazon", false, "Online", modifiedAt: olderDate)       // Second = older
        };

        // When: Payee matches both patterns
        var result = PayeeMatchingHelper.FindBestMatch("Amazon.com", rules);

        // Then: Newer rule should win
        Assert.That(result, Is.EqualTo("E-Commerce"));
    }

    [Test]
    public void FindBestMatch_NewerRegexWins_MultipleRegexMatch_ReturnsFirstRegex()
    {
        // Given: Two regex rules (pre-sorted by ModifiedAt DESC)
        var olderDate = DateTimeOffset.UtcNow.AddDays(-10);
        var newerDate = DateTimeOffset.UtcNow;
        var rules = new List<PayeeMatchingRule>
        {
            CreateRule("^AMZN.*", true, "Tech", modifiedAt: newerDate),      // First = newer
            CreateRule("Amazon", true, "Shopping", modifiedAt: olderDate)    // Second = older
        };

        // When: Payee matches both patterns
        var result = PayeeMatchingHelper.FindBestMatch("AMZN Marketplace", rules);

        // Then: First regex match should be returned (newer)
        Assert.That(result, Is.EqualTo("Tech"));
    }

    #endregion

    #region Edge Cases

    [Test]
    public void FindBestMatch_NullPayee_ReturnsNull()
    {
        // Given: Rules exist
        var rules = new List<PayeeMatchingRule>
        {
            CreateRule("Amazon", false, "Shopping", modifiedAt: DateTimeOffset.UtcNow)
        };

        // When: Payee is null
        var result = PayeeMatchingHelper.FindBestMatch(null!, rules);

        // Then: Null should be returned
        Assert.That(result, Is.Null);
    }

    [Test]
    public void FindBestMatch_EmptyPayee_ReturnsNull()
    {
        // Given: Rules exist
        var rules = new List<PayeeMatchingRule>
        {
            CreateRule("Amazon", false, "Shopping", modifiedAt: DateTimeOffset.UtcNow)
        };

        // When: Payee is empty string
        var result = PayeeMatchingHelper.FindBestMatch("", rules);

        // Then: Null should be returned
        Assert.That(result, Is.Null);
    }

    [Test]
    public void FindBestMatch_WhitespacePayee_ReturnsNull()
    {
        // Given: Rules exist
        var rules = new List<PayeeMatchingRule>
        {
            CreateRule("Amazon", false, "Shopping", modifiedAt: DateTimeOffset.UtcNow)
        };

        // When: Payee is whitespace only
        var result = PayeeMatchingHelper.FindBestMatch("   ", rules);

        // Then: Null should be returned
        Assert.That(result, Is.Null);
    }

    [Test]
    public void FindBestMatch_EmptyRuleList_ReturnsNull()
    {
        // Given: No rules
        var rules = new List<PayeeMatchingRule>();

        // When: Payee is valid
        var result = PayeeMatchingHelper.FindBestMatch("Amazon", rules);

        // Then: Null should be returned
        Assert.That(result, Is.Null);
    }

    [Test]
    public void FindBestMatch_NoMatchingRules_ReturnsNull()
    {
        // Given: Rules that don't match
        var rules = new List<PayeeMatchingRule>
        {
            CreateRule("Walmart", false, "Shopping", modifiedAt: DateTimeOffset.UtcNow),
            CreateRule("Target", false, "Shopping", modifiedAt: DateTimeOffset.UtcNow)
        };

        // When: Payee doesn't match any rule
        var result = PayeeMatchingHelper.FindBestMatch("Amazon", rules);

        // Then: Null should be returned
        Assert.That(result, Is.Null);
    }

    #endregion

    #region Complex Scenarios

    [Test]
    public void FindBestMatch_MultipleRulesComplexScenario_AppliesAllPrecedenceRules()
    {
        // Given: Multiple rules with different types and lengths
        var rules = new List<PayeeMatchingRule>
        {
            CreateRule("Store", false, "General", modifiedAt: DateTimeOffset.UtcNow.AddDays(-5)),
            CreateRule("Grocery Store", false, "Food", modifiedAt: DateTimeOffset.UtcNow.AddDays(-3)),
            CreateRule("^Safeway.*", true, "Safeway", modifiedAt: DateTimeOffset.UtcNow.AddDays(-1))
        };

        // When: Payee matches regex rule
        var result = PayeeMatchingHelper.FindBestMatch("Safeway Grocery Store", rules);

        // Then: Regex should win despite matching longer substring
        Assert.That(result, Is.EqualTo("Safeway"));
    }

    [Test]
    public void FindBestMatch_MultipleRulesOnlySubstringMatches_ReturnsLongest()
    {
        // Given: Multiple substring rules, no regex matches
        var rules = new List<PayeeMatchingRule>
        {
            CreateRule("Store", false, "General", modifiedAt: DateTimeOffset.UtcNow.AddDays(-5)),
            CreateRule("Grocery Store", false, "Food", modifiedAt: DateTimeOffset.UtcNow.AddDays(-3)),
            CreateRule("^Safeway.*", true, "Safeway", modifiedAt: DateTimeOffset.UtcNow.AddDays(-1))
        };

        // When: Payee matches only substring rules (not regex)
        var result = PayeeMatchingHelper.FindBestMatch("Target Grocery Store", rules);

        // Then: Longest substring should win
        Assert.That(result, Is.EqualTo("Food"));
    }

    #endregion

    #region Exception Handling

    [Test]
    public void FindBestMatch_InvalidRegexPattern_ThrowsRegexParseException()
    {
        // Given: Rule with invalid regex syntax
        var rules = new List<PayeeMatchingRule>
        {
            CreateRule("(?<invalid", true, "Shopping", modifiedAt: DateTimeOffset.UtcNow)
        };

        // When: Matching is attempted with invalid regex
        // Then: RegexParseException should be thrown (.NET 10+)
        Assert.Throws<RegexParseException>(() =>
            PayeeMatchingHelper.FindBestMatch("Amazon", rules));
    }

    [Test]
    public void FindBestMatch_UnsupportedRegexFeature_ThrowsNotSupportedException()
    {
        // Given: Rule with backreference (not supported by NonBacktracking)
        var rules = new List<PayeeMatchingRule>
        {
            CreateRule(@"(\w+)\s+\1", true, "Duplicate", modifiedAt: DateTimeOffset.UtcNow)
        };

        // When: Matching is attempted with unsupported regex feature
        // Then: NotSupportedException should be thrown
        Assert.Throws<NotSupportedException>(() =>
            PayeeMatchingHelper.FindBestMatch("test test", rules));
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a PayeeMatchingRule for testing.
    /// </summary>
    private static PayeeMatchingRule CreateRule(
        string pattern,
        bool isRegex,
        string category,
        DateTimeOffset modifiedAt)
    {
        return new PayeeMatchingRule
        {
            Id = 1,
            Key = Guid.NewGuid(),
            TenantId = 1,
            PayeePattern = pattern,
            PayeeIsRegex = isRegex,
            Category = category,
            CreatedAt = modifiedAt,
            ModifiedAt = modifiedAt,
            LastUsedAt = null,
            MatchCount = 0
        };
    }

    #endregion
}
