# Clean Architecture Project Template Design

**Status:** Design Phase
**Created:** 2025-12-17
**Package:** `JColiz.Templates.CleanArchitecture`
**Goal:** Create a `dotnet new` template that generates production-ready Clean Architecture projects with proven patterns from YoFi.V3

## Executive Summary

This template provides a complete, opinionated Clean Architecture solution based on battle-tested patterns from YoFi.V3. It eliminates weeks of boilerplate setup, allowing developers to focus immediately on business logic while inheriting proven infrastructure, testing patterns, and architectural conventions.

### Key Value Propositions

1. **Instant Production-Ready Structure** - Complete solution with 7+ projects, tests, and documentation
2. **Proven Patterns** - Every pattern tested in production (YoFi.V3)
3. **Comprehensive Testing** - Unit, integration, and functional test infrastructure included
4. **Multi-Tenancy Ready** - Optional complete SaaS framework
5. **Modern Stack** - .NET 10+, latest C# features, nullable reference types
6. **Developer Experience** - Aspire orchestration, hot reload, structured logging

## Template Parameters

### Core Parameters

```bash
dotnet new jcoliz-cleanarch \
  --name MyApp \
  --database sqlite \
  --auth identity \
  --frontend nuxt \
  --tenancy true
```

| Parameter | Type | Default | Values | Description |
|-----------|------|---------|--------|-------------|
| `name` | string | Required | - | Solution and root namespace name |
| `database` | choice | `sqlite` | `sqlite`, `postgresql`, `sqlserver`, `none` | Database provider |
| `auth` | choice | `identity` | `none`, `identity`, `nuxtidentity`, `identityserver` | Authentication system |
| `frontend` | choice | `none` | `none`, `nuxt`, `react`, `blazor` | Frontend framework |
| `tenancy` | bool | `false` | `true`, `false` | Include multi-tenancy framework |
| `aspire` | bool | `true` | `true`, `false` | Include .NET Aspire orchestration |
| `functional-tests` | bool | `true` | `true`, `false` | Include Playwright functional tests |

### Advanced Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `opentelemetry` | bool | `true` | Include OpenTelemetry instrumentation |
| `application-insights` | bool | `false` | Add Application Insights support |
| `docker` | bool | `true` | Include Docker configurations |
| `ci-pipeline` | choice | `none` | `none`, `github`, `azure`, `gitlab` - CI/CD workflow files |
| `azure-infra` | bool | `false` | Include Azure Bicep infrastructure templates |
| `entity-sample` | string | `Product` | Name of sample entity to generate |
| `api-versioning` | bool | `false` | Include API versioning setup |

## Generated Solution Structure

### Complete File Tree

