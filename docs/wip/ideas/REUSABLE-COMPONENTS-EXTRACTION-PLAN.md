# Reusable Components Extraction Plan

**Status:** Planning
**Created:** 2025-12-17
**Goal:** Extract application-independent code from YoFi.V3 into reusable NuGet packages for future applications

## Executive Summary

This document identifies all application-independent components in YoFi.V3 that should be extracted into reusable libraries. The goal is to enable future applications to leverage proven patterns without rewriting boilerplate code, focusing only on application-specific features.

### Extraction Categories

1. **Infrastructure & Middleware** - Exception handling, test correlation, logging patterns
2. **Base Types & Validation** - Foundation classes, custom validation attributes
3. **Data Access Patterns** - Provider interfaces, query extensions
4. **Development Tooling** - PowerShell scripts, test infrastructure, API generation
5. **Architecture Templates** - Project structure, conventions, extension patterns
6. **Multi-Tenancy Framework** - Complete tenant isolation and authorization system

## Part 1: Infrastructure & Middleware Components

### 1.1 Exception Handling System

**Package:** `JColiz.AspNetCore.ProblemDetails`

**Components to Extract:**

| Component | Current Location | Description |
|-----------|-----------------|-------------|
| [`CustomExceptionHandler`](../../src/Controllers/Middleware/CustomExceptionHandler.cs) | `src/Controllers/Middleware/` | Configurable exception-to-HTTP mapping with ProblemDetails |
| [`ResourceNotFoundException`](../../src/Entities/Exceptions/ResourceNotFoundException.cs) | `src/Entities/Exceptions/` | Base class for 404 exceptions with resource metadata |

**Features:**
- Pattern matching for exception-to-status code mapping
- Automatic ProblemDetails generation with trace IDs
- Extensible design for adding custom exception handlers
- Built-in handlers for common scenarios (404, 400, 409)
- Activity/W3C trace context integration

**Usage Pattern:**
```csharp
// In Program.cs
builder.Services.AddExceptionHandler<CustomExceptionHandler>();

// Custom exceptions
public class ProductNotFoundException : ResourceNotFoundException
{
    public override string ResourceType => "Product";
    public ProductNotFoundException(Guid key) : base(key) { }
}
```

**Dependencies:**
- `Microsoft.AspNetCore.Diagnostics` (framework)
- `Microsoft.AspNetCore.Mvc` (framework)

**Migration Strategy:**
- Move to new package with namespace `JColiz.AspNetCore.ProblemDetails`
- Keep extension points for custom exception mappings
- Tenancy-specific exceptions remain separate (see Part 6)

---

### 1.2 Test Correlation Middleware

**Package:** `JColiz.AspNetCore.Testing`

**Components to Extract:**

| Component | Current Location | Description |
|-----------|-----------------|-------------|
| [`TestCorrelationMiddleware`](../../src/Controllers/Middleware/TestCorrelationMiddleware.cs) | `src/Controllers/Middleware/` | Extracts test metadata from headers, adds to telemetry |

**Features:**
- Extracts `X-Test-Name`, `X-Test-Id`, `X-Test-Class` headers
- Adds test metadata to Activity tags (OpenTelemetry)
- Adds test metadata to logging scope (structured logging)
- Query parameter logging for debugging
- W3C Trace Context integration

**Usage Pattern:**
```csharp
// In Program.cs
app.UseTestCorrelation();

// In tests
var request = new HttpRequestMessage(HttpMethod.Get, "/api/products");
request.Headers.Add("X-Test-Name", "GetProducts_ValidRequest_ReturnsProducts");
request.Headers.Add("X-Test-Id", testRunId);
```

**Dependencies:**
- `Microsoft.AspNetCore.Http` (framework)
- `System.Diagnostics.DiagnosticSource` (framework)

**Integration Points:**
- Works with Application Insights, Aspire Dashboard, Jaeger
- No coupling to specific test framework (Playwright, xUnit, NUnit)

---

### 1.3 Structured Logging System

**Package:** `JColiz.Extensions.Logging.Console`

**Components to Extract:**

| Component | Current Location | Description |
|-----------|-----------------|-------------|
| [`CustomConsoleLogger`](../../src/BackEnd/Logging/CustomConsoleLogger.cs) | `src/BackEnd/Logging/` | Systemd-style console logger with scope support |
| [`CustomConsoleLoggerProvider`](../../src/BackEnd/Logging/CustomConsoleLoggerProvider.cs) | `src/BackEnd/Logging/` | Logger provider implementation |
| [`LoggingBuilderExtensions`](../../src/BackEnd/Logging/LoggingBuilderExtensions.cs) | `src/BackEnd/Logging/` | Fluent registration API |

