using FluentValidation;
using YoFi.V3.Application.Dto;

namespace YoFi.V3.Application.Validation;

/// <summary>
/// Validator for TransactionEditDto ensuring all business rules and constraints are met.
/// </summary>
/// <remarks>
/// This validator consolidates all validation rules for transaction creation and updates:
/// - Date must be within 50 years in the past and 5 years in the future
/// - Amount must be non-zero (can be negative for credits/refunds)
/// - Payee is required, cannot be whitespace, max 200 characters
/// - Memo, Source, ExternalId, Category are optional with max length constraints
/// </remarks>
public class TransactionEditDtoValidator : AbstractValidator<TransactionEditDto>
{
    private static readonly DateOnly MinDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-50));
    private static readonly DateOnly MaxDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(5));

    public TransactionEditDtoValidator()
    {
        RuleFor(x => x.Date)
            .Must(BeWithinValidRange)
            .WithMessage("Transaction date must be within 50 years in the past and 5 years in the future");

        RuleFor(x => x.Amount)
            .NotEqual(0)
            .WithMessage("Transaction amount cannot be zero");

        RuleFor(x => x.Payee)
            .NotEmpty()
            .WithMessage("Payee is required")
            .Must(p => !string.IsNullOrWhiteSpace(p))
            .WithMessage("Payee cannot be empty")
            .MaximumLength(200)
            .WithMessage("Payee cannot exceed 200 characters");

        RuleFor(x => x.Memo)
            .MaximumLength(1000)
            .WithMessage("Memo cannot exceed 1000 characters")
            .When(x => x.Memo != null);

        RuleFor(x => x.Source)
            .MaximumLength(200)
            .WithMessage("Source cannot exceed 200 characters")
            .When(x => x.Source != null);

        RuleFor(x => x.ExternalId)
            .MaximumLength(100)
            .WithMessage("ExternalId cannot exceed 100 characters")
            .When(x => x.ExternalId != null);

        RuleFor(x => x.Category)
            .MaximumLength(200)
            .WithMessage("Category cannot exceed 200 characters")
            .When(x => x.Category != null);
    }

    private static bool BeWithinValidRange(DateOnly date)
    {
        return date >= MinDate && date <= MaxDate;
    }
}
