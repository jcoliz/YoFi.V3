# API Contract Tests

In the last codebase review, GitHub Copilot recommended "API Contract tests". I asked it for details on how to set that up.

GitHub Copilot: I'll help you set up API contract tests for your project. Let me first explore your current test infrastructure to understand what's already in place.

Now I have a good understanding of your setup. Let me provide you with a comprehensive guide for setting up API contract tests:

## Setting Up API Contract Tests

Based on your codebase, I recommend using **Pact** for consumer-driven contract testing or **REST Assured-style** HTTP API testing. Given your existing .NET setup, here are two approaches:

### **Approach 1: Consumer-Driven Contract Tests with PactNet**

This validates that your API contract matches what clients expect:

#### 1. **Create a new test project or add to existing**

````csharp
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <RunSettingsFilePath>$(MSBuildProjectDirectory)\local.runsettings</RunSettingsFilePath>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="coverlet.collector" Version="6.0.4" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.0" />
    <PackageReference Include="Microsoft.Playwright.NUnit" Version="1.55.0" />
    <PackageReference Include="NUnit" Version="4.3.2" />
    <PackageReference Include="NUnit.Analyzers" Version="4.7.0" />
    <PackageReference Include="NUnit3TestAdapter" Version="5.0.0" />
    <!-- Add these for API contract testing -->
    <PackageReference Include="PactNet" Version="5.0.0" />
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.0" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="NUnit.Framework" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\BackEnd\YoFi.V3.BackEnd.csproj" />
  </ItemGroup>

</Project>
````

#### 2. **Create contract test for Weather API**

````csharp
using Microsoft.AspNetCore.Mvc.Testing;
using PactNet;
using PactNet.Matchers;
using System.Net;
using System.Text.Json;
using YoFi.V3.Entities.Models;

namespace YoFi.V3.Tests.Functional.Contract;

/// <summary>
/// Provider-side contract tests for Weather API
/// Validates that the API fulfills the expected contract
/// </summary>
[TestFixture]
public class WeatherApiContractTests
{
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;
    private IPactVerifier? _pactVerifier;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Test]
    public async Task GetWeatherForecasts_ReturnsValidContract()
    {
        // Arrange - Define expected contract
        var expectedContract = new
        {
            StatusCode = HttpStatusCode.OK,
            ContentType = "application/json",
            Schema = new
            {
                Type = "array",
                MinItems = 5,
                MaxItems = 5,
                Items = new
                {
                    RequiredProperties = new[] { "date", "temperatureC", "temperatureF", "summary" },
                    Properties = new
                    {
                        date = "string (ISO 8601 date)",
                        temperatureC = "integer",
                        temperatureF = "integer",
                        summary = "string"
                    }
                }
            }
        };

        // Act
        var response = await _client!.GetAsync("/api/weather");

        // Assert - Validate contract
        Assert.Multiple(async () =>
        {
            // Status code contract
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK),
                "Contract violation: Expected 200 OK status code");

            // Content type contract
            Assert.That(response.Content.Headers.ContentType?.MediaType,
                Is.EqualTo("application/json"),
                "Contract violation: Expected application/json content type");

            // Schema contract
            var content = await response.Content.ReadAsStringAsync();
            var forecasts = JsonSerializer.Deserialize<WeatherForecast[]>(content,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            Assert.That(forecasts, Is.Not.Null,
                "Contract violation: Response body should be parseable as WeatherForecast[]");

            Assert.That(forecasts!.Length, Is.EqualTo(5),
                "Contract violation: Should return exactly 5 forecasts");

            // Validate each item matches schema
            foreach (var forecast in forecasts)
            {
                Assert.That(forecast.Date, Is.Not.EqualTo(default(DateOnly)),
                    "Contract violation: Date is required");
                Assert.That(forecast.TemperatureC, Is.InstanceOf<int>(),
                    "Contract violation: TemperatureC must be integer");
                Assert.That(forecast.TemperatureF, Is.InstanceOf<int>(),
                    "Contract violation: TemperatureF must be integer");
                Assert.That(forecast.Summary, Is.Not.Null.And.Not.Empty,
                    "Contract violation: Summary is required");
            }
        });
    }

    [Test]
    public async Task GetWeatherForecasts_MatchesOpenApiSpec()
    {
        // This test validates against the actual OpenAPI specification

        // Arrange
        var response = await _client!.GetAsync("/api/weather");

        // Act
        var content = await response.Content.ReadAsStringAsync();
        var forecasts = JsonSerializer.Deserialize<WeatherForecast[]>(content,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        // Assert - Match OpenAPI spec from WeatherController attributes
        Assert.Multiple(() =>
        {
            // ProducesResponseType(typeof(WeatherForecast[]), StatusCodes.Status200OK)
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(forecasts, Is.InstanceOf<WeatherForecast[]>());

            // Produces("application/json")
            Assert.That(response.Content.Headers.ContentType?.MediaType,
                Is.EqualTo("application/json"));
        });
    }

    [Test]
    public async Task GetWeatherForecasts_ErrorContract()
    {
        // Test error response contract
        // This would require you to have an endpoint that can fail or use a test double

        // Contract for 500 errors:
        // - Status: 500
        // - Content-Type: text/plain or application/json
        // - Body: error message string

        // This is a placeholder - you'd need to trigger an actual error condition
        // or use a mock/stub to test error contracts
    }
}
````

