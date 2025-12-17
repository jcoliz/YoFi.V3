using System.Security.Claims;

namespace YoFi.V3.Controllers.Tenancy.Authorization;

/// <summary>
/// Provides custom claims to be added to user authentication tokens.
/// </summary>
/// <remarks>
/// This abstraction allows the multi-tenancy system to be decoupled from specific
/// authentication frameworks. Implementations can add tenant-specific claims that
/// are used for authorization decisions throughout the application.
/// </remarks>
public interface IClaimsEnricher
{
    /// <summary>
    /// Gets additional claims for the specified user ID.
    /// </summary>
    /// <param name="userId">The unique identifier of the user.</param>
    /// <returns>A collection of claims to be added to the user's authentication token.</returns>
    Task<IEnumerable<Claim>> GetClaimsAsync(string userId);
}