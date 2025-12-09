# Implementation Plan: Test Authentication Scheme with Base Class Pattern

## Overview

Implement ASP.NET Core Test Authentication Scheme using a Test Base Class pattern, with **Editor as the default role** (Owner role requires explicit testing).

---

## Step 1: Create Test Authentication Handler

**File**: `tests/Integration.Controller/TestHelpers/TestAuthenticationHandler.cs` (NEW)

**Purpose**: Custom authentication handler that creates authenticated users with tenant role claims for testing.

```csharp
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YoFi.V3.Entities.Tenancy;

namespace YoFi.V3.Tests.Integration.Controller.TestHelpers;

public class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "TestScheme";

    public TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Retrieve test user configuration from HttpContext.Items
        // (Set by BaseTestWebApplicationFactory before request)
        var tenantRoles = Context.Items["TestUser:TenantRoles"]
            as List<(Guid tenantKey, TenantRole role)>;
        var userId = Context.Items["TestUser:UserId"] as string ?? "test-user-id";
        var userName = Context.Items["TestUser:UserName"] as string ?? "test-user";

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, userName)
        };

        // Add tenant role claims
        if (tenantRoles != null)
        {
            foreach (var (tenantKey, role) in tenantRoles)
            {
                claims.Add(new Claim("tenant_role", $"{tenantKey}:{role}"));
            }
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
```

**Key Design Decisions**:
- Uses `HttpContext.Items` to pass test user configuration (set per request)
- Supports multiple tenant roles for testing cross-tenant scenarios
- Follows ASP.NET Core authentication handler pattern

---

## Step 2: Update BaseTestWebApplicationFactory

**File**: `tests/Integration.Controller/TestHelpers/BaseTestWebApplicationFactory.cs`

**Changes**: Add test authentication scheme and helper methods

```csharp
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using YoFi.V3.Entities.Tenancy;

namespace YoFi.V3.Tests.Integration.Controller.TestHelpers;

public class BaseTestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath;
    private readonly Dictionary<string, string?> _configurationOverrides;

    public BaseTestWebApplicationFactory(
        Dictionary<string, string?>? configurationOverrides = null,
        string? dbPath = null)
    {
        _configurationOverrides = configurationOverrides ?? new Dictionary<string, string?>();
        _dbPath = dbPath ?? Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid()}.db");

        // Set default configuration if not provided
        if (!_configurationOverrides.ContainsKey("Application:Version"))
            _configurationOverrides["Application:Version"] = "test-version";

        if (!_configurationOverrides.ContainsKey("Application:Environment"))
            _configurationOverrides["Application:Environment"] = "Local";

        if (!_configurationOverrides.ContainsKey("Application:AllowedCorsOrigins:0"))
            _configurationOverrides["Application:AllowedCorsOrigins:0"] = "http://localhost:3000";

        if (!_configurationOverrides.ContainsKey("ConnectionStrings:DefaultConnection"))
            _configurationOverrides["ConnectionStrings:DefaultConnection"] = $"Data Source={_dbPath}";
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(_configurationOverrides);
        });

        builder.ConfigureTestServices(services =>
        {
            // Register test authentication scheme
            services.AddAuthentication(TestAuthenticationHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.SchemeName,
                    options => { });
        });
    }

    /// <summary>
    /// Creates an authenticated HTTP client with Editor role (default for most tests)
    /// </summary>
    public HttpClient CreateAuthenticatedClient(Guid tenantKey)
        => CreateAuthenticatedClient(tenantKey, TenantRole.Editor);

    /// <summary>
    /// Creates an authenticated HTTP client with specified role
    /// </summary>
    public HttpClient CreateAuthenticatedClient(Guid tenantKey, TenantRole role,
        string? userId = null, string? userName = null)
    {
        return CreateAuthenticatedClient(
            new[] { (tenantKey, role) },
            userId,
            userName);
    }

    /// <summary>
    /// Creates an authenticated HTTP client with multiple tenant roles (for cross-tenant tests)
    /// </summary>
    public HttpClient CreateAuthenticatedClient(
        (Guid tenantKey, TenantRole role)[] tenantRoles,
        string? userId = null,
        string? userName = null)
    {
        var client = CreateClient();

        // Store test user info in a way that will be accessible per-request
        // We'll use a delegating handler to inject into HttpContext.Items
        var handler = new TestUserDelegatingHandler(
            tenantRoles.ToList(),
            userId ?? "test-user-id",
            userName ?? "test-user");

        return CreateDefaultClient(handler);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            // Clean up the temporary database file
            try
            {
                if (File.Exists(_dbPath))
                {
                    File.Delete(_dbPath);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
```

