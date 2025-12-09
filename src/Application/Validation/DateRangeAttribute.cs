using System.ComponentModel.DataAnnotations;

namespace YoFi.V3.Application.Validation;

/// <summary>
/// Validates that a DateOnly value falls within a relative range from today.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class DateRangeAttribute : ValidationAttribute
{
    /// <summary>
    /// Maximum years in the past (positive number)
    /// </summary>
    public int YearsInPast { get; set; }

    /// <summary>
    /// Maximum years in the future (positive number)
    /// </summary>
    public int YearsInFuture { get; set; }

    public DateRangeAttribute(int yearsInPast, int yearsInFuture)
    {
        YearsInPast = yearsInPast;
        YearsInFuture = yearsInFuture;
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null)
        {
            return ValidationResult.Success; // Let [Required] handle null validation
        }

        if (value is not DateOnly date)
        {
            return new ValidationResult("Value must be a DateOnly.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var minDate = today.AddYears(-YearsInPast);
        var maxDate = today.AddYears(YearsInFuture);

        if (date < minDate || date > maxDate)
        {
            return new ValidationResult(
                ErrorMessage ?? $"Date must be between {minDate:yyyy-MM-dd} and {maxDate:yyyy-MM-dd}."
            );
        }

        return ValidationResult.Success;
    }
}
