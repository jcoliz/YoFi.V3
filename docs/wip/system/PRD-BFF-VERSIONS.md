---
status: Draft
target_release: Beta 2
design_document: TBD
ado: TBD
related_adr: docs/adr/0010-backend-for-frontend.md
---

# Product Requirements Document: Frontend/Backend Version Compatibility

## Problem Statement

When frontend and backend are deployed independently in a BFF architecture, users can be left running outdated frontend versions that are no longer compatible with the current backend. Without automatic detection and refresh, users experience broken functionality until they manually clear cache or hard-refresh their browser. We need a server-controlled mechanism to detect version mismatches and automatically refresh outdated clients.

---

## Goals & Non-Goals

### Goals
- [ ] Backend can detect which frontend version made each API request
- [ ] Backend can signal when frontend needs to refresh (N-2 or older)
- [ ] Frontend automatically refreshes when signaled by backend
- [ ] No user intervention required for version updates
- [ ] Version detection works on every API call (immediate feedback)
- [ ] Informational version endpoint for troubleshooting

### Non-Goals
- Grace period or deployment timing coordination (N-1 policy eliminates race conditions)
- Forward compatibility (backend supporting newer frontends)
- Build-time version checking (all checks happen at runtime)
- Blue-green deployment infrastructure (resource constraints)
- Automated rollback triggers (requires standby infrastructure)

---

## User Stories

### Story 1: Developer - Deploy Backend Without Breaking Users

**As a** developer deploying a new backend version
**I want** existing frontend users (N-1) to continue working
**So that** I can deploy backend independently without coordinating with frontend deployment timing

**Acceptance Criteria**:
- [ ] Backend N deployment does not break frontend N-1 functionality
- [ ] API endpoints maintain N-1 compatible behavior
- [ ] DTOs maintain N-1 compatible structure (additive changes only)
- [ ] Backend can distinguish between frontend N and N-1 requests

### Story 2: User - Automatic Update to Latest Frontend

**As a** user running an outdated frontend (N-2 or older)
**I want** the application to automatically refresh to the latest version
**So that** I don't have to manually clear cache or troubleshoot broken functionality

**Acceptance Criteria**:
- [ ] On any API call, if frontend is N-2 or older, backend includes refresh header
- [ ] Frontend HTTP interceptor detects refresh header
- [ ] Frontend triggers page reload automatically
- [ ] User receives latest frontend from CDN after reload
- [ ] Refresh happens seamlessly without error messages
- [ ] User's current page state is lost (acceptable - rare scenario)

### Story 3: Operations - Troubleshoot Version Mismatch Issues

**As an** operations engineer troubleshooting production issues
**I want** to query backend and frontend versions independently
**So that** I can verify version compatibility and diagnose deployment problems

**Acceptance Criteria**:
- [ ] Backend exposes `/version` endpoint returning version information
- [ ] Endpoint returns semantic version (major.minor.patch)
- [ ] Endpoint accessible without authentication
- [ ] Frontend includes version in all API request headers
- [ ] Version information visible in browser dev tools (network tab)

### Story 4: Developer - Implement Breaking Changes Safely

**As a** developer making a breaking API change
**I want** to follow N-1 deprecation pattern
**So that** I can evolve the API without breaking existing users

**Acceptance Criteria**:
- [ ] Release N marks field/endpoint as obsolete, maintains functionality
- [ ] Release N+1 removes obsolete field/endpoint (N-1 no longer uses it)
- [ ] Backend can adapt response based on frontend version if absolutely necessary
- [ ] Version-specific logic is temporary (removed in N+1)

---

## Technical Approach

**Layers Affected**:
- [x] Frontend (Vue/Nuxt) - HTTP interceptor, version header injection
- [x] Controllers (API endpoints) - Version endpoint, middleware integration
- [ ] Application (Features/Business logic) - No changes needed
- [ ] Entities (Domain models) - No changes needed
- [ ] Database (Schema changes) - No changes needed

**Key Components**:

1. **Version Detection (Backend)**
   - Middleware inspects incoming request header for frontend version
   - Compares frontend version against current backend version
   - Determines if refresh is required (frontend is N-2 or older)

2. **Version Signaling (Backend)**
   - Backend includes response header when refresh is required
   - Header name and value to be determined during implementation
   - Header included on every response when condition is met

3. **Auto-Refresh (Frontend)**
   - HTTP interceptor checks response headers on every API call
   - Detects refresh signal header
   - Triggers `window.location.reload()` to fetch latest frontend from CDN

4. **Version Endpoint (Backend)**
   - Informational endpoint: `GET /version`
   - Returns JSON with version information
   - No authentication required (public endpoint)

**Version Header Specification** (to be determined):
- Frontend sends version in request header (e.g., `X-Frontend-Version: 1.2.3`)
- Backend responds with refresh signal when needed (e.g., `X-Refresh-Required: true`)

**Version Format**:
- Semantic versioning: `major.minor.patch`
- Version embedded at build time from Git tags or CI/CD pipeline
- Version comparison logic: Compare semantic version components

**Key Business Rules**:

1. **N-1 Compatibility Guarantee** - Backend MUST maintain compatibility with frontend version N-1. Under no circumstances should a backend deployment break N-1 frontend functionality.

2. **N-2 Triggers Refresh** - When frontend version is N-2 or older, backend includes refresh header. Frontend reloads automatically on next API call.

