# Integration Test Helpers

This directory contains reusable helper classes and utilities for integration tests.

## BaseTestWebApplicationFactory

**File**: [`BaseTestWebApplicationFactory.cs`](BaseTestWebApplicationFactory.cs)

A base `WebApplicationFactory` that provides common test configuration for all integration tests.

### Features

- Configures default application settings (version, environment, CORS)
- Sets up a temporary SQLite database for test isolation
- Automatically cleans up the temporary database after tests
- Supports configuration overrides via constructor parameter

### Usage

```csharp
// Use directly with custom configuration
var factory = new BaseTestWebApplicationFactory(
    configurationOverrides: new Dictionary<string, string?>
    {
        ["SomeSetting"] = "CustomValue"
    }
);

// Or inherit to create specialized factories
public class MyTestFactory : BaseTestWebApplicationFactory
{
    public MyTestFactory() : base(/* optional config */) { }
}
```

### Why Use a Base Factory?

The base factory eliminates code duplication by providing:
- Consistent default configuration across all tests
- Automatic database lifecycle management
- A single place to update common test setup

## CustomVersionWebApplicationFactory

**File**: [`CustomVersionWebApplicationFactory.cs`](CustomVersionWebApplicationFactory.cs)

Extends [`BaseTestWebApplicationFactory`](BaseTestWebApplicationFactory.cs) to inject specific version and environment configuration for testing the [`VersionController`](../../../src/Controllers/VersionController.cs).

### Purpose

Allows integration tests to inject specific configuration values without modifying `appsettings.json` files. This enables:
- Deterministic testing with known values
- Testing different configuration scenarios
- Isolated test execution

### Usage

```csharp
using YoFi.V3.Tests.Integration.Controller.TestHelpers;

// Create factory with specific configuration
var factory = new CustomVersionWebApplicationFactory(
    version: "1.2.3-test",
    environment: EnvironmentType.Local
);

// Create HTTP client
var client = factory.CreateClient();

// Make requests and assert on results
var response = await client.GetAsync("/version");
var version = await response.Content.ReadFromJsonAsync<string>();

Assert.That(version, Is.EqualTo("1.2.3-test (Local)"));

// Clean up
client.Dispose();
factory.Dispose();
```

### How It Works

The factory passes configuration overrides to the base class constructor:

```csharp
public class CustomVersionWebApplicationFactory : BaseTestWebApplicationFactory
{
    public CustomVersionWebApplicationFactory(string version, EnvironmentType environment)
        : base(new Dictionary<string, string?>
        {
            ["Application:Version"] = version,
            ["Application:Environment"] = environment.ToString()
        })
    {
    }
}
```

### Example Test

See [`VersionControllerTests.cs`](../VersionControllerTests.cs) for complete examples:

```csharp
[TestCase(EnvironmentType.Production, "1.2.3-test", ExpectedResult = "1.2.3-test")]
[TestCase(EnvironmentType.Local, "1.2.3-test", ExpectedResult = "1.2.3-test (Local)")]
[TestCase(EnvironmentType.Container, "1.2.3-test", ExpectedResult = "1.2.3-test (Container)")]
public async Task<string> GetVersion_AllEnvironmentTypes_ReturnsCorrectFormat(
    EnvironmentType environment,
    string version)
{
    using var factory = new CustomVersionWebApplicationFactory(version, environment);
    using var client = factory.CreateClient();

    var response = await client.GetAsync("/version");
    var result = await response.Content.ReadFromJsonAsync<string>();

    return result ?? string.Empty;
}
```

## Creating New Test Helpers

When creating new test helpers, follow these patterns:

### 1. Custom WebApplicationFactory for Service Overrides

```csharp
public class CustomDatabaseWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove existing service
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor != null)
                services.Remove(descriptor);

            // Add test service
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase("TestDb");
            });
        });
    }
}
```

### 2. Test Data Builders

```csharp
public class TenantTestDataBuilder
{
    private Guid _key = Guid.NewGuid();
    private string _name = "Test Tenant";

    public TenantTestDataBuilder WithKey(Guid key)
    {
        _key = key;
        return this;
    }

    public TenantTestDataBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public Tenant Build()
    {
        return new Tenant
        {
            Key = _key,
            Name = _name
        };
    }
}
```

### 3. Test Fixtures

```csharp
public class DatabaseFixture : IDisposable
{
    public ApplicationDbContext DbContext { get; }

    public DatabaseFixture()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        DbContext = new ApplicationDbContext(options);
        DbContext.Database.EnsureCreated();

        // Seed test data
        SeedTestData();
    }

    private void SeedTestData()
    {
        // Add test data
    }

    public void Dispose()
    {
        DbContext.Dispose();
    }
}
```

## Best Practices

1. **Reusability** - Create helpers that can be used across multiple tests
2. **Isolation** - Ensure each factory/fixture creates isolated test environments
3. **Documentation** - Document parameters and expected behavior
4. **Disposal** - Implement proper cleanup (IDisposable)
5. **Naming** - Use descriptive names that indicate purpose
6. **Defaults** - Provide sensible default values where appropriate

## Related Documentation

- [Integration Tests README](../README.md)
- [WebApplicationFactory Documentation](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.mvc.testing.webapplicationfactory-1)
- [Testing Best Practices](../../../docs/wip/TENANT-MIDDLEWARE-TESTING-PLAN.md)
