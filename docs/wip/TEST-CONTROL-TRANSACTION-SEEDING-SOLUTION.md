# Transaction Seeding Solution for Test Control API

## Problem Analysis

### The Issue

The [`SeedTransactions`](../../src/Controllers/TestControlController.cs:398) method in TestControlController fails with `TenantContextNotSetException` when trying to seed transactions for test workspaces.

### Root Cause

The problem occurs in this dependency chain:

```
SeedTransactions (line 398)
  ↓ [FromServices] TransactionsFeature (line 403) - DI attempts to inject
    ↓ Constructor calls (line 21 in TransactionsFeature.cs)
      ↓ tenantProvider.CurrentTenant - IMMEDIATE ACCESS
        ↓ TenantContext.CurrentTenant getter (line 28)
          ↓ Throws TenantContextNotSetException (line 35)
```

**The critical issue**: [`TransactionsFeature`](../../src/Application/Features/TransactionsFeature.cs:19) accesses `CurrentTenant` **in its constructor** (line 21), which happens during dependency injection **before** any manual setup can occur.

## Solution Options

### Option 1: Lazy Tenant Resolution in TransactionsFeature ❌

**Approach**: Change TransactionsFeature to defer tenant access until methods are called.

**Verdict**: ❌ Not recommended - violates architectural boundaries

### Option 2: Direct Repository Access in Test Controller ✅

**Approach**: Bypass TransactionsFeature and create transactions directly using `IDataProvider` and `ITenantRepository`.

**Verdict**: ✅ Solid fallback option but duplicates validation logic

### Option 3: Create Dedicated SeedingFeature 🤔

**Approach**: New feature class that takes tenant ID/Key as method parameters instead of constructor injection.

**Verdict**: 🤔 Over-engineered for this use case

### Option 4: Manual TransactionsFeature Creation ✅✅

**Approach**: Set up TenantContext first, then manually create TransactionsFeature instance in the method.

**Verdict**: ✅✅ Excellent solution - reuses all feature logic

### Option 5: Anonymous Tenant Access Authorization Policy ✅✅✅ **RECOMMENDED**

**Approach**: Create a new authorization policy that allows unauthenticated access to tenant-scoped endpoints by setting `HttpContext.Items["TenantKey"]` without requiring authentication.

**Pros**:
- ✅✅✅ **MOST ARCHITECTURALLY SOUND** - Works within existing framework
- ✅✅ **EXPLICIT SECURITY INTENT** - Policy name declares "this endpoint allows anonymous tenant access"
- ✅ **NO CODE DUPLICATION** - Uses existing TransactionsFeature via DI
- ✅ **REUSES INFRASTRUCTURE** - Leverages existing TenantContextMiddleware flow
- ✅ **STANDARD PATTERN** - Uses ASP.NET Core authorization system properly
- ✅ **TESTABLE** - Authorization policies are easily unit tested
- ✅ **DISCOVERABLE** - Policy appears in authorization configuration
- ✅ **MAINTAINABLE** - Changes to tenant context flow apply automatically

**Cons**:
- Requires new authorization handler and requirement classes
- Slightly more upfront setup (but cleaner long-term)

**Verdict**: ✅✅✅ **BEST SOLUTION** - Most elegant and architecturally sound

## Recommended Solution: Option 5 - Anonymous Tenant Access Policy

### How The Current Authorization Flow Works

Looking at [`TenantRoleHandler`](../../src/Controllers/Tenancy/Authorization/TenantRoleHandler.cs:21):

1. **Extracts tenant key from route** (line 41): `var tenantKey = httpContext.Request.RouteValues["tenantKey"]?.ToString();`
2. **Validates user has tenant_role claim** (lines 54-56): Checks for `tenant_role` claim matching tenant
3. **Stores in HttpContext.Items** (lines 67-68):
   ```csharp
   httpContext.Items["TenantKey"] = Guid.Parse(tenantKey);
   httpContext.Items["TenantRole"] = userRole;
   ```
4. **TenantContextMiddleware reads it** (line 57-62 in TenantContextMiddleware.cs):
   ```csharp
   if (context.Items.TryGetValue("TenantKey", out var tenantKeyObj) &&
       tenantKeyObj is Guid tenantKey)
   {
       await tenantContext.SetCurrentTenantAsync(tenantKey);
   }
   ```

### Proposed Architecture

Create a **parallel authorization path** for test endpoints:

