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

    [Test]
    public void SanitizeCategory_Null_ReturnsEmpty()
    {
        // Given: Null category input

        // When: Category is sanitized
        var result = CategoryHelper.SanitizeCategory(null);

        // Then: Empty string should be returned
        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [Test]
    public void SanitizeCategory_EmptyString_ReturnsEmpty()
    {
        // Given: Empty string category input

        // When: Category is sanitized
        var result = CategoryHelper.SanitizeCategory(string.Empty);

        // Then: Empty string should be returned
        Assert.That(result, Is.EqualTo(string.Empty));
    }

    [TestCase(" ")]
    [TestCase("  ")]
    [TestCase("\t")]
    [TestCase("\n")]
    [TestCase(" \t\n ")]
    public void SanitizeCategory_WhitespaceOnly_ReturnsEmpty(string whitespace)
    {
        // Given: Whitespace-only category input

        // When: Category is sanitized
        var result = CategoryHelper.SanitizeCategory(whitespace);

        // Then: Empty string should be returned
        Assert.That(result, Is.EqualTo(string.Empty));
    }

    #endregion

    #region Trimming Tests

    [TestCase(" Home", "Home")]
    [TestCase("Home ", "Home")]
    [TestCase(" Home ", "Home")]
    [TestCase("  Home  ", "Home")]
    public void SanitizeCategory_LeadingOrTrailingWhitespace_TrimsWhitespace(string input, string expected)
    {
        // Given: Category with leading or trailing whitespace

        // When: Category is sanitized
        var result = CategoryHelper.SanitizeCategory(input);

        // Then: Whitespace should be trimmed
        Assert.That(result, Is.EqualTo(expected));
    }

    #endregion

    #region Space Consolidation Tests

    [TestCase("Home  Garden", "Home Garden")]
    [TestCase("Home   Garden", "Home Garden")]
    [TestCase("Home    Garden", "Home Garden")]
    [TestCase("Home     Garden", "Home Garden")]
    public void SanitizeCategory_MultipleSpaces_ConsolidatesToSingleSpace(string input, string expected)
    {
        // Given: Category with multiple consecutive spaces

        // When: Category is sanitized
        var result = CategoryHelper.SanitizeCategory(input);

        // Then: Multiple spaces should be consolidated to single space
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void SanitizeCategory_MultipleSpacesInMultipleLocations_ConsolidatesAll()
    {
        // Given: Category with multiple space sequences

        // When: Category is sanitized
        var result = CategoryHelper.SanitizeCategory("Home    And    Garden");

        // Then: All multiple spaces should be consolidated
        Assert.That(result, Is.EqualTo("Home And Garden"));
    }

    #endregion

    #region Capitalization Tests

    [TestCase("home", "Home")]
    [TestCase("HOME", "HOME")]
    [TestCase("Home", "Home")]
    public void SanitizeCategory_SingleWord_CapitalizesFirstLetter(string input, string expected)
    {
        // Given: Single word category with various casing

        // When: Category is sanitized
        var result = CategoryHelper.SanitizeCategory(input);

        // Then: First letter should be capitalized, rest preserved
        Assert.That(result, Is.EqualTo(expected));
    }

    [TestCase("home garden", "Home Garden")]
    [TestCase("home and garden", "Home And Garden")]
    [TestCase("HOME AND GARDEN", "HOME AND GARDEN")]
    public void SanitizeCategory_MultipleWords_CapitalizesEachWord(string input, string expected)
    {
        // Given: Multiple word category with various casing

        // When: Category is sanitized
        var result = CategoryHelper.SanitizeCategory(input);

        // Then: First letter of each word should be capitalized
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void SanitizeCategory_CamelCase_CapitalizesFirstLetter()
    {
        // Given: CamelCase category input

        // When: Category is sanitized
        var result = CategoryHelper.SanitizeCategory("homeAndGarden");

        // Then: First letter should be capitalized, rest preserved
        Assert.That(result, Is.EqualTo("HomeAndGarden"));
    }

    #endregion

    #region Colon Separator Normalization Tests

    [TestCase("Home : Garden", "Home:Garden")]
    [TestCase("Home  :  Garden", "Home:Garden")]
    [TestCase("Home:Garden", "Home:Garden")]
    [TestCase("Home :Garden", "Home:Garden")]
    [TestCase("Home: Garden", "Home:Garden")]
    public void SanitizeCategory_ColonWithWhitespace_RemovesWhitespaceAroundColon(string input, string expected)
    {
        // Given: Category with whitespace around colon separator

        // When: Category is sanitized
        var result = CategoryHelper.SanitizeCategory(input);

        // Then: Whitespace around colon should be removed
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void SanitizeCategory_MultipleColonTerms_NormalizesAll()
    {
        // Given: Category with multiple colon-separated terms and whitespace

        // When: Category is sanitized
        var result = CategoryHelper.SanitizeCategory("Home : Garden : Flowers");

        // Then: All colons should be normalized
        Assert.That(result, Is.EqualTo("Home:Garden:Flowers"));
    }

    #endregion

    #region Empty Term Removal Tests

    [TestCase("Home::", "Home")]
    [TestCase("::Home", "Home")]
    [TestCase("Home::Garden", "Home:Garden")]
    public void SanitizeCategory_DoubleColons_RemovesEmptyTerms(string input, string expected)
    {
        // Given: Category with double colons (empty terms)

        // When: Category is sanitized
        var result = CategoryHelper.SanitizeCategory(input);

        // Then: Empty terms should be removed
        Assert.That(result, Is.EqualTo(expected));
    }

    [TestCase("Home: ", "Home")]
    [TestCase(" :Garden", "Garden")]
    [TestCase("Home: :Garden", "Home:Garden")]
    public void SanitizeCategory_ColonWithWhitespaceOnly_RemovesEmptyTerms(string input, string expected)
    {
        // Given: Category with colon followed/preceded by whitespace only

        // When: Category is sanitized
        var result = CategoryHelper.SanitizeCategory(input);

        // Then: Empty terms should be removed
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void SanitizeCategory_OnlyColons_ReturnsEmpty()
    {
        // Given: Category with only colons and whitespace

        // When: Category is sanitized
        var result = CategoryHelper.SanitizeCategory(":::");

        // Then: Empty string should be returned
        Assert.That(result, Is.EqualTo(string.Empty));
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
