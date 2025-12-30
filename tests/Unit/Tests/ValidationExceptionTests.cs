namespace YoFi.V3.Tests.Unit.Tests;

using NUnit.Framework;
using YoFi.V3.Entities.Exceptions;

/// <summary>
/// Unit tests for ValidationException.
/// </summary>
[TestFixture]
public class ValidationExceptionTests
{
    [Test]
    public void Constructor_WithMessage_SetsPropertiesCorrectly()
    {
        // Given: A validation error message
        var message = "File is required and cannot be empty.";

        // When: Exception is created with message only
        var exception = new ValidationException(message);

        // Then: Properties are set correctly
        Assert.That(exception.Message, Is.EqualTo(message));
        Assert.That(exception.ParameterName, Is.Null);
        Assert.That(exception.InnerException, Is.Null);
    }

    [Test]
    public void Constructor_WithParameterNameAndMessage_SetsPropertiesCorrectly()
    {
        // Given: A parameter name and validation error message
        var parameterName = "file";
        var message = "File is required and cannot be empty.";

        // When: Exception is created with parameter name and message
        var exception = new ValidationException(parameterName, message);

        // Then: Properties are set correctly
        Assert.That(exception.ParameterName, Is.EqualTo(parameterName));
        Assert.That(exception.Message, Is.EqualTo(message));
        Assert.That(exception.InnerException, Is.Null);
    }

    [Test]
    public void Constructor_WithMessageAndInnerException_SetsPropertiesCorrectly()
    {
        // Given: A validation error message and inner exception
        var message = "Validation failed due to internal error";
        var innerException = new InvalidOperationException("Inner exception message");

        // When: Exception is created with message and inner exception
        var exception = new ValidationException(message, innerException);

        // Then: Properties are set correctly
        Assert.That(exception.Message, Is.EqualTo(message));
        Assert.That(exception.ParameterName, Is.Null);
        Assert.That(exception.InnerException, Is.EqualTo(innerException));
        Assert.That(exception.InnerException.Message, Is.EqualTo("Inner exception message"));
    }

    [Test]
    public void Constructor_WithParameterNameMessageAndInnerException_SetsPropertiesCorrectly()
    {
        // Given: A parameter name, validation error message, and inner exception
        var parameterName = "keys";
        var message = "At least one transaction key must be provided.";
        var innerException = new ArgumentException("Argument exception message");

        // When: Exception is created with all parameters
        var exception = new ValidationException(parameterName, message, innerException);

        // Then: Properties are set correctly
        Assert.That(exception.ParameterName, Is.EqualTo(parameterName));
        Assert.That(exception.Message, Is.EqualTo(message));
        Assert.That(exception.InnerException, Is.EqualTo(innerException));
        Assert.That(exception.InnerException.Message, Is.EqualTo("Argument exception message"));
    }

    [Test]
    public void Constructor_WithMessage_InheritsFromException()
    {
        // Given: A validation error message
        var message = "Validation error";

        // When: Exception is created
        var exception = new ValidationException(message);

        // Then: Exception inherits from Exception
        Assert.That(exception, Is.InstanceOf<Exception>());
    }

    [Test]
    public void ParameterName_PropertyGetter_ReturnsCorrectValue()
    {
        // Given: A validation exception with parameter name
        var parameterName = "testParam";
        var exception = new ValidationException(parameterName, "Test message");

        // When: ParameterName property is accessed
        var retrievedParameterName = exception.ParameterName;

        // Then: Correct value is returned
        Assert.That(retrievedParameterName, Is.EqualTo(parameterName));
    }

    [Test]
    public void ParameterName_WhenNotProvided_ReturnsNull()
    {
        // Given: A validation exception without parameter name
        var exception = new ValidationException("Test message");

        // When: ParameterName property is accessed
        var parameterName = exception.ParameterName;

        // Then: Null is returned
        Assert.That(parameterName, Is.Null);
    }

    [Test]
    public void Message_PropertyGetter_ReturnsCorrectValue()
    {
        // Given: A validation exception with custom message
        var message = "Custom validation error message";
        var exception = new ValidationException(message);

        // When: Message property is accessed
        var retrievedMessage = exception.Message;

        // Then: Correct message is returned
        Assert.That(retrievedMessage, Is.EqualTo(message));
    }
}
