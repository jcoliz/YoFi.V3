## Recommended Error Handling Approach for ASP.NET Web APIs

### 1. **Use Problem Details (RFC 7807)** - Industry Standard

ASP.NET Core has built-in support for RFC 7807, which provides standardized error responses:

```csharp
// In Program.cs
builder.Services.AddProblemDetails();

// In the pipeline (order matters - early in pipeline)
app.UseExceptionHandler();
app.UseStatusCodePages();
```

**What you get:**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "Validation failed for field 'email'",
  "traceId": "00-abc123-def456-00"
}
```

### 2. **Global Exception Handling Middleware** - Centralized Control

Instead of try-catch in every controller action (like your current [`WeatherController`](src/Controllers/WeatherController.cs:21-34)), use middleware:

```csharp
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(ex, "Validation error");
            await WriteValidationErrorResponse(context, ex);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "Resource not found");
            await WriteNotFoundResponse(context, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteServerErrorResponse(context, ex);
        }
    }

    private static async Task WriteValidationErrorResponse(
        HttpContext context, ValidationException ex)
    {
        context.Response.StatusCode = 400;
        context.Response.ContentType = "application/problem+json";

        var problemDetails = new ProblemDetails
        {
            Status = 400,
            Title = "Validation Error",
            Detail = ex.Message,
            Instance = context.Request.Path
        };

        await context.Response.WriteAsJsonAsync(problemDetails);
    }
}
```

### 3. **Simplified Controller Code** - Let Middleware Handle It

Your controllers become much cleaner:

```csharp
// BEFORE (current approach in WeatherController)
[HttpGet]
public async Task<IActionResult> GetWeatherForecasts()
{
    try
    {
        var weather = await weatherFeature.GetWeatherForecasts(5);
        return Ok(weather);
    }
    catch (Exception ex)
    {
        LogErrorFetchingWeatherForecasts(ex);
        return StatusCode(500, ex.Message);
    }
}

// AFTER (with global exception middleware)
[HttpGet]
public async Task<IActionResult> GetWeatherForecasts()
{
    LogFetchingWeatherForecasts();
    var weather = await weatherFeature.GetWeatherForecasts(5);
    LogSuccessfullyFetchedWeatherForecasts(weather.Length);
    return Ok(weather);
}
```

### 4. **Custom Exception Types** - Domain-Specific Errors

Create meaningful exceptions that middleware can handle appropriately:

```csharp
public class NotFoundException : Exception
{
    public NotFoundException(string resource, object key)
        : base($"{resource} with key '{key}' was not found.") { }
}

public class ValidationException : Exception
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }
}

public class BusinessRuleException : Exception
{
    public string RuleViolated { get; }

    public BusinessRuleException(string rule, string message)
        : base(message)
    {
        RuleViolated = rule;
    }
}
```

### 5. **Built-in `UseExceptionHandler` with Custom Handler**

ASP.NET Core's recommended approach:

```csharp
// In Program.cs
app.UseExceptionHandler(exceptionHandlerApp =>
{
    exceptionHandlerApp.Run(async context =>
    {
        var exceptionHandlerFeature =
            context.Features.Get<IExceptionHandlerFeature>();
        var exception = exceptionHandlerFeature?.Error;

        var logger = context.RequestServices
            .GetRequiredService<ILogger<Program>>();

        logger.LogError(exception, "Unhandled exception occurred");

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An error occurred",
            Detail = exception?.Message,
            Instance = context.Request.Path
        };

        // Don't expose internal details in production
        if (!context.RequestServices
            .GetRequiredService<IWebHostEnvironment>()
            .IsDevelopment())
        {
            problemDetails.Detail = "An internal error occurred";
        }

        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problemDetails);
    });
});
```

### 6. **Recommended Pipeline Configuration**

```csharp
// In Program.cs - Order is critical!
var app = builder.Build();

// 1. Exception handling (must be early)
app.UseExceptionHandler();