```
MyApp/
├── .editorconfig                      # Code style configuration
├── .gitignore                         # Standard .NET + Node.js ignores
├── global.json                        # .NET SDK version pinning
├── MyApp.sln                          # Solution file
├── README.md                          # Getting started guide
├── LICENSE                            # MIT license
│
├── src/
│   ├── Directory.Build.props          # Shared MSBuild properties
│   │
│   ├── MyApp.Entities/               # Domain Layer (no dependencies)
│   │   ├── MyApp.Entities.csproj
│   │   ├── Models/
│   │   │   ├── IModel.cs             # Base entity interface
│   │   │   ├── BaseModel.cs          # Base entity with ID, timestamps
│   │   │   ├── Product.cs            # Sample entity
│   │   │   └── [Tenancy]/            # (if --tenancy true)
│   │   │       ├── ITenantModel.cs
│   │   │       └── BaseTenantModel.cs
│   │   ├── Exceptions/
│   │   │   └── ProductNotFoundException.cs
│   │   ├── Providers/
│   │   │   └── IDataProvider.cs      # Repository abstraction
│   │   └── [Tenancy]/                # (if --tenancy true)
│   │       ├── Models/
│   │       ├── Providers/
│   │       └── Exceptions/
│   │
│   ├── MyApp.Application/            # Application Layer (Features)
│   │   ├── MyApp.Application.csproj
│   │   ├── Features/
│   │   │   └── ProductFeature.cs     # Sample feature
│   │   ├── Dto/
│   │   │   ├── ProductEditDto.cs
│   │   │   └── ProductResultDto.cs
│   │   ├── Validation/
│   │   │   └── ProductValidator.cs   # FluentValidation
│   │   └── Extensions/
│   │       └── ServiceCollectionExtensions.cs
│   │
│   ├── MyApp.Controllers/            # API Controllers Layer
│   │   ├── MyApp.Controllers.csproj
│   │   ├── ProductController.cs      # Sample controller
│   │   ├── VersionController.cs      # Health/version endpoint
│   │   ├── Middleware/
│   │   │   ├── CustomExceptionHandler.cs
│   │   │   └── TestCorrelationMiddleware.cs
│   │   └── Extensions/
│   │       ├── ServiceCollectionExtensions.cs
│   │       └── ApplicationBuilderExtensions.cs
│   │
│   ├── MyApp.Data/                   # Data Access Layer
│   │   ├── Sqlite/                   # (if --database sqlite)
│   │   │   ├── MyApp.Data.Sqlite.csproj
│   │   │   ├── ApplicationDbContext.cs
│   │   │   ├── Configurations/
│   │   │   │   └── ProductConfiguration.cs
│   │   │   └── Repositories/
│   │   │       └── DataProvider.cs   # IDataProvider implementation
│   │   └── Sqlite.MigrationHost/     # Migration tooling
│   │       ├── MyApp.Data.Sqlite.MigrationHost.csproj
│   │       └── Program.cs
│   │
│   ├── MyApp.BackEnd/                # API Host
│   │   ├── MyApp.BackEnd.csproj
│   │   ├── Program.cs                # Startup configuration
│   │   ├── appsettings.json
│   │   ├── appsettings.Development.json
│   │   ├── Logging/                  # Custom console logger
│   │   │   ├── CustomConsoleLogger.cs
│   │   │   ├── CustomConsoleLoggerProvider.cs
│   │   │   └── LoggingBuilderExtensions.cs
│   │   └── Properties/
│   │       └── launchSettings.json
│   │
│   ├── MyApp.ServiceDefaults/        # (if --aspire true)
│   │   ├── MyApp.ServiceDefaults.csproj
│   │   └── Extensions.cs             # OpenTelemetry, health checks
│   │
│   ├── MyApp.AppHost/                # (if --aspire true)
│   │   ├── MyApp.AppHost.csproj
│   │   ├── Program.cs                # Aspire orchestration
│   │   ├── appsettings.json
│   │   └── Properties/
│   │       └── launchSettings.json
│   │
│   ├── MyApp.WireApiHost/            # (if --frontend != none)
│   │   ├── MyApp.WireApiHost.csproj
│   │   ├── Program.cs                # Minimal host for NSwag
│   │   ├── nswag.json                # TypeScript client config
│   │   └── appsettings.json
│   │
│   └── MyApp.FrontEnd.Nuxt/          # (if --frontend nuxt)
│       ├── nuxt.config.ts
│       ├── package.json
│       ├── tsconfig.json
│       ├── .gitignore
│       ├── .prettierrc
│       ├── .roorules                 # Frontend-specific patterns
│       └── app/
│           ├── pages/
│           │   └── products.vue      # Sample page
│           ├── components/
│           ├── utils/
│           │   └── apiclient.ts      # Generated API client
│           └── stores/
│
├── tests/
│   ├── Directory.Build.props          # Shared test properties
│   │
│   ├── MyApp.Tests.Unit/             # Application layer tests
│   │   ├── MyApp.Tests.Unit.csproj
│   │   ├── coverlet.runsettings      # Code coverage config
│   │   └── Features/
│   │       └── ProductFeatureTests.cs
│   │
│   ├── MyApp.Tests.Integration.Data/ # Data layer tests
│   │   ├── MyApp.Tests.Integration.Data.csproj
│   │   └── ProductRepositoryTests.cs
│   │
│   ├── MyApp.Tests.Integration.Controller/ # API tests
│   │   ├── MyApp.Tests.Integration.Controller.csproj
│   │   ├── ProductControllerTests.cs
│   │   └── TestHelpers/
│   │       ├── AuthenticatedTestBase.cs
│   │       ├── BaseTestWebApplicationFactory.cs
│   │       └── TestAuthenticationHandler.cs
│   │
│   └── MyApp.Tests.Functional/       # (if --functional-tests true)
│       ├── MyApp.Tests.Functional.csproj
│       ├── local.runsettings
│       ├── docker.runsettings
│       ├── Pages/
│       │   ├── BasePage.cs
│       │   ├── HomePage.cs
│       │   └── ProductsPage.cs
│       ├── Components/
│       │   └── Nav.cs
│       ├── Features/
│       │   └── Products.feature      # Gherkin scenarios
│       └── Steps/
│           └── ProductSteps.cs
│
├── docs/
│   ├── README.md                      # Documentation index
│   ├── ARCHITECTURE.md                # Architecture overview
│   ├── GETTING-STARTED.md             # Quick start guide
│   ├── TESTING.md                     # Testing strategy
│   ├── DEPLOYMENT.md                  # Deployment guide
│   ├── adr/                           # Architecture Decision Records
│   │   ├── README.md
│   │   ├── template.md
│   │   └── 0001-project-created-from-template.md
│   └── wip/                           # Work-in-progress docs
│
├── scripts/                           # PowerShell automation
│   ├── README.md
│   ├── Run-Tests.ps1                  # Run unit/integration tests
│   ├── Start-LocalDev.ps1             # (if --aspire true)
│   ├── Build-Container.ps1            # (if --docker true)
│   └── Add-Migration.ps1              # (if --database != none)
│
├── docker/                            # (if --docker true)
│   ├── README.md
│   └── docker-compose.yml
│
├── .github/                           # (if --ci-pipeline github)
│   └── workflows/
│       ├── build.yml                  # CI/CD workflow
│       └── deploy.yml
│
├── azure-pipelines.yml                # (if --ci-pipeline azure)
│
└── infra/                             # (if --azure-infra true)
    ├── main.bicep                     # Azure infrastructure as code
    ├── main.bicepparam                # Parameters file
    └── README.md                      # Provisioning instructions
```

