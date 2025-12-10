# Integration Controller Tests

This project contains integration tests for API controllers using `WebApplicationFactory` to test the full HTTP request pipeline with **test authentication support for tenant-based authorization**.

## Overview

Integration tests verify the complete request/response cycle including:
- **Test authentication** (bypasses production JWT authentication)
- **Tenant-based authorization** (validates tenant role claims)
- Middleware pipeline execution
- Routing
- Controller action execution
- Response serialization
- HTTP status codes

## Test Infrastructure

### Test Authentication Architecture

The test infrastructure provides a complete authentication system that mimics production authentication while allowing programmatic control for testing:

#### Components

1. **[`BaseTestWebApplicationFactory`](TestHelpers/BaseTestWebApplicationFactory.cs)** - Factory that configures test authentication
   - Replaces production authentication (ASP.NET Identity + NuxtIdentity) with test scheme
   - Provides `CreateAuthenticatedClient()` methods for creating authenticated HTTP clients
   - Manages temporary SQLite database (auto-created, auto-cleaned)

2. **[`TestAuthenticationHandler`](TestHelpers/TestAuthenticationHandler.cs)** - Custom authentication handler
   - Reads test user data from HTTP headers (`X-Test-User-Id`, `X-Test-User-Name`, `X-Test-Tenant-Roles`)
   - Creates claims including `tenant_role` claims for multi-tenancy
   - Registered as the default authentication scheme for tests

3. **[`AuthenticatedTestBase`](TestHelpers/AuthenticatedTestBase.cs)** - Base class for authenticated tests
   - Default role: **Editor** (most common permission level)
   - Provides `SwitchToViewer()`, `SwitchToEditor()`, `SwitchToOwner()` methods
   - Includes `CreateTestTenantAsync()` helper
   - Supports multi-tenant testing via `CreateMultiTenantClient()`

#### Authentication Flow

```
1. Test creates authenticated client → factory.CreateAuthenticatedClient(tenantKey, TenantRole.Editor)
2. Delegating handler adds HTTP headers → X-Test-Tenant-Roles: "guid:Editor"
3. Authentication middleware invokes TestAuthenticationHandler
4. Handler reads headers and creates claims → tenant_role: "guid:Editor"
5. Authorization middleware invokes TenantRoleHandler
6. Handler validates user has required tenant role
7. Request proceeds with authenticated user
```

### WebApplicationFactory

The tests use `WebApplicationFactory<Program>` which:
- Hosts the entire application in-memory
- Provides an `HttpClient` for making real HTTP requests
- Runs the full middleware pipeline (including authentication/authorization)
- Uses the actual configuration and dependencies

### Injecting Configuration for Testing

You can override application configuration to inject specific test values. The [`VersionControllerTests`](VersionControllerTests.cs) demonstrates this pattern:

```csharp
public class CustomVersionWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _version;
    private readonly EnvironmentType _environment;

    public CustomVersionWebApplicationFactory(string version, EnvironmentType environment)
    {
        _version = version;
        _environment = environment;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Add in-memory configuration to override settings
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Application:Version"] = _version,
                ["Application:Environment"] = _environment.ToString()
            });
        });
    }
}
```

This allows you to:
- **Inject specific values** for deterministic testing
- **Override any configuration** from appsettings.json
- **Test with different configurations** without changing files

### Example: Testing Version Endpoint

The version controller depends on [`ApplicationOptions`](../../src/Entities/Options/ApplicationOptions.cs) configuration. By injecting a test version, we can validate the exact response:

```csharp
[Test]
public async Task GetVersion_ReturnsConfiguredVersion()
{
    // Factory configured with version "1.2.3-test"
    var response = await _client.GetAsync("/version");
    var version = await response.Content.ReadFromJsonAsync<string>();

    // Assert we get exactly what we configured
    Assert.That(version, Does.Contain("1.2.3-test"));
}
```

## Running the Tests

```bash
# Run all integration controller tests
dotnet test tests/Integration.Controller

# Run specific test
dotnet test --filter "FullyQualifiedName~VersionControllerTests.GetVersion_ReturnsConfiguredVersion"

# Run with detailed output
dotnet test tests/Integration.Controller --logger "console;verbosity=detailed"
```

## Current Test Results

```
Test Run Successful.
Total tests: 4
     Passed: 4
 Total time: 2.2640 Seconds
```

## Project Dependencies

- `Microsoft.AspNetCore.Mvc.Testing` - WebApplicationFactory support
- `NUnit` - Test framework
- `YoFi.V3.BackEnd` - The application being tested

## Adding New Tests

### Authenticated Controller Test (Recommended)

For controllers that require tenant-based authorization (most controllers):

```csharp
[TestFixture]
public class MyControllerTests : AuthenticatedTestBase
{
    [OneTimeSetUp]
    public override async Task OneTimeSetUp()
    {
        // Call base to set up factory, tenant, and authenticated client
        await base.OneTimeSetUp();

        // Optional: Add test data
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        // ... seed data
    }

    [Test]
    public async Task GetData_AsEditor_ReturnsData()
    {
        // _client is already authenticated as Editor
        // _testTenantKey is already created
        var response = await _client.GetAsync($"/api/tenant/{_testTenantKey}/data");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task DeleteData_AsViewer_ReturnsForbidden()
    {
        SwitchToViewer(); // Change to read-only role

        var response = await _client.DeleteAsync($"/api/tenant/{_testTenantKey}/data/123");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task ManageTenant_AsOwner_Succeeds()
    {
        SwitchToOwner(); // Explicitly switch to Owner role

        var response = await _client.PutAsync($"/api/tenant/{_testTenantKey}/settings", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }
}
```