// 2. Handle status codes without body content
app.UseStatusCodePages();

// 3. HTTPS redirection
app.UseHttpsRedirection();

// 4. CORS (if needed)
app.UseCors();

// 5. Authentication/Authorization
app.UseAuthentication();
app.UseAuthorization();

// 6. Your endpoints
app.MapControllers();
```

### Key Principles

1. **Centralize error handling** - Don't use try-catch in controllers
2. **Use Problem Details** - Industry standard JSON error format
3. **Log appropriately** - Errors at middleware level, success at controller level
4. **Hide internal details** - Only expose stack traces in development
5. **Consistent responses** - Same error format across all endpoints
6. **HTTP status codes** - Use correct codes (400 for validation, 404 for not found, 500 for server errors)

### What to Avoid

❌ `app.UseStatusCodePages()` alone - too basic for APIs
❌ Try-catch in every controller action - repetitive and error-prone
❌ Returning `ex.Message` to clients - can expose sensitive information
❌ Different error formats per endpoint - inconsistent API experience

This approach aligns with your existing architecture (Clean Architecture with logging policy) and provides production-ready error handling.

---

Let me show you exactly what the Problem Details responses would look like for each catch block in the GlobalExceptionMiddleware example.

## Problem Details Response Examples

### 1. **ValidationException** (400 Bad Request)

```csharp
catch (ValidationException ex)
{
    _logger.LogWarning(ex, "Validation error");
    await WriteValidationErrorResponse(context, ex);
}

private static async Task WriteValidationErrorResponse(
    HttpContext context, ValidationException ex)
{
    context.Response.StatusCode = 400;
    context.Response.ContentType = "application/problem+json";

    var problemDetails = new ValidationProblemDetails(ex.Errors)
    {
        Status = 400,
        Title = "One or more validation errors occurred",
        Detail = ex.Message,
        Instance = context.Request.Path,
        Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1"
    };

    await context.Response.WriteAsJsonAsync(problemDetails);
}
```

**Response JSON:**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred",
  "status": 400,
  "detail": "Validation failed",
  "instance": "/api/weather",
  "errors": {
    "email": ["Email is required", "Email format is invalid"],
    "age": ["Age must be between 0 and 120"]
  },
  "traceId": "00-abc123def456-789-00"
}
```

### 2. **NotFoundException** (404 Not Found)

```csharp
catch (NotFoundException ex)
{
    _logger.LogWarning(ex, "Resource not found");
    await WriteNotFoundResponse(context, ex);
}

private static async Task WriteNotFoundResponse(
    HttpContext context, NotFoundException ex)
{
    context.Response.StatusCode = 404;
    context.Response.ContentType = "application/problem+json";

    var problemDetails = new ProblemDetails
    {
        Status = 404,
        Title = "Resource Not Found",
        Detail = ex.Message,
        Instance = context.Request.Path,
        Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4"
    };

    await context.Response.WriteAsJsonAsync(problemDetails);
}
```

**Response JSON:**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Resource Not Found",
  "status": 404,
  "detail": "Forecast with key '12345' was not found.",
  "instance": "/api/weather/12345",
  "traceId": "00-abc123def456-789-00"
}
```

### 3. **Unhandled Exception** (500 Internal Server Error)

```csharp
catch (Exception ex)
{
    _logger.LogError(ex, "Unhandled exception");
    await WriteServerErrorResponse(context, ex);
}

