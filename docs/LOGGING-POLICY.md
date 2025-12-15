# Logging Policy

**Status:** Active
**Last Updated:** 2024-12-15
**Applies To:** All YoFi.V3 application code

## Overview

This document defines the logging standards and practices for the YoFi.V3 application. The policy balances operational visibility, debugging capability, performance, and security requirements.

## Core Principles

1. **API Layer Logging** - Log at the Controllers (API boundary) only. Application Features and Data Repositories remain logging-free to maintain Clean Architecture principles.
2. **Structured Logging** - Use LoggerMessage source generation with CallerMemberName for consistent, high-performance logging.
3. **Privacy First** - Never log sensitive user data (emails, transaction details, tokens).
4. **Trace-Driven Debugging** - Surface TraceIds in error responses to enable log correlation without logging PII.
5. **Signal Over Noise** - Minimize log volume in production; use Debug level judiciously in development.

## Log Levels by Environment

### Development
- **Default Level:** `Debug` (shows "Starting" and "OK" messages)
- **Framework Logs:** `Warning` (minimal noise from ASP.NET Core, EF Core)
- **EF Core SQL:** `Warning` (enable `Information` only when debugging database issues)
- **Viewing:** Aspire Dashboard (Traces or Structured Logs with full scope)

### Container/CI
- **Default Level:** `Debug` (full visibility for CI/CD debugging)
- **Framework Logs:** `Warning`
- **Storage:** Docker logs, attached to CI/CD pipeline artifacts (subject to Azure Pipeline retention policies)
- **Viewing:** Docker console logs (message template content + limited scope: TraceId, SpanId, TestName)

### Production
- **Default Level:** `Information` (operational visibility without Debug noise)
- **Framework Logs:** `Warning`
- **Storage:** Application Insights (30-day retention)
- **Viewing:** Application Insights structured logs and traces (full scope including UserId, TenantId)

