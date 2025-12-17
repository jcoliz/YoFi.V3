# Logging Policy

**Status:** Active
**Last Updated:** 2024-12-17
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

### ✅ ALWAYS Safe to Log
- **User GUIDs** - Non-PII identifiers for correlation (all environments)
- **Tenant GUIDs** - Non-PII identifiers for correlation (all environments)
- **TraceIds/SpanIds** - For distributed tracing correlation (all environments)
- **Test correlation data** - For functional test debugging (all environments)

### ⚠️ CONDITIONAL Logging (Environment-Dependent)

**Email Addresses and Usernames:**
- **Local Development** - CAN log at Debug level
  - Test/seed data only, not real users
  - Significantly improves debugging of authentication and user-related issues
  - Makes it easy to correlate logs with test scenarios
- **Container/CI** - CAN log at Debug level
  - Test users only, no real PII
  - Essential for debugging functional test failures
  - Helps correlate test scenarios with log output
- **Production** - NEVER log
  - Real user PII that must be protected
  - Use User GUID instead; users can provide email when reporting issues
  - TraceIds in error responses enable log correlation without PII

**Refresh Tokens:**
- **Local Development** - CAN log full tokens at Debug level
  - No real resources at risk in local development
  - Anyone with log access already has database access, browser DevTools, and full system access
  - Full tokens significantly improve debugging of authentication issues
  - Development tokens have no real-world value
- **Container/CI** - Log first 8-12 characters only (e.g., `"abc12345..."`)
  - Shared environment with longer log retention
  - CI/CD logs may be accessed by more people and stored longer
  - Partial logging balances debugging needs with reasonable caution
- **Production** - NEVER log (not even partial)
  - Real user sessions with real security implications
  - Token theft could enable actual user impersonation
  - Use TraceId correlation for debugging instead

### ❌ NEVER Log (Any Environment)
- **Passwords** - Security credentials (obvious security violation)
- **JWT access tokens** - Security credentials (even in development, use refresh tokens for debugging instead)
- **API keys/secrets** - Configuration secrets (even test keys should be in secure configuration)

### 🔒 Production-Only Restrictions
These can be logged in development/container but NEVER in production:
- **Tenant names** - May contain business-sensitive information in production
- **Transaction amounts** - Real financial data in production
- **Payee names** - Real personal financial information in production

### Rationale: Environment-Based Sensitivity

**The Core Principle:** "Don't log data that could enable real harm or expose real users."

**Development/Container Environments:**
- **Test data only** - No real users, no real financial information
- **Debugging is paramount** - Development friction directly impacts productivity
- **Access control exists** - Anyone with log access already has full system access
- **Benefit outweighs risk** - Being able to see "test@example.com failed login" is invaluable for debugging

**Production Environment:**
- **Real user PII** - Actual email addresses of real people
- **Real financial data** - Actual transaction amounts and payee names
- **Compliance requirements** - May be subject to GDPR, CCPA, financial regulations
- **Alternative exists** - TraceIds in error responses enable correlation without PII

**Why This Matters:**
- Development teams waste hours debugging "user X can't login" when they can't see that it's actually "test-user-with-typo@example.com"
- Functional test debugging requires correlating test scenarios with log output
- In production, we have TraceIds and User GUIDs which are sufficient
- Security policies should remove real risks, not create unnecessary friction

**Example Scenarios:**

*Development/Container:*
```
DEBUG: Login: Starting Email=test@example.com
ERROR: Login: Failed Email=test@example.com Reason=InvalidPassword
```
✅ Acceptable - test data, significant debugging value

*Production:*
```
DEBUG: Login: Starting UserId=550e8400-e29b-41d4-a716-446655440000
ERROR: Login: Failed UserId=550e8400-e29b-41d4-a716-446655440000 Reason=InvalidPassword TraceId=abc123
```
✅ Acceptable - user can report "I got error with TraceId abc123" and you can correlate without knowing their email

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
- [ ] Verify no production-sensitive data (real emails, real tokens, real amounts) in log messages
- [ ] Verify development logs include helpful debug information (test emails, test usernames)
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
- 2024-12-17: Updated sensitive data policy to allow test data (emails, usernames, tokens) in development/container environments while maintaining production restrictions
- 2024-12-15: Initial policy created based on established practices and questionnaire
