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
        services.AddScoped<ITenantProvider, TenantContext>();
        services.AddScoped<TenantContext>();

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
