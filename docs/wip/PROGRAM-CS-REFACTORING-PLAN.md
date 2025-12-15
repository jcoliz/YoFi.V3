# Program.cs Refactoring Plan

## Executive Summary

This document proposes a comprehensive refactoring of [`Program.cs`](../../src/BackEnd/Program.cs) to improve organization, clarity, and maintainability. The current file is 191 lines and contains inline service registrations, middleware configuration, and startup logic. By extracting related configurations into cohesive extension methods, we can reduce the main file to approximately 80 lines while improving readability and testability.

## Current State Analysis

### Strengths
- ✅ Clear section comments delineate major areas
- ✅ Proper error handling with try/catch
- ✅ Startup logger configuration
- ✅ Some extension methods already in use ([`AddServiceDefaults`](../../src/ServiceDefaults/Extensions.cs), [`AddApplicationFeatures`](../../src/Application/ServiceCollectionExtensions.cs), [`AddTenancy`](../../src/Controllers/Tenancy/ServiceCollectionExtensions.cs))

### Areas for Improvement
- ❌ **Identity configuration (14 lines)** - Inline configuration clutters the main flow
- ❌ **CORS configuration (18 lines)** - Business logic mixed with registration
- ❌ **Middleware pipeline** - 17 lines with inline comments explaining order dependencies
- ❌ **Startup logging** - 20 lines of logger factory setup duplicated for startup and application loggers
- ❌ **Conditional logic** - Production vs. development checks scattered throughout
- ❌ **Missing grouping** - Controllers, problem details, and exception handler registered separately without cohesion

## Proposed Architecture

### Service Collection Extensions

Create three new extension method files in `src/BackEnd/Setup/`:

```
src/BackEnd/Setup/
├── SetupApplicationOptions.cs  (✅ Already exists)
├── SetupSwagger.cs             (✅ Already exists)
├── SetupIdentity.cs            (🆕 New)
└── SetupCors.cs                (🆕 New)
```

Create extension methods in Controllers project:

```
src/Controllers/
├── ServiceCollectionExtensions.cs  (🆕 New)
└── ApplicationBuilderExtensions.cs (🆕 New)
```

Move logging extensions:

```
src/BackEnd/Logging/
└── LoggingBuilderExtensions.cs  (✏️ Enhance existing)
```

### Application Builder Extensions

Create one new extension method file in `src/BackEnd/Setup/`:

```
src/BackEnd/Setup/
└── SetupMiddleware.cs          (🆕 New)
```

## Detailed Design

### 1. SetupIdentity.cs

**Purpose:** Consolidate all Identity and NuxtIdentity configuration.

**Location:** `src/BackEnd/Setup/SetupIdentity.cs`

**API:**
```csharp
public static class SetupIdentity
{
    public static IServiceCollection AddIdentityConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure Identity options
        services.AddIdentity<IdentityUser, IdentityRole>(options =>
        {
            options.Password.RequireDigit = false;
            options.Password.RequireLowercase = false;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequireUppercase = false;
            options.Password.RequiredLength = 6;
            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        // Add NuxtIdentity
        services.AddNuxtIdentityWithEntityFramework<IdentityUser, ApplicationDbContext>(configuration);

        return services;
    }
}
```

**Benefits:**
- Encapsulates all Identity-related configuration
- Single responsibility: Identity setup
- Easily testable in isolation
- Can be conditionally registered if needed
- Avoids naming conflict with existing `AddIdentity<TUser, TRole>()` method

### 2. Controllers/ServiceCollectionExtensions.cs

**Purpose:** Register Controllers-specific exception handlers.

**Location:** `src/Controllers/ServiceCollectionExtensions.cs`

**API:**
```csharp
namespace YoFi.V3.Controllers;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddControllerServices(this IServiceCollection services)
    {
        // Register custom exception handler for application-specific exceptions
        services.AddExceptionHandler<CustomExceptionHandler>();

        return services;
    }
}
```

**Benefits:**
- Keeps Controllers project self-contained
- Exception handler registration lives with the exception handler code
- Follows pattern established by [`Controllers/Tenancy/ServiceCollectionExtensions.cs`](../../src/Controllers/Tenancy/ServiceCollectionExtensions.cs)

