# Integration Controller Tests

Integration tests for API controllers using `WebApplicationFactory` with **test authentication support** for tenant-based authorization.

## Quick Start

### Authenticated Test (Most Common)

```csharp
[TestFixture]
public class MyControllerTests : AuthenticatedTestBase
{
    [Test]
    public async Task GetData_AsEditor_ReturnsData()
    {
        // _client is already authenticated as Editor
        // _testTenantKey is already created
        var response = await _client.GetAsync($"/api/tenant/{_testTenantKey}/data");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }
}
```

### Public Endpoint Test

```csharp
[TestFixture]
public class PublicControllerTests
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();
    }

    [Test]
    public async Task GetVersion_ReturnsVersion()
    {
        var response = await _client.GetAsync("/version");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }
}
```

## What Gets Tested

Integration tests verify the complete HTTP pipeline:

- ✅ **Test authentication** (bypasses production JWT for programmatic control)
- ✅ **Tenant-based authorization** (validates tenant role claims)
- ✅ **Middleware pipeline** (including exception handlers, logging)
- ✅ **Routing** (URL patterns and route constraints)
- ✅ **Controller actions** (full request/response cycle)
- ✅ **Serialization** (JSON request/response)
- ✅ **HTTP status codes** (200, 404, 403, etc.)

## Test Authentication

Tests use a custom authentication system that mimics production while allowing programmatic control:

### Default Role: Editor

Most tests use the **Editor** role (read/write access):

```csharp
// Default - no role switching needed
var response = await _client.GetAsync($"/api/tenant/{_testTenantKey}/data");
```

### Role Switching

```csharp
SwitchToViewer();  // Read-only
SwitchToEditor();  // Read/write (default)
SwitchToOwner();   // Full control
```

### Multi-Tenant Testing

```csharp
var client = CreateMultiTenantClient(
    (tenant1Key, TenantRole.Editor),
    (tenant2Key, TenantRole.Viewer)
);
```

## Running Tests

```bash
# Run all integration tests
dotnet test tests/Integration.Controller

# Run specific test
dotnet test --filter "FullyQualifiedName~MyControllerTests.GetData_AsEditor_ReturnsData"

# Run with detailed output
dotnet test tests/Integration.Controller --logger "console;verbosity=detailed"
```

## Documentation

- **[TESTING-GUIDE.md](TESTING-GUIDE.md)** - Practical patterns and examples for writing tests
- **[TestHelpers/README.md](TestHelpers/README.md)** - Authentication architecture and component details
- **[Project Rules](../../.roorules)** - Gherkin-style test documentation standard

## Project Structure

```
Integration.Controller/
├── README.md                          # This file - quick start
├── TESTING-GUIDE.md                   # Implementation patterns and examples
├── TestHelpers/                       # Test authentication infrastructure
│   ├── README.md                      # Architecture details
│   ├── BaseTestWebApplicationFactory.cs
│   ├── TestAuthenticationHandler.cs
│   └── AuthenticatedTestBase.cs
├── TenantContextMiddlewareTests.cs   # Example authenticated tests
├── TransactionsControllerTests.cs    # Example public endpoint tests
└── VersionControllerTests.cs         # Example custom config tests
```

## Related Documentation

- [ASP.NET Core Integration Tests](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests)
- [WebApplicationFactory](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.mvc.testing.webapplicationfactory-1)
