# Architecture Overview

## Principles

YoFi.V3 follows **Clean Architecture** principles with clear dependency flow:

```
UI → Controllers → Application → Entities
```

Dependencies point inward. Inner layers know nothing about outer layers.

## Project Organization

### Core Business Logic

- **Entities** (`src/Entities/`) - Data models and repository interfaces
  - Pure data structures (records, interfaces)
  - No dependencies on other layers
  - Defines contracts (interfaces) for data access

- **Application** (`src/Application/`) - Business logic as Features
  - Implements use cases
  - Depends only on Entities
  - Tested in isolation (Unit tests)
  - Each feature is self-contained

### API Layer

- **Controllers** (`src/Controllers/`) - HTTP API endpoints
  - Thin layer - just HTTP concerns
  - Calls Application Features
  - Returns DTOs/View Models
  - Adds logging/error handling

- **BackEnd** (`src/BackEnd/`) - API Host
  - Hosts Controllers
  - Configures middleware, DI, etc.
  - Entry point for API

### Frontend

- **FrontEnd.Nuxt** (`src/FrontEnd.Nuxt/`) - Vue/Nuxt SPA
  - Completely separate from backend
  - Calls backend via REST API
  - Uses generated TypeScript client

### Development Tools

- **AppHost** (`src/AppHost/`) - .NET Aspire orchestration
  - Runs entire stack in development
  - Provides dashboard and observability

- **WireApiHost** (`src/WireApiHost/`) - TypeScript generator
  - Minimal host for API client generation
  - Generates `apiclient.ts` for frontend

- **ServiceDefaults** (`src/ServiceDefaults/`) - Shared Aspire config
  - Health checks, telemetry, service discovery

## Data Flow

1. User interacts with **FrontEnd.Nuxt** (Vue components)
2. Frontend calls **BackEnd** REST API (generated TS client)
3. **Controllers** receive request, call **Application** Feature
4. **Application** Feature executes business logic
5. **Application** returns result to Controller
6. **Controller** formats HTTP response
7. Frontend updates UI

## Key Design Decisions

See [Architecture Decision Records](adr/README.md):
- [SPA vs Server-Rendered](adr/0001-spa-web-app.md)
- [Vue.js Frontend](adr/0002-vue-js.md)
- [Nuxt Meta-Framework](adr/0003-nuxt.md)
- [Aspire Development](adr/0004-aspire-development.md)

## Testing Strategy

- **Unit Tests** (`tests/Unit/`) - Application layer business logic
- **Functional (E2E) Tests** (future) - Full UI workflows

### Under consideration

Also considering Claude's recommendation to include these:

- **Integration Tests** (future) - Database/external service integration
- **API Tests** (future) - Contract testing for REST API