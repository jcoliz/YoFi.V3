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

13. **Sensitive data rules** - NEVER log in production: email addresses, passwords, JWT tokens, refresh tokens, tenant names, transaction amounts, payee names. CAN log: User GUIDs, Tenant GUIDs, TraceIds/SpanIds. In local development, full refresh tokens CAN be logged at Debug level for debugging. In container/CI, log first 8-12 chars only. Users provide TraceId or GUID when reporting issues.

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

## 5. Implement Log Redaction (Recommended Approach)

**Status:** .NET 8+ includes `Microsoft.Extensions.Compliance.Redaction` for environment-aware log redaction

**Benefits:**
- ✅ Declarative approach - annotate parameters, not manual checks
- ✅ Centralized configuration per environment
- ✅ Works with LoggerMessage source generation
- ✅ Production-safe by default
- ✅ Easy to extend with custom redactors

### 5.1 Add Package Reference

**Location:** `src/BackEnd/YoFi.V3.BackEnd.csproj`

```xml
<PackageReference Include="Microsoft.Extensions.Compliance.Redaction" Version="9.0.0" />
```

### 5.2 Create Custom Redactors

**Location:** `src/BackEnd/Logging/EnvironmentAwareRedactor.cs`

```csharp
using Microsoft.Extensions.Compliance.Redaction;

namespace YoFi.V3.BackEnd.Logging;

/// <summary>
/// Redacts sensitive data based on environment.
/// </summary>
/// <remarks>
/// In Development: No redaction (full visibility for debugging)
/// In Container: Partial redaction (first 8-12 characters)
/// In Production: Full redaction (complete protection)
/// </remarks>
public class EnvironmentAwareRedactor : Redactor
{
    private readonly string _environment;
    private readonly int _partialLength;

    public EnvironmentAwareRedactor(string environment = "Production", int partialLength = 12)
    {
        _environment = environment;
        _partialLength = partialLength;
    }

    public override int GetRedactedLength(ReadOnlySpan<char> input)
        => _environment == "Development"
            ? input.Length
            : _environment == "Container"
                ? Math.Min(input.Length, _partialLength + 3)
                : 10; // "[REDACTED]"

    public override int Redact(ReadOnlySpan<char> source, Span<char> destination)
    {
        // Development: No redaction
        if (_environment == "Development")
        {
            source.CopyTo(destination);
            return source.Length;
        }

        // Container: Partial redaction (first N chars + "...")
        if (_environment == "Container")
        {
            var length = Math.Min(source.Length, _partialLength);
            source.Slice(0, length).CopyTo(destination);
            if (source.Length > _partialLength)
            {
                "...".AsSpan().CopyTo(destination.Slice(length));
                return length + 3;
            }
            return length;
        }

        // Production: Full redaction
        "[REDACTED]".AsSpan().CopyTo(destination);
        return 10;
    }
}
```

### 5.3 Define Data Classifications

**Location:** `src/BackEnd/Logging/DataClassifications.cs`

**Philosophy:** Classifications describe **what the data IS** (semantically), considering both the **type of information** (PII, financial, etc.) AND the **nature of the data** (test vs. production). Redaction policy is configured separately based on environment and classification.