private static async Task WriteServerErrorResponse(
    HttpContext context, Exception ex)
{
    var isDevelopment = context.RequestServices
        .GetRequiredService<IWebHostEnvironment>()
        .IsDevelopment();

    context.Response.StatusCode = 500;
    context.Response.ContentType = "application/problem+json";

    var problemDetails = new ProblemDetails
    {
        Status = 500,
        Title = "Internal Server Error",
        Detail = isDevelopment
            ? ex.Message
            : "An unexpected error occurred. Please try again later.",
        Instance = context.Request.Path,
        Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1"
    };

    // Add stack trace in development only
    if (isDevelopment)
    {
        problemDetails.Extensions["stackTrace"] = ex.StackTrace;
        problemDetails.Extensions["exceptionType"] = ex.GetType().Name;
    }

    await context.Response.WriteAsJsonAsync(problemDetails);
}
```

**Response JSON (Development):**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.6.1",
  "title": "Internal Server Error",
  "status": 500,
  "detail": "Object reference not set to an instance of an object.",
  "instance": "/api/weather",
  "stackTrace": "   at YoFi.V3.Application.WeatherFeature...\n   at ...",
  "exceptionType": "NullReferenceException",
  "traceId": "00-abc123def456-789-00"
}
```

**Response JSON (Production):**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.6.1",
  "title": "Internal Server Error",
  "status": 500,
  "detail": "An unexpected error occurred. Please try again later.",
  "instance": "/api/weather",
  "traceId": "00-abc123def456-789-00"
}
```

## Key Differences Between Each Response

| Property | ValidationException (400) | NotFoundException (404) | Unhandled Exception (500) |
|----------|---------------------------|-------------------------|---------------------------|
| **Status** | 400 | 404 | 500 |
| **Title** | "One or more validation errors occurred" | "Resource Not Found" | "Internal Server Error" |
| **Detail** | Validation message | Specific resource message | Generic (prod) / Specific (dev) |
| **Type** | RFC 7231 §6.5.1 | RFC 7231 §6.5.4 | RFC 7231 §6.6.1 |
| **Errors** | ✅ Dictionary of field errors | ❌ Not included | ❌ Not included |
| **StackTrace** | ❌ Not included | ❌ Not included | ✅ Development only |

## Standard Problem Details Properties

All responses include these standard RFC 7807 properties:

- **`type`** - URI reference identifying the problem type
- **`title`** - Short, human-readable summary
- **`status`** - HTTP status code
- **`detail`** - Human-readable explanation specific to this occurrence
- **`instance`** - URI reference identifying the specific occurrence
- **`traceId`** - (ASP.NET Core automatically adds this for correlation)

## Additional Custom Properties

You can extend Problem Details with custom properties:

```csharp
var problemDetails = new ProblemDetails
{
    Status = 400,
    Title = "Business Rule Violation",
    Detail = "Cannot delete forecast that is in use",
    Instance = context.Request.Path
};

// Add custom extensions
problemDetails.Extensions["ruleId"] = "FORECAST_IN_USE";
problemDetails.Extensions["affectedEntities"] = new[] { "forecast-123" };
problemDetails.Extensions["suggestedAction"] = "Remove dependencies first";
```

**Response:**
```json
{
  "type": "about:blank",
  "title": "Business Rule Violation",
  "status": 400,
  "detail": "Cannot delete forecast that is in use",
  "instance": "/api/weather/123",
  "ruleId": "FORECAST_IN_USE",
  "affectedEntities": ["forecast-123"],
  "suggestedAction": "Remove dependencies first",
  "traceId": "00-abc123def456-789-00"
}
```

This standardized format ensures consistent error responses across your entire API, making it easier for frontend clients to parse and display errors appropriately.

---

Great question! Let me show you several approaches to control what goes into the Problem Details response for `BusinessRuleException`.

## Approach 1: Add Custom Catch Block in Middleware

Add a specific catch block for `BusinessRuleException` before the generic `Exception` handler:

```csharp
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(ex, "Validation error");
            await WriteValidationErrorResponse(context, ex);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "Resource not found");
            await WriteNotFoundResponse(context, ex);
        }
        catch (BusinessRuleException ex)  // ← Add this BEFORE generic Exception
        {
            _logger.LogWarning(ex, "Business rule violation: {RuleViolated}", ex.RuleViolated);
            await WriteBusinessRuleErrorResponse(context, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteServerErrorResponse(context, ex);
        }
    }

    private static async Task WriteBusinessRuleErrorResponse(
        HttpContext context, BusinessRuleException ex)
    {
        context.Response.StatusCode = 422; // Unprocessable Entity
        context.Response.ContentType = "application/problem+json";

        var problemDetails = new ProblemDetails
        {
            Status = 422,
            Title = "Business Rule Violation",
            Detail = ex.Message,
            Instance = context.Request.Path,
            Type = "https://datatracker.ietf.org/doc/html/rfc4918#section-11.2"
        };

        // Add custom properties from the exception
        problemDetails.Extensions["ruleViolated"] = ex.RuleViolated;

        // Optionally add more context if available
        if (ex.Data.Count > 0)
        {
            problemDetails.Extensions["additionalInfo"] = ex.Data;
        }

        await context.Response.WriteAsJsonAsync(problemDetails);
    }
}
```

**Response JSON:**
```json
{
  "type": "https://datatracker.ietf.org/doc/html/rfc4918#section-11.2",
  "title": "Business Rule Violation",
  "status": 422,
  "detail": "Cannot delete forecast that has active dependencies",
  "instance": "/api/weather/123",
  "ruleViolated": "FORECAST_HAS_DEPENDENCIES",
  "traceId": "00-abc123def456-789-00"
}
```

## Approach 2: Rich BusinessRuleException with Custom Properties

Enhance the exception class to carry more structured information:

```csharp
public class BusinessRuleException : Exception
{
    public string RuleViolated { get; }
    public string SuggestedAction { get; }
    public int StatusCode { get; }
    public Dictionary<string, object> CustomData { get; }

