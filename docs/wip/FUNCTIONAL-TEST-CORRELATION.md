# Test Correlation Implementation Examples

This document provides complete, production-ready code examples for implementing test-log correlation in a new project using both approaches.

## Prerequisites

- ASP.NET Core 8.0+ application
- Playwright for functional testing
- OpenTelemetry (optional but recommended for Option 2)
- Application Insights or similar telemetry backend (optional)

## Option 1: Custom Headers (Simpler)

### Test Side Implementation

Create a base test class that all functional tests inherit from:

```csharp
// FunctionalTest.cs - Base class for all functional tests
using Microsoft.Playwright.NUnit;
using System.Web;

public abstract class FunctionalTest : PageTest
{
    protected ObjectStore _objectStore = new();
    protected Uri? baseUrl;

    public override BrowserNewContextOptions ContextOptions() =>
    new()
    {
        AcceptDownloads = true,
        ViewportSize = new ViewportSize() { Width = 1280, Height = 720 },
        BaseURL = checkEnvironment(TestContext.Parameters["webAppUrl"]!),
        ExtraHTTPHeaders = new Dictionary<string, string>
        {
            ["X-Test-Name"] = HttpUtility.UrlEncode(TestContext.CurrentContext.Test.Name),
            ["X-Test-Id"] = Guid.NewGuid().ToString(),
            ["X-Test-Class"] = TestContext.CurrentContext.Test.ClassName ?? "Unknown"
        }
    };
```

### Server Side Implementation

Create ASP.NET Core middleware to extract test correlation:

```csharp
// Middleware/TestCorrelationMiddleware.cs
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

public class TestCorrelationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TestCorrelationMiddleware> _logger;

    public TestCorrelationMiddleware(
        RequestDelegate next,
        ILogger<TestCorrelationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
{
    var scope = new Dictionary<string, object>();

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
    var testName = context.Request.Headers.TryGetValue("X-Test-Name", out var headerValue)
        ? headerValue.ToString()
        : null;

    if (testName != null)
    {
        scope["TestName"] = testName;
    }

    //
    // Extract TestId from headers
    //
    if (context.Request.Headers.TryGetValue("X-Test-Id", out var testIdValue))
    {
        scope["TestId"] = testIdValue.ToString();
    }

    //
    // Extract TestClass from headers
    //
    if (context.Request.Headers.TryGetValue("X-Test-Class", out var testClassValue))
    {
        scope["TestClass"] = testClassValue.ToString();
    }

    using (_logger.BeginScope(scope))
    {
        await _next(context);
    }
}

// Register in Program.cs
app.UseMiddleware<TestCorrelationMiddleware>();
```

## Option 2: Activity + Headers (Hybrid - Recommended for OpenTelemetry Users)

### Test Side Implementation

```csharp
// FunctionalTest.cs - Base class for all functional tests
using Microsoft.Playwright.NUnit;
using System.Diagnostics;
using System.Web;

public abstract class FunctionalTest : PageTest
{
    // ... existing fields ...
    
    protected Activity? _testActivity;

    [SetUp]
    public async Task SetUp()
    {
        Playwright.Selectors.SetTestIdAttribute("data-test-id");

        if (Int32.TryParse(TestContext.Parameters["defaultTimeout"], out var val))
            Context.SetDefaultTimeout(val);

        _objectStore = new ObjectStore();
        
        _objectStore.Add("Tester", new UserDetails() 
        {
            Email = checkEnvironment(TestContext.Parameters["userName"]!),
            Password = checkEnvironment(TestContext.Parameters["userPassword"]!)
        });

        //
        // Create test activity for distributed tracing
        //
        var testName = TestContext.CurrentContext.Test.Name;
        var testClass = TestContext.CurrentContext.Test.ClassName ?? "Unknown";
        var testId = Guid.NewGuid().ToString();
        
        _testActivity = new Activity("FunctionalTest");
        _testActivity.SetTag("test.name", testName);
        _testActivity.SetTag("test.class", testClass);
        _testActivity.SetTag("test.id", testId);
        _testActivity.SetTag("test.framework", "NUnit");
        _testActivity.Start();

        //
        // Set headers for both W3C trace propagation and direct correlation
        //
        var traceParent = $"00-{_testActivity.TraceId}-{_testActivity.SpanId}-01";
        
        await Context.SetExtraHTTPHeadersAsync(new Dictionary<string, string>
        {
            // W3C Trace Context standard
            ["traceparent"] = traceParent,
            
            // Direct test correlation (fallback and convenience)
            ["X-Test-Name"] = HttpUtility.UrlEncode(testName),
            ["X-Test-Id"] = testId,
            ["X-Test-Class"] = testClass
        });
    }

    [TearDown]
    public void TearDown()
    {
        _testActivity?.Stop();
        _testActivity?.Dispose();
    }

    // ... rest of existing code ...
}
```

