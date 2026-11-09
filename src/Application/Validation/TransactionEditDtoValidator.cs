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
/// Date limits are evaluated against the current UTC date on every validation.
/// </remarks>
public class TransactionEditDtoValidator : AbstractValidator<TransactionEditDto>
{
    private readonly TimeProvider _timeProvider;

    /// <param name="timeProvider">Provider for the current UTC date used by date validation.</param>
    public TransactionEditDtoValidator(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
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

    private bool BeWithinValidRange(DateOnly date)
    {
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        return date >= today.AddYears(-50) && date <= today.AddYears(5);
    }
}
