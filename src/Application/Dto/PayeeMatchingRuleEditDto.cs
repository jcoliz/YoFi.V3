namespace YoFi.V3.Application.Dto;

/// <summary>
/// Payee matching rule data for creating or updating rules (input DTO).
/// </summary>
/// <param name="PayeePattern">Pattern to match against transaction payee (substring or regex)</param>
/// <param name="PayeeIsRegex">If true, PayeePattern is treated as regex; if false, as substring</param>
/// <param name="Category">Category to assign when rule matches</param>
/// <remarks>
/// This DTO is validated by <see cref="YoFi.V3.Application.Validation.PayeeMatchingRuleEditDtoValidator"/>
/// at the controller boundary. For query results, see <see cref="PayeeMatchingRuleResultDto"/>.
///
/// All validation rules are enforced by FluentValidation - see PayeeMatchingRuleEditDtoValidator for:
/// - PayeePattern required and max length 200
/// - Category required (not whitespace-only) and max length 200
/// - Regex validation when PayeeIsRegex is true (syntax and ReDoS protection)
///
/// Category is automatically sanitized using CategoryHelper.SanitizeCategory() when the rule is saved.
/// </remarks>
public record PayeeMatchingRuleEditDto(
    string PayeePattern,
    bool PayeeIsRegex,
    string Category
);
