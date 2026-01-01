# 0010. Backend For Frontend Pattern

Date: 2026-01-01

## Status

In review

## Context

What philosophy governs the interaction between frontend and backend?

### Technical Constraints

The project has stronger technical depth in C# and .NET than in TypeScript and Vue. Concentrating business logic and complexity in the backend leverages this expertise, resulting in more robust, stable, and well-architected code. This constraint favors placing as much logic as possible in the backend Application layer where it can be better maintained and tested.

## Decision

YoFi.V3 backend is a dedicated backend service, built from the ground up, tailored to the specific needs of the frontend.

- The backend exists solely to serve the YoFi.V3 specific frontend
- Its API shape is optimized for the frontend’s UX
- It evolves at the pace of the frontend
- It is not intended for reuse by other clients

### Details

- To the extent possible, business logic and complexity is concentrated in the backend -- ideally, the Application layer.
- DTOs are precisely tailored for what the user will want to see, or need to send at any given moment.
- Application logic shapes database queries directly into (or out of) the same DTOs visible to the front-end.

### Excluded concerns

- This is not a public API for third-party consumption
- This is not designed for mobile apps, CLI tools, or other clients
- API stability guarantees beyond N-1 compatibility are minimal (frontend and backend evolve together in coordinated releases)

## Consequences

### What gets easier

- **Simplified frontend** - Frontend focuses on presentation logic without duplicating business rules
- **Optimized data shape** - DTOs match exact frontend needs, reducing client-side transformation
- **Centralized validation** - Business rules enforced in one place (backend Application layer)
- **Faster iteration** - Frontend and backend can evolve together without API contract negotiations
- **Better testability** - Complex logic tested in C#/.NET with mature testing tools

### What gets harder

- **Tight coupling** - Frontend and backend evolve together in the same release cycle, though N-1 compatibility allows gradual rollout
- **Reusability concerns** - If multiple frontends are needed in future, logic may need duplication or extraction
- **Coordinated changes** - Frontend changes typically require corresponding backend changes in the same release (though backend can adapt behavior based on frontend version if needed)
- **Team coordination** - Full-stack changes required for most features (cannot parallelize frontend/backend work easily)

## Alternatives Considered

### GraphQL for Flexible Queries

GraphQL is typically advantageous when frontend teams have strong expertise and need flexible data fetching capabilities while keeping the backend simple. Given the project's technical constraints (stronger backend expertise than frontend), this approach would not leverage the team's strengths effectively.

### Generic/Reusable REST API

A generic API designed for multiple clients is prone to over-fetching, unnecessary complexity, and bloated design. This approach optimizes for future reuse scenarios that may never materialize. The preferred strategy is to start specific and expand to generic patterns only when multiple concrete use cases are identified.

### Microservices Architecture

The project is being built from scratch without existing microservices that could readily support domain-specific requirements. Given the system's current simplicity, decomposing into microservices would add operational complexity without clear architectural benefits.

### Shared API for Multiple Client Types

Designing for multiple client types (mobile apps, CLI tools, third-party integrations) would be speculative at this stage. The current requirement is a single web frontend. If additional client types are needed in the future, appropriate refactoring can be undertaken with concrete requirements in hand. This avoids over-engineering for hypothetical scenarios.

## Versioning and Compatibility

### Version Support Policy

- **N-1 Compatibility:** Backend must maintain compatibility with the previous frontend version (N-1) indefinitely
- **Never Break N-1:** Under no circumstances should a backend deployment break N-1 frontend functionality
- **Forward Compatibility Not Guaranteed:** Backend is NOT required to support frontends that are newer than itself (N+1, N+2, etc.). Frontend should always expect backend to be at the same version or newer.
- **Version Format:** Semantic versioning (major.minor.patch)
- **Version Communication:**
  - Backend exposes `/version` endpoint for informational purposes
  - Frontend sends version in request header on every API call (specific header name to be designed during implementation)
  - Backend includes response header indicating refresh required when frontend is N-2 or older
- **Version Checking:** Enforced at runtime (not build time)

**Deployment Implications:**

The asymmetric compatibility guarantee (backend supports old frontends, but not vice versa) means:
- Backend can be deployed independently ahead of frontend (N-1 frontends continue working)
- Frontend should NOT be deployed ahead of backend (would create unsupported N+1 scenario)
- Typical deployment order: Backend first, then frontend (ensures compatibility throughout rollout)

