namespace YoFi.V3.Application.Tenancy.Dto;

/// <summary>
/// Data transfer object for creating or editing a tenant.
/// </summary>
/// <param name="Name">The name of the tenant.</param>
/// <param name="Description">A description of the tenant.</param>
/// <remarks>
/// This DTO is validated by <see cref="YoFi.V3.Application.Tenancy.Validation.TenantEditDtoValidator"/>
/// at the controller boundary.
/// </remarks>
public record TenantEditDto(
    string Name,
    string Description
);