**Note:** This is separate from Tenancy's exception handler, which is registered in [`AddTenancy()`](../../src/Controllers/Tenancy/ServiceCollectionExtensions.cs).

### 3. Controllers/ApplicationBuilderExtensions.cs

**Purpose:** Register Controllers-specific middleware.

**Location:** `src/Controllers/ApplicationBuilderExtensions.cs`

**API:**
```csharp
namespace YoFi.V3.Controllers;

public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Adds Controllers-specific middleware to the application pipeline.
    /// </summary>
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
```

**Benefits:**
- Middleware registration lives with middleware code
- Self-documenting with clear comments about ordering requirements
- Can be called from SetupMiddleware.cs with proper ordering

### 4. SetupCors.cs

**Purpose:** Extract CORS configuration with its validation logic.

**Location:** `src/BackEnd/Setup/SetupCors.cs`

**API:**
```csharp
public static class SetupCors
{
    public static IServiceCollection AddCorsServices(
        this IServiceCollection services,
        ApplicationOptions applicationOptions,
        ILogger logger)
    {
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                if (applicationOptions.AllowedCorsOrigins.Length == 0)
                {
                    logger.LogError("No allowed CORS origins configured. Please set Application:AllowedCorsOrigins in configuration.");
                }
                else
                {
                    policy.WithOrigins(applicationOptions.AllowedCorsOrigins)
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials();
                    logger.LogInformation(4, "CORS configured with allowed origins: {Origins}",
                        string.Join(", ", applicationOptions.AllowedCorsOrigins));
                }
            });
        });

        return services;
    }
}
```

**Benefits:**
- Isolates CORS policy configuration
- Keeps validation logic with the configuration
- Logger parameter makes diagnostic output explicit
- Easier to unit test CORS policy behavior

### 5. Logging/LoggingBuilderExtensions.cs (Enhanced)

**Purpose:** Extract duplicated logger configuration into reusable methods.

**Location:** `src/BackEnd/Logging/LoggingBuilderExtensions.cs` (enhance existing file)

**API:**
```csharp
public static class LoggingBuilderExtensions
{
    // ... existing AddCustomConsole methods ...

    /// <summary>
    /// Creates a startup logger with custom console configuration.
    /// </summary>
    public static ILogger CreateStartupLogger()
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddCustomConsole(ConfigureConsoleOptions);
            builder.AddEventSourceLogger();
        });

        return loggerFactory.CreateLogger("Startup");
    }

    /// <summary>
    /// Configures application logging with custom console provider.
    /// </summary>
    public static ILoggingBuilder AddApplicationLogging(this ILoggingBuilder logging)
    {
        logging.ClearProviders();
        logging.AddCustomConsole(ConfigureConsoleOptions);
        return logging;
    }

    private static void ConfigureConsoleOptions(CustomConsoleLoggerOptions options)
    {
        options.IncludeScopes = true;
#if DEBUG
        options.TimestampFormat = "MM-dd'T'HH:mm:ss ";
        options.UseUtcTimestamp = false;
#endif
    }
}
```

**Benefits:**
- DRY: Configuration defined once, used in two places
- Consistency: Startup and application loggers use identical settings
- Easier to modify: Change logging config in one place
- Lives with the logging code it configures

### 6. SetupMiddleware.cs

**Purpose:** Organize middleware pipeline with clear ordering and documentation.

**Location:** `src/BackEnd/Setup/SetupMiddleware.cs`

**API:**
```csharp
public static class SetupMiddleware
{
    public static WebApplication ConfigureMiddlewarePipeline(
        this WebApplication app,
        ApplicationOptions applicationOptions,
        ILogger logger)
    {
        // Production-specific middleware
        if (applicationOptions.Environment == EnvironmentType.Production)
        {
            app.UseHsts();
            app.UseHttpsRedirection();
        }

        // Swagger (enabled in all environments during development phase)
        if (ShouldEnableSwagger(applicationOptions))
        {
            logger.LogInformation(8, "Enabling Swagger UI");
            app.UseSwagger();
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

    private static bool ShouldEnableSwagger(ApplicationOptions applicationOptions)
    {
        // During development phase, we keep swagger up even in non-development environments
        return true; // TODO: Revisit when moving to production
    }
}
```