3. **No Forward Compatibility Required** - Backend is NOT required to support frontends newer than itself (N+1, N+2). Frontend should always expect backend to be at same version or newer.

4. **Version-Aware Response Adaptation** - When necessary (sparingly), backend can adapt response or behavior based on frontend version detected in request headers. This provides an escape hatch for situations where N and N-1 frontends require different responses from the same endpoint.

5. **Breaking Change Migration Pattern**:
   - Release N: Mark field/endpoint as obsolete, maintain functionality
   - Release N+1: Remove obsolete field/endpoint (N-1 no longer uses it)

**Deployment Implications**:

The asymmetric compatibility guarantee means:
- ✅ Backend can be deployed independently ahead of frontend (N-1 frontends continue working)
- ❌ Frontend should NOT be deployed ahead of backend (would create unsupported N+1 scenario)
- ✅ Typical deployment order: Backend first, then frontend (ensures compatibility throughout rollout)

**Self-Regulating System** (No Grace Period Needed):

The N-1 compatibility policy creates a natural deployment buffer:
1. Field state: Frontend v1.1.0 + Backend v1.1.0 (stable)
2. Deploy Backend v1.1.1 → N-1 frontends (v1.1.0) continue working, no refresh triggered
3. Deploy Frontend v1.1.1 → Assets propagate to CDN
4. Deploy Backend v1.1.2 → N-2 frontends (v1.1.0) now receive refresh header
5. v1.1.0 refreshes → Receives v1.1.1 from CDN (guaranteed available from step 3)

By the time a frontend becomes N-2 (triggering refresh), there's always a guaranteed-available N-1 frontend in the CDN because it was deployed in the previous release cycle.

**Code Patterns to Follow**:
- Middleware pattern: [`TenantContextMiddleware.cs`](../../src/Controllers/Tenancy/Context/TenantContextMiddleware.cs)
- Informational endpoints: [`VersionController.cs`](../../src/Controllers/VersionController.cs) (if exists, otherwise create new)
- Frontend HTTP interceptor: Existing API client patterns in [`apiclient.ts`](../../src/FrontEnd.Nuxt/app/utils/apiclient.ts)

---

## Open Questions

- [ ] **Header names**: What should the request/response header names be? (`X-Frontend-Version` / `X-Refresh-Required`?)
- [ ] **Version source**: Where does version come from at build time? Git tags? CI/CD variables? `package.json`?
- [ ] **Version endpoint detail**: What additional information should `/version` endpoint return beyond version string?
- [ ] **Frontend version display**: Should frontend version be visible in UI for troubleshooting? Where?
- [ ] **Refresh UX**: Should we show a brief notification before reload, or just reload immediately?
- [ ] **Testing N-1 compatibility**: Should we add automated CI testing of N-1 frontend against N backend using Docker containers?

---

## Success Metrics

**Operational Metrics**:
- Zero N-1 compatibility breaks in production (target: 100%)
- Average time for outdated clients to refresh after backend deployment (target: <5 minutes)
- % of users manually clearing cache (target: <1% - should be unnecessary)

**Monitoring**:
- Track frontend version distribution in backend logs (which versions are hitting the API)
- Alert on unexpected version patterns (e.g., N-3 or older appearing in logs)
- Track refresh header response rate (how many users triggered auto-refresh)

---

## Dependencies & Constraints

**Dependencies**:
- Version information available at build time (Git tags or CI/CD variables)
- Frontend assets deployed to CDN before backend triggers refresh
- HTTP interceptor infrastructure in frontend API client

**Constraints**:
- No blue-green deployment (resource constraints)
- No automated rollback (requires standby infrastructure)
- Database migrations must follow N-1 compatibility pattern
- Rollback requires revert + redeploy (same timeline as forward deployment)

---

## Notes & Context

This PRD implements the version compatibility requirements defined in [ADR 0010: Backend For Frontend Pattern](../../docs/adr/0010-backend-for-frontend.md).

**Key Insight**: The N-1 compatibility policy eliminates race conditions and removes the need for grace periods or deployment timing coordination. The system is self-regulating - by the time a frontend needs to refresh, the new frontend is guaranteed to be available in the CDN.

**Rationale for Server-Controlled Refresh**:
- Leverages stronger backend expertise (simpler to implement version logic in C# than TypeScript)
- Backend has authoritative view of compatibility requirements
- Works across minor version boundaries (backend specifies minimum supported version regardless of semver gaps)
- Every API call is checked (immediate detection, not periodic polling)

**Related Documents**:
- [ADR 0010: Backend For Frontend Pattern](../../docs/adr/0010-backend-for-frontend.md) - Architectural decision and N-1 compatibility policy
- [DEPLOYMENT.md](../../docs/DEPLOYMENT.md) - Deployment procedures and sequences

---

## Handoff Checklist (for AI implementation)

When handing this off for detailed design/implementation:
- [x] Document stays within PRD scope (WHAT/WHY). Implementation details belong in separate Design Document. See [`PRD-GUIDANCE.md`](../PRD-GUIDANCE.md).
- [x] All user stories have clear acceptance criteria
- [ ] Open questions are resolved or documented as design decisions
- [x] Technical approach section indicates affected layers
- [x] Code patterns to follow are referenced (links to similar controllers/features)
- [ ] Companion design document created if needed (may be needed for detailed middleware/interceptor implementation)
