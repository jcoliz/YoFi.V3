## Where to Place Logging Policies

Status: DRAFT

Logging policies can be implemented in multiple ways, and for a scalable application, I'd recommend both written documentation and code implementation.

### 1. **Written Policy** (Documentation)
Create a new documentation file:

````markdown
# Logging Policy

## Log Levels and Usage

### TRACE
- **When**: Extremely detailed diagnostic information
- **Examples**: Method entry/exit with parameters, loop iterations
- **Environment**: Development only
- **Retention**: 1 day

### DEBUG
- **When**: Diagnostic information for debugging
- **Examples**: Configuration values, intermediate calculations, cache hits/misses
- **Environment**: Development and staging
- **Retention**: 7 days

### INFORMATION
- **When**: General application flow, business events
- **Examples**: User logged in, transaction created, external API called
- **Environment**: All environments
- **Retention**: 30 days

### WARNING
- **When**: Potentially harmful situations that don't stop execution
- **Examples**: Deprecated API usage, fallback scenarios, retry attempts
- **Environment**: All environments
- **Retention**: 90 days

### ERROR
- **When**: Error events that don't stop application
- **Examples**: Handled exceptions, validation failures, external service unavailable
- **Environment**: All environments
- **Retention**: 1 year

### CRITICAL
- **When**: Serious errors that might cause application termination
- **Examples**: Database connection lost, unhandled exceptions, security breaches
- **Environment**: All environments
- **Retention**: Permanent

## Sensitive Data Policy
- **Never log**: Passwords, credit card numbers, SSNs, tokens
- **Mask**: Email addresses (user@******.com), IP addresses (last octet)
- **Hash**: User IDs when needed for correlation

## Structured Logging Format
```json
{
  "timestamp": "2025-11-18T10:30:00Z",
  "level": "Information",
  "message": "User logged in successfully",
  "userId": "hashed_user_id",
  "correlationId": "abc123",
  "source": "AuthenticationService",
  "environment": "Production"
}
```
````

### 2. **Code Implementation** (Enforcement)

Create a logging configuration class:

````csharp
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace YoFi.V3.ServiceDefaults.Logging;

public static class LoggingPolicyExtensions
{
    public static IServiceCollection AddLoggingPolicy(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure Serilog with policy-based settings
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .Enrich.WithCorrelationId()
            .Enrich.WithEnvironmentName()
            .Filter.ByExcluding(IsHealthCheckRequest)
            .WriteTo.Console(outputTemplate: GetOutputTemplate())
            .WriteTo.Conditional(evt => IsProduction(), 
                wt => wt.ApplicationInsights(configuration.GetConnectionString("ApplicationInsights")))
            .CreateLogger();

        services.AddSerilog(Log.Logger);
        return services;
    }

    private static string GetOutputTemplate()
    {
        return "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj} " +
               "{Properties:j}{NewLine}{Exception}";
    }

    private static bool IsHealthCheckRequest(LogEvent logEvent)
    {
        return logEvent.Properties.ContainsKey("RequestPath") &&
               logEvent.Properties["RequestPath"].ToString().Contains("/health");
    }

    private static bool IsProduction()
    {
        return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production";
    }
}
````

### 3. **Policy Enforcement Attributes**

Create custom logging attributes to enforce policies:

````csharp
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace YoFi.V3.ServiceDefaults.Logging;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class LoggingPolicyAttribute : Attribute
{
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;
    public bool LogParameters { get; set; } = false;
    public bool LogResult { get; set; } = false;
    public string[] SensitiveParameters { get; set; } = Array.Empty<string>();
}

// Usage example:
[LoggingPolicy(MinimumLevel = LogLevel.Debug, LogParameters = true, 
               SensitiveParameters = new[] { "password", "token" })]
public async Task<LoginResult> LoginAsync(string email, string password)
{
    // Implementation
}
````

### 4. **Configuration-Based Policies**

Add to your appsettings:

````json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning",
        "YoFi.V3.Application.Features": "Debug",
        "YoFi.V3.Entities": "Information"
      }
    },
    "WriteTo": [
      {
        "Name": "Console"
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/app-.txt",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName"],
    "Properties": {
      "Application": "YoFi.V3"
    }
  },
  "LoggingPolicy": {
    "EnablePerformanceLogging": true,
    "EnableSensitiveDataMasking": true,
    "MaximumMessageLength": 1000,
    "EnvironmentSpecific": {
      "Development": {
        "LogSqlQueries": true,
        "LogHttpRequests": true
      },
      "Production": {
        "LogSqlQueries": false,
        "LogHttpRequests": false
      }
    }
  }
}
````

### 5. **Middleware for Policy Enforcement**

````csharp
public class LoggingPolicyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<LoggingPolicyMiddleware> _logger;

    public LoggingPolicyMiddleware(RequestDelegate next, ILogger<LoggingPolicyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Apply logging policies based on request context
        using (LogContext.PushProperty("UserId", GetUserId(context)))
        using (LogContext.PushProperty("CorrelationId", GetCorrelationId(context)))
        {
            await _next(context);
        }
    }

    private string GetUserId(HttpContext context)
    {
        // Extract and hash user ID for privacy
        var userId = context.User?.FindFirst("sub")?.Value;
        return userId != null ? HashUserId(userId) : "anonymous";
    }
}
````

## Summary

**Written policies** go in `docs/LOGGING-POLICY.md` for team reference, while **code implementation** goes in:

- `src/ServiceDefaults/Logging/` - Core logging infrastructure
- `appsettings.json` - Environment-specific configuration
- Individual feature classes - Specific logging implementations

This dual approach ensures both human understanding and automated enforcement of your logging standards as you scale up! 📝