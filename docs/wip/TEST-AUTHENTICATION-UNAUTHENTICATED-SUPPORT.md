# Proposal: Support for Unauthenticated Requests in Test Infrastructure

## Problem Statement

The current test authentication infrastructure (`TestAuthenticationHandler`) always creates an authenticated user, even when no authentication headers are present. This prevents testing of unauthenticated scenarios where endpoints should return HTTP 401 Unauthorized.

### Current Behavior

When creating an unauthenticated client with `_factory.CreateClient()`, the `TestAuthenticationHandler`:
1. Defaults to `userId = "test-user-id"` and `userName = "test-user"` (lines 65-66 in TestAuthenticationHandler.cs)
2. Always creates claims and returns `AuthenticateResult.Success()`
3. Makes all requests appear authenticated

### Impact

Three test scenarios in `TenantControllerTests.cs` are marked as `[Explicit]`:
- `GetTenants_Unauthenticated_Returns401`
- `GetTenant_Unauthenticated_Returns401`
- `CreateTenant_Unauthenticated_Returns401`

These tests cannot verify that `[Authorize]` attribute properly blocks unauthenticated access.

## Proposed Solution

### Option 1: Header-Based Authentication Flag (Recommended)

Modify `TestAuthenticationHandler` to check for the presence of authentication headers before creating an authenticated user.

#### Implementation

**In `TestAuthenticationHandler.HandleAuthenticateAsync()`:**

```csharp
protected override Task<AuthenticateResult> HandleAuthenticateAsync()
{
    // Check if authentication headers are present
    var userIdHeader = Context.Request.Headers["X-Test-User-Id"].FirstOrDefault();

    // If no authentication headers present, return NoResult (unauthenticated)
    if (string.IsNullOrEmpty(userIdHeader))
    {
        return Task.FromResult(AuthenticateResult.NoResult());
    }

    // Read test user data from custom headers
    var userId = userIdHeader;
    var userName = Context.Request.Headers["X-Test-User-Name"].FirstOrDefault() ?? "test-user";

    // ... rest of existing code to create authenticated user
}
```

#### Benefits
- ✅ Minimal code changes
- ✅ Backward compatible (all existing tests continue to work)
- ✅ Clear intent (no headers = unauthenticated)
- ✅ Consistent with existing header-based approach

#### Testing
```csharp
// Unauthenticated request (no headers added)
using var unauthClient = _factory.CreateClient();
var response = await unauthClient.GetAsync("/api/tenant");
// Expected: 401 Unauthorized

// Authenticated request (headers added via CreateAuthenticatedClient)
var authClient = _factory.CreateAuthenticatedClient(userId, tenantKey, role);
var response = await authClient.GetAsync("/api/tenant");
// Expected: 200 OK (or appropriate authenticated response)
```

### Option 2: Explicit Unauthenticated Client Method

Add a new method to `BaseTestWebApplicationFactory` that creates a client configured to fail authentication.

#### Implementation

**In `BaseTestWebApplicationFactory`:**

```csharp
/// <summary>
/// Creates an HTTP client that will fail authentication (for testing [Authorize] attribute)
/// </summary>
public HttpClient CreateUnauthenticatedClient()
{
    var handler = new UnauthenticatedHandler();
    return CreateDefaultClient(handler);
}

private class UnauthenticatedHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Add a special header to signal unauthenticated mode
        request.Headers.Add("X-Test-Unauthenticated", "true");
        return base.SendAsync(request, cancellationToken);
    }
}
```

**In `TestAuthenticationHandler`:**

```csharp
protected override Task<AuthenticateResult> HandleAuthenticateAsync()
{
    // Check for explicit unauthenticated flag
    if (Context.Request.Headers["X-Test-Unauthenticated"].Any())
    {
        return Task.FromResult(AuthenticateResult.NoResult());
    }

    // ... rest of existing code
}
```

#### Benefits
- ✅ Explicit intent in test code
- ✅ No risk of accidentally creating unauthenticated clients
- ✅ Backward compatible

