using DotNetNuxt.Abstractions.Options;
using YoFi.V3.BackEnd.Startup;
using YoFi.V3.Controllers.Extensions;
using YoFi.V3.Controllers.Tenancy;

namespace YoFi.V3.BackEnd.Setup;

/// <summary>
/// Extension methods for configuring the middleware pipeline.
/// </summary>
public static class SetupMiddleware
{
    /// <summary>
    /// Configures the complete middleware pipeline for the application.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="startupOptions">Startup options for configuring middleware behavior.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <returns>The application builder for chaining.</returns>
    /// <remarks>
    /// Middleware order is critical. This method ensures the correct sequence:
    /// 1. Production-specific middleware (HSTS, HTTPS redirection)
    /// 2. OpenAPI and Scalar (if enabled)
    /// 3. CORS
    /// 4. Test correlation middleware
    /// 5. Exception handler
    /// 6. Status code pages
    /// 7. Authentication/Authorization
    /// 8. Tenancy
    /// 9. Endpoints
    /// </remarks>
    public static WebApplication ConfigureMiddlewarePipeline(
        this WebApplication app,
        StartupOptions startupOptions,
        ILogger logger)
    {
        // Production-specific middleware
        if (startupOptions.AllowHttpInsecure == false)
        {
            app.UseHsts();
            app.UseHttpsRedirection();
        }

        // API documentation and (now) Scalar UI
        if (startupOptions.EnableSwaggerUi)
        {
            logger.LogEnablingApiDocumentation();
            app.MapApiDocumentation();
        }

        // CORS must come before other middleware that might reject requests
        app.UseCors();

        // Controllers middleware (test correlation) must come early to capture all requests
        app.UseControllerMiddleware();

        // Exception handler must come BEFORE middleware that might throw exceptions
        // This activates the registered IExceptionHandler implementations
        app.UseExceptionHandler();

        // Status code pages middleware to handle routing failures
        // (e.g., invalid route constraints)
        app.UseStatusCodePages();

        // Authentication and Authorization must come before authorization-protected endpoints
        app.UseAuthentication();
        app.UseAuthorization();

        // Tenancy middleware must come AFTER authentication to access user claims
        app.UseTenancy();

        // Map endpoints
        app.MapDefaultEndpoints();
        app.MapControllers();

        return app;
    }
}