## Code Generation Patterns

### 1. Entity Generation

**Pattern:** Generate sample entity with complete CRUD scaffolding

```csharp
// src/MyApp.Entities/Models/Product.cs
namespace MyApp.Entities.Models;

/// <summary>
/// Represents a product in the system.
/// </summary>
/// <remarks>
/// This is a sample entity generated by the template.
/// Modify or replace this with your actual domain entities.
/// </remarks>
public class Product : BaseModel
{
    /// <summary>
    /// The product name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The product description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The product price.
    /// </summary>
    public decimal Price { get; set; }

    /// <summary>
    /// Whether the product is currently active.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
```

**With Tenancy** (if `--tenancy true`):

```csharp
public class Product : BaseTenantModel
{
    // Properties same as above
    // BaseTenantModel adds: TenantId, Tenant navigation property
}
```

### 2. Feature Generation

**Pattern:** Complete feature with logging, validation, DTOs

```csharp
// src/MyApp.Application/Features/ProductFeature.cs
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace MyApp.Application.Features;

/// <summary>
/// Provides business logic for product operations.
/// </summary>
/// <param name="dataProvider">Data provider for database operations.</param>
/// <param name="logger">Logger for diagnostic output.</param>
public partial class ProductFeature(
    IDataProvider dataProvider,
    ILogger<ProductFeature> logger)
{
    /// <summary>
    /// Retrieves all active products.
    /// </summary>
    public async Task<IReadOnlyCollection<ProductResultDto>> GetProductsAsync()
    {
        LogStarting();

        var query = dataProvider.Get<Product>()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name);

        var products = await dataProvider.ToListNoTrackingAsync(query);
        var result = products.Select(MapToDto).ToList();

        LogOkCount(result.Count);
        return result;
    }

    /// <summary>
    /// Retrieves a specific product by its unique identifier.
    /// </summary>
    /// <param name="id">The product ID.</param>
    /// <exception cref="ProductNotFoundException">Thrown when product not found.</exception>
    public async Task<ProductResultDto> GetProductByIdAsync(Guid id)
    {
        LogStartingKey(id);

        var product = await dataProvider.Get<Product>()
            .Where(p => p.Id == id)
            .SingleOrDefaultAsync();

        if (product == null)
        {
            throw new ProductNotFoundException(id);
        }

        LogOkKey(id);
        return MapToDto(product);
    }

    /// <summary>
    /// Creates a new product.
    /// </summary>
    /// <param name="dto">The product data.</param>
    public async Task<ProductResultDto> CreateProductAsync(ProductEditDto dto)
    {
        LogStarting();

        var product = new Product
        {
            Name = dto.Name,
            Description = dto.Description,
            Price = dto.Price,
            IsActive = true
        };

        dataProvider.Add(product);
        await dataProvider.SaveChangesAsync();

        LogOkKey(product.Id);
        return MapToDto(product);
    }

    private static ProductResultDto MapToDto(Product product) => new(
        product.Id,
        product.Name,
        product.Description,
        product.Price,
        product.IsActive
    );

    [LoggerMessage(1, LogLevel.Debug, "{Location}: Starting")]
    private partial void LogStarting([CallerMemberName] string? location = null);

    [LoggerMessage(2, LogLevel.Debug, "{Location}: Starting {Key}")]
    private partial void LogStartingKey(Guid key, [CallerMemberName] string? location = null);

    [LoggerMessage(3, LogLevel.Information, "{Location}: OK")]
    private partial void LogOk([CallerMemberName] string? location = null);

    [LoggerMessage(4, LogLevel.Information, "{Location}: OK {Key}")]
    private partial void LogOkKey(Guid key, [CallerMemberName] string? location = null);

    [LoggerMessage(5, LogLevel.Information, "{Location}: OK {Count}")]
    private partial void LogOkCount(int count, [CallerMemberName] string? location = null);
}
```

