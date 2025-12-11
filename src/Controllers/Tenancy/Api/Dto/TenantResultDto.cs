namespace YoFi.V3.Controllers.Tenancy.Api.Dto;

/// <summary>
/// Data transfer object representing a tenant in API responses.
/// </summary>
/// <param name="Key">The unique identifier for the tenant.</param>
/// <param name="Name">The name of the tenant.</param>
/// <param name="Description">A description of the tenant.</param>
/// <param name="CreatedAt">The timestamp when the tenant was created.</param>
public record TenantResultDto(
    Guid Key,
    string Name,
    string Description,
    DateTimeOffset CreatedAt
);
