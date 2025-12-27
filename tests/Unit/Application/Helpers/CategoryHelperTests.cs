using NUnit.Framework;
using YoFi.V3.Application.Helpers;

namespace YoFi.V3.Tests.Unit.Application.Helpers;

/// <summary>
/// Unit tests for CategoryHelper sanitization logic.
/// </summary>
/// <remarks>
/// Tests verify category sanitization rules per PRD-TRANSACTION-SPLITS.md (lines 155-200).
/// </remarks>
[TestFixture]
public class CategoryHelperTests
{
    #region Null/Empty/Whitespace Tests

    [TestCase(null, "")]
    [TestCase("", "")]
    [TestCase(" ", "")]
    [TestCase("  ", "")]
    [TestCase("\t", "")]
    [TestCase("\n", "")]
    [TestCase(" \t\n ", "")]
    public void SanitizeCategory_NullEmptyOrWhitespace_ReturnsEmpty(string? input, string expected)
    {
        // Given: Null, empty, or whitespace-only category input

        // When: Category is sanitized
        var result = CategoryHelper.SanitizeCategory(input);

        // Then: Empty string should be returned
        Assert.That(result, Is.EqualTo(expected));
    }

    #endregion

    #region Basic Sanitization Tests

    // Trimming
    [TestCase(" Home", "Home")]
    [TestCase("Home ", "Home")]
    [TestCase(" Home ", "Home")]
    [TestCase("  Home  ", "Home")]
    // Space consolidation
    [TestCase("Home  Garden", "Home Garden")]
    [TestCase("Home   Garden", "Home Garden")]
    [TestCase("Home    Garden", "Home Garden")]
    [TestCase("Home     Garden", "Home Garden")]
    [TestCase("Home    And    Garden", "Home And Garden")]
    // Capitalization - single word
    [TestCase("home", "Home")]
    [TestCase("HOME", "HOME")]
    [TestCase("Home", "Home")]
    [TestCase("homeAndGarden", "HomeAndGarden")]
    // Capitalization - multiple words
    [TestCase("home garden", "Home Garden")]
    [TestCase("home and garden", "Home And Garden")]
    [TestCase("HOME AND GARDEN", "HOME AND GARDEN")]
    // Colon normalization
    [TestCase("Home : Garden", "Home:Garden")]
    [TestCase("Home  :  Garden", "Home:Garden")]
    [TestCase("Home:Garden", "Home:Garden")]
    [TestCase("Home :Garden", "Home:Garden")]
    [TestCase("Home: Garden", "Home:Garden")]
    [TestCase("Home : Garden : Flowers", "Home:Garden:Flowers")]
    // Empty term removal
    [TestCase("Home::", "Home")]
    [TestCase("::Home", "Home")]
    [TestCase("Home::Garden", "Home:Garden")]
    [TestCase("Home: ", "Home")]
    [TestCase(" :Garden", "Garden")]
    [TestCase("Home: :Garden", "Home:Garden")]
    [TestCase(":::", "")]
    public void SanitizeCategory_ValidInput_AppliesSanitizationRules(string input, string expected)
    {
        // Given: Category input requiring sanitization

        // When: Category is sanitized
        var result = CategoryHelper.SanitizeCategory(input);

        // Then: Appropriate sanitization rules should be applied
        Assert.That(result, Is.EqualTo(expected));
    }

    #endregion

    #region Complex Scenario Tests

    [Test]
    public void SanitizeCategory_ComplexWhitespaceAndColons_AppliesAllRules()
    {
        // Given: Category with leading/trailing whitespace, multiple spaces, and colon issues

        // When: Category is sanitized
        var result = CategoryHelper.SanitizeCategory(" home : : garden ");

        // Then: All rules should be applied: trim, consolidate, capitalize, normalize colons, remove empty
        Assert.That(result, Is.EqualTo("Home:Garden"));
    }

    [Test]
    public void SanitizeCategory_ComplexMultipleSpaces_AppliesAllRules()
    {
        // Given: Category with multiple spaces between words

        // When: Category is sanitized
        var result = CategoryHelper.SanitizeCategory("  home  and  garden  ");

        // Then: All rules should be applied: trim, consolidate, capitalize
        Assert.That(result, Is.EqualTo("Home And Garden"));
    }

    [Test]
    public void SanitizeCategory_ComplexHierarchyWithCapitalization_AppliesAllRules()
    {
        // Given: Category hierarchy with lowercase words and spacing issues

        // When: Category is sanitized
        var result = CategoryHelper.SanitizeCategory("home improvement:kitchen:appliances");

        // Then: All rules should be applied: capitalize all words in all terms
        Assert.That(result, Is.EqualTo("Home Improvement:Kitchen:Appliances"));
    }

    [Test]
    public void SanitizeCategory_ComplexMixedCasing_PreservesNonFirstLetters()
    {
        // Given: Category with mixed casing (e.g., acronyms or special names)

        // When: Category is sanitized
        var result = CategoryHelper.SanitizeCategory("hoME:garDEN");

        // Then: First letters capitalized, rest preserved
        Assert.That(result, Is.EqualTo("HoME:GarDEN"));
    }

    [Test]
    public void SanitizeCategory_RealWorldExample_HandlesCorrectly()
    {
        // Given: Real-world messy category input

        // When: Category is sanitized
        var result = CategoryHelper.SanitizeCategory("  home   :  garden  and  yard  : : flowers  ");

        // Then: All rules should produce clean output
        Assert.That(result, Is.EqualTo("Home:Garden And Yard:Flowers"));
    }

    #endregion
}