### Server Side Implementation

```csharp
// Middleware/TestCorrelationMiddleware.cs
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

public class TestCorrelationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TestCorrelationMiddleware> _logger;

    public TestCorrelationMiddleware(
        RequestDelegate next,
        ILogger<TestCorrelationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var scope = new Dictionary<string, object>();
        var activity = Activity.Current; // ASP.NET Core auto-creates this

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

        using (_logger.BeginScope(scope))
        {
            await _next(context);
        }
    }
}
```

### Custom Log Formatter (Optional)

If you want custom console log formatting showing test names:

```csharp
// Logging/TerseConsoleLogFormatter.cs
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

public sealed class TerseConsoleLogFormatter : ConsoleFormatter
{
    private readonly TerseConsoleLogOptions _options;

    public TerseConsoleLogFormatter(IOptions<TerseConsoleLogOptions> options)
        : base("TerseConsole")
    {
        _options = options.Value;
    }

    public override void Write<TState>(
    in LogEntry<TState> logEntry,
    IExternalScopeProvider? scopeProvider,
    TextWriter textWriter)
{
    var message = logEntry.Formatter?.Invoke(logEntry.State, logEntry.Exception);
    if (message is null) return;

    var category = logEntry.Category;
    var testName = default(string);
    var traceId = default(string);
    var collectedScopes = new List<KeyValuePair<string, object>>();

    scopeProvider?.ForEachScope((x, t) => 
    {
        if (x is IEnumerable<KeyValuePair<string, object>> values)
        {
            var found = values.Where(y => y.Key == "ActionName");
            if (found.Any())
            {
                var kvp = found.FirstOrDefault();
                var action = kvp.Value?.ToString() ?? string.Empty;
                var match = ActionRegex().Match(action);
                if (match.Success)
                {
                    var first = match.Groups[1].Value;
                    var split = first.Split('.');
                    var chosen = split[^2..];
                    category = string.Join('.', chosen).Replace("Controller", "");
                }
            }
            
            found = values.Where(y => y.Key == "TestName");
            if (found.Any())
            {
                testName = found.FirstOrDefault().Value?.ToString();
            }
            
            found = values.Where(y => y.Key == "TraceId");
            if (found.Any())
            {
                var fullTraceId = found.FirstOrDefault().Value?.ToString();
                // Show first 8 chars of TraceId for readability
                traceId = fullTraceId?.Length > 8 ? fullTraceId[0..8] : fullTraceId;
            }

            collectedScopes.AddRange(values);
        }
    }, typeof(TState));

    // Enhanced format with optional TraceId
    // Example: INF 02/21 02:02:48 [1003] trace:b4982c97 /TestLoginSuccess/ Identity.Login: OK
    textWriter.Write(
        "{0} {1} [{2:D4}] {3}{4}{5}: ",
        logEntry.LogLevel.ToString().ToUpper()[0..3],
        DateTimeOffset.Now.ToString("MM/dd HH:mm:ss"),
        category.StartsWith("Microsoft") ? "****" : logEntry.EventId.Id.ToString("D4"),
        traceId != null ? $"trace:{traceId} " : string.Empty,
        testName != null ? $"/{testName}/ " : string.Empty,
        category
    );
    textWriter.WriteLine(message);

    if (logEntry.Exception != null)
    {
        textWriter.WriteLine(
            "                EXCEPTION {0}: {1}",
            logEntry.Exception.GetType().Name,
            logEntry.Exception.Message
        );
        textWriter.WriteLine();
    }

    if (_options.IncludeScopes)
    {
        foreach (var kvp in collectedScopes)
        {
            textWriter.WriteLine("    {0}: {1}", kvp.Key, kvp.Value);
        }
    }
}

public sealed class TerseConsoleLogOptions : ConsoleFormatterOptions { }

// Register in Program.cs
builder.Logging.AddConsole(options => options.FormatterName = "TerseConsole")
    .AddConsoleFormatter<TerseConsoleLogFormatter, TerseConsoleLogOptions>();
```