```
Test Endpoint with [Authorize("AllowAnonymousTenantAccess")]
  ↓
  Authentication Middleware (user may be anonymous)
  ↓
  Authorization Middleware → AnonymousTenantAccessHandler
    → Extracts tenantKey from route
    → Sets HttpContext.Items["TenantKey"] = tenantKey
    → context.Succeed(requirement) - ALLOWS anonymous access
  ↓
  TenantContextMiddleware (same as before)
    → Reads HttpContext.Items["TenantKey"]
    → Sets TenantContext.CurrentTenant
  ↓
  Controller action
    → DI injects TransactionsFeature
    → Constructor succeeds (CurrentTenant is set)
    → Everything works normally!
```

### Key Insight

The **ONLY difference** from normal authenticated flow:
- Normal: User authenticated → Claims checked → TenantKey stored
- Test: User anonymous → Route checked → TenantKey stored

**Everything downstream is identical!**

## Implementation Plan

### Step 1: Create AnonymousTenantAccessRequirement

Create new file: `src/Controllers/Tenancy/Authorization/AnonymousTenantAccessRequirement.cs`

```csharp
using Microsoft.AspNetCore.Authorization;

namespace YoFi.V3.Controllers.Tenancy.Authorization;

/// <summary>
/// Authorization requirement that allows anonymous access to tenant-scoped endpoints.
/// </summary>
/// <remarks>
/// This requirement is used for test utility endpoints that need tenant context
/// without user authentication. The handler validates that a tenant key exists
/// in the route and sets it in HttpContext.Items for downstream middleware.
///
/// **SECURITY WARNING**: Only use this for test utility endpoints that have
/// their own validation logic (e.g., checking for __TEST__ prefix).
/// </remarks>
public class AnonymousTenantAccessRequirement : IAuthorizationRequirement
{
    // Marker class - no properties needed
}
```

### Step 2: Create AnonymousTenantAccessHandler

Create new file: `src/Controllers/Tenancy/Authorization/AnonymousTenantAccessHandler.cs`

```csharp
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace YoFi.V3.Controllers.Tenancy.Authorization;

/// <summary>
/// Authorization handler that allows anonymous access to tenant-scoped endpoints.
/// </summary>
/// <param name="httpContextAccessor">Accessor for the current HTTP context.</param>
/// <param name="logger">Logger for diagnostic output.</param>
/// <remarks>
/// This handler extracts the tenant key from the route and stores it in
/// <see cref="HttpContext.Items"/> without requiring user authentication.
/// The downstream <see cref="Context.TenantContextMiddleware"/> will pick up
/// the tenant key and set the tenant context normally.
///
/// **SECURITY**: This handler ONLY validates that a tenant key exists in the route.
/// Endpoints using this policy MUST implement their own security validation
/// (e.g., checking __TEST__ prefix on tenant names).
/// </remarks>
public partial class AnonymousTenantAccessHandler(
    IHttpContextAccessor httpContextAccessor,
    ILogger<AnonymousTenantAccessHandler> logger)
    : AuthorizationHandler<AnonymousTenantAccessRequirement>
{
    /// <summary>
    /// Handles the authorization requirement by extracting and storing the tenant key.
    /// </summary>
    /// <param name="context">The authorization context.</param>
    /// <param name="requirement">The anonymous tenant access requirement.</param>
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AnonymousTenantAccessRequirement requirement)
    {
        LogHandlerCalled(context.User.Identity?.IsAuthenticated ?? false);

        var httpContext = httpContextAccessor.HttpContext;

        if (httpContext == null)
        {
            LogNoHttpContext();
            return Task.CompletedTask;
        }

        // Extract tenant key from route (same as TenantRoleHandler)
        var tenantKeyString = httpContext.Request.RouteValues["tenantKey"]?.ToString();

        if (string.IsNullOrEmpty(tenantKeyString))
        {
            LogNoTenantKey();
            return Task.CompletedTask; // Fail - no tenant key in route
        }

        if (!Guid.TryParse(tenantKeyString, out var tenantKey))
        {
            LogInvalidTenantKey(tenantKeyString);
            return Task.CompletedTask; // Fail - invalid GUID format
        }

        LogSettingTenantKey(tenantKey);

        // Store tenant key in HttpContext.Items for TenantContextMiddleware
        // (same location as TenantRoleHandler uses)
        httpContext.Items["TenantKey"] = tenantKey;

        // Succeed - allow anonymous access with tenant context
        context.Succeed(requirement);

        LogAuthorizationSuccess(tenantKey);
        return Task.CompletedTask;
    }

    [LoggerMessage(1, LogLevel.Debug, "{Location}: Handler called. IsAuthenticated={IsAuthenticated}")]
    private partial void LogHandlerCalled(bool isAuthenticated, [CallerMemberName] string? location = null);

    [LoggerMessage(2, LogLevel.Warning, "{Location}: No HttpContext available")]
    private partial void LogNoHttpContext([CallerMemberName] string? location = null);

    [LoggerMessage(3, LogLevel.Warning, "{Location}: No tenant key in route")]
    private partial void LogNoTenantKey([CallerMemberName] string? location = null);

    [LoggerMessage(4, LogLevel.Warning, "{Location}: Invalid tenant key format: {TenantKeyString}")]
    private partial void LogInvalidTenantKey(string tenantKeyString, [CallerMemberName] string? location = null);

    [LoggerMessage(5, LogLevel.Debug, "{Location}: Setting tenant key {TenantKey}")]
    private partial void LogSettingTenantKey(Guid tenantKey, [CallerMemberName] string? location = null);

    [LoggerMessage(6, LogLevel.Information, "{Location}: Anonymous tenant access granted for tenant {TenantKey}")]
    private partial void LogAuthorizationSuccess(Guid tenantKey, [CallerMemberName] string? location = null);
}
```

