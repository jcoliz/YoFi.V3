namespace YoFi.V3.Controllers.Tenancy.Api.Dto;

/// <summary>
/// Data transfer object for creating or editing a tenant.
/// </summary>
/// <param name="Name">The name of the tenant.</param>
/// <param name="Description">A description of the tenant.</param>
public record TenantEditDto(
    string Name,
    string Description
);