```csharp
using Microsoft.Extensions.Compliance.Classification;

namespace YoFi.V3.BackEnd.Logging;

/// <summary>
/// Personally Identifiable Information (PII).
/// </summary>
/// <remarks>
/// Examples: Email addresses, usernames, full names, phone numbers.
///
/// Redaction policy:
/// - Development: Not redacted (test data)
/// - Container/CI: Not redacted (test data)
/// - Production: Fully redacted (real user PII)
///
/// Used for data that identifies or can be used to identify a specific person.
/// In development/testing, this is synthetic test data. In production, it's real PII
/// that must be protected for privacy compliance (GDPR, CCPA, etc.).
/// </remarks>
public class PIIClassification : DataClassificationAttribute
{
    public PIIClassification() : base("PII") { }
}

/// <summary>
/// Test/synthetic data that mimics sensitive information but has no real-world value.
/// </summary>
/// <remarks>
/// Examples: Test emails, test usernames, seed data, synthetic financial records.
///
/// Redaction policy:
/// - Development: Not redacted (helps debugging)
/// - Container/CI: Not redacted (helps debugging functional tests)
/// - Production: N/A (should never appear in production)
///
/// This classification is for data that LOOKS like PII or sensitive data but is
/// actually synthetic/fake. Unlike [PII] which describes data that might be test
/// data in dev but is real in production, [TestData] describes data that is ALWAYS
/// fake and has no real-world value.
///
/// Use this when you want to explicitly document "this is safe to log everywhere
/// because it's always test data." Useful for:
/// - Hardcoded test users (test@example.com)
/// - Seed data accounts
/// - Demo/sample data
/// - Data in test-only endpoints
/// </remarks>
public class TestDataClassification : DataClassificationAttribute
{
    public TestDataClassification() : base("TestData") { }
}

/// <summary>
/// Security secrets and credentials that should NEVER be logged.
/// </summary>
/// <remarks>
/// Examples: Passwords, API keys, connection strings, JWT access tokens, private keys.
///
/// Redaction policy:
/// - Development: Fully redacted (security best practice)
/// - Container/CI: Fully redacted (security best practice)
/// - Production: Fully redacted (security requirement)
///
/// Used for credentials that should never appear in logs in any environment.
/// Even in development, logging these creates security risks (accidental commits,
/// exposed logs, bad habits). Always redacted using ErasingRedactor.
/// </remarks>
public class SecretsClassification : DataClassificationAttribute
{
    public SecretsClassification() : base("Secrets") { }
}

/// <summary>
/// Authentication credentials and security tokens.
/// </summary>
/// <remarks>
/// Examples: Refresh tokens, session tokens, bearer tokens.
/// NOTE: Access tokens (JWTs) should use SecretsClassification instead.
///
/// Redaction policy:
/// - Development: Not redacted (full visibility for debugging)
/// - Container/CI: Partially redacted (first 12 characters visible)
/// - Production: Fully redacted (security credentials)
///
/// Used for tokens that enable session authentication. Unlike access tokens,
/// these are long-lived credentials that benefit from partial visibility in
/// testing environments for debugging authentication flows.
/// </remarks>
public class AuthTokenClassification : DataClassificationAttribute
{
    public AuthTokenClassification() : base("AuthToken") { }
}

/// <summary>
/// Financial or sensitive business data.
/// </summary>
/// <remarks>
/// Examples: Transaction amounts, payee names, account balances, tenant names.
///
/// Redaction policy:
/// - Development: Not redacted (test/seed data)
/// - Container/CI: Not redacted (test/seed data)
/// - Production: Fully redacted (sensitive financial information)
///
/// Used for financial data that has no real-world value in test environments
/// but represents actual sensitive business/financial information in production.
/// Similar policy to PII but semantically different (business data vs. personal data).
/// </remarks>
public class FinancialDataClassification : DataClassificationAttribute
{
    public FinancialDataClassification() : base("FinancialData") { }
}

```

**Note:** There is NO `[SystemIdentifier]` classification. Data that never needs redaction (GUIDs, TraceIds, SpanIds, counts, IDs) simply doesn't need a classification attribute. The absence of a classification attribute means "this is safe to log as-is in all environments."

### When to Use Each Classification

**Use `[PII]`** when the data:
- Identifies a person (email, username, phone)
- Is test data in dev/container but real data in production
- Needs environment-aware redaction

**Use `[TestData]`** when the data:
- Is ALWAYS synthetic/fake (even in "production" if that endpoint is test-only)
- Looks like sensitive data but has no real-world value
- Is hardcoded test data (test@example.com, demo accounts)
- Appears in test control endpoints

**Use `[Secrets]`** when the data:
- Should NEVER appear in logs (passwords, API keys)
- Is a security risk even as test data
- Needs to be fully redacted everywhere

**Use `[AuthToken]`** when the data:
- Is a session/refresh token (not an access token)
- Benefits from partial visibility in testing (debugging auth flows)
- Needs different treatment than secrets

**Use `[FinancialData]`** when the data:
- Represents money, transactions, or sensitive business info
- Is test/seed data in dev/container but real in production
- Has compliance implications separate from PII

**Don't use any classification** when the data:
- Is inherently safe (GUIDs, IDs, TraceIds, SpanIds, counts, status codes)
- Never needs redaction in any environment
- Is just a technical identifier with no PII or sensitive information
- Logging it provides operational visibility without privacy concerns

### 5.4 Configure Redaction in Program.cs

**Location:** `src/BackEnd/Program.cs`