### Step 3: Register Policy in ServiceCollectionExtensions

Update [`src/Controllers/Tenancy/ServiceCollectionExtensions.cs`](../../src/Controllers/Tenancy/ServiceCollectionExtensions.cs:24):

```csharp
public static IServiceCollection AddTenancy(this IServiceCollection services)
{
    // ... existing registrations ...

    // Register the authorization handlers
    services.AddSingleton<IAuthorizationHandler, TenantRoleHandler>();
    services.AddSingleton<IAuthorizationHandler, AnonymousTenantAccessHandler>(); // ← ADD THIS

    // Register authorization policies
    services.AddAuthorization(options =>
    {
        // Register tenant role policies
        foreach (TenantRole role in Enum.GetValues<TenantRole>())
        {
            options.AddPolicy($"TenantRole_{role}", policy =>
                policy.Requirements.Add(new TenantRoleRequirement(role)));
        }

        // Register anonymous tenant access policy for test endpoints
        options.AddPolicy("AllowAnonymousTenantAccess", policy =>
        {
            policy.Requirements.Add(new AnonymousTenantAccessRequirement());
            policy.AllowAnonymousUser(); // ← Explicitly allow anonymous users
        });
    });

    return services;
}
```

### Step 4: Apply Policy to SeedTransactions Endpoint

Update [`TestControlController.SeedTransactions`](../../src/Controllers/TestControlController.cs:398):

```csharp
/// <summary>
/// Seed test transactions in a workspace for a user.
/// </summary>
/// <param name="username">The username (must include __TEST__ prefix) of the user.</param>
/// <param name="workspaceKey">The unique key of the workspace.</param>
/// <param name="request">The transaction seeding details.</param>
/// <param name="transactionsFeature">Feature providing transaction operations.</param>
/// <returns>The collection of created transactions.</returns>
/// <remarks>
/// Validates that user has access to the workspace and both user and workspace have __TEST__ prefix.
/// Uses anonymous tenant access policy to allow unauthenticated seeding of test data.
/// Returns 403 if either username or workspace name lacks the prefix.
/// </remarks>
[HttpPost("users/{username}/workspaces/{tenantKey:guid}/transactions/seed")]
[Authorize("AllowAnonymousTenantAccess")] // ← ADD THIS
[ProducesResponseType(typeof(IReadOnlyCollection<TransactionResultDto>), StatusCodes.Status201Created)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
public async Task<IActionResult> SeedTransactions(
    string username,
    Guid tenantKey,
    [FromBody] TransactionSeedRequest request,
    [FromServices] TransactionsFeature transactionsFeature) // ← BACK TO DI!
{
    LogStartingCount(request.Count);

    // ... all existing validation code stays the same ...

    // REMOVE the manual tenant context setup:
    // await tenantContext.SetCurrentTenantAsync(tenantKey);

    // TransactionsFeature is now injected normally via DI
    // TenantContext is already set by middleware reading HttpContext.Items["TenantKey"]

    // Create transactions using the feature
    var random = new Random();
    var createdTransactions = new List<TransactionResultDto>();
    var baseDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));

    for (int i = 1; i <= request.Count; i++)
    {
        var transaction = new TransactionEditDto(
            Date: baseDate.AddDays(random.Next(0, 30)),
            Amount: Math.Round((decimal)(random.NextDouble() * 490 + 10), 2),
            Payee: $"{request.PayeePrefix} {i}"
        );

        var result = await transactionsFeature.AddTransactionAsync(transaction);
        createdTransactions.Add(result);
    }

    LogOkCount(createdTransactions.Count);
    return CreatedAtAction(nameof(SeedTransactions), new { username, tenantKey }, createdTransactions);
}
```