**Benefits:**
- **Self-documenting:** Comments explain ordering requirements
- **Centralized:** All middleware in one place with clear flow
- **Conditional logic encapsulated:** Production checks contained within the method
- **Easier to reason about:** Middleware order is critical; having it in one method makes dependencies clear
- **Testable:** Can create unit tests that verify middleware order

## Refactored Program.cs

After implementing the above extensions, [`Program.cs`](../../src/BackEnd/Program.cs) would look like this:

```csharp
using System.Reflection;
using YoFi.V3.Application;
using YoFi.V3.BackEnd.Logging;
using YoFi.V3.BackEnd.Setup;
using YoFi.V3.Controllers;
using YoFi.V3.Controllers.Tenancy;
using YoFi.V3.Data;
using YoFi.V3.Entities.Options;

ILogger? logger = default;
try
{
    //
    // Set up Startup logger
    //

    logger = LoggingBuilderExtensions.CreateStartupLogger();
    logger.LogInformation("Starting {App} Process ID: {ProcessId}, Thread ID: {ThreadId}",
        Assembly.GetExecutingAssembly().FullName,
        Environment.ProcessId,
        Environment.CurrentManagedThreadId);

    //
    // Set up Web application
    //

    var builder = WebApplication.CreateBuilder(args);

    // Configure logging
    builder.Logging.AddApplicationLogging();

    // Get application options for startup configuration
    ApplicationOptions applicationOptions = new();
    builder.Configuration.Bind(ApplicationOptions.Section, applicationOptions);

    // Add service defaults & Aspire client integrations
    builder.AddServiceDefaults(logger);

    // Add version information to the configuration
    builder.AddApplicationOptions(logger);

    //
    // Add services to the container
    //

    builder.Services.AddControllers();
    builder.Services.AddProblemDetails();
    builder.Services.AddControllerServices();
    builder.Services.AddSwagger();
    builder.Services.AddApplicationFeatures();
    builder.Services.AddDatabase(builder.Configuration);
    builder.Services.AddIdentityConfiguration(builder.Configuration);
    builder.Services.AddTenancy();
    builder.Services.AddCorsServices(applicationOptions, logger);

    //
    // Build and configure the app
    //

    var app = builder.Build();

    // Prepare the database
    app.PrepareDatabaseAsync();

    // Configure the HTTP request pipeline
    app.ConfigureMiddlewarePipeline(applicationOptions, logger);

    logger.LogInformation(10, "OK. Environment: {Environment}", applicationOptions.Environment);
    logger.LogInformation("[DIAG] ==================== APP READY ====================");

    app.Run();

    logger.LogInformation("[DIAG] ==================== APP STOPPED ====================");
}
catch (Exception ex)
{
    if (logger is not null)
    {
        logger?.LogCritical(ex, "Failed to start {App}", Assembly.GetExecutingAssembly().FullName);
    }
    else
    {
        Console.WriteLine("CRITICAL: Failed to start application");
        Console.WriteLine(ex.ToString());
    }
}

// Make Program class accessible to WebApplicationFactory for testing
public partial class Program { }
```

## Line Count Comparison

| Section | Before | After | Reduction |
|---------|--------|-------|-----------|
| Startup logger setup | 20 lines | 7 lines | **-65%** |
| Application logging setup | 9 lines | 1 line | **-89%** |
| Service registration | 48 lines | 8 lines | **-83%** |
| Middleware pipeline | 17 lines | 1 line | **-94%** |
| **Total** | **191 lines** | **~80 lines** | **-58%** |

## Benefits Summary

### Clarity
- **Reduced cognitive load:** Main file focuses on high-level flow
- **Self-documenting:** Extension method names describe their purpose
- **Easier onboarding:** New developers can understand startup sequence quickly

### Maintainability
- **Single Responsibility:** Each extension method has one clear purpose
- **Easier to modify:** Change Identity config without touching CORS or middleware
- **Reduced merge conflicts:** Changes isolated to specific files

