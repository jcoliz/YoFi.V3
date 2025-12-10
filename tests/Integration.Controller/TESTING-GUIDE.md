# Integration Testing Guide

This guide provides practical patterns and examples for writing integration tests for tenant-protected API endpoints.

## Quick Start

### Authenticated Test (Most Common)

For endpoints requiring tenant-based authorization:

```csharp
[TestFixture]
public class MyControllerTests : AuthenticatedTestBase
{
    [Test]
    public async Task GetData_AsEditor_ReturnsData()
    {
        // Given: Authenticated as Editor (default)
        // And: Test tenant already created (_testTenantKey)

        // When: API Client requests data
        var response = await _client.GetAsync($"/api/tenant/{_testTenantKey}/data");

        // Then: Data is returned successfully
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }
}
```

### Public Endpoint Test

For endpoints that don't require authentication:

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

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Test]
    public async Task GetVersion_ReturnsVersion()
    {
        // When: API Client requests version
        var response = await _client.GetAsync("/version");

        // Then: Version is returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }
}
```

## Testing Different Roles

### Viewer Role (Read-Only)

```csharp
[Test]
public async Task DeleteData_AsViewer_ReturnsForbidden()
{
    // Given: Switch to Viewer role (read-only)
    SwitchToViewer();

    // When: Viewer attempts to delete data
    var response = await _client.DeleteAsync($"/api/tenant/{_testTenantKey}/data/123");

    // Then: 403 Forbidden should be returned
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
}
```

### Editor Role (Default)

```csharp
[Test]
public async Task CreateData_AsEditor_Succeeds()
{
    // Given: Authenticated as Editor (default, no SwitchToEditor() needed)
    var newData = new DataDto { Name = "Test" };

    // When: Editor creates new data
    var response = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/data", newData);

    // Then: Data is created successfully
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
}
```

### Owner Role (Full Control)

```csharp
[Test]
public async Task DeleteTenant_AsOwner_Succeeds()
{
    // Given: Explicitly switch to Owner role
    SwitchToOwner();

    // When: Owner deletes tenant
    var response = await _client.DeleteAsync($"/api/tenant/{_testTenantKey}");

    // Then: Tenant is deleted successfully
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
}
```

## Multi-Tenant Testing

### Cross-Tenant Isolation

```csharp
[Test]
public async Task GetData_DifferentTenants_ReturnsIsolatedData()
{
    // Given: Two tenants with their own data
    var tenant1Key = _testTenantKey; // From base class
    var tenant2Key = await CreateTestTenantAsync("Tenant 2");

    await SeedDataForTenant(tenant1Key, "Tenant1Data", count: 3);
    await SeedDataForTenant(tenant2Key, "Tenant2Data", count: 5);

    // And: User has Editor access to both tenants
    var multiTenantClient = CreateMultiTenantClient(
        (tenant1Key, TenantRole.Editor),
        (tenant2Key, TenantRole.Editor)
    );

    // When: User requests data from tenant 1
    var response1 = await multiTenantClient.GetAsync($"/api/tenant/{tenant1Key}/data");
    var data1 = await response1.Content.ReadFromJsonAsync<List<DataDto>>();

    // Then: Only tenant 1's data is returned
    Assert.That(data1, Has.Count.EqualTo(3));
    Assert.That(data1.All(d => d.Name.StartsWith("Tenant1Data")), Is.True);

    // When: User requests data from tenant 2
    var response2 = await multiTenantClient.GetAsync($"/api/tenant/{tenant2Key}/data");
    var data2 = await response2.Content.ReadFromJsonAsync<List<DataDto>>();

    // Then: Only tenant 2's data is returned
    Assert.That(data2, Has.Count.EqualTo(5));
    Assert.That(data2.All(d => d.Name.StartsWith("Tenant2Data")), Is.True);
}
```

### Unauthorized Tenant Access

```csharp
[Test]
public async Task GetData_TenantWithoutRole_Returns403()
{
    // Given: User has access to tenant 1 only
    var tenant1Key = _testTenantKey;
    var tenant2Key = await CreateTestTenantAsync("Unauthorized Tenant");

    // When: User attempts to access tenant 2's data
    var response = await _client.GetAsync($"/api/tenant/{tenant2Key}/data");

    // Then: 403 Forbidden should be returned
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
}
```

## Test Data Setup

### Using OneTimeSetUp

```csharp
[TestFixture]
public class MyControllerTests : AuthenticatedTestBase
{
    private Guid _firstDataKey;
    private const int TestDataCount = 5;