**Features:**
- Systemd-compatible output format (priority level prefixes)
- Structured scope property inclusion
- Configurable timestamp format (local vs UTC)
- Efficient for container/CI environments
- Color support for development

**Usage Pattern:**
```csharp
// Startup logger
var logger = LoggingBuilderExtensions.CreateStartupLogger();

// In Program.cs
builder.Logging.AddApplicationLogging();
```

**Documentation to Extract:**
- [Logging Policy](../LOGGING-POLICY.md) - Adapt to be framework-agnostic
- [Logging Architecture Analysis](LOGGING-ARCHITECTURE-ANALYSIS.md)

---

### 1.4 Service Defaults (OpenTelemetry Configuration)

**Package:** `JColiz.ServiceDefaults` (inspired by Aspire)

**Components to Extract:**

| Component | Current Location | Description |
|-----------|-----------------|-------------|
| [`Extensions.cs`](../../src/ServiceDefaults/Extensions.cs) | `src/ServiceDefaults/` | OpenTelemetry, health checks, service discovery setup |

**Features:**
- Pre-configured OpenTelemetry with metrics, tracing, logging
- Azure Monitor integration
- OTLP exporter support
- Health check endpoints (`/health`, `/alive`)
- EF Core instrumentation
- ASP.NET Core instrumentation

**Usage Pattern:**
```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults(logger);

var app = builder.Build();
app.MapDefaultEndpoints();
```

**Customization Points:**
- Allow overriding OTLP endpoints
- Custom health check registration
- Selective instrumentation enabling

---

## Part 2: Base Types & Validation

### 2.1 Validation Attributes

**Package:** `JColiz.ComponentModel.DataAnnotations`

**Components to Extract:**

| Component | Current Location | Description |
|-----------|-----------------|-------------|
| [`NotWhiteSpaceAttribute`](../../src/Application/Validation/NotWhiteSpaceAttribute.cs) | `src/Application/Validation/` | Validates string is not empty or whitespace |
| [`DateRangeAttribute`](../../src/Application/Validation/DateRangeAttribute.cs) | `src/Application/Validation/` | Validates DateOnly within relative range from today |

**Usage Pattern:**
```csharp
public record ProductDto
{
    [Required]
    [NotWhiteSpace(ErrorMessage = "Product name is required")]
    public string Name { get; init; }

    [DateRange(yearsInPast: 1, yearsInFuture: 0)]
    public DateOnly ManufactureDate { get; init; }
}
```

**Future Extensions:**
- `[PhoneNumber]` with country code validation
- `[CreditCard]` with checksum validation
- `[Url]` with scheme validation
- `[StrongPassword]` with configurable rules

---

### 2.2 Data Provider Interface

**Package:** `JColiz.Data.Abstractions`

**Components to Extract:**

| Component | Current Location | Description |
|-----------|-----------------|-------------|
| [`IDataProvider`](../../src/Entities/Providers/IDataProvider.cs) | `src/Entities/Providers/` | Repository-agnostic data access interface |
| [`IModel`](../../src/Entities/Models/IModel.cs) | `src/Entities/Models/` | Base interface for entities |

**Features:**
- Generic queryable access (`Get<TEntity>()`)
- CRUD operations (Add, Update, Remove)
- Async execution helpers (`ToListAsync`, `SingleOrDefaultAsync`)
- No-tracking query support
- No coupling to EF Core (implementation detail)

**Usage Pattern:**
```csharp
public class ProductFeature(IDataProvider dataProvider)
{
    public async Task<List<Product>> GetProductsAsync()
    {
        var query = dataProvider.Get<Product>()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name);

        return await dataProvider.ToListNoTrackingAsync(query);
    }
}
```

**Why Extract:**
- Enables testing with in-memory implementations
- Abstracts EF Core away from business logic
- Makes Clean Architecture boundaries explicit

---

## Part 3: Development Tooling

### 3.1 PowerShell Script Patterns

**Package:** Not packaged - provide as templates/examples

**Documentation to Create:**

`Templates/PowerShell/README.md` - PowerShell scripting conventions
- Comment-based help format
- Error handling with try/catch/finally
- `$PSScriptRoot` for path resolution
- Colored output standards (Cyan/Green/Yellow for info/success/warning)
- `Push-Location`/`Pop-Location` patterns
- Exit code handling for external commands

**Reference Scripts:**
- [`Run-Tests.ps1`](../../scripts/Run-Tests.ps1) - Test execution pattern
- [`Build-Container.ps1`](../../scripts/Build-Container.ps1) - Docker build pattern
- [`Start-LocalDev.ps1`](../../scripts/Start-LocalDev.ps1) - Development environment startup

