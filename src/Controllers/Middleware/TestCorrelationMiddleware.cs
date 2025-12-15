using System.Diagnostics;
using System.Runtime.CompilerServices;
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
public partial class TestCorrelationMiddleware(
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

        // Extract query parameters for logging
        AddQueryToScope(context, scope);

        // Extract test correlation headers
        var (testName, testId, testClass) = ExtractTestCorrelationHeaders(context);

        // Add metadata to Activity (OpenTelemetry) and logging scope
        var activity = Activity.Current;
        if (activity != null)
        {
            AddTestMetadataToActivityAndScope(activity, testName, testId, testClass, scope);
        }

        using (logger.BeginScope(scope))
        {
            await next(context);
        }
    }

    /// <summary>
    /// Extracts query parameters from the request and adds them to the logging scope.
    /// </summary>
    private static void AddQueryToScope(HttpContext context, Dictionary<string, object> scope)
    {
        var query = context.Request.Query;
        if (query.Count > 0)
        {
            scope["Query"] = JsonSerializer.Serialize(query);
        }
    }

    /// <summary>
    /// Extracts test correlation headers (X-Test-Name, X-Test-Id, X-Test-Class) from the request.
    /// </summary>
    /// <returns>A tuple containing the test name, test ID, and test class (all nullable).</returns>
    private static (string? TestName, string? TestId, string? TestClass) ExtractTestCorrelationHeaders(HttpContext context)
    {
        var headers = context.Request.Headers;

        var testName = headers.TryGetValue("X-Test-Name", out var testNameValue)
            ? testNameValue.ToString()
            : null;

        var testId = headers.TryGetValue("X-Test-Id", out var testIdValue)
            ? testIdValue.ToString()
            : null;

        var testClass = headers.TryGetValue("X-Test-Class", out var testClassValue)
            ? testClassValue.ToString()
            : null;

        return (testName, testId, testClass);
    }

    /// <summary>
    /// Adds test correlation metadata to both Activity tags (for OpenTelemetry) and logging scope.
    /// </summary>
    private void AddTestMetadataToActivityAndScope(
        Activity activity,
        string? testName,
        string? testId,
        string? testClass,
        Dictionary<string, object> scope)
    {
        var hasTestMetadata = false;

        if (testName != null)
        {
            activity.SetTag("test.name", testName);
            scope["TestName"] = testName;
            hasTestMetadata = true;
        }

        if (testId != null)
        {
            activity.SetTag("test.id", testId);
            scope["TestId"] = testId;
            hasTestMetadata = true;
        }

        if (testClass != null)
        {
            activity.SetTag("test.class", testClass);
            scope["TestClass"] = testClass;
            hasTestMetadata = true;
        }

        // Add TraceId to scope for local logging
        scope["TraceId"] = activity.TraceId.ToString();

        if (hasTestMetadata)
        {
            LogAddedTestMetadataToActivity(
                testName ?? "null",
                testId ?? "null",
                testClass ?? "null",
                activity.TraceId.ToString());
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

    [LoggerMessage(7, LogLevel.Trace, "{Location}: Added test metadata to activity: TestName={TestName}, TestId={TestId}, TestClass={TestClass}, TraceId={TraceId}")]
    private partial void LogAddedTestMetadataToActivity(string testName, string testId, string testClass, string traceId, [CallerMemberName] string? location = null);

    /// <summary>
    /// Simple DTO for deserializing test correlation metadata from cookie.
    /// </summary>
    private record TestCorrelationMetadata(string? Name, string? Id, string? Class, string? Traceparent);
}
