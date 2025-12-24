# Architecture Overview

## Principles

YoFi.V3 follows **Clean Architecture** principles with clear dependency flow:

```
UI → Controllers → Application → Entities ← Data
```

Dependencies point inward. Inner layers know nothing about outer layers.

## Project Organization

### Core Business Logic

- **Entities** (`src/Entities/`) - Domain models, business rules, and contracts
  - Domain models (`Models/` - WeatherForecast, future financial entities)
  - Repository interfaces (planned for financial data)
  - Configuration options (`Options/` - ApplicationOptions)
  - Entity providers and validation logic
  - No dependencies on other layers

- **Application** (`src/Application/`) - Business logic as Features
  - Feature-based organization (`Features/Weather/WeatherFeature.cs`)
  - Implements use cases and business logic
  - Depends only on Entities interfaces
  - Service registration via `ServiceCollectionExtensions`
  - Tested in isolation (Unit tests)

### API & Infrastructure

- **Controllers** (`src/Controllers/`) - HTTP API endpoints
  - Thin layer handling HTTP concerns (`WeatherController`, `VersionController`)
  - Calls Application Features
  - Returns DTOs/View Models
  - Includes logging and error handling

- **BackEnd** (`src/BackEnd/`) - API Host & Startup Configuration
  - Hosts Controllers and configures the HTTP pipeline
  - Dependency injection setup in `Program.cs`
  - CORS configuration for frontend integration
  - Application Insights and monitoring setup
  - Authentication and authorization with multi-tenancy

- **Data** (`src/Data/`) - Data Access Layer
  - SQLite implementation (`Sqlite/` folder)
  - Entity Framework migrations (`Sqlite.MigrationHost/`)
  - Repository implementations (future)
  - Database context and configuration

### Frontend

- **FrontEnd.Nuxt** (`src/FrontEnd.Nuxt/`) - Vue 3 + Nuxt 4 SPA
  - TypeScript-based Vue.js single-page application
  - Completely decoupled from backend
  - Calls backend via REST API
  - Uses generated TypeScript client for type safety
  - Modern Vue Composition API patterns

### Development & Tooling

- **AppHost** (`src/AppHost/`) - .NET Aspire Orchestration
  - Development-time orchestration of all services
  - Provides unified dashboard and observability
  - Manages service discovery and health checks
  - Hot reload and development workflow

- **WireApiHost** (`src/WireApiHost/`) - API Client Generation
  - Minimal ASP.NET Core host for tooling
  - Generates TypeScript client (`apiclient.ts`) for frontend
  - Ensures type safety between frontend and backend
  - Uses NSwag for OpenAPI/Swagger generation

- **ServiceDefaults** (`src/ServiceDefaults/`) - Shared Configuration
  - Common Aspire service configurations
  - Health checks, telemetry, and observability setup
  - Shared middleware and extension methods
  - Cross-cutting concerns

## Data Flow

### Development Workflow
1. **AppHost** starts all services via Aspire orchestration
2. **BackEnd** API starts with health checks and monitoring
3. **FrontEnd.Nuxt** starts with hot reload capabilities
4. Services communicate through service discovery

### Runtime Application Flow
1. User interacts with **Vue.js components** in browser
2. Frontend calls **BackEnd** REST API using generated TypeScript client
3. **Controllers** receive HTTP requests, delegate to **Application** Features
4. **Application** Features execute business logic using **Entities**
5. **Data** layer handles persistence (SQLite via Entity Framework)
6. Results flow back through layers to update UI

### Build & Deployment Flow
1. **Unit tests** validate Application layer logic
2. **Integration tests** verify Data layer operations
3. **Functional tests** (Playwright) validate end-to-end workflows
4. **Azure Pipelines** builds, tests, and deploys to Azure
5. **Frontend** → Azure Static Web Apps
6. **Backend** → Azure App Service

## Multi-Tenancy Architecture

### Tenant Model (Implementation: "Tenant", UI: "Workspace")
- **Tenant-Scoped Data**: All financial data isolated by tenant ID
- **Role-Based Access**: Owner/Editor/Viewer roles per tenant
- **User Context**: Current tenant context flows through all layers
- **Authorization**: Tenant-scoped policies at API level

### Database Schema
```sql
-- Multi-tenancy foundation
Tenants (Id, Name, IsActive)
UserTenantRoles (UserId, TenantId, Role)

-- Tenant-scoped business entities (future)
Transactions (Id, TenantId, Amount, Description, Source)
Categories (Id, TenantId, Name)
Budgets (Id, TenantId, Month, Amount)
```

## Technology Stack

### Backend (.NET 10)
- **Framework**: ASP.NET Core with minimal APIs
- **Database**: SQLite with Entity Framework Core
- **Authentication**: ASP.NET Core Identity with JWT tokens
- **Monitoring**: Application Insights + Log Analytics
- **Development**: .NET Aspire for orchestration

### Frontend (Vue 3 + TypeScript)
- **Framework**: Nuxt 4 meta-framework
- **Language**: TypeScript for type safety
- **Testing**: Playwright for E2E testing
- **Build**: Vite bundling and hot module replacement

### Infrastructure & DevOps
- **Cloud**: Azure (Static Web Apps + App Service)
- **IaC**: Azure Bicep templates
- **CI/CD**: Azure Pipelines
- **Containerization**: Docker (development & testing only)