---

### 3.2 NSwag API Client Generation

**Package:** Not packaged - provide as template project

**Components to Document:**

| Component | Current Location | Description |
|-----------|-----------------|-------------|
| [`nswag.json`](../../src/WireApiHost/nswag.json) | `src/WireApiHost/` | NSwag configuration for TypeScript client |
| [`Program.cs`](../../src/WireApiHost/Program.cs) | `src/WireApiHost/` | Minimal host for API generation |

**Template Project Structure:**
```
Templates/WireApiHost/
├── WireApiHost.csproj
├── Program.cs              # Minimal host configuration
├── nswag.json             # Client generation config
├── README.md              # Usage instructions
└── appsettings.json
```

**Features to Template:**
- TypeScript client generation
- Problem details support
- Date handling configuration
- Enum to string conversion
- Generated client location configuration

---

### 3.3 Functional Test Infrastructure

**Package:** `JColiz.Testing.Functional`

**Components to Extract:**

| Component | Current Location | Description |
|-----------|-----------------|-------------|
| [`DataTable.cs`](../../tests/Functional/Helpers/DataTable.cs) | `tests/Functional/Helpers/` | Gherkin table support for SpecFlow-style tests |
| [`DataTableExtensions.cs`](../../tests/Functional/Helpers/DataTableExtensions.cs) | `tests/Functional/Helpers/` | Convenient table parsing methods |

**Features:**
- Key-value table parsing (`GetKeyValue()`)
- Single-column table to list conversion
- Integration with Playwright page object models
- Error messages with available keys for debugging

**Usage Pattern:**
```csharp
// In step definitions
[When(@"the user enters the following credentials:")]
public async Task WhenUserEntersCredentials(DataTable table)
{
    var email = table.GetKeyValue("Email");
    var password = table.GetKeyValue("Password");
    await loginPage.LoginAsync(email, password);
}
```

**Additional Test Helpers (Future):**
- HTTP client extensions for test correlation headers
- Docker container management helpers
- Test database seeding utilities

---

## Part 4: Architecture Patterns & Templates

### 4.1 Clean Architecture Project Template

**Package:** `JColiz.Templates.CleanArchitecture` (dotnet new template)

**Template Structure:**
```
Templates/CleanArchitecture/
├── src/
│   ├── Entities/               # Domain models, interfaces, exceptions
│   ├── Application/            # Features, DTOs, validation
│   ├── Controllers/            # API controllers
│   ├── BackEnd/               # API host
│   ├── Data/                  # EF Core implementation
│   └── ServiceDefaults/       # Shared Aspire configuration
├── tests/
│   ├── Unit/                  # Application layer tests
│   ├── Integration.Data/      # Data layer tests
│   ├── Integration.Controller/# API tests with auth
│   └── Functional/            # Playwright E2E tests
└── docs/
    ├── ARCHITECTURE.md
    └── adr/                   # Architecture Decision Records
```

**Template Parameters:**
- `--name`: Solution name
- `--database`: sqlite|postgresql|sqlserver
- `--auth`: none|identity|identityserver
- `--frontend`: none|nuxt|react|blazor
- `--tenancy`: Include multi-tenancy framework

**Files to Template:**
- `Directory.Build.props` - Shared MSBuild properties
- `.editorconfig` - Code style configuration
- `global.json` - .NET SDK version pinning
- `.gitignore` - Standard ignores
- Solution structure with project references

---

### 4.2 Feature-Based Organization Convention

**Documentation Package:** Not packaged - provide as guide

**Document to Create:** `Templates/CleanArchitecture/PATTERNS.md`

**Patterns to Document:**

1. **Feature Organization**
   ```csharp
   Application/
   ├── Features/
   │   ├── ProductFeature.cs
   │   ├── OrderFeature.cs
   │   └── UserFeature.cs
   └── Dto/
       ├── ProductDto.cs
       └── OrderDto.cs
   ```

2. **Service Registration Pattern**
   ```csharp
   services.Scan(scan => scan
       .FromAssemblyOf<ProductFeature>()
       .AddClasses(classes => classes.Where(type => type.Name.EndsWith("Feature")))
       .AsSelf()
       .WithScopedLifetime());
   ```

3. **Controller Pattern**
   - Primary constructor injection
   - Logging with `[LoggerMessage]` attribute
   - Single feature dependency per controller
   - Gherkin-style test documentation

4. **Repository Interface Pattern**
   - Interfaces in Entities project
   - Implementations in Data project
   - Registered as scoped services