## Application Insights Queries

With the Activity approach, you can query in Application Insights:

### Find All Logs for a Test

```kql
traces
| where customDimensions.["test.name"] == "TestLoginSuccess"
| project timestamp, message, severityLevel, customDimensions
| order by timestamp desc
```

### Find Failing Tests

```kql
union traces, exceptions
| where customDimensions.["test.id"] != ""
| where severityLevel >= 3 or itemType == "exception"
| project 
    timestamp,
    testName = customDimensions.["test.name"],
    testId = customDimensions.["test.id"],
    testClass = customDimensions.["test.class"],
    message,
    itemType
| order by timestamp desc
```

### Test Performance Over Time

```kql
requests
| where customDimensions.["test.name"] != ""
| summarize 
    avgDuration = avg(duration),
    p95Duration = percentile(duration, 95),
    count = count()
    by bin(timestamp, 1h), testName = tostring(customDimensions.["test.name"])
| order by timestamp desc
```

### Distributed Trace View

```kql
// Find all operations in a test execution
dependencies
| where operation_Id == "your-trace-id-here"
| union (requests | where operation_Id == "your-trace-id-here")
| project timestamp, name, type, duration, success
| order by timestamp asc
```

## Local Development Console Output

### With Headers Only
```
INF 12/14 22:15:32 [1003] /TestLoginSuccess/ Identity.Login: OK
INF 12/14 22:15:32 [1004] /TestLoginSuccess/ Identity.Access: OK 5 entitlements
```

### With Activity + Headers
```
INF 12/14 22:15:32 [1003] trace:b4982c97 /TestLoginSuccess/ Identity.Login: OK
INF 12/14 22:15:32 [1004] trace:b4982c97 /TestLoginSuccess/ Identity.Access: OK 5 entitlements
```

Notice the TraceId prefix makes it even easier to correlate related operations.

## Required NuGet Packages

For Option 2 (Activity approach), ensure you have:

```xml
<ItemGroup>
  <!-- Already in Tests.Functional -->
  <PackageReference Include="Microsoft.Playwright.NUnit" Version="..." />
  
  <!-- Already in Main.Vue via Azure.Monitor.OpenTelemetry.AspNetCore -->
  <PackageReference Include="System.Diagnostics.DiagnosticSource" Version="..." />
</ItemGroup>
```

No additional packages needed - you already have everything!

## Implementation Checklist

### For New Project Setup

- [ ] Create `TestCorrelationMiddleware` class
- [ ] Register middleware in Program.cs: `app.UseMiddleware<TestCorrelationMiddleware>();`
- [ ] Create base `FunctionalTest` class with header configuration
- [ ] (Optional) Create custom log formatter
- [ ] (Optional) Configure OpenTelemetry if using Option 2
- [ ] Test with sample functional test
- [ ] Document approach for team

## Summary

Both approaches are production-ready. Choose based on:

- **Option 1 (Headers)**: Simpler, works everywhere, good for local dev
- **Option 2 (Activity + Headers)**: Leverages your OpenTelemetry investment, enables powerful Application Insights queries, industry standard

Given your existing OpenTelemetry + Application Insights setup, **Option 2 is recommended** for the new project.