    public BusinessRuleException(
        string ruleViolated,
        string message,
        string suggestedAction = null,
        int statusCode = 422)
        : base(message)
    {
        RuleViolated = ruleViolated;
        SuggestedAction = suggestedAction;
        StatusCode = statusCode;
        CustomData = new Dictionary<string, object>();
    }

    public BusinessRuleException WithData(string key, object value)
    {
        CustomData[key] = value;
        return this;
    }
}

// Usage in your application layer:
throw new BusinessRuleException(
    ruleViolated: "FORECAST_HAS_DEPENDENCIES",
    message: "Cannot delete forecast that has active dependencies",
    suggestedAction: "Remove all dependencies before deleting",
    statusCode: 422
).WithData("dependencyCount", 5)
 .WithData("affectedResources", new[] { "resource-1", "resource-2" });
```

**Middleware handler:**
```csharp
private static async Task WriteBusinessRuleErrorResponse(
    HttpContext context, BusinessRuleException ex)
{
    context.Response.StatusCode = ex.StatusCode;
    context.Response.ContentType = "application/problem+json";

    var problemDetails = new ProblemDetails
    {
        Status = ex.StatusCode,
        Title = "Business Rule Violation",
        Detail = ex.Message,
        Instance = context.Request.Path,
        Type = "https://datatracker.ietf.org/doc/html/rfc4918#section-11.2"
    };

    // Add all custom properties from the exception
    problemDetails.Extensions["ruleViolated"] = ex.RuleViolated;

    if (!string.IsNullOrEmpty(ex.SuggestedAction))
    {
        problemDetails.Extensions["suggestedAction"] = ex.SuggestedAction;
    }

    // Add any custom data
    foreach (var kvp in ex.CustomData)
    {
        problemDetails.Extensions[kvp.Key] = kvp.Value;
    }

    await context.Response.WriteAsJsonAsync(problemDetails);
}
```

**Response JSON:**
```json
{
  "type": "https://datatracker.ietf.org/doc/html/rfc4918#section-11.2",
  "title": "Business Rule Violation",
  "status": 422,
  "detail": "Cannot delete forecast that has active dependencies",
  "instance": "/api/weather/123",
  "ruleViolated": "FORECAST_HAS_DEPENDENCIES",
  "suggestedAction": "Remove all dependencies before deleting",
  "dependencyCount": 5,
  "affectedResources": ["resource-1", "resource-2"],
  "traceId": "00-abc123def456-789-00"
}
```

## Approach 3: Interface-Based Approach for Maximum Flexibility

Create an interface that exceptions can implement to control their own Problem Details:

```csharp
public interface IProblemDetailsProvider
{
    ProblemDetails ToProblemDetails(HttpContext context);
}

