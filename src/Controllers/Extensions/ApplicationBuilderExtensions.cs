using Microsoft.AspNetCore.Builder;
using YoFi.V3.Controllers.Middleware;

namespace YoFi.V3.Controllers.Extensions;

/// <summary>
/// Extension methods for configuring Controllers-specific middleware.
/// </summary>
public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Adds Controllers-specific middleware to the application pipeline.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <returns>The application builder for chaining.</returns>
    /// <remarks>
    /// This middleware must be added early in the pipeline to capture all requests.
    /// </remarks>
    public static IApplicationBuilder UseControllerMiddleware(this IApplicationBuilder app)
    {
        // Test correlation middleware captures all requests for functional test correlation
        app.UseMiddleware<TestCorrelationMiddleware>();

        return app;
    }
}
