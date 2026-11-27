using NUnit.Framework;
using YoFi.V3.Application.Services;

namespace YoFi.V3.Tests.Unit.Application.PayeeRules;

/// <summary>
/// Unit tests for RegexValidationService.
/// </summary>
/// <remarks>
/// Tests verify regex validation logic per DESIGN-PAYEE-RULES-STORIES-1-2.md:
/// - Valid patterns are accepted
/// - Invalid syntax is rejected with error message
/// - Unsupported features (backreferences, lookahead/lookbehind) are rejected
/// - NonBacktracking engine provides ReDoS protection
/// </remarks>
[TestFixture]
public class RegexValidationServiceTests
{
    private RegexValidationService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new RegexValidationService();
    }

    #region Valid Pattern Tests

    [Test]
    public void ValidateRegex_SimplePattern_ReturnsValid()
    {
        // Given: Simple valid regex pattern
        var pattern = "Amazon";

        // When: Pattern is validated
        var result = _service.ValidateRegex(pattern);

        // Then: Should be valid
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.ErrorMessage, Is.Null);
    }

    [Test]
    public void ValidateRegex_PatternWithWildcard_ReturnsValid()
    {
        // Given: Pattern with wildcard
        var pattern = "^AMZN.*";

        // When: Pattern is validated
        var result = _service.ValidateRegex(pattern);

        // Then: Should be valid
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.ErrorMessage, Is.Null);
    }

    [Test]
    public void ValidateRegex_PatternWithAlternation_ReturnsValid()
    {
        // Given: Pattern with alternation (OR)
        var pattern = "(amazon|amzn|aws)";

        // When: Pattern is validated
        var result = _service.ValidateRegex(pattern);

        // Then: Should be valid
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.ErrorMessage, Is.Null);
    }

    [Test]
    public void ValidateRegex_PatternWithCharacterClass_ReturnsValid()
    {
        // Given: Pattern with character class
        var pattern = @"\d{4}-\d{2}-\d{2}";

        // When: Pattern is validated
        var result = _service.ValidateRegex(pattern);

        // Then: Should be valid
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.ErrorMessage, Is.Null);
    }

    [Test]
    public void ValidateRegex_PatternWithQuantifiers_ReturnsValid()
    {
        // Given: Pattern with various quantifiers
        var pattern = @"\w+@\w+\.\w{2,4}";

        // When: Pattern is validated
        var result = _service.ValidateRegex(pattern);

        // Then: Should be valid
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.ErrorMessage, Is.Null);
    }

    [Test]
    public void ValidateRegex_PatternWithAnchors_ReturnsValid()
    {
        // Given: Pattern with start/end anchors
        var pattern = "^Amazon$";

        // When: Pattern is validated
        var result = _service.ValidateRegex(pattern);

        // Then: Should be valid
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.ErrorMessage, Is.Null);
    }

    [Test]
    public void ValidateRegex_ComplexValidPattern_ReturnsValid()
    {
        // Given: Complex but valid pattern
        var pattern = @"^(AMZN|Amazon).*(Marketplace|Prime)?.*";

        // When: Pattern is validated
        var result = _service.ValidateRegex(pattern);

        // Then: Should be valid
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.ErrorMessage, Is.Null);
    }

    #endregion

    #region Invalid Syntax Tests

    [Test]
    public void ValidateRegex_EmptyPattern_ReturnsInvalid()
    {
        // Given: Empty pattern
        var pattern = "";

        // When: Pattern is validated
        var result = _service.ValidateRegex(pattern);

        // Then: Should be invalid
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("empty or whitespace"));
    }

    [Test]
    public void ValidateRegex_WhitespacePattern_ReturnsInvalid()
    {
        // Given: Whitespace-only pattern
        var pattern = "   ";

        // When: Pattern is validated
        var result = _service.ValidateRegex(pattern);

        // Then: Should be invalid
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("empty or whitespace"));
    }

    [Test]
    public void ValidateRegex_NullPattern_ReturnsInvalid()
    {
        // Given: Null pattern
        string pattern = null!;

        // When: Pattern is validated
        var result = _service.ValidateRegex(pattern);

        // Then: Should be invalid
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("empty or whitespace"));
    }

    [Test]
    public void ValidateRegex_UnmatchedParenthesis_ReturnsInvalidWithMessage()
    {
        // Given: Pattern with unmatched opening parenthesis
        var pattern = "(?<invalid";

        // When: Pattern is validated
        var result = _service.ValidateRegex(pattern);

        // Then: Should be invalid with error message
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("Invalid regex pattern"));
    }

    [Test]
    public void ValidateRegex_UnmatchedBracket_ReturnsInvalidWithMessage()
    {
        // Given: Pattern with unmatched bracket
        var pattern = "[abc";

        // When: Pattern is validated
        var result = _service.ValidateRegex(pattern);

        // Then: Should be invalid with error message
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("Invalid regex pattern"));
    }

    [Test]
    public void ValidateRegex_InvalidEscape_ReturnsInvalidWithMessage()
    {
        // Given: Pattern with invalid escape sequence
        var pattern = @"\k";

        // When: Pattern is validated
        var result = _service.ValidateRegex(pattern);

        // Then: Should be invalid with error message
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("Invalid regex pattern"));
    }

    [Test]
    public void ValidateRegex_InvalidQuantifierRange_ReturnsInvalidWithMessage()
    {
        // Given: Pattern with invalid quantifier (max < min)
        var pattern = "a{5,2}";

        // When: Pattern is validated
        var result = _service.ValidateRegex(pattern);

        // Then: Should be invalid with error message
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("Invalid regex pattern"));
    }

    #endregion

    #region Unsupported Features (NonBacktracking) Tests

    [Test]
    public void ValidateRegex_Backreference_ReturnsInvalidWithMessage()
    {
        // Given: Pattern with backreference (not supported by NonBacktracking)
        var pattern = @"(\w+)\s+\1";

        // When: Pattern is validated
        var result = _service.ValidateRegex(pattern);

        // Then: Should be invalid with ReDoS-safe message
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("ReDoS-safe"));
        Assert.That(result.ErrorMessage, Does.Contain("backreferences"));
    }

    [Test]
    public void ValidateRegex_PositiveLookahead_ReturnsInvalidWithMessage()
    {
        // Given: Pattern with positive lookahead (not supported by NonBacktracking)
        var pattern = @"test(?=\d)";

        // When: Pattern is validated
        var result = _service.ValidateRegex(pattern);

        // Then: Should be invalid with ReDoS-safe message
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("ReDoS-safe"));
        Assert.That(result.ErrorMessage, Does.Contain("lookahead"));
    }

    [Test]
    public void ValidateRegex_NegativeLookahead_ReturnsInvalidWithMessage()
    {
        // Given: Pattern with negative lookahead (not supported by NonBacktracking)
        var pattern = @"test(?!\d)";

        // When: Pattern is validated
        var result = _service.ValidateRegex(pattern);

        // Then: Should be invalid with ReDoS-safe message
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("ReDoS-safe"));
        Assert.That(result.ErrorMessage, Does.Contain("lookahead"));
    }

    [Test]
    public void ValidateRegex_PositiveLookbehind_ReturnsInvalidWithMessage()
    {
        // Given: Pattern with positive lookbehind (not supported by NonBacktracking)
        var pattern = @"(?<=\d)test";

        // When: Pattern is validated
        var result = _service.ValidateRegex(pattern);

        // Then: Should be invalid with ReDoS-safe message
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("ReDoS-safe"));
        Assert.That(result.ErrorMessage, Does.Contain("lookbehind"));
    }

    [Test]
    public void ValidateRegex_NegativeLookbehind_ReturnsInvalidWithMessage()
    {
        // Given: Pattern with negative lookbehind (not supported by NonBacktracking)
        var pattern = @"(?<!\d)test";

        // When: Pattern is validated
        var result = _service.ValidateRegex(pattern);

        // Then: Should be invalid with ReDoS-safe message
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.ErrorMessage, Does.Contain("ReDoS-safe"));
        Assert.That(result.ErrorMessage, Does.Contain("lookbehind"));
    }

    #endregion

    #region Edge Cases

    [Test]
    public void ValidateRegex_VeryLongPattern_ReturnsValid()
    {
        // Given: Very long but valid pattern
        var longPattern = string.Join("|", Enumerable.Range(1, 50).Select(i => $"pattern{i}"));

        // When: Pattern is validated
        var result = _service.ValidateRegex(longPattern);

        // Then: Should be valid
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.ErrorMessage, Is.Null);
    }

    [Test]
    public void ValidateRegex_SpecialCharactersEscaped_ReturnsValid()
    {
        // Given: Pattern with properly escaped special characters
        var pattern = @"\$\d+\.\d{2}";

        // When: Pattern is validated
        var result = _service.ValidateRegex(pattern);

        // Then: Should be valid
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.ErrorMessage, Is.Null);
    }

    [Test]
    public void ValidateRegex_UnicodePattern_ReturnsValid()
    {
        // Given: Pattern with Unicode characters
        var pattern = @"Café|naïve";

        // When: Pattern is validated
        var result = _service.ValidateRegex(pattern);

        // Then: Should be valid
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.ErrorMessage, Is.Null);
    }

    #endregion

    #region Real-World Pattern Tests

    [Test]
    public void ValidateRegex_RealWorldAmazon_ReturnsValid()
    {
        // Given: Real-world pattern for Amazon variations
        var pattern = @"^(AMZN|Amazon|AWS).*";

        // When: Pattern is validated
        var result = _service.ValidateRegex(pattern);

        // Then: Should be valid
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.ErrorMessage, Is.Null);
    }

    [Test]
    public void ValidateRegex_RealWorldCreditCard_ReturnsValid()
    {
        // Given: Real-world pattern for credit card companies
        var pattern = @"(VISA|MASTERCARD|DISCOVER).*\d{4}";

        // When: Pattern is validated
        var result = _service.ValidateRegex(pattern);

        // Then: Should be valid
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.ErrorMessage, Is.Null);
    }

    [Test]
    public void ValidateRegex_RealWorldUtility_ReturnsValid()
    {
        // Given: Real-world pattern for utility bills
        var pattern = @"^(PGE|SCE|SDGE|PG&E).*";

        // When: Pattern is validated
        var result = _service.ValidateRegex(pattern);

        // Then: Should be valid
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.ErrorMessage, Is.Null);
    }

    #endregion
}
