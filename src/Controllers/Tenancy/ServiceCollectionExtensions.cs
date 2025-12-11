using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using NuxtIdentity.Core.Abstractions;
using YoFi.V3.Controllers.Tenancy.Authorization;
using YoFi.V3.Controllers.Tenancy.Context;
using YoFi.V3.Controllers.Tenancy.Features;
using YoFi.V3.Entities.Tenancy.Models;
using YoFi.V3.Entities.Tenancy.Providers;

namespace YoFi.V3.Controllers.Tenancy;

/// <summary>
/// Extension methods for configuring tenancy services and middleware.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds tenancy services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTenancy(this IServiceCollection services)
    {
        // Register TenantContext as a single scoped service
        services.AddScoped<TenantContext>();

        // Register ITenantProvider as an alias to the same TenantContext instance
        services.AddScoped<ITenantProvider>(sp => sp.GetRequiredService<TenantContext>());

        // Register TenantFeature
        services.AddScoped<TenantFeature>();

        // Add claims to token
        services.AddScoped<IUserClaimsProvider<IdentityUser>, TenantUserClaimsService<IdentityUser>>();

        // Register the authorization handler for tenant role requirements
        services.AddSingleton<IAuthorizationHandler, TenantRoleHandler>();

        // Register each policy NAME that the attribute might create
        services.AddAuthorization(options =>
        {
            // Register each policy NAME that the attribute might create
            foreach (TenantRole role in Enum.GetValues<TenantRole>())
            {
                // This creates: "TenantRole_Viewer", "TenantRole_Editor", "TenantRole_Owner", etc
                options.AddPolicy($"TenantRole_{role}", policy =>
                    policy.Requirements.Add(new TenantRoleRequirement(role)));
            }
        });

        return services;
    }

    /// <summary>
    /// Adds the TenantContextMiddleware to the application pipeline.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    /// <remarks>
    /// Be sure to add this middleware after authentication and authorization middlewares!!
    /// </remarks>
    public static IApplicationBuilder UseTenancy(this IApplicationBuilder app)
    {
        app.UseMiddleware<TenantContextMiddleware>();

        return app;
    }
}
