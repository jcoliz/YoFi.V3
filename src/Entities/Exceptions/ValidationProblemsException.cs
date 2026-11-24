namespace YoFi.V3.Entities.Exceptions;

/// <summary>Carries a title and grouped field problems for one failed validation.</summary>
public sealed class ValidationProblemsException
    : ValidationException
{
    /// <summary>Gets grouped validation error messages.</summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    /// <summary>Initializes a validation exception with grouped problems.</summary>
    /// <param name="title">User-facing validation title.</param>
    /// <param name="detail">User-facing validation detail.</param>
    /// <param name="errors">Grouped validation messages.</param>
    public ValidationProblemsException(
        string title,
        string detail,
        IReadOnlyDictionary<string, string[]> errors)
        : base(detail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);
        ArgumentNullException.ThrowIfNull(errors);
        if (errors.Count == 0 || errors.Any(entry =>
                string.IsNullOrWhiteSpace(entry.Key)
                || entry.Value is not { Length: > 0 }
                || entry.Value.Any(string.IsNullOrWhiteSpace)))
        {
            throw new ArgumentException(
                "At least one validation problem with one message is required.",
                nameof(errors));
        }

        Title = title;
        Errors = errors.ToDictionary(
            entry => entry.Key,
            entry => entry.Value.ToArray(),
            StringComparer.Ordinal);
    }
}