```csharp
using Microsoft.Extensions.Compliance.Redaction;
using YoFi.V3.BackEnd.Logging;

var builder = WebApplication.CreateBuilder(args);

// Get environment name
var environment = builder.Environment.EnvironmentName;

// Configure compliance redaction
builder.Services.AddRedaction(redaction =>
{
    // Test data: NEVER redacted (always synthetic, safe in all environments)
    redaction.SetRedactor<TestDataClassification>(
        new NullRedactor()); // No redaction

    // PII: Not redacted in dev/container (test data), redacted in production (real PII)
    redaction.SetRedactor<PIIClassification>(
        new EnvironmentAwareRedactor(environment, partialLength: 0)); // Full or none

    // Financial data: Not redacted in dev/container (test data), redacted in production
    redaction.SetRedactor<FinancialDataClassification>(
        new EnvironmentAwareRedactor(environment, partialLength: 0)); // Full or none

    // Auth tokens: Full in dev, partial in container, redacted in production
    redaction.SetRedactor<AuthTokenClassification>(
        new EnvironmentAwareRedactor(environment, partialLength: 12));

    // Secrets: ALWAYS fully redacted in ALL environments
    redaction.SetRedactor<SecretsClassification>(
        new ErasingRedactor());
});

// ... rest of Program.cs
```

### 5.5 Use in Controllers with LoggerMessage

**Example: Logging with redaction**

```csharp
using Microsoft.Extensions.Compliance.Classification;
using YoFi.V3.BackEnd.Logging;

public partial class AuthController : ControllerBase
{
    // PII: Email addresses
    [LoggerMessage(10, LogLevel.Debug, "{Location}: Login attempt {Email}")]
    private partial void LogLoginAttempt(
        [PII] string email,
        [CallerMemberName] string? location = null);

    // Auth tokens: Refresh tokens (partial visibility helpful for debugging)
    [LoggerMessage(11, LogLevel.Debug, "{Location}: Refresh token generated {Token}")]
    private partial void LogRefreshToken(
        [AuthToken] string token,
        [CallerMemberName] string? location = null);

    // Mixed: System identifier (UserId) is always safe, Email is PII
    [LoggerMessage(12, LogLevel.Debug, "{Location}: User created {UserId} {Email}")]
    private partial void LogUserCreated(
        Guid userId,  // No classification needed - GUIDs are safe
        [PII] string email,
        [CallerMemberName] string? location = null);

    // Secrets: NEVER log passwords (even in development)
    [LoggerMessage(13, LogLevel.Debug, "{Location}: Password validation failed for user {UserId}")]
    private partial void LogPasswordValidationFailed(
        Guid userId,  // Safe to log
        // NOTE: Never log the password itself, even with [Secrets] attribute
        [CallerMemberName] string? location = null);

    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        // In development: Logs "Login attempt test@example.com"
        // In container: Logs "Login attempt test@example.com"
        // In production: Logs "Login attempt [REDACTED]"
        LogLoginAttempt(request.Email);

        // ... authentication logic

        var tokens = await GenerateTokens(user);

        // In development: Logs full token
        // In container: Logs first 12 chars + "..."
        // In production: Logs "[REDACTED]"
        LogRefreshToken(tokens.RefreshToken);

        return Ok(tokens);
    }
}

/// <summary>
/// Example: Transactions controller with financial data
/// </summary>
public partial class TransactionsController : ControllerBase
{
    // Financial data: Transaction amounts, payee names
    [LoggerMessage(20, LogLevel.Debug, "{Location}: Creating transaction {Amount} to {Payee}")]
    private partial void LogTransactionCreate(
        [FinancialData] decimal amount,
        [FinancialData] string payee,
        [CallerMemberName] string? location = null);

    // No classification needed for safe identifiers
    [LoggerMessage(21, LogLevel.Information, "{Location}: OK Count={Count} TenantId={TenantId}")]
    private partial void LogOkCount(
        int count,
        Guid tenantId,  // No attribute - GUIDs are safe, don't need classification
        [CallerMemberName] string? location = null);
}

/// <summary>
/// Example: Test control controller with test data
/// </summary>
public partial class TestControlController : ControllerBase
{
    // Test data: Hardcoded test users - ALWAYS safe to log
    [LoggerMessage(30, LogLevel.Debug, "{Location}: Seeding test user {Email}")]
    private partial void LogSeedTestUser(
        [TestData] string email,  // test@example.com - always fake
        [CallerMemberName] string? location = null);

    // Distinction: [TestData] = hardcoded (always fake) vs [PII] = from request (test in dev, real in prod)
}
```