## Key Design Decisions

Documented in [Architecture Decision Records](adr/README.md):

### Foundational
- [0001: SPA Web Application](adr/0001-spa-web-app.md) - Single-page app vs server-rendered
- [0002: Vue.js Frontend](adr/0002-vue-js.md) - Vue over React/Angular
- [0003: Nuxt Meta-Framework](adr/0003-nuxt.md) - Nuxt 4 for Vue development
- [0004: Aspire Development](adr/0004-aspire-development.md) - .NET Aspire orchestration

### Infrastructure & Data
- [0005: Database Backend](adr/0005-database-backend.md) - SQLite database, PostgreSql for scale
- [0006: Production Infrastructure](adr/0006-production-infrastructure.md) - Azure deployment
- [0007: Backend Integration](adr/0007-backend-proxy-or-direct.md) - Direct API calls

### Security & Multi-Tenancy
- [0008: Identity System](adr/0008-identity.md) - ASP.NET Core Identity
- [0009: Multi-Tenancy Model](adr/0009-accounts-and-tenancy.md) - Tenant/Workspace architecture

## Testing Strategy

YoFi.V3 follows a **Testing Pyramid** strategy optimized for API-driven applications. See **[TESTING-STRATEGY.md](TESTING-STRATEGY.md)** for complete guidance on mapping PRD acceptance criteria to test layers.

### Test Distribution Target
- **60% Controller Integration tests** - Primary layer for API contract verification, authentication, and authorization
- **25% Unit tests** - Business logic, algorithms, and validation rules
- **15% Functional tests** - Critical end-to-end user workflows through the browser

Controller Integration tests are the sweet spot: they validate complete API contracts including authentication, authorization, and database operations while remaining fast (~100-200ms) and maintainable.

### Test Organization
- **Unit Tests** ([`tests/Unit/`](../tests/Unit/)) - Application layer business logic isolation with mocked dependencies
- **Integration.Data Tests** ([`tests/Integration.Data/`](../tests/Integration.Data/)) - Repository operations and Entity Framework configurations
- **Integration.Controller Tests** ([`tests/Integration.Controller/`](../tests/Integration.Controller/)) - API endpoints with authentication and authorization
- **Functional Tests** ([`tests/Functional/`](../tests/Functional/)) - End-to-end user workflows with Playwright and BDD-style scenarios

All tests use **Gherkin-style documentation** (Given/When/Then) for clear scenario descriptions. See [`.roorules`](../.roorules) for complete testing patterns.

### Current Test Coverage
- ✅ **Application layer** - Comprehensive unit tests approaching 100% coverage
- ✅ **Database operations** - Integration tests for all repository operations
- ✅ **API controllers** - Integration tests with authentication and multi-tenancy
- ✅ **Authentication & authorization** - End-to-end authentication implemented with functional tests in progress
- ✅ **Multi-tenancy** - Complete security and isolation testing
- 📋 **Additional features** - Test coverage expands as features are developed (targeting ~300 total tests for current PRDs)

### Testing Tools
- **Unit**: NUnit with constraint-based assertions and Moq for mocking
- **Integration.Data**: NUnit with SQLite in-memory database
- **Integration.Controller**: NUnit with WebApplicationFactory and test authentication handlers
- **Functional**: Playwright with C# bindings and SpecFlow-style Gherkin scenarios
- **API Client**: NSwag-generated TypeScript client for type-safe frontend integration

## Security Considerations

### Authentication & Authorization
- ✅ **Identity**: ASP.NET Core Identity with JWT tokens (implemented)
- ✅ **Multi-Tenancy**: Role-based authorization with claims (Owner/Editor/Viewer)
- ✅ **Tenant Isolation**: Middleware-enforced tenant context with authorization policies
- ✅ **Security**: 403 responses prevent tenant enumeration attacks
- ✅ **CORS**: Frontend domain configured for cross-origin requests
- ✅ **HTTPS**: Enforced in production environments

See [TENANCY.md](TENANCY.md) for complete multi-tenancy architecture details.

### Data Protection
- **Database**: SQLite+Azure Storage, moving to PostgreSql for scale
- **Secrets**: Azure Key Vault integration planned
- **Logging**: Structured logging with PII filtering
- **Monitoring**: Application Insights for observability

## Scalability Considerations

### Current Scale (MVP)
- **Single-region** deployment
- **SQLite database** for low-volume scenarios
- **Single App Service** instance

### Future Scaling Options
- **Database**: Migration path to PostgreSQL
- **Caching**: Redis for session state and data caching
- **CDN**: Azure Front Door for global distribution
- **Compute**: App Service Plan scaling

## Development Workflow

### Local Development
1. Clone repository with submodules: `git submodule update --init --recursive`
2. Start with Aspire: `dotnet run --project src/AppHost`
3. Access dashboard at `https://localhost:17191`
4. Frontend: `http://localhost:5173` (hot reload)
5. Backend API: `http://localhost:5001` (with live reload)

### Code Organization Patterns
- **Feature-based** organization in Application layer
- **Clean Architecture** dependency rules enforced
- **Repository pattern** interfaces in Entities
- **Dependency injection** via extension methods
- **Configuration** via strongly-typed options pattern

This architecture supports both rapid development and future scalability while maintaining clear separation of concerns and testability.
