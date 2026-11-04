using FluentValidation;
using YoFi.V3.Application.Dto;

namespace YoFi.V3.Application.Validation;

public sealed class TenantDataDeletionRequestValidator : AbstractValidator<TenantDataDeletionRequest>
{
    public TenantDataDeletionRequestValidator()
    {
        RuleFor(request => request.ConfirmationWorkspaceName)
            .NotEmpty()
            .MaximumLength(100);
    }
}