### Public Endpoint Test (No Authentication)

For controllers that don't require authentication:

```csharp
[TestFixture]
public class MyControllerTests
{
    private WebApplicationFactory<Program> _factory;
    private HttpClient _client;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task MyEndpoint_ReturnsExpectedResult()
    {
        var response = await _client.GetAsync("/my-endpoint");
        Assert.That(response.IsSuccessStatusCode, Is.True);
    }
}
```

### Multi-Tenant Test

For testing cross-tenant isolation:

```csharp
[Test]
public async Task GetData_DifferentTenants_ReturnsIsolatedData()
{
    // Create second tenant
    var tenant2Key = await CreateTestTenantAsync("Tenant 2");

    // Create client with access to both tenants
    var multiTenantClient = CreateMultiTenantClient(
        (_testTenantKey, TenantRole.Editor),
        (tenant2Key, TenantRole.Viewer)
    );

    // Verify each tenant gets only their data
    var response1 = await multiTenantClient.GetAsync($"/api/tenant/{_testTenantKey}/data");
    var response2 = await multiTenantClient.GetAsync($"/api/tenant/{tenant2Key}/data");

    // Assert responses are different and properly isolated
}
```

### Custom Configuration Test

For tests that need specific application configuration:

For controllers that need specific configuration:

```csharp
[TestFixture]
public class MyConfigurableControllerTests
{
    private CustomWebApplicationFactory _factory;
    private HttpClient _client;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new CustomWebApplicationFactory(
            configValue1: "test-value",
            configValue2: 42
        );
        _client = _factory.CreateClient();
    }

    [Test]
    public async Task Endpoint_UsesInjectedConfiguration()
    {
        var response = await _client.GetAsync("/my-endpoint");
        var result = await response.Content.ReadFromJsonAsync<MyDto>();

        Assert.That(result.ConfiguredValue, Is.EqualTo("test-value"));
    }
}
```

## Advanced Customization Patterns

### Override Services

Replace specific services for testing:

```csharp
protected override void ConfigureWebHost(IWebHostBuilder builder)
{
    builder.ConfigureServices(services =>
    {
        // Remove existing service
        var descriptor = services.SingleOrDefault(
            d => d.ServiceType == typeof(IMyService));
        if (descriptor != null)
            services.Remove(descriptor);

        // Add mock or test implementation
        services.AddScoped<IMyService, MockMyService>();
    });
}
```

### Override Database

Use in-memory or test database:

```csharp
protected override void ConfigureWebHost(IWebHostBuilder builder)
{
    builder.ConfigureServices(services =>
    {
        // Remove existing DbContext
        var descriptor = services.SingleOrDefault(
            d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
        if (descriptor != null)
            services.Remove(descriptor);

        // Add in-memory database
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseInMemoryDatabase("TestDb");
        });
    });
}
```

### Add Test Middleware

Insert middleware for testing:

```csharp
protected override void ConfigureWebHost(IWebHostBuilder builder)
{
    builder.ConfigureServices(services =>
    {
        services.AddSingleton<IStartupFilter, TestStartupFilter>();
    });
}

public class TestStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.UseMiddleware<TestMiddleware>();
            next(app);
        };
    }
}
```

## Best Practices

### General Testing
1. **Use OneTimeSetUp/OneTimeTearDown** for factory and client creation (faster)
2. **Inject configuration** instead of changing appsettings files
3. **Test HTTP behavior** - status codes, headers, content types
4. **Test actual responses** - deserialize JSON and verify structure
5. **Keep tests independent** - each test should work in isolation
6. **Clean up resources** - dispose factory and client properly
7. **Use const for test data** - makes tests more maintainable
8. **Test one thing per test** - focused tests are easier to debug

### Authentication & Authorization Testing
1. **Extend AuthenticatedTestBase** for tenant-protected endpoints
2. **Use descriptive role switching** - `SwitchToOwner()` makes intent clear
3. **Default to Editor role** - matches most common use case
4. **Test authorization boundaries** - verify each role's permissions
5. **Test cross-tenant isolation** - ensure data doesn't leak between tenants
6. **Use Gherkin comments** - Given/When/Then for test documentation (see [project rules](../../.roorules))

## Configuration Override Examples

### Application Settings
```csharp
config.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["Application:Version"] = "1.0.0-test",
    ["Application:Environment"] = "Local"
});
```

### Connection Strings
```csharp
config.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["ConnectionStrings:DefaultConnection"] = "Server=testserver;Database=testdb"
});
```

### Feature Flags
```csharp
config.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["Features:NewFeature"] = "true",
    ["Features:BetaFeature"] = "false"
});
```

### Nested Configuration
```csharp
config.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["Logging:LogLevel:Default"] = "Debug",
    ["Logging:LogLevel:Microsoft"] = "Warning"
});
```

## Test Helper Classes

- **[`BaseTestWebApplicationFactory`](TestHelpers/BaseTestWebApplicationFactory.cs)** - Factory with test authentication
- **[`TestAuthenticationHandler`](TestHelpers/TestAuthenticationHandler.cs)** - Custom auth handler for tests
- **[`AuthenticatedTestBase`](TestHelpers/AuthenticatedTestBase.cs)** - Base class for authenticated tests
- **[`CustomVersionWebApplicationFactory`](TestHelpers/CustomVersionWebApplicationFactory.cs)** - Example custom factory

## Related Documentation

- [ASP.NET Core Integration Tests](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests)
- [WebApplicationFactory Documentation](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.mvc.testing.webapplicationfactory-1)
- [Configuration in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/)
- [Project Testing Rules](../../.roorules) - Gherkin-style test documentation