### Testability
- **Unit testable:** Extension methods can be tested independently
- **Integration testable:** Can verify service registrations resolve correctly
- **Middleware order testable:** Can verify middleware pipeline configuration

### Consistency
- **Pattern established:** Follows existing patterns in the codebase ([`ServiceCollectionExtensions`](../../src/Application/ServiceCollectionExtensions.cs), [`Extensions`](../../src/ServiceDefaults/Extensions.cs))
- **Discoverable:** All setup code in `Setup/` directory
- **Conventions:** Clear naming (`AddXxxServices`, `ConfigureXxx`)

## Implementation Checklist

- [ ] Enhance [`Logging/LoggingBuilderExtensions.cs`](../../src/BackEnd/Logging/LoggingBuilderExtensions.cs) with `CreateStartupLogger()` and `AddApplicationLogging()`
- [ ] Create [`Controllers/ServiceCollectionExtensions.cs`](../../src/Controllers/ServiceCollectionExtensions.cs)
- [ ] Create [`Controllers/ApplicationBuilderExtensions.cs`](../../src/Controllers/ApplicationBuilderExtensions.cs)
- [ ] Create [`SetupIdentity.cs`](../../src/BackEnd/Setup/SetupIdentity.cs)
- [ ] Create [`SetupCors.cs`](../../src/BackEnd/Setup/SetupCors.cs)
- [ ] Create [`SetupMiddleware.cs`](../../src/BackEnd/Setup/SetupMiddleware.cs)
- [ ] Move `TenancyExceptionHandler` registration to [`Controllers/Tenancy/ServiceCollectionExtensions.cs`](../../src/Controllers/Tenancy/ServiceCollectionExtensions.cs)
- [ ] Refactor [`Program.cs`](../../src/BackEnd/Program.cs) to use new extensions
- [ ] Update [`src/BackEnd/README.md`](../../src/BackEnd/README.md) to document new structure
- [ ] Run tests to verify no behavioral changes

## Answers to Design Questions

1. **Naming Convention:** Use `AddIdentityConfiguration` to avoid conflict with existing `AddIdentity<TUser, TRole>()` method
2. **TenancyExceptionHandler:** Move registration to [`Controllers/Tenancy/ServiceCollectionExtensions.cs`](../../src/Controllers/Tenancy/ServiceCollectionExtensions.cs) since it's a tenancy concern
3. **Logging Extension Location:** Enhance existing [`Logging/LoggingBuilderExtensions.cs`](../../src/BackEnd/Logging/LoggingBuilderExtensions.cs) to keep extensions with the code they support
4. **Test Coverage:** Skip unit tests for extension methods - they are typically not unit tested in most projects, and integration tests will verify correct behavior

## Alternative Approaches Considered

### Option A: Minimal Refactoring
Extract only the largest inline blocks (Identity, CORS) but leave middleware pipeline inline.

**Pros:** Less invasive, smaller change
**Cons:** Still leaves significant inline configuration, doesn't fully address the problem

### Option B: Single Configuration File
Create one large `SetupServices.cs` with multiple methods.

**Pros:** All configuration in one file
**Cons:** Large file defeats the purpose, harder to navigate, violates single responsibility

### Option C: Proposed Approach (Multiple Focused Extensions)
Create multiple small, focused extension files in `Setup/` directory.

**Pros:** Best separation of concerns, most maintainable, follows existing patterns
**Cons:** More files to create (but that's the point!)

## Recommendation

**Proceed with Option C** - the proposed approach of creating multiple focused extension methods. This provides the best balance of clarity, maintainability, and consistency with existing codebase patterns.

The refactoring can be done incrementally:
1. Start with [`SetupIdentity.cs`](../../src/BackEnd/Setup/SetupIdentity.cs) (most isolated)
2. Add [`SetupWebApi.cs`](../../src/BackEnd/Setup/SetupWebApi.cs) and [`SetupCors.cs`](../../src/BackEnd/Setup/SetupCors.cs)
3. Finish with [`SetupMiddleware.cs`](../../src/BackEnd/Setup/SetupMiddleware.cs) (most complex)
4. Extract logging configuration last

Each step can be tested independently before moving to the next.