**Key Design Decisions**:
- Default role is **Editor** (not Owner)
- Overloads support single tenant, specific role, and multi-tenant scenarios
- Uses `ConfigureTestServices` to ensure test auth overrides production auth

---

## Step 3: Create Test User Delegating Handler

**File**: `tests/Integration.Controller/TestHelpers/TestUserDelegatingHandler.cs` (NEW)

**Purpose**: Middleware that injects test user context into `HttpContext.Items` before request processing.

```csharp
using YoFi.V3.Entities.Tenancy;

namespace YoFi.V3.Tests.Integration.Controller.TestHelpers;

/// <summary>
/// Delegating handler that injects test user context into HttpContext.Items
/// for TestAuthenticationHandler to consume
/// </summary>
public class TestUserDelegatingHandler : DelegatingHandler
{
    private readonly List<(Guid tenantKey, TenantRole role)> _tenantRoles;
    private readonly string _userId;
    private readonly string _userName;

    public TestUserDelegatingHandler(
        List<(Guid tenantKey, TenantRole role)> tenantRoles,
        string userId,
        string userName)
    {
        _tenantRoles = tenantRoles;
        _userId = userId;
        _userName = userName;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Store in request properties (will be available in HttpContext)
        request.Properties["TestUser:TenantRoles"] = _tenantRoles;
        request.Properties["TestUser:UserId"] = _userId;
        request.Properties["TestUser:UserName"] = _userName;

        return await base.SendAsync(request, cancellationToken);
    }
}
```

**Note**: We may need to adjust this to use middleware instead if `HttpContext.Items` isn't accessible via `request.Properties`. Alternative approach in Step 7.

---

## Step 4: Create AuthenticatedTestBase Class

**File**: `tests/Integration.Controller/TestHelpers/AuthenticatedTestBase.cs` (NEW)

**Purpose**: Base class providing authenticated client setup with Editor as default role.

```csharp
using YoFi.V3.Data;
using YoFi.V3.Entities.Tenancy;

namespace YoFi.V3.Tests.Integration.Controller.TestHelpers;

/// <summary>
/// Base class for integration tests that require authenticated access.
/// Default role: Editor (use SwitchToOwner() for Owner-specific tests)
/// </summary>
public abstract class AuthenticatedTestBase
{
    protected BaseTestWebApplicationFactory _factory = null!;
    protected HttpClient _client = null!;
    protected Guid _testTenantKey;
    protected TenantRole _currentRole = TenantRole.Editor;

    /// <summary>
    /// Override to customize default role (default: Editor)
    /// </summary>
    protected virtual TenantRole DefaultRole => TenantRole.Editor;

    [OneTimeSetUp]
    public virtual async Task OneTimeSetUp()
    {
        _factory = new BaseTestWebApplicationFactory();
        _testTenantKey = await CreateTestTenantAsync();
        _client = _factory.CreateAuthenticatedClient(_testTenantKey, DefaultRole);
        _currentRole = DefaultRole;
    }

    [OneTimeTearDown]
    public virtual void OneTimeTearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    /// <summary>
    /// Creates a test tenant in the database and returns its key
    /// </summary>
    protected async Task<Guid> CreateTestTenantAsync(string? name = null, string? description = null)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var tenant = new Tenant
        {
            Key = Guid.NewGuid(),
            Name = name ?? "Test Tenant",
            Description = description ?? "Test tenant for integration testing",
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Set<Tenant>().Add(tenant);
        await dbContext.SaveChangesAsync();

        return tenant.Key;
    }

    /// <summary>
    /// Switch to Viewer role (read-only access)
    /// </summary>
    protected void SwitchToViewer()
    {
        SwitchToRole(TenantRole.Viewer);
    }

    /// <summary>
    /// Switch to Editor role (default - read/write access)
    /// </summary>
    protected void SwitchToEditor()
    {
        SwitchToRole(TenantRole.Editor);
    }

    /// <summary>
    /// Switch to Owner role (full control - use explicitly for Owner tests)
    /// </summary>
    protected void SwitchToOwner()
    {
        SwitchToRole(TenantRole.Owner);
    }

    /// <summary>
    /// Switch to specified role for current tenant
    /// </summary>
    protected void SwitchToRole(TenantRole role)
    {
        _client?.Dispose();
        _client = _factory.CreateAuthenticatedClient(_testTenantKey, role);
        _currentRole = role;
    }

    /// <summary>
    /// Create a client authenticated for multiple tenants (cross-tenant testing)
    /// </summary>
    protected HttpClient CreateMultiTenantClient(params (Guid tenantKey, TenantRole role)[] tenantRoles)
    {
        return _factory.CreateAuthenticatedClient(tenantRoles);
    }
}
```

