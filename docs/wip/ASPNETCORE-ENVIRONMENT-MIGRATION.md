# Migration Analysis: Replace Custom ApplicationOptions.Environment with ASPNETCORE_ENVIRONMENT

## Summary

**Recommendation: ✅ YES - Migrate to ASPNETCORE_ENVIRONMENT**

**Environment Mapping:**
- Local → `ASPNETCORE_ENVIRONMENT=Development` (standard)
- Container → `ASPNETCORE_ENVIRONMENT=Container` (custom, explicitly supported)
- Production → `ASPNETCORE_ENVIRONMENT=Production` (standard)

**Version String Display:** Use actual environment names without aliases
- Development → `"1.2.3 (Development)"`
- Container → `"1.2.3 (Container)"`
- Production → `"1.2.3"` (no suffix)

## Current State Analysis

### Complete Usage Inventory

**Custom Implementation**:
- **Location**: [`src/Entities/Options/ApplicationOptions.cs`](../src/Entities/Options/ApplicationOptions.cs)
- **Enum Values**: `Production`, `Container`, `Local`
- **Configuration**: Set via `Application:Environment` in appsettings or environment variables

**All Usages Found** (4 production locations + tests):

1. **[`src/Controllers/VersionController.cs`](../src/Controllers/VersionController.cs)** - Line 32-36
   - Appends environment suffix to version string: `$"{version} (Local)"` or `$"{version} (Container)"`
   - Production returns version without suffix

2. **[`src/BackEnd/Setup/SetupMiddleware.cs`](../src/BackEnd/Setup/SetupMiddleware.cs)** - Line 40
   - Checks `if (applicationOptions.Environment == EnvironmentType.Production)`
   - Enables HSTS and HTTPS redirection for Production only
   - Container and Local skip these middleware components

3. **[`src/BackEnd/Program.cs`](../src/BackEnd/Program.cs)** - Line 69
   - Logs the environment at startup: `logger.LogOkEnvironment(applicationOptions.Environment)`
   - Information-level log message

4. **[`src/BackEnd/Setup/StartupLogging.cs`](../src/BackEnd/Setup/StartupLogging.cs)** - Line 37-41
   - LoggerMessage method definition for logging environment
   - Takes `EnvironmentType` parameter

**Test Usage**:
- **[`tests/Integration.Controller/VersionControllerTests.cs`](../tests/Integration.Controller/VersionControllerTests.cs)** - Parameterized tests for all three environment types

### Current Configuration Points
- **Local**: `appsettings.Development.json` → `"Application": { "Environment": "Local" }`
- **Container**: `docker-compose-ci.yml` → `APPLICATION__ENVIRONMENT=Container`
- **Production**: Azure Bicep already sets → `ASPNETCORE_ENVIRONMENT=Production` (line 70-71 in [`webapp.bicep`](../infra/AzDeploy.Bicep/Web/webapp.bicep))

## Is Adding "Container" to ASPNETCORE_ENVIRONMENT OK?

### ✅ YES - It's Explicitly Supported

Microsoft documentation states:

> "ASP.NET Core reads the environment variable `ASPNETCORE_ENVIRONMENT` at app startup and stores the value in `IWebHostEnvironment.EnvironmentName`. `ASPNETCORE_ENVIRONMENT` can be set to any value, but three values are provided by the framework: `Development`, `Staging`, and `Production`."

**Source**: [Microsoft Docs - Use multiple environments in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/environments)

### Benefits of Standard Values vs Custom Values

**Framework-Provided Values** (`Development`, `Staging`, `Production`):
- Have special behavior (e.g., `IsDevelopment()` helper methods)
- Recognized by hosting providers
- Standard across .NET ecosystem
- Enable Developer Exception Page automatically in Development
- Convention over configuration

**Custom Values** (like `Container`):
- Require explicit string comparisons: `env.EnvironmentName == "Container"`
- No framework-provided helper methods
- Must document custom values for team
- Full control over behavior

### Precedent in the Ecosystem

Many projects use custom environment values:
- **Testing environments**: "Test", "IntegrationTest", "E2E"
- **CI/CD environments**: "CI", "Pipeline", "Build"
- **Docker environments**: "Docker", "Container", "Compose"
- **Preview environments**: "Preview", "Staging2", "UAT"

