# 0011. Clean Architecture Pattern

Date: 2026-01-01

## Status

Accepted

## Context

### Background

YoFi.V3 is a complete rewrite of the [YoFi personal finance application](https://github.com/jcoliz/yofi). Starting from scratch provided an opportunity to evaluate architectural patterns and select the best foundation for a modern, maintainable application.

Requirements for the architectural pattern:
- Clear separation between business rules and infrastructure
- Testability at all layers
- Independence from UI frameworks and databases
- Ability to defer infrastructure decisions
- Support for multiple clients (web, future mobile app)

### Architectural Pattern Alternatives

**1. N-Tier/Layered Architecture** - Traditional three-tier with strict layering but no dependency inversion. Business logic depends on data layer, causing database coupling and testing difficulties.

**2. Hexagonal Architecture (Ports and Adapters)** - Domain isolated by interfaces. Conceptually similar to Clean Architecture but uses less familiar terminology for .NET developers.

**3. Vertical Slice Architecture** - Features contain own controller, logic, and data. Causes duplication of shared concerns like multi-tenancy. Better for microservices than monoliths.

**4. Onion Architecture** - Emphasizes domain model at center. Nearly identical to Clean Architecture, just less recognized in .NET community with fewer examples available.

**5. Feature Folders (Simple Structure)** - Flat organization by feature with minimal constraints. No dependency rules or testability. Would recreate problems from original YoFi codebase.

## Decision

YoFi.V3 adopts **Clean Architecture** principles with the following layer structure:

### Layer Definitions

```
UI → Controllers → Application → Entities ← Data
```

Dependencies flow inward. Outer layers depend on inner layers. Inner layers have no knowledge of outer layers.

#### 1. Entities
**The Core Domain Layer** (`src/Entities/`)

Contains:
- Domain models
- Data access interfaces (IDataProvider for queryable table access, ITenantRepository for special tenancy operations)
- Domain exceptions

**Rules:**
- Zero dependencies on other layers
- No knowledge of databases, frameworks, or UI
- Contains ONLY domain concepts and contracts
- Framework-agnostic (pure C# with minimal framework dependencies)

#### 2. Application
**The Business Logic Layer** (`src/Application/`)

Contains:
- Feature implementations
- DTOs for data transfer
- Business workflows and use cases
- Validation logic

**Rules:**
- Depends ONLY on Entities layer (interfaces and models)
- No knowledge of Controllers, databases, or UI frameworks
- Orchestrates business logic using IDataProvider for direct queryable access
- Contains no infrastructure concerns

#### 3. Controllers
**The API Boundary Layer** (`src/Controllers/`)

Contains:
- All ASP.NET concerns
- HTTP endpoint implementations
- Request/response handling
- Logging (API boundary only)
- Error handling and HTTP status code mapping
- OpenAPI/Swagger documentation

**Rules:**
- Depends on Application layer (Features) and Entities layer (exceptions)
- Thin layer - delegates all business logic to Application Features
- Handles ONLY HTTP concerns (routing, status codes, authentication, logging)
- No business logic in controllers

#### 4. Data
**The Infrastructure Layer** (Database-specific implementations)

Contains:
- All Entity Framework concerns (e.g. DbContext and configurations)
- Database migrations
- IDataProvider implementation (exposes IQueryable<T> for each table)
- Special-case implementations (ITenantRepository for tenancy operations)

**Rules:**
- Depends on Entities layer (implements data access interfaces)
- No knowledge of Controllers or Application layer
- Handles ONLY data persistence concerns

**Current Implementations:**
- Data.Sqlite (src/Data/Sqlite/) - SQLite-specific DbContext and migrations
- Future: Data.Postgres - PostgreSQL implementation when scaling requires it

#### 5. BackEnd
**The Host Layer** (`src/BackEnd/`)

Contains:
- Application startup and configuration
- Dependency injection registration
- Middleware pipeline setup
- Authentication and authorization configuration
- CORS policies
- Environment-specific settings

**Rules:**
- References all other projects to wire them together
- Contains no business logic
- Purely composition root

### Dependency Inversion Principle

The key architectural principle is **Dependency Inversion**:

> High-level modules should not depend on low-level modules. Both should depend on abstractions.

**How YoFi.V3 Implements This:**

1. **Application Features depend on IDataProvider interface** (defined in Entities), NOT on Entity Framework
2. **Tenancy logic depends on ITenantRepository interface** (defined in Entities), NOT on database implementation
3. **Controllers depend on Feature classes** (Application), NOT on data access
4. **Entities layer has zero dependencies** - it's the stable core

### Clean Architecture + BFF Pattern

YoFi.V3 combines Clean Architecture with the **Backend for Frontend (BFF)** pattern:

- **DTOs are precisely tailored** for frontend needs (see [ADR 0010: Backend for Frontend](0010-backend-for-frontend.md))
- **Business logic concentrated in Application layer**
- **Controllers are thin** - just HTTP plumbing
- **Frontend gets optimized data shapes** without client-side transformation

## Key Architectural Patterns

**Data Access Pattern (NOT Repository Pattern):**
- IDataProvider<T> exposes IQueryable<T> for direct LINQ queries
- Application Features write queries without intermediate repository methods
- Only tenancy (ITenantRepository) needs special operations beyond queryable access
- Avoids repository pattern boilerplate and repetition

**Feature Pattern:**
- Each business capability is a Feature class (TransactionsFeature, TenantFeature)
- Features use constructor injection to get IDataProvider
- Features return DTOs, not entities

**Dependency Injection:**
- ServiceCollectionExtensions in each layer register dependencies
- Controllers → Application → Entities registration chain
- Interfaces resolved to implementations at runtime

## Consequences

### Easier

- Features testable in isolation via mocked interfaces
- Business logic has no Entity Framework dependencies
- Can swap SQLite for PostgreSQL without changing Features
- Each layer has single, well-defined responsibility
- Changes isolated to appropriate layer
- Easy to locate code by feature
- Application layer reusable in CLI tools, background jobs, or different APIs
- Can defer infrastructure decisions (database, authentication)

### More Complex

- New developers must understand layer boundaries and dependency rules
- DTOs separate from entities requiring more files
- More layers to trace through when debugging
- Creating interfaces, DTOs, and feature classes takes time

## Future Enhancements

### Services Projects

YoFi.V3 currently has no external service integrations beyond database access. When external services are added, each will be implemented in its own project following Single Responsibility Principle. (e.g. `Services.Email`, `Services.Storage`).

Each service is kept separate rather than combined into a monolithic Infrastructure project, making them independently testable and swappable.

### Additional Database Implementations

- Add database-specific Data projects as needed (Data.Postgres for scaling)

## References

**Clean Architecture Resources:**
- Robert C. Martin, "Clean Architecture: A Craftsman's Guide to Software Structure and Design"
- [Clean Architecture Blog Post](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [Microsoft .NET Application Architecture](https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures#clean-architecture)