---

### 4.3 Logging Pattern (From .roorules)

**Documentation:** Extract to standalone guide

**Create:** `Templates/Logging/LOGGING-PATTERNS.md`

**Key Patterns:**
- `[LoggerMessage]` source generation
- CallerMemberName for location tracking
- Event ID management (unique per class, never reuse)
- Log level conventions (Debug for Starting, Information for OK)
- Sensitive data rules
- API-layer-only logging in Clean Architecture

**Example Code Snippets:**
```csharp
// Pattern template
public partial class MyController(ILogger<MyController> logger)
{
    [LoggerMessage(1, LogLevel.Debug, "{Location}: Starting {Key}")]
    private partial void LogStartingKey(Guid key, [CallerMemberName] string? location = null);

    [LoggerMessage(2, LogLevel.Information, "{Location}: OK {Key}")]
    private partial void LogOkKey(Guid key, [CallerMemberName] string? location = null);
}
```

---

## Part 5: Testing Infrastructure & Patterns

### 5.1 NUnit Test Base Classes

**Package:** `JColiz.Testing.NUnit`

**Components to Extract:**

| Component | Current Location | Description |
|-----------|-----------------|-------------|
| [`AuthenticatedTestBase`](../../tests/Integration.Controller/TestHelpers/AuthenticatedTestBase.cs) | `tests/Integration.Controller/TestHelpers/` | Base class for authenticated API tests |
| [`BaseTestWebApplicationFactory`](../../tests/Integration.Controller/TestHelpers/BaseTestWebApplicationFactory.cs) | `tests/Integration.Controller/TestHelpers/` | Customizable WebApplicationFactory |
| [`TestAuthenticationHandler`](../../tests/Integration.Controller/TestHelpers/TestAuthenticationHandler.cs) | `tests/Integration.Controller/TestHelpers/` | Mock authentication for integration tests |

**Features:**
- Simplified authenticated test setup
- In-memory database per test
- HTTP client with auth token injection
- Configurable test user creation
- WebApplicationFactory with service overrides

**Usage Pattern:**
```csharp
[TestFixture]
public class ProductControllerTests : AuthenticatedTestBase
{
    [Test]
    public async Task GetProducts_Authenticated_ReturnsProducts()
    {
        // Given: Test user is authenticated (handled by base class)

        // When: Client requests products
        var response = await Client.GetAsync("/api/products");

        // Then: Success response is returned
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }
}
```

---

### 5.2 Test Documentation Pattern

**Package:** Not packaged - enforce via .roorules

**Convention:** Gherkin-style test comments (Given/When/Then/And)

**Rationale:**
- More readable than Arrange/Act/Assert
- Aligns with BDD practices
- Describes scenario, not implementation
- Better for non-technical stakeholders

**Enforcement:**
- Include in project .roorules
- Code review checklist
- Test template snippets

---

## Part 6: Multi-Tenancy Framework

**Package:** `JColiz.AspNetCore.Tenancy`

### 6.1 Core Tenancy Components

**Major Extractable System** - Complete framework for tenant isolation

**Components to Extract:**

