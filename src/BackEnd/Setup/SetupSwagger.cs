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
