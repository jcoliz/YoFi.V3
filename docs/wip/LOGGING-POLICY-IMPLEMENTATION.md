# Logging Policy Implementation Guide

**Status:** Recommendations
**Date:** 2024-12-15
**Related:** [`docs/LOGGING-POLICY.md`](../LOGGING-POLICY.md)

## Overview

This document provides actionable recommendations for implementing the logging policy across the YoFi.V3 application.

## 1. Update .roorules

**Action Required:** Add new logging rules to `.roorules` (requires code mode to edit)

**Additions to Logging Pattern section (after rule #11):**

```markdown
12. **API-layer logging only** - Log at Controllers (API boundary) only. Application Features and Data Repositories should remain logging-free to maintain Clean Architecture principles. Add logging to lower layers only for specific scenarios (long-running operations, external service calls, complex workflows).

13. **Sensitive data rules** - NEVER log: email addresses, passwords, JWT tokens, refresh tokens (except first 8 chars in dev/container), tenant names, transaction amounts, payee names. CAN log: User GUIDs, Tenant GUIDs, TraceIds/SpanIds. Users provide TraceId or GUID when reporting issues.

14. **Logging scope for context** - Push UserId and TenantId to logging scope via middleware/filters when available. Do NOT pass them as parameters to every log method. Logging scope automatically propagates through the call chain.
```

**Also add reference link at top of Logging Pattern section:**

```markdown
## Logging Pattern

**Always follow these logging conventions. See [`docs/LOGGING-POLICY.md`](docs/LOGGING-POLICY.md) for complete policy.**
```

## 2. Create Middleware for UserId/TenantId Scope Injection

**Status:** Recommended enhancement (not required immediately)

**Location:** `src/Controllers/Middleware/LoggingContextMiddleware.cs`

**Purpose:** Automatically inject UserId and TenantId into logging scope for all requests.

```csharp
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace YoFi.V3.Controllers.Middleware;

/// <summary>
/// Middleware that enriches logging context with user and tenant information.
/// </summary>
/// <remarks>
/// Pushes UserId and TenantId to logging scope when available, making them
/// automatically available in all logs without explicit parameter passing.
/// </remarks>
public class LoggingContextMiddleware(RequestDelegate next, ILogger<LoggingContextMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var userId = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var tenantId = context.Items["TenantId"] as Guid?;

        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["UserId"] = userId ?? "anonymous",
            ["TenantId"] = tenantId?.ToString() ?? "none"
        }))
        {
            await next(context);
        }
    }
}
```

**Registration in Program.cs:**

```csharp
// After authentication/authorization middleware
app.UseMiddleware<LoggingContextMiddleware>();
```

**Benefits:**
- UserId and TenantId automatically included in all logs
- No need to pass these as parameters to logger methods
- Shows up in structured logging in Application Insights
- Custom console logger already captures scope information

## 3. Update appsettings.json Configuration

**Location:** `src/BackEnd/appsettings.json`

**Current state:** Check if logging levels align with policy

**Recommended production configuration:**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "System": "Warning",
      "Microsoft.AspNetCore.Hosting": "Warning",
      "Microsoft.AspNetCore.Routing": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Warning",
      "Microsoft.AspNetCore.Authentication": "Warning"
    }
  }
}
```

**Development configuration (`appsettings.Development.json`):**

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft": "Warning",
      "System": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Warning"
    }
  }
}
```

**Note:** EF Core SQL logging can be temporarily enabled by changing `Database.Command` to `Information` when debugging database issues.

## 4. Enhance CustomExceptionHandler Logging

**Location:** `src/Controllers/Middleware/CustomExceptionHandler.cs`

**Current state:** Already logs exceptions, verify it includes all required context

**Verify includes:**
- ✅ Full stack trace (including inner exceptions)
- ✅ Exception data dictionary
- ✅ Request path and HTTP method
- ✅ UserId (if authenticated) - via logging scope
- ✅ TenantId (if in tenant context) - via logging scope
- ✅ TraceId - automatically included via Activity

**If missing any context, enhance the logging call.**

## 5. Add Refresh Token Logging for Development

**Location:** Where refresh tokens are generated (likely in AuthController or related service)

**Implementation:**

```csharp
// In development/container environments only
#if DEBUG
private void LogRefreshTokenGenerated(string refreshToken)
{
    // Log only first 8 characters for security
    var tokenPreview = refreshToken.Length > 8
        ? refreshToken.Substring(0, 8) + "..."
        : refreshToken;

    logger.LogDebug("Refresh token generated: {TokenPreview}", tokenPreview);
}
#endif
```

**Alternative using conditional compilation:**

```csharp
private void LogRefreshTokenInDevelopment(string refreshToken)
{
    var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

    if (environment == "Development" || environment == "Container")
    {
        var tokenPreview = refreshToken.Length > 8
            ? refreshToken.Substring(0, 8) + "..."
            : refreshToken;

        logger.LogDebug("Refresh token generated: {TokenPreview}", tokenPreview);
    }
}
```

**IMPORTANT:** Never log full refresh tokens in any environment.

## 6. File Import/Export Logging (Future)

**Status:** Design recommendations for when feature is implemented

