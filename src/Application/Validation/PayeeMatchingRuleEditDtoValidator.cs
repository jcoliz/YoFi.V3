using FluentValidation;
using YoFi.V3.Application.Dto;
using YoFi.V3.Application.Services;

namespace YoFi.V3.Application.Validation;

/// <summary>
/// Validator for PayeeMatchingRuleEditDto ensuring all business rules and constraints are met.
/// </summary>
/// <remarks>
/// This validator consolidates all validation rules for payee matching rule creation and updates:
/// - PayeePattern is required, max 200 characters
/// - Category is required, cannot be whitespace, max 200 characters
/// - When PayeeIsRegex is true, validates regex pattern for correctness and ReDoS protection
/// </remarks>
public class PayeeMatchingRuleEditDtoValidator : AbstractValidator<PayeeMatchingRuleEditDto>
{
    private readonly IRegexValidationService _regexValidationService;
    private string? _lastRegexError;

    /// <summary>
    /// Initializes a new instance of the <see cref="PayeeMatchingRuleEditDtoValidator"/> class.
    /// </summary>
    /// <param name="regexValidationService">Service for validating regex patterns.</param>
    public PayeeMatchingRuleEditDtoValidator(IRegexValidationService regexValidationService)
    {
        _regexValidationService = regexValidationService;

        RuleFor(x => x.PayeePattern)
            .NotEmpty()
            .WithMessage("Payee pattern is required")
            .MaximumLength(200)
            .WithMessage("Payee pattern cannot exceed 200 characters");

        RuleFor(x => x.Category)
            .NotEmpty()
            .WithMessage("Category is required")
            .Must(c => !string.IsNullOrWhiteSpace(c))
            .WithMessage("Category cannot be whitespace only")
            .MaximumLength(200)
            .WithMessage("Category cannot exceed 200 characters");

        // Validate regex pattern when PayeeIsRegex is true
        RuleFor(x => x.PayeePattern)
            .Must(ValidateRegexPattern)
            .WithMessage(x => GetRegexValidationErrorMessage(x.PayeePattern))
            .When(x => x.PayeeIsRegex);
    }

    /// <summary>
    /// Validates a regex pattern using the regex validation service.
    /// </summary>
    private bool ValidateRegexPattern(string pattern)
    {
        var result = _regexValidationService.ValidateRegex(pattern);
        if (!result.IsValid)
        {
            _lastRegexError = result.ErrorMessage;
            return false;
        }

        _lastRegexError = null;
        return true;
    }

    /// <summary>
    /// Gets the error message from the last regex validation failure.
    /// </summary>
    private string GetRegexValidationErrorMessage(string pattern)
    {
        return _lastRegexError ?? "Invalid regex pattern";
    }
}