### Configuration Example

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "System": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Warning"
    }
  }
}
```

## Sensitive Data Rules

### ✅ CAN Log
- **User GUIDs** - Non-PII identifiers for correlation
- **Tenant GUIDs** - Non-PII identifiers for correlation
- **TraceIds/SpanIds** - For distributed tracing correlation
- **Test correlation data** - For functional test debugging

### ⚠️ CONDITIONAL Logging
- **Refresh Tokens** - NEVER in production. In development/container environments, log first 8 characters only (e.g., `"abc12345..."`)

### ❌ NEVER Log
- **Email addresses** - Use User GUID instead; users can provide email when reporting issues
- **Passwords** - Obvious security violation
- **JWT tokens** - Security credentials
- **Tenant names** - May contain business-sensitive information
- **Transaction amounts** - Financial data
- **Payee names** - Personal financial information
- **API keys/secrets** - Security credentials

### Rationale: Email Address Policy

**Why not log emails:**
- YoFi is a personal finance application (sensitive category)
- TraceIds are surfaced on every error response for log correlation
- Users can provide their User GUID or TraceId when reporting issues
- Eliminates risk of email exposure in log aggregation systems

## Structured Logging Context

### Required in All Logs
- **TraceId** - Automatically included via OpenTelemetry Activity
- **SpanId** - Automatically included via OpenTelemetry Activity
- **Location** - Method name via `[CallerMemberName]`

### Automatic Scope Injection
When available, the following should be pushed to logging scope:
- **UserId** - User GUID from authenticated claims
- **TenantId** - Active tenant GUID from tenant context
- **Test Correlation Data** - TestRunId, TestName (functional tests only)

**Implementation Note:** Logging scope should be set via middleware/filters. This makes UserId and TenantId available in structured logging (Application Insights, Aspire Dashboard) automatically. However, if you want UserId or TenantId to appear in the console/container log MESSAGE itself (not just as structured properties), you must still include them as parameters in the log method and message template (e.g., `"{Location}: OK {TenantId}"`).

### Log Viewing by Environment

**Local Development (Aspire Dashboard):**
- View logs in Aspire Dashboard: Traces or Structured Logs
- **Full scope available** - UserId, TenantId, TraceId, SpanId, TestName all captured
- Real-time log streaming
- OpenTelemetry distributed tracing

**Container-Based Testing (Docker):**
- View logs via `docker logs <container-name>` or in CI/CD pipeline output
- **Limited scope** - Message template content + selected scope properties (TraceId, SpanId, TestName)
- UserId and TenantId are NOT visible unless explicitly included as message parameters
- This is why including TenantId/UserId as parameters is critical for container debugging
- Logs attached to CI/CD artifacts for post-run analysis

**Production (Application Insights):**
- View logs via Azure Portal or Application Insights queries
- **Full scope available** - All structured properties from logging scope
- 30-day retention
- Advanced querying capabilities (KQL)
- Correlation with distributed traces and dependencies

## Exception Logging

### All Environments
- **Full stack traces** - Always log complete stack traces (including inner exceptions)
- **Exception data dictionary** - Include all exception data
- **Request context** - Include request path, HTTP method
- **User context** - Include UserId if authenticated
- **Tenant context** - Include TenantId if in tenant scope

**Rationale:** Complete exception information is critical for troubleshooting. Stack traces in production help diagnose issues quickly. If stack traces contain sensitive data, that's a code smell indicating the exception message itself is problematic.

### CustomExceptionHandler Responsibility
The [`CustomExceptionHandler`](../src/Controllers/Middleware/CustomExceptionHandler.cs) middleware is responsible for logging all unhandled exceptions with full context.

## Logging Pattern (From .roorules)

### LoggerMessage Source Generation
All logging must use the `[LoggerMessage]` attribute with partial methods. See [.roorules Logging Pattern](../.roorules#logging-pattern) for complete implementation details.

**Key Requirements:**
- Explicit event IDs (1-9999, unique per class)
- CallerMemberName as last parameter
- Location in message template: `"{Location}: Message"`
- Debug level for "Starting" messages
- Information level for "OK" messages

### Example
```csharp
public partial class TransactionsController(
    TransactionsFeature transactionsFeature,
    ILogger<TransactionsController> logger) : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        LogStartingKey(id);
        var result = await transactionsFeature.GetByIdAsync(id);
        LogOkKey(id);
        return Ok(result);
    }

    [LoggerMessage(1, LogLevel.Debug, "{Location}: Starting {Key}")]
    private partial void LogStartingKey(Guid key, [CallerMemberName] string? location = null);

    [LoggerMessage(2, LogLevel.Information, "{Location}: OK {Key}")]
    private partial void LogOkKey(Guid key, [CallerMemberName] string? location = null);
}
```

## Performance Logging

**Status:** Future consideration

Performance metrics (request duration, query execution time) are **not currently logged**. Use OpenTelemetry metrics and Application Insights for performance monitoring.

**If performance logging is needed:**
- Add duration to "OK" messages only when troubleshooting specific issues
- Consider using metrics instead of logs for continuous performance monitoring
- Log slow operations (>1000ms) as Warning messages

## Health Checks

- **Level:** Debug only (filtered out in production Information level)
- **Failures:** Health check failures that generate Warning/Error logs will still be logged
- **Rationale:** Health checks run frequently (10-30 seconds) and create noise; exceptions from health checks provide sufficient operational visibility

## Startup and Configuration Logging

**Policy:** Log detailed configuration at Information level (except secrets)

**Rationale:** Configuration issues are the most common cause of production failures. Comprehensive startup logging provides critical troubleshooting information.

**Current Implementation:** [`StartupLogging`](../src/BackEnd/Setup/StartupLogging.cs) logs application configuration on startup.

## Architectural Boundaries

### API Layer (Controllers)
✅ **Log here** - All controller methods should log:
- Debug: "Starting" with key parameters
- Information: "OK" with result counts/keys
- Exceptions: Handled via CustomExceptionHandler

### Application Layer (Features)
❌ **No logging** - Keep Application Features infrastructure-agnostic:
- Maintains Clean Architecture principles
- Easier unit testing (no logger mocks)
- Portable to other hosting models (CLI, desktop apps)
- Controller logs provide sufficient context

**Exception:** Add Application layer logging only for:
- Long-running operations (>5 seconds) requiring progress tracking
- External service calls (APIs, message queues)
- Complex business workflows with multiple decision points

### Data Layer (Repositories)
❌ **No logging** - Use EF Core built-in logging if needed:
- Enable `Microsoft.EntityFrameworkCore.Database.Command` at Information level in development
- Use database profiling tools for performance analysis
- OpenTelemetry automatically traces database operations

See [`docs/wip/LOGGING-ARCHITECTURE-ANALYSIS.md`](wip/LOGGING-ARCHITECTURE-ANALYSIS.md) for detailed architectural rationale.

## Test Control Endpoints

[`TestControlController`](../src/Controllers/TestControlController.cs) follows the same logging rules as production endpoints.

**No special handling** - Test endpoints log identically to production endpoints for consistency.

## Log Message Format

**Current Standard:** `"{Location}: Brief description {Parameter}"`

**Keep simple and consistent:**
- Location automatically captured via CallerMemberName
- Brief description describes the outcome or action
- Structured parameters for log filtering

**Do NOT:**
- Add operation codes or special prefixes
- Create elaborate structured formats
- Include redundant information (Location already provides context)

**Structured data** (Action, Resource, Status) should be added to logging **scope**, not individual message templates. This keeps messages concise while providing rich context in production log aggregation.

## Third-Party Library Logging

### Framework Default Levels (All Environments)
```json
{
  "Microsoft": "Warning",
  "System": "Warning",
  "Microsoft.AspNetCore.Hosting": "Warning",
  "Microsoft.AspNetCore.Routing": "Warning",
  "Microsoft.EntityFrameworkCore.Database.Command": "Warning",
  "Microsoft.AspNetCore.Authentication": "Warning"
}
```

**Rationale:** Framework logs are extremely noisy. Set to Warning by default in all environments. Lower to Information/Debug temporarily when troubleshooting specific framework issues.

## Future Considerations

### File Import/Export Operations
**Status:** Planned feature, logging requirements TBD

When implementing file operations:
- Consider progress logging for large files
- Log file metadata (size, format) but not contents
- Log validation failures with row/line numbers
- Evaluate if this justifies Application layer logging

### Other Future Features
As the application evolves, reassess this policy for:
- Background jobs/scheduled tasks
- Real-time features (SignalR, WebSockets)
- Integration with external APIs
- Multi-tenant data migration operations

## Implementation Checklist

When adding logging to a new controller:

- [ ] Use LoggerMessage source generation (no direct `_logger.Log*()` calls)
- [ ] Include explicit event IDs (unique within the class, 1-9999)
- [ ] Add CallerMemberName as last parameter: `[CallerMemberName] string? location = null`
- [ ] Start message template with `"{Location}: "`
- [ ] Use Debug level for "Starting" messages
- [ ] Use Information level for "OK" outcome messages
- [ ] Verify no sensitive data (emails, tokens, amounts) in log messages
- [ ] Add XML documentation comments to the controller class and methods
- [ ] Run tests to verify logging behavior

## Related Documentation

- [`.roorules` - Logging Pattern](../.roorules#logging-pattern) - Complete technical implementation details
- [`docs/wip/LOGGING-ARCHITECTURE-ANALYSIS.md`](wip/LOGGING-ARCHITECTURE-ANALYSIS.md) - Architectural decision rationale
- [`src/BackEnd/Logging/README.md`](../src/BackEnd/Logging/README.md) - Custom console logger implementation
- [`src/Controllers/Middleware/CustomExceptionHandler.cs`](../src/Controllers/Middleware/CustomExceptionHandler.cs) - Exception logging implementation

## Policy Compliance

This policy is enforced through:
1. **Code review** - All PRs reviewed for logging compliance
2. **Roo rules** - AI assistant automatically applies logging patterns from `.roorules`
3. **Testing** - Integration tests verify logging behavior
4. **Documentation** - This policy document serves as the source of truth

## Questions or Exceptions

If you need to deviate from this policy or have questions:
1. Document the rationale in code comments
2. Discuss in PR review
3. Consider if the policy needs updating
4. Update this document if a new pattern emerges

---

**Document History:**
- 2024-12-15: Initial policy created based on established practices and questionnaire