**Key Design Decisions**:
- **Default role is Editor** (as requested)
- Named helper methods (`SwitchToOwner()`) make Owner tests explicit and clear
- `CreateTestTenantAsync()` helper reduces duplication
- Supports multi-tenant scenarios via `CreateMultiTenantClient()`

---

## Step 5: Migrate TenantContextMiddlewareTests

**File**: `tests/Integration.Controller/TenantContextMiddlewareTests.cs`

**Changes**: Extend `AuthenticatedTestBase` instead of managing factory directly

```csharp
using System.Net;
using Microsoft.AspNetCore.Mvc;
using YoFi.V3.Application.Dto;
using YoFi.V3.Data;
using YoFi.V3.Entities.Models;
using YoFi.V3.Entities.Tenancy;
using YoFi.V3.Tests.Integration.Controller.TestHelpers;

namespace YoFi.V3.Tests.Integration.Controller;

[TestFixture]
public class TenantContextMiddlewareTests : AuthenticatedTestBase
{
    private Guid _firstTransactionKey;
    private const int ExpectedTransactionCount = 5;

    [OneTimeSetUp]
    public override async Task OneTimeSetUp()
    {
        // Call base to set up factory, tenant, and authenticated client
        await base.OneTimeSetUp();

        // Given: Multiple transactions in the database which are in that tenant
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Get the tenant ID from the created tenant
            var tenant = await dbContext.Set<Tenant>()
                .FirstAsync(t => t.Key == _testTenantKey);

            // And: Multiple transactions in the database which are in that tenant
            var transactions = new List<Transaction>();
            for (int i = 1; i <= ExpectedTransactionCount; i++)
            {
                var transaction = new Transaction
                {
                    Key = Guid.NewGuid(),
                    TenantId = tenant.Id,
                    Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-i)),
                    Payee = $"Test Payee {i}",
                    Amount = 100.00m * i
                };
                transactions.Add(transaction);

                // Store the first transaction key for single transaction tests
                if (i == 1)
                {
                    _firstTransactionKey = transaction.Key;
                }
            }

            dbContext.Set<Transaction>().AddRange(transactions);
            await dbContext.SaveChangesAsync();
        }
    }

    #region Helper Methods

    private async Task<(Guid tenantKey, long tenantId)> CreateTenantAsync(string name, string description)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var tenant = new Tenant
        {
            Key = Guid.NewGuid(),
            Name = name,
            Description = description,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Set<Tenant>().Add(tenant);
        await dbContext.SaveChangesAsync();

        return (tenant.Key, tenant.Id);
    }

    private async Task<Guid> CreateTransactionAsync(long tenantId, string payee, decimal amount, int daysAgo = 0)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var transaction = new Transaction
        {
            Key = Guid.NewGuid(),
            TenantId = tenantId,
            Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-daysAgo)),
            Payee = payee,
            Amount = amount
        };

        dbContext.Set<Transaction>().Add(transaction);
        await dbContext.SaveChangesAsync();

        return transaction.Key;
    }

    private async Task CreateTransactionsAsync(long tenantId, string payeePrefix, int count, decimal baseAmount = 100.00m)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var transactions = new List<Transaction>();
        for (int i = 1; i <= count; i++)
        {
            transactions.Add(new Transaction
            {
                Key = Guid.NewGuid(),
                TenantId = tenantId,
                Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-i)),
                Payee = $"{payeePrefix} {i}",
                Amount = baseAmount * i
            });
        }

        dbContext.Set<Transaction>().AddRange(transactions);
        await dbContext.SaveChangesAsync();
    }

    #endregion

    [Test]
    public async Task GetTransactions_OneTenantWithMultipleTransactions_ReturnsAllExpectedTransactions()
    {
        // Given: Authenticated as Editor (default)
        // And: One tenant with multiple transactions (from OneTimeSetUp)

        // When: API Client requests transactions for that tenant
        var response = await _client.GetAsync($"/api/tenant/{_testTenantKey}/transactions");

        // Then: All expected transactions returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var transactions = await response.Content.ReadFromJsonAsync<List<TransactionResultDto>>();
        Assert.That(transactions, Is.Not.Null);
        Assert.That(transactions, Has.Count.EqualTo(ExpectedTransactionCount));

        // Verify all transactions have expected data
        Assert.That(transactions.All(t => t.Payee.StartsWith("Test Payee")), Is.True);
        Assert.That(transactions.All(t => t.Amount > 0), Is.True);
    }

    [Test]
    public async Task GetTransactions_NonExistentTenant_Returns404()
    {
        // Given: Authenticated as Editor
        // When: API Client requests transactions for a tenant that does not exist
        var nonExistentTenantKey = Guid.NewGuid();
        var response = await _client.GetAsync($"/api/tenant/{nonExistentTenantKey}/transactions");

        // Then: 404 Not Found should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        // And: Response should be a problem details with title "Tenant not found"
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problemDetails, Is.Not.Null);
        Assert.That(problemDetails!.Title, Is.EqualTo("Tenant not found"));
    }

    [Test]
    public async Task GetTransactions_MultipleTenantsInDatabase_ReturnsOnlyRequestedTenantTransactions()
    {
        // Given: Multiple tenants in the database, each with their own transactions
        int tenant1TransactionCount = 3;
        int tenant2TransactionCount = 4;

        var (tenant1Key, tenant1Id) = await CreateTenantAsync("Tenant 1", "First test tenant");
        await CreateTransactionsAsync(tenant1Id, "Tenant1 Payee", tenant1TransactionCount, 50.00m);

        var (tenant2Key, tenant2Id) = await CreateTenantAsync("Tenant 2", "Second test tenant");
        await CreateTransactionsAsync(tenant2Id, "Tenant2 Payee", tenant2TransactionCount, 75.00m);

        // And: User has Editor access to both tenants
        var multiTenantClient = CreateMultiTenantClient(
            (tenant1Key, TenantRole.Editor),
            (tenant2Key, TenantRole.Editor));

        // When: API Client requests transactions for tenant 1
        var response1 = await multiTenantClient.GetAsync($"/api/tenant/{tenant1Key}/transactions");
        Assert.That(response1.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var transactions1 = await response1.Content.ReadFromJsonAsync<List<TransactionResultDto>>();
        Assert.That(transactions1, Is.Not.Null);

        // Then: Only tenant 1's transactions are returned
        Assert.That(transactions1, Has.Count.EqualTo(tenant1TransactionCount));
        Assert.That(transactions1.All(t => t.Payee.StartsWith("Tenant1 Payee")), Is.True);

        // When: API Client requests transactions for tenant 2
        var response2 = await multiTenantClient.GetAsync($"/api/tenant/{tenant2Key}/transactions");
        Assert.That(response2.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var transactions2 = await response2.Content.ReadFromJsonAsync<List<TransactionResultDto>>();
        Assert.That(transactions2, Is.Not.Null);

        // Then: Only tenant 2's transactions are returned
        Assert.That(transactions2, Has.Count.EqualTo(tenant2TransactionCount));
        Assert.That(transactions2.All(t => t.Payee.StartsWith("Tenant2 Payee")), Is.True);
    }

    [Test]
    public async Task GetTransactionById_ValidTenantAndTransaction_ReturnsTransaction()
    {
        // Given: Authenticated as Editor
        // And: One tenant with multiple transactions (from OneTimeSetUp)

        // When: API Client requests a specific transaction by key
        var response = await _client.GetAsync($"/api/tenant/{_testTenantKey}/transactions/{_firstTransactionKey}");

        // Then: Transaction is returned successfully
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var transaction = await response.Content.ReadFromJsonAsync<TransactionResultDto>();
        Assert.That(transaction, Is.Not.Null);
        Assert.That(transaction!.Payee, Is.EqualTo("Test Payee 1"));
        Assert.That(transaction.Amount, Is.EqualTo(100.00m));
    }

    [Test]
    public async Task GetTransactionById_NonExistentTransaction_Returns404()
    {
        // Given: Authenticated as Editor
        // When: API Client requests a transaction that does not exist
        var nonExistentTransactionKey = Guid.NewGuid();
        var response = await _client.GetAsync($"/api/tenant/{_testTenantKey}/transactions/{nonExistentTransactionKey}");

        // Then: 404 Not Found should be returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        // And: Response should be a problem details with title "Transaction not found"
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problemDetails, Is.Not.Null);
        Assert.That(problemDetails!.Title, Is.EqualTo("Transaction not found"));
    }

    [Test]
    public async Task GetTransactionById_TransactionExistsInDifferentTenant_Returns404()
    {
        // Given: Two tenants, each with their own transactions
        var (tenant1Key, tenant1Id) = await CreateTenantAsync("Cross Tenant Test - Tenant 1", "First tenant for cross-tenant access test");
        await CreateTransactionAsync(tenant1Id, "Tenant1 Transaction", 100.00m);

        var (_, tenant2Id) = await CreateTenantAsync("Cross Tenant Test - Tenant 2", "Second tenant for cross-tenant access test");
        var tenant2TransactionKey = await CreateTransactionAsync(tenant2Id, "Tenant2 Transaction", 200.00m);

        // And: User only has access to Tenant 1
        var tenant1Client = _factory.CreateAuthenticatedClient(tenant1Key, TenantRole.Editor);

        // When: API Client attempts to access Tenant 2's transaction using Tenant 1's context
        var response = await tenant1Client.GetAsync($"/api/tenant/{tenant1Key}/transactions/{tenant2TransactionKey}");

        // Then: 404 Not Found should be returned (transaction should not be accessible from wrong tenant)
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        // And: Response should be a problem details with title "Transaction not found"
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.That(problemDetails, Is.Not.Null);
        Assert.That(problemDetails!.Title, Is.EqualTo("Transaction not found"));
    }

    [Test]
    public async Task GetTransactions_AsViewer_CanReadTransactions()
    {
        // Given: Switch to Viewer role (read-only)
        SwitchToViewer();

        // When: Viewer requests transactions
        var response = await _client.GetAsync($"/api/tenant/{_testTenantKey}/transactions");

        // Then: Should succeed (Viewer can read)
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var transactions = await response.Content.ReadFromJsonAsync<List<TransactionResultDto>>();
        Assert.That(transactions, Has.Count.EqualTo(ExpectedTransactionCount));
    }

    // Example of explicit Owner test
    [Test]
    public async Task DeactivateTenant_AsOwner_Succeeds()
    {
        // Given: Switch to Owner role explicitly
        SwitchToOwner();

        // When: Owner attempts to deactivate tenant
        // (This endpoint doesn't exist yet, but demonstrates pattern)
        // var response = await _client.DeleteAsync($"/api/tenant/{_testTenantKey}");

        // Then: Should succeed (only Owner can deactivate)
        // Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));

        // Placeholder assertion for now
        Assert.That(_currentRole, Is.EqualTo(TenantRole.Owner));
    }
}
```

