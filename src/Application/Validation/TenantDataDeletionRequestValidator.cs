using FluentValidation;
using YoFi.V3.Application.Dto;

namespace YoFi.V3.Application.Validation;

/// <summary>Validates confirmation fields for tenant data deletion requests.</summary>
public sealed class TenantDataDeletionRequestValidator : AbstractValidator<TenantDataDeletionRequest>
{
    /// <summary>Initializes the tenant data deletion confirmation rules.</summary>
    public TenantDataDeletionRequestValidator()
    {
        RuleFor(request => request.ConfirmationWorkspaceName)
            .NotEmpty()
            .MaximumLength(100);
    }
}
