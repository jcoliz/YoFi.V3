# Tenancy Remaining Work

**Last Updated:** 2025-12-10
**Status:** Core tenancy features production-ready; enhancements and management features remain

## Overview

The core tenancy implementation (isolation, authorization, security) is complete and production-ready. Remaining work focuses on tenant management operations and optional enhancements.

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

### 4. Tenant Deactivation/Soft Delete

**Current State:** Deactivation properties commented out in [`Tenant`](../../src/Entities/Tenancy/Tenant.cs:30-34)

**When Implemented:**
```csharp
public bool IsActive { get; set; } = true;
public DateTimeOffset? DeactivatedAt { get; set; }
public string? DeactivatedByUserId { get; set; }
```

**Requirements:**
- Soft-delete behavior (inactive tenants return 403)
- Reactivation endpoint (only for inactive tenants)
- Index on `Tenant.IsActive` for performance
- Hard delete with safety checks (must be inactive for 1+ week)
- Only Owners can deactivate/reactivate
- Cannot deactivate if user's only tenant (prevent lock-out)

### 5. Tenant Quotas and Limits

**Future Consideration:** Rate limiting or resource quotas per tenant

**Possible Features:**
- Maximum transactions per tenant
- Storage limits
- API rate limiting per tenant
- Concurrent user limits

### 6. Audit Logging for Tenant Operations

**Track:**
- Tenant creation/deletion
- User role assignments/changes
- Tenant configuration changes
- Access attempts (successful and failed)

**Implementation:**
- Separate audit log table
- Log entries with timestamp, user, action, tenant
- Read-only audit API for Owners

### 7. Tenant Invitation System

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

## Notes

- **Security:** Core isolation and authorization are production-ready
- **Maintainability:** Lightweight pattern is superior to original design
- **Performance:** Direct EF Core queries with proper indexes
- **Testing:** Comprehensive integration test coverage validates security

## References

- [Main Tenancy Documentation](../TENANCY.md)
- [ADR 0009: Multi-tenancy and Workspace Model](../adr/0009-accounts-and-tenancy.md)
- [Implementation Status Review](tenancy/TENANCY-IMPLEMENTATION-STATUS.md) (WIP, to be removed)