**Recommendations:**
1. **Log at API layer** - Controller logs start/completion of import/export
2. **Progress logging** - If Application layer needs to log progress:
   ```csharp
   // Application/Features/ImportFeature.cs
   public async Task<ImportResultDto> ImportTransactionsAsync(
       IReadOnlyCollection<TransactionEditDto> transactions)
   {
       LogImportStarting(transactions.Count);

       for (int i = 0; i < transactions.Count; i++)
       {
           await ProcessTransaction(transactions[i]);

           if (i % 100 == 0)
           {
               LogImportProgress(i, transactions.Count);
           }
       }

       LogImportComplete(transactions.Count);
   }
   ```
3. **Validation errors** - Log row/line numbers for failed validations
4. **File metadata** - Log file size, format, but NOT file contents

## 7. Documentation Updates

### Update scripts/README.md

**If not already documented:**

Add note about log level configuration:

```markdown
## Logging

All scripts use Write-Host with colors:
- Cyan: Informational messages
- Green: Success messages
- Yellow: Warnings
- Red: Errors (via Write-Error)

Application logs are configured per environment in appsettings.json.
See [docs/LOGGING-POLICY.md](../docs/LOGGING-POLICY.md) for details.
```

### Update README.md (Project Root)

**Add link to logging policy in relevant section:**

```markdown
## Development

- [Logging Policy](docs/LOGGING-POLICY.md) - Logging standards and conventions
```

## 8. Code Review Checklist Updates

**Create or update:** `docs/CODE-REVIEW-CHECKLIST.md` (if exists)

**Add logging section:**

```markdown
### Logging
- [ ] Uses LoggerMessage source generation (no direct `_logger.Log*()` calls)
- [ ] Event IDs are explicit and unique within the class
- [ ] CallerMemberName used for location tracking
- [ ] No sensitive data logged (emails, tokens, amounts, payee names)
- [ ] Logging at API layer only (unless justified exception)
- [ ] Debug level for "Starting", Information level for "OK"
- [ ] Full XML documentation on class and public methods
```

## 9. Audit Existing Code

**Action:** Review existing controllers for policy compliance

**Check for:**
1. ✅ All controllers use LoggerMessage pattern
2. ✅ No sensitive data in log messages
3. ✅ Event IDs are unique and sequential
4. ✅ Debug "Starting" and Information "OK" pattern followed
5. ❌ Any Application/Data layer logging that should be removed

**Known compliant controllers:**
- [`TransactionsController`](../../src/Controllers/TransactionsController.cs)
- [`TenantController`](../../src/Controllers/Tenancy/Api/TenantController.cs)
- [`WeatherController`](../../src/Controllers/WeatherController.cs)
- [`VersionController`](../../src/Controllers/VersionController.cs)

**Review needed:**
- [`AuthController`](../../src/Controllers/AuthController.cs) - Check if refresh tokens are logged
- [`TestControlController`](../../src/Controllers/TestControlController.cs) - Verify logging compliance

## 10. Testing Updates

### Integration Tests

**Verify:** Integration tests that validate logging behavior

**Example test pattern:**

```csharp
[Test]
public async Task GetTransactions_ValidRequest_LogsStartAndOk()
{
    // Given: A valid request with mocked logger

    // When: Request is made
    var response = await _client.GetAsync("/api/transactions");

    // Then: Response is successful
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

    // And: Debug "Starting" and Information "OK" logs were written
    // (Verify via test logger capture if implemented)
}
```

**Consider:** Implementing test logger capture for verifying logging behavior in integration tests.

## 11. CI/CD Pipeline Updates

**Location:** `.github/workflows/` or Azure Pipelines configuration

**Ensure:**
1. Container logs are captured during CI runs
2. Logs are attached to pipeline artifacts for debugging
3. Log retention follows policy (subject to Azure Pipeline retention policies)

**Example GitHub Actions step:**

```yaml
- name: Capture container logs
  if: failure()
  run: docker logs <container-name> > container-logs.txt

- name: Upload logs
  if: failure()
  uses: actions/upload-artifact@v3
  with:
    name: container-logs
    path: container-logs.txt
    retention-days: 30
```

## 12. Application Insights Configuration

**Location:** `src/ServiceDefaults/Extensions.cs` or Application Insights setup

**Verify:**
- Application Insights is configured with 30-day retention
- Telemetry includes custom dimensions (UserId, TenantId from logging scope)
- Sampling is configured appropriately for production

**Check appsettings.json:**

```json
{
  "ApplicationInsights": {
    "ConnectionString": "...",
    "EnableAdaptiveSampling": true,
    "EnableDependencyTracking": true,
    "EnableEventCounterCollection": true
  }
}
```

## Summary Checklist

- [ ] Update `.roorules` with new logging rules (requires code mode)
- [ ] Consider implementing LoggingContextMiddleware for automatic UserId/TenantId injection
- [ ] Verify appsettings.json log levels match policy
- [ ] Audit CustomExceptionHandler for complete exception context
- [ ] Add development-only refresh token logging (first 8 chars)
- [ ] Design file import logging strategy (when feature is implemented)
- [ ] Update project documentation with logging policy links
- [ ] Create/update code review checklist
- [ ] Audit existing controllers for policy compliance
- [ ] Verify CI/CD captures and retains logs appropriately
- [ ] Confirm Application Insights configuration and retention

## Questions or Issues

If implementation questions arise:
1. Reference the [Logging Policy](../LOGGING-POLICY.md) document
2. Check [Logging Architecture Analysis](LOGGING-ARCHITECTURE-ANALYSIS.md) for rationale
3. Review [Custom Console Logger README](../../src/BackEnd/Logging/README.md) for technical details
4. Consult team for policy clarifications

---

**Next Steps:** Switch to code mode to implement `.roorules` updates and any code changes needed.
