using FluentValidation;
using YoFi.V3.Application.Dto;

namespace YoFi.V3.Application.Validation;

/// <summary>
/// Generic validator for CollectionRequest that validates each item in the collection.
/// </summary>
/// <typeparam name="T">The type of items in the collection.</typeparam>
/// <param name="itemValidator">Validator for individual items of type T, injected by DI.</param>
/// <remarks>
/// This validator requires that a validator for type T is registered in the DI container.
/// It will automatically apply that validator to each item in the Items collection using
/// FluentValidation's RuleForEach feature.
///
/// <para>Example usage in a controller:</para>
/// <code>
/// [HttpPost("bulk")]
/// public async Task&lt;IActionResult&gt; BulkCreate(
///     [FromBody] CollectionRequest&lt;TransactionEditDto&gt; request)
/// {
///     // Each TransactionEditDto is automatically validated
///     foreach (var item in request.Items) { ... }
/// }
/// </code>
/// </remarks>
public class CollectionRequestValidator<T> : AbstractValidator<CollectionRequest<T>>
{
    public CollectionRequestValidator(IValidator<T> itemValidator)
    {
        RuleForEach(x => x.Items)
            .SetValidator(itemValidator);
    }
}
