namespace YoFi.V3.Entities.Exceptions;

/// <summary>
/// Exception thrown when input validation fails.
/// Automatically maps to HTTP 400 Bad Request in the API pipeline.
/// </summary>
/// <remarks>
/// Use this exception for controller-level validation errors such as invalid file formats,
/// file size limits, or missing required parameters. For model validation (FluentValidation),
/// use the built-in validation pipeline instead.
/// </remarks>
public class ValidationException : Exception
{
    /// <summary>
    /// Gets the name of the parameter or field that failed validation.
    /// </summary>
    public string? ParameterName { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationException"/> class.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the validation failure.</param>
    public ValidationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationException"/> class with a parameter name.
    /// </summary>
    /// <param name="parameterName">The name of the parameter or field that failed validation.</param>
    /// <param name="message">The error message that explains the reason for the validation failure.</param>
    public ValidationException(string parameterName, string message)
        : base(message)
    {
        ParameterName = parameterName;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationException"/> class with an inner exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the validation failure.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public ValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationException"/> class with a parameter name and inner exception.
    /// </summary>
    /// <param name="parameterName">The name of the parameter or field that failed validation.</param>
    /// <param name="message">The error message that explains the reason for the validation failure.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public ValidationException(string parameterName, string message, Exception innerException)
        : base(message, innerException)
    {
        ParameterName = parameterName;
    }
}