public class BusinessRuleException : Exception, IProblemDetailsProvider
{
    public string RuleViolated { get; }
    public string SuggestedAction { get; }
    public int StatusCode { get; }
    public Dictionary<string, object> Extensions { get; }

    public BusinessRuleException(
        string ruleViolated,
        string message,
        string suggestedAction = null,
        int statusCode = 422)
        : base(message)
    {
        RuleViolated = ruleViolated;
        SuggestedAction = suggestedAction;
        StatusCode = statusCode;
        Extensions = new Dictionary<string, object>();
    }

    public ProblemDetails ToProblemDetails(HttpContext context)
    {
        var problemDetails = new ProblemDetails
        {
            Status = StatusCode,
            Title = "Business Rule Violation",
            Detail = Message,
            Instance = context.Request.Path,
            Type = "https://datatracker.ietf.org/doc/html/rfc4918#section-11.2"
        };

        problemDetails.Extensions["ruleViolated"] = RuleViolated;

        if (!string.IsNullOrEmpty(SuggestedAction))
        {
            problemDetails.Extensions["suggestedAction"] = SuggestedAction;
        }

        foreach (var kvp in Extensions)
        {
            problemDetails.Extensions[kvp.Key] = kvp.Value;
        }

        return problemDetails;
    }

    public BusinessRuleException WithExtension(string key, object value)
    {
        Extensions[key] = value;
        return this;
    }
}
```

**Simplified middleware:**
```csharp
catch (BusinessRuleException ex)
{
    _logger.LogWarning(ex, "Business rule violation: {RuleViolated}", ex.RuleViolated);

    context.Response.StatusCode = ex.StatusCode;
    context.Response.ContentType = "application/problem+json";

    var problemDetails = ex.ToProblemDetails(context);
    await context.Response.WriteAsJsonAsync(problemDetails);
}
```

## Approach 4: Centralized Exception-to-ProblemDetails Mapping

Use a factory pattern for cleaner separation:

```csharp
public interface IExceptionMapper
{
    bool CanMap(Exception exception);
    ProblemDetails Map(Exception exception, HttpContext context);
}

public class BusinessRuleExceptionMapper : IExceptionMapper
{
    public bool CanMap(Exception exception)
        => exception is BusinessRuleException;

    public ProblemDetails Map(Exception exception, HttpContext context)
    {
        var ex = (BusinessRuleException)exception;

        var problemDetails = new ProblemDetails
        {
            Status = 422,
            Title = "Business Rule Violation",
            Detail = ex.Message,
            Instance = context.Request.Path,
            Type = "https://datatracker.ietf.org/doc/html/rfc4918#section-11.2"
        };

        problemDetails.Extensions["ruleViolated"] = ex.RuleViolated;
        problemDetails.Extensions["suggestedAction"] = ex.SuggestedAction;

        return problemDetails;
    }
}

