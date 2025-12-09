using System.ComponentModel.DataAnnotations;
using YoFi.V3.Application.Validation;

namespace YoFi.V3.Tests.Unit.Application.Validation;

public class NotWhiteSpaceAttributeTests
{
    [Test]
    public void NotWhiteSpace_WithValidString_IsValid()
    {
        // Given: A non-empty, non-whitespace string
        var attribute = new NotWhiteSpaceAttribute();
        var value = "Valid string";
        var context = new ValidationContext(new object()) { DisplayName = "TestProperty" };

        // When: Validating the string
        var result = attribute.GetValidationResult(value, context);

        // Then: Should be valid
        Assert.That(result, Is.EqualTo(ValidationResult.Success));
    }

    [Test]
    public void NotWhiteSpace_WithEmptyString_IsInvalid()
    {
        // Given: An empty string
        var attribute = new NotWhiteSpaceAttribute();
        var value = "";
        var context = new ValidationContext(new object()) { DisplayName = "TestProperty" };

        // When: Validating the empty string
        var result = attribute.GetValidationResult(value, context);

        // Then: Should be invalid
        Assert.That(result, Is.Not.EqualTo(ValidationResult.Success));
        Assert.That(result?.ErrorMessage, Does.Contain("cannot be empty or whitespace"));
    }

    [Test]
    public void NotWhiteSpace_WithWhitespaceOnly_IsInvalid()
    {
        // Given: A string containing only whitespace
        var attribute = new NotWhiteSpaceAttribute();
        var value = "   ";
        var context = new ValidationContext(new object()) { DisplayName = "TestProperty" };

        // When: Validating the whitespace string
        var result = attribute.GetValidationResult(value, context);

        // Then: Should be invalid
        Assert.That(result, Is.Not.EqualTo(ValidationResult.Success));
        Assert.That(result?.ErrorMessage, Does.Contain("cannot be empty or whitespace"));
    }

    [Test]
    public void NotWhiteSpace_WithTabsAndNewlines_IsInvalid()
    {
        // Given: A string with tabs and newlines only
        var attribute = new NotWhiteSpaceAttribute();
        var value = "\t\n\r";
        var context = new ValidationContext(new object()) { DisplayName = "TestProperty" };

        // When: Validating the string
        var result = attribute.GetValidationResult(value, context);

        // Then: Should be invalid
        Assert.That(result, Is.Not.EqualTo(ValidationResult.Success));
        Assert.That(result?.ErrorMessage, Does.Contain("cannot be empty or whitespace"));
    }

    [Test]
    public void NotWhiteSpace_WithNullValue_IsValid()
    {
        // Given: A null value (let Required attribute handle null validation)
        var attribute = new NotWhiteSpaceAttribute();
        var context = new ValidationContext(new object()) { DisplayName = "TestProperty" };

        // When: Validating null
        var result = attribute.GetValidationResult(null, context);

        // Then: Should be valid (Required attribute handles nulls)
        Assert.That(result, Is.EqualTo(ValidationResult.Success));
    }

    [Test]
    public void NotWhiteSpace_WithStringContainingWhitespace_IsValid()
    {
        // Given: A valid string that contains whitespace but also content
        var attribute = new NotWhiteSpaceAttribute();
        var value = "  Valid string with spaces  ";
        var context = new ValidationContext(new object()) { DisplayName = "TestProperty" };

        // When: Validating the string
        var result = attribute.GetValidationResult(value, context);

        // Then: Should be valid
        Assert.That(result, Is.EqualTo(ValidationResult.Success));
    }

    [Test]
    public void NotWhiteSpace_WithCustomErrorMessage_UsesCustomMessage()
    {
        // Given: An attribute with a custom error message
        var attribute = new NotWhiteSpaceAttribute
        {
            ErrorMessage = "Custom error message"
        };
        var value = "   ";
        var context = new ValidationContext(new object()) { DisplayName = "TestProperty" };

        // When: Validating an invalid string
        var result = attribute.GetValidationResult(value, context);

        // Then: Should use the custom error message
        Assert.That(result?.ErrorMessage, Is.EqualTo("Custom error message"));
    }

    [Test]
    public void NotWhiteSpace_WithNonStringValue_IsInvalid()
    {
        // Given: A non-string value (e.g., an integer)
        var attribute = new NotWhiteSpaceAttribute();
        var value = 123;
        var context = new ValidationContext(new object()) { DisplayName = "TestProperty" };

        // When: Validating the non-string value
        var result = attribute.GetValidationResult(value, context);

        // Then: Should be invalid with appropriate message
        Assert.That(result, Is.Not.EqualTo(ValidationResult.Success));
        Assert.That(result?.ErrorMessage, Does.Contain("must be a string"));
    }

    [Test]
    public void NotWhiteSpace_UsesDisplayNameInDefaultErrorMessage()
    {
        // Given: A validation context with a specific display name
        var attribute = new NotWhiteSpaceAttribute();
        var value = "";
        var context = new ValidationContext(new object()) { DisplayName = "UserName" };

        // When: Validating an invalid string
        var result = attribute.GetValidationResult(value, context);

        // Then: Should include the display name in the error message
        Assert.That(result?.ErrorMessage, Does.Contain("UserName"));
        Assert.That(result?.ErrorMessage, Does.Contain("cannot be empty or whitespace"));
    }

    [Test]
    public void NotWhiteSpace_WithSingleCharacter_IsValid()
    {
        // Given: A single non-whitespace character
        var attribute = new NotWhiteSpaceAttribute();
        var value = "A";
        var context = new ValidationContext(new object()) { DisplayName = "TestProperty" };

        // When: Validating the string
        var result = attribute.GetValidationResult(value, context);

        // Then: Should be valid
        Assert.That(result, Is.EqualTo(ValidationResult.Success));
    }
}