| Component | Current Location | Description |
|-----------|-----------------|-------------|
| **Entities** |
| [`ITenantModel`](../../src/Entities/Tenancy/ITenantModel.cs) | `src/Entities/Tenancy/` | Interface marking tenant-scoped entities |
| [`Tenant`](../../src/Entities/Tenancy/Tenant.cs) | `src/Entities/Tenancy/Models/` | Tenant entity |
| [`UserTenantRoleAssignment`](../../src/Entities/Tenancy/UserTenantRoleAssignment.cs) | `src/Entities/Tenancy/Models/` | User-tenant-role junction |
| [`TenantRole`](../../src/Entities/Tenancy/UserTenantRoleAssignment.cs) | `src/Entities/Tenancy/Models/` | Role enum (Viewer/Editor/Owner) |
| [`ITenantProvider`](../../src/Entities/Tenancy/ITenantProvider.cs) | `src/Entities/Tenancy/Providers/` | Current tenant access |
| [`ITenantRepository`](../../src/Entities/Tenancy/ITenantRepository.cs) | `src/Entities/Tenancy/Providers/` | Tenant data operations |
| **Exceptions** |
| [`TenancyException`](../../src/Entities/Tenancy/Exceptions/TenancyException.cs) | `src/Entities/Tenancy/Exceptions/` | Base tenancy exception |
| [`TenantNotFoundException`](../../src/Entities/Tenancy/Exceptions/TenantNotFoundException.cs) | `src/Entities/Tenancy/Exceptions/` | Tenant not found (403) |
| [`TenantAccessDeniedException`](../../src/Entities/Tenancy/Exceptions/TenantAccessDeniedException.cs) | `src/Entities/Tenancy/Exceptions/` | Access denied (403) |
| [`TenantContextNotSetException`](../../src/Entities/Tenancy/Exceptions/TenantContextNotSetException.cs) | `src/Entities/Tenancy/Exceptions/` | Missing context (500) |
| **Authorization** |
| [`TenantRoleHandler`](../../src/Controllers/Tenancy/Authorization/TenantRoleHandler.cs) | `src/Controllers/Tenancy/Authorization/` | Authorization policy handler |
| [`TenantRoleRequirement`](../../src/Controllers/Tenancy/Authorization/TenantRoleRequirement.cs) | `src/Controllers/Tenancy/Authorization/` | Authorization requirement |
| [`RequireTenantRoleAttribute`](../../src/Controllers/Tenancy/Authorization/RequireTenantRoleAttribute.cs) | `src/Controllers/Tenancy/Authorization/` | Controller decoration |
| [`IClaimsEnricher`](../../src/Controllers/Tenancy/Authorization/IClaimsEnricher.cs) | `src/Controllers/Tenancy/Authorization/` | Claims enrichment interface |
| [`TenantClaimsEnricher`](../../src/Controllers/Tenancy/Authorization/TenantClaimsEnricher.cs) | `src/Controllers/Tenancy/Authorization/` | Tenant claims provider |
| [`TenantUserClaimsService`](../../src/Controllers/Tenancy/Authorization/TenantUserClaimsService.cs) | `src/Controllers/Tenancy/Authorization/` | JWT claims integration |
| [`AnonymousTenantAccessHandler`](../../src/Controllers/Tenancy/Authorization/AnonymousTenantAccessHandler.cs) | `src/Controllers/Tenancy/Authorization/` | Test endpoint support |
| **Context Management** |
| [`TenantContext`](../../src/Controllers/Tenancy/Context/TenantContext.cs) | `src/Controllers/Tenancy/Context/` | Current tenant state |
| [`TenantContextMiddleware`](../../src/Controllers/Tenancy/Context/TenantContextMiddleware.cs) | `src/Controllers/Tenancy/Context/TenantContextMiddleware.cs) | Tenant context setup |
| **Exception Handling** |
| [`TenancyExceptionHandler`](../../src/Controllers/Tenancy/Exceptions/TenancyExceptionHandler.cs) | `src/Controllers/Tenancy/Exceptions/` | HTTP response mapping |
| **API** |
| [`TenantController`](../../src/Controllers/Tenancy/Api/TenantController.cs) | `src/Controllers/Tenancy/Api/` | Tenant management endpoints |
| **Application Logic** |
| [`TenantFeature`](../../src/Application/Tenancy/Features/TenantFeature.cs) | `src/Application/Tenancy/Features/` | Tenant business logic |
| [`TenantEditDto`](../../src/Application/Tenancy/Dto/TenantEditDto.cs) | `src/Application/Tenancy/Dto/` | Tenant input DTO |
| [`TenantResultDto`](../../src/Application/Tenancy/Dto/TenantResultDto.cs) | `src/Application/Tenancy/Dto/` | Tenant output DTO |
| [`TenantRoleResultDto`](../../src/Application/Tenancy/Dto/TenantRoleResultDto.cs) | `src/Application/Tenancy/Dto/` | User role assignment DTO |
| **Service Registration** |
| [`ServiceCollectionExtensions`](../../src/Controllers/Tenancy/ServiceCollectionExtensions.cs) | `src/Controllers/Tenancy/` | Fluent registration API |

### 6.2 Tenancy Framework Features

**Complete Multi-Tenant SaaS Framework:**

1. **Database Pattern**: Shared database, shared schema with tenant discriminator
2. **Authorization**: Claims-based with role hierarchy (Viewer < Editor < Owner)
3. **Security**: Tenant enumeration prevention (both 404 and 403 return 403)
4. **Context Management**: Request-scoped tenant provider
5. **Data Isolation**: Single enforcement point pattern
6. **API Integration**: Controller attributes and middleware
7. **Exception Handling**: Tenancy-specific exceptions with HTTP mapping

**Usage Pattern:**
```csharp
// In Program.cs
builder.Services.AddTenancy();
builder.Services.AddScoped<ITenantRepository, YourDbContext>();

app.UseAuthentication();
app.UseAuthorization();
app.UseTenancy();  // After auth, before controllers