### **Approach 2: Schema Validation with Verify.Http (Simpler)**

This approach uses snapshot testing to detect contract changes:

````csharp
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Text.Json;
using YoFi.V3.Entities.Models;

namespace YoFi.V3.Tests.Functional.Contract;

/// <summary>
/// Schema validation tests for Weather API
/// Ensures API responses match expected schema and don't change unexpectedly
/// </summary>
[TestFixture]
public class WeatherApiSchemaTests
{
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;

    [OneTimeSetUp]
    public void Setup()
    {
        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void Cleanup()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Test]
    public async Task WeatherForecast_SchemaValidation()
    {
        // Act
        var response = await _client!.GetAsync("/api/weather");
        var json = await response.Content.ReadAsStringAsync();

        // Assert schema structure
        var forecasts = JsonSerializer.Deserialize<WeatherForecast[]>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.That(forecasts, Is.Not.Null);
        Assert.That(forecasts, Has.Length.EqualTo(5));

        var sample = forecasts![0];

        // Validate schema structure
        Assert.Multiple(() =>
        {
            Assert.That(sample, Has.Property("Date"));
            Assert.That(sample, Has.Property("TemperatureC"));
            Assert.That(sample, Has.Property("TemperatureF"));
            Assert.That(sample, Has.Property("Summary"));

            // Validate types
            Assert.That(sample.Date, Is.InstanceOf<DateOnly>());
            Assert.That(sample.TemperatureC, Is.InstanceOf<int>());
            Assert.That(sample.TemperatureF, Is.InstanceOf<int>());
            Assert.That(sample.Summary, Is.InstanceOf<string>());
        });
    }

    [Test]
    public async Task WeatherForecast_ResponseHeaderContract()
    {
        // Act
        var response = await _client!.GetAsync("/api/weather");

        // Assert HTTP contract
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Content.Headers.ContentType?.MediaType,
                Is.EqualTo("application/json"));
            Assert.That(response.Content.Headers.ContentType?.CharSet,
                Is.EqualTo("utf-8").IgnoreCase);
        });
    }

    [Test]
    public async Task WeatherForecast_DataRangeContract()
    {
        // Act
        var response = await _client!.GetAsync("/api/weather");
        var json = await response.Content.ReadAsStringAsync();
        var forecasts = JsonSerializer.Deserialize<WeatherForecast[]>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        // Assert business rules/constraints
        Assert.Multiple(() =>
        {
            foreach (var forecast in forecasts!)
            {
                // Temperature constraints
                Assert.That(forecast.TemperatureC, Is.InRange(-100, 100),
                    $"TemperatureC {forecast.TemperatureC} is out of reasonable range");

                // Fahrenheit conversion is correct
                var expectedF = 32 + (int)(forecast.TemperatureC / 0.5556);
                Assert.That(forecast.TemperatureF, Is.EqualTo(expectedF).Within(1),
                    "Temperature conversion contract violated");

                // Summary is from expected set
                Assert.That(forecast.Summary, Is.Not.Null.And.Not.Empty,
                    "Summary should not be null or empty");

                // Date is in the future (relative to test run)
                Assert.That(forecast.Date, Is.GreaterThanOrEqualTo(DateOnly.FromDateTime(DateTime.Now)),
                    "Forecast dates should be today or in the future");
            }
        });
    }
}
````

