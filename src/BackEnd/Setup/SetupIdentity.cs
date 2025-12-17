using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NuxtIdentity.Core.Abstractions;
using NuxtIdentity.EntityFrameworkCore.Extensions;
using YoFi.V3.Controllers.Tenancy.Authorization;
using YoFi.V3.Data;

namespace YoFi.V3.BackEnd.Setup;

/// <summary>
/// Extension methods for configuring Identity services.
/// </summary>
public static class SetupIdentity
{
    /// <summary>
    /// Adds Identity and NuxtIdentity services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddIdentityConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure Identity options
        services.AddIdentity<IdentityUser, IdentityRole>(options =>
        {
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequiredLength = 6;
            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        // Add NuxtIdentity
        services.AddNuxtIdentityWithEntityFramework<IdentityUser, ApplicationDbContext>(configuration);

        // Register NuxtIdentity adapter for tenant claims
        services.AddScoped<IUserClaimsProvider<IdentityUser>, NuxtIdentityTenantClaimsAdapter<IdentityUser>>();

        return services;
    }
}