### 3. Controller Generation

**Pattern:** RESTful controller with OpenAPI documentation

```csharp
// src/MyApp.Controllers/ProductController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace MyApp.Controllers;

/// <summary>
/// Manages product operations.
/// </summary>
/// <param name="productFeature">Feature providing product operations.</param>
/// <param name="logger">Logger for diagnostic output.</param>
[ApiController]
[Route("api/[controller]")]
[Authorize] // Remove if --auth none
public partial class ProductController(
    ProductFeature productFeature,
    ILogger<ProductController> logger) : ControllerBase
{
    /// <summary>
    /// Retrieves all active products.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<ProductResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProducts()
    {
        LogStarting();

        var products = await productFeature.GetProductsAsync();

        LogOk();
        return Ok(products);
    }

    /// <summary>
    /// Retrieves a specific product by ID.
    /// </summary>
    /// <param name="id">The product ID.</param>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ProductResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProduct(Guid id)
    {
        LogStartingKey(id);

        var product = await productFeature.GetProductByIdAsync(id);

        LogOkKey(id);
        return Ok(product);
    }

    /// <summary>
    /// Creates a new product.
    /// </summary>
    /// <param name="dto">The product data.</param>
    [HttpPost]
    [ProducesResponseType(typeof(ProductResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateProduct([FromBody] ProductEditDto dto)
    {
        LogStarting();

        var product = await productFeature.CreateProductAsync(dto);

        LogOkKey(product.Id);
        return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
    }

    [LoggerMessage(1, LogLevel.Debug, "{Location}: Starting")]
    private partial void LogStarting([CallerMemberName] string? location = null);

    [LoggerMessage(2, LogLevel.Debug, "{Location}: Starting {Key}")]
    private partial void LogStartingKey(Guid key, [CallerMemberName] string? location = null);

    [LoggerMessage(3, LogLevel.Information, "{Location}: OK")]
    private partial void LogOk([CallerMemberName] string? location = null);

    [LoggerMessage(4, LogLevel.Information, "{Location}: OK {Key}")]
    private partial void LogOkKey(Guid key, [CallerMemberName] string? location = null);
}
```

