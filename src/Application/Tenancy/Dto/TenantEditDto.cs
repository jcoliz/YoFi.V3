namespace YoFi.V3.Application.Tenancy.Dto;

/// <summary>
/// Data transfer object for creating or editing a tenant.
/// </summary>
/// <param name="Name">The name of the tenant.</param>
/// <param name="Description">A description of the tenant.</param>
public record TenantEditDto(
    string Name,
    string Description
);