// In controllers
[Route("api/tenant/{tenantKey:guid}/[controller]")]
[ApiController]
public class ProductController(ProductFeature feature) : ControllerBase
{
    [HttpGet]
    [RequireTenantRole(TenantRole.Viewer)]
    public async Task<IActionResult> GetProducts()
    {
        var products = await feature.GetProductsAsync();
        return Ok(products);
    }
}

// In features
public class ProductFeature(ITenantProvider tenantProvider, IDataProvider dataProvider)
{
    private readonly Tenant _currentTenant = tenantProvider.CurrentTenant;

    private IQueryable<Product> GetBaseQuery()
    {
        return dataProvider.Get<Product>()
            .Where(p => p.TenantId == _currentTenant.Id);
    }
}
```

### 6.3 Tenancy Package Structure

**Namespace Organization:**
```
JColiz.AspNetCore.Tenancy/
├── Entities/
│   ├── ITenantModel.cs
│   ├── Tenant.cs
│   └── UserTenantRoleAssignment.cs
├── Providers/
│   ├── ITenantProvider.cs
│   └── ITenantRepository.cs
├── Authorization/
│   ├── TenantRoleHandler.cs
│   ├── RequireTenantRoleAttribute.cs
│   └── TenantClaimsEnricher.cs
├── Context/
│   ├── TenantContext.cs
│   └── TenantContextMiddleware.cs
├── Exceptions/
│   └── (all tenancy exceptions)
└── Extensions/
    └── ServiceCollectionExtensions.cs
```

**Separate Application Package:**
```
JColiz.AspNetCore.Tenancy.Application/
├── Features/
│   └── TenantFeature.cs
├── Dto/
│   └── (all tenant DTOs)
└── Controllers/
    └── TenantController.cs
```

**Why Two Packages:**
- Core tenancy framework is infrastructure
- Application/Controllers are optional (consumers may want their own)
- Separates concerns: authorization vs business logic

### 6.4 Documentation to Extract

**Primary Documentation:**
- [TENANCY.md](../TENANCY.md) - Adapt to be framework-agnostic
- [ADR 0009: Multi-tenancy Model](../adr/0009-accounts-and-tenancy.md) - Include design rationale

**Additional Guides:**
- Tenancy implementation guide (getting started)
- Security best practices
- Testing patterns for tenant-scoped code
- Migration guide from non-tenant to tenant architecture

---

## Part 7: Package Organization & Naming

### 7.1 NuGet Package Structure

**Package Naming Convention:** `JColiz.<Area>.<Technology>`

| Package Name | Description | Dependencies |
|-------------|-------------|--------------|
| **Infrastructure** |
| `JColiz.AspNetCore.ProblemDetails` | Exception handling with ProblemDetails | ASP.NET Core |
| `JColiz.AspNetCore.Testing` | Test correlation middleware | ASP.NET Core |
| `JColiz.Extensions.Logging.Console` | Systemd-style console logger | Microsoft.Extensions.Logging |
| `JColiz.ServiceDefaults` | OpenTelemetry + health checks | ASP.NET Core, OpenTelemetry |
| **Validation & Data** |
| `JColiz.ComponentModel.DataAnnotations` | Custom validation attributes | System.ComponentModel.Annotations |
| `JColiz.Data.Abstractions` | IDataProvider, IModel interfaces | None (pure abstractions) |
| **Testing** |
| `JColiz.Testing.Functional` | DataTable helpers for Gherkin tests | None |
| `JColiz.Testing.NUnit` | Base classes for integration tests | NUnit, Microsoft.AspNetCore.Mvc.Testing |
| **Multi-Tenancy** |
| `JColiz.AspNetCore.Tenancy` | Core tenancy framework | ASP.NET Core, Identity |
| `JColiz.AspNetCore.Tenancy.Application` | Tenant management controllers/features | JColiz.AspNetCore.Tenancy |
| **Templates** |
| `JColiz.Templates.CleanArchitecture` | dotnet new template | N/A (template) |

### 7.2 Versioning Strategy

**Semantic Versioning:** MAJOR.MINOR.PATCH

- **MAJOR**: Breaking API changes
- **MINOR**: New features, backward compatible
- **PATCH**: Bug fixes, backward compatible

**Initial Versions:**
- All packages start at `0.1.0` (pre-release)
- First stable release: `1.0.0`
- Breaking changes increment major version

### 7.3 Package Metadata

**Common Metadata:**
```xml
<PropertyGroup>
  <Authors>James Coliz</Authors>
  <Company>JColiz</Company>
  <Product>JColiz Application Framework</Product>
  <PackageLicenseExpression>MIT</PackageLicenseExpression>
  <PackageProjectUrl>https://github.com/jcoliz/YoFi.V3</PackageProjectUrl>
  <RepositoryUrl>https://github.com/jcoliz/YoFi.V3</RepositoryUrl>
  <RepositoryType>git</RepositoryType>
  <PackageTags>aspnetcore;clean-architecture;multi-tenancy</PackageTags>
