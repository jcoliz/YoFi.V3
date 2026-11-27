using Moq;
using NUnit.Framework;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Services;
using YoFi.V3.Application.Validation;

namespace YoFi.V3.Tests.Unit.Application.PayeeRules;

/// <summary>
/// Unit tests for PayeeMatchingRuleEditDtoValidator.
/// </summary>
/// <remarks>
/// Tests verify validation rules per DESIGN-PAYEE-RULES-STORIES-1-2.md:
/// - PayeePattern is required and max 200 characters
/// - Category is required, cannot be whitespace, max 200 characters
/// - When PayeeIsRegex is true, validates regex pattern via IRegexValidationService
/// </remarks>
[TestFixture]
public class PayeeMatchingRuleEditDtoValidatorTests
{
    private Mock<IRegexValidationService> _mockRegexService = null!;
    private PayeeMatchingRuleEditDtoValidator _validator = null!;

    [SetUp]
    public void SetUp()
    {
        _mockRegexService = new Mock<IRegexValidationService>();
        _validator = new PayeeMatchingRuleEditDtoValidator(_mockRegexService.Object);
    }

    #region PayeePattern Validation Tests

    [Test]
    public void Validate_ValidPayeePattern_Passes()
    {
        // Given: Valid DTO with valid payee pattern
        var dto = new PayeeMatchingRuleEditDto("Amazon", false, "Shopping");

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_EmptyPayeePattern_Fails()
    {
        // Given: DTO with empty payee pattern
        var dto = new PayeeMatchingRuleEditDto("", false, "Shopping");

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should fail
        Assert.That(result.IsValid, Is.False);

        // And: Error should mention payee pattern is required
        Assert.That(result.Errors, Has.Count.EqualTo(1));
        Assert.That(result.Errors[0].PropertyName, Is.EqualTo(nameof(PayeeMatchingRuleEditDto.PayeePattern)));
        Assert.That(result.Errors[0].ErrorMessage, Does.Contain("required"));
    }

    [Test]
    public void Validate_PayeePatternTooLong_Fails()
    {
        // Given: DTO with payee pattern exceeding 200 characters
        var longPattern = new string('A', 201);
        var dto = new PayeeMatchingRuleEditDto(longPattern, false, "Shopping");

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should fail
        Assert.That(result.IsValid, Is.False);

        // And: Error should mention maximum length
        Assert.That(result.Errors, Has.Count.EqualTo(1));
        Assert.That(result.Errors[0].PropertyName, Is.EqualTo(nameof(PayeeMatchingRuleEditDto.PayeePattern)));
        Assert.That(result.Errors[0].ErrorMessage, Does.Contain("200 characters"));
    }

    [Test]
    public void Validate_PayeePatternExactly200Characters_Passes()
    {
        // Given: DTO with payee pattern exactly at 200 character limit
        var pattern = new string('A', 200);
        var dto = new PayeeMatchingRuleEditDto(pattern, false, "Shopping");

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    #endregion

    #region Category Validation Tests

    [Test]
    public void Validate_EmptyCategory_Fails()
    {
        // Given: DTO with empty category
        var dto = new PayeeMatchingRuleEditDto("Amazon", false, "");

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should fail
        Assert.That(result.IsValid, Is.False);

        // And: Error should mention category is required
        var categoryErrors = result.Errors.Where(e => e.PropertyName == nameof(PayeeMatchingRuleEditDto.Category)).ToList();
        Assert.That(categoryErrors, Has.Count.GreaterThan(0));
        Assert.That(categoryErrors[0].ErrorMessage, Does.Contain("required"));
    }

    [Test]
    public void Validate_WhitespaceOnlyCategory_Fails()
    {
        // Given: DTO with whitespace-only category
        var dto = new PayeeMatchingRuleEditDto("Amazon", false, "   ");

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should fail
        Assert.That(result.IsValid, Is.False);

        // And: Error should mention whitespace not allowed
        var categoryErrors = result.Errors.Where(e => e.PropertyName == nameof(PayeeMatchingRuleEditDto.Category)).ToList();
        Assert.That(categoryErrors, Has.Count.GreaterThan(0));
        Assert.That(categoryErrors.Any(e => e.ErrorMessage.Contains("whitespace")), Is.True);
    }

    [Test]
    public void Validate_CategoryTooLong_Fails()
    {
        // Given: DTO with category exceeding 200 characters
        var longCategory = new string('A', 201);
        var dto = new PayeeMatchingRuleEditDto("Amazon", false, longCategory);

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should fail
        Assert.That(result.IsValid, Is.False);

        // And: Error should mention maximum length
        var categoryErrors = result.Errors.Where(e => e.PropertyName == nameof(PayeeMatchingRuleEditDto.Category)).ToList();
        Assert.That(categoryErrors, Has.Count.GreaterThan(0));
        Assert.That(categoryErrors.Any(e => e.ErrorMessage.Contains("200 characters")), Is.True);
    }

    [Test]
    public void Validate_CategoryExactly200Characters_Passes()
    {
        // Given: DTO with category exactly at 200 character limit
        var category = new string('A', 200);
        var dto = new PayeeMatchingRuleEditDto("Amazon", false, category);

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_CategoryWithLeadingTrailingSpaces_Passes()
    {
        // Given: DTO with category that has leading/trailing spaces (will be sanitized later)
        var dto = new PayeeMatchingRuleEditDto("Amazon", false, " Shopping ");

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass (sanitization happens at save, not validation)
        Assert.That(result.IsValid, Is.True);
    }

    #endregion

    #region Regex Pattern Validation Tests

    [Test]
    public void Validate_RegexPatternValid_Passes()
    {
        // Given: DTO with valid regex pattern
        _mockRegexService.Setup(s => s.ValidateRegex("^AMZN.*"))
            .Returns(new RegexValidationResult(true, null));
        var dto = new PayeeMatchingRuleEditDto("^AMZN.*", true, "Shopping");

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);

        // And: Regex validation service should be called
        _mockRegexService.Verify(s => s.ValidateRegex("^AMZN.*"), Times.Once);
    }

    [Test]
    public void Validate_RegexPatternInvalid_Fails()
    {
        // Given: DTO with invalid regex pattern
        _mockRegexService.Setup(s => s.ValidateRegex("(?<invalid"))
            .Returns(new RegexValidationResult(false, "Invalid regex pattern: Unterminated group"));
        var dto = new PayeeMatchingRuleEditDto("(?<invalid", true, "Shopping");

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should fail
        Assert.That(result.IsValid, Is.False);

        // And: Error should contain regex validation error message
        var patternErrors = result.Errors.Where(e => e.PropertyName == nameof(PayeeMatchingRuleEditDto.PayeePattern)).ToList();
        Assert.That(patternErrors, Has.Count.EqualTo(1));
        Assert.That(patternErrors[0].ErrorMessage, Does.Contain("Invalid regex pattern"));
        Assert.That(patternErrors[0].ErrorMessage, Does.Contain("Unterminated group"));
    }

    [Test]
    public void Validate_RegexPatternWithUnsupportedFeature_Fails()
    {
        // Given: DTO with regex using unsupported feature (backreference)
        _mockRegexService.Setup(s => s.ValidateRegex(@"(\w+)\s+\1"))
            .Returns(new RegexValidationResult(false, "Pattern uses features not supported by the ReDoS-safe regex engine"));
        var dto = new PayeeMatchingRuleEditDto(@"(\w+)\s+\1", true, "Shopping");

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should fail
        Assert.That(result.IsValid, Is.False);

        // And: Error should mention ReDoS-safe engine
        var patternErrors = result.Errors.Where(e => e.PropertyName == nameof(PayeeMatchingRuleEditDto.PayeePattern)).ToList();
        Assert.That(patternErrors, Has.Count.EqualTo(1));
        Assert.That(patternErrors[0].ErrorMessage, Does.Contain("ReDoS-safe"));
    }

    [Test]
    public void Validate_SubstringPatternNotRegex_SkipsRegexValidation()
    {
        // Given: DTO with substring pattern (PayeeIsRegex = false)
        var dto = new PayeeMatchingRuleEditDto("Amazon", false, "Shopping");

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);

        // And: Regex validation service should NOT be called
        _mockRegexService.Verify(s => s.ValidateRegex(It.IsAny<string>()), Times.Never);
    }

    #endregion

    #region Combined Validation Tests

    [Test]
    public void Validate_MultipleFieldsInvalid_ReturnsAllErrors()
    {
        // Given: DTO with multiple invalid fields
        var dto = new PayeeMatchingRuleEditDto("", false, "");

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should fail
        Assert.That(result.IsValid, Is.False);

        // And: Should have errors for both fields
        Assert.That(result.Errors, Has.Count.GreaterThanOrEqualTo(2));
        Assert.That(result.Errors.Any(e => e.PropertyName == nameof(PayeeMatchingRuleEditDto.PayeePattern)), Is.True);
        Assert.That(result.Errors.Any(e => e.PropertyName == nameof(PayeeMatchingRuleEditDto.Category)), Is.True);
    }

    [Test]
    public void Validate_AllFieldsValid_Passes()
    {
        // Given: DTO with all valid fields
        var dto = new PayeeMatchingRuleEditDto("Amazon", false, "Shopping");

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Errors, Is.Empty);
    }

