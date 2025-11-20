namespace YoFi.V3.BackEnd.Startup;

public static class __SetupSwagger
{
    public static IServiceCollection AddSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddOpenApiDocument(options =>
        {
            options.Title = "YoFi.V3 Backend";
            options.Description = "Application boundary between .NET backend and YoFi.V3 frontend.";
            options.Version = "v1";

            options.AddSecurity("Bearer", new NSwag.OpenApiSecurityScheme
            {
                Type = NSwag.OpenApiSecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Enter your JWT token (without 'Bearer' prefix)"
            });

            options.OperationProcessors.Add(new NSwag.Generation.Processors.Security.AspNetCoreOperationSecurityScopeProcessor("Bearer"));

        });
        return services;
    }

    public static WebApplication UseSwagger(this WebApplication app)
    {
        app.UseOpenApi();
        app.UseSwaggerUi();

        return app;
    }
}
