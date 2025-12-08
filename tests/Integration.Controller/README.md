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

### Example: VersionController Tests

[`VersionControllerTests.cs`](VersionControllerTests.cs) demonstrates the basic pattern:

```csharp
[TestFixture]
public class VersionControllerTests
{
    private WebApplicationFactory<Program> _factory;
    private HttpClient _client;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();
    }

    [Test]
    public async Task GetVersion_ReturnsSuccessAndVersion()
    {
        var response = await _client.GetAsync("/version");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }
}
```

## Running the Tests

```bash
# Run all integration controller tests
dotnet test tests/Integration.Controller

# Run specific test
dotnet test --filter "FullyQualifiedName~VersionControllerTests.GetVersion_ReturnsSuccessAndVersion"

# Run with detailed output
dotnet test tests/Integration.Controller --logger "console;verbosity=detailed"
```

## Project Dependencies

- `Microsoft.AspNetCore.Mvc.Testing` - WebApplicationFactory support
- `NUnit` - Test framework
- `YoFi.V3.BackEnd` - The application being tested

## Adding New Tests

To add tests for a new controller:

1. Create a new test class in this project
2. Use `WebApplicationFactory<Program>` to create the test server
3. Use `HttpClient` to make requests
4. Assert on the response status, content, and headers

Example:
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

## Customizing WebApplicationFactory

For tests that need custom configuration (e.g., different database, mocked services):

```csharp
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace services
            // Override configuration
            // Setup test database
        });
    }
}
```

## Best Practices

1. **Use OneTimeSetUp/OneTimeTearDown** for factory and client creation
2. **Test HTTP behavior** - status codes, headers, content types
3. **Test actual responses** - deserialize JSON and verify structure
4. **Keep tests independent** - each test should work in isolation
5. **Clean up resources** - dispose factory and client properly

## Related Documentation

- [ASP.NET Core Integration Tests](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests)
- [WebApplicationFactory Documentation](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.mvc.testing.webapplicationfactory-1)
- [Main Testing Plan](../../docs/wip/TENANT-MIDDLEWARE-TESTING-PLAN.md)
