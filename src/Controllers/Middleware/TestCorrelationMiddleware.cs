using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace YoFi.V3.Controllers.Middleware;

/// <summary>
/// Middleware that extracts test correlation information from HTTP headers and adds it to logging scopes and Activity tags.
/// </summary>
/// <param name="next">The next middleware in the pipeline.</param>
/// <param name="logger">Logger for diagnostic output.</param>
/// <remarks>
/// This middleware supports both direct header-based correlation (X-Test-Name, X-Test-Id, X-Test-Class)
/// and W3C Trace Context propagation (traceparent header) for distributed tracing integration.
/// Test metadata is added to both Activity tags (for OpenTelemetry) and logging scopes (for structured logging).
/// </remarks>
public class TestCorrelationMiddleware(
    RequestDelegate next,
    ILogger<TestCorrelationMiddleware> logger)
{
    /// <summary>
    /// Processes the HTTP request and enriches it with test correlation data.
    /// </summary>
    /// <param name="context">The HTTP context for the current request.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        var scope = new Dictionary<string, object>();
        var activity = Activity.Current; // ASP.NET Core auto-creates this from traceparent header

        //
        // Extract Query
        //
        var query = context.Request.Query;
        if (query.Count > 0)
        {
            scope["Query"] = JsonSerializer.Serialize(query);
        }

        //
        // Extract test correlation from headers
        //
        var testName = context.Request.Headers.TryGetValue("X-Test-Name", out var testNameValue)
            ? testNameValue.ToString()
            : null;

        var testId = context.Request.Headers.TryGetValue("X-Test-Id", out var testIdValue)
            ? testIdValue.ToString()
            : null;

        var testClass = context.Request.Headers.TryGetValue("X-Test-Class", out var testClassValue)
            ? testClassValue.ToString()
            : null;

        //
        // Add test metadata to both Activity (for OpenTelemetry) and Scope (for logging)
        //
        if (activity != null)
        {
            // Add standard OpenTelemetry semantic conventions
            if (testName != null)
            {
                activity.SetTag("test.name", testName);
                scope["TestName"] = testName;
            }

            if (testId != null)
            {
                activity.SetTag("test.id", testId);
                scope["TestId"] = testId;
            }

            if (testClass != null)
            {
                activity.SetTag("test.class", testClass);
                scope["TestClass"] = testClass;
            }

            // Add TraceId to scope for local logging
            scope["TraceId"] = activity.TraceId.ToString();
        }

        using (logger.BeginScope(scope))
        {
            await next(context);
        }
    }
}