**With Tenancy** (if `--tenancy true`):

```csharp
[ApiController]
[Route("api/tenant/{tenantKey:guid}/[controller]")]
[Authorize]
public partial class ProductController(
    ProductFeature productFeature,
    ILogger<ProductController> logger) : ControllerBase
{
    [HttpGet]
    [RequireTenantRole(TenantRole.Viewer)]
    public async Task<IActionResult> GetProducts()
    {
        // Implementation same as above
    }
}
```

### 4. Test Generation

**Pattern:** Complete test coverage for generated code

```csharp
// tests/MyApp.Tests.Unit/Features/ProductFeatureTests.cs
using NUnit.Framework;

namespace MyApp.Tests.Unit.Features;

[TestFixture]
public class ProductFeatureTests
{
    private Mock<IDataProvider> _mockDataProvider = null!;
    private Mock<ILogger<ProductFeature>> _mockLogger = null!;
    private ProductFeature _feature = null!;

    [SetUp]
    public void SetUp()
    {
        _mockDataProvider = new Mock<IDataProvider>();
        _mockLogger = new Mock<ILogger<ProductFeature>>();
        _feature = new ProductFeature(_mockDataProvider.Object, _mockLogger.Object);
    }

    [Test]
    public async Task GetProductsAsync_WithActiveProducts_ReturnsProducts()
    {
        // Given: Active products exist in the database
        var products = new List<Product>
        {
            new() { Id = Guid.NewGuid(), Name = "Product 1", Price = 10.00m, IsActive = true },
            new() { Id = Guid.NewGuid(), Name = "Product 2", Price = 20.00m, IsActive = true }
        };

        var queryable = products.AsQueryable();
        _mockDataProvider.Setup(x => x.Get<Product>()).Returns(queryable);
        _mockDataProvider.Setup(x => x.ToListNoTrackingAsync(It.IsAny<IQueryable<Product>>()))
            .ReturnsAsync(products);

        // When: GetProductsAsync is called
        var result = await _feature.GetProductsAsync();

        // Then: All active products are returned
        Assert.That(result, Has.Count.EqualTo(2));
        Assert.That(result.Select(p => p.Name), Is.EquivalentTo(new[] { "Product 1", "Product 2" }));
    }

    [Test]
    public void GetProductByIdAsync_ProductNotFound_ThrowsProductNotFoundException()
    {
        // Given: Product does not exist
        var id = Guid.NewGuid();
        var queryable = Enumerable.Empty<Product>().AsQueryable();
        _mockDataProvider.Setup(x => x.Get<Product>()).Returns(queryable);

        // When: GetProductByIdAsync is called
        // Then: ProductNotFoundException is thrown
        Assert.ThrowsAsync<ProductNotFoundException>(async () =>
            await _feature.GetProductByIdAsync(id));
    }
}
```

## Configuration & Conventions

### MSBuild Configuration

**src/Directory.Build.props:**
```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <IncludeSourceRevisionInInformationalVersion>false</IncludeSourceRevisionInInformationalVersion>
  </PropertyGroup>
</Project>
```

**tests/Directory.Build.props:**
```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
</Project>
```

### Implicit Usings

**Global usings for all projects:**
```csharp
global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Threading.Tasks;
```

**Additional for Application/Controllers:**
```csharp
global using MyApp.Entities.Models;
global using MyApp.Entities.Providers;
global using MyApp.Application.Dto;
```

**Additional for Tests:**
```csharp
global using NUnit.Framework;
global using Moq;
```

### Code Style (.editorconfig)

