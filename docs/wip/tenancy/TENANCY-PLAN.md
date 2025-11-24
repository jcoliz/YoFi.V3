# Multi-Tenancy Implementation Plan

Phased implementation plan for the multi-tenancy feature in YoFi.V3. This breaks the work into sequential phases with clear deliverables, starting with foundational entities and building up to full tenant management and data isolation.

## Phase 1: Foundation & Entities (Days 1-2)

1. **Create `ApplicationUser` entity** in `src/Entities` - Replace `IdentityUser` with custom class containing `TenantRoleAssignments` navigation property
2. **Create tenant entities** in `src/Entities` - Add `Tenant.cs`, `UserTenantRoleAssignment.cs`, `TenantRole.cs` enum, and `ITenantModel.cs` interface
3. **Update `ApplicationDbContext`** in `src/Data` - Switch to `IdentityDbContext<ApplicationUser>`, add `DbSet<Tenant>` and `DbSet<UserTenantRoleAssignment>`, configure relationships in `OnModelCreating`
4. **Generate EF migration** using `.\scripts\Add-Migration.ps1 -Name "AddTenantSupport"` - Creates database schema for tenants
5. **Test migration** - Verify database schema creation, foreign keys, and unique constraints work correctly

## Phase 2: Authorization Infrastructure (Days 3-4)

1. **Create authorization types** in `src/Controllers` or new `Authorization` folder - Implement `RequireTenantRoleAttribute`, `TenantRoleRequirement`, and `TenantRoleHandler` with HttpContext item storage
2. **Create `AddTenantPolicies` extension** in `src/Application` - Service collection extension registering `IAuthorizationHandler` and policies for each `TenantRole`
3. **Update `Program.cs`** in `src/BackEnd` - Call `AddTenantPolicies()`, add `HttpContextAccessor` registration
4. **Write unit tests** in `tests/Unit` - Test `TenantRoleHandler` with mock `HttpContext` and various claim scenarios

## Phase 3: Claims Provider (Days 5-6)

1. **Create `TenantClaimsProvider`** in `src/Application` - Implement `IUserClaimsProvider<ApplicationUser>` from NuxtIdentity, query `UserTenantRoleAssignment` table, format as `tenant_role` claims
2. **Register claims provider** in `src/BackEnd/Program.cs` - Add to NuxtIdentity configuration
3. **Test claim generation** - Verify JWT tokens contain correct `tenant_role` claims after login
4. **Handle edge cases** - Empty tenant lists, deactivated tenants, async database queries

## Phase 4: Tenant Data Provider (Days 7-8)

1. **Create `ITenantDataProvider` interface** in `src/Application` - Mirror `IDataProvider` but constrained to `ITenantModel` entities
2. **Implement `TenantDataProvider`** in `src/Data` - Read tenant context from `HttpContext.Items`, auto-filter by `TenantId` in `Get<TEntity>()`, auto-set `TenantId` in `Add()` and other mutation methods
3. **Register in DI container** - Scoped lifetime in `AddApplication` extension
4. **Add validation** - Throw exceptions if `TenantId` is null or mismatched, write unit tests for cross-tenant prevention

## Phase 5: Tenant Management Feature (Days 9-11)

1. **Create `TenantFeature` class** in `src/Application/Features` - Methods for `CreateTenant`, `DeactivateTenant`, `ReactivateTenant`, `AddUserRole`, `RemoveUserRole`, `DeleteTenant` with business rule validation
2. **Create `TenantController`** in `src/Controllers` - Routes at `/api/tenants` for listing accessible tenants, and `/api/tenant/{tenantId}/...` for tenant-specific operations with `[RequireTenantRole]` attributes
3. **Implement business rules** - Owner count checks, 1-week deletion waiting period, role hierarchy validation, duplicate name prevention per owner
4. **Add error handling** - 404 for no access, 403 for insufficient role, 400 for validation failures

## Phase 6: User Provisioning Integration (Days 12-13)

1. **Update user approval workflow** - Modify existing admin user approval process to auto-create personal tenant with Owner role assignment
2. **Add default tenant preference** - Store in browser localStorage or create `UserPreferences` table
3. **Handle missing tenant scenario** - Detect users with no tenants, display helpful message directing to administrator
4. **Test new user flow** - Verify account creation → email verification → admin approval → tenant creation → login works end-to-end

## Phase 7: Integration Testing (Days 14-15)

1. **Write tenant isolation tests** in `tests/Functional` - Create test with two tenants, attempt cross-tenant data access, verify 404 responses
2. **Write authorization tests** - Verify Viewer can't modify, Editor can modify, Owner can manage roles
3. **Write concurrent operation tests** - Test owner removal race conditions with parallel requests
4. **Add route vs body validation** - Attempt `POST /api/tenant/{tenantId}/transactions` with mismatched body `TenantId`

## Phase 8: Documentation & Deployment (Days 16-17)

1. **Update API documentation** - Document all tenant endpoints, claim format, authorization requirements
2. **Create migration guide** for existing users - Export from YoFi classic, import to YoFi.V3 (requires import feature implementation)
3. **Add monitoring/logging** - Log tenant operations, failed authorization attempts, tenant creation events
4. **Performance testing** - Verify query performance with indexes, consider caching if needed

## Further Considerations

1. **Decide on int vs Guid primary keys** - Current `IModel` uses `int`, design uses `Guid` for tenants. Options: (A) Make tenants use `int` for consistency, (B) Keep `Guid` and create separate `ITenantModel` hierarchy, (C) Migrate all entities to `Guid`. Recommend option B for flexibility.

2. **Add optimistic concurrency** - Include `RowVersion` on `UserTenantRoleAssignment` before production to prevent owner removal race conditions (currently in "Future Topics" but should be phase 5).

3. **Clarify ITenantDataProvider vs IDataProvider** - Design shows both interfaces. Decide if `TenantDataProvider` implements existing `IDataProvider` with runtime filtering or creates new `ITenantDataProvider` interface. Recommend new interface for type safety.

4. **Plan for claim size limits** - If users gain access to >10 tenants, JWT may exceed size limits. Consider pagination endpoint `/api/tenants/accessible` instead of relying solely on claims.

5. **Background job tenant context** - Design doesn't address how scheduled tasks access tenant data. Need service account pattern or explicit tenant context injection before implementing any background jobs.