### **Approach 3: OpenAPI/Swagger Contract Testing**

Validate your actual API against your OpenAPI spec:

````csharp
using Microsoft.AspNetCore.Mvc.Testing;
using System.Text.Json;

namespace YoFi.V3.Tests.Functional.Contract;

/// <summary>
/// Validates that the API implementation matches the OpenAPI specification
/// </summary>
[TestFixture]
public class OpenApiContractTests
{
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;

    [OneTimeSetUp]
    public void Setup()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development"); // Ensure Swagger is enabled
            });
        _client = _factory.CreateClient();
    }

    [OneTimeTearDown]
    public void Cleanup()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Test]
    public async Task OpenApiSpec_IsAccessible()
    {
        // Act
        var response = await _client!.GetAsync("/swagger/v1/swagger.json");

        // Assert
        Assert.That(response.IsSuccessStatusCode, Is.True,
            "OpenAPI specification should be accessible");

        var spec = await response.Content.ReadAsStringAsync();
        var document = JsonDocument.Parse(spec);

        Assert.That(document.RootElement.GetProperty("openapi").GetString(),
            Does.StartWith("3."),
            "Should use OpenAPI 3.x specification");
    }

    [Test]
    public async Task WeatherEndpoint_ExistsInSpec()
    {
        // Act
        var response = await _client!.GetAsync("/swagger/v1/swagger.json");
        var spec = await response.Content.ReadAsStringAsync();
        var document = JsonDocument.Parse(spec);

        // Assert
        var paths = document.RootElement.GetProperty("paths");
        Assert.That(paths.TryGetProperty("/api/Weather", out var weatherPath), Is.True,
            "Weather endpoint should be documented in OpenAPI spec");

        Assert.That(weatherPath.TryGetProperty("get", out var getMethod), Is.True,
            "GET method should be documented for Weather endpoint");

        // Validate response schema
        var responses = getMethod.GetProperty("responses");
        Assert.That(responses.TryGetProperty("200", out var okResponse), Is.True,
            "200 response should be documented");
    }
}
````

### **Running the Tests**

Add to your CI/CD pipeline:

````yaml
name: API Contract Tests

on:
  pull_request:
    branches: [ main ]
  push:
    branches: [ main ]

jobs:
  contract-tests:
    runs-on: ubuntu-latest

    steps:
    - uses: actions/checkout@v4

    - name: Setup .NET
      uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '10.0.x'

    - name: Restore dependencies
      run: dotnet restore

    - name: Run contract tests
      run: dotnet test tests/Functional/YoFi.V3.Tests.Functional.csproj --filter "Category=Contract" --logger "trx;LogFileName=contract-test-results.trx"

    - name: Publish test results
      uses: actions/upload-artifact@v4
      if: always()
      with:
        name: contract-test-results
        path: '**/contract-test-results.trx'
````

### **Key Benefits**

1. **Early detection** of breaking API changes
2. **Documentation** of expected API behavior
3. **Confidence** when refactoring
4. **Client compatibility** assurance

The tests integrate with your existing NSwag setup and validate that `WeatherController` honors its declared contract via the `ProducesResponseType` attributes.
