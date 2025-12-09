using System.ComponentModel.DataAnnotations;

namespace YoFi.V3.Application.Validation;

/// <summary>
/// Validates that a string value is not null, empty, or whitespace.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public class NotWhiteSpaceAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null)
        {
            return ValidationResult.Success; // Let [Required] handle null validation
        }

        if (value is not string stringValue)
        {
            return new ValidationResult("Value must be a string.");
        }

        if (string.IsNullOrWhiteSpace(stringValue))
        {
            return new ValidationResult(
                ErrorMessage ?? $"{validationContext.DisplayName} cannot be empty or whitespace."
            );
        }

        return ValidationResult.Success;
    }
}
