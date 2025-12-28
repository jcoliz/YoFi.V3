using FluentValidation;
using YoFi.V3.Application.Dto;

namespace YoFi.V3.Application.Validation;

/// <summary>
/// Validator for <see cref="TransactionQuickEditDto"/> used in quick edit operations.
/// </summary>
/// <remarks>
/// Validates Payee, Memo, and Category fields for quick editing from list view.
/// This is a lighter validation compared to <see cref="TransactionEditDtoValidator"/>
/// as it only validates the fields that can be quick-edited.
/// </remarks>
public class TransactionQuickEditDtoValidator : AbstractValidator<TransactionQuickEditDto>
{
    /// <summary>
    /// Initializes validation rules for <see cref="TransactionQuickEditDto"/>.
    /// </summary>
    public TransactionQuickEditDtoValidator()
    {
        // Payee validation: Required, not whitespace, max 200 characters
        RuleFor(x => x.Payee)
            .NotEmpty()
            .WithMessage("Transaction payee is required")
            .Must(payee => !string.IsNullOrWhiteSpace(payee))
            .WithMessage("Transaction payee cannot be empty or whitespace")
            .MaximumLength(200)
            .WithMessage("Transaction payee cannot exceed 200 characters");

        // Memo validation: Optional, max 1000 characters
        RuleFor(x => x.Memo)
            .MaximumLength(1000)
            .When(x => x.Memo != null)
            .WithMessage("Transaction memo cannot exceed 1000 characters");

        // Category validation: Optional, max 200 characters
        RuleFor(x => x.Category)
            .MaximumLength(200)
            .When(x => x.Category != null)
            .WithMessage("Transaction category cannot exceed 200 characters");
    }
}
