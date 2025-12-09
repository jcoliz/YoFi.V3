using System.ComponentModel.DataAnnotations;
using YoFi.V3.Application.Validation;

namespace YoFi.V3.Tests.Unit.Application.Validation;

public class DateRangeAttributeTests
{
    [Test]
    public void DateRange_WithinRange_IsValid()
    {
        // Given: A date within the allowed range
        var attribute = new DateRangeAttribute(50, 5);
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        var context = new ValidationContext(new object());

        // When: Validating the date
        var result = attribute.GetValidationResult(date, context);

        // Then: Should be valid
        Assert.That(result, Is.EqualTo(ValidationResult.Success));
    }

    [Test]
    public void DateRange_TooFarInPast_IsInvalid()
    {
        // Given: A date too far in the past (more than 50 years)
        var attribute = new DateRangeAttribute(50, 5);
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-51));
        var context = new ValidationContext(new object());

        // When: Validating the date
        var result = attribute.GetValidationResult(date, context);

        // Then: Should be invalid
        Assert.That(result, Is.Not.EqualTo(ValidationResult.Success));
        Assert.That(result?.ErrorMessage, Does.Contain("must be between"));
    }

    [Test]
    public void DateRange_TooFarInFuture_IsInvalid()
    {
        // Given: A date too far in the future (more than 5 years)
        var attribute = new DateRangeAttribute(50, 5);
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(6));
        var context = new ValidationContext(new object());

        // When: Validating the date
        var result = attribute.GetValidationResult(date, context);

        // Then: Should be invalid
        Assert.That(result, Is.Not.EqualTo(ValidationResult.Success));
        Assert.That(result?.ErrorMessage, Does.Contain("must be between"));
    }

    [Test]
    public void DateRange_EdgeCase_ExactlyAtPastLimit_IsValid()
    {
        // Given: A date exactly at the past limit (50 years ago today)
        var attribute = new DateRangeAttribute(50, 5);
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-50));
        var context = new ValidationContext(new object());

        // When: Validating the date
        var result = attribute.GetValidationResult(date, context);

        // Then: Should be valid
        Assert.That(result, Is.EqualTo(ValidationResult.Success));
    }

    [Test]
    public void DateRange_EdgeCase_ExactlyAtFutureLimit_IsValid()
    {
        // Given: A date exactly at the future limit (5 years from today)
        var attribute = new DateRangeAttribute(50, 5);
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(5));
        var context = new ValidationContext(new object());

        // When: Validating the date
        var result = attribute.GetValidationResult(date, context);

        // Then: Should be valid
        Assert.That(result, Is.EqualTo(ValidationResult.Success));
    }

    [Test]
    public void DateRange_WithCustomErrorMessage_UsesCustomMessage()
    {
        // Given: An attribute with a custom error message
        var attribute = new DateRangeAttribute(50, 5)
        {
            ErrorMessage = "Custom error message"
        };
        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-100));
        var context = new ValidationContext(new object());

        // When: Validating an invalid date
        var result = attribute.GetValidationResult(date, context);

        // Then: Should use the custom error message
        Assert.That(result?.ErrorMessage, Is.EqualTo("Custom error message"));
    }

    [Test]
    public void DateRange_WithNullValue_IsValid()
    {
        // Given: A null value (let Required handle null validation)
        var attribute = new DateRangeAttribute(50, 5);
        var context = new ValidationContext(new object());

        // When: Validating null
        var result = attribute.GetValidationResult(null, context);

        // Then: Should be valid (Required attribute handles nulls)
        Assert.That(result, Is.EqualTo(ValidationResult.Success));
    }
}
