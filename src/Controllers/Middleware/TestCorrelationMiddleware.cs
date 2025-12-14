using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Web;
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
public partial class TestCorrelationMiddleware(
    RequestDelegate next,
    ILogger<TestCorrelationMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Processes the HTTP request and enriches it with test correlation data.
    /// </summary>
    /// <param name="context">The HTTP context for the current request.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        var scope = new Dictionary<string, object>();
        var activity = Activity.Current; // ASP.NET Core auto-creates this from traceparent header

        //
        // Log all request headers and cookies for debugging
        //
        var allHeaders = string.Join(", ", context.Request.Headers.Select(h => $"{h.Key}={h.Value}"));
        LogRequestHeaders(allHeaders);

        var allCookies = string.Join(", ", context.Request.Cookies.Select(c => $"{c.Key}={c.Value}"));
        LogRequestCookies(allCookies);

        //
        // Extract Query
        //
        var query = context.Request.Query;
        if (query.Count > 0)
        {
            scope["Query"] = JsonSerializer.Serialize(query);
        }

        //
        // Extract test correlation from headers (preferred) or cookie (fallback)
        //
        string? testName = null;
        string? testId = null;
        string? testClass = null;

        // Try headers first (faster, used by Playwright-controlled requests)
        if (context.Request.Headers.TryGetValue("X-Test-Name", out var testNameValue))
        {
            testName = testNameValue.ToString();
            testId = context.Request.Headers.TryGetValue("X-Test-Id", out var testIdValue) ? testIdValue.ToString() : null;
            testClass = context.Request.Headers.TryGetValue("X-Test-Class", out var testClassValue) ? testClassValue.ToString() : null;
        }
        // Fall back to cookie (used by frontend-initiated requests)
        else if (context.Request.Cookies.TryGetValue("x-test-correlation", out var cookieValue))
        {
            try
            {
                // ASP.NET Core auto-decodes cookies, but we double-encoded, so decode again
                var decoded = HttpUtility.UrlDecode(cookieValue);
                LogAttemptingCookieParse(decoded);

                // Use case-insensitive deserialization to match lowercase JSON property names
                var testMetadata = JsonSerializer.Deserialize<TestCorrelationMetadata>(decoded, JsonOptions);
                if (testMetadata != null)
                {
                    testName = testMetadata.Name;
                    testId = testMetadata.Id;
                    testClass = testMetadata.Class;

                    // If traceparent is in cookie, manually set it on the current activity to link traces
                    if (testMetadata.Traceparent != null && activity != null)
                    {
                        // Parse traceparent: 00-{trace-id}-{parent-span-id}-{flags}
                        var parts = testMetadata.Traceparent.Split('-');
                        if (parts.Length == 4 && parts[0] == "00")
                        {
                            activity.SetParentId(ActivityTraceId.CreateFromString(parts[1]), ActivitySpanId.CreateFromString(parts[2]));
                            LogLinkedActivityToTestTrace(parts[1]);
                        }
                    }

                    LogTestCorrelationFromCookie(testName ?? "null", testId ?? "null");
                }
                else
                {
                    LogTestCorrelationCookieDeserializedNull(decoded);
                }
            }
            catch (JsonException ex)
            {
                LogFailedToParseTestCorrelationCookie(ex, cookieValue);
            }
        }

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

    [LoggerMessage(0, LogLevel.Debug, "{Location}: Request headers: {Headers}")]
    private partial void LogRequestHeaders(string headers, [CallerMemberName] string? location = null);

    [LoggerMessage(1, LogLevel.Debug, "{Location}: Request cookies: {Cookies}")]
    private partial void LogRequestCookies(string cookies, [CallerMemberName] string? location = null);

    [LoggerMessage(2, LogLevel.Debug, "{Location}: Attempting to parse cookie value: {DecodedValue}")]
    private partial void LogAttemptingCookieParse(string decodedValue, [CallerMemberName] string? location = null);

    [LoggerMessage(3, LogLevel.Debug, "{Location}: Test correlation from cookie: TestName={TestName}, TestId={TestId}")]
    private partial void LogTestCorrelationFromCookie(string testName, string testId, [CallerMemberName] string? location = null);

    [LoggerMessage(4, LogLevel.Warning, "{Location}: Failed to parse test correlation cookie. Cookie value: {CookieValue}")]
    private partial void LogFailedToParseTestCorrelationCookie(JsonException ex, string cookieValue, [CallerMemberName] string? location = null);

    [LoggerMessage(5, LogLevel.Warning, "{Location}: Test correlation cookie deserialized to null. Cookie value: {CookieValue}")]
    private partial void LogTestCorrelationCookieDeserializedNull(string cookieValue, [CallerMemberName] string? location = null);

    [LoggerMessage(6, LogLevel.Debug, "{Location}: Linked activity to test trace: {TraceId}")]
    private partial void LogLinkedActivityToTestTrace(string traceId, [CallerMemberName] string? location = null);

    /// <summary>
    /// Simple DTO for deserializing test correlation metadata from cookie.
    /// </summary>
    private record TestCorrelationMetadata(string? Name, string? Id, string? Class, string? Traceparent);
}
