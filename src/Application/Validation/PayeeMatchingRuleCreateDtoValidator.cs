using FluentValidation;
using YoFi.V3.Application.Dto;

namespace YoFi.V3.Application.Validation;

/// <summary>
/// Validates rule creation using the same rule-field validation as updates.
/// </summary>
public class PayeeMatchingRuleCreateDtoValidator : AbstractValidator<PayeeMatchingRuleCreateDto>
{
    /// <summary>
    /// Initializes shared rule validation.
    /// </summary>
    /// <param name="ruleValidator">Validator for the common rule fields.</param>
    public PayeeMatchingRuleCreateDtoValidator(IValidator<PayeeMatchingRuleEditDto> ruleValidator)
    {
        Include(ruleValidator);
    }
}
