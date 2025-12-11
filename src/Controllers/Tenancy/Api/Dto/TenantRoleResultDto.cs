using YoFi.V3.Entities.Tenancy.Models;

namespace YoFi.V3.Controllers.Tenancy.Api.Dto;

/// <summary>
/// Data transfer object representing a tenant with the user's role assignment.
/// </summary>
/// <param name="Key">The unique identifier for the tenant.</param>
/// <param name="Name">The name of the tenant.</param>
/// <param name="Description">A description of the tenant.</param>
/// <param name="Role">The user's role within this tenant.</param>
/// <param name="CreatedAt">The timestamp when the tenant was created.</param>
public record TenantRoleResultDto(
    Guid Key,
    string Name,
    string Description,
    TenantRole Role,
    DateTimeOffset CreatedAt
);
