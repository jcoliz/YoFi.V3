# Tenant Access Denied Username Display Options

## Current State

When a user is denied access to a tenant, a `TenantAccessDeniedException` is thrown that includes:
- `UserId` (Guid) - Currently populated correctly
- `UserName` (string) - Currently always `string.Empty`
- `TenantKey` (Guid) - Currently populated correctly

The exception handler ([`TenancyExceptionHandler.cs`](../src/Controllers/Tenancy/Exceptions/TenancyExceptionHandler.cs:108-112)) already exposes all three fields in the ProblemDetails response, but `userName` is always empty.

**Why is UserName empty?**
- [`TenantFeature`](../src/Application/Tenancy/Features/TenantFeature.cs:86) (Application layer) throws the exception
- `TenantFeature` has access to `userId` but not to `userName`
- `TenantFeature` cannot inject `UserManager<ApplicationUser>` due to architectural constraints (Application layer shouldn't depend on Identity)

**Where is UserName available?**
- [`TenantController`](../src/Controllers/Tenancy/Api/TenantController.cs:48) (Controller layer) has access via `User.Identity.Name` or claims
- `UserManager<ApplicationUser>` (Identity layer) can look it up by userId

## Problem Statement

Should we display the username to users when they are denied access to a tenant? If so, how should we obtain it given the architectural constraints?

## Options Analysis

### Option 1: Keep UserName Empty (Do Nothing)

**Approach:** Accept that `userName` will remain empty in the exception and ProblemDetails response.

**Pros:**
- ✅ Zero implementation work
- ✅ Maintains clean architectural boundaries
- ✅ UserId is already present and uniquely identifies who was denied
- ✅ Simple and straightforward
- ✅ User probably knows their own name anyway

**Cons:**
- ❌ Less user-friendly error messages
- ❌ Exception class has a property that's never used
- ❌ Frontend would need to display UserId instead of name

**Implementation:** None required.

**Verdict:** ⭐ **SIMPLEST** - Best if we decide username isn't important enough to warrant the complexity.

---

### Option 2: Pass UserName from Controller to Feature

**Approach:** Add `userName` parameter to Feature methods that can throw `TenantAccessDeniedException`. Controller extracts name from `User.Identity.Name` and passes it down.

**Example:**
```csharp
// TenantFeature.cs
public async Task<TenantRoleResultDto> GetTenantForUserAsync(
    Guid userId,
    string userName,  // NEW parameter
    Guid tenantKey)
{
    var tenant = await tenantRepository.GetTenantByKeyAsync(tenantKey);
    if (tenant == null)
        throw new TenantNotFoundException(tenantKey);

    var role = await tenantRepository.GetUserTenantRoleAsync(userId.ToString(), tenant.Id);
    if (role == null)
        throw new TenantAccessDeniedException(userId, userName, tenantKey);  // Now has userName

    return new TenantRoleResultDto(...);
}

// TenantController.cs
public async Task<IActionResult> GetTenant(Guid key)
{
    var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    var userName = User.Identity!.Name ?? "Unknown";  // Extract from claims
    var tenant = await tenantFeature.GetTenantForUserAsync(userId, userName, key);
    return Ok(tenant);
}
```

**Pros:**
- ✅ Clean data flow (controller → feature → exception)
- ✅ No architectural violations
- ✅ No async lookups during error handling
- ✅ userName is available where exception is thrown
- ✅ Controller already has this information

**Cons:**
- ❌ Changes method signatures (breaking change if used elsewhere)
- ❌ Adds parameter to every method that might throw this exception
- ❌ Clutters method signatures with error-handling concerns
- ❌ Must update 4 methods: `GetTenantForUserAsync`, `UpdateTenantForUserAsync`, `DeleteTenantForUserAsync`, and potentially others

**Affected Methods in TenantFeature:**
- Line 72: `GetTenantForUserAsync`
- Line 107: `UpdateTenantForUserAsync`
- Line 145: `DeleteTenantForUserAsync`

**Implementation Effort:** Medium (modify 4 methods in Feature, 4 methods in Controller)

**Verdict:** ⭐⭐ **CLEAN** - Best if we want username without architectural compromises, but requires modest refactoring.

---

### Option 3: Look Up UserName in Exception Handler

**Approach:** Inject `UserManager<ApplicationUser>` into `TenancyExceptionHandler` and look up the username when handling `TenantAccessDeniedException`.

**Example:**
```csharp
// TenancyExceptionHandler.cs - Make it a class instead of static
public class TenancyExceptionHandler(UserManager<ApplicationUser> userManager)
{
    public async Task HandleAsync(
        HttpContext httpContext,
        TenancyException exception,
        CancellationToken cancellationToken = default)
    {
        var (statusCode, title) = MapExceptionToResponse(exception);
        httpContext.Response.StatusCode = statusCode;

        var problemDetails = CreateProblemDetails(httpContext, statusCode, title, exception.Message);

        // Look up username if needed
        if (exception is TenantAccessDeniedException accessDenied &&
            string.IsNullOrEmpty(accessDenied.UserName))
        {
            var user = await userManager.FindByIdAsync(accessDenied.UserId.ToString());
            if (user != null)
            {
                // Can't modify exception, so modify problemDetails directly
                problemDetails.Extensions["userName"] = user.UserName ?? "Unknown";
            }
        }
        else
        {
            AddExceptionExtensions(problemDetails, exception);
        }

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
    }
}
```

**Pros:**
- ✅ No changes to Feature layer
- ✅ No method signature changes
- ✅ Centralized in one place
- ✅ Username lookup happens only when needed (on error)

**Cons:**
- ❌ Async database call during error handling (performance impact)
- ❌ Exception handler depends on Identity layer (architectural coupling)
- ❌ Requires converting static class to instance class
- ❌ Must wire up DI registration
- ❌ Error handling code becomes more complex
- ❌ If UserManager call fails, we lose the original error context
- ❌ Violates separation of concerns (exception handler doing business logic)

**Architectural Impact:**
- Controllers → Identity dependency (adds coupling)
- Error handling now has database dependencies
- Makes exception handling slower and more fragile

**Implementation Effort:** Medium-High (convert to instance class, add DI, handle async lookup failures)

**Verdict:** ⭐ **COMPLEX** - Architecturally questionable, adds error-handling risk. Not recommended.

---

### Option 4: Include UserName in HttpContext Items

**Approach:** Controller adds username to `HttpContext.Items`, exception handler reads it from there.

**Example:**
```csharp
// TenantController.cs - in each action method
public async Task<IActionResult> GetTenant(Guid key)
{
    var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    HttpContext.Items["UserName"] = User.Identity!.Name ?? "Unknown";  // Store in context

    var tenant = await tenantFeature.GetTenantForUserAsync(userId, key);
    return Ok(tenant);
}

// TenancyExceptionHandler.cs
private static void AddExceptionExtensions(
    HttpContext httpContext,
    ProblemDetails problemDetails,
    TenancyException exception)
{
    if (exception is TenantAccessDeniedException accessDenied)
    {
        var userName = accessDenied.UserName;
        if (string.IsNullOrEmpty(userName) && httpContext.Items.TryGetValue("UserName", out var contextUserName))
        {
            userName = contextUserName?.ToString() ?? "Unknown";
        }

        problemDetails.Extensions["userId"] = accessDenied.UserId;
        problemDetails.Extensions["userName"] = userName;
        problemDetails.Extensions["tenantKey"] = accessDenied.TenantKey;
    }
}
```

**Pros:**
- ✅ No method signature changes
- ✅ No architectural violations
- ✅ No async lookups
- ✅ Decoupled from Feature layer

**Cons:**
- ❌ Must add to HttpContext.Items in every controller action
- ❌ Easy to forget (no compile-time safety)
- ❌ Implicit communication channel (code is less obvious)
- ❌ Fragile - breaks silently if someone forgets to set it
- ❌ Not self-documenting

**Implementation Effort:** Low (but error-prone)

**Verdict:** ⭐ **FRAGILE** - Works but relies on convention rather than enforcement. Easy to break.

---

## Recommendation Matrix

| Criterion | Option 1<br/>(Do Nothing) | Option 2<br/>(Pass from Controller) | Option 3<br/>(Lookup in Handler) | Option 4<br/>(HttpContext.Items) |
|-----------|:---:|:---:|:---:|:---:|
| **Simplicity** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ | ⭐ | ⭐⭐ |
| **Architecture** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐ | ⭐⭐⭐ |
| **User Experience** | ⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ |
| **Maintainability** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐ | ⭐⭐ |
| **Performance** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐ | ⭐⭐⭐⭐⭐ |
| **Type Safety** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐⭐ |

## Final Recommendation

### Primary Recommendation: **Option 1 (Do Nothing)** ⭐⭐⭐⭐⭐

**Reasoning:**
1. **User probably knows their own name** - In 99% of cases, the user triggering the access denied error IS the user being denied. They don't need to be told their own name.

2. **UserId is sufficient** - The response already includes the `userId`. If needed, the frontend can display: "Access denied for your account (ID: {userId})" or simply "You don't have access to this workspace."

3. **YAGNI (You Aren't Gonna Need It)** - Adding username display adds complexity without clear user benefit. The user knows who they are.

4. **Consider the user experience flow:**
   - User clicks on a workspace they don't have access to
   - Gets error: "You don't have access to this workspace"
   - User thinks: "Oh right, that's not my workspace"
   - Showing username adds nothing: "User 'john@example.com' doesn't have access" - user already knows they are john@example.com

### Backup Recommendation: **Option 2 (Pass from Controller)** ⭐⭐⭐⭐

**When to use:** If user testing shows that seeing the username is genuinely valuable (e.g., in admin scenarios where an admin might be performing actions on behalf of different users).

**Implementation would be:**
1. Add `string userName` parameter to affected Feature methods
2. Extract `User.Identity.Name` in controller actions
3. Pass it through to Feature layer
4. Feature includes it in exception

This is clean, type-safe, and doesn't violate architectural boundaries.

## Decision

**Recommended:** Stick with Option 1 (Do Nothing) unless user feedback demonstrates a real need for username display.

**Rationale:** The exception already captures the `userId`. The user experiencing the error knows who they are. Adding username display would add complexity for marginal UX benefit. Frontend can handle this gracefully with messages like "You don't have access to this workspace."

If username display becomes important later, Option 2 provides a clean upgrade path.
