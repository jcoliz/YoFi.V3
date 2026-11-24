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
    private string? _title;
    private IReadOnlyDictionary<string, string[]> _errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

    /// <summary>
    /// Gets or initializes an optional user-facing validation title.
    /// </summary>
    public string? Title
    {
        get => _title;
        init
        {
            if (value is not null)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(value);
            }

            _title = value;
        }
    }

    /// <summary>
    /// Gets the name of the parameter or field that failed validation.
    /// </summary>
    public string? ParameterName { get; }

    /// <summary>
    /// Gets or initializes grouped field validation messages.
    /// </summary>
    public IReadOnlyDictionary<string, string[]> Errors
    {
        get => _errors;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value.Any(entry =>
                string.IsNullOrWhiteSpace(entry.Key)
                || entry.Value is not { Length: > 0 }
                || entry.Value.Any(string.IsNullOrWhiteSpace)))
            {
                throw new ArgumentException(
                    "Each validation problem must have a non-empty key and at least one non-empty message.",
                    nameof(value));
            }

            _errors = value.ToDictionary(
                entry => entry.Key,
                entry => entry.Value.ToArray(),
                StringComparer.Ordinal);
        }
    }

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