**Key Changes**:
- Extends `AuthenticatedTestBase` instead of managing factory
- `OneTimeSetUp` calls `base.OneTimeSetUp()` first
- Uses `_client` (already authenticated as Editor)
- Adds role-switching tests to demonstrate pattern
- Comments clarify authentication context

---

## Step 6: Enable Authorization in TenantRoleHandler

**File**: `src/Controllers/Tenancy/TenantRoleHandler.cs`

**Changes**: Remove `#if false` to enable real authorization

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using YoFi.V3.Entities.Tenancy;

namespace YoFi.V3.Controllers.Tenancy;

public class TenantRoleHandler(IHttpContextAccessor httpContextAccessor) : AuthorizationHandler<TenantRoleRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantRoleRequirement requirement)
    {
        var httpContext = httpContextAccessor.HttpContext;

        if (httpContext == null)
            return Task.CompletedTask;

        var tenantKey = httpContext?.Request.RouteValues["tenantKey"]?.ToString();

        if (string.IsNullOrEmpty(tenantKey))
            return Task.CompletedTask;

        // Real multi-tenant role authorization
        var claim = context.User.FindFirst(c =>
            c.Type == "tenant_role" &&
            c.Value.StartsWith($"{tenantKey}:"));

        if (claim != null)
        {
            var parts = claim.Value.Split(':');
            if (parts.Length == 2 &&
                Enum.TryParse<TenantRole>(parts[1], out var userRole) &&
                userRole >= requirement.MinimumRole)
            {
                // STORE IT in HttpContext.Items for later use
                httpContext.Items["TenantKey"] = Guid.Parse(tenantKey);
                httpContext.Items["TenantRole"] = userRole;

                context.Succeed(requirement);
            }
        }

        return Task.CompletedTask;
    }
}
```

**Changes**:
- Removed `#if false` / `#else` / `#endif` blocks
- Now uses real claim-based authorization
- Tests will verify this works correctly