```ini
root = true

[*]
charset = utf-8
insert_final_newline = true
trim_trailing_whitespace = true

[*.{cs,csx}]
indent_style = space
indent_size = 4

# Nullable reference types
dotnet_diagnostic.CS8618.severity = error  # Non-nullable field must contain non-null value
dotnet_diagnostic.CS8602.severity = error  # Dereference of a possibly null reference
dotnet_diagnostic.CS8603.severity = error  # Possible null reference return

# Modern C# patterns
csharp_prefer_simple_using_statement = true
csharp_style_namespace_declarations = file_scoped

[*.{csproj,props,targets}]
indent_size = 2
```

## Template Customization Points

### 1. Entity Customization

**Template Token:** `{{EntityName}}`

Users can specify custom entity via `--entity-sample`:

```bash
dotnet new jcoliz-cleanarch --name MyApp --entity-sample Order
```

Generates: `Order.cs`, `OrderFeature.cs`, `OrderController.cs`, etc.

### 2. Database Provider

**Conditional Compilation:**

```csharp
#if (database == "sqlite")
services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
#elif (database == "postgresql")
services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));
#elif (database == "sqlserver")
services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
#endif
```

### 3. Authentication

**Conditional Registration:**

```csharp
#if (auth == "identity")
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => {
        // JWT configuration
    });
#elif (auth == "nuxtidentity")
// ASP.NET Core Identity with NuxtIdentity integration
builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// NuxtIdentity provides JWT + refresh token flow optimized for Nuxt
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => {
        // JWT configuration with NuxtIdentity conventions
    });
#elif (auth == "identityserver")
// IdentityServer4 configuration
#endif
```

### 4. Multi-Tenancy

**Feature Toggle:**

When `--tenancy true`:
- Include `src/Entities/Tenancy/` directory
- Include `TenantController`, `TenantFeature`
- Add `[RequireTenantRole]` attributes to controllers
- Include tenancy middleware in `Program.cs`
- Update entity base classes to `BaseTenantModel`
- Include tenant-scoped tests

### 5. Frontend Integration

**Frontend-Specific Generation:**

```bash
# Nuxt frontend
--frontend nuxt
  → src/MyApp.FrontEnd.Nuxt/
  → src/MyApp.WireApiHost/ (API client generation)

# React frontend
--frontend react
  → src/MyApp.FrontEnd.React/
  → src/MyApp.WireApiHost/

# No frontend
--frontend none
  → No frontend or WireApiHost projects
```

## Documentation Generation

### Auto-Generated Documentation

**README.md:**
```markdown
# MyApp

A Clean Architecture application built with .NET 10 and [frontend framework].

## Quick Start

1. Prerequisites:
   - .NET 10 SDK
   - [Database-specific requirements]
   - [Frontend-specific requirements]

2. Run the application:
   ```bash
   dotnet run --project src/MyApp.AppHost  # With Aspire
   # OR
   dotnet run --project src/MyApp.BackEnd   # Direct
   ```

3. Access:
   - API: http://localhost:5001
   - Frontend: http://localhost:5173 (if applicable)
   - Aspire Dashboard: https://localhost:17191

## Project Structure

[Generated based on selected options]

## Testing

```bash
# Run all tests
.\scripts\Run-Tests.ps1

# Run specific test category
dotnet test tests/MyApp.Tests.Unit
```

## Architecture

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for details.

This project follows Clean Architecture principles with:
- Domain-Driven Design
- Feature-based organization
- Comprehensive testing strategy
- [Multi-tenancy support] (if applicable)

## Generated with JColiz.Templates.CleanArchitecture

Template version: [version]
Generated: [timestamp]
```

**ARCHITECTURE.md:**
```markdown
# Architecture Overview

## Principles

MyApp follows Clean Architecture with dependency inversion:

```
UI → Controllers → Application → Entities ← Data
```

[Include diagrams based on selected options]

## Project Dependencies

[Auto-generated dependency graph]

## Design Patterns

- Repository Pattern (via IDataProvider)
- Feature Pattern (vertical slices)
- CQRS Light (separate read/write DTOs)
- [Multi-Tenancy Pattern] (if applicable)

[Detailed sections based on selected options]
```