## Analysis of "Container" Environment

### What Makes Container Special?

From [`docs/ENVIRONMENTS.md`](../docs/ENVIRONMENTS.md):

| Aspect | Local (Development) | Container | Production |
|--------|-------------------|-----------|------------|
| Purpose | Development | Functional tests / Evaluation | Live system |
| Frontend | Node dev server | nginx (static) | Azure Static Web Apps |
| Backend | .NET process | Docker container | Azure App Service |
| Security | Relaxed (dev keys) | Test keys | Production secrets |
| HTTPS | No HTTPS redirection | No HTTPS redirection | HTTPS enforced |

### Key Insight: Container Is Production-Like Architecture

**Your observation is correct** - Container environment is **closer to Production than Development**:

✅ **Production-like characteristics**:
- Static frontend build (like Production)
- Containerized deployment (production-ready)
- Direct API calls from browser (like Production)
- More realistic network topology
- Tests production deployment artifacts

❌ **NOT like Local Development**:
- No hot module replacement
- No Aspire orchestration
- No proxied API calls
- Requires full build cycle

**Purpose**: Container is a **production-like test environment** for validating deployment artifacts before they reach actual production.

## Recommendation: Use Custom "Container" Value

### Chosen Approach: ASPNETCORE_ENVIRONMENT=Container (RECOMMENDED ✅)

**Change**: Set `ASPNETCORE_ENVIRONMENT=Container` for Container environment

**Rationale**:
1. **Semantic accuracy** - Container is production-like, not development-like
2. **Clear differentiation** - Three distinct environments with clear purposes
3. **Middleware alignment** - Container skips HTTPS redirection (like Local), unlike Production
4. **Flexibility** - Can add Container-specific behavior without conflating with Development or Production
5. **Version string clarity** - `"1.2.3 (Container)"` clearly indicates the deployment context

**Trade-offs**:
- Requires string comparison: `env.EnvironmentName == "Container"` (vs helper methods)
- Non-standard value requires minimal team documentation
- Must explicitly handle Container in switch expressions (no default fallback)

**Implementation**:
```yaml
# docker/docker-compose-ci.yml
environment:
  - ASPNETCORE_ENVIRONMENT=Container
```

```csharp
// SetupMiddleware.cs - No changes needed to logic
if (env.IsProduction())  // Only Production gets HTTPS middleware
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

// VersionController.cs - Use actual environment names (no aliasing)
var versionWithEnv = env.IsProduction()
    ? version
    : $"{version} ({env.EnvironmentName})";
```

**Simplification**: Don't alias "Development" to "Local". Just display the actual environment name in the version string. This is clearer and requires less mapping logic.

## Migration Path

### Phase 1: Configuration Changes
1. ✅ **Production** - Already uses `ASPNETCORE_ENVIRONMENT=Production` (no change needed)
2. **Container** - Change `docker-compose-ci.yml`: `ASPNETCORE_ENVIRONMENT=Container`
3. **Local** - Remove `Application:Environment` from `appsettings.Development.json`
   - ASP.NET Core defaults to `Development` when running via `dotnet run` or IDE

### Phase 2: Code Changes (4 files to modify)

#### 1. [`src/Controllers/VersionController.cs`](../src/Controllers/VersionController.cs)

```csharp
// Before: Inject IOptions<ApplicationOptions>
public partial class VersionController(
    IOptions<ApplicationOptions> options,
    ILogger<VersionController> logger) : ControllerBase

// After: Inject IWebHostEnvironment + keep IOptions for Version
public partial class VersionController(
    IWebHostEnvironment env,
    IOptions<ApplicationOptions> options,
    ILogger<VersionController> logger) : ControllerBase
```

```csharp
// Before: Switch on EnvironmentType enum
var versionWithEnv = options.Value.Environment switch
{
    EnvironmentType.Local => $"{version} (Local)",
    EnvironmentType.Container => $"{version} (Container)",
    _ => version
};

// After: Simple check - append environment name if not Production
var versionWithEnv = env.IsProduction()
    ? version
    : $"{version} ({env.EnvironmentName})";
```

**Benefits of this approach:**
- ✅ Simpler logic (no switch expression needed)
- ✅ Automatically works for any future non-production environments
- ✅ Clear and honest (displays actual environment name)
- ✅ Less code to maintain