    [Test]
    public void Validate_AllFieldsValidWithRegex_Passes()
    {
        // Given: DTO with all valid fields including valid regex
        _mockRegexService.Setup(s => s.ValidateRegex("^AMZN.*"))
            .Returns(new RegexValidationResult(true, null));
        var dto = new PayeeMatchingRuleEditDto("^AMZN.*", true, "Shopping");

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.Errors, Is.Empty);
    }

    #endregion

    #region Edge Cases

    [Test]
    public void Validate_CategoryWithColons_Passes()
    {
        // Given: DTO with hierarchical category (will be sanitized later)
        var dto = new PayeeMatchingRuleEditDto("Amazon", false, "Shopping:Online:Amazon");

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_SpecialCharactersInPattern_Passes()
    {
        // Given: DTO with special characters in substring pattern
        var dto = new PayeeMatchingRuleEditDto("Amazon.com", false, "Shopping");

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_UnicodeCharacters_Passes()
    {
        // Given: DTO with Unicode characters
        var dto = new PayeeMatchingRuleEditDto("Café", false, "Dining");

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_NumericPattern_Passes()
    {
        // Given: DTO with numeric pattern
        var dto = new PayeeMatchingRuleEditDto("1234", false, "Account");

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    #endregion

    #region Real-World Scenarios

    [Test]
    public void Validate_RealWorldAmazonRule_Passes()
    {
        // Given: Real-world DTO for Amazon rule
        _mockRegexService.Setup(s => s.ValidateRegex("(amazon|amzn|aws)"))
            .Returns(new RegexValidationResult(true, null));
        var dto = new PayeeMatchingRuleEditDto("(amazon|amzn|aws)", true, "Shopping:Online");

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_RealWorldCoffeeshopRule_Passes()
    {
        // Given: Real-world DTO for coffee shop rule
        var dto = new PayeeMatchingRuleEditDto("Starbucks", false, "Dining:Coffee");

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_RealWorldUtilityRule_Passes()
    {
        // Given: Real-world DTO for utility bill rule
        _mockRegexService.Setup(s => s.ValidateRegex("^(PGE|SCE|SDGE).*"))
            .Returns(new RegexValidationResult(true, null));
        var dto = new PayeeMatchingRuleEditDto("^(PGE|SCE|SDGE).*", true, "Utilities:Electric");

        // When: DTO is validated
        var result = _validator.Validate(dto);

        // Then: Validation should pass
        Assert.That(result.IsValid, Is.True);
    }

    #endregion
}