### Breaking Change Strategy

**Definition of Breaking Change:**
- Inability for a frontend to deliver a scenario to the user
- Examples: Removing a DTO field, removing an endpoint, changing required parameters
- Non-breaking: Adding new fields, adding new optional parameters

**Migration Pattern for Breaking Changes:**
1. **Release N:** Mark field/endpoint as obsolete, maintain functionality
2. **Release N+1:** Remove obsolete field/endpoint (now N-1 frontend no longer uses it)

**Version-Aware Backend Behavior:**

When necessary, backend can adapt response or behavior based on frontend version detected in request headers. This provides an escape hatch for situations where N and N-1 frontends require different responses from the same endpoint.

**Example scenarios:**
- Return different DTO shapes based on frontend version (though prefer additive changes)
- Apply different business logic for deprecated vs. current behavior
- Include/exclude fields based on frontend capability

**Note:** This should be used sparingly. The preferred approach is additive changes that work for both N and N-1 frontends. Version-specific logic should only be added when absolutely necessary to maintain compatibility during migration periods.

### Frontend Version Detection and Auto-Refresh

**Mechanism:**
- Frontend sends its version in a request header on every API call
- Backend inspects frontend version and determines if refresh is needed
- If frontend is N-2 or older, backend includes a response header indicating refresh is required
- Frontend monitors response headers (via HTTP interceptor) and triggers refresh when header is present

**Key Insight - No Grace Period Required:**

The N-1 compatibility policy creates a natural deployment buffer that eliminates race conditions:

**Example deployment sequence:**
1. **Field state:** Frontend v1.1.0 + Backend v1.1.0 (stable)
2. **Deploy Backend v1.1.1** → N-1 frontends (v1.1.0) continue working, no refresh triggered
3. **Deploy Frontend v1.1.1** → Assets propagate to CDN
4. **Deploy Backend v1.1.2** → N-2 frontends (v1.1.0) now receive refresh header
5. **v1.1.0 refreshes** → Receives v1.1.1 from CDN (guaranteed available from step 3)

By the time a frontend becomes N-2 (triggering refresh), there's always a guaranteed-available N-1 frontend in the CDN because it was deployed in the previous release cycle. No grace period, deployment ordering constraints, or race condition handling is needed - the system is self-regulating.

**Benefits:**
- Server-controlled policy: Backend determines compatibility requirements
- Works across minor version boundaries: Backend can specify minimum supported version regardless of semver gaps
- Every request is checked: Users get refresh signal immediately
- Shifts complexity to backend: Leverages stronger backend expertise

### Deployment Model

**Deployment sequence:**
- Deploy to staging environment first
- Run functional tests against staging
- Perform ad-hoc release testing (to be defined)
- Promote to production after validation
- No blue-green deployment (resource constraints)
- Rollback: Revert changes and deploy previous version as N+1

**Testing N-1 Compatibility:**

*For consideration:* Since Docker containers are published for each release, it would be feasible to add automated N-1 compatibility testing to the CI/CD pipeline. The pipeline could:
- Pull the previous frontend container (N-1)
- Deploy it against the current backend (N)
- Run functional tests to verify N-1 frontend still works with N backend

This would provide concrete validation of the N-1 compatibility guarantee before production deployment. The Docker container approach makes this relatively straightforward to implement compared to maintaining separate frontend builds or version branches.

**Automated rollback triggers:**
- Not implemented due to resource constraints
- Automated rollback requires pre-deployed standby infrastructure (blue-green deployment model)
- Without standby infrastructure, rollback requires revert + redeploy (same timeline as forward deployment)

**Database migrations:**
- Database schema changes must follow N-1 compatibility guarantee
- Avoid N-1 breaking migrations whenever possible
- Migration strategy: Additive changes in release N, remove obsolete schema in release N+1

**Maintenance mode:**
- Not planned for normal operations
- N-1 compatibility policy should eliminate need for maintenance windows
- Catastrophic scenarios (if they occur) will be handled ad-hoc

**Open issues:**
- How long should staging be validated before production promotion?
- What constitutes sufficient "ad-hoc release testing"? (Checklist to be defined)