#### Drawbacks
- ❌ Requires adding new method
- ❌ More verbose in tests

### Option 3: Anonymous Authentication Scheme

Register a separate "Anonymous" authentication scheme that always fails authentication.

#### Implementation

**In `BaseTestWebApplicationFactory.ConfigureTestServices()`:**

```csharp
builder.ConfigureTestServices(services =>
{
    services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
        options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
        options.DefaultScheme = TestAuthenticationHandler.SchemeName;
    })
    .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
        TestAuthenticationHandler.SchemeName,
        options => { })
    .AddScheme<AuthenticationSchemeOptions, AnonymousAuthenticationHandler>(
        "Anonymous",
        options => { });
});

public HttpClient CreateUnauthenticatedClient()
{
    var handler = new AnonymousSchemeHandler();
    return CreateDefaultClient(handler);
}

private class AnonymousSchemeHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // This client uses Anonymous scheme which always fails
        return await base.SendAsync(request, cancellationToken);
    }
}

private class AnonymousAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        return Task.FromResult(AuthenticateResult.NoResult());
    }
}
```

#### Benefits
- ✅ Clean separation of concerns
- ✅ Follows ASP.NET Core authentication patterns

#### Drawbacks
- ❌ More complex implementation
- ❌ Requires multiple authentication schemes

## Recommendation

**Option 1: Header-Based Authentication Flag** is recommended because:

1. **Simplest implementation** - Single point of change in `TestAuthenticationHandler`
2. **Backward compatible** - All existing tests continue to work without modification
3. **Consistent** - Aligns with existing header-based authentication approach
4. **Clear semantics** - No headers = unauthenticated (natural and intuitive)

## Implementation Plan

1. **Update `TestAuthenticationHandler.HandleAuthenticateAsync()`**
   - Check if `X-Test-User-Id` header is present
   - Return `AuthenticateResult.NoResult()` if not present
   - Otherwise, proceed with existing authentication logic

2. **Update tests in `TenantControllerTests.cs`**
   - Remove `[Explicit]` attributes from unauthenticated tests
   - Verify tests pass with `dotnet test`

3. **Document behavior**
   - Update `TestHelpers/README.md` to explain unauthenticated client behavior
   - Add example showing unauthenticated vs authenticated requests

## Testing Plan

### Unit Tests for TestAuthenticationHandler
```csharp
[Test]
public async Task HandleAuthenticateAsync_NoHeaders_ReturnsNoResult()
{
    // Given: A request without authentication headers
    var context = CreateHttpContext(withHeaders: false);

    // When: Handler processes authentication
    var result = await handler.AuthenticateAsync();

    // Then: Should return NoResult (unauthenticated)
    Assert.That(result.None, Is.True);
}

[Test]
public async Task HandleAuthenticateAsync_WithHeaders_ReturnsSuccess()
{
    // Given: A request with authentication headers
    var context = CreateHttpContext(withHeaders: true);

    // When: Handler processes authentication
    var result = await handler.AuthenticateAsync();

    // Then: Should return Success (authenticated)
    Assert.That(result.Succeeded, Is.True);
}
```

### Integration Tests
Enable the three explicit tests in `TenantControllerTests.cs` and verify they pass.

## Migration Path

1. **Phase 1**: Implement Option 1 in `TestAuthenticationHandler`
2. **Phase 2**: Remove `[Explicit]` attributes from unauthenticated tests
3. **Phase 3**: Run full test suite to ensure no regressions
4. **Phase 4**: Update documentation

## Risk Assessment

**Low Risk** - The change is minimal and backward compatible:
- Existing tests continue to work (they all use `CreateAuthenticatedClient()` which adds headers)
- New behavior only affects clients created with `CreateClient()` (no headers)
- Easy to revert if issues arise

## Alternative Considerations

If header-based approach proves insufficient, can escalate to Option 2 or Option 3, but current assessment suggests Option 1 is adequate for the use case.