#### 2. [`src/BackEnd/Setup/SetupMiddleware.cs`](../src/BackEnd/Setup/SetupMiddleware.cs)

```csharp
// Before: Method signature with ApplicationOptions
public static WebApplication ConfigureMiddlewarePipeline(
    this WebApplication app,
    ApplicationOptions applicationOptions,
    ILogger logger)

// After: Method signature with IWebHostEnvironment
public static WebApplication ConfigureMiddlewarePipeline(
    this WebApplication app,
    IWebHostEnvironment env,
    ILogger logger)
```

```csharp
// Before: Check enum value
if (applicationOptions.Environment == EnvironmentType.Production)

// After: Use helper method
if (env.IsProduction())
```

```csharp
// Before: Pass ApplicationOptions
private static bool ShouldEnableSwagger(ApplicationOptions applicationOptions)

// After: Pass IWebHostEnvironment
private static bool ShouldEnableSwagger(IWebHostEnvironment env)
```

#### 3. [`src/BackEnd/Program.cs`](../src/BackEnd/Program.cs)

```csharp
// Before: Bind ApplicationOptions early, pass to ConfigureMiddlewarePipeline
ApplicationOptions applicationOptions = new();
builder.Configuration.Bind(ApplicationOptions.Section, applicationOptions);
// ... later ...
app.ConfigureMiddlewarePipeline(applicationOptions, logger);
logger.LogOkEnvironment(applicationOptions.Environment);

// After: Use app.Environment directly
app.ConfigureMiddlewarePipeline(app.Environment, logger);
logger.LogOkEnvironment(app.Environment.EnvironmentName);
```

#### 4. [`src/BackEnd/Setup/StartupLogging.cs`](../src/BackEnd/Setup/StartupLogging.cs)

```csharp
// Before: Parameter type EnvironmentType
[LoggerMessage(10, LogLevel.Information, "{Location}: OK. Environment: {Environment}")]
public static partial void LogOkEnvironment(
    this ILogger logger,
    EnvironmentType environment,
    [CallerMemberName] string? location = null);

// After: Parameter type string
[LoggerMessage(10, LogLevel.Information, "{Location}: OK. Environment: {Environment}")]
public static partial void LogOkEnvironment(
    this ILogger logger,
    string environment,
    [CallerMemberName] string? location = null);
```

### Phase 3: Cleanup
1. Remove `Environment` property from [`ApplicationOptions.cs`](../src/Entities/Options/ApplicationOptions.cs)
2. Remove `EnvironmentType` enum from [`ApplicationOptions.cs`](../src/Entities/Options/ApplicationOptions.cs)
3. Update test files in [`tests/Integration.Controller/`](../tests/Integration.Controller/)
   - Update `VersionControllerTests.cs` to use string environment names
   - Update `CustomVersionWebApplicationFactory.cs` constructor signature

### Phase 4: Update Documentation

Multiple documentation files reference the custom `Application:Environment` configuration and the "Local" environment name. These need to be updated to reflect the new `ASPNETCORE_ENVIRONMENT` approach:

#### 1. [`docs/ENVIRONMENTS.md`](../docs/ENVIRONMENTS.md)
**Updates needed:**
- Remove references to custom `Application:Environment` configuration
- Update Local environment section to clarify it uses `ASPNETCORE_ENVIRONMENT=Development`
- Update examples showing environment configuration
- Update summary table to show `Development` instead of `Local`

#### 2. [`docs/CONTAINER-ENVIRONMENT.md`](../docs/CONTAINER-ENVIRONMENT.md)
**Updates needed:**
- Update references from `APPLICATION__ENVIRONMENT=Container` to `ASPNETCORE_ENVIRONMENT=Container`
- Update any examples showing environment variable configuration

#### 3. [`docker/README.md`](../docker/README.md)
**Updates needed:**
- Update environment variable documentation
- Change from `APPLICATION__ENVIRONMENT` to `ASPNETCORE_ENVIRONMENT`

#### 4. [`src/BackEnd/README.md`](../src/BackEnd/README.md)
**Updates needed:**
- Update configuration documentation if it references `Application:Environment`

#### 5. [`tests/Integration.Controller/README.md`](../tests/Integration.Controller/README.md) or [`TESTING-GUIDE.md`](../tests/Integration.Controller/TESTING-GUIDE.md)
**Updates needed:**
- Update any references to environment configuration in test setup
- Update examples showing how to set environment for tests

