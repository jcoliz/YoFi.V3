namespace YoFi.V3.Tests.Unit.Tests;

using NUnit.Framework;
using YoFi.V3.Entities.Exceptions;

[TestFixture]
public class TransactionNotFoundExceptionTests
{
    [Test]
    public void Constructor_WithKey_SetsPropertiesCorrectly()
    {
        // Arrange
        var key = Guid.NewGuid();

        // Act
        var exception = new TransactionNotFoundException(key);

        // Assert
        Assert.That(exception.TransactionKey, Is.EqualTo(key));
        Assert.That(exception.Message, Does.Contain(key.ToString()));
        Assert.That(exception.Message, Does.Contain("not found"));
        Assert.That(exception.InnerException, Is.Null);
    }

    [Test]
    public void Constructor_WithKeyAndMessage_SetsPropertiesCorrectly()
    {
        // Arrange
        var key = Guid.NewGuid();
        var customMessage = "Custom error message for testing";

        // Act
        var exception = new TransactionNotFoundException(key, customMessage);

        // Assert
        Assert.That(exception.TransactionKey, Is.EqualTo(key));
        Assert.That(exception.Message, Is.EqualTo(customMessage));
        Assert.That(exception.InnerException, Is.Null);
    }

    [Test]
    public void Constructor_WithKeyMessageAndInnerException_SetsPropertiesCorrectly()
    {
        // Arrange
        var key = Guid.NewGuid();
        var customMessage = "Custom error message with inner exception";
        var innerException = new InvalidOperationException("Inner exception message");

        // Act
        var exception = new TransactionNotFoundException(key, customMessage, innerException);

        // Assert
        Assert.That(exception.TransactionKey, Is.EqualTo(key));
        Assert.That(exception.Message, Is.EqualTo(customMessage));
        Assert.That(exception.InnerException, Is.EqualTo(innerException));
        Assert.That(exception.InnerException.Message, Is.EqualTo("Inner exception message"));
    }

    [Test]
    public void Constructor_WithKey_InheritsFromException()
    {
        // Arrange
        var key = Guid.NewGuid();

        // Act
        var exception = new TransactionNotFoundException(key);

        // Assert
        Assert.That(exception, Is.InstanceOf<Exception>());
    }

    [Test]
    public void TransactionKey_PropertyGetter_ReturnsCorrectValue()
    {
        // Arrange
        var key = Guid.NewGuid();
        var exception = new TransactionNotFoundException(key);

        // Act
        var retrievedKey = exception.TransactionKey;

        // Assert
        Assert.That(retrievedKey, Is.EqualTo(key));
    }
}
