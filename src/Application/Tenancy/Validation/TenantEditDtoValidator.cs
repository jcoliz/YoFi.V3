using FluentValidation;
using YoFi.V3.Application.Tenancy.Dto;

namespace YoFi.V3.Application.Tenancy.Validation;

/// <summary>
/// Validator for <see cref="TenantEditDto"/> used in tenant create and update operations.
/// </summary>
/// <remarks>
/// Validates tenant name and description fields to ensure they meet database constraints
/// and business rules defined in ApplicationDbContext.
/// </remarks>
public class TenantEditDtoValidator : AbstractValidator<TenantEditDto>
{
    /// <summary>
    /// Initializes validation rules for <see cref="TenantEditDto"/>.
    /// </summary>
    public TenantEditDtoValidator()
    {
        // Name validation: Required, not whitespace, max 100 characters
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Tenant name is required")
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Tenant name cannot be empty or whitespace")
            .MaximumLength(100)
            .WithMessage("Tenant name cannot exceed 100 characters");

        // Description validation: Required, not whitespace, max 500 characters
        RuleFor(x => x.Description)
            .NotEmpty()
            .WithMessage("Tenant description is required")
            .Must(description => !string.IsNullOrWhiteSpace(description))
            .WithMessage("Tenant description cannot be empty or whitespace")
            .MaximumLength(500)
            .WithMessage("Tenant description cannot exceed 500 characters");
    }
}