## PowerShell Scripts

### Run-Tests.ps1

```powershell
<#
.SYNOPSIS
Runs all unit and integration tests for MyApp.

.DESCRIPTION
Executes unit tests and integration tests (Data and Controller layers).
Excludes functional tests which require special setup.

.EXAMPLE
.\Run-Tests.ps1
Runs all unit and integration tests.

.NOTES
Generated by JColiz.Templates.CleanArchitecture
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

try {
    $repoRoot = Split-Path $PSScriptRoot -Parent
    Push-Location $repoRoot

    Write-Host "Running Unit Tests..." -ForegroundColor Cyan
    dotnet test tests/MyApp.Tests.Unit --no-build
    if ($LASTEXITCODE -ne 0) {
        throw "Unit tests failed"
    }

    Write-Host "Running Integration.Data Tests..." -ForegroundColor Cyan
    dotnet test tests/MyApp.Tests.Integration.Data --no-build
    if ($LASTEXITCODE -ne 0) {
        throw "Integration.Data tests failed"
    }

    Write-Host "Running Integration.Controller Tests..." -ForegroundColor Cyan
    dotnet test tests/MyApp.Tests.Integration.Controller --no-build
    if ($LASTEXITCODE -ne 0) {
        throw "Integration.Controller tests failed"
    }

    Write-Host "OK All tests passed" -ForegroundColor Green
}
catch {
    Write-Error "Test execution failed: $_"
    Write-Error $_.ScriptStackTrace
    exit 1
}
finally {
    Pop-Location
}
```

## Template Implementation Strategy

### Phase 1: Core Template Infrastructure

1. **Template Project Setup**
   - Create `template.json` with all parameters
   - Define conditional compilation symbols
   - Set up token replacement ({{EntityName}}, etc.)

2. **Base Solution Structure**
   - All projects with minimal code
   - Solution file with project references
   - Directory.Build.props files

3. **Documentation Templates**
   - README.md with dynamic sections
   - ARCHITECTURE.md with conditional content
   - ADR templates

### Phase 2: Code Generation Logic

1. **Entity Scaffolding**
   - Base entity classes
   - Sample entity with configurable name
   - Exception classes

2. **Feature Scaffolding**
   - Feature template with CRUD operations
   - DTO generation
   - Service registration

3. **Controller Scaffolding**
   - RESTful controller template
   - OpenAPI documentation
   - Logging patterns

4. **Test Scaffolding**
   - Unit test templates
   - Integration test base classes
   - Functional test structure

### Phase 3: Optional Features

1. **Multi-Tenancy**
   - Tenancy entity models
   - Middleware and authorization
   - Tenant-aware feature base

2. **Frontend Integration**
   - Nuxt project template
   - API client generation setup
   - Sample pages

3. **Aspire Orchestration**
   - AppHost project
   - ServiceDefaults
   - Configuration

### Phase 4: Documentation & Examples

1. **Getting Started Guide**
   - Step-by-step setup
   - Common scenarios
   - Troubleshooting

2. **Code Examples**
   - Adding new entities
   - Custom validation
   - Advanced queries

3. **Migration Guides**
   - From other templates
   - Version upgrades

## Usage Examples

### Example 1: Basic API with SQLite

```bash
dotnet new jcoliz-cleanarch \
  --name InventoryApi \
  --database sqlite \
  --auth identity \
  --frontend none \
  --entity-sample Product
```

**Generates:**
- API-only solution
- SQLite database
- JWT authentication
- Product CRUD endpoints
- Complete test coverage

**Time to first API call:** ~5 minutes

### Example 2: Full-Stack SaaS Application

```bash
dotnet new jcoliz-cleanarch \
  --name SaasApp \
  --database postgresql \
  --auth identity \
  --frontend nuxt \
  --tenancy true \
  --application-insights true \
  --entity-sample Workspace
```