</PropertyGroup>
```

---

## Part 8: Migration Strategy

### 8.1 Extraction Phases

**Phase 1: Foundation Packages (Immediate Value)**
1. `JColiz.ComponentModel.DataAnnotations` - Validation attributes
2. `JColiz.Data.Abstractions` - IDataProvider interface
3. `JColiz.AspNetCore.ProblemDetails` - Exception handling
4. `JColiz.Extensions.Logging.Console` - Console logger

**Rationale:** These have zero dependencies on YoFi-specific code and provide immediate reuse value.

**Phase 2: Testing Infrastructure**
1. `JColiz.Testing.Functional` - DataTable helpers
2. `JColiz.Testing.NUnit` - Integration test base classes
3. `JColiz.AspNetCore.Testing` - Test correlation middleware

**Rationale:** Enables consistent testing patterns across multiple applications.

**Phase 3: Multi-Tenancy Framework**
1. `JColiz.AspNetCore.Tenancy` - Core tenancy infrastructure
2. `JColiz.AspNetCore.Tenancy.Application` - Tenant management features

**Rationale:** Largest extraction, requires most careful planning and testing.

**Phase 4: Service Defaults & Templates**
1. `JColiz.ServiceDefaults` - OpenTelemetry configuration
2. `JColiz.Templates.CleanArchitecture` - Project template

**Rationale:** These tie everything together for new project bootstrap.

### 8.2 Migration Path for YoFi.V3

**Step 1: Create NuGet packages** (external repository)
- New GitHub repository: `JColiz.Framework` or similar
- CI/CD pipeline for package publishing
- Comprehensive README and documentation

**Step 2: Reference packages in YoFi.V3**
- Add package references to YoFi.V3 projects
- Update namespaces (e.g., `YoFi.V3.Controllers.Middleware` → `JColiz.AspNetCore.Testing`)
- Remove original code files

**Step 3: Validate with tests**
- Run full test suite (unit, integration, functional)
- Verify no behavioral changes
- Update documentation references

**Step 4: Document migration**
- Update ARCHITECTURE.md with package references
- Add PACKAGES.md listing all JColiz packages used
- Update .roorules if needed

### 8.3 Breaking Changes Management

**Extracted Code Changes:**
- Namespace changes: `YoFi.V3.*` → `JColiz.*`
- Assembly names change
- Some configuration methods may be renamed for clarity

**Mitigation:**
- Provide migration guide with find/replace instructions
- Consider keeping old namespaces as aliases initially
- Version 0.x allows breaking changes before 1.0 stabilization

---

## Part 9: Implementation Roadmap

### Priority 1: Quick Wins (Week 1-2)

- [ ] Create `JColiz.ComponentModel.DataAnnotations` package
  - Extract `NotWhiteSpaceAttribute`, `DateRangeAttribute`
  - Create package project with README
  - Set up CI/CD for package publishing
  - Reference in YoFi.V3, validate tests

- [ ] Create `JColiz.Data.Abstractions` package
  - Extract `IDataProvider`, `IModel`
  - Pure interface package (no dependencies)
  - Reference in YoFi.V3, validate tests

### Priority 2: Infrastructure (Week 3-4)

- [ ] Create `JColiz.AspNetCore.ProblemDetails` package
  - Extract `CustomExceptionHandler`, `ResourceNotFoundException`
  - Include comprehensive documentation
  - Reference in YoFi.V3, validate tests

- [ ] Create `JColiz.Extensions.Logging.Console` package
  - Extract custom console logger
  - Include logging policy documentation (adapted)
  - Reference in YoFi.V3, validate tests

### Priority 3: Testing (Week 5-6)

- [ ] Create `JColiz.Testing.Functional` package
  - Extract `DataTable`, `DataTableExtensions`
  - Add usage examples
  - Reference in YoFi.V3 functional tests, validate

- [ ] Create `JColiz.AspNetCore.Testing` package
  - Extract `TestCorrelationMiddleware`
  - Document integration with test frameworks
  - Reference in YoFi.V3, validate tests

### Priority 4: Multi-Tenancy (Week 7-10)

- [ ] Create `JColiz.AspNetCore.Tenancy` package (LARGE EFFORT)
  - Extract all core tenancy infrastructure
  - Comprehensive documentation (adapt TENANCY.md)
  - Reference in YoFi.V3, extensive testing
  - Security review of tenant isolation

- [ ] Create `JColiz.AspNetCore.Tenancy.Application` package
  - Extract tenant management features
  - Optional package for consumers
  - Reference in YoFi.V3, validate tests

### Priority 5: Templates (Week 11-12)

- [ ] Create `JColiz.Templates.CleanArchitecture` template
  - Project template with full structure
  - Parameter-based customization
  - Test template generation
  - Publish to NuGet templates feed

- [ ] Create `JColiz.ServiceDefaults` package
  - Extract OpenTelemetry configuration
  - Health check setup
  - Reference in YoFi.V3, validate

### Priority 6: Documentation (Ongoing)

- [ ] Create central documentation site
  - Getting started guides
  - API reference documentation
  - Migration guides
  - Best practices

- [ ] Update YoFi.V3 documentation
  - PACKAGES.md listing all JColiz packages
  - Update ARCHITECTURE.md with package references
  - Migration notes in HISTORY.md

---

## Part 10: Success Criteria

### Package Quality Metrics

**Each package must have:**
- [ ] Comprehensive README with usage examples
- [ ] XML documentation on all public APIs
- [ ] CI/CD pipeline with automated testing
- [ ] NuGet package published to public feed
- [ ] Version badge in README
- [ ] GitHub Issues enabled for feedback
- [ ] MIT license clearly stated

### Validation Criteria

**Before declaring a package "production-ready":**
- [ ] Used in YoFi.V3 with all tests passing
- [ ] Used in at least one other project (new application)
- [ ] Documentation reviewed for clarity
- [ ] API surface reviewed for consistency
- [ ] Breaking changes minimized
- [ ] Security review completed (for tenancy package)

### Adoption Metrics

**Track for each package:**
- NuGet download count
- GitHub stars/forks
- Open issues vs resolved
- Time saved when starting new projects

---

## Part 11: Open Questions

### Technical Decisions

1. **Repository Location**
   - Single mono-repo for all JColiz packages?
   - Separate repositories per package?
   - **Recommendation:** Mono-repo initially for easier development, split later if needed

2. **Identity Integration**
   - Tenancy package requires ASP.NET Core Identity
   - Make Identity optional vs required dependency?
   - **Recommendation:** Required for v1, consider abstraction in v2

3. **Database Support**
   - IDataProvider targets EF Core but could support others
   - Document EF Core as "reference implementation"?
   - **Recommendation:** Yes, keep interface generic, provide EF Core implementation

4. **Testing Framework**
   - `JColiz.Testing.NUnit` is NUnit-specific
   - Create similar packages for xUnit, MSTest?
   - **Recommendation:** Start with NUnit (used in YoFi.V3), add others on demand

### Business Decisions

1. **Open Source Strategy**
   - MIT license for all packages?
   - Accept external contributions?
   - **Recommendation:** Yes to both, builds community

2. **Support Model**
   - GitHub Issues for support?
   - Dedicated support channel?
   - **Recommendation:** GitHub Issues, add Discussions for Q&A

3. **Marketing**
   - Blog posts for each package release?
   - Conference talks?
   - **Recommendation:** Yes, increases adoption

---

## Conclusion

This extraction plan identifies **10+ reusable NuGet packages** and **1 project template** that can be extracted from YoFi.V3. These components represent hundreds of hours of development and refinement, providing:

1. **Immediate Development Velocity** - New applications can focus on business logic, not boilerplate
2. **Proven Patterns** - All components are battle-tested in production
3. **Comprehensive Multi-Tenancy** - Complete SaaS framework with Microsoft pattern compliance
4. **Testing Excellence** - Infrastructure for writing maintainable, well-documented tests
5. **Clean Architecture Enforcement** - Templates and patterns that guide developers toward best practices

**Estimated Effort:**
- Package Creation: 8-12 weeks (full-time equivalent)
- Documentation: 2-4 weeks
- Testing/Validation: 2-3 weeks
- Total: 12-19 weeks for complete extraction

**Expected ROI:**
- Each new application saves 4-8 weeks of initial setup
- Multi-tenancy framework alone saves 8-12 weeks
- Consistent patterns across applications reduce maintenance burden
- Shared infrastructure reduces bug count through reuse

**Next Steps:**
1. Review and approve this plan
2. Create JColiz.Framework repository
3. Set up CI/CD pipeline for package publishing
4. Begin Phase 1 extraction (Quick Wins)
5. Iterate based on feedback from first packages

---

**Document History:**
- 2025-12-17: Initial comprehensive extraction plan created