#### 6. Project root [`README.md`](../README.md)
**Check and update if needed:**
- Any quick-start instructions mentioning environment configuration
- Environment variables documentation

#### Documentation Search Needed
Search all documentation files for:
- `Application:Environment`
- `APPLICATION__ENVIRONMENT`
- `EnvironmentType`
- `"Local"` (in context of environment names)

These will need to be reviewed and updated to reflect:
- Use of `ASPNETCORE_ENVIRONMENT` instead of custom option
- "Development" instead of "Local" for local environment
- Clarification that "Container" is a custom (but supported) environment value

### Phase 5: Update Configuration Files

#### [`docker/docker-compose-ci.yml`](../docker/docker-compose-ci.yml) (line 29)
```yaml
# Before
environment:
  - APPLICATION__ENVIRONMENT=Container

# After
environment:
  - ASPNETCORE_ENVIRONMENT=Container
```

#### [`src/BackEnd/appsettings.Development.json`](../src/BackEnd/appsettings.Development.json) (lines 19-22)
```json
// Before - REMOVE Environment PROPERTY
"Application": {
  "Environment": "Local",
  "AllowedCorsOrigins": [ "http://localhost:5173" ]
}

// After
"Application": {
  "AllowedCorsOrigins": [ "http://localhost:5173" ]
}
```

**Note:** ASP.NET Core automatically sets `ASPNETCORE_ENVIRONMENT=Development` when running via `dotnet run` or Visual Studio, so no explicit configuration needed for local development.

## Impact Assessment

### ✅ Low Risk - Minimal Surface Area
- **4 production code files** to modify (VersionController, SetupMiddleware, Program, StartupLogging)
- **2 test files** to update (VersionControllerTests, CustomVersionWebApplicationFactory)
- **2 configuration files** to change (docker-compose-ci.yml, appsettings.Development.json)
- **No database changes** required
- **No API contract changes** - Version endpoint returns same format

### Benefits
- **Standards compliance** - Uses standard ASP.NET Core environment variable
- **Reduced complexity** - One less custom configuration option to maintain
- **Better tooling support** - IDEs and hosting providers recognize `ASPNETCORE_ENVIRONMENT`
- **Clearer semantics** - Aligns with .NET hosting conventions
- **Simplified configuration** - Single source of truth for environment

### Trade-offs
- **Custom value "Container"** - Requires string comparison instead of enum
- **Less compile-time safety** - String matching instead of enum (but still type-safe via `IWebHostEnvironment`)
- **Documentation needed** - Team must know about custom "Container" value

## Summary Table: Environment Variable Mapping

| Environment | Old Config | New Config | Display String |
|-------------|-----------|------------|----------------|
| **Local** | `Application:Environment=Local` | `ASPNETCORE_ENVIRONMENT=Development` (auto-set by framework) | `{version} (Development)` |
| **Container** | `APPLICATION__ENVIRONMENT=Container` | `ASPNETCORE_ENVIRONMENT=Container` | `{version} (Container)` |
| **Production** | ✅ Already: `ASPNETCORE_ENVIRONMENT=Production` | No change | `{version}` (no suffix) |

**Key Change:** Display actual environment name "Development" instead of aliasing to "Local". This is clearer and more honest about the runtime environment.

## Final Recommendation

**✅ Proceed with migration to ASPNETCORE_ENVIRONMENT**

**Environment Strategy:**
- Use standard `Development` for local (not aliased as "Local")
- Use custom `Container` for container environment (explicitly supported by Microsoft)
- Use standard `Production` for Azure production

**Benefits:**
- ✅ Aligns with .NET standards
- ✅ Custom "Container" value is explicitly supported
- ✅ Simpler version string logic (no aliasing needed)
- ✅ Clear and honest environment naming
- ✅ Minimal code changes (4 files + tests)
- ✅ Low risk migration path

**Version String Changes:**
- Before: `"1.2.3 (Local)"` → After: `"1.2.3 (Development)"`
- Before: `"1.2.3 (Container)"` → After: `"1.2.3 (Container)"` (no change)
- Before: `"1.2.3"` → After: `"1.2.3"` (no change)