**Generates:**
- Full-stack Nuxt + .NET application
- PostgreSQL database
- Complete multi-tenancy framework
- Application Insights telemetry
- Workspace management UI
- Comprehensive testing

**Time to first feature:** ~15 minutes

### Example 3: Microservice with Docker

```bash
dotnet new jcoliz-cleanarch \
  --name OrderService \
  --database postgresql \
  --auth none \
  --frontend none \
  --aspire false \
  --docker true \
  --ci-pipeline github \
  --entity-sample Order
```

**Generates:**
- Microservice-ready API
- Docker configurations
- GitHub Actions workflow
- No auth (handled by gateway)
- Order processing endpoints

**Time to containerized deployment:** ~20 minutes

## Testing Strategy

### Template Testing

1. **Generation Tests**
   - Verify all parameter combinations generate valid solutions
   - Ensure all generated code compiles
   - Validate project references

2. **Build Tests**
   - `dotnet build` succeeds for all configurations
   - No warnings with TreatWarningsAsErrors
   - Nullable reference type compliance

3. **Runtime Tests**
   - Generated tests pass
   - API endpoints return expected responses
   - Database migrations apply successfully

4. **Integration Tests**
   - Aspire orchestration works
   - Frontend can call backend
   - Authentication flow completes

### Validation Checklist

Before publishing template:

- [ ] All parameter combinations generate successfully
- [ ] Generated solution builds without errors/warnings
- [ ] Generated tests pass (unit, integration)
- [ ] Documentation is accurate and complete
- [ ] Scripts execute successfully
- [ ] Sample entity CRUD operations work end-to-end
- [ ] Multi-tenancy (when enabled) enforces isolation
- [ ] Frontend (when enabled) integrates correctly
- [ ] Aspire orchestration (when enabled) starts all services

## Success Metrics

### Developer Experience

- **Time to First Run:** < 5 minutes
- **Time to First Feature:** < 30 minutes
- **Documentation Clarity:** No external references needed for basic usage

### Code Quality

- **Test Coverage:** Generated tests provide >80% coverage
- **Build Success Rate:** 100% on first try
- **Compiler Warnings:** Zero with TreatWarningsAsErrors

### Adoption Metrics

- NuGet template downloads
- GitHub template repository stars
- Community feedback (issues/discussions)
- Blog post views and shares

## Future Enhancements

### Version 1.x Roadmap

- [ ] gRPC service support
- [ ] GraphQL API option
- [ ] Redis caching integration
- [ ] RabbitMQ/Azure Service Bus messaging
- [ ] Blazor WebAssembly frontend option
- [ ] Angular frontend option
- [ ] Vertical slice architecture variant

### Version 2.x Ideas

- [ ] Domain event sourcing option
- [ ] CQRS with MediatR
- [ ] Modular monolith architecture
- [ ] Multi-region deployment templates
- [ ] Kubernetes manifest generation
- [ ] Terraform infrastructure templates

## Conclusion

This Clean Architecture template represents years of accumulated knowledge from YoFi.V3 and distills it into a reusable, production-ready starting point. By eliminating 4-8 weeks of initial setup and providing battle-tested patterns, it allows developers to focus immediately on delivering business value.

**Key Differentiators:**

1. **Production-Ready** - Not a toy example; based on real production code
2. **Comprehensive** - Complete solution including tests, docs, scripts
3. **Opinionated** - Clear patterns and conventions (but customizable)
4. **Modern** - Latest .NET, C# features, nullable reference types
5. **Tested** - Every generated feature includes tests
6. **Multi-Tenancy** - Optional complete SaaS framework

**Next Steps:**
1. Review and approve this design
2. Implement template infrastructure (Phase 1)
3. Build code generation logic (Phase 2)
4. Add optional features (Phase 3)
5. Create comprehensive documentation (Phase 4)
6. Test with real-world scenarios
7. Publish to NuGet templates feed

---

**Document Status:** Design Complete - Ready for Implementation
**Estimated Implementation Time:** 4-6 weeks (full-time)
**Expected ROI:** 4-8 weeks saved per new project