// In middleware:
public class GlobalExceptionMiddleware
{
    private readonly IEnumerable<IExceptionMapper> _mappers;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        IEnumerable<IExceptionMapper> mappers)
    {
        _next = next;
        _mappers = mappers;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var mapper = _mappers.FirstOrDefault(m => m.CanMap(ex));

            if (mapper != null)
            {
                var problemDetails = mapper.Map(ex, context);
                context.Response.StatusCode = problemDetails.Status ?? 500;
                context.Response.ContentType = "application/problem+json";
                await context.Response.WriteAsJsonAsync(problemDetails);
            }
            else
            {
                // Fallback to generic error handler
                await WriteServerErrorResponse(context, ex);
            }
        }
    }
}
```

## Recommended Approach

**For most cases, I recommend Approach 2** (Rich BusinessRuleException) because:

✅ **Clear intent** - Exception properties explicitly define what goes in response
✅ **Type-safe** - Properties are strongly typed
✅ **Easy to use** - Fluent API makes throwing exceptions intuitive
✅ **Testable** - Easy to verify exception properties in tests
✅ **Centralized** - Middleware handles conversion to Problem Details

**Example usage in your application:**

```csharp
// In WeatherFeature.cs
public async Task DeleteForecast(int id)
{
    var dependencies = await CheckDependencies(id);

    if (dependencies.Any())
    {
        throw new BusinessRuleException(
            ruleViolated: "FORECAST_HAS_DEPENDENCIES",
            message: "Cannot delete forecast that has active dependencies",
            suggestedAction: "Remove dependencies or archive instead"
        )
        .WithData("dependencyCount", dependencies.Count)
        .WithData("dependencyTypes", dependencies.Select(d => d.Type).Distinct());
    }

    // proceed with deletion
}
```

This gives you full control over the Problem Details response while keeping your code clean and maintainable.

---

Excellent observation! The `traceId` is automatically added by ASP.NET Core when you serialize `ProblemDetails` using `WriteAsJsonAsync()`. Let me explain how it works and how to control it.

## How `traceId` Gets Added Automatically

When you call `context.Response.WriteAsJsonAsync(problemDetails)`, ASP.NET Core's `System.Text.Json` serializer uses a special converter called `ProblemDetailsJsonConverter` that automatically adds the `traceId` from the current HTTP context.

### Where it comes from:

```csharp
// In your middleware:
await context.Response.WriteAsJsonAsync(problemDetails);

// ASP.NET Core internally does this:
var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
// Adds it to the JSON output automatically
```

The `traceId` comes from:
1. **`Activity.Current.Id`** - If you're using distributed tracing (OpenTelemetry, Application Insights)
2. **`HttpContext.TraceIdentifier`** - ASP.NET Core's built-in correlation ID for each request

### Example trace ID format:

```
Activity.Current.Id format:
"00-abc123def456789-012345678-00"
└─┬─┘ └────┬──────┘ └───┬───┘ └┬┘
  │        │             │      └─ Flags
  │        │             └──────── Span ID (8 bytes)
  │        └────────────────────── Trace ID (16 bytes)
  └─────────────────────────────── Version (W3C format)

HttpContext.TraceIdentifier format (fallback):
"0HMVD8QK9N2KL:00000001"
```

## How to Control the traceId

### Option 1: It's Added Automatically (Do Nothing)

The simplest approach - just let ASP.NET Core add it:

```csharp
private static async Task WriteBusinessRuleErrorResponse(
    HttpContext context, BusinessRuleException ex)
{
    context.Response.StatusCode = 422;
    context.Response.ContentType = "application/problem+json";

    var problemDetails = new ProblemDetails
    {
        Status = 422,
        Title = "Business Rule Violation",
        Detail = ex.Message,
        Instance = context.Request.Path
    };

    // No need to manually add traceId!
    await context.Response.WriteAsJsonAsync(problemDetails);
}
```

**Result:**
```json
{
  "type": "about:blank",
  "title": "Business Rule Violation",
  "status": 422,
  "detail": "Cannot delete forecast",
  "instance": "/api/weather/123",
  "traceId": "00-abc123def456789-012345678-00"  // ← Added automatically
}
```

### Option 2: Explicitly Add It to Extensions

If you want more control or custom naming:

```csharp
private static async Task WriteBusinessRuleErrorResponse(
    HttpContext context, BusinessRuleException ex)
{
    var problemDetails = new ProblemDetails
    {
        Status = 422,
        Title = "Business Rule Violation",
        Detail = ex.Message,
        Instance = context.Request.Path
    };

    // Explicitly add trace ID (overrides automatic behavior)
    problemDetails.Extensions["traceId"] = Activity.Current?.Id
        ?? context.TraceIdentifier;

    // Or use a custom name:
    problemDetails.Extensions["correlationId"] = Activity.Current?.Id
        ?? context.TraceIdentifier;
    problemDetails.Extensions["requestId"] = context.TraceIdentifier;

    context.Response.StatusCode = 422;
    context.Response.ContentType = "application/problem+json";
    await context.Response.WriteAsJsonAsync(problemDetails);
}
```

**Result with custom names:**
```json
{
  "type": "about:blank",
  "title": "Business Rule Violation",
  "status": 422,
  "detail": "Cannot delete forecast",
  "instance": "/api/weather/123",
  "correlationId": "00-abc123def456789-012345678-00",
  "requestId": "0HMVD8QK9N2KL:00000001",
  "traceId": "00-abc123def456789-012345678-00"  // ← Still auto-added
}
```

### Option 3: Disable Automatic traceId

If you want to completely control it or remove it:

```csharp
// In Program.cs during service configuration:
builder.Services.Configure<JsonOptions>(options =>
{
    // Remove the ProblemDetails converter that auto-adds traceId
    options.SerializerOptions.Converters
        .RemoveAll(c => c is ProblemDetailsJsonConverter);
});
```

Then manually add it if needed:

```csharp
var problemDetails = new ProblemDetails
{
    Status = 422,
    Title = "Business Rule Violation",
    Detail = ex.Message,
    Instance = context.Request.Path
};