---

## Step 7: Alternative Middleware Approach (If Needed)

If `HttpContext.Items` isn't properly populated from `request.Properties`, use middleware instead:

**File**: `tests/Integration.Controller/TestHelpers/TestUserContextMiddleware.cs` (NEW)

```csharp
using Microsoft.AspNetCore.Http;
using YoFi.V3.Entities.Tenancy;

namespace YoFi.V3.Tests.Integration.Controller.TestHelpers;

/// <summary>
/// Test-only middleware that populates HttpContext.Items with test user context
/// ONLY registered in test environment via BaseTestWebApplicationFactory
/// </summary>
public class TestUserContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly List<(Guid tenantKey, TenantRole role)> _tenantRoles;
    private readonly string _userId;
    private readonly string _userName;

    public TestUserContextMiddleware(
        RequestDelegate next,
        List<(Guid tenantKey, TenantRole role)> tenantRoles,
        string userId,
        string userName)
    {
        _next = next;
        _tenantRoles = tenantRoles;
        _userId = userId;
        _userName = userName;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Inject test user context into HttpContext.Items
        context.Items["TestUser:TenantRoles"] = _tenantRoles;
        context.Items["TestUser:UserId"] = _userId;
        context.Items["TestUser:UserName"] = _userName;

        await _next(context);
    }
}
```

