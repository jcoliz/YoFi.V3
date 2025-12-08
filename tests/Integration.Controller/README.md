# Integration Controller Tests

This project contains integration tests for API controllers using `WebApplicationFactory` to test the full HTTP request pipeline.

## Overview

Integration tests verify the complete request/response cycle including:
- Middleware pipeline execution
- Routing
- Controller action execution
- Response serialization
- HTTP status codes

## Test Infrastructure

### WebApplicationFactory

The tests use `WebApplicationFactory<Program>` which:
- Hosts the entire application in-memory
- Provides an `HttpClient` for making real HTTP requests
- Runs the full middleware pipeline
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

### Simple Controller Test (No Configuration Override)

For controllers that don't need special configuration:

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

### Custom Configuration Test

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

1. **Use OneTimeSetUp/OneTimeTearDown** for factory and client creation (faster)
2. **Inject configuration** instead of changing appsettings files
3. **Test HTTP behavior** - status codes, headers, content types
4. **Test actual responses** - deserialize JSON and verify structure
5. **Keep tests independent** - each test should work in isolation
6. **Clean up resources** - dispose factory and client properly
7. **Use const for test data** - makes tests more maintainable
8. **Test one thing per test** - focused tests are easier to debug

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

## Related Documentation

- [ASP.NET Core Integration Tests](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests)
- [WebApplicationFactory Documentation](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.mvc.testing.webapplicationfactory-1)
- [Configuration in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/)
- [Next Steps for Middleware Testing](NEXT-STEPS.md)
- [Main Testing Plan](../../docs/wip/TENANT-MIDDLEWARE-TESTING-PLAN.md)