### Step 5: Add Required Using Statements

Ensure these are present:
```csharp
using Microsoft.AspNetCore.Authorization; // For [Authorize]
```

## Why This Is The Best Solution

### Comparison with All Options

| Aspect | Option 2 (Direct) | Option 4 (Manual) | **Option 5 (Policy)** |
|--------|-------------------|-------------------|------------------------|
| Code duplication | ❌ Duplicates validation | ✅ Reuses feature | ✅ Reuses feature |
| Explicit security | 🤔 Implicit | 🤔 Implicit | ✅✅ **Explicit in policy** |
| Architecture | ❌ Breaks layers | ✅ Respects layers | ✅✅ **Standard ASP.NET pattern** |
| Maintenance | ❌ Two places | ✅ One place | ✅✅ **Framework handles it** |
| Discoverability | ❌ Hidden in code | 🤔 Hidden in code | ✅✅ **Policy in config** |
| Testability | 🤔 Test controller | 🤔 Test controller | ✅✅ **Test policy separately** |
| DI usage | ❌ Manual instances | 🤔 Manual feature | ✅✅ **Pure DI** |

### Key Benefits

1. ✅✅✅ **EXPLICIT SECURITY DECLARATION** - `[Authorize("AllowAnonymousTenantAccess")]` clearly states intent
2. ✅ **STANDARD ASP.NET CORE PATTERN** - Uses authorization system as designed
3. ✅ **REUSES ALL INFRASTRUCTURE** - TenantContextMiddleware works unchanged
4. ✅ **ZERO CODE DUPLICATION** - TransactionsFeature used via DI
5. ✅ **DISCOVERABLE** - Policy shows up in authorization configuration
6. ✅ **TESTABLE** - Authorization handlers are easily unit tested
7. ✅ **MAINTAINABLE** - Tenant context flow changes apply automatically
8. ✅ **COMPOSABLE** - Can add additional requirements to policy if needed

### Security Analysis

**Question**: Is it safe to allow anonymous tenant access?

**Answer**: YES, because:
1. **Test-only endpoints** - All TestControlController methods validate `__TEST__` prefix
2. **Explicit policy name** - "AllowAnonymousTenantAccess" makes security intent clear
3. **Additional validation** - Endpoints still validate user exists, workspace exists, user has role
4. **Audit trail** - Authorization logs show when policy is used
5. **Standard pattern** - Similar to `[AllowAnonymous]` but with tenant context

The policy name itself **documents the security model**: "This endpoint intentionally allows anonymous access to tenant-scoped resources for testing purposes."

## Testing Verification

After implementation, verify:

1. ✅ Can seed transactions for test user in test workspace
2. ✅ Policy name appears in authorization logs
3. ✅ HttpContext.Items["TenantKey"] is set by handler
4. ✅ TenantContextMiddleware successfully sets tenant context
5. ✅ TransactionsFeature is injected normally via DI
6. ✅ All validation from TransactionsFeature applies
7. ✅ Returns 201 with collection of created transactions
8. ✅ Transactions appear in database with correct TenantId

## Conclusion

**Recommended Approach**: Option 5 - Anonymous Tenant Access Authorization Policy

This solution is:
- ✅✅✅ **Most architecturally sound** - Works within ASP.NET Core framework
- ✅✅ **Most explicit** - Security intent clear in policy name
- ✅ **Most maintainable** - Standard authorization pattern
- ✅ **Most discoverable** - Policy visible in configuration
- ✅ **Most testable** - Handler can be unit tested independently

The key insight: **Use the authorization system to set up tenant context, not bypass it**. The authorization handler becomes a "tenant context provider" that works for both authenticated and anonymous scenarios.

This is exactly what authorization policies are designed for: declaring "this endpoint has special access rules" in a standardized, testable, maintainable way.
