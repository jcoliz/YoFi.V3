using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using YoFi.V3.Entities.Tenancy;

namespace YoFi.V3.Controllers.Tenancy;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds tenancy services to the service collection.
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddTenancy(this IServiceCollection services)
    {
        // Register TenantContext as a single scoped service
        services.AddScoped<TenantContext>();

        // Register ITenantProvider as an alias to the same TenantContext instance
        services.AddScoped<ITenantProvider>(sp => sp.GetRequiredService<TenantContext>());

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
    /// <remarks>
    /// Be sure to add this middleware after authentication and authorization middlewares!!
    /// </remarks>
    /// <param name="app"></param>
    /// <returns></returns>
    public static IApplicationBuilder UseTenancy(this IApplicationBuilder app)
    {
        app.UseMiddleware<TenantContextMiddleware>();

        return app;
    }
}