    [OneTimeSetUp]
    public override async Task OneTimeSetUp()
    {
        // Call base to set up factory, tenant, and authenticated client
        await base.OneTimeSetUp();

        // Seed test data
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var tenant = await dbContext.Set<Tenant>()
            .FirstAsync(t => t.Key == _testTenantKey);

        for (int i = 1; i <= TestDataCount; i++)
        {
            var data = new Data
            {
                Key = Guid.NewGuid(),
                TenantId = tenant.Id,
                Name = $"Test Data {i}",
                Value = i * 10
            };

            if (i == 1)
                _firstDataKey = data.Key;

            dbContext.Set<Data>().Add(data);
        }

        await dbContext.SaveChangesAsync();
    }
}
```

### Helper Methods for Data Creation

```csharp
private async Task<Guid> CreateDataAsync(Guid tenantKey, string name, int value)
{
    using var scope = _factory.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    var tenant = await dbContext.Set<Tenant>()
        .FirstAsync(t => t.Key == tenantKey);

    var data = new Data
    {
        Key = Guid.NewGuid(),
        TenantId = tenant.Id,
        Name = name,
        Value = value
    };

    dbContext.Set<Data>().Add(data);
    await dbContext.SaveChangesAsync();

    return data.Key;
}
```

## Custom Configuration

### Override Application Settings

```csharp
[TestFixture]
public class ConfigDependentTests
{
    private BaseTestWebApplicationFactory _factory = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var config = new Dictionary<string, string?>
        {
            ["Application:Feature:NewFeature"] = "true",
            ["Application:MaxPageSize"] = "50"
        };

        _factory = new BaseTestWebApplicationFactory(config);
        _client = _factory.CreateClient();
    }
}
```

## Common Patterns

### Testing Error Responses

```csharp
[Test]
public async Task GetData_NonExistentKey_Returns404()
{
    // Given: Non-existent data key
    var nonExistentKey = Guid.NewGuid();

    // When: API Client requests non-existent data
    var response = await _client.GetAsync($"/api/tenant/{_testTenantKey}/data/{nonExistentKey}");

    // Then: 404 Not Found should be returned
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

    // And: Response should contain problem details
    var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
    Assert.That(problemDetails, Is.Not.Null);
    Assert.That(problemDetails!.Title, Is.EqualTo("Data not found"));
}
```

### Testing Invalid Input

```csharp
[Test]
public async Task CreateData_InvalidInput_Returns400()
{
    // Given: Invalid data (missing required field)
    var invalidData = new DataDto { /* Name missing */ Value = 10 };

    // When: API Client creates data with invalid input
    var response = await _client.PostAsJsonAsync($"/api/tenant/{_testTenantKey}/data", invalidData);

    // Then: 400 Bad Request should be returned
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

    // And: Response should contain validation problem details
    var problemDetails = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
    Assert.That(problemDetails, Is.Not.Null);
    Assert.That(problemDetails!.Errors, Contains.Key("Name"));
}
```

## Best Practices

### Use Gherkin-Style Comments

Follow the project's [testing rules](../../.roorules) - use Given/When/Then/And comments:

```csharp
[Test]
public async Task GetData_WithFilter_ReturnsFilteredResults()
{
    // Given: Multiple data items in the database
    await CreateDataAsync(_testTenantKey, "Alpha", 10);
    await CreateDataAsync(_testTenantKey, "Beta", 20);
    await CreateDataAsync(_testTenantKey, "Gamma", 30);

    // When: API Client requests data with filter
    var response = await _client.GetAsync($"/api/tenant/{_testTenantKey}/data?filter=Beta");

    // Then: Only filtered results are returned
    var data = await response.Content.ReadFromJsonAsync<List<DataDto>>();
    Assert.That(data, Has.Count.EqualTo(1));
    Assert.That(data![0].Name, Is.EqualTo("Beta"));
}
```

### Keep Tests Independent

Each test should work in isolation:

```csharp
// ❌ BAD: Test depends on order of execution
[Test]
public async Task Test1_CreateData() { /* creates data */ }

[Test]
public async Task Test2_GetData() { /* assumes Test1 ran */ }

// ✅ GOOD: Each test is self-contained
[Test]
public async Task GetData_AfterCreation_ReturnsData()
{
    // Given: Data exists
    var dataKey = await CreateDataAsync(_testTenantKey, "Test", 10);

    // When: API Client requests data
    var response = await _client.GetAsync($"/api/tenant/{_testTenantKey}/data/{dataKey}");

    // Then: Data is returned
    Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
}
```

### Test One Thing Per Test

```csharp
// ❌ BAD: Testing multiple scenarios in one test
[Test]
public async Task TestEverything()
{
    // Tests creation, retrieval, update, and deletion all in one test
}

// ✅ GOOD: Separate tests for each scenario
[Test]
public async Task CreateData_ValidInput_ReturnsCreated() { }

[Test]
public async Task GetData_ExistingKey_ReturnsData() { }

[Test]
public async Task UpdateData_ValidInput_ReturnsUpdated() { }

[Test]
public async Task DeleteData_ExistingKey_ReturnsNoContent() { }
```

## Related Documentation

- [Main README](README.md) - Overview and project structure
- [TestHelpers README](TestHelpers/README.md) - Authentication architecture details
- [Project Testing Rules](../../.roorules) - Gherkin-style documentation standard
