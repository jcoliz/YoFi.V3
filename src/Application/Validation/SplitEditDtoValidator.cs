using FluentValidation;
using YoFi.V3.Application.Dto;

namespace YoFi.V3.Application.Validation;

/// <summary>
/// Validates split data used by single-split operations.
/// </summary>
public class SplitEditDtoValidator : AbstractValidator<SplitEditDto>
{
    public SplitEditDtoValidator()
    {
        RuleFor(split => split.Amount)
            .InclusiveBetween(-999999999m, 999999999m)
            .WithMessage("Split amount must be a valid value");

        RuleFor(split => split.Category)
            .MaximumLength(200)
            .WithMessage("Category cannot exceed 200 characters")
            .When(split => split.Category != null);

        RuleFor(split => split.Memo)
            .MaximumLength(500)
            .WithMessage("Memo cannot exceed 500 characters")
            .When(split => split.Memo != null);
    }
}