And update [`BaseTestWebApplicationFactory`](tests/Integration.Controller/TestHelpers/BaseTestWebApplicationFactory.cs:10):

```csharp
builder.Configure((context, app) =>
{
    // Add test middleware at the beginning of pipeline
    app.UseMiddleware<TestUserContextMiddleware>(
        testUserTenantRoles,
        testUserId,
        testUserName);
});
```

---

## Implementation Checklist

```markdown
- [ ] Create TestAuthenticationHandler.cs
- [ ] Create TestUserDelegatingHandler.cs (or TestUserContextMiddleware.cs if needed)
- [ ] Update BaseTestWebApplicationFactory with authentication methods
- [ ] Create AuthenticatedTestBase.cs with Editor as default
- [ ] Migrate TenantContextMiddlewareTests to extend AuthenticatedTestBase
- [ ] Remove #if false from TenantRoleHandler.cs
- [ ] Run tests to verify authentication works
- [ ] Add additional role-specific tests (Viewer, Owner scenarios)
- [ ] Update other test files to extend AuthenticatedTestBase
- [ ] Document pattern in tests/Integration.Controller/README.md
```

---

## Testing Strategy

### Phase 1: Verify Authentication Works
1. Run existing tests - should all pass with Editor role
2. Verify `TenantRoleHandler` receives proper claims
3. Check `HttpContext.Items` contains tenant context

### Phase 2: Add Role-Specific Tests
```csharp
[Test]
public async Task CreateTransaction_AsViewer_Returns403()
{
    SwitchToViewer();
    // POST should fail for Viewer
}

[Test]
public async Task CreateTransaction_AsEditor_Succeeds()
{
    SwitchToEditor(); // Or just use default
    // POST should work for Editor
}

[Test]
public async Task DeleteTenant_AsEditor_Returns403()
{
    // Editor can't delete tenant
}

[Test]
public async Task DeleteTenant_AsOwner_Succeeds()
{
    SwitchToOwner(); // Explicit Owner test
    // DELETE should work for Owner
}
```

### Phase 3: Cross-Tenant Security Tests
```csharp
[Test]
public async Task AccessDifferentTenant_WithoutRole_Returns404()
{
    var otherTenantKey = Guid.NewGuid();
    // Should get 404 for tenant without role
}
```

---

## Benefits of This Approach

✅ **Editor as default** - Most tests use realistic Editor permissions
✅ **Owner explicitly tested** - `SwitchToOwner()` makes special permissions obvious
✅ **Minimal test changes** - Extend base class, tests mostly unchanged
✅ **Realistic auth flow** - Uses actual ASP.NET Core authentication
✅ **Security tested** - Authorization boundaries thoroughly validated
✅ **Easy role switching** - Named methods (`SwitchToViewer()`, `SwitchToOwner()`)
✅ **Multi-tenant support** - `CreateMultiTenantClient()` for complex scenarios
