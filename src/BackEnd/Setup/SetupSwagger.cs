using System.Reflection;

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

            // Enable XML documentation comments for controller descriptions
            options.UseControllerSummaryAsTagDescription = true;

            // Include XML documentation from Controllers project
            var controllersXmlFile = Path.Combine(AppContext.BaseDirectory, "YoFi.V3.Controllers.xml");
            if (File.Exists(controllersXmlFile))
            {
                options.DocumentProcessors.Add(new NSwag.Generation.Processors.DocumentTagsProcessor());
            }

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