// Now you control if/when to add it
if (shouldIncludeTraceId)
{
    problemDetails.Extensions["traceId"] = context.TraceIdentifier;
}

await context.Response.WriteAsJsonAsync(problemDetails);
```

## Integration with Application Insights / OpenTelemetry

If you're using Application Insights (which you have configured in your project), the `traceId` is crucial for correlation:

```csharp
// Your logging in WeatherController already works with this:
LogErrorFetchingWeatherForecasts(ex);

// In Application Insights, you can search by traceId:
// - Find all logs for a specific request
// - Trace request flow across services
// - Link frontend errors to backend logs
```

### Example Application Insights query:

```kusto
// Find all telemetry for a specific request
traces
| where operation_Id == "abc123def456789"
| union (exceptions | where operation_Id == "abc123def456789")
| union (requests | where operation_Id == "abc123def456789")
| order by timestamp desc
```

## Best Practice: Include traceId for Debugging

**Recommended approach:**

```csharp
private static async Task WriteErrorResponse(
    HttpContext context,
    int statusCode,
    string title,
    string detail,
    Dictionary<string, object> extensions = null)
{
    var problemDetails = new ProblemDetails
    {
        Status = statusCode,
        Title = title,
        Detail = detail,
        Instance = context.Request.Path,
        Type = GetProblemTypeUri(statusCode)
    };

    // Add any custom extensions
    if (extensions != null)
    {
        foreach (var kvp in extensions)
        {
            problemDetails.Extensions[kvp.Key] = kvp.Value;
        }
    }

    // traceId will be auto-added by ASP.NET Core
    // Can be used to correlate with Application Insights logs

    context.Response.StatusCode = statusCode;
    context.Response.ContentType = "application/problem+json";
    await context.Response.WriteAsJsonAsync(problemDetails);
}
```

## Why traceId is Important

1. **Debugging** - Users can report the traceId when encountering errors
2. **Log Correlation** - Find all logs related to a specific request
3. **Distributed Tracing** - Track requests across multiple services
4. **Support** - Support teams can quickly locate the exact request in logs
5. **Monitoring** - Tools like Application Insights use it for request tracking

**User flow example:**
```
1. User gets error with traceId: "00-abc123..."
2. User reports: "I got an error, traceId: 00-abc123..."
3. Support searches Application Insights: operation_Id == "abc123"
4. Finds exact request, logs, exceptions, and timing
```

So in summary: **The `traceId` is automatically added by ASP.NET Core when you use `WriteAsJsonAsync(problemDetails)`**, and it's a valuable feature for debugging and observability that you should keep enabled.
