using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using NuxtIdentity.AspNetCore.Controllers;
using NuxtIdentity.Core.Abstractions;
using YoFi.V3.Controllers.Tenancy.Api.Dto;
using YoFi.V3.Controllers.Tenancy.Features;

namespace YoFi.V3.Controllers;

/// <summary>
/// Authentication controller for the NuxtIdentity sample backend.
/// </summary>
/// <remarks>
/// This controller demonstrates the minimal implementation needed when using
/// NuxtAuthControllerBase. It simply provides a concrete class that ASP.NET Core
/// can instantiate and map routes to.
///
/// The base controller provides complete implementations for all endpoints:
/// - POST /api/auth/login - Username/password authentication
/// - POST /api/auth/signup - User registration
/// - GET /api/auth/user - Get current user session with roles and claims
/// - POST /api/auth/refresh - Token refresh with rotation
/// - POST /api/auth/logout - Token revocation
///
/// All endpoints can be overridden if custom behavior is needed, but the defaults
/// work well for most ASP.NET Core Identity scenarios.
/// </remarks>
public class AuthController(
    TenantFeature tenantFeature,
    IJwtTokenService<IdentityUser> jwtTokenService,
    IRefreshTokenService refreshTokenService,
    UserManager<IdentityUser> userManager,
    SignInManager<IdentityUser> signInManager,
    ILogger<AuthController> logger)
    : NuxtAuthControllerBase<IdentityUser>(
        jwtTokenService,
        refreshTokenService,
        userManager,
        signInManager,
        logger)
{
    /// <summary>
    /// Called after a new user has been created.
    /// </summary>
    /// <remarks>
    /// Provision a tenant for this new user
    /// </remarks>
    protected override async Task OnUserCreatedAsync(IdentityUser user)
    {
        // Create a tenant for the new user
        await tenantFeature.CreateTenantForUserAsync(
            Guid.Parse(user.Id),
            new TenantEditDto(
                $"Default workspace for {user.UserName}",
                "A personal financial management workspace"
        ));
    }
}
