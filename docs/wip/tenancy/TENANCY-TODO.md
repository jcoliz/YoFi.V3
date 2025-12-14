# Tenancy Remaining Work

**Last Updated:** 2025-12-14
**Status:** Core tenancy features production-ready; working toward 100% Microsoft pattern compliance
**Goal:** Achieve full compliance with [Microsoft Multi-tenant SaaS patterns](https://learn.microsoft.com/en-us/azure/architecture/guide/multitenant/considerations/tenancy-models)

## Overview

The core tenancy implementation (isolation, authorization, security) is complete and production-ready with excellent alignment to Microsoft patterns. Remaining work focuses on achieving 100% compliance with Microsoft's multi-tenancy recommendations, particularly in lifecycle management, audit logging, and documentation.

For detailed compliance analysis, see [TENANCY-MICROSOFT-PATTERNS-ANALYSIS.md](TENANCY-MICROSOFT-PATTERNS-ANALYSIS.md).

## Microsoft Pattern Compliance Items

These items are required to achieve 100% compliance with Microsoft's multi-tenancy architectural guidance.

### Critical for Microsoft Compliance

#### 1. Tenant Lifecycle Management (Soft Delete)

**Microsoft Guidance:** "Plan for tenant onboarding, suspension, reactivation, and deletion. Implement soft delete where appropriate."

**Current State:** Properties scaffolded but commented out in [`Tenant.cs`](../../../src/Entities/Tenancy/Tenant.cs:30-34)

**Required Implementation:**
- [ ] Activate `IsActive`, `DeactivatedAt`, `DeactivatedByUserId` properties in [`Tenant`](../../../src/Entities/Tenancy/Tenant.cs)
- [ ] Add database migration for new columns
- [ ] Add index on `Tenant.IsActive` for query performance
- [ ] Update all tenant queries to filter by `IsActive = true`
- [ ] Implement `POST /api/tenant/{tenantKey}/deactivate` endpoint
- [ ] Implement `POST /api/tenant/{tenantKey}/reactivate` endpoint
- [ ] Add business rules:
  - Only Owners can deactivate/reactivate
  - Cannot deactivate if user's only tenant (prevent lock-out)
  - Deactivated tenants return 403 Forbidden (maintain enumeration prevention)
- [ ] Add functional tests for activation lifecycle
- [ ] Document soft delete behavior in [`TENANCY.md`](../../TENANCY.md)

**Priority:** High (required for full Microsoft compliance)

#### 2. Audit Logging for Tenant Access

**Microsoft Guidance:** "Document isolation boundaries. Implement audit logging. Support data export/deletion for GDPR compliance."

**Current State:** Basic structured logging exists, no tenant-specific audit trail

**Required Implementation:**
- [ ] Create `ITenantAuditLogger` interface
- [ ] Create audit log entity with:
  - Tenant ID, User ID, Action, Timestamp, IP Address, Details
- [ ] Add database migration for audit table
- [ ] Implement audit logging for:
  - Tenant access (successful and failed)
  - Tenant CRUD operations
  - User role changes
  - Tenant activation/deactivation
- [ ] Add `GET /api/tenant/{tenantKey}/audit-log` endpoint (Owner-only)
- [ ] Add retention policy configuration for audit logs
- [ ] Add unit tests for audit logger
- [ ] Add integration tests for audit log API
- [ ] Document audit logging in [`TENANCY.md`](../../TENANCY.md)

**Priority:** High (compliance and security requirement)

#### 3. Document Scalability Migration Path

**Microsoft Guidance:** "For shared schema, monitor database size. Plan for vertical scaling, read replicas, or sharding if tenant growth is unbounded."

**Current State:** SQLite suitable for development/small scale, no documented migration path

**Required Documentation:**
- [ ] Create `docs/wip/tenancy/TENANCY-SCALABILITY-GUIDE.md` covering:
  - SQLite → PostgreSQL migration steps
  - SQLite → SQL Server migration steps
  - Connection pooling configuration
  - Read replica setup patterns
  - Caching strategies (tenant metadata)
  - Performance monitoring guidance
  - When to consider database-per-tenant model
- [ ] Add scalability section to [`TENANCY.md`](../../TENANCY.md)
- [ ] Document composite index recommendations for common query patterns
- [ ] Provide example `CachedTenantRepository` implementation

**Priority:** High (documentation requirement for production use)

#### 4. Caching Pattern Documentation

**Microsoft Guidance:** "Use caching for tenant metadata. Consider distributed cache for multi-instance deployments. Cache per tenant, not globally."

**Current State:** No caching layer (appropriate for library), but patterns not documented

**Required Documentation:**
- [ ] Add caching patterns section to [`TENANCY.md`](../../TENANCY.md)
- [ ] Provide example `CachedTenantRepository` wrapper implementation
- [ ] Document distributed cache considerations (Redis, Azure Cache)
- [ ] Document cache key patterns (e.g., `tenant:{key}`)
- [ ] Document cache invalidation strategies
- [ ] Provide example integration with `IMemoryCache` and `IDistributedCache`

**Priority:** Medium (documentation for users to implement)

### Compliance Enhancements

#### 5. Data Export for GDPR Compliance

**Microsoft Guidance:** "Support tenant data export (GDPR Article 20). Implement complete data deletion (GDPR Article 17)."

**Current State:** Deletion supported via cascade, no export functionality

**Required Implementation:**
- [ ] Document data export pattern in [`TENANCY.md`](../../TENANCY.md)
- [ ] Provide example `ExportTenantDataAsync()` implementation
- [ ] Document data retention policies
- [ ] Add guidance for implementing GDPR-compliant export (JSON, CSV formats)
- [ ] Document complete deletion verification process

**Priority:** Medium (application-level concern, provide guidance)

#### 6. Tenant Configuration System

**Microsoft Guidance:** "Support per-tenant configuration where needed. Store tenant settings separately from shared configuration."

**Current State:** Minimal tenant metadata (name, description only)

**Required Implementation (if needed):**
- [ ] Design tenant configuration schema (JSON column vs separate table)
- [ ] Implement `TenantConfiguration` entity/table
- [ ] Add configuration CRUD endpoints
- [ ] Add validation for configuration keys/values
- [ ] Document configuration patterns
- [ ] Consider separate package: `JColiz.MultiTenant.Configuration`

**Priority:** Low (implement when specific per-tenant settings needed)

## High Priority

### 1. Complete Tenant Management API Implementation

**Current State:** [`TenantController`](../../src/Controllers/Tenancy/TenantController.cs) has working CRUD operations via [`TenantFeature`](../../src/Controllers/Tenancy/TenantFeature.cs)

**Status:** ✅ Complete - All basic tenant CRUD operations implemented:
- ✅ `GET /api/user/tenants` - List tenants for user
- ✅ `GET /api/tenant/{tenantKey}` - Get tenant details
- ✅ `POST /api/user/tenants` - Create tenant
- ✅ `PUT /api/tenant/{tenantKey}` - Update tenant
- ✅ `DELETE /api/tenant/{tenantKey}` - Delete tenant

**Remaining:**
- Add validation for tenant names/descriptions
- Add input sanitization for tenant data
- Consider tenant name uniqueness constraints per owner

### 2. User Role Management Endpoints

**Current State:** Repository methods exist in [`ITenantRepository`](../../src/Entities/Tenancy/ITenantRepository.cs), but no API endpoints

**Need to Implement:**
- `POST /api/tenant/{tenantKey}/users` - Invite user to tenant
- `PUT /api/tenant/{tenantKey}/users/{userId}/role` - Change user's role
- `DELETE /api/tenant/{tenantKey}/users/{userId}` - Remove user from tenant
- `GET /api/tenant/{tenantKey}/users` - List all users in tenant

**Requirements:**
- Only Owners can manage roles
- Cannot remove the last Owner from a tenant
- Validate role transitions (no direct Viewer → Owner without Editor)

### 3. Functional Tests for Tenant API

**Current State:** Integration tests exist for middleware and authorization, but no end-to-end functional tests

**Need to Implement:**
- Functional tests for tenant CRUD operations
- User role management workflow tests
- Cross-tenant access validation
- Multi-user collaboration scenarios
- Error handling and edge cases

**Implementation:**
- Use functional test framework (similar to other API tests)
- Test against actual deployed service
- Validate JWT claims and authorization flow
- Test tenant switching and multi-tenant scenarios

### 4. Unit Tests for Authorization Logic

**Current State:** Comprehensive integration tests exist, no unit tests for authorization components

**Need Tests For:**
- [`TenantRoleHandler`](../../src/Controllers/Tenancy/TenantRoleHandler.cs) with mock claims principal
- Policy requirement validation
- Edge cases (invalid GUIDs, malformed claims, missing claims)
- Role comparison logic

## Medium Priority

### 5. Tenant Quotas and Limits

**Future Consideration:** Rate limiting or resource quotas per tenant

**Possible Features:**
- Maximum transactions per tenant
- Storage limits
- API rate limiting per tenant
- Concurrent user limits

### 6. Tenant Invitation System

**Current State:** Out of scope for initial tenancy feature

**Requirements:**
- Email invitation workflow
- Invitation tokens with expiration
- Accept/decline invitation
- Pending invitation management

## Low Priority / Optional Enhancements

### 8. Bulk Tenant Operations

**Features:**
- Bulk user import/export
- Bulk role assignment
- Tenant template/cloning

### 9. Tenant Metadata and Settings

**Extensible Settings:**
- Custom properties per tenant
- Tenant-specific configuration
- Feature flags per tenant

### 10. Advanced Authorization

**Enhanced Permissions:**
- Granular permissions beyond Viewer/Editor/Owner
- Resource-specific permissions (e.g., can edit transactions but not budgets)
- Time-limited access grants
- Delegation/impersonation for support

### 11. Multi-Tenant Reporting

**Analytics:**
- Cross-tenant summaries (for users with multiple tenants)
- Tenant usage statistics
- Billing/subscription integration

## Testing Improvements

### Integration Test Additions

**Scenarios to Test:**
- Concurrent tenant operations
- Race conditions in role management
- Database constraint violations
- Long-running transactions across tenants

### Performance Testing

**Benchmarks:**
- Query performance with many tenants
- Authorization overhead measurement
- Claims generation performance
- Middleware pipeline timing

## Documentation Updates

### User Documentation

**End-User Guides:**
- How to create/manage workspaces
- Inviting users to workspaces
- Role explanations for non-technical users
- Switching between workspaces (UI flow)

### Developer Documentation

**Additional Topics:**
- Implementing new tenant-scoped features
- Testing patterns for tenant isolation
- Common pitfalls and best practices
- Performance optimization guidelines

## Architecture Considerations

### Future Library Extraction

If extracting tenancy into a reusable NuGet package:

**Two-Package Approach:**
1. **YourOrg.Tenancy.Abstractions** - Minimal interface (`ITenantModel`)
2. **YourOrg.Tenancy.EntityFramework** - Full implementation

**Migration Path:**
- Application entities depend only on abstractions
- Data layer depends on full package
- Minimal coupling to specific applications

### Database Migration Strategy

**For Existing Applications:**
- Migration script to add tenancy tables
- Default tenant creation for existing data
- User-to-tenant assignment script
- Backward compatibility considerations

## Microsoft Compliance Checklist

Track progress toward 100% Microsoft pattern compliance:

| Pattern/Practice | Status | Priority | Notes |
|------------------|--------|----------|-------|
| **Core Architecture** | | | |
| Shared DB, Shared Schema | ✅ Complete | - | Production-ready |
| Tenant Discriminator | ✅ Complete | - | `TenantId` with FK |
| Query Filtering | ✅ Complete | - | Single enforcement point |
| Foreign Keys & Indexes | ✅ Complete | - | Proper constraints |
| **Security & Authorization** | | | |
| Claims-Based RBAC | ✅ Complete | - | JWT with tenant roles |
| Enumeration Prevention | ✅ Complete | - | 403 for not-found/denied |
| Pipeline Ordering | ✅ Complete | - | Auth → Context → Business |
| Tenant Context | ✅ Complete | - | Scoped per request |
| **Lifecycle Management** | | | |
| Tenant Onboarding | ✅ Complete | - | Create endpoint |
| Tenant Deletion | ✅ Complete | - | Cascade delete |
| Soft Delete/Suspension | ⚠️ Partial | High | Properties scaffolded |
| Reactivation | ❌ Missing | High | Needs implementation |
| **Compliance & Audit** | | | |
| Audit Logging | ⚠️ Partial | High | Basic logging only |
| Tenant Access Tracking | ❌ Missing | High | Needs implementation |
| Data Export (GDPR) | ⚠️ Guidance | Medium | Document pattern |
| Data Deletion | ✅ Complete | - | Cascade delete |
| **Scalability** | | | |
| Abstraction for Scaling | ✅ Complete | - | Repository pattern |
| Scalability Documentation | ❌ Missing | High | Needs creation |
| Caching Patterns | ❌ Missing | Medium | Document for users |
| Resource Limits | ⚠️ N/A | - | Application concern |
| **Configuration** | | | |
| Per-Tenant Settings | ⚠️ Optional | Low | Future enhancement |

**Legend:**
- ✅ Complete - Fully implemented
- ⚠️ Partial - Partially implemented, needs completion
- ⚠️ N/A - Not applicable to library (application concern)
- ⚠️ Guidance - Document pattern for users
- ⚠️ Optional - Not critical for current scope
- ❌ Missing - Needs implementation

## Notes

- **Security:** Core isolation and authorization are production-ready
- **Maintainability:** Lightweight pattern is superior to original design
- **Performance:** Direct EF Core queries with proper indexes
- **Testing:** Comprehensive integration test coverage validates security

## References

- [Main Tenancy Documentation](../TENANCY.md)
- [ADR 0009: Multi-tenancy and Workspace Model](../adr/0009-accounts-and-tenancy.md)
- [Implementation Status Review](tenancy/TENANCY-IMPLEMENTATION-STATUS.md) (WIP, to be removed)