### Additional Classification Examples

```csharp
// PII: Usernames, emails, phone numbers
[LoggerMessage(1, LogLevel.Debug, "{Location}: User {Username} registration")]
private partial void LogUserRegistration(
    [PII] string username,
    [CallerMemberName] string? location = null);

// TestData: Hardcoded test accounts (always fake, never redact)
[LoggerMessage(2, LogLevel.Debug, "{Location}: Using test account {Email}")]
private partial void LogTestAccount(
    [TestData] string email,  // "demo@example.com" - always fake
    [CallerMemberName] string? location = null);

// FinancialData: Tenant names (may contain business-sensitive info)
[LoggerMessage(3, LogLevel.Debug, "{Location}: Tenant created {TenantName}")]
private partial void LogTenantCreated(
    [FinancialData] string tenantName,
    [CallerMemberName] string? location = null);

// Secrets: API keys (NEVER log, even with attribute - this is for documentation)
// DON'T do this - don't log secrets at all:
// [LoggerMessage(4, LogLevel.Debug, "{Location}: API call with key {ApiKey}")]
// private partial void LogApiCall([Secrets] string apiKey, ...);

// Instead, log that an API call was made without the key:
[LoggerMessage(4, LogLevel.Debug, "{Location}: External API call initiated")]
private partial void LogApiCall([CallerMemberName] string? location = null);

// No classification: Safe technical data
[LoggerMessage(5, LogLevel.Debug, "{Location}: Processing {Count} items for tenant {TenantId}")]
private partial void LogProcessing(
    int count,        // No attribute - counts are safe
    Guid tenantId,    // No attribute - GUIDs are safe
    [CallerMemberName] string? location = null);
```

### 5.6 Benefits Over Manual Redaction

**Declarative and type-safe:**
```csharp
// ✅ Good: Compiler enforces classification
LogLoginAttempt([TestData] string email)

// ❌ Bad: Manual environment checks everywhere
if (env == "Production") LogRedacted(email) else Log(email)
```

**Centralized policy:**
- All redaction logic in one place (Program.cs)
- Change environment behavior without touching controller code
- Easy to add new classifications

**Works with all log sinks:**
- Console logger
- Application Insights
- File logging
- Any ILogger implementation

**Audit trail:**
- Easy to find all redacted parameters (search for attributes)
- Clear intent in code (what data is sensitive)

### 5.7 Testing Redaction

**Unit test example:**

```csharp
[Test]
public void Redactor_Development_DoesNotRedact()
{
    // Given: Development environment redactor
    var redactor = new EnvironmentAwareRedactor("Development");
    var input = "test@example.com";
    var destination = new char[100];

    // When: Redacting sensitive data
    var length = redactor.Redact(input.AsSpan(), destination.AsSpan());

    // Then: Data is not redacted
    var result = new string(destination, 0, length);
    Assert.That(result, Is.EqualTo("test@example.com"));
}

[Test]
public void Redactor_Production_FullyRedacts()
{
    // Given: Production environment redactor
    var redactor = new EnvironmentAwareRedactor("Production");
    var input = "test@example.com";
    var destination = new char[100];

    // When: Redacting sensitive data
    var length = redactor.Redact(input.AsSpan(), destination.AsSpan());

    // Then: Data is fully redacted
    var result = new string(destination, 0, length);
    Assert.That(result, Is.EqualTo("[REDACTED]"));
}
```

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

- [x] Update `.roorules` with new logging rules
- [ ] **RECOMMENDED:** Implement log redaction using `Microsoft.Extensions.Compliance.Redaction`
  - [ ] Add package reference to BackEnd project
  - [ ] Create `EnvironmentAwareRedactor` class
  - [ ] Define data classifications (`TestDataClassification`, `AuthTokenClassification`, etc.)
  - [ ] Configure redaction in Program.cs
  - [ ] Add classification attributes to log method parameters
  - [ ] Add unit tests for redactors
- [ ] Consider implementing LoggingContextMiddleware for automatic UserId/TenantId injection
- [ ] Verify appsettings.json log levels match policy
- [ ] Audit CustomExceptionHandler for complete exception context
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